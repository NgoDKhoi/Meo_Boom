using System.Collections;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using TMPro;
using UnityEngine.SceneManagement;

public class FirebaseAuthManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public TMP_Text statusText; 

    private FirebaseAuth auth;
    private FirebaseUser user;

    void Start()
    {
        InitializeFirebase();
        statusText.text = "TEST HIỂN THỊ";
    }

    void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                Debug.Log("Firebase đã sẵn sàng!");
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
            statusText.text = "Vui lòng nhập đầy đủ email và mật khẩu!";
            return;
        }

        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
            if (task.IsCanceled)
            {
                Debug.LogError("Đăng nhập bị huỷ.");
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError("Đăng nhập thất bại: " + task.Exception);
                statusText.text = "Email hoặc mật khẩu không đúng.";
                return;
            }

            user = task.Result.User;
            Debug.LogFormat("Đăng nhập thành công: {0} ({1})", user.DisplayName, user.Email);
            statusText.text = "Đăng nhập thành công!";
            StartCoroutine(LoadAfterLogin());
        });
    }
    IEnumerator LoadAfterLogin()
    {
        yield return new WaitForSeconds(1f); // đợi 1 giây cho người dùng đọc thông báo
        SceneManager.LoadScene("LoadRoomScene");
    }

    public void OnRegisterButtonClicked()
    {
        string email = emailInput.text;
        string password = passwordInput.text;

        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Đăng ký thất bại: " + task.Exception);
                statusText.text = "Đăng ký thất bại, thử lại!";
                return;
            }

            user = task.Result.User;
            Debug.LogFormat("Tạo tài khoản thành công: {0}", user.Email);
            statusText.text = "Đăng ký thành công!";
        });
    }
}

