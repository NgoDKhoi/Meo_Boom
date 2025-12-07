using UnityEngine;
using System.Collections;

public class ExplodeSequence : MonoBehaviour
{
    // Kéo 2 Prefabs đã thiết lập vào đây trong Inspector của Prefab này
    public GameObject fusePrefab;       // FX_Fuse_Prefab (Animation 1)
    public GameObject explosionPrefab;  // FX_Explosion_Prefab (Animation 2)

    // Khai báo thời gian cố định cho từng animation để đảm bảo tính chính xác
    public float fuseDurationTime = 1.0f;     // << Dây cháy chạy trong 1.0 giây
    public float explosionDurationTime = 1.5f; // << Vụ nổ lớn chạy trong 1.5 giây

    void Start()
    {
        StartCoroutine(RunSequence());
    }

    IEnumerator RunSequence()
    {
        // 1. CHẠY ANIMATION DÂY CHÁY (Animation 1)
        if (fusePrefab != null)
        {
            // Vị trí/Scale đã được thiết lập trong Prefab
            GameObject fuseFX = Instantiate(fusePrefab, transform.position, Quaternion.identity, transform);

            // Tùy chọn: Đảm bảo EffectAnimation trên Prefab con cũng có duration là 1.0f 
            // nếu bạn muốn nó tự hủy đúng thời gian.

            // Chờ dây cháy hết
            yield return new WaitForSeconds(fuseDurationTime);

            // (Không cần Destroy(fuseFX) nếu script EffectAnimation của nó tự hủy)
        }

        // 2. CHẠY ANIMATION VỤ NỔ LỚN (Animation 2)
        if (explosionPrefab != null)
        {
            GameObject explosionFX = Instantiate(explosionPrefab, transform.position, Quaternion.identity, transform);

            // Tùy chọn: Đảm bảo EffectAnimation trên Prefab con cũng có duration là 1.5f.

            // Chờ vụ nổ kết thúc
            yield return new WaitForSeconds(explosionDurationTime);
        }

        // 3. Hủy GameObject quản lý sequence này
        Destroy(gameObject);
    }
}