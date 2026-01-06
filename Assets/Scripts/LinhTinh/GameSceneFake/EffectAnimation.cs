using UnityEngine;
using System.Collections;

public class EffectAnimation : MonoBehaviour
{
    [Tooltip("Thời gian tồn tại tối đa")]
    public float effectDuration = 1.0f;

    [Header("Sound Settings")]
    [Tooltip("Bắt đầu phát từ giây thứ mấy của file Sound?")]
    public float soundOffset = 0f;

    private AudioSource audioSource;
    private bool isMoving = false;
    private Vector3 targetPosition;
    public float moveSpeed = 5f;

    void Awake()
    {
        // Lấy AudioSource ngay khi Prefab được tạo ra
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (effectDuration > 0)
        {
            Destroy(gameObject, effectDuration);
        }
    }

    // --- HÀM QUAN TRỌNG ĐỂ ĐỒNG BỘ ---
    // Hàm này sẽ được gọi từ Animation Event
    public void TriggerEffectSound()
    {
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.time = soundOffset; // Nhảy đến đoạn âm thanh "lực" nhất
            audioSource.Play();
        }
    }

    public void StartMoveToTarget(Vector3 target, float duration)
    {
        isMoving = true;
        targetPosition = target;
        effectDuration = duration;
        StartCoroutine(MoveToPosition(transform, target, duration));
    }

    IEnumerator MoveToPosition(Transform obj, Vector3 target, float duration)
    {
        float time = 0;
        Vector3 start = obj.position;
        while (time < duration)
        {
            time += Time.deltaTime;
            obj.position = Vector3.Lerp(start, target, time / duration);
            yield return null;
        }
        obj.position = target;
    }
}