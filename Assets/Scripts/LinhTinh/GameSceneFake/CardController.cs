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
    private Vector3 originalWorldPosition; // Vị trí cũ (World Space)
    private Vector3 actualLocalScaleInHand; // Kích thước cũ (Local Scale)
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
    }

    void OnMouseDown() => HandleClickInput();
    public void OnPointerDown(PointerEventData eventData) => HandleClickInput();

    void HandleClickInput()
    {
        // Chỉ Human mới được chơi
        if (GameManager.Instance != null && GameManager.Instance.players[GameManager.Instance.currentPlayerIndex].type != GameManager.PlayerType.Human)
            return;

        // Chỉ được đánh lá defuse
        if (GameManager.Instance.IsDefusing)
        {
            if (this.cardType != DrawPileManager.CardType.Defuse)
            {
                Debug.Log("Đang dính bom! Phải chọn thẻ Defuse!");
                // Có thể thêm hiệu ứng lắc lá bài để báo lỗi ở dưới
                return;
            }
        }

        // Nếu lá đang được phóng to, thì click để thu nhỏ
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

        // LƯU KÍCH THƯỚC THỰC TẾ TRONG TAY
        actualLocalScaleInHand = transform.localScale;

        // 1. LƯU: Vị trí, thứ tự và đối tượng cha ban đầu
        originalWorldPosition = transform.position; // <--- VỊ TRÍ NÀY QUAN TRỌNG ĐỂ THU VỀ MƯỢT MÀ
        originalSiblingIndex = transform.GetSiblingIndex();
        originalParent = transform.parent;

        // 2. Tắt Layout Element
        LayoutElement le = GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = true;

        // 3. FIX Z-ORDER: Chuyển lên Panel_GamePlay
        if (CardController.canvasTransform != null)
        {
            transform.SetParent(CardController.canvasTransform, true);
            transform.SetAsLastSibling();
        }

        if (spriteRenderer != null) spriteRenderer.sortingOrder = sortingOrderWhenSelected;

        StopAllCoroutines();
        // Gọi hàm di chuyển trong World Space.
        StartCoroutine(MoveAndScaleWorldSpace(screenCenterWorldPos, actualLocalScaleInHand * zoomMultiplier));

        // DỪNG TIMER KHI CHỌN DEFUSE
        if (GameManager.Instance != null && GameManager.Instance.IsDefusing)
        {
            GameManager.Instance.CheckDefuseSelection(this.cardType, true);
        }
    }

    public void Deselect()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsDefusing)
        {
            // Phải kiểm tra lá này có phải Defuse không trước khi gọi
            if (this.cardType == DrawPileManager.CardType.Defuse)
            {
                GameManager.Instance.CheckDefuseSelection(this.cardType, false);
            }
        }

        isZoomed = false;
        CardController.selectedCard = null;

        StopAllCoroutines();
        // BẮT ĐẦU COROUTINE TRẢ VỀ VỚI ANIMATION
        StartCoroutine(MoveBackToHandWorldSpace());
    }

    // THÊM VÀO CardController.cs
    public void ForceDeselect()
    {
        if (!isZoomed) return;

        Debug.Log($"ForceDeselect: {cardType} bị bỏ chọn do hết giờ");

        isZoomed = false;
        selectedCard = null;

        // Reset về trạng thái ban đầu
        StopAllCoroutines();

        // Trả về vị trí cũ
        if (originalParent != null)
        {
            transform.SetParent(originalParent, true);
            transform.SetSiblingIndex(originalSiblingIndex);
            transform.localScale = actualLocalScaleInHand;

            LayoutElement le = GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = false;
        }

        // Reset lại sorting order
        if (spriteRenderer != null) spriteRenderer.sortingOrder = originalSortingOrder;
    }

    // ===================================================
    // CÁC COROUTINE
    // ===================================================

    // COROUTINE MỚI: Di chuyển lá bài từ giữa màn hình về vị trí ban đầu (World Space)
    IEnumerator MoveBackToHandWorldSpace()
    {
        // 1. Dùng Coroutine di chuyển/scale World Space có sẵn
        // Target: Vị trí World Space ban đầu (originalWorldPosition)
        // Scale Target: Kích thước Local Scale ban đầu (actualLocalScaleInHand)
        yield return StartCoroutine(
            MoveAndScaleWorldSpace(originalWorldPosition, actualLocalScaleInHand)
        );

        // 2. SAU KHI DI CHUYỂN XONG, GỌI LOGIC GẮN LẠI VÀO LAYOUT GROUP
        ReturnCardToHand();
    }

    // COROUTINE CŨ: Logic gắn lại vào Layout Group (KHÔNG CÒN LÀ IEnumerator NỮA)
    void ReturnCardToHand()
    {
        LayoutElement le = GetComponent<LayoutElement>();

        if (le != null) le.ignoreLayout = true;

        // 1. Gắn lá bài trở lại đối tượng cha ban đầu (Hand Area)
        transform.SetParent(originalParent, true);

        // 2. Đặt lá bài vào đúng vị trí Sibling Index ban đầu
        transform.SetSiblingIndex(originalSiblingIndex);

        // 3. KHÔI PHỤC KÍCH THƯỚC THỰC TẾ (Đã được set trong MoveBackToHandWorldSpace, chỉ đặt lại để đảm bảo)
        transform.localScale = actualLocalScaleInHand;

        // 4. BẬT LẠI Layout Element. Layout Group sẽ tính toán lại VỊ TRÍ LOCAL
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
        StartCoroutine(MoveToDiscardPile(discardPileTarget.position, GameManager.Instance.discardPileCardScale));
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

        transform.SetParent(GameManager.Instance.discardPileTransform, true);
        transform.SetAsLastSibling();
    }
}