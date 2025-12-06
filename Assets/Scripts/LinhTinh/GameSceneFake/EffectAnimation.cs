using UnityEngine;
using System.Collections;

// Script chung để điều khiển các hiệu ứng Visual (FX)
public class EffectAnimation : MonoBehaviour
{
    // Cấu hình trong Inspector
    [Tooltip("Thời gian tồn tại tối đa (thường bằng độ dài animation)")]
    public float effectDuration = 1.0f;

    // Tùy chọn: Nếu muốn di chuyển (Attack, Defuse bay về Discard,...)
    private bool isMoving = false;
    private Vector3 targetPosition;

    // Tốc độ di chuyển
    public float moveSpeed = 5f;

    void Start()
    {
        // 1. Nếu có Animator, nó sẽ tự động chạy Animation Flipbook

        // 2. Tự hủy GameObject sau khi animation kết thúc
        if (effectDuration > 0)
        {
            Destroy(gameObject, effectDuration);
        }

        // 3. Nếu không phải là hiệu ứng di chuyển (isMoving=false) thì dừng ở đây
    }

    // Hàm Public để GameManager gọi khi muốn hiệu ứng bay từ A -> B
    public void StartMoveToTarget(Vector3 target, float duration)
    {
        isMoving = true;
        targetPosition = target;
        // Cập nhật duration để đảm bảo hiệu ứng tồn tại đủ lâu cho chuyển động
        effectDuration = duration;

        // Bắt đầu Coroutine Di chuyển
        StartCoroutine(MoveToPosition(transform, target, duration));
    }

    // Coroutine Di chuyển (sử dụng Lerp giống như trong GameManager)
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
        // Đảm bảo đến đúng vị trí cuối cùng
        obj.position = target;

        // (Tùy chọn) Gọi Destroy ngay khi đến đích nếu cần
        // Nếu không gọi, nó sẽ tự hủy sau effectDuration (đã được set bằng duration)
    }
}