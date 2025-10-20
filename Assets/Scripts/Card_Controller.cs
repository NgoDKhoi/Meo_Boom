using UnityEngine;
using System.Collections;

public class Card_Controller : MonoBehaviour
{
    // --- BIEN STATIC DE LUU LA BAI DANG DUOC CHON ---
    public static Card_Controller selectedCard = null;

    // --- BIEN QUAN LY HIEN THI ---
    private SpriteRenderer spriteRenderer;
    private int originalSortingOrder;

    private Vector3 originalScale;
    private Vector3 originalPosition;
    private Transform originalParent;
    private bool isZoomed = false;
    private Vector3 screenCenterWorldPos;

    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_TIME = 0.3f;

    [Header("Thiet lap la bai")]
    public float zoomMultiplier = 2f;
    public float moveSpeed = 8f;
    public int sortingOrderWhenSelected = 100; // Order in Layer khi duoc chon
    public int sortingOrderWhenPlayed = 10;    // Order in Layer khi da danh ra

    [Tooltip("Vi tri se di chuyen toi khi nhan nut 'Danh bai'")]
    public Vector3 playPosition = new Vector3(0.14f, 0.91f, 0f); // vao game chinh, dung chinh o day

    void Start()
    {
        // Lay component SpriteRenderer va luu lai Order in Layer goc
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalSortingOrder = spriteRenderer.sortingOrder;
        }

        originalScale = transform.localScale;
        originalPosition = transform.position;
        originalParent = transform.parent;

        Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, Camera.main.nearClipPlane + 10);
        screenCenterWorldPos = Camera.main.ScreenToWorldPoint(screenCenter);
        screenCenterWorldPos.z = 0;
    }

    void OnMouseDown()
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
                if (selectedCard != null)
                {
                    selectedCard.Deselect();
                }
                Select();
            }
            lastClickTime = Time.time;
        }
    }

    // Ham CHON la bai nay
    private void Select()
    {
        isZoomed = true;
        selectedCard = this;

        // --- TANG ORDER IN LAYER DE NOI LEN TREN CUNG ---
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = sortingOrderWhenSelected;
        }

        StopAllCoroutines();
        StartCoroutine(MoveAndScale(screenCenterWorldPos, originalScale * zoomMultiplier));
    }

    // Ham HUY CHON la bai nay
    public void Deselect()
    {
        isZoomed = false;

        // --- TRA VE ORDER IN LAYER BAN DAU ---
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = originalSortingOrder;
        }

        StopAllCoroutines();
        StartCoroutine(MoveAndScale(originalPosition, originalScale));
    }

    // Ham DANH BAI (chi duoc goi boi GameManager)
    public void PlayCard()
    {
        // --- THIET LAP ORDER IN LAYER KHI DA DANH RA ---
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = sortingOrderWhenPlayed;
        }

        transform.SetParent(null, true);
        StopAllCoroutines();
        StartCoroutine(MoveAndScale(playPosition, originalScale));
        GetComponent<Collider2D>().enabled = false;
    }

    IEnumerator MoveAndScale(Vector3 targetPosition, Vector3 targetScale)
    {
        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed;
            transform.position = Vector3.Lerp(startPos, targetPosition, t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        transform.position = targetPosition;
        transform.localScale = targetScale;
    }
}