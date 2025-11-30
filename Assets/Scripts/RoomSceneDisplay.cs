using UnityEngine;
using UnityEngine.UI;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using TMPro;
using System.Linq;

public class RoomSceneDisplay : MonoBehaviour
{
    public GameObject playerListContainer;
    public GameObject playerListItemPrefab;
    public TMP_Text roomIDText;

    private DatabaseReference roomDbRef;

    private void Awake()
    {
        //  Gọi UpdateList ở Awake() để đảm bảo UI được vẽ sớm nhất
        if (RoomManager.Instance != null && RoomManager.Instance.currentRoomPlayers != null)
        {
            UpdatePlayerList(RoomManager.Instance.currentRoomPlayers);
        }
    }

    private void Start()
    {
        // 1. Kiểm tra Singleton và thiết lập tham chiếu Firebase
        if (RoomManager.Instance == null || string.IsNullOrEmpty(RoomManager.Instance.currentRoomID))
        {
            Debug.LogError("❌ Lỗi: RoomManager hoặc RoomID không tồn tại.");
            return;
        }

        string roomID = RoomManager.Instance.currentRoomID;

        // Hiển thị Room ID
        if (roomIDText != null)
        {
            roomIDText.text = "ID: " + roomID;
        }

        // Lấy tham chiếu đến node /rooms/{roomID}/players
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.Database != null)
        {
            // Lấy tham chiếu đến node /rooms/{roomID}/players
            roomDbRef = FirebaseManager.Instance.Database.RootReference
                        .Child("rooms").Child(roomID).Child("players");

            // 2. Bắt đầu lắng nghe thay đổi danh sách người chơi
            roomDbRef.ValueChanged += HandlePlayersValueChanged;
        }
        else
        {
            Debug.LogError("❌ Lỗi: FirebaseManager chưa sẵn sàng.");
        }
    }

    private void OnDestroy()
    {
        // Ngừng lắng nghe khi đối tượng bị hủy
        if (roomDbRef != null)
        {
            roomDbRef.ValueChanged -= HandlePlayersValueChanged;
        }
    }

    private void HandlePlayersValueChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("❌ Lỗi Firebase khi lắng nghe players: " + args.DatabaseError.Message);
            return;
        }

        // Kiểm tra xem script đã bị hủy chưa trước khi gọi Update UI
        if (this == null) return;

        if (args.Snapshot.Exists && args.Snapshot.Value != null)
        {
            List<string> players = new List<string>();

            // Xử lý dữ liệu từ Firebase
            if (args.Snapshot.Value is List<object> playerObjects)
            {
                foreach (object playerObj in playerObjects)
                {
                    if (playerObj != null) players.Add(playerObj.ToString());
                }
            }
            else if (args.Snapshot.Value is Dictionary<string, object> playerDict)
            {
                // Nếu Firebase lưu array dưới dạng Map { "0": "User1", "1": "User2" }
                // Cần sắp xếp theo key số (0, 1, 2...) để đảm bảo thứ tự
                var sortedPlayers = playerDict
                    .OrderBy(kvp => int.Parse(kvp.Key))
                    .Select(kvp => kvp.Value.ToString());

                players.AddRange(sortedPlayers);
            }

            RoomManager.Instance.currentRoomPlayers = players;
            UpdatePlayerList(players);
        }
    }

    /// Cập nhật UI hiển thị danh sách người chơi.
    public void UpdatePlayerList(List<string> players)
    {
        // Thêm kiểm tra RoomManager.Instance
        if (RoomManager.Instance == null)
        {
            Debug.LogError("❌ Lỗi: RoomManager không sẵn sàng.");
            return;
        }

        // Lấy Host (người đầu tiên trong danh sách)
        string roomHost = players.Count > 0 ? players[0] : null;

        if (playerListContainer == null)
        {
            Debug.LogError("❌ Lỗi: playerListContainer không được gán hoặc đã bị hủy.");
            return;
        }

        Transform containerTransform = playerListContainer.transform;

        // XÓA CÁC PLAYER CŨ BẰNG CÁCH LẶP NGƯỢC (Đã fix MissingReferenceException)
        for (int i = containerTransform.childCount - 1; i >= 0; i--)
        {
            GameObject child = containerTransform.GetChild(i).gameObject;
            if (child != null)
            {
                Destroy(child);
            }
        }

        // THÊM CÁC PLAYER MỚI
        foreach (string playerName in players)
        {
            if (playerListItemPrefab != null)
            {
                GameObject newItem = Instantiate(playerListItemPrefab, containerTransform);

                // Giả định Prefab có component TextMeshProUGUI là thành phần con
                TMP_Text nameText = newItem.GetComponentInChildren<TMP_Text>();

                if (nameText != null)
                {
                    string displayText = playerName;

                    //  Kiểm tra người đang được vẽ có phải là Host thực sự (players[0]) hay không.
                    if (playerName == roomHost)
                    {
                        displayText = playerName + " (HOST)";
                    }
                    // Các Client khác (không phải Host) chỉ hiển thị tên bình thường (playerName)

                    nameText.text = displayText;
                }
            }
        }
    }
}