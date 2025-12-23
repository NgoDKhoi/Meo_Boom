using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class OnlineCardController : MonoBehaviour, IPointerClickHandler
{
    public DrawPileManager.CardType cardType;
    public static OnlineCardController SelectedCard;

    [Header("--- Cấu hình Di chuyển ---")]
    public float raiseAmount = 40f;
    public float zoomScale = 1.2f;

    private Vector3 startPosition;
    private Vector3 startScale;
    private bool isSelected = false;
    private bool isZoomed = false;
    private bool hasStoredOriginalPos = false; // Cờ kiểm tra đã lưu vị trí chuẩn chưa

    // Hỗ trợ Double Click
    private float lastClickTime = 0f;
    private const float doubleClickInterval = 0.3f;

    void Awake()
    {
        startScale = transform.localScale;
    }

    // Quan trọng: Khi lá bài được kích hoạt lại (nếu dùng pooling)
    void OnEnable()
    {
        ResetCardUI();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 1. Luôn đảm bảo đã lưu vị trí gốc chuẩn từ Layout ngay khi click
        if (!hasStoredOriginalPos)
        {
            StorePosition();
        }

        float timeSinceLastClick = Time.time - lastClickTime;

        if (timeSinceLastClick <= doubleClickInterval)
        {
            ToggleZoom();
        }
        else
        {
            HandleSelection();
        }
        lastClickTime = Time.time;
    }

    private void StorePosition()
    {
        startPosition = transform.localPosition;
        hasStoredOriginalPos = true;
    }

    private void HandleSelection()
    {
        // Chỉ cho phép chọn bài nếu đang trong lượt
        if (OnlineGameLogic.Instance != null && !OnlineGameLogic.Instance.IsMyTurn()) return;

        if (SelectedCard == this)
        {
            DeselectCard();
            SelectedCard = null;
        }
        else
        {
            if (SelectedCard != null) SelectedCard.DeselectCard();

            SelectedCard = this;
            SelectCard();
        }

        // Cập nhật trạng thái nút bấm đánh bài
        if (OnlineGameLogic.Instance != null)
            OnlineGameLogic.Instance.UpdateTurnUI();
    }

    private void SelectCard()
    {
        isSelected = true;
        // Đặt vị trí nhích lên tuyệt đối dựa trên vị trí gốc đã lưu
        transform.localPosition = startPosition + new Vector3(0, raiseAmount, 0);
    }

    public void DeselectCard()
    {
        isSelected = false;
        transform.localPosition = startPosition;
        if (isZoomed) ResetZoom();
    }

    private void ToggleZoom()
    {
        isZoomed = !isZoomed;
        if (isZoomed)
        {
            transform.localScale = startScale * zoomScale;
            transform.SetAsLastSibling(); // Đưa lên trên cùng để xem
        }
        else
        {
            ResetZoom();
        }
    }

    private void ResetZoom()
    {
        isZoomed = false;
        transform.localScale = startScale;
    }

    public void ResetCardUI()
    {
        isSelected = false;
        isZoomed = false;
        hasStoredOriginalPos = false; // Reset cờ để lấy lại vị trí mới nếu cần
        transform.localScale = startScale;
    }
}