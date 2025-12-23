using UnityEngine;
using UnityEngine.UI;

public class HandCard : MonoBehaviour
{
    [Header("Thiết lập lá bài")]
    public DrawPileManager.CardType type;
    public string cardID; // ID duy nhất của lá bài (nếu có)

    void Start()
    {
        // Tự động tìm Button và gán sự kiện nếu chưa gán trong Inspector
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(PlayThisCard);
        }
    }

    public void PlayThisCard()
    {
        if (OnlineGameActionManager.Instance != null)
        {
            Debug.Log("Đang gửi yêu cầu đánh lá: " + type);
            OnlineGameActionManager.Instance.RequestPlayCard(type, cardID);

            // Tạm thời ẩn hoặc xóa lá bài trên tay sau khi bấm (hoặc đợi server phản hồi)
            // gameObject.SetActive(false); 
        }
        else
        {
            Debug.LogError("Không tìm thấy OnlineGameActionManager Instance!");
        }
    }
}