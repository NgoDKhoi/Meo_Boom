using UnityEngine;
using UnityEngine.UI; // 1. BẮT BUỘC PHẢI CÓ dòng này để dùng Button

public class GameManager : MonoBehaviour
{
    [Header("Cấu hình UI")]
    public Button btnPlayCard; // 2. Biến để kéo nút Đánh bài vào

    // 3. Hàm Update chạy liên tục mỗi khung hình
    void Update()
    {
        if (btnPlayCard != null)
        {
            // Nếu có lá bài đang chọn (selectedCard != null) -> Nút sáng lên (true)
            // Nếu không có lá bài nào (selectedCard == null) -> Nút xám đi (false)
            btnPlayCard.interactable = (Card_Controller.selectedCard != null);
        }
    }

    // Ham nay se duoc goi boi Button 'Danh Bai'
    public DeckManager deckManager; // Kéo DeckManager vào đây

    public void PlaySelectedCard()
    {
        if (Card_Controller.selectedCard != null)
        {
            // Gọi sang DeckManager để xử lý toàn bộ logic
            deckManager.PlayCardAction(Card_Controller.selectedCard);

            // Reset trạng thái chọn
            Card_Controller.selectedCard = null;

            // Tắt nút đánh
            if (btnPlayCard != null) btnPlayCard.interactable = false;
        }
        else
        {
            Debug.Log("Chưa chọn bài!");
        }
    }
}
