using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardController : MonoBehaviour, IPointerDownHandler
{
    public DrawPileManager.CardType cardType;
    public static CardController selectedCard = null;

    private SpriteRenderer spriteRenderer;
    private CanvasGroup canvasGroup;
    private int originalSortingOrder;

    public static Transform canvasTransform;

    // LƯU TRẠNG THÁI NGAY TRƯỚC KHI ZOOM
    private Vector3 originalWorldPosition;
    private Vector3 actualLocalScaleInHand;
    private int originalSiblingIndex;
    private Transform originalParent;

    private bool isZoomed = false;
    private Vector3 screenCenterWorldPos;

    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_TIME = 0.3f;

    [Header("Thiet lap")]
    public float zoomMultiplier = 8.0f;
    public float moveSpeed = 4f;
    public int sortingOrderWhenSelected = 100;
    public int sortingOrderWhenPlayed = 10;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) originalSortingOrder = spriteRenderer.sortingOrder;

        Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, Camera.main.nearClipPlane + 10);
        screenCenterWorldPos = Camera.main.ScreenToWorldPoint(screenCenter);
        screenCenterWorldPos.z = 0;

        UpdateVisual();
    }

    public void UpdateVisual()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        // Logic cập nhật sprite cũ của bạn giữ nguyên ở đây
        Debug.Log($"<color=cyan>[CardController]</color> Đã cập nhật hiển thị cho: {cardType}");
    }

    void OnMouseDown() => HandleClickInput();
    public void OnPointerDown(PointerEventData eventData) => HandleClickInput();

    void HandleClickInput()
    {
        // --- KIỂM TRA LƯỢT ONLINE (NẾU CÓ MANAGER ONLINE) ---
        if (OnlineDrawManager.Instance != null)
        {
            if (!OnlineDrawManager.Instance.IsMyTurn())
            {
                Debug.Log("<color=red>Chưa đến lượt Online!</color>");
                return;
            }
        }
        // --- LOGIC OFFLINE CŨ ---
        else if (GameManager.Instance != null && GameManager.Instance.players[GameManager.Instance.currentPlayerIndex].type != GameManager.PlayerType.Human)
            return;

        // Xử lý khi đang dính bom (Giữ nguyên logic offline)
        if (GameManager.Instance != null && GameManager.Instance.IsDefusing)
        {
            if (this.cardType != DrawPileManager.CardType.Defuse)
            {
                Debug.Log("Đang dính bom! Phải chọn thẻ Defuse!");
                return;
            }
        }

        if (isZoomed)
        {
            Deselect();
        }
        else
        {
            float timeSinceLastClick = Time.time - lastClickTime;
            if (timeSinceLastClick <= DOUBLE_CLICK_TIME)
            {
                if (selectedCard != null && selectedCard != this) selectedCard.Deselect();
                Select();
            }
            lastClickTime = Time.time;
        }
    }

    private void Select()
    {
        isZoomed = true;
        selectedCard = this;
        actualLocalScaleInHand = transform.localScale;
        originalWorldPosition = transform.position;
        originalSiblingIndex = transform.GetSiblingIndex();
        originalParent = transform.parent;

        LayoutElement le = GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = true;

        if (CardController.canvasTransform != null)
        {
            transform.SetParent(CardController.canvasTransform, true);
            transform.SetAsLastSibling();
        }

        if (spriteRenderer != null) spriteRenderer.sortingOrder = sortingOrderWhenSelected;

        StopAllCoroutines();
        StartCoroutine(MoveAndScaleWorldSpace(screenCenterWorldPos, actualLocalScaleInHand * zoomMultiplier));

        if (GameManager.Instance != null && GameManager.Instance.IsDefusing)
        {
            GameManager.Instance.CheckDefuseSelection(this.cardType, true);
        }
    }

    public void Deselect()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsDefusing)
        {
            if (this.cardType == DrawPileManager.CardType.Defuse)
            {
                GameManager.Instance.CheckDefuseSelection(this.cardType, false);
            }
        }

        isZoomed = false;
        if (CardController.selectedCard == this) CardController.selectedCard = null;
        StopAllCoroutines();
        StartCoroutine(MoveBackToHandWorldSpace());
    }

    // ================================================================
    // HÀM MỚI: THỰC THI ĐÁNH BÀI ONLINE
    // ================================================================
    public void ExecuteOnlinePlay()
    {
        if (OnlineGameActionManager.Instance != null)
        {
            // 1. Gửi lệnh lên Firebase
            OnlineGameActionManager.Instance.RequestPlayCard(cardType, gameObject.name);

            // 2. Hiệu ứng biến mất hoặc bay đi (Tùy bạn chọn, ở đây ta Destroy)
            // Nếu bạn muốn nó bay vào đống rác Online, ta sẽ xử lý ở OnlineGameActionManager sau
            Destroy(gameObject);
            selectedCard = null;
        }
    }

    public void ForceDeselect()
    {
        if (!isZoomed) return;
        isZoomed = false;
        selectedCard = null;
        StopAllCoroutines();

        if (originalParent != null)
        {
            transform.SetParent(originalParent, true);
            transform.SetSiblingIndex(originalSiblingIndex);
            transform.localScale = actualLocalScaleInHand;
            LayoutElement le = GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = false;
        }

        if (spriteRenderer != null) spriteRenderer.sortingOrder = originalSortingOrder;
    }

    IEnumerator MoveBackToHandWorldSpace()
    {
        yield return StartCoroutine(MoveAndScaleWorldSpace(originalWorldPosition, actualLocalScaleInHand));
        ReturnCardToHand();
    }

    void ReturnCardToHand()
    {
        LayoutElement le = GetComponent<LayoutElement>();
        transform.SetParent(originalParent, true);
        transform.SetSiblingIndex(originalSiblingIndex);
        transform.localScale = actualLocalScaleInHand;
        if (le != null) le.ignoreLayout = false;
    }

    IEnumerator MoveAndScaleWorldSpace(Vector3 targetWorldPos, Vector3 targetWorldScale)
    {
        Vector3 startWorldPos = transform.position;
        Vector3 startWorldScale = transform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed;
            transform.position = Vector3.Lerp(startWorldPos, targetWorldPos, t);
            transform.localScale = Vector3.Lerp(startWorldScale, targetWorldScale, t);
            yield return null;
        }
        transform.position = targetWorldPos;
        transform.localScale = targetWorldScale;
    }

    public void PlayCard(Transform discardPileTarget)
    {
        isZoomed = false;
        CardController.selectedCard = null;
        LayoutElement le = GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = true;
        if (spriteRenderer != null) spriteRenderer.sortingOrder = sortingOrderWhenPlayed;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        StopAllCoroutines();
        // Kiểm tra GameManager để tránh lỗi null khi chơi Online
        Vector3 targetScale = (GameManager.Instance != null) ? GameManager.Instance.discardPileCardScale : new Vector3(0.5f, 0.5f, 1);
        StartCoroutine(MoveToDiscardPile(discardPileTarget.position, targetScale));
    }

    IEnumerator MoveToDiscardPile(Vector3 targetPos, Vector3 targetScale)
    {
        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;
        float duration = 0.3f;
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        transform.position = targetPos;
        transform.localScale = targetScale;

        if (GameManager.Instance != null)
        {
            transform.SetParent(GameManager.Instance.discardPileTransform, true);
        }
        transform.SetAsLastSibling();
    }
}