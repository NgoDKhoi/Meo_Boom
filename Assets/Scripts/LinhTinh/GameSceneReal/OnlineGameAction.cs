using UnityEngine;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System;

public class OnlineGameActionManager : MonoBehaviour
{
    public static OnlineGameActionManager Instance;
    private DatabaseReference roomRef;

    [Header("--- TRẠNG THÁI VÒNG CHƠI ---")]
    public int turnsToDraw = 1;
    private string roomID;

    void Awake() => Instance = this;

    void Start()
    {
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

    // ================================================================
    // PHẦN 1: GỬI LỆNH ĐÁNH BÀI
    // ================================================================
    public void RequestPlayCard(DrawPileManager.CardType cardType, string cardObjectID)
    {
        if (OnlineDrawManager.Instance == null || !OnlineDrawManager.Instance.IsMyTurn()) return;

        Dictionary<string, object> action = new Dictionary<string, object>();
        action["type"] = "PLAY_ACTION";
        action["sender"] = RoomManager.Instance.currentUsername;
        action["cardType"] = cardType.ToString();
        action["cardID"] = cardObjectID;

        roomRef.Child("actions").Push().SetValueAsync(action);
    }

    // ================================================================
    // PHẦN 2: LẮNG NGHE VÀ XỬ LÝ CHỨC NĂNG
    // ================================================================
    private void ListenForGameActions()
    {
        roomRef.Child("actions").ChildAdded += (s, e) => {
            if (!e.Snapshot.Exists) return;
            var data = e.Snapshot.Value as Dictionary<string, object>;
            if (data == null || !data.ContainsKey("type")) return;

            string type = data["type"].ToString();

            if (type == "PLAY_ACTION")
            {
                string cardTypeStr = data["cardType"].ToString();
                DrawPileManager.CardType cardType = (DrawPileManager.CardType)Enum.Parse(typeof(DrawPileManager.CardType), cardTypeStr);
                string sender = data["sender"].ToString();

                ShowCardPlayedInDiscardPile(cardType);

                // Chỉ máy Host thực hiện xử lý logic bài
                if (OnlineDrawManager.Instance.isHost)
                {
                    ExecuteCardLogic(cardType, sender);
                }
            }
        };
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
                // Gọi hàm xào bài của DrawPileManager (máy Host xào)
                DrawPileManager.Instance.ShuffleDrawPile();
                // Sau khi xào xong phải đồng bộ bộ bài mới lên Firebase
                SyncDeckAfterAction();
                break;

            case DrawPileManager.CardType.SeeFuture:
                HandleSeeFuture(sender);
                break;

            case DrawPileManager.CardType.DrawBottom:
                HandleDrawBottom(sender);
                break;
        }
    }

    // ================================================================
    // PHẦN 3: LOGIC VÒNG CHƠI & ĐỒNG BỘ
    // ================================================================

    private void SyncDeckAfterAction()
    {
        // Bạn cần một hàm trong OnlineDrawManager để lấy list bài hiện tại và upload lên Firebase
        // Tôi mượn logic Upload bài của bạn nếu có
        OnlineDrawManager.Instance.UpdateDeckToFirebaseFromManager();
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

    private void ListenForTurnData()
    {
        roomRef.Child("gameData/turnsToDraw").ValueChanged += (s, e) => {
            if (e.Snapshot.Exists)
                turnsToDraw = Convert.ToInt32(e.Snapshot.Value);
        };
    }

    private void ShowCardPlayedInDiscardPile(DrawPileManager.CardType type)
    {
        Debug.Log($"<color=yellow>[Action]</color> Lá {type} đã được đánh!");
        // Thêm lá bài vào chồng bài bỏ trên mọi máy
        DrawPileManager.Instance.AddToDiscardPile(type);
    }

    private void HandleSeeFuture(string receiver)
    {
        // Lấy 3 lá đầu từ DrawPileManager
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
        // Gọi hàm rút đáy của bạn
        DrawPileManager.CardType bottomCard = DrawPileManager.Instance.DrawBottomCardData();

        // Cập nhật bộ bài mới lên Firebase (vì đã mất 1 lá đáy)
        SyncDeckAfterAction();

        // Gửi xác nhận cho máy khách để họ nhận lá bài đó vào tay
        Dictionary<string, object> result = new Dictionary<string, object>();
        result["type"] = "DRAW_CONFIRMED";
        result["target"] = receiver;
        result["cardType"] = bottomCard.ToString();

        roomRef.Child("actions").Push().SetValueAsync(result).ContinueWithOnMainThread(t => {
            HandleEndTurnLogic();
        });
    }
}