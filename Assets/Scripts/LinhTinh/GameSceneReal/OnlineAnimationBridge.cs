using UnityEngine;
using Firebase.Database;
using System.Collections.Generic;

public class OnlineAnimationBridge : MonoBehaviour
{
    private DatabaseReference roomRef;

    void Start()
    {
        // Đợi kết nối Firebase thông qua RoomID
        if (RoomManager.Instance == null || string.IsNullOrEmpty(RoomManager.Instance.currentRoomID))
        {
            StartCoroutine(WaitAndConnect());
        }
        else
        {
            ConnectToActions();
        }
    }

    private System.Collections.IEnumerator WaitAndConnect()
    {
        while (RoomManager.Instance == null || string.IsNullOrEmpty(RoomManager.Instance.currentRoomID))
            yield return new WaitForSeconds(0.5f);

        ConnectToActions();
    }

    private void ConnectToActions()
    {
        string roomID = RoomManager.Instance.currentRoomID;
        roomRef = FirebaseManager.Instance.Database.RootReference.Child("rooms").Child(roomID);

        // Lắng nghe các hành động mới được đẩy lên node "actions"
        roomRef.Child("actions").ChildAdded += HandleActionAdded;
    }

    private void HandleActionAdded(object sender, ChildChangedEventArgs e)
    {
        if (!e.Snapshot.Exists) return;
        var data = e.Snapshot.Value as Dictionary<string, object>;
        if (data == null) return;

        string type = data.ContainsKey("type") ? data["type"].ToString() : "";

        // Chuyển về Main Thread để thực thi Animation trong Unity
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            ExecuteAnimation(type, data);
        });
    }

    private void ExecuteAnimation(string type, Dictionary<string, object> data)
    {
        if (OnlineAnimationManager.Instance == null) return;

        switch (type)
        {
            case "DRAW_CONFIRMED":
                // KIỂM TRA AN TOÀN: Nếu không có key receiver thì bỏ qua không chạy animation này
                if (!data.ContainsKey("receiver"))
                {
                    Debug.LogWarning("[AnimationBridge] DRAW_CONFIRMED bi thieu key 'receiver'");
                    return;
                }

                string receiver = data["receiver"].ToString();

                // Xác định vị trí tay bài
                Transform targetArea = (receiver == RoomManager.Instance.currentUsername)
                    ? OnlineDrawManager.Instance.playerHandArea
                    : OnlineDrawManager.Instance.GetOpponentArea(receiver);

                // Kiểm tra rút đáy (isBottom)
                bool isBottom = data.ContainsKey("isBottom") && System.Convert.ToBoolean(data["isBottom"]);

                OnlineAnimationManager.Instance.PlayDrawCardAnimation(receiver, targetArea, isBottom);
                break;

            case "PLAY_CARD":
                // PLAY_CARD thuong dung key 'sender'
                if (!data.ContainsKey("sender")) return;

                string player = data["sender"].ToString();

                if (data.ContainsKey("cardType"))
                {
                    string cardStr = data["cardType"].ToString();
                    if (System.Enum.TryParse(cardStr, out DrawPileManager.CardType cType))
                    {
                        OnlineAnimationManager.Instance.PlayCardEffectAnimation(player, cType);
                    }
                }
                break;

            case "EXPLODE_EVENT":
                // Kiem tra key 'victim' (nan nhan)
                if (data.ContainsKey("victim"))
                {
                    string victim = data["victim"].ToString();
                    OnlineAnimationManager.Instance.PlayCardEffectAnimation(victim, DrawPileManager.CardType.Explode);
                }
                break;
        }
    }
    void OnDestroy()
    {
        if (roomRef != null)
        {
            roomRef.Child("actions").ChildAdded -= HandleActionAdded;
        }
    }
}