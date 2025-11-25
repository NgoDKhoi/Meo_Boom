using UnityEngine;
using UnityEngine.UI;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GameStarterManager : MonoBehaviour
{
    public Button startButton;
    private const string GAMEPLAY_SCENE = "GameScene"; // Tên Scene game của bạn
    private const int MIN_PLAYERS = 2; // Số người chơi tối thiểu để bắt đầu

    // Tham chiếu Firebase
    private DatabaseReference dbRef;
    private DatabaseReference roomRef;
    private System.EventHandler<ValueChangedEventArgs> startStatusListener;
    private System.EventHandler<ValueChangedEventArgs> playerListListener;

    void Start()
    {
        // Khởi tạo các tham chiếu cần thiết
        if (RoomManager.Instance == null || FirebaseManager.Instance == null) return;

        dbRef = FirebaseManager.Instance.Database.RootReference;
        string roomID = RoomManager.Instance.currentRoomID;

        if (!string.IsNullOrEmpty(roomID))
        {
            roomRef = dbRef.Child("rooms").Child(roomID);

            ListenForPlayerChanges();
            ListenForGameStartSignal();
        }
    }

    void OnDestroy()
    {
        if (roomRef != null)
        {
            roomRef.Child("players").ValueChanged -= playerListListener;
            roomRef.Child("Started").ValueChanged -= startStatusListener;
        }
    }

    // ----------------------------------------------------------------------
    // PHẦN LOGIC 1: QUẢN LÝ NÚT BẮT ĐẦU (CHỈ DÀNH CHO HOST)
    // ----------------------------------------------------------------------

    private void ListenForPlayerChanges()
    {
        playerListListener = (object sender, ValueChangedEventArgs args) =>
        {
            if (startButton == null) return;

            if (args.Snapshot.Exists && args.Snapshot.ChildrenCount > 0)
            {
                // Lấy danh sách người chơi hiện tại
                List<string> players = new List<string>();
                foreach (var child in args.Snapshot.Children)
                {
                    players.Add(child.Value.ToString());
                }

                string currentUsername = RoomManager.Instance.currentUsername;
                string hostName = players[0]; 

                bool isHost = (currentUsername == hostName);
                bool isEnoughPlayers = players.Count >= MIN_PLAYERS;

                // 1. CHỈ HIỂN THỊ NÚT CHO HOST
                startButton.gameObject.SetActive(isHost);

                if (isHost)
                {
                    // 2. CHỈ BẬT (INTERACTABLE) NÚT KHI ĐỦ NGƯỜI
                    startButton.interactable = isEnoughPlayers;
                }
            }
        };
        roomRef.Child("players").ValueChanged += playerListListener;
    }

    // ----------------------------------------------------------------------
    // PHẦN LOGIC 2: XỬ LÝ SỰ KIỆN NHẤN NÚT BẮT ĐẦU (CHỈ HOST GỌI)
    // ----------------------------------------------------------------------
    public void OnStartGameButtonClick()
    {
        if (startButton != null && !startButton.interactable)
        {
            // Kiểm tra lại lần cuối nếu nút bị khóa
            Debug.LogWarning("Chưa đủ người chơi để bắt đầu.");
            return;
        }

        if (roomRef == null) return;

        // Cập nhật trường 'Started' thành TRUE trên Firebase
        roomRef.Child("Started").SetValueAsync(true).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                Debug.Log("✅ Gửi tín hiệu BẮT ĐẦU game thành công.");
            }
            else if (task.IsFaulted)
            {
                Debug.LogError("❌ Lỗi gửi tín hiệu BẮT ĐẦU: " + task.Exception);
            }
        });
    }

    // ----------------------------------------------------------------------
    // PHẦN LOGIC 3: LẮNG NGHE TÍN HIỆU BẮT ĐẦU (HOST VÀ CLIENT CÙNG GỌI)
    // ----------------------------------------------------------------------

    private void ListenForGameStartSignal()
    {
        startStatusListener = (object sender, ValueChangedEventArgs args) =>
        {
            if (args.Snapshot.Exists && args.Snapshot.Value != null)
            {
                // Kiểm tra nếu giá trị là boolean true
                if (args.Snapshot.Value is bool isStarted && isStarted)
                {
                    Debug.Log("🚀 NHẬN TÍN HIỆU: GAME BẮT ĐẦU! Chuyển Scene...");

                    // Dừng lắng nghe và chuyển Scene (Quan trọng)
                    roomRef.Child("Started").ValueChanged -= startStatusListener;
                    roomRef.Child("players").ValueChanged -= playerListListener;

                    SceneManager.LoadScene(GAMEPLAY_SCENE);
                }
            }
        };

        roomRef.Child("Started").ValueChanged += startStatusListener;
    }
}