using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.UI; // Cần thiết để thao tác với Layout

public class Card_Controller : MonoBehaviour, IPointerDownHandler
{
    public DeckManager.CardType cardType;
    public static Card_Controller selectedCard = null;

    private SpriteRenderer spriteRenderer;
    private CanvasGroup canvasGroup; // Dùng cái này để chặn raycast khi đang bay
    private int originalSortingOrder;

    // Lưu trạng thái ngay trước khi zoom
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private int originalSiblingIndex; // Quan trọng để trả về đúng khe trong Hand

    private bool isZoomed = false;
    private Vector3 screenCenterWorldPos;

    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_TIME = 0.3f;

    [Header("Thiet lap")]
    public float zoomMultiplier = 1.8f; // Giảm xuống chút cho vừa màn hình
    public float moveSpeed = 10f;       // Tăng tốc độ cho mượt
    public int sortingOrderWhenSelected = 100;
    public int sortingOrderWhenPlayed = 10;
    public Vector3 playPosition = new Vector3(3.05f, -0.13f, 0f);

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) originalSortingOrder = spriteRenderer.sortingOrder;

        originalScale = transform.localScale;

        // Tính tâm màn hình
        Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, Camera.main.nearClipPlane + 10);
        screenCenterWorldPos = Camera.main.ScreenToWorldPoint(screenCenter);
        screenCenterWorldPos.z = 0;
    }

    // Hỗ trợ cả Click vào Collider (Sprite) và Click vào UI
    void OnMouseDown() => HandleClickInput();
    public void OnPointerDown(PointerEventData eventData) => HandleClickInput();

    void HandleClickInput()
    {
        if (isZoomed)
        {
            Deselect();
            selectedCard = null;
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

        // 1. QUAN TRỌNG: Lưu vị trí HIỆN TẠI (chứ không phải vị trí hồi Start)
        originalPosition = transform.position;
        originalSiblingIndex = transform.GetSiblingIndex(); // Lưu thứ tự trong Layout Group

        // 2. Tạm thời tắt Layout Element (nếu có) để lá bài không bị Layout Group giật lại
        LayoutElement le = GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = true;

        // 3. Đưa lên lớp trên cùng để không bị che
        if (spriteRenderer != null) spriteRenderer.sortingOrder = sortingOrderWhenSelected;
        transform.SetAsLastSibling(); // Đưa xuống cuối danh sách con để vẽ lên trên cùng trong UI

        StopAllCoroutines();
        StartCoroutine(MoveAndScale(screenCenterWorldPos, originalScale * zoomMultiplier));
    }

    public void Deselect()
    {
        isZoomed = false;

        // Trả về Order cũ cho Sprite
        if (spriteRenderer != null) spriteRenderer.sortingOrder = originalSortingOrder;

        StopAllCoroutines();
        // Bay về vị trí cũ, sau khi bay xong thì trả lại vào Layout
        StartCoroutine(MoveBackToHand());
    }

    IEnumerator MoveBackToHand()
    {
        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;

        // Bay về
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed;
            transform.position = Vector3.Lerp(startPos, originalPosition, t);
            transform.localScale = Vector3.Lerp(startScale, originalScale, t);
            yield return null;
        }

        // Đảm bảo thông số chuẩn xác
        transform.position = originalPosition;
        transform.localScale = originalScale;

        // 4. QUAN TRỌNG: Trả lại vào Layout Group
        transform.SetSiblingIndex(originalSiblingIndex); // Nhét lại vào đúng khe cũ

        LayoutElement le = GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = false; // Bật lại Layout để tự căn chỉnh các lần sau
    }

    IEnumerator MoveAndScale(Vector3 targetPos, Vector3 targetScale)
    {
        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed;
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        transform.position = targetPos;
        transform.localScale = targetScale;
    }

    public void PlayCard()
    {
        if (spriteRenderer != null) spriteRenderer.sortingOrder = sortingOrderWhenPlayed;
        transform.SetParent(null, true);
        StopAllCoroutines();
        StartCoroutine(MoveAndScale(playPosition, originalScale));

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }
}   