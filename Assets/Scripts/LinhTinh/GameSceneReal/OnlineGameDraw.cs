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
    private bool isWaitingForFirebase = false;

    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_THRESHOLD = 0.3f;

    void Awake() => Instance = this;

    void Start()
    {
        StartCoroutine(InitializeFirebaseConnection());
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

        if (isHost && DrawPileManager.Instance != null)
        {
            StartCoroutine(HostStartGameSequence(players));
        }

        ListenToGameState();
        ListenToActions();
    }

    private System.Collections.IEnumerator HostStartGameSequence(List<string> players)
    {
        Debug.Log("<color=green>[Host]</color> Đang chuẩn bị bộ bài an toàn...");
        DrawPileManager.Instance.PrepareSafeDeck(players.Count);

        Debug.Log("<color=green>[Host]</color> Đang chia bài cho người chơi...");
        foreach (string playerName in players)
        {
            SendConfirmedCard(playerName, DrawPileManager.CardType.Defuse.ToString());
            yield return new WaitForSeconds(0.2f);

            for (int i = 0; i < cardsPerPlayer; i++)
            {
                DrawPileManager.CardType randomCard = DrawPileManager.Instance.DrawCardData();
                SendConfirmedCard(playerName, randomCard.ToString());
                yield return new WaitForSeconds(0.1f);
            }
        }

        DrawPileManager.Instance.AddExplodingKittens();
        Debug.Log("<color=red>[Host]</color> Đã thêm Bom. Trận đấu bắt đầu!");

        UpdateDeckToFirebaseFromManager();
        roomRef.Child("gameData/currentTurnIndex").SetValueAsync(0);
    }

    private void SendConfirmedCard(string receiver, string cardName)
    {
        Dictionary<string, object> result = new Dictionary<string, object>();
        result["type"] = "DRAW_CONFIRMED";
        result["receiver"] = receiver;
        result["cardType"] = cardName;
        roomRef.Child("actions").Push().SetValueAsync(result);
    }

    public void UpdateDeckToFirebaseFromManager()
    {
        if (!isHost || DrawPileManager.Instance == null) return;

        List<DrawPileManager.CardType> currentDeck = DrawPileManager.Instance.GetTopCards(DrawPileManager.Instance.GetRemainingCount());
        List<string> deckStr = new List<string>();
        foreach (var card in currentDeck)
        {
            deckStr.Add(card.ToString());
        }

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
        if (!IsMyTurn() || isWaitingForFirebase) return;

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
                    SendDrawRequest();
                    break;
                }
            }
        }
    }

    private void SendDrawRequest()
    {
        Dictionary<string, object> action = new Dictionary<string, object>();
        action["type"] = "DRAW_REQUEST";
        action["sender"] = RoomManager.Instance.currentUsername;
        roomRef.Child("actions").Push().SetValueAsync(action);
    }

    private void ListenToActions()
    {
        roomRef.Child("actions").ChildAdded += (s, e) => {
            if (!e.Snapshot.Exists) return;
            var data = e.Snapshot.Value as Dictionary<string, object>;
            if (data == null) return;

            // --- SỬA LỖI Ở ĐÂY: KIỂM TRA KEY TRƯỚC KHI TRUY XUẤT ---
            string type = data.ContainsKey("type") ? data["type"].ToString() : "";

            if (type == "DRAW_REQUEST" && isHost)
            {
                if (data.ContainsKey("sender"))
                {
                    string sender = data["sender"].ToString();
                    UnityMainThreadDispatcher.Instance().Enqueue(() => {
                        ProcessDrawRequestByHost(sender);
                    });
                }
            }
            else if (type == "DRAW_CONFIRMED")
            {
                string receiver = data.ContainsKey("receiver") ? data["receiver"].ToString() : "";
                string cardName = data.ContainsKey("cardType") ? data["cardType"].ToString() : "";

                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    if (receiver == RoomManager.Instance.currentUsername)
                    {
                        isWaitingForFirebase = false;
                        SpawnCardToHand(cardName);
                    }
                    else if (!string.IsNullOrEmpty(receiver))
                    {
                        SpawnCardBackForOpponent(receiver);
                    }
                });
            }
        };
    }

    private void ProcessDrawRequestByHost(string senderName)
    {
        if (DrawPileManager.Instance == null || !isHost) return;

        DrawPileManager.CardType drawnCard = DrawPileManager.Instance.DrawCardData();
        SendConfirmedCard(senderName, drawnCard.ToString());
        UpdateDeckToFirebaseFromManager();

        int total = RoomManager.Instance.currentRoomPlayers.Count;
        int nextIndex = (currentTurnIndex + 1) % total;
        roomRef.Child("gameData/currentTurnIndex").SetValueAsync(nextIndex);
    }

    private void SpawnCardToHand(string cardName)
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

    private void SpawnCardBackForOpponent(string opponentName)
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
                UpdateTurnUI();
            });
        };
    }

    private void UpdateTurnUI()
    {
        if (RoomManager.Instance.currentRoomPlayers == null || RoomManager.Instance.currentRoomPlayers.Count == 0) return;

        string activePlayer = RoomManager.Instance.currentRoomPlayers[currentTurnIndex % RoomManager.Instance.currentRoomPlayers.Count];
        bool myTurn = (activePlayer == RoomManager.Instance.currentUsername);

        if (turnStatusText != null)
            turnStatusText.text = myTurn ? "<color=yellow>LƯỢT CỦA BẠN</color>" : $"Lượt của: {activePlayer}";
    }

    public bool IsMyTurn()
    {
        if (RoomManager.Instance.currentRoomPlayers == null || currentTurnIndex >= RoomManager.Instance.currentRoomPlayers.Count)
            return false;
        return RoomManager.Instance.currentUsername == RoomManager.Instance.currentRoomPlayers[currentTurnIndex];
    }
}