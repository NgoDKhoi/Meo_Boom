using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.Events; // Cần thiết cho Event khi hết giờ

public class TurnTimer : MonoBehaviour
{
    // Cần thiết để Game Manager có thể gọi các hàm
    public static TurnTimer Instance;

    [Header("--- UI References ---")]
    public TextMeshProUGUI timerText; // Hiển thị 00:XX

    [Header("--- Settings ---")]
    private float maxTime;
    private float currentTime;
    public float CurrentTimeValue
    {
        get { return currentTime; }
    }
    private Coroutine timerCoroutine;

    // Sự kiện được gọi khi thời gian về 0 (để GameManager xử lý)
    public UnityEvent OnTimerTimeout = new UnityEvent();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Hàm public để khởi động đồng hồ
    public void StartTimer(float duration, bool isDefuseMode)
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }

        maxTime = duration;
        currentTime = duration;

        // Cập nhật UI ngay lập tức
        UpdateTimerUI();

        // Bắt đầu đếm ngược
        timerCoroutine = StartCoroutine(CountDownRoutine(isDefuseMode));

        Debug.Log($"Timer bắt đầu: {duration} giây. Defuse Mode: {isDefuseMode}");
    }

    // Coroutine đếm ngược chính
    IEnumerator CountDownRoutine(bool isDefuseMode)
    {
        while (currentTime > 0)
        {
            yield return null; // Chờ 1 Frame

            currentTime -= Time.deltaTime; // Giảm thời gian trôi qua

            UpdateTimerUI();

            // Nếu hết giờ
            if (currentTime <= 0)
            {
                currentTime = 0;
                UpdateTimerUI();

                // Kích hoạt sự kiện để GameManager biết
                OnTimerTimeout.Invoke();

                yield break; // Kết thúc đếm ngược
            }
        }
    }

    // Hàm public để dừng đồng hồ
    public void StopTimer()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        // Ẩn UI nếu không cần thiết
        // gameObject.SetActive(false); 
        Debug.Log("Timer đã dừng.");
    }

    // Cập nhật UI hiển thị
    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            // Định dạng thời gian thành 00:XX (phút:giây)
            int seconds = Mathf.CeilToInt(currentTime);
            timerText.text = "00:" + seconds.ToString("00");
        }
    }
}