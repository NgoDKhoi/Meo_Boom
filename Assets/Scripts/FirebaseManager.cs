using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections;
using System.Threading.Tasks;
using System; // Cần thêm System để sử dụng Uri

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

    public FirebaseAuth Auth { get; private set; }
    public FirebaseDatabase Database { get; private set; }

    private const string DATABASE_URL = "https://mygametest2-default-rtdb.asia-southeast1.firebasedatabase.app";
    public bool isFirebaseReady = false;

    // THÔNG TIN CẤU HÌNH DÀNH CHO UNITY EDITOR (Lấy từ google-services.json)
    private const string WEB_API_KEY = "AIzaSyCKV7K-qbIfhv8HiWsDKwXZoQpzvLJlre0"; // <-- API Key của bạn
    private const string PROJECT_ID = "mygametest2"; // <-- Project ID của bạn

    // Thời gian chờ tối đa cho việc kiểm tra phụ thuộc (5 giây)
    private const int INITIALIZATION_TIMEOUT_SECONDS = 5;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // KHỞI TẠO BẰNG COROUTINE để xử lý timeout an toàn hơn
            StartCoroutine(InitializeFirebaseCoroutine());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Thay thế InitializeFirebase() bằng Coroutine
    private IEnumerator InitializeFirebaseCoroutine()
    {
        Debug.Log("Đang kiểm tra và khởi tạo Firebase...");

        // 1. Khởi tạo Firebase App nếu đang chạy trong Unity Editor và chưa được tạo
        if (FirebaseApp.DefaultInstance == null)
        {
            // TẠO CẤU HÌNH RÕ RÀNG TRONG EDITOR VỚI THÔNG TIN BẠN CUNG CẤP
            AppOptions options = new AppOptions
            {
                ApiKey = WEB_API_KEY,
                AppId = "1:296390677235:android:8927440e41755b99a01d86", // Mobilesdk_app_id
                // SỬA LỖI CS0029: Chuyển chuỗi thành đối tượng System.Uri
                DatabaseUrl = new Uri(DATABASE_URL),
                ProjectId = PROJECT_ID
            };
            FirebaseApp.Create(options);
            Debug.Log("FirebaseApp đã được tạo với cấu hình tùy chỉnh cho Editor.");
        }


        var dependencyTask = FirebaseApp.CheckAndFixDependenciesAsync();

        // Chờ task hoàn thành hoặc hết thời gian chờ
        float startTime = Time.time;
        while (!dependencyTask.IsCompleted)
        {
            if (Time.time - startTime > INITIALIZATION_TIMEOUT_SECONDS)
            {
                Debug.LogError("LỖI KHỞI TẠO: Kiểm tra phụ thuộc Firebase đã hết thời gian chờ (5 giây). Vui lòng kiểm tra kết nối mạng hoặc cấu hình Firebase.");
                isFirebaseReady = false;
                yield break; // Ngừng Coroutine
            }
            yield return null; // Chờ 1 frame
        }

        // --- Xử lý kết quả Task sau khi hoàn thành ---

        if (dependencyTask.IsFaulted)
        {
            Debug.LogError($"LỖI NGHIÊM TRỌNG: Kiểm tra phụ thuộc Firebase bị lỗi: {dependencyTask.Exception}");
            isFirebaseReady = false;
            yield break;
        }

        var dependencyStatus = dependencyTask.Result;
        if (dependencyStatus == DependencyStatus.Available)
        {
            // 2. Khởi tạo các dịch vụ
            Auth = FirebaseAuth.DefaultInstance;

            try
            {
                // Đối với GetInstance, chúng ta vẫn dùng chuỗi
                Database = FirebaseDatabase.GetInstance(DATABASE_URL);
                Database.SetPersistenceEnabled(false);
                isFirebaseReady = true;
                Debug.Log("Firebase services initialized successfully! (Sẵn sàng: True)");
            }
            catch (System.Exception e)
            {
                Debug.LogError("LỖI KHỞI TẠO DATABASE: " + e.Message);
                isFirebaseReady = false;
            }
        }
        else
        {
            Debug.LogError($"LỖI NGHIÊM TRỌNG: Không thể giải quyết các phụ thuộc của Firebase: {dependencyStatus}");
            isFirebaseReady = false;
        }
    }


    /// <summary>
    /// Cho phép các script khác gọi StartCoroutine(FirebaseManager.Instance.WaitUntilReady()) 
    /// để chờ Firebase sẵn sàng trước khi thực hiện các tác vụ Auth/DB.
    /// </summary>
    public IEnumerator WaitUntilReady()
    {
        // Vòng lặp chờ isFirebaseReady = true.
        while (!isFirebaseReady)
        {
            yield return null;
        }
    }
}
