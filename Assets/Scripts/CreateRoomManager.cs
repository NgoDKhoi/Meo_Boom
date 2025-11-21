using UnityEngine;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine.SceneManagement;
using GameUtilities;

public class CreateRoomManager : MonoBehaviour
{
    public string roomSceneName = "RoomScene";
    private DatabaseReference dbReference;

    // Số lần thử tối đa để tránh loop vô tận
    private const int MAX_ATTEMPTS = 10;

    void Start()
    {
        // Lấy tham chiếu Database từ FirebaseManager (giả định đã khởi tạo)
        // Lưu ý: Nếu FirebaseManager.Instance.Database bị lỗi, dbReference sẽ là null.
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.Database != null)
        {
            dbReference = FirebaseManager.Instance.Database.RootReference;
        }
        else
        {
            Debug.LogError("❌ Lỗi: FirebaseManager hoặc Database chưa sẵn sàng! Vui lòng kiểm tra quá trình khởi tạo Firebase.");
        }
    }

    public void OnCreateRoomButtonClick()
    {
        // Kiểm tra username trước khi tạo phòng
        if (GameDataManager.Instance == null || string.IsNullOrEmpty(GameDataManager.Instance.username))
        {
            Debug.LogError("❌ Lỗi: GameDataManager hoặc Username chưa được tải. Không thể tạo phòng.");
            return;
        }

        // Kiểm tra dbReference đã sẵn sàng
        if (dbReference == null)
        {
            Debug.LogError("❌ Lỗi: Database Reference chưa được thiết lập. Vui lòng kiểm tra lại Start().");
            return;
        }

        // Bắt đầu quá trình tạo ID và kiểm tra tính duy nhất
        CheckAndCreateRoom(1);
    }

    // -------------------------------------------------------------
    // PHƯƠNG THỨC PHỤ ĐỂ KIỂM TRA SỰ CỐ HIỂN THỊ HÀM TRONG UNITY EDITOR
    public void TestButtonClick()
    {
        Debug.Log("🎉 Test: Nút Tạo Phòng đã hoạt động!");
    }

    /// Lặp lại việc tạo ID và kiểm tra tính duy nhất.
    private void CheckAndCreateRoom(int attempt)
    {
        if (attempt > MAX_ATTEMPTS)
        {
            Debug.LogError("❌ Lỗi: Không thể tạo ID phòng duy nhất sau " + MAX_ATTEMPTS + " lần thử.");
            return;
        }

        string newRoomID = CreateRoomID.GenerateRoomID();

        // Kiểm tra xem ID này đã tồn tại trong Firebase chưa
        dbReference.Child("rooms").Child(newRoomID).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("❌ Lỗi khi kiểm tra ID phòng: " + task.Exception);
                return;
            }

            if (task.Result.Exists)
            {
                // ID đã tồn tại, thử lại
                Debug.LogWarning("⚠️ ID phòng " + newRoomID + " đã tồn tại. Thử lại lần " + (attempt + 1));
                CheckAndCreateRoom(attempt + 1);
            }
            else
            {
                // ID phòng duy nhất, tiến hành tạo phòng
                Debug.Log("✅ Tạo ID phòng duy nhất thành công: " + newRoomID);
                SendRoomDataToFirebase(newRoomID);
            }
        });
    }

    /// Gửi dữ liệu phòng lên Firebase và chuyển Scene.
    private void SendRoomDataToFirebase(string roomID)
    {
        string currentUsername = GameDataManager.Instance.username;

        // Tạo đối tượng RoomData. HostName tự động được thêm vào List<players>
        RoomData roomData = new RoomData(currentUsername);

        string json = JsonUtility.ToJson(roomData);

        // Gửi dữ liệu lên node /rooms/roomID
        dbReference.Child("rooms").Child(roomID).SetRawJsonValueAsync(json)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("❌ Lỗi khi gửi RoomData lên Firebase: " + task.Exception);
                    return;
                }

                if (task.IsCompleted)
                {
                    Debug.Log("✅ Phòng đã được tạo thành công với ID: " + roomID);

                    // 1. Lưu thông tin phòng vào RoomManager (Singleton)
                    if (RoomManager.Instance != null)
                    {
                        RoomManager.Instance.currentRoomID = roomID;
                        RoomManager.Instance.currentUsername = currentUsername;
                        RoomManager.Instance.currentRoomPlayers = roomData.players;
                    }
                    else
                    {
                        Debug.LogError("❌ Lỗi: RoomManager.Instance không tồn tại.");
                    }

                    // 2. Chuyển sang RoomScene
                    SceneManager.LoadScene(roomSceneName);
                }
            });
    }
}