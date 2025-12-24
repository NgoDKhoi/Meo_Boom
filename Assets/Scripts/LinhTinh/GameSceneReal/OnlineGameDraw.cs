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
    public float firebaseResponseTimeout = 3.5f; // Thời gian tối đa chờ Firebase phản hồi

    [Header("--- Game State ---")]
    public int currentTurnIndex = 0;
    public bool isHost = false;
    public bool isWaitingForDefuse = false;
    [HideInInspector] public bool isWaitingForFirebase = false;

    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_THRESHOLD = 0.3f;
    private Coroutine timeoutCoroutine;

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

        List<string> players = RoomManager.Instance.currentRoomPlayers;
        if (players != null && players.Count > 0)
        {
            isHost = (RoomManager.Instance.currentUsername == players[0]);
        }

        if (isHost && DrawPileManager.Instance != null)
        {
            StartCoroutine(HostStartGameSequence(players));
        }

        ListenToGameState();
        ListenForVisualActions();
    }

    private IEnumerator HostStartGameSequence(List<string> players)
    {
        DrawPileManager.Instance.PrepareSafeDeck(players.Count);

        foreach (string playerName in players)
        {
            SendInitialConfirmedCard(playerName, DrawPileManager.CardType.Defuse.ToString());
            yield return new WaitForSeconds(0.2f);

            for (int i = 0; i < cardsPerPlayer; i++)
            {
                DrawPileManager.CardType randomCard = DrawPileManager.Instance.DrawCardData();
                SendInitialConfirmedCard(playerName, randomCard.ToString());
                yield return new WaitForSeconds(0.15f);
            }
        }

        DrawPileManager.Instance.AddExplodingKittens();
        UpdateDeckToFirebaseFromManager();
        roomRef.Child("gameData/currentTurnIndex").SetValueAsync(0);
        roomRef.Child("gameData/isWaitingForDefuse").SetValueAsync(false);
    }

    private void SendInitialConfirmedCard(string receiver, string cardName)
    {
        Dictionary<string, object> result = new Dictionary<string, object>();
        result["type"] = "DRAW_CONFIRMED";
        result["target"] = receiver;
        result["cardType"] = cardName;
        roomRef.Child("actions").Push().SetValueAsync(result);
    }

    private void ListenForVisualActions()
    {
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
                        // Thành công: Hủy timeout và mở khóa
                        StopWaitingFirebase();
                        SpawnCardToHand(cardName);
                    }
                    else if (!string.IsNullOrEmpty(target))
                    {
                        SpawnCardBackForOpponent(target);
                    }
                }
                else if (type == "BOMB_TRAPPED")
                {
                    if (data.ContainsKey("target") && data["target"].ToString() == RoomManager.Instance.currentUsername)
                    {
                        // Dính bom: Hủy timeout và mở khóa để xử lý gỡ bom
                        StopWaitingFirebase();
                        Debug.Log("Đã dính bom, mở khóa trạng thái chờ Firebase.");
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
            Debug.Log($"Không thể rút: WaitingFirebase={isWaitingForFirebase}, WaitingDefuse={isWaitingForDefuse}");
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

                    // Kích hoạt cơ chế chống kẹt (Timeout)
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
                CardController cc = p.GetComponent<CardController>();
                if (cc != null && cc.cardType == type) { selected = p; break; }
            }

            if (selected != null && playerHandArea != null)
            {
                Instantiate(selected, playerHandArea);
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
                    // Khi trạng thái gỡ bom kết thúc, đảm bảo các biến chờ cũng được reset
                    if (!waiting) StopWaitingFirebase();
                });
            }
        };
    }
}