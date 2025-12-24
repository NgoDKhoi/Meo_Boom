using UnityEngine;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System;
using UnityEngine.UI;

public class OnlineGameActionManager : MonoBehaviour
{
    public static OnlineGameActionManager Instance;
    private DatabaseReference roomRef;

    [Header("--- UI REFERENCES ---")]
    public GameObject seeFuturePanel;
    public Image[] futureCardSlots;
    public Image discardPileDisplay;
    public List<CardVisualData> cardVisuals;
    public Text bombTimerText; // Kéo thả Text UI hiển thị đếm ngược (Vd: "Bom nổ sau: 5s")

    [Header("--- TRẠNG THÁI VÒNG CHƠI ---")]
    public int turnsToDraw = 1;
    public bool isWaitingForDefuse = false;
    private string roomID;
    private bool isLocalProcessing = false;

    private Coroutine bombCountdownCoroutine; // Lưu Coroutine để hủy khi cần

    [Serializable]
    public struct CardVisualData
    {
        public DrawPileManager.CardType type;
        public Sprite cardSprite;
    }

    void Awake() => Instance = this;

    void Start()
    {
        if (seeFuturePanel != null) seeFuturePanel.SetActive(false);
        if (discardPileDisplay != null) discardPileDisplay.gameObject.SetActive(false);
        if (bombTimerText != null) bombTimerText.gameObject.SetActive(false);
        StartCoroutine(InitializeFirebase());
    }

    private System.Collections.IEnumerator InitializeFirebase()
    {
        while (RoomManager.Instance == null || string.IsNullOrEmpty(RoomManager.Instance.currentRoomID))
            yield return new WaitForSeconds(0.5f);

        roomID = RoomManager.Instance.currentRoomID;
        roomRef = FirebaseManager.Instance.Database.RootReference.Child("rooms").Child(roomID);

        ListenForGameActions();
        ListenForTurnData();
        ListenForDefuseStatus();
    }

    private bool CheckIfItIsMyTurn()
    {
        if (OnlineDrawManager.Instance == null || RoomManager.Instance == null) return false;
        List<string> players = RoomManager.Instance.currentRoomPlayers;
        string myName = RoomManager.Instance.currentUsername;
        int currentTurnIndex = OnlineDrawManager.Instance.currentTurnIndex;
        if (players == null || currentTurnIndex < 0 || currentTurnIndex >= players.Count) return false;
        return players[currentTurnIndex] == myName;
    }

    // ================================================================
    // PHẦN 1: GỬI LỆNH TỪ NGƯỜI CHƠI
    // ================================================================

    public void RequestPlayCard(DrawPileManager.CardType cardType, string cardObjectID)
    {
        if (!CheckIfItIsMyTurn() || isLocalProcessing) return;

        if (isWaitingForDefuse && cardType != DrawPileManager.CardType.Defuse)
        {
            Debug.Log("<color=orange>BẠN ĐANG DÍNH BOM! CHỈ CÓ THỂ ĐÁNH DEFUSE!</color>");
            return;
        }

        // Nếu đánh Defuse thành công, dừng đếm ngược ngay lập tức
        if (cardType == DrawPileManager.CardType.Defuse && bombCountdownCoroutine != null)
        {
            StopCoroutine(bombCountdownCoroutine);
            if (bombTimerText != null) bombTimerText.gameObject.SetActive(false);
        }

        Dictionary<string, object> action = new Dictionary<string, object>();
        action["type"] = "PLAY_ACTION";
        action["sender"] = RoomManager.Instance.currentUsername;
        action["cardType"] = cardType.ToString();
        action["cardID"] = cardObjectID;

        roomRef.Child("actions").Push().SetValueAsync(action);
    }

    public void RequestDrawCard()
    {
        if (!CheckIfItIsMyTurn() || isLocalProcessing) return;

        if (isWaitingForDefuse)
        {
            Debug.LogWarning("Phải gỡ bom trước khi rút bài!");
            return;
        }

        isLocalProcessing = true;
        Dictionary<string, object> action = new Dictionary<string, object>();
        action["type"] = "DRAW_REQUEST";
        action["sender"] = RoomManager.Instance.currentUsername;

        roomRef.Child("actions").Push().SetValueAsync(action).ContinueWithOnMainThread(t => {
            isLocalProcessing = false;
        });
    }

    public void RequestExplode()
    {
        // Chỉ gửi yêu cầu nổ nếu thực sự đang trong trạng thái chờ Defuse
        if (!isWaitingForDefuse) return;

        if (bombTimerText != null) bombTimerText.gameObject.SetActive(false);

        Dictionary<string, object> action = new Dictionary<string, object>();
        action["type"] = "PLAYER_EXPLODED";
        action["sender"] = RoomManager.Instance.currentUsername;
        roomRef.Child("actions").Push().SetValueAsync(action);
    }

    // ================================================================
    // PHẦN 2: LẮNG NGHE VÀ XỬ LÝ
    // ================================================================

    private void ListenForGameActions()
    {
        roomRef.Child("actions").ChildAdded += (s, e) => {
            if (!e.Snapshot.Exists) return;
            var data = e.Snapshot.Value as Dictionary<string, object>;
            if (data == null || !data.ContainsKey("type")) return;

            string type = data["type"].ToString();
            string sender = data.ContainsKey("sender") ? data["sender"].ToString() : "";

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                HandleIncomingAction(type, sender, data, e.Snapshot);
            });
        };
    }

    private void HandleIncomingAction(string type, string sender, Dictionary<string, object> data, DataSnapshot snapshot)
    {
        switch (type)
        {
            case "PLAY_ACTION":
                if (Enum.TryParse(data["cardType"].ToString(), out DrawPileManager.CardType cardType))
                {
                    UpdateDiscardPileVisual(cardType);
                    ShowCardPlayedInDiscardPile(cardType);
                    if (OnlineDrawManager.Instance.isHost)
                        ExecuteCardLogic(cardType, sender);
                }
                break;

            case "DRAW_REQUEST":
                if (OnlineDrawManager.Instance.isHost)
                    Host_ProcessDraw(sender);
                break;

            case "BOMB_TRAPPED":
                if (data["target"].ToString() == RoomManager.Instance.currentUsername)
                {
                    Debug.Log("<color=red>BẠN DÍNH BOM! HÃY DÙNG LÁ DEFUSE TRONG 5 GIÂY!</color>");
                    // Bắt đầu đếm ngược 5 giây cho local player
                    if (bombCountdownCoroutine != null) StopCoroutine(bombCountdownCoroutine);
                    bombCountdownCoroutine = StartCoroutine(BombCountdownTimer(5f));
                }
                break;

            case "PLAYER_EXPLODED":
                Debug.Log($"<color=black>{sender} đã nổ tung!</color>");
                if (OnlineDrawManager.Instance.isHost) Host_HandlePlayerExploded(sender);
                break;

            case "FUTURE_DATA":
                if (data["receiver"].ToString() == RoomManager.Instance.currentUsername)
                {
                    string[] cards = data["data"].ToString().Split(',');
                    ShowSeeFutureUI(cards);
                }
                break;
        }
        if (OnlineDrawManager.Instance.isHost) snapshot.Reference.RemoveValueAsync();
    }

    private System.Collections.IEnumerator BombCountdownTimer(float duration)
    {
        float remaining = duration;
        if (bombTimerText != null) bombTimerText.gameObject.SetActive(true);

        while (remaining > 0)
        {
            if (bombTimerText != null) bombTimerText.text = $"BẠN DÍNH BOM! GỠ TRONG: {Mathf.CeilToInt(remaining)}s";
            yield return new WaitForSeconds(1f);
            remaining -= 1f;

            // Nếu trạng thái đã được gỡ (bởi lá bài Defuse) thì thoát
            if (!isWaitingForDefuse) break;
        }

        if (isWaitingForDefuse)
        {
            Debug.Log("<color=red>HẾT GIỜ! BẠN ĐÃ NỔ!</color>");
            RequestExplode();
        }

        if (bombTimerText != null) bombTimerText.gameObject.SetActive(false);
    }

    private void ExecuteCardLogic(DrawPileManager.CardType type, string sender)
    {
        if (isWaitingForDefuse && type != DrawPileManager.CardType.Defuse) return;

        switch (type)
        {
            case DrawPileManager.CardType.Skip: HandleEndTurnLogic(); break;
            case DrawPileManager.CardType.Attack: HandleEndTurnLogic(true); break;
            case DrawPileManager.CardType.Shuffle:
                DrawPileManager.Instance.ShuffleDrawPile();
                SyncDeckAfterAction();
                break;
            case DrawPileManager.CardType.SeeFuture: HandleSeeFuture(sender); break;
            case DrawPileManager.CardType.DrawBottom: HandleDrawBottom(sender); break;
            case DrawPileManager.CardType.Defuse: Host_HandleDefuse(sender); break;
        }
    }

    // ================================================================
    // PHẦN 3: LOGIC DÀNH RIÊNG CHO HOST
    // ================================================================

    private void Host_ProcessDraw(string player)
    {
        DrawPileManager.CardType drawn = DrawPileManager.Instance.DrawCardData();
        SyncDeckAfterAction();

        if (drawn == DrawPileManager.CardType.Explode)
        {
            Dictionary<string, object> res = new Dictionary<string, object>();
            res["type"] = "BOMB_TRAPPED";
            res["target"] = player;
            roomRef.Child("actions").Push().SetValueAsync(res);

            roomRef.Child("gameData/isWaitingForDefuse").SetValueAsync(true);
        }
        else
        {
            Dictionary<string, object> res = new Dictionary<string, object>();
            res["type"] = "DRAW_CONFIRMED";
            res["cardType"] = drawn.ToString();
            res["target"] = player;
            roomRef.Child("actions").Push().SetValueAsync(res).ContinueWithOnMainThread(t => HandleEndTurnLogic());
        }
    }

    private void Host_HandleDefuse(string player)
    {
        if (isWaitingForDefuse)
        {
            OnlineDrawManager.Instance.Host_InsertBombToFirebaseDeck();
            Invoke("Host_FinalizeDefuse", 1.0f);
        }
    }

    private void Host_FinalizeDefuse()
    {
        roomRef.Child("gameData/isWaitingForDefuse").SetValueAsync(false);
        HandleEndTurnLogic();
    }

    private void Host_HandlePlayerExploded(string player)
    {
        roomRef.Child("players").Child(player).Child("isDead").SetValueAsync(true);
        roomRef.Child("gameData/isWaitingForDefuse").SetValueAsync(false);
        HandleEndTurnLogic();
    }

    private void HandleEndTurnLogic(bool isAttackAction = false)
    {
        if (!OnlineDrawManager.Instance.isHost) return;

        int playersCount = RoomManager.Instance.currentRoomPlayers.Count;
        int nextTurn;

        if (turnsToDraw > 1 && !isAttackAction)
        {
            turnsToDraw--;
            nextTurn = OnlineDrawManager.Instance.currentTurnIndex;
        }
        else
        {
            // Tạm thời chuyển lượt đơn giản, OnlineGameLogic sẽ xử lý skip người chơi dead tốt hơn
            nextTurn = (OnlineDrawManager.Instance.currentTurnIndex + 1) % playersCount;
            turnsToDraw = isAttackAction ? 2 : 1;
        }

        roomRef.Child("gameData/currentTurnIndex").SetValueAsync(nextTurn);
        roomRef.Child("gameData/turnsToDraw").SetValueAsync(turnsToDraw);
    }

    private void ListenForDefuseStatus()
    {
        roomRef.Child("gameData/isWaitingForDefuse").ValueChanged += (s, e) => {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (e.Snapshot.Exists)
                {
                    isWaitingForDefuse = (bool)e.Snapshot.Value;
                    // Nếu trạng thái đổi sang false (đã gỡ xong), ẩn text UI nếu còn hiện
                    if (!isWaitingForDefuse && bombTimerText != null)
                        bombTimerText.gameObject.SetActive(false);
                }
                else
                    isWaitingForDefuse = false;
            });
        };
    }

    private void SyncDeckAfterAction() => OnlineDrawManager.Instance?.UpdateDeckToFirebaseFromManager();

    private void HandleSeeFuture(string receiver)
    {
        List<DrawPileManager.CardType> top3 = DrawPileManager.Instance.GetTopCards(3);
        string top3Str = string.Join(",", top3);

        Dictionary<string, object> msg = new Dictionary<string, object>();
        msg["type"] = "FUTURE_DATA";
        msg["receiver"] = receiver;
        msg["data"] = top3Str;
        roomRef.Child("actions").Push().SetValueAsync(msg);
    }

    private void HandleDrawBottom(string receiver)
    {
        DrawPileManager.CardType bottomCard = DrawPileManager.Instance.DrawBottomCardData();
        SyncDeckAfterAction();

        Dictionary<string, object> result = new Dictionary<string, object>();
        result["type"] = "DRAW_CONFIRMED";
        result["target"] = receiver;
        result["cardType"] = bottomCard.ToString();

        roomRef.Child("actions").Push().SetValueAsync(result).ContinueWithOnMainThread(t => HandleEndTurnLogic());
    }

    private void ListenForTurnData()
    {
        roomRef.Child("gameData/turnsToDraw").ValueChanged += (s, e) => {
            if (e.Snapshot.Exists) turnsToDraw = Convert.ToInt32(e.Snapshot.Value);
        };
    }

    private void ShowCardPlayedInDiscardPile(DrawPileManager.CardType type)
    {
        if (DrawPileManager.Instance != null)
            DrawPileManager.Instance.AddToDiscardPile(type);
    }

    private void UpdateDiscardPileVisual(DrawPileManager.CardType type)
    {
        if (discardPileDisplay == null) return;
        Sprite s = GetSpriteByType(type);
        if (s != null)
        {
            discardPileDisplay.gameObject.SetActive(true);
            discardPileDisplay.enabled = true;
            discardPileDisplay.sprite = s;
            discardPileDisplay.color = Color.white;
            discardPileDisplay.transform.SetAsLastSibling();
        }
    }

    private void ShowSeeFutureUI(string[] cardNames)
    {
        if (seeFuturePanel == null) return;
        seeFuturePanel.SetActive(true);
        for (int i = 0; i < futureCardSlots.Length; i++)
        {
            if (i < cardNames.Length)
            {
                if (Enum.TryParse(cardNames[i], out DrawPileManager.CardType type))
                {
                    futureCardSlots[i].sprite = GetSpriteByType(type);
                    futureCardSlots[i].gameObject.SetActive(true);
                }
            }
            else futureCardSlots[i].gameObject.SetActive(false);
        }
        CancelInvoke("HideSeeFutureUI");
        Invoke("HideSeeFutureUI", 5f);
    }

    public void HideSeeFutureUI() => seeFuturePanel.SetActive(false);

    private Sprite GetSpriteByType(DrawPileManager.CardType type)
    {
        if (cardVisuals == null) return null;
        foreach (var visual in cardVisuals)
        {
            if (visual.type == type) return visual.cardSprite;
        }
        return null;
    }
}