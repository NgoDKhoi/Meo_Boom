using UnityEngine;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

public class OnlineDrawManager : MonoBehaviour
{
    public static OnlineDrawManager Instance;
    private DatabaseReference roomRef;

    [Header("--- UI & Areas ---")]
    public TextMeshProUGUI turnStatusText;
    public Transform playerHandArea;
    public List<GameObject> cardPrefabs;

    [Header("--- Opponent Visuals ---")]
    public GameObject cardBackPrefab;
    public List<Transform> opponentAreas;

    [Header("--- Game Config ---")]
    public int cardsPerPlayer = 4;
    public float firebaseResponseTimeout = 3.5f;

    [Header("--- Game State ---")]
    public int currentTurnIndex = 0;
    public bool isHost = false;
    public bool isWaitingForDefuse = false;
    [HideInInspector] public bool isWaitingForFirebase = false;

    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_THRESHOLD = 0.3f;
    private Coroutine timeoutCoroutine;
    private bool isListeningToActions = false;

    void Awake() => Instance = this;

    void Start()
    {
        StartCoroutine(InitializeFirebaseConnection());
    }

    public bool IsMyTurn()
    {
        if (OnlineGameLogic.Instance != null)
        {
            return OnlineGameLogic.Instance.IsMyTurn();
        }

        if (RoomManager.Instance != null && RoomManager.Instance.currentRoomPlayers != null)
        {
            string currentTurnPlayer = RoomManager.Instance.currentRoomPlayers[currentTurnIndex % RoomManager.Instance.currentRoomPlayers.Count];
            return currentTurnPlayer == RoomManager.Instance.currentUsername;
        }

        return false;
    }

    private IEnumerator InitializeFirebaseConnection()
    {
        while (RoomManager.Instance == null || string.IsNullOrEmpty(RoomManager.Instance.currentRoomID))
            yield return new WaitForSeconds(0.5f);

        string roomID = RoomManager.Instance.currentRoomID;
        roomRef = FirebaseManager.Instance.Database.RootReference.Child("rooms").Child(roomID);

        // Đợi một chút để đảm bảo Firebase đã sẵn sàng trước khi lắng nghe
        yield return new WaitForSeconds(0.2f);

        List<string> players = RoomManager.Instance.currentRoomPlayers;
        if (players != null && players.Count > 0)
        {
            isHost = (RoomManager.Instance.currentUsername == players[0]);
        }

        // Đăng ký lắng nghe TRƯỚC khi Host chia bài
        ListenToGameState();
        ListenForVisualActions();

        if (isHost && DrawPileManager.Instance != null)
        {
            // Host đợi thêm 1 giây để các máy Client kịp vào Scene và đăng ký Listener
            yield return new WaitForSeconds(1.0f);
            StartCoroutine(HostStartGameSequence(players));
        }
    }

    private IEnumerator HostStartGameSequence(List<string> players)
    {
        // Xóa các actions cũ nếu có để tránh xung đột bài cũ
        yield return roomRef.Child("actions").RemoveValueAsync();

        DrawPileManager.Instance.PrepareSafeDeck(players.Count);

        foreach (string playerName in players)
        {
            // Tặng mỗi người 1 lá Defuse mặc định
            SendInitialConfirmedCard(playerName, DrawPileManager.CardType.Defuse.ToString());
            yield return new WaitForSeconds(0.2f);

            // Chia thêm số lá bài theo cấu hình
            for (int i = 0; i < cardsPerPlayer; i++)
            {
                DrawPileManager.CardType randomCard = DrawPileManager.Instance.DrawCardData();
                SendInitialConfirmedCard(playerName, randomCard.ToString());
                yield return new WaitForSeconds(0.15f); // Delay nhỏ để tránh spam Firebase
            }
        }

        DrawPileManager.Instance.AddExplodingKittens();
        UpdateDeckToFirebaseFromManager();

        roomRef.Child("gameData/currentTurnIndex").SetValueAsync(0);
        roomRef.Child("gameData/isWaitingForDefuse").SetValueAsync(false);
        Debug.Log("<color=cyan>Host đã chia bài xong cho tất cả người chơi.</color>");
    }

    private void SendInitialConfirmedCard(string receiver, string cardName)
    {
        Dictionary<string, object> result = new Dictionary<string, object>();
        result["type"] = "DRAW_CONFIRMED";
        result["target"] = receiver;
        result["cardType"] = cardName;
        result["timestamp"] = ServerValue.Timestamp; // Thêm timestamp để đảm bảo thứ tự
        roomRef.Child("actions").Push().SetValueAsync(result);
    }

    private void ListenForVisualActions()
    {
        if (isListeningToActions) return;
        isListeningToActions = true;

        roomRef.Child("actions").ChildAdded += (s, e) => {
            if (!e.Snapshot.Exists) return;
            var data = e.Snapshot.Value as Dictionary<string, object>;
            if (data == null) return;

            string type = data.ContainsKey("type") ? data["type"].ToString() : "";

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (type == "DRAW_CONFIRMED")
                {
                    string target = data.ContainsKey("target") ? data["target"].ToString() : "";
                    string cardName = data.ContainsKey("cardType") ? data["cardType"].ToString() : "";

                    if (target == RoomManager.Instance.currentUsername)
                    {
                        StopWaitingFirebase();
                        SpawnCardToHand(cardName);
                        Debug.Log($"<color=green>Đã nhận bài: {cardName}</color>");
                    }
                    else if (!string.IsNullOrEmpty(target))
                    {
                        // Hiển thị lá bài úp cho đối thủ
                        SpawnCardBackForOpponent(target);
                    }
                }
                else if (type == "BOMB_TRAPPED")
                {
                    if (data.ContainsKey("target") && data["target"].ToString() == RoomManager.Instance.currentUsername)
                    {
                        StopWaitingFirebase();
                        Debug.Log("<color=red>Dính bom!</color>");
                    }
                }
            });
        };
    }

    private void StopWaitingFirebase()
    {
        isWaitingForFirebase = false;
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }
    }

    private IEnumerator StartFirebaseTimeout()
    {
        yield return new WaitForSeconds(firebaseResponseTimeout);
        if (isWaitingForFirebase)
        {
            Debug.LogWarning("Firebase Timeout! Tự động giải phóng trạng thái chờ rút bài.");
            isWaitingForFirebase = false;
        }
        timeoutCoroutine = null;
    }

    public void UpdateDeckToFirebaseFromManager()
    {
        if (!isHost || DrawPileManager.Instance == null) return;

        List<DrawPileManager.CardType> currentDeck = DrawPileManager.Instance.GetTopCards(DrawPileManager.Instance.GetRemainingCount());
        List<string> deckStr = new List<string>();
        foreach (var card in currentDeck) deckStr.Add(card.ToString());

        roomRef.Child("gameData/drawPile").SetValueAsync(deckStr);
    }

    public void Host_InsertBombToFirebaseDeck()
    {
        if (!isHost) return;

        roomRef.Child("gameData/drawPile").GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsCompleted && task.Result.Exists)
            {
                List<object> list = task.Result.Value as List<object>;
                List<string> currentDeck = new List<string>();
                if (list != null) foreach (var item in list) currentDeck.Add(item.ToString());

                int randomIndex = UnityEngine.Random.Range(0, currentDeck.Count + 1);
                currentDeck.Insert(randomIndex, DrawPileManager.CardType.Explode.ToString());

                roomRef.Child("gameData/drawPile").SetValueAsync(currentDeck);
            }
        });
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            float timeDelta = Time.time - lastClickTime;
            if (timeDelta <= DOUBLE_CLICK_THRESHOLD) OnDeckDoubleClick();
            lastClickTime = Time.time;
        }
    }

    private void OnDeckDoubleClick()
    {
        if (!IsMyTurn()) return;

        if (isWaitingForFirebase || isWaitingForDefuse)
        {
            return;
        }

        if (EventSystem.current.IsPointerOverGameObject())
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (var r in results)
            {
                if (r.gameObject.name == "DrawPileDeck")
                {
                    isWaitingForFirebase = true;
                    if (timeoutCoroutine != null) StopCoroutine(timeoutCoroutine);
                    timeoutCoroutine = StartCoroutine(StartFirebaseTimeout());

                    if (OnlineGameActionManager.Instance != null)
                        OnlineGameActionManager.Instance.RequestDrawCard();
                    break;
                }
            }
        }
    }

    public void SpawnCardToHand(string cardName)
    {
        if (cardPrefabs == null || string.IsNullOrEmpty(cardName)) return;

        if (Enum.TryParse(cardName, out DrawPileManager.CardType type))
        {
            GameObject selected = null;
            foreach (var p in cardPrefabs)
            {
                if (p == null) continue;
                // Lưu ý: Đảm bảo Prefab bài của bạn có OnlineCardController hoặc CardController tương ứng
                var cc = p.GetComponent<OnlineCardController>();
                if (cc == null) cc = p.GetComponentInChildren<OnlineCardController>();

                if (cc != null && cc.cardType == type) { selected = p; break; }
            }

            if (selected != null && playerHandArea != null)
            {
                GameObject card = Instantiate(selected, playerHandArea);
                card.name = cardName + "_" + Guid.NewGuid().ToString().Substring(0, 4);
            }
        }
    }

    public void SpawnCardBackForOpponent(string opponentName)
    {
        if (cardBackPrefab == null || string.IsNullOrEmpty(opponentName)) return;

        Transform targetArea = GetOpponentArea(opponentName);
        if (targetArea != null)
        {
            Instantiate(cardBackPrefab, targetArea);
        }
    }

    public Transform GetOpponentArea(string opponentName)
    {
        if (RoomManager.Instance == null) return null;
        List<string> players = RoomManager.Instance.currentRoomPlayers;
        if (players == null) return null;

        int myIdx = players.IndexOf(RoomManager.Instance.currentUsername);
        int oppIdx = players.IndexOf(opponentName);
        if (myIdx == -1 || oppIdx == -1) return null;

        int relativePos = (oppIdx - myIdx + players.Count) % players.Count;
        int areaIndex = relativePos - 1;

        if (areaIndex >= 0 && areaIndex < opponentAreas.Count)
            return opponentAreas[areaIndex];
        return null;
    }

    private void ListenToGameState()
    {
        roomRef.Child("gameData/currentTurnIndex").ValueChanged += (s, e) => {
            if (!e.Snapshot.Exists) return;
            int newIndex = Convert.ToInt32(e.Snapshot.Value);
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                currentTurnIndex = newIndex;
            });
        };

        roomRef.Child("gameData/isWaitingForDefuse").ValueChanged += (s, e) => {
            if (e.Snapshot.Exists)
            {
                bool waiting = (bool)e.Snapshot.Value;
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    isWaitingForDefuse = waiting;
                    if (!waiting) StopWaitingFirebase();
                });
            }
        };
    }

    private void OnDestroy()
    {
        if (roomRef != null)
        {
            roomRef.Child("actions").ChildAdded -= (s, e) => { };
        }
    }
}