using UnityEngine;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System;

public class LoadUsernameFromFirebase : MonoBehaviour
{
    public static LoadUsernameFromFirebase Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        DontDestroyOnLoad(gameObject);
    }

    /// Lấy username và gọi hàm onComplete SAU KHI DỮ LIỆU ĐƯỢC GÁN.
    /// <param name="user">Người dùng Firebase hiện tại</param>
    /// <param name="onComplete">Hàm Callback sẽ chạy khi username đã được lưu vào GameDataManager.</param>
    public void FetchUsername(FirebaseUser user, Action onComplete)
    {
        if (user == null || FirebaseManager.Instance == null || FirebaseManager.Instance.Database == null)
        {
            Debug.LogWarning("❌ Firebase/User chưa sẵn sàng. Gọi callback.");
            if (onComplete != null) onComplete();
            return;
        }

        string userId = user.UserId;
        var dbRef = FirebaseManager.Instance.Database.RootReference
                            .Child("users").Child(userId);

        dbRef.GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || !task.Result.Exists || !task.Result.Child("username").Exists)
            {
                Debug.LogError("❌ Lỗi/Không tìm thấy username: " + (task.IsFaulted ? task.Exception.ToString() : "Data not found"));
            }
            else
            {
                // Gán username thành công
                GameDataManager.Instance.username = task.Result.Child("username").Value.ToString();
                GameDataManager.Instance.userUID = userId;
                Debug.Log(" Username đã được lưu vào GameDataManager: " + GameDataManager.Instance.username);
            }

            // Gọi Callback để script Login biết đã xong
            if (onComplete != null)
            {
                onComplete();
            }
        });
    }
}