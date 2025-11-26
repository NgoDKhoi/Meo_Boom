using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(HorizontalLayoutGroup))]
public class DynamicHandSpacing : MonoBehaviour
{
    [Header("Cấu hình")]
    [Tooltip("Khoảng cách mong muốn ban đầu (VD: -20 để xếp chồng nhẹ)")]
    public float targetSpacing = -20f;

    [Tooltip("Chiều rộng chính xác của 1 Prefab lá bài (Xem trong Inspector của lá bài)")]
    public float cardWidth = 100f;

    private HorizontalLayoutGroup layoutGroup;
    private RectTransform rectTransform;

    void Start()
    {
        layoutGroup = GetComponent<HorizontalLayoutGroup>();
        rectTransform = GetComponent<RectTransform>();

        // Đảm bảo cài đặt đúng cho Layout Group
        layoutGroup.childForceExpandWidth = false;
    }

    void Update()
    {
        CalculateSpacing();
    }

    void CalculateSpacing()
    {
        int count = transform.childCount;

        // Nếu có 0 hoặc 1 lá bài thì không cần tính khoảng cách
        if (count <= 1)
        {
            layoutGroup.spacing = targetSpacing;
            return;
        }

        // 1. Lấy chiều rộng khả dụng của Panel (trừ đi lề 2 bên)
        float availableWidth = rectTransform.rect.width - layoutGroup.padding.left - layoutGroup.padding.right;

        // 2. Tính tổng chiều rộng nếu dùng Spacing mặc định (-20)
        // Công thức: (Tổng chiều rộng các lá bài) + (Tổng các khoảng hở)
        float currentUsedWidth = (count * cardWidth) + ((count - 1) * targetSpacing);

        // 3. So sánh và xử lý
        if (currentUsedWidth <= availableWidth)
        {
            // Nếu vẫn còn chỗ trống: Giữ nguyên spacing mong muốn (-20)
            layoutGroup.spacing = targetSpacing;
        }
        else
        {
            // Nếu bị tràn: Tính lại Spacing để ép vừa khít khung
            // Công thức: Spacing Mới = (Chiều rộng khung - Tổng chiều rộng bài) / (Số khoảng hở)
            // Vì (Chiều rộng khung - Tổng rộng bài) sẽ ra số âm lớn, nên spacing sẽ càng âm -> bài càng chồng lên nhau
            float newSpacing = (availableWidth - (count * cardWidth)) / (count - 1);
            layoutGroup.spacing = newSpacing;
        }
    }
}