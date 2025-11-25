using UnityEngine;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Collections.Generic;

public class RoomExitManager : MonoBehaviour
{
    private DatabaseReference dbRef;
    private string currentRoomID;
    private string currentUsername;
    private string hostName;
    private bool isFirebaseReady => FirebaseManager.Instance != null && FirebaseManager.Instance.Database != null;

    void Start()
    {
        if (RoomManager.Instance == null || !isFirebaseReady)
        {
            Debug.LogError("❌ RoomManager hoặc Firebase chưa sẵn sàng.");
            return;
        }

        currentRoomID = RoomManager.Instance.currentRoomID;
        currentUsername = RoomManager.Instance.currentUsername;
        dbRef = FirebaseManager.Instance.Database.RootReference;

        // Xác định Host (người đầu tiên trong danh sách)
        if (RoomManager.Instance.currentRoomPlayers.Count > 0)
        {
            hostName = RoomManager.Instance.currentRoomPlayers[0];
        }
    }

    /// Hàm này được gọi khi người chơi nhấn nút "THOÁT".
    public void OnExitRoomButtonClick()
    {
        if (string.IsNullOrEmpty(currentRoomID) || string.IsNullOrEmpty(currentUsername))
        {
            Debug.LogError("Dữ liệu phòng không hợp lệ để thoát.");
            GoToLobby();
            return;
        }

        // 1. Kiểm tra vai trò và thực hiện hành động tương ứng
        if (currentUsername == hostName)
        {
            DestroyRoom(); // Host hủy phòng
        }
        else
        {
            RemovePlayerFromRoom(); // Client rời phòng
        }
    }

    private void DestroyRoom()
    {
        // Xóa toàn bộ node phòng trên Firebase
        dbRef.Child("rooms").Child(currentRoomID).RemoveValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                Debug.Log("✅ Host đã hủy phòng thành công.");
            }
            else if (task.IsFaulted)
            {
                Debug.LogError("❌ Lỗi hủy phòng: " + task.Exception.Message);
            }
            GoToLobby();
        });
    }

    private void RemovePlayerFromRoom()
    {
        DatabaseReference playersRef = dbRef.Child("rooms").Child(currentRoomID).Child("players");

        // Lấy danh sách hiện tại từ RoomManager và xóa người chơi hiện tại
        List<string> updatedPlayers = RoomManager.Instance.currentRoomPlayers.ToList();
        updatedPlayers.Remove(currentUsername);

        // Ghi đè danh sách mới lên Firebase
        playersRef.SetValueAsync(updatedPlayers).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                Debug.Log("✅ Client đã rời khỏi phòng thành công.");
            }
            else if (task.IsFaulted)
            {
                Debug.LogError("❌ Lỗi rời phòng: " + task.Exception.Message);
            }
            GoToLobby();
        });
    }

    private void GoToLobby()
    {
        // Reset dữ liệu trong Singleton trước khi chuyển Scene
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.currentRoomID = null;
            RoomManager.Instance.currentRoomPlayers.Clear();
        }

        // Chuyển về LoadRoomScene (hoặc MainMenuScene)
        SceneManager.LoadScene("LoadRoomScene");
    }
}