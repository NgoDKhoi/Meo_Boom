using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro; 

public class JoinRoomManager : MonoBehaviour
{
    public static JoinRoomManager Instance;
    private DatabaseReference dbRef;
    private const string DATABASE_URL = "https://mygametest2-default-rtdb.asia-southeast1.firebasedatabase.app";
    private const int MAX_PLAYERS = 4;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        dbRef = FirebaseDatabase.GetInstance(FirebaseApp.DefaultInstance, DATABASE_URL).RootReference;
    }

    private void SetStatus(string message, TMP_Text statusText)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log(message);
    }
    public void JoinRoom(string roomID, string username, TMP_Text statusText)
    {
        if (dbRef == null)
        {
            SetStatus("❌ Lỗi: Database Reference chưa sẵn sàng.", statusText);
            return;
        }

        SetStatus("Đang kiểm tra phòng...", statusText); // Thông báo ban đầu
        DatabaseReference roomRef = dbRef.Child("rooms").Child(roomID);

        roomRef.GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || !task.Result.Exists)
            {
                SetStatus("❌ Phòng không tồn tại hoặc lỗi kết nối!", statusText);
                return;
            }

            DataSnapshot roomSnapshot = task.Result;
            DataSnapshot playersListSnapshot = roomSnapshot.Child("players");

            List<string> currentPlayers = new List<string>();
            foreach (var p in playersListSnapshot.Children)
            {
                currentPlayers.Add(p.Value.ToString());
            }

            // --- 1. KIỂM TRA ĐIỀU KIỆN ---
            if (currentPlayers.Count >= MAX_PLAYERS)
            {
                SetStatus("❌ Phòng đã đủ người chơi (" + MAX_PLAYERS + ").", statusText);
                return;
            }
            if (currentPlayers.Contains(username))
            {
                SetStatus("✅ Đã tham gia phòng (Reconnect).", statusText);
                FinalizeJoin(roomID, username, currentPlayers);
                return;
            }

            // --- 2. THÊM NGƯỜI CHƠI MỚI ---
            int nextIndex = currentPlayers.Count;
            roomRef.Child("players").Child(nextIndex.ToString()).SetValueAsync(username)
                .ContinueWithOnMainThread(t =>
                {
                    if (t.IsCompleted && !t.IsFaulted)
                    {
                        currentPlayers.Add(username);
                        SetStatus("✅ Tham gia phòng thành công!", statusText);
                        FinalizeJoin(roomID, username, currentPlayers);
                    }
                    else
                    {
                        SetStatus("❌ Lỗi khi thêm người chơi vào Firebase.", statusText);
                        Debug.LogError("❌ Lỗi Firebase: " + t.Exception);
                    }
                });
        });
    }

    private void FinalizeJoin(string roomID, string username, List<string> players)
    {
        if (RoomManager.Instance != null && GameDataManager.Instance != null)
        {
            RoomManager.Instance.currentRoomID = roomID;
            RoomManager.Instance.currentUsername = username;
            RoomManager.Instance.currentRoomPlayers = players;
            GameDataManager.Instance.roomID = roomID;
            SceneManager.LoadScene("RoomScene");
        }
        else
        {
            Debug.LogError("❌ Lỗi: RoomManager hoặc GameDataManager không tồn tại.");
        }
    }
}