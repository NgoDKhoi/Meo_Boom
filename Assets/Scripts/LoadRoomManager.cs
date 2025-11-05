using UnityEngine;
using TMPro;
using Firebase.Auth;
using Firebase.Database;
using System.Threading.Tasks;
using Firebase;
using System.Linq;
using Firebase.Extensions;
using UnityEngine.SceneManagement;

public class LoadRoomManager : MonoBehaviour
{
    private bool isLeaderboardOpen = false;

    // GIỮ LẠI URL NẾU DEFAULT INSTANCE GẶP VẤN ĐỀ
    private const string DatabaseUrl = "https://mygametest2-default-rtdb.asia-southeast1.firebasedatabase.app";

    [SerializeField] private TextMeshProUGUI Text_LoginName;
    [SerializeField] private TextMeshProUGUI Text_Score;
    [SerializeField] private GameObject Panel_Leaderboard;
    [SerializeField] public GameObject LeaderboardItem_Prefab;
    [SerializeField] private GameObject LeaderboardContent_Parent;
    [SerializeField] private GameObject Panel_MainUI;
    // [SerializeField] private GameObject Panel_NhapID; -> cho code

    void Start()
    {
        InitializeFirebase();
        LoadUserDataFromFirebase();

        if (Panel_MainUI != null)
            Panel_MainUI.SetActive(true);
        if (Panel_Leaderboard != null)
            Panel_Leaderboard.SetActive(false);
    }

    private void InitializeFirebase()
    {
        var app = FirebaseApp.DefaultInstance;
        if (app.Options.DatabaseUrl == null)
        {
            app.Options.DatabaseUrl = new System.Uri(DatabaseUrl);
        }
    }

    // Cập nhật để ưu tiên tải Username và Score
    private void LoadUserDataFromFirebase()
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;

        if (user != null)
        {
            string userId = user.UserId;
            // Hiển thị email hoặc một tên tạm thời trước
            Text_LoginName.text = "Đang tải tên...";

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Text_Score.text = "Điểm: Đang tải...";
            });

            Debug.Log("UID của tôi là: " + userId);

            DatabaseReference reference = FirebaseDatabase.GetInstance(DatabaseUrl).RootReference;

            reference.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError($"LỖI ĐIỂM CÁ NHÂN: {task.Exception.InnerException?.Message ?? task.Exception.Message}");
                    Text_LoginName.text = user.Email; // Dùng email tạm thời nếu lỗi
                    Text_Score.text = "Điểm: LỖI KẾT NỐI";
                    return;
                }

                if (task.IsCompleted)
                {
                    DataSnapshot snapshot = task.Result;

                    if (snapshot.Exists)
                    {
                        // 1. Lấy Username
                        string username = snapshot.Child("username").Exists ? snapshot.Child("username").Value.ToString() : user.Email;

                        // 2. Lấy Score
                        object scoreObject = snapshot.Child("score").Value;
                        long score = 0;

                        if (scoreObject is long l) score = l;
                        else if (scoreObject is int i) score = i;
                        else if (scoreObject is double d) score = (long)d;

                        // Cập nhật UI
                        Text_LoginName.text = username;
                        Text_Score.text = "Điểm: " + score.ToString();
                    }
                    else
                    {
                        Text_LoginName.text = user.Email;
                        Text_Score.text = "Điểm: 0 (Chưa có dữ liệu)";
                    }
                }
            });
        }
        else
        {
            Text_LoginName.text = "Đăng nhập thất bại";
            Text_Score.text = "Điểm: 0";
        }
    }

    public void OnCreateRoomClicked()
    {
        Debug.Log("Tạo Phòng. (Cần logic chuyển Scene)");
        /*
        SceneManager.LoadScene("RoomScene");
        */
    }

    public void OnJoinRoomClicked()
    {
        Debug.Log("Tham Gia Phòng. (Cần logic bật Panel nhập mã phòng)");

        /*
        if (Panel_NhapID != null)
        {
            Panel_NhapID.SetActive(true);
            Panel_MainUI.SetActive(false);
            Panel_Leaderboard.SetActive(false);
        }
        else
        {
            Debug.LogError("LỖI CẤU HÌNH: Thiếu Panel_NhapID.");
        }
        */
    }

    public void OnVsBotClicked()
    {
        Debug.Log("Chơi với Máy. (Cần logic chuyển Scene game)");
        SceneManager.LoadScene("Gameplay");
    }

    public void OnLeaderboardClicked()
    {
        if (Panel_Leaderboard == null || Panel_MainUI == null) return;

        bool isLeaderboardShowing = Panel_Leaderboard.activeSelf;

        if (!isLeaderboardShowing)
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

    private void LoadLeaderboardData()
    {
        if (LeaderboardItem_Prefab == null || LeaderboardContent_Parent == null)
        {
            Debug.LogError("LỖI CẤU HÌNH: Thiếu Prefab hoặc Content Parent.");
            return;
        }

        Transform contentParent = LeaderboardContent_Parent.transform;

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            // Xóa các item cũ
            for (int i = contentParent.childCount - 1; i >= 0; i--)
            {
                GameObject child = contentParent.GetChild(i).gameObject;
                if (child != null)
                {
                    Destroy(child);
                }
            }
            StartFirebaseQuery(contentParent);
        });
    }

    const int TopLimit = 5;

    private void StartFirebaseQuery(Transform contentParent)
    {
        if (!isLeaderboardOpen)
        {
            Debug.Log("⚠️ Leaderboard đã đóng, bỏ qua callback Firebase.");
            return;
        }

        if (contentParent == null)
        {
            Debug.LogWarning("Content Parent đã bị hủy trước khi truy vấn Firebase bắt đầu.");
            return;
        }

        DatabaseReference reference = FirebaseDatabase.GetInstance(DatabaseUrl).GetReference("users");

        FirebaseUser currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
        string currentUserId = currentUser?.UserId;
        string currentUserEmail = currentUser?.Email; // Giữ lại email để làm tên dự phòng

        // Đã thay đổi: Lưu cả userId, username, và score
        reference.OrderByChild("score").LimitToLast(TopLimit).GetValueAsync().ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"LỖI TẢI BXH: {task.Exception.InnerException?.Message ?? task.Exception.Message}");
                return;
            }

            if (task.IsCompleted)
            {
                DataSnapshot snapshot = task.Result;
                var userScores = new System.Collections.Generic.List<(string userId, string username, long score)>();

                foreach (var childSnapshot in snapshot.Children)
                {
                    string userId = childSnapshot.Key;

                    // LẤY USERNAME (hoặc dùng email nếu username không tồn tại)
                    string username = childSnapshot.Child("username").Exists ? childSnapshot.Child("username").Value.ToString() : (childSnapshot.Child("email").Exists ? childSnapshot.Child("email").Value.ToString() : "N/A");

                    object scoreObject = childSnapshot.Child("score").Value;
                    long score = 0;
                    if (scoreObject is long l) score = l;
                    else if (scoreObject is int i) score = i;
                    else if (scoreObject is double d) score = (long)d;

                    userScores.Add((userId, username, score));
                }
                userScores.Reverse();

                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {

                    if (!isLeaderboardOpen || this == null || gameObject == null ||
                        !gameObject.activeInHierarchy || contentParent == null ||
                        LeaderboardItem_Prefab == null)
                    {
                        Debug.LogWarning("⚠️ Bỏ qua cập nhật vì UI đã đóng hoặc bị hủy.");
                        return;
                    }

                    for (int i = 0; i < userScores.Count; i++)
                    {
                        if (!isLeaderboardOpen) break;
                        int index = i;

                        string nameToDisplay = userScores[index].username;

                        if (userScores[index].userId == currentUserId)
                        {
                            // Thêm chuỗi đánh dấu vào tên người chơi
                            nameToDisplay += " (HẠNG CỦA BẠN)";
                        }

                        GameObject newEntry = Instantiate(LeaderboardItem_Prefab, contentParent);
                        newEntry.SetActive(true);

                        LeaderboardItem itemScript = newEntry.GetComponent<LeaderboardItem>();
                        if (itemScript != null)
                            itemScript.SetData(index + 1, nameToDisplay, (int)userScores[index].score);
                    }

                    // Truyền currentUserEmail để LoadCurrentUserRank có tên dự phòng
                    LoadCurrentUserRank(currentUserId, currentUserEmail, userScores.Select(u => (u.userId, u.score)).ToList());
                });
            }
        }, TaskScheduler.Default);
    }

    // Cập nhật tham số topScores
    private void LoadCurrentUserRank(string currentUserId, string currentUserEmail, System.Collections.Generic.List<(string userId, long score)> topScores)
    {
        if (string.IsNullOrEmpty(currentUserId)) return;
        Transform contentParent = LeaderboardContent_Parent.transform;
        if (contentParent == null) return;

        DatabaseReference userReference = FirebaseDatabase.GetInstance(DatabaseUrl).GetReference($"users/{currentUserId}");

        userReference.GetValueAsync().ContinueWith(task => // Lấy toàn bộ data người dùng hiện tại
        {
            if (task.IsCompleted && !task.IsFaulted && task.Result.Exists)
            {
                DataSnapshot snapshot = task.Result;

                // 1. Lấy Tên và Điểm
                string currentUsername = snapshot.Child("username").Exists ? snapshot.Child("username").Value.ToString() : currentUserEmail;
                long currentScore = snapshot.Child("score").Value is long l ? l : (snapshot.Child("score").Value is int i ? (long)i : (snapshot.Child("score").Value is double d ? (long)d : 0));

                bool isInTopX = topScores.Any(item => item.userId == currentUserId);
                if (isInTopX) return;

                DatabaseReference rankRef = FirebaseDatabase.GetInstance(DatabaseUrl).GetReference("users");

                rankRef.OrderByChild("score")
                    .StartAt(currentScore)
                    .GetValueAsync()
                    .ContinueWith(rankTask =>
                    {
                        if (rankTask.IsCompleted && !rankTask.IsFaulted)
                        {
                            DataSnapshot rankSnapshot = rankTask.Result;
                            // Số lượng người chơi có điểm lớn hơn hoặc bằng điểm của người chơi hiện tại
                            long myRank = rankSnapshot.ChildrenCount;

                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                if (contentParent == null || LeaderboardItem_Prefab == null) return;

                                bool showSeparator = (int)myRank > TopLimit;

                                if (showSeparator)
                                {
                                    GameObject separatorEntry = Instantiate(LeaderboardItem_Prefab, contentParent);
                                    separatorEntry.SetActive(true);
                                    LeaderboardItem sepScript = separatorEntry.GetComponent<LeaderboardItem>();
                                    if (sepScript != null)
                                        sepScript.SetData(0, ".........", 0);
                                }

                                GameObject rankEntry = Instantiate(LeaderboardItem_Prefab, contentParent);
                                rankEntry.SetActive(true);
                                LeaderboardItem itemScript = rankEntry.GetComponent<LeaderboardItem>();
                                if (itemScript != null)
                                    itemScript.SetData((int)myRank, currentUsername + " (HẠNG CỦA BẠN)", (int)currentScore);
                            });
                        }
                    }, TaskScheduler.Default);
            }
        }, TaskScheduler.Default);
    }
}
