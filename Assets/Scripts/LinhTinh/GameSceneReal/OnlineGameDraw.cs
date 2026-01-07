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
    public float firebaseResponseTimeout = 5.0f;

    [Header("--- Game State ---")]
    public int currentTurnIndex = 0;
    public bool isHost = false;
    public bool isWaitingForDefuse = false;
    [HideInInspector] public bool isWaitingForFirebase = false;

    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_THRESHOLD = 0.3f;
    private Coroutine timeoutCoroutine;
    private bool isListeningToActions = false;

    // Handshake variables
    private bool hasReportedReady = false;

    // Firebase Event Handlers (Dùng để Unsubscribe)
    private EventHandler<ValueChangedEventArgs> turnIndexHandler;
    private EventHandler<ValueChangedEventArgs> defuseStatusHandler;
    private EventHandler<ChildChangedEventArgs> actionAddedHandler;

    void Awake() => Instance = this;

    void Start()
    {
        StartCoroutine(InitializeFirebaseConnection());
    }

    public bool IsMyTurn()
    {
        if (OnlineGameLogic.Instance != null) return OnlineGameLogic.Instance.IsMyTurn();
        return false;
    }

    private IEnumerator InitializeFirebaseConnection()
    {
        while (RoomManager.Instance == null || string.IsNullOrEmpty(RoomManager.Instance.currentRoomID))
            yield return new WaitForSeconds(0.5f);

        string roomID = RoomManager.Instance.currentRoomID;
        roomRef = FirebaseManager.Instance.Database.RootReference.Child("rooms").Child(roomID);

        // Xác định Host (Người chơi đầu tiên trong danh sách)
        List<string> players = RoomManager.Instance.currentRoomPlayers;
        if (players != null && players.Count > 0)
        {
            isHost = (RoomManager.Instance.currentUsername == players[0]);
        }

        // Đăng ký lắng nghe các sự kiện game
        ListenToGameState();
        ListenForVisualActions();

        yield return new WaitForSeconds(0.5f);

        // Báo cáo đã load xong scene và sẵn sàng nhận bài
        ReportReady();

        if (isHost)
        {
            StartCoroutine(HostWaitForReadyAndStart(players));
        }
    }

    private void ReportReady()
    {
        if (hasReportedReady || roomRef == null) return;
        string myName = RoomManager.Instance.currentUsername;

        roomRef.Child("readyStatus").Child(myName).SetValueAsync(true).ContinueWithOnMainThread(t => {
            hasReportedReady = true;
            Debug.Log($"<color=green>[Handshake] {myName} đã sẵn sàng!</color>");
        });
    }

    private IEnumerator HostWaitForReadyAndStart(List<string> players)
    {
        bool allReady = false;
        while (!allReady)
        {
            var task = roomRef.Child("readyStatus").GetValueAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsCompletedSuccessfully)
            {
                DataSnapshot snapshot = task.Result;
                int readyCount = 0;
                foreach (string p in players)
                {
                    if (snapshot.HasChild(p) && (bool)snapshot.Child(p).Value == true)
                        readyCount++;
                }

                if (readyCount >= players.Count) allReady = true;
                else yield return new WaitForSeconds(1.0f);
            }
            else yield return new WaitForSeconds(1.0f);
        }

        yield return StartCoroutine(HostStartGameSequence(players));
    }

    private IEnumerator HostStartGameSequence(List<string> players)
    {
        // 1. Reset toàn bộ dữ liệu game cũ
        yield return roomRef.Child("actions").RemoveValueAsync();
        yield return roomRef.Child("playersStatus").RemoveValueAsync(); // THÊM MỚI: Reset trạng thái sống/chết

        if (DrawPileManager.Instance == null) yield break;

        // 2. Chuẩn bị bộ bài sạch (chưa có bom)
        DrawPileManager.Instance.PrepareSafeDeck(players.Count);

        // 3. Chia bài cho từng người qua Firebase Action
        foreach (string playerName in players)
        {
            // Reset trạng thái sống cho từng người chơi
            roomRef.Child("playersStatus").Child(playerName).Child("isDead").SetValueAsync(false);

            // Chia Defuse
            SendInitialConfirmedCard(playerName, DrawPileManager.CardType.Defuse.ToString());
            yield return new WaitForSeconds(0.2f);

            // Chia bài ngẫu nhiên
            for (int i = 0; i < cardsPerPlayer; i++)
            {
                DrawPileManager.CardType randomCard = DrawPileManager.Instance.DrawCardData();
                SendInitialConfirmedCard(playerName, randomCard.ToString());
                yield return new WaitForSeconds(0.15f);
            }
        }

        // 4. Thêm bom vào bộ bài và đồng bộ lên Firebase
        DrawPileManager.Instance.AddExplodingKittens();
        UpdateDeckToFirebaseFromManager();

        // 5. Thiết lập trạng thái bắt đầu
        roomRef.Child("gameData/currentTurnIndex").SetValueAsync(0);
        roomRef.Child("gameData/isWaitingForDefuse").SetValueAsync(false);
        roomRef.Child("gameData/turnsToDraw").SetValueAsync(1);

        // 6. Tín hiệu bắt đầu game
        roomRef.Child("actions").Push().Child("type").SetValueAsync("GAME_START_SIGNAL");
    }


    public void UpdateDeckToFirebaseFromManager()
    {
        if (!isHost || DrawPileManager.Instance == null) return;
        List<DrawPileManager.CardType> currentDeck = DrawPileManager.Instance.GetFullDeckList();
        List<string> deckStr = new List<string>();
        foreach (var card in currentDeck) deckStr.Add(card.ToString());
        roomRef.Child("gameData/drawPile").SetValueAsync(deckStr);
    }

    public void Host_InsertBombToFirebaseDeck()
    {
        if (!isHost) return;

        // Lấy bộ bài hiện tại từ Firebase để đảm bảo tính nhất quán cao nhất
        roomRef.Child("gameData/drawPile").GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsCompleted && task.Result.Exists)
            {
                List<object> list = task.Result.Value as List<object>;
                List<string> currentDeck = new List<string>();
                if (list != null) foreach (var item in list) currentDeck.Add(item.ToString());

                // Chèn bom vào vị trí ngẫu nhiên
                int randomIndex = UnityEngine.Random.Range(0, currentDeck.Count + 1);
                currentDeck.Insert(randomIndex, DrawPileManager.CardType.Explode.ToString());

                // Cập nhật lại lên Firebase
                roomRef.Child("gameData/drawPile").SetValueAsync(currentDeck).ContinueWithOnMainThread(t => {
                    // Sau khi cập nhật Deck, cập nhật luôn bộ bài của Host cục bộ để đồng bộ
                    if (DrawPileManager.Instance != null)
                    {
                        List<DrawPileManager.CardType> newDeckTypes = new List<DrawPileManager.CardType>();
                        foreach (string s in currentDeck)
                        {
                            if (Enum.TryParse(s, out DrawPileManager.CardType type))
                                newDeckTypes.Add(type);
                        }
                        DrawPileManager.Instance.SyncDeck(newDeckTypes);
                    }
                    Debug.Log($"[Draw] Host đã nhét lại BOM vào vị trí: {randomIndex}");
                });
            }
        });
    }

    private void SendInitialConfirmedCard(string receiver, string cardName)
    {
        Dictionary<string, object> result = new Dictionary<string, object>
        {
            ["type"] = "DRAW_CONFIRMED",
            ["target"] = receiver,
            ["cardType"] = cardName,
            ["timestamp"] = ServerValue.Timestamp
        };
        roomRef.Child("actions").Push().SetValueAsync(result);
    }

    private void ListenForVisualActions()
    {
        if (isListeningToActions) return;
        isListeningToActions = true;

        actionAddedHandler = (s, e) => {
            if (!e.Snapshot.Exists) return;
            var data = e.Snapshot.Value as Dictionary<string, object>;
            if (data == null) return;

            string type = data.ContainsKey("type") ? data["type"].ToString() : "";

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (type == "DRAW_CONFIRMED")
                {
                    string target = data.ContainsKey("target") ? data["target"].ToString() : "";
                    string cardName = data.ContainsKey("cardType") ? data["cardType"].ToString() : "";

                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlaySFX(AudioManager.Instance.drawCardSound);

                    if (target == RoomManager.Instance.currentUsername)
                    {
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
                        StopWaitingFirebase();
                }
                else if (type == "GAME_START_SIGNAL")
                {
                    UpdateTurnStatusUI();
                }
            });
        };
        roomRef.Child("actions").ChildAdded += actionAddedHandler;
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
        isWaitingForFirebase = false;
        timeoutCoroutine = null;
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
        if (!IsMyTurn() || isWaitingForFirebase || isWaitingForDefuse) return;

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
                var cc = p.GetComponent<OnlineCardController>() ?? p.GetComponentInChildren<OnlineCardController>();
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
        if (targetArea != null) Instantiate(cardBackPrefab, targetArea);
    }

    public Transform GetOpponentArea(string opponentName)
    {
        if (RoomManager.Instance == null) return null;
        List<string> players = RoomManager.Instance.currentRoomPlayers;
        if (players == null || players.Count == 0) return null;

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
        turnIndexHandler = (s, e) => {
            if (!e.Snapshot.Exists) return;
            int newIndex = Convert.ToInt32(e.Snapshot.Value);
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                currentTurnIndex = newIndex;
                UpdateTurnStatusUI();
            });
        };
        roomRef.Child("gameData/currentTurnIndex").ValueChanged += turnIndexHandler;

        defuseStatusHandler = (s, e) => {
            if (e.Snapshot.Exists)
            {
                bool waiting = (bool)e.Snapshot.Value;
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    isWaitingForDefuse = waiting;
                    UpdateTurnStatusUI(); // Cập nhật màu sắc UI khi gỡ bom
                    if (!waiting) StopWaitingFirebase();
                });
            }
        };
        roomRef.Child("gameData/isWaitingForDefuse").ValueChanged += defuseStatusHandler;
    }

    private void UpdateTurnStatusUI()
    {
        if (turnStatusText == null || RoomManager.Instance == null) return;

        List<string> players = RoomManager.Instance.currentRoomPlayers;
        if (players == null || players.Count == 0 || currentTurnIndex < 0) return;

        string activePlayer = players[currentTurnIndex % players.Count];
        bool isMe = (activePlayer == RoomManager.Instance.currentUsername);

        // Kiểm tra xem người đang tới lượt có phải người đã chết không (để UI chính xác hơn)
        bool isActivePlayerDead = false;
        if (OnlineGameLogic.Instance != null && OnlineGameLogic.Instance.playerLifeStatus.ContainsKey(activePlayer))
        {
            isActivePlayerDead = !OnlineGameLogic.Instance.playerLifeStatus[activePlayer];
        }

        if (isActivePlayerDead)
        {
            turnStatusText.text = $"Đang chuyển lượt từ {activePlayer}...";
            turnStatusText.color = Color.gray;
            return;
        }

        if (isWaitingForDefuse)
        {
            turnStatusText.text = isMe ? "<color=red> BẠN ĐANG GỠ BOM! </color>" : $"<color=orange>{activePlayer} đang gỡ bom...</color>";
        }
        else
        {
            if (isMe)
            {
                turnStatusText.text = "Lượt của bạn!";
                turnStatusText.color = Color.yellow;
            }
            else
            {
                turnStatusText.text = "Lượt của: " + activePlayer;
                turnStatusText.color = Color.white;
            }
        }
    }

    private void OnDestroy()
    {
        if (roomRef != null)
        {
            roomRef.Child("gameData/currentTurnIndex").ValueChanged -= turnIndexHandler;
            roomRef.Child("gameData/isWaitingForDefuse").ValueChanged -= defuseStatusHandler;
            roomRef.Child("actions").ChildAdded -= actionAddedHandler;

            if (RoomManager.Instance != null)
                roomRef.Child("readyStatus").Child(RoomManager.Instance.currentUsername).RemoveValueAsync();
        }
    }
}