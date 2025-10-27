using System.Collections;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using TMPro;
using UnityEngine.SceneManagement;
using Firebase.Extensions;
using UnityEngine.UI;
using Firebase.Database; 

public class FirebaseAuthManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public TMP_Text statusText;
    public GameObject notificationPanel;
    public TMP_Text notificationText;
    public Image notificationBackground;

    private FirebaseAuth auth;
    private FirebaseUser user;
    private FirebaseDatabase database;

    private async void InitializeUserInDatabase(string userId, string email)
    {
        if (database == null)
        {
            Debug.LogError("Lỗi: Firebase Database chưa được khởi tạo thành công!");
            return;
        }

        var dbReference = database.GetReference("users").Child(userId);

        var dataSnapshot = await dbReference.GetValueAsync();

        if (!dataSnapshot.Exists)
        {
            Debug.Log($"Người dùng mới {email} đang khởi tạo dữ liệu RTDB.");

            var userInitialData = new System.Collections.Generic.Dictionary<string, object>
        {
            { "email", email },
            { "score", 0L }
        };

            await dbReference.SetValueAsync(userInitialData);
            Debug.Log("Khởi tạo dữ liệu người dùng thành công.");
        }
        else
        {
            Debug.Log($"Dữ liệu người dùng {email} đã tồn tại trên RTDB.");
        }
    }

    void Start()
    {
        InitializeFirebase();
  
    }

    void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                try
                {
                    const string DATABASE_URL = "https://mygametest2-default-rtdb.asia-southeast1.firebasedatabase.app";

                    database = FirebaseDatabase.GetInstance(DATABASE_URL);

                    Debug.Log("Firebase đã sẵn sàng!");
                }
                catch (System.Exception e)
                {
                    Debug.LogError("LỖI KHỞI TẠO DATABASE: " + e.Message);
                }
            }
            else
            {
                Debug.LogError($"Không thể khởi tạo Firebase: {dependencyStatus}");
            }
        });
    }

    public void OnLoginButtonClicked()
    {
        string email = emailInput.text;
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            //statusText.text = "Vui lòng nhập đầy đủ email và mật khẩu!";
            ShowNotification("Vui lòng nhập đầy đủ email và mật khẩu!", Color.red);
            return;
        }

        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task => {
            if (task.IsCanceled)
            {
                Debug.LogError("Đăng nhập bị huỷ.");
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError("Đăng nhập thất bại: " + task.Exception);
                // statusText.text = "Email hoặc mật khẩu không đúng.";
                ShowNotification("Email hoặc mật khẩu không đúng.", Color.red);
                return;
            }

            user = task.Result.User;
            InitializeUserInDatabase(user.UserId, user.Email);
            Debug.LogFormat("Đăng nhập thành công: {0} ({1})", user.DisplayName, user.Email);
            //statusText.text = "Đăng nhập thành công!";
            ShowNotification("Đăng nhập thành công!", Color.green);
            StartCoroutine(LoadAfterLogin());
        });
    }

    IEnumerator LoadAfterLogin()
    {
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene("LoadRoomScene");
    }

    public void OnRegisterButtonClicked()
    {
        string email = emailInput.text;
        string password = passwordInput.text;

        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task => {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Đăng ký thất bại: " + task.Exception);
                //statusText.text = "Đăng ký thất bại, thử lại!";
                ShowNotification("Đăng ký thất bại, thử lại!", Color.red);
                return;
            }

            user = task.Result.User;
            
            InitializeUserInDatabase(user.UserId, user.Email);

            Debug.LogFormat("Tạo tài khoản thành công: {0}", user.Email);
            //statusText.text = "Đăng ký thành công!";
            ShowNotification("Đăng ký thành công!", Color.green);
            StartCoroutine(LoadAfterLogin());
        });
    }

    void ShowNotification(string message, Color backgroundColor, float duration = 2f)
    {
        if (notificationPanel == null || notificationText == null) return;

        notificationText.text = message;
        notificationBackground.color = backgroundColor;
        notificationPanel.SetActive(true);

        CancelInvoke(nameof(HideNotification));
        Invoke(nameof(HideNotification), duration);
    }

    void HideNotification()
    {
        notificationPanel.SetActive(false);
    }

}

