using UnityEngine;
using UnityEngine.UI;

public class OnlinePlayButtonHandler : MonoBehaviour
{
    public static OnlinePlayButtonHandler Instance;

    [Header("--- UI References ---")]
    public Button playButton; // Kéo Button Đánh Bài vào đây

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (playButton != null)
        {
            // Gán sự kiện click cho nút
            playButton.onClick.AddListener(OnPlayButtonClicked);

            // Mặc định lúc đầu chưa chọn bài thì nút sẽ tắt
            playButton.interactable = false;
        }
    }

    public void OnPlayButtonClicked()
    {
        // 1. Kiểm tra xem có đang chọn lá bài nào không
        if (OnlineCardController.SelectedCard == null)
        {
            Debug.Log("<color=orange>[UI]</color> Vui lòng chọn một lá bài trước!");
            return;
        }

        // --- CHÈN VÀO ĐÂY: Tiếng đánh bài cho chính bạn nghe ---
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.playCardSound);
        // ------------------------------------------------------

        // 2. Kiểm tra xem có đúng lượt không (Bảo mật thêm một lớp nữa)
        if (OnlineGameLogic.Instance != null && !OnlineGameLogic.Instance.IsMyTurn())
        {
            Debug.Log("<color=red>[UI]</color> Không phải lượt của bạn!");
            return;
        }

        // 3. Lấy thông tin lá bài đang chọn
        OnlineCardController currentSelected = OnlineCardController.SelectedCard;

        Debug.Log($"<color=green>[Action]</color> Đang đánh lá: {currentSelected.cardType}");

        // 4. Gửi lệnh đánh bài lên hệ thống Online
        // Lưu ý: Đảm bảo trong Scene đã có GameObject gắn script OnlineGameActionManager
        if (OnlineGameActionManager.Instance != null)
        {
            OnlineGameActionManager.Instance.RequestPlayCard(currentSelected.cardType, currentSelected.gameObject.name);
        }
        else
        {
            Debug.LogError("Thiếu OnlineGameActionManager trong Scene!");
        }

        // 5. Xóa lá bài khỏi tay người chơi (Visual)
        Destroy(currentSelected.gameObject);

        // 6. Reset biến static SelectedCard về null
        OnlineCardController.SelectedCard = null;

        // 7. Cập nhật lại trạng thái nút (Sẽ tự động khóa lại vì SelectedCard giờ là null)
        if (OnlineGameLogic.Instance != null)
        {
            OnlineGameLogic.Instance.UpdateTurnUI();
        }
    }
}