using UnityEngine;
using UnityEngine.UI;
using Firebase.Auth;
using System.Collections;
using System;

public class RoomSceneLoadManager : MonoBehaviour
{
    // Tham chiếu tới các nút cần khóa
    public Button createRoomButton;
    public Button joinRoomButton;
    private void Start()
    {
        // 1. Khóa các nút ngay lập tức
        SetButtonsActive(false);

        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;

        if (user != null)
        {
            // 2. Kiểm tra và bắt đầu tải username (nếu chưa có)
            if (string.IsNullOrEmpty(GameDataManager.Instance.username))
            {
                // Gọi FetchUsername và truyền null cho tham số onComplete
                LoadUsernameFromFirebase.Instance.FetchUsername(user, null);

                // Bắt đầu chờ đợi Coroutine
                StartCoroutine(WaitForUsernameReady());
            }
            else
            {
                // Username đã có sẵn, kích hoạt ngay
                SetButtonsActive(true);
            }
        }
        else
        {
            Debug.LogError("Người dùng không đăng nhập. Quay lại Login.");
            UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScene");
        }
    }

    private void SetButtonsActive(bool active)
    {
        // Chỉ điều khiển tương tác của nút (interactable), không cần gọi SetActive cho loading indicator
        if (createRoomButton != null) createRoomButton.interactable = active;
        if (joinRoomButton != null) joinRoomButton.interactable = active;
    }

    // Coroutine chờ đợi username
    private IEnumerator WaitForUsernameReady()
    {
        float timeout = 10f;
        float startTime = Time.time;

        while (string.IsNullOrEmpty(GameDataManager.Instance.username) && Time.time < startTime + timeout)
        {
            yield return null; 
        }

        if (!string.IsNullOrEmpty(GameDataManager.Instance.username))
        {
            Debug.Log("✅ Username đã tải xong! Kích hoạt nút.");
            SetButtonsActive(true);
        }
        else
        {
            Debug.LogError("❌ Lỗi Timeout: Tải username thất bại sau 10s.");
        }
    }
}