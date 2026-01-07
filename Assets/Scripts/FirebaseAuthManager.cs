using System.Collections;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using TMPro;
using UnityEngine.SceneManagement;
using Firebase.Extensions;
using UnityEngine.UI;
using Firebase.Database;
using System.Text.RegularExpressions; // Thêm để kiểm tra định dạng email

public class FirebaseAuthManager : MonoBehaviour
{
    [Header("UI References - Login")]
    public TMP_InputField loginEmailInput; // Đổi tên để phân biệt với Đăng ký
    public TMP_InputField loginPasswordInput; // Đổi tên để phân biệt với Đăng ký

    [Header("UI References - Register")]
    public TMP_InputField registerEmailInput;
    public TMP_InputField registerUsernameInput; // Giả sử có thêm trường Username
    public TMP_InputField registerPasswordInput;
    public TMP_InputField registerConfirmPasswordInput;

    [Header("UI References - Forgot Password")]
    public TMP_InputField resetPasswordEmailInput;

    [Header("Notification UI")]
    public TMP_Text statusText; // Không dùng nhưng giữ lại để phòng trường hợp
    public GameObject notificationPanel;
    public TMP_Text notificationText;
    public Image notificationBackground;

    private FirebaseAuth auth;
    private FirebaseUser user;
    private FirebaseDatabase database;

    // Tham chiếu đến UI Manager để điều hướng (nên được khai báo)
    public UI_Manager uiManager;

    private const string DATABASE_URL = "https://mygametest2-default-rtdb.asia-southeast1.firebasedatabase.app";
    private const string LOAD_ROOM_SCENE_NAME = "LoadRoomScene";

    // Regex đơn giản để kiểm tra định dạng email
    private readonly Regex emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");


    private async void InitializeUserInDatabase(string userId, string email, string username = "Player")
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
                { "username", username }, // Lưu thêm Username
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

    // Hàm xử lý ĐĂNG NHẬP
    public void OnLoginButtonClicked()
    {
        string email = loginEmailInput.text;
        string password = loginPasswordInput.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowNotification("Vui lòng nhập đầy đủ email và mật khẩu!", Color.red);
            return;
        }

        if (!emailRegex.IsMatch(email))
        {
            ShowNotification("Email không đúng định dạng!", Color.red);
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
                string errorMessage = task.Exception.InnerExceptions[0].Message;
                // Kiểm tra lỗi để hiển thị thông báo thân thiện hơn
                if (errorMessage.Contains("wrong-password") || errorMessage.Contains("user-not-found"))
                {
                    ShowNotification("Email hoặc mật khẩu không đúng.", Color.red);
                }
                else
                {
                    ShowNotification("Đăng nhập thất bại: " + errorMessage, Color.red);
                }
                Debug.LogError("Đăng nhập thất bại: " + task.Exception);
                return;
            }

            user = task.Result.User;
            InitializeUserInDatabase(user.UserId, user.Email);
            Debug.LogFormat("Đăng nhập thành công: {0} ({1})", user.DisplayName, user.Email);
            ShowNotification("Đăng nhập thành công!", Color.green);
            StartCoroutine(LoadAfterLogin());
        });
    }

    // Hàm xử lý ĐĂNG KÝ (MỚI)
    public void OnRegisterButtonClicked()
    {
        string email = registerEmailInput.text;
        string username = registerUsernameInput.text;
        string password = registerPasswordInput.text;
        string confirmPassword = registerConfirmPasswordInput.text;

        // 1. Kiểm tra đầu vào
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
        {
            ShowNotification("Vui lòng điền đầy đủ thông tin!", Color.red);
            return;
        }

        if (password != confirmPassword)
        {
            ShowNotification("Mật khẩu và Xác nhận mật khẩu không khớp!", Color.red);
            return;
        }

        if (password.Length < 6)
        {
            ShowNotification("Mật khẩu phải có ít nhất 6 ký tự!", Color.red);
            return;
        }

        if (!emailRegex.IsMatch(email))
        {
            ShowNotification("Email không đúng định dạng!", Color.red);
            return;
        }

        // 2. Gọi API Đăng ký
        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task => {
            if (task.IsCanceled || task.IsFaulted)
            {
                string errorMessage = task.Exception.InnerExceptions[0].Message;
                if (errorMessage.Contains("email-already-in-use"))
                {
                    ShowNotification("Email này đã được sử dụng. Vui lòng chọn Email khác!", Color.red);
                }
                else
                {
                    ShowNotification("Đăng ký thất bại: " + errorMessage, Color.red);
                }
                Debug.LogError("Đăng ký thất bại: " + task.Exception);
                return;
            }

            // 3. Đăng ký thành công
            user = task.Result.User;

            // Cập nhật tên hiển thị trong Firebase Auth
            UserProfile profile = new UserProfile { DisplayName = username };
            user.UpdateUserProfileAsync(profile).ContinueWithOnMainThread(updateTask =>
            {
                if (updateTask.IsFaulted)
                {
                    Debug.LogError("Lỗi cập nhật Display Name: " + updateTask.Exception);
                }

                // Khởi tạo data trên Realtime Database
                InitializeUserInDatabase(user.UserId, user.Email, username);

                Debug.LogFormat("Tạo tài khoản thành công: {0} ({1})", user.Email, username);
                ShowNotification("Đăng ký thành công!", Color.green);
                StartCoroutine(LoadAfterLogin());
            });
        });
    }

    // Hàm xử lý QUÊN MẬT KHẨU (MỚI)
    public void OnResetPasswordClicked()
    {
        string email = resetPasswordEmailInput.text;

        if (string.IsNullOrEmpty(email))
        {
            ShowNotification("Vui lòng nhập Email để gửi liên kết đặt lại mật khẩu.", Color.red);
            return;
        }

        if (!emailRegex.IsMatch(email))
        {
            ShowNotification("Email không đúng định dạng!", Color.red);
            return;
        }

        auth.SendPasswordResetEmailAsync(email).ContinueWithOnMainThread(task => {
            if (task.IsCanceled || task.IsFaulted)
            {
                string errorMessage = task.Exception.InnerExceptions[0].Message;
                if (errorMessage.Contains("user-not-found"))
                {
                    ShowNotification("Không tìm thấy tài khoản với email này.", Color.red);
                }
                else
                {
                    ShowNotification("Lỗi gửi liên kết đặt lại mật khẩu: " + errorMessage, Color.red);
                }
                Debug.LogError("Lỗi gửi email reset password: " + task.Exception);
                return;
            }

            ShowNotification($"Đã gửi liên kết đặt lại mật khẩu đến Email: {email}", Color.yellow);
            // Quay lại màn hình Đăng nhập sau khi gửi thành công
            if (uiManager != null)
            {
                uiManager.ShowEmailLogin();
            }
        });
    }

    IEnumerator LoadAfterLogin()
    {
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene(LOAD_ROOM_SCENE_NAME);
    }

    void ShowNotification(string message, Color backgroundColor, float duration = 2f)
    {
        if (notificationPanel == null || notificationText == null) return;

        notificationText.text = message;

        if (notificationBackground != null)
        {
            notificationBackground.color = backgroundColor;
        }

        notificationPanel.SetActive(true);

        // --- PHẦN THÊM VÀO: PHÁT ÂM THANH THEO MÀU SẮC ---
        if (AudioManager.Instance != null)
        {
            if (backgroundColor == Color.green)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.successSound);
            }
            else if (backgroundColor == Color.red)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.failureSound);
            }
            else if (backgroundColor == Color.yellow)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.successSound);
            }
        }

        CancelInvoke(nameof(HideNotification));
        Invoke(nameof(HideNotification), duration);
    }

    void HideNotification()
    {
        notificationPanel.SetActive(false);
    }

}
