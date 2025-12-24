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

    [Header("--- UI Feedback ---")]
    public Image cardImage; // Kéo Image của lá bài vào đây
    public Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 0.8f); // Màu tối khi bị khóa

    private Vector3 startPosition;
    private Vector3 startScale;
    private bool isSelected = false;
    private bool isZoomed = false;
    private bool hasStoredOriginalPos = false;

    private float lastClickTime = 0f;
    private const float doubleClickInterval = 0.3f;

    void Awake()
    {
        startScale = transform.localScale;
        if (cardImage == null) cardImage = GetComponent<Image>();
    }

    void Update()
    {
        // Tự động cập nhật trạng thái hiển thị dựa trên logic game
        UpdateCardVisualState();
    }

    void OnEnable()
    {
        ResetCardUI();
    }

    private void UpdateCardVisualState()
    {
        if (OnlineGameActionManager.Instance == null) return;

        // Nếu ĐANG DÍNH BOM mà lá này KHÔNG PHẢI DEFUSE
        if (OnlineGameActionManager.Instance.isWaitingForDefuse)
        {
            if (cardType != DrawPileManager.CardType.Defuse)
            {
                cardImage.color = lockedColor;
            }
            else
            {
                // Lá Defuse thì làm nổi bật lên
                cardImage.color = Color.white;
            }
        }
        else
        {
            // Trạng thái bình thường
            cardImage.color = Color.white;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // KHÓA CLICK: Nếu đang dính bom mà click vào bài thường thì không xử lý
        if (OnlineGameActionManager.Instance != null && OnlineGameActionManager.Instance.isWaitingForDefuse)
        {
            if (cardType != DrawPileManager.CardType.Defuse)
            {
                Debug.Log("Lá bài này đang bị khóa!");
                return;
            }
        }

        if (!hasStoredOriginalPos) StorePosition();

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

        if (OnlineGameLogic.Instance != null)
            OnlineGameLogic.Instance.UpdateTurnUI();
        OnlineGameLogic.Instance.OnCardSelectionChanged();
    }

    private void SelectCard()
    {
        isSelected = true;
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
            transform.SetAsLastSibling();
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
        hasStoredOriginalPos = false;
        transform.localScale = startScale;
        if (cardImage != null) cardImage.color = Color.white;
    }
}