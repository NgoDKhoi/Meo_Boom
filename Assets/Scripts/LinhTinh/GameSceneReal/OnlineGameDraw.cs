using UnityEngine;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine.EventSystems;

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

    [Header("--- Game State ---")]
    public int currentTurnIndex = 0;
    public bool isHost = false;
    [HideInInspector] public bool isWaitingForFirebase = false;

    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_THRESHOLD = 0.3f;

    void Awake() => Instance = this;

    void Start()
    {
        StartCoroutine(InitializeFirebaseConnection());
    }

    // ================================================================
    // HÀM BỔ SUNG ĐỂ CARDCONTROLLER KHÔNG BỊ LỖI
    // ================================================================
    public bool IsMyTurn()
    {
        // Hỏi OnlineGameLogic xem có đúng lượt của mình không
        if (OnlineGameLogic.Instance != null)
        {
            return OnlineGameLogic.Instance.IsMyTurn();
        }

        // Fallback: Nếu không có Logic Manager, kiểm tra dựa trên TurnIndex đơn giản
        if (RoomManager.Instance != null && RoomManager.Instance.currentRoomPlayers != null)
        {
            string currentTurnPlayer = RoomManager.Instance.currentRoomPlayers[currentTurnIndex % RoomManager.Instance.currentRoomPlayers.Count];
            return currentTurnPlayer == RoomManager.Instance.currentUsername;
        }

        return false;
    }

    private System.Collections.IEnumerator InitializeFirebaseConnection()
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

        // CHỈ HOST: Thực hiện chuỗi khởi tạo bài đầu trận (Chia bài an toàn)
        if (isHost && DrawPileManager.Instance != null)
        {
            StartCoroutine(HostStartGameSequence(players));
        }

        ListenToGameState();
        // QUAN TRỌNG: Lắng nghe các lệnh hiển thị để tự động Spawn bài cho mình và đối thủ
        ListenForVisualActions();
    }

    private System.Collections.IEnumerator HostStartGameSequence(List<string> players)
    {
        // 1. Lọc sạch bom khỏi Deck để chia bài an toàn ban đầu
        DrawPileManager.Instance.PrepareSafeDeck(players.Count);

        // 2. Chia bài đầu trận cho từng người
        foreach (string playerName in players)
        {
            // Tặng 1 lá Defuse cố định cho mỗi người
            SendInitialConfirmedCard(playerName, DrawPileManager.CardType.Defuse.ToString());
            yield return new WaitForSeconds(0.2f);

            // Chia thêm số lượng bài ngẫu nhiên theo cấu hình (chắc chắn không có bom)
            for (int i = 0; i < cardsPerPlayer; i++)
            {
                DrawPileManager.CardType randomCard = DrawPileManager.Instance.DrawCardData();
                SendInitialConfirmedCard(playerName, randomCard.ToString());
                yield return new WaitForSeconds(0.15f);
            }
        }

        // 3. Sau khi chia xong, mới thêm các lá Bom (Exploding Kittens) vào bộ bài
        DrawPileManager.Instance.AddExplodingKittens();

        // 4. Đồng bộ bộ bài chính thức lên Firebase và đặt lượt đầu tiên
        UpdateDeckToFirebaseFromManager();
        roomRef.Child("gameData/currentTurnIndex").SetValueAsync(0);
    }

    // Hàm bổ trợ gửi lệnh xác nhận bài trực tiếp vào node actions (Dùng cho cả lúc chia bài và rút bài)
    private void SendInitialConfirmedCard(string receiver, string cardName)
    {
        Dictionary<string, object> result = new Dictionary<string, object>();
        result["type"] = "DRAW_CONFIRMED";
        result["target"] = receiver;
        result["cardType"] = cardName;
        roomRef.Child("actions").Push().SetValueAsync(result);
    }

    // Lắng nghe node 'actions' để thực hiện hiển thị bài (Visuals Only)
    private void ListenForVisualActions()
    {
        roomRef.Child("actions").ChildAdded += (s, e) => {
            if (!e.Snapshot.Exists) return;
            var data = e.Snapshot.Value as Dictionary<string, object>;
            if (data == null) return;

            string type = data.ContainsKey("type") ? data["type"].ToString() : "";

            if (type == "DRAW_CONFIRMED")
            {
                string target = data.ContainsKey("target") ? data["target"].ToString() : "";
                string cardName = data.ContainsKey("cardType") ? data["cardType"].ToString() : "";

                // Thực thi trên Main Thread của Unity
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    if (target == RoomManager.Instance.currentUsername)
                    {
                        // Nếu là bài của mình: Spawn lá bài thật
                        isWaitingForFirebase = false;
                        SpawnCardToHand(cardName);
                    }
                    else if (!string.IsNullOrEmpty(target))
                    {
                        // Nếu là bài của đối thủ: Spawn mặt sau lá bài (Card Back)
                        SpawnCardBackForOpponent(target);
                    }
                });
            }
        };
    }

    public void UpdateDeckToFirebaseFromManager()
    {
        if (!isHost || DrawPileManager.Instance == null) return;

        List<DrawPileManager.CardType> currentDeck = DrawPileManager.Instance.GetTopCards(DrawPileManager.Instance.GetRemainingCount());
        List<string> deckStr = new List<string>();
        foreach (var card in currentDeck) deckStr.Add(card.ToString());

        roomRef.Child("gameData/drawPile").SetValueAsync(deckStr);
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
        // Kiểm tra lượt chơi sử dụng hàm mới tạo
        if (!IsMyTurn()) return;
        if (isWaitingForFirebase) return;

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
                    // Yêu cầu rút bài thông qua Action Manager
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
    }

}

