using UnityEngine;
using UnityEngine.UI;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using TMPro;

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
                foreach (var entry in playerDict)
                {
                    if (entry.Value != null) players.Add(entry.Value.ToString());
                }
            }

            RoomManager.Instance.currentRoomPlayers = players;
            UpdatePlayerList(players);
        }
    }

    /// Cập nhật UI hiển thị danh sách người chơi.
    public void UpdatePlayerList(List<string> players)
    {
        // ❌ KIỂM TRA LỖI BẠN GẶP (Dòng 104)
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
                    // Thêm logic để hiển thị Host (nếu cần)
                    if (playerName == RoomManager.Instance.currentUsername)
                    {
                        nameText.text = playerName + " (HOST)";
                    }
                    else if (playerName == RoomManager.Instance.currentRoomPlayers[0])
                    {
                        nameText.text = playerName + " (Host)";
                    }
                    else
                    {
                        nameText.text = playerName;
                    }
                }
            }
        }
    }
}