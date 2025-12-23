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
    public GameObject seeFuturePanel;    // Bảng xem trước tương lai
    public Image[] futureCardSlots;      // 3 Slot hình ảnh trong bảng xem trước
    public Image discardPileDisplay;     // Image hiển thị lá bài vừa đánh lên bàn
    public List<CardVisualData> cardVisuals; // Danh sách Mapping Type -> Sprite

    [Header("--- TRẠNG THÁI VÒNG CHƠI ---")]
    public int turnsToDraw = 1;
    private string roomID;

    [Serializable]
    public struct CardVisualData
    {
        public DrawPileManager.CardType type;
        public Sprite cardSprite;
    }

    void Awake() => Instance = this;

    void Start()
    {
        // Khởi tạo trạng thái ban đầu cho UI
        if (seeFuturePanel != null) seeFuturePanel.SetActive(false);

        if (discardPileDisplay != null)
        {
            // Tạm thời ẩn nếu chưa có lá bài nào được đánh
            discardPileDisplay.gameObject.SetActive(false);
        }

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
    }

    private bool CheckIfItIsMyTurn()
    {
        if (OnlineDrawManager.Instance == null || RoomManager.Instance == null) return false;

        List<string> players = RoomManager.Instance.currentRoomPlayers;
        string myName = RoomManager.Instance.currentUsername;
        int currentTurnIndex = OnlineDrawManager.Instance.currentTurnIndex;

        if (players == null || currentTurnIndex < 0 || currentTurnIndex >= players.Count)
            return false;

        return players[currentTurnIndex] == myName;
    }

    // ================================================================
    // PHẦN 1: GỬI LỆNH TỪ NGƯỜI CHƠI
    // ================================================================

    public void RequestPlayCard(DrawPileManager.CardType cardType, string cardObjectID)
    {
        if (!CheckIfItIsMyTurn())
        {
            Debug.LogWarning("Không phải lượt của bạn!");
            return;
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
        if (!CheckIfItIsMyTurn()) return;

        Dictionary<string, object> action = new Dictionary<string, object>();
        action["type"] = "DRAW_REQUEST";
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
                    // Cập nhật hình ảnh lá bài vừa đánh lên bàn cho tất cả mọi người xem
                    UpdateDiscardPileVisual(cardType);

                    // Đồng bộ với logic Discard Pile cục bộ
                    ShowCardPlayedInDiscardPile(cardType);

                    // Chỉ Host mới thực hiện thay đổi logic game
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
                    Debug.Log("<color=red>BẠN DÍNH BOM!</color>");
                }
                break;

            case "FUTURE_DATA":
                if (data["receiver"].ToString() == RoomManager.Instance.currentUsername)
                {
                    string[] cards = data["data"].ToString().Split(',');
                    ShowSeeFutureUI(cards);
                }
                break;
        }

        // Tùy chọn: Xóa action sau khi xử lý để tránh nặng database (chỉ Host xóa)
        // if (OnlineDrawManager.Instance.isHost) snapshot.Reference.RemoveValueAsync();
    }

    // Cập nhật hình ảnh lá bài nằm ngửa trên bàn chơi
    private void UpdateDiscardPileVisual(DrawPileManager.CardType type)
    {
        if (discardPileDisplay == null) return;

        Sprite s = GetSpriteByType(type);
        if (s != null)
        {
            // Đảm bảo Image và Object đều được bật
            discardPileDisplay.gameObject.SetActive(true);
            discardPileDisplay.enabled = true;

            // Gán Sprite mới
            discardPileDisplay.sprite = s;

            // Đảm bảo độ hiển thị (Alpha = 1)
            Color c = discardPileDisplay.color;
            c.a = 1f;
            discardPileDisplay.color = c;

            // Đưa lên lớp trên cùng trong nhóm UI để không bị che khuất
            discardPileDisplay.transform.SetAsLastSibling();

            Debug.Log($"<color=green>[UI]</color> Đã hiển thị lá bài {type} lên bàn chơi.");
        }
        else
        {
            Debug.LogWarning($"Không tìm thấy Sprite cho loại bài: {type}. Hãy kiểm tra CardVisuals trong Inspector.");
        }
    }

    private void ExecuteCardLogic(DrawPileManager.CardType type, string sender)
    {
        switch (type)
        {
            case DrawPileManager.CardType.Skip:
                HandleEndTurnLogic();
                break;
            case DrawPileManager.CardType.Attack:
                HandleEndTurnLogic(true);
                break;
            case DrawPileManager.CardType.Shuffle:
                DrawPileManager.Instance.ShuffleDrawPile();
                SyncDeckAfterAction();
                break;
            case DrawPileManager.CardType.SeeFuture:
                HandleSeeFuture(sender);
                break;
            case DrawPileManager.CardType.DrawBottom:
                HandleDrawBottom(sender);
                break;
            case DrawPileManager.CardType.Defuse:
                Host_HandleDefuse(sender);
                break;
        }
    }

    // ================================================================
    // PHẦN 3: LOGIC DÀNH RIÊNG CHO HOST
    // ================================================================

    private void Host_ProcessDraw(string player)
    {
        DrawPileManager.CardType drawn = DrawPileManager.Instance.DrawCardData();
        SyncDeckAfterAction();

        Dictionary<string, object> res = new Dictionary<string, object>();
        res["sender"] = "SYSTEM";
        res["target"] = player;

        if (drawn == DrawPileManager.CardType.Explode)
        {
            res["type"] = "BOMB_TRAPPED";
        }
        else
        {
            res["type"] = "DRAW_CONFIRMED";
            res["cardType"] = drawn.ToString();
            HandleEndTurnLogic();
        }
        roomRef.Child("actions").Push().SetValueAsync(res);
    }

    private void Host_HandleDefuse(string player)
    {
        DrawPileManager.Instance.AddExplodingKittens();
        DrawPileManager.Instance.ShuffleDrawPile();
        SyncDeckAfterAction();
        HandleEndTurnLogic();
    }

    private void HandleEndTurnLogic(bool isAttackAction = false)
    {
        int playersCount = RoomManager.Instance.currentRoomPlayers.Count;
        int nextTurn;

        if (turnsToDraw > 1 && !isAttackAction)
        {
            turnsToDraw--;
            nextTurn = OnlineDrawManager.Instance.currentTurnIndex;
        }
        else
        {
            nextTurn = (OnlineDrawManager.Instance.currentTurnIndex + 1) % playersCount;
            turnsToDraw = isAttackAction ? 2 : 1;
        }

        roomRef.Child("gameData/currentTurnIndex").SetValueAsync(nextTurn);
        roomRef.Child("gameData/turnsToDraw").SetValueAsync(turnsToDraw);
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

    // ================================================================
    // PHẦN 4: HIỂN THỊ UI SEE FUTURE
    // ================================================================

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

        // Cách tìm kiếm tối ưu hơn bằng Loop
        foreach (var visual in cardVisuals)
        {
            if (visual.type == type) return visual.cardSprite;
        }
        return null;
    }
}