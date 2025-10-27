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

    private void LoadUserDataFromFirebase()
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;

        if (user != null)
        {
            Text_LoginName.text = user.Email;

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Text_Score.text = "Điểm: Đang tải...";
            });

            string userId = user.UserId;
            Debug.Log("UID của tôi là: " + userId);

            DatabaseReference reference = FirebaseDatabase.GetInstance(DatabaseUrl).RootReference;

            reference.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError($"LỖI ĐIỂM CÁ NHÂN: {task.Exception.InnerException?.Message ?? task.Exception.Message}");
                    Text_Score.text = "Điểm: LỖI KẾT NỐI";
                    return;
                }

                if (task.IsCompleted)
                {
                    DataSnapshot snapshot = task.Result;

                    if (snapshot.Exists)
                    {
                        object scoreObject = snapshot.Child("score").Value;
                        long score = 0;

                        if (scoreObject is long l) score = l;
                        else if (scoreObject is int i) score = i;
                        else if (scoreObject is double d) score = (long)d;

                        Text_Score.text = "Điểm: " + score.ToString();
                    }
                    else
                    {
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
        string currentUserEmail = currentUser?.Email;

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
                var userScores = new System.Collections.Generic.List<(string userId, string email, long score)>();

                foreach (var childSnapshot in snapshot.Children)
                {
                    string userId = childSnapshot.Key;
                    string email = childSnapshot.Child("email").Exists ? childSnapshot.Child("email").Value.ToString() : "N/A";

                    object scoreObject = childSnapshot.Child("score").Value;
                    long score = 0;
                    if (scoreObject is long l) score = l;
                    else if (scoreObject is int i) score = i;
                    else if (scoreObject is double d) score = (long)d;

                    userScores.Add((userId, email, score));
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

                        string emailToDisplay = userScores[index].email;

                        if (userScores[index].userId == currentUserId)
                        {
                            // Thêm chuỗi đánh dấu vào tên người chơi
                            emailToDisplay += " (HẠNG CỦA BẠN)";
                        }

                        GameObject newEntry = Instantiate(LeaderboardItem_Prefab, contentParent);
                        newEntry.SetActive(true);

                        LeaderboardItem itemScript = newEntry.GetComponent<LeaderboardItem>();
                        if (itemScript != null)
                            itemScript.SetData(index + 1, emailToDisplay, (int)userScores[index].score);
                    }

                    LoadCurrentUserRank(currentUserId, currentUserEmail, userScores);
                });
            }
        }, TaskScheduler.Default);
    }

    private void LoadCurrentUserRank(string currentUserId, string currentUserEmail, System.Collections.Generic.List<(string userId, string email, long score)> topScores)
    {

        if (string.IsNullOrEmpty(currentUserId)) return;
        Transform contentParent = LeaderboardContent_Parent.transform;
        if (contentParent == null) return;

        DatabaseReference userReference = FirebaseDatabase.GetInstance(DatabaseUrl).GetReference($"users/{currentUserId}");

        userReference.Child("score").GetValueAsync().ContinueWith(task =>
        {
            if (task.IsCompleted && !task.IsFaulted && task.Result.Exists)
            {
                long currentScore = task.Result.Value is long l ? l : (task.Result.Value is int i ? (long)i : 0);
                if (currentScore == 0 && (task.Result.Value is double d)) currentScore = (long)d;

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
                            long myRank = rankSnapshot.ChildrenCount;

                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                if (contentParent == null || LeaderboardItem_Prefab == null) return;

                                bool showSeparator = (int)myRank >= TopLimit + 2;

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
                                    itemScript.SetData((int)myRank, currentUserEmail + " (HẠNG CỦA BẠN)", (int)currentScore);
                            });
                        }
                    }, TaskScheduler.Default);
            }
        }, TaskScheduler.Default);
    }
}
