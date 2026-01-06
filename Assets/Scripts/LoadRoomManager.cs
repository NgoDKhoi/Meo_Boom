using UnityEngine;
using TMPro;
using Firebase.Auth;
using Firebase.Database;
using System.Threading.Tasks;
using Firebase;
using System.Linq;
using Firebase.Extensions;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class LoadRoomManager : MonoBehaviour
{
    private bool isLeaderboardOpen = false;

    // Firebase Database URL
    private const string DatabaseUrl = "https://mygametest2-default-rtdb.asia-southeast1.firebasedatabase.app";

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI Text_LoginName;
    [SerializeField] private TextMeshProUGUI Text_Score;
    [SerializeField] private GameObject Panel_Leaderboard;
    [SerializeField] private GameObject LeaderboardItem_Prefab;
    [SerializeField] private GameObject LeaderboardContent_Parent;
    [SerializeField] private GameObject Panel_MainUI;
    [SerializeField] private GameObject Panel_NhapID;

    private DatabaseReference userRef;
    private FirebaseUser currentUser;

    void Start()
    {
        InitializeFirebase();

        if (Panel_MainUI != null) Panel_MainUI.SetActive(true);
        if (Panel_Leaderboard != null) Panel_Leaderboard.SetActive(false);
        if (Panel_NhapID != null) Panel_NhapID.SetActive(false);

        SetupRealtimeUserData();
    }

    private void OnDestroy()
    {
        // Hủy lắng nghe để tránh lỗi memory leak khi chuyển Scene
        if (userRef != null)
        {
            userRef.ValueChanged -= HandleUserDataChanged;
        }
    }

    private void InitializeFirebase()
    {
        var app = FirebaseApp.DefaultInstance;
        if (app.Options.DatabaseUrl == null)
        {
            app.Options.DatabaseUrl = new System.Uri(DatabaseUrl);
        }
        currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
    }

    // Thiết lập lắng nghe dữ liệu Real-time
    private void SetupRealtimeUserData()
    {
        if (currentUser == null)
        {
            Text_LoginName.text = "Chưa đăng nhập";
            Text_Score.text = "Điểm: 0";
            return;
        }

        Text_LoginName.text = "Đang tải...";
        Text_Score.text = "Điểm: ...";

        userRef = FirebaseDatabase.GetInstance(DatabaseUrl).GetReference("users").Child(currentUser.UserId);

        // Đăng ký sự kiện: Mỗi khi Data trên Firebase đổi, hàm HandleUserDataChanged sẽ tự chạy
        userRef.ValueChanged += HandleUserDataChanged;
    }

    private void HandleUserDataChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError($"Lỗi Realtime: {args.DatabaseError.Message}");
            return;
        }

        if (args.Snapshot.Exists)
        {
            // Lấy Username
            string username = args.Snapshot.Child("username").Exists
                ? args.Snapshot.Child("username").Value.ToString()
                : currentUser.Email;

            // Lấy Score an toàn
            long score = 0;
            object scoreVal = args.Snapshot.Child("score").Value;
            if (scoreVal != null)
            {
                score = System.Convert.ToInt64(scoreVal);
            }

            // Cập nhật UI (Luôn chạy trên Main Thread vì ValueChanged của Firebase đôi khi chạy thread riêng)
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                Text_LoginName.text = username;
                Text_Score.text = $"Điểm: {score}";
            });
        }
        else
        {
            Text_LoginName.text = currentUser.Email;
            Text_Score.text = "Điểm: 0";
        }
    }

    #region Button Events

    public void OnCreateRoomClicked()
    {
        SceneManager.LoadScene("RoomScene");
    }

    public void OnJoinRoomClicked()
    {
        if (Panel_NhapID != null)
        {
            Panel_NhapID.SetActive(true);
            Panel_MainUI.SetActive(false);
        }
    }

    public void OnVsBotClicked()
    {
        SceneManager.LoadScene("GameSceneFake");

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMusic(AudioManager.Instance.battleMusic);
        }
    }

    public void OnLeaderboardClicked()
    {
        if (Panel_Leaderboard != null && Panel_MainUI != null)
        {
            isLeaderboardOpen = true;
            Panel_MainUI.SetActive(false);
            Panel_Leaderboard.SetActive(true);
            LoadLeaderboardData();
        }
    }

    public void OnCloseLeaderboardClicked()
    {
        isLeaderboardOpen = false;
        Panel_Leaderboard.SetActive(false);
        Panel_MainUI.SetActive(true);
    }

    #endregion

    #region Leaderboard Logic

    private void LoadLeaderboardData()
    {
        if (LeaderboardItem_Prefab == null || LeaderboardContent_Parent == null) return;

        // Xóa sạch list cũ
        foreach (Transform child in LeaderboardContent_Parent.transform)
        {
            Destroy(child.gameObject);
        }

        StartFirebaseLeaderboardQuery();
    }

    private void StartFirebaseLeaderboardQuery()
    {
        DatabaseReference dbRef = FirebaseDatabase.GetInstance(DatabaseUrl).GetReference("users");

        // Lấy top 5 điểm cao nhất
        dbRef.OrderByChild("score").LimitToLast(5).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (!isLeaderboardOpen || task.IsFaulted) return;

            DataSnapshot snapshot = task.Result;
            List<(string uid, string name, long score)> entries = new List<(string uid, string name, long score)>();

            foreach (var child in snapshot.Children)
            {
                string uid = child.Key;
                string name = child.Child("username").Exists ? child.Child("username").Value.ToString() : "Người chơi";
                long score = child.Child("score").Exists ? System.Convert.ToInt64(child.Child("score").Value) : 0;
                entries.Add((uid, name, score));
            }

            // Firebase trả về từ thấp đến cao -> Cần đảo ngược lại
            entries.Reverse();

            // Hiển thị Top 5
            for (int i = 0; i < entries.Count; i++)
            {
                CreateLeaderboardItem(i + 1, entries[i].uid, entries[i].name, entries[i].score);
            }

            // Xử lý hiển thị hạng cá nhân nếu không nằm trong Top 5
            CheckAndDisplayUserRank(entries);
        });
    }

    private void CreateLeaderboardItem(int rank, string uid, string name, long score)
    {
        GameObject go = Instantiate(LeaderboardItem_Prefab, LeaderboardContent_Parent.transform);
        var item = go.GetComponent<LeaderboardItem>();

        string displayName = name;
        if (uid == currentUser.UserId) displayName += " <color=yellow>(BẠN)</color>";

        if (item != null) item.SetData(rank, displayName, (int)score);
    }

    private void CheckAndDisplayUserRank(List<(string uid, string name, long score)> topFive)
    {
        // Nếu mình đã ở trong Top 5 rồi thì không cần hiển thị thêm hạng riêng lẻ phía dưới
        if (topFive.Any(u => u.uid == currentUser.UserId)) return;

        // Lấy điểm hiện tại của mình để tính hạng
        userRef.GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || !task.Result.Exists) return;

            long myScore = System.Convert.ToInt64(task.Result.Child("score").Value);
            string myName = task.Result.Child("username").Exists ? task.Result.Child("username").Value.ToString() : currentUser.Email;

            // Truy vấn xem có bao nhiêu người điểm cao hơn mình
            FirebaseDatabase.GetInstance(DatabaseUrl).GetReference("users")
                .OrderByChild("score").StartAt(myScore).GetValueAsync().ContinueWithOnMainThread(rankTask =>
                {
                    if (rankTask.IsFaulted) return;

                    // Hạng = số người có điểm >= mình
                    int myRank = (int)rankTask.Result.ChildrenCount;

                    // Tạo vạch ngăn cách "..."
                    CreateLeaderboardItem(0, "", ".........", 0);
                    // Hiển thị hạng của mình
                    CreateLeaderboardItem(myRank, currentUser.UserId, myName, myScore);
                });
        });
    }

    #endregion
}