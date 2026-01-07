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
        // 1. CHẠY ANIMATION DÂY CHÁY
        if (fusePrefab != null)
        {
            GameObject fuseFX = Instantiate(fusePrefab, transform.position, Quaternion.identity, transform);

            // ÉP SCALE CỦA CON THEO CHA (Đảm bảo chắc chắn nó nhỏ)
            fuseFX.transform.localScale = new Vector3(4f, 4f, 4f);
            // Vì đã là con của Master (0.25), nên gán localScale = 1 
            // thì kích thước thực tế của nó sẽ là 0.25 so với thế giới.

            yield return new WaitForSeconds(fuseDurationTime);
        }

        // 2. CHẠY ANIMATION VỤ NỔ LỚN
        if (explosionPrefab != null)
        {
            GameObject explosionFX = Instantiate(explosionPrefab, transform.position, Quaternion.identity, transform);

            // TƯƠNG TỰ VỚI VỤ NỔ
            explosionFX.transform.localScale = new Vector3(18f, 12f, 12f);

            yield return new WaitForSeconds(explosionDurationTime);
        }

        // 3. Hủy GameObject quản lý sequence này
        Destroy(gameObject);
    }
}