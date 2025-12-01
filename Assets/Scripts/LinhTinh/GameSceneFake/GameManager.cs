using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DrawPileManager;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // --- PLAYER CLASS CHUYỂN VỀ ĐÂY ---
    public enum PlayerType { Human, Bot }

    [System.Serializable]
    public class Player
    {
        public string name;
        public PlayerType type;

        // Dùng Enum từ DrawPileManager
        [HideInInspector] public List<DrawPileManager.CardType> hand = new List<DrawPileManager.CardType>();
        [HideInInspector] public bool isDead = false;

        public OpponentDisplay botDisplayUI;
    }

    [Header("--- REFERENCES ---")]
    public DrawPileManager deckManager; // Kết nối với người chia bài

    [Header("--- PREFABS & UI ---")]
    public GameObject defuseCardPrefab;
    public GameObject explodeCardPrefab;
    public GameObject skipCardPrefab;
    public GameObject attackCardPrefab;
    public GameObject cardBackPrefab;

    public Transform playerHandArea;       // Khu vực tay người
    public Transform discardPileTransform; // Khu vực bài đánh ra
    public Button drawButton;              // Nút rút bài
    public Button playButton;              // Nút đánh bài
    public TextMeshProUGUI turnInfoText;   // Thông báo lượt

    [Header("--- GAME STATE ---")]
    public List<Player> players = new List<Player>();
    public int currentPlayerIndex = 0;

    public Vector3 discardPileCardScale = new Vector3(0.5f, 0.5f, 0.5f);

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            // Trường hợp có 2 GameManager (không nên xảy ra)
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (turnInfoText != null)
        {
            CardController.canvasTransform = turnInfoText.transform.parent;
        }

        StartGameSequence();
    }

    void Update()
    {
        // Update trạng thái nút Đánh bài
        if (playButton != null)
        {
            playButton.interactable = (CardController.selectedCard != null);
        }
    }
    void StartGameSequence()
    {
        // 1. Reset trạng thái người chơi
        foreach (var p in players)
        {
            p.hand.Clear();
            p.isDead = false;
            if (p.type == PlayerType.Bot && p.botDisplayUI != null)
                p.botDisplayUI.UpdateDisplay(p.name, 0, false);
        }

        // 2. Bảo DrawPileManager tạo bộ bài AN TOÀN (chưa có bom)
        deckManager.PrepareSafeDeck(players.Count);

        // 4. Bảo DrawPileManager nhét BOM vào và xào lại
        deckManager.AddExplodingKittens();

        // 5. BẮT ĐẦU COROUTINE CHIA BÀI (Hàm này sẽ gọi StartTurn() sau khi xong)
        StartCoroutine(DealInitialCardsRoutine());
    }

    IEnumerator DealInitialCardsRoutine()
    {
        foreach (var p in players)
        {
            // 1. Hiển thị người chơi đang được chia bài
            Debug.Log($"Chia bài cho: {p.name}");

            // --- BẮT ĐẦU CHIA BÀI ---

            // A. Tặng riêng mỗi người 1 lá Defuse
            p.hand.Add(DrawPileManager.CardType.Defuse);

            // Gộp logic Animation/Update UI vào một Coroutine đơn lẻ
            yield return StartCoroutine(AnimateCardDrawAndAddToHand(p, DrawPileManager.CardType.Defuse, false));

            // B. Rút thêm 4 lá ngẫu nhiên từ bộ bài an toàn
            for (int k = 0; k < 4; k++)
            {
                DrawPileManager.CardType drawnCard = deckManager.DrawCardData();
                p.hand.Add(drawnCard);

                // Dùng Coroutine chung cho 4 lá còn lại
                yield return StartCoroutine(AnimateCardDrawAndAddToHand(p, drawnCard, true));
            }

            yield return new WaitForSeconds(0.35f);
        }

        // 4. Bắt đầu lượt đầu tiên (Sau khi chia bài xong)
        Debug.Log("GameManager: Đã chia xong (1 Defuse + 4 Random) cho mọi người!");
        StartTurn();
    }

    // --- TURN LOGIC ---
    void StartTurn()
    {
        Player currentP = players[currentPlayerIndex];
        if (currentP.isDead) { EndTurn(); return; }

        if (turnInfoText != null) turnInfoText.text = $"Lượt của: {currentP.name}";

        if (currentP.type == PlayerType.Human)
        {
            if (drawButton != null) drawButton.interactable = true;
        }
        else
        {
            if (drawButton != null) drawButton.interactable = false;
            StartCoroutine(BotPlayRoutine());
        }
    }

    IEnumerator BotPlayRoutine()
    {
        // Bot sẽ chờ một chút (2s) để "suy nghĩ"
        yield return new WaitForSeconds(2f);

        // Thay vì gọi OnDrawButtonPress(), gọi thẳng DrawCardRoutine()
        StartCoroutine(DrawCardRoutine());
    }

    public void EndTurn()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count; // Giống bounded buffer
        StartTurn();
    }

    // --- ACTION LOGIC ---
    // Hàm này gắn vào nút Rút Bài (Draw Button)
    IEnumerator DrawCardRoutine()
    {
        // Kiểm tra lá bài còn hay không, nếu không thì break Coroutine
        if (deckManager.GetRemainingCount() <= 0) yield break;

        Player currentP = players[currentPlayerIndex];

        // VÔ HIỆU HÓA nút Rút bài nếu là người chơi
        if (currentP.type == PlayerType.Human && drawButton != null) drawButton.interactable = false;

        // 1. Lấy dữ liệu từ DrawPileManager
        DrawPileManager.CardType drawnCard = deckManager.DrawCardData();

        // 2. Xử lý logic Explode (Rút trúng bom)
        if (drawnCard == DrawPileManager.CardType.Explode)
        {
            Debug.Log($"<color=red>{currentP.name} RÚT TRÚNG BOM!</color>");
            currentP.isDead = true;
            // DÙ RÚT BOM THÌ CŨNG CẬP NHẬT UI ĐỂ HIỂN THỊ TRẠNG THÁI 'IS DEAD'
            UpdateUIForPlayer(currentP);

            // Rút trúng bom thì kết thúc lượt luôn
            EndTurn();
            yield break; // Kết thúc Coroutine
        }

        // 3. Xử lý logic Rút bài thành công
        currentP.hand.Add(drawnCard);

        // 4. CHẠY ANIMATION BAY VÀ CẬP NHẬT UI
        yield return StartCoroutine(AnimateCardDrawAndAddToHand(currentP, drawnCard, true)); // Luôn là random card khi rút trong lượt

        // 6. Kết thúc lượt
        EndTurn();
    }

    // Hàm này gắn vào nút Đánh Bài (Play Button)
    public void OnPlayCardButtonPress()
    {
        if (CardController.selectedCard == null) return;

        CardController cardObj = CardController.selectedCard;
        Player currentP = players[currentPlayerIndex];

        // 1. Xóa data
        if (currentP.hand.Contains(cardObj.cardType))
            currentP.hand.Remove(cardObj.cardType);

        // 2. Đưa data vào chồng bài bỏ
        deckManager.AddToDiscardPile(cardObj.cardType);

        // 3. KÍCH HOẠT ANIMATION BAY TỪ TAY ĐẾN BỘ BỎ
        // CardController sẽ tự xử lý việc bay và Destroy
        cardObj.PlayCard(discardPileTransform); // Truyền mục tiêu DiscardPile

        // 4. Reset trạng thái
        CardController.selectedCard = null;

        // Cập nhật lại UI tay (chỉ cần gọi UpdateUIForPlayer để đảm bảo)
        UpdateUIForPlayer(currentP);

        // Bạn có thể thêm logic EndTurn/ApplyEffect tùy theo loại bài tại đây
        // Ví dụ: EndTurn();
    }

    // --- UI LOGIC ---
    GameObject GetPrefabByType(DrawPileManager.CardType type)
    {
        switch (type)
        {
            case DrawPileManager.CardType.Defuse: return defuseCardPrefab;
            case DrawPileManager.CardType.Explode: return explodeCardPrefab;
            case DrawPileManager.CardType.Skip: return skipCardPrefab;
            case DrawPileManager.CardType.Attack: return attackCardPrefab;
            default: return null;
        }
    }

    void UpdateUIForPlayer(Player p)
    {
        if (p.type == PlayerType.Human)
        {
            return;
        }

        else
        {
            Transform botHandArea = p.botDisplayUI.handArea;

            // 1. Xóa tất cả các CardBack tĩnh cũ
            foreach (Transform child in botHandArea)
            {
                Destroy(child.gameObject);
            }

            // 2. Tạo lại CardBack tĩnh cho SỐ LƯỢNG bài hiện tại trong tay Bot
            for (int i = 0; i < p.hand.Count; i++)
            {
                GameObject newCard = Instantiate(cardBackPrefab, botHandArea);
                newCard.transform.localScale = Vector3.one;
            }

            // 3. Cập nhật Text số lượng
            if (p.botDisplayUI != null)
            {
                p.botDisplayUI.UpdateDisplay(p.name, p.hand.Count, p.isDead);
            }
        }
    }

    IEnumerator MoveCardToHand(CardController card, Transform targetParent, int targetSiblingIndex, bool isCardBack)
    {
        // --- LOGIC BAY BÌNH THƯỜNG CỦA HUMAN (isCardBack == false) ---

        float duration = 0.35f;
        float elapsedTime = 0f;
        Vector3 startPos = card.transform.position;
        Vector3 startScale = card.transform.localScale;
        Vector3 targetScale = Vector3.one;

        LayoutElement le = card.GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = true;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            card.transform.position = Vector3.Lerp(startPos, targetParent.position, t);
            card.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        // 5. Kết thúc bay: Xử lý cho HUMAN
        card.transform.SetParent(targetParent, false);
        card.transform.localPosition = Vector3.zero;
        card.transform.localScale = Vector3.one;
        card.transform.SetSiblingIndex(targetSiblingIndex);
        if (le != null) le.ignoreLayout = false;
    }

    public void OnDrawButtonPress()
    {
        // Cần phải kiểm tra xem có phải lượt của Human Player không (đảm bảo an toàn)
        Player currentP = players[currentPlayerIndex];
        if (currentP.type == PlayerType.Human)
        {
            // Bắt đầu Coroutine rút bài
            StartCoroutine(DrawCardRoutine());
        }
    }

    IEnumerator AnimateCardDrawAndAddToHand(Player p, DrawPileManager.CardType cardType, bool isRandomCard)
    {
        // Lấy vị trí World Space của nút bốc bài
        Vector3 startPosition = drawButton.transform.position;

        GameObject prefabToSpawn;
        if (p.type == PlayerType.Human)
        {
            prefabToSpawn = GetPrefabByType(cardType);
        }
        else
        {
            prefabToSpawn = cardBackPrefab;
        }

        if (prefabToSpawn == null) yield break;

        // 1. Tạo GameObject (luôn tạo trên Canvas để bay)
        GameObject newCardGO = Instantiate(prefabToSpawn, CardController.canvasTransform);
        newCardGO.transform.position = startPosition;
        newCardGO.transform.localScale = Vector3.one * 1f;

        // Lấy vị trí đích (tay người chơi hoặc vị trí Bot Hand Area)
        Transform targetParent = (p.type == PlayerType.Human) ? playerHandArea : p.botDisplayUI.handArea;

        // Tính toán VỊ TRÍ CUỐI CÙNG trong World Space.
        // Đối với Human: Vị trí cuối cùng là vị trí ảo trong Layout Group (Khó tính)
        // -> Cần dùng logic MoveCardToHand (sẽ sửa lại)
        // Đối với Bot: Vị trí cuối cùng là vị trí của Hand Area (tạm chấp nhận bay đến giữa Hand Area)
        Vector3 targetPos = (p.type == PlayerType.Human) ? Vector3.zero : targetParent.position;

        if (p.type == PlayerType.Human)
        {
            // Dùng logic cũ của Human để tính toán đích đến (trong MoveCardToHand)
            CardController newCardController = newCardGO.GetComponent<CardController>();
            if (newCardController != null)
            {
                newCardController.cardType = cardType;
                // Vẫn dùng MoveCardToHand nhưng thay đổi logic bay
                yield return StartCoroutine(MoveCardToHand(newCardController, targetParent, targetParent.childCount, false));
            }
        }
        else // Logic cho BOT
        {
            // Logic Bay Tới giữa Hand Area của Bot (đơn giản hóa)
            float duration = 0.35f;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
                newCardGO.transform.position = Vector3.Lerp(startPosition, targetPos, t);
                yield return null;
            }

            // 2. Kết thúc Animation: HỦY lá bài Animation tạm thời
            Destroy(newCardGO);

            // 3. CẬP NHẬT UI TĨNH (Tạo Card Back mới)
            UpdateUIForPlayer(p);
        }

        yield return new WaitForSeconds(0.1f); // Chờ một chút trước khi chuyển sang lá bài/người chơi tiếp theo
    }
}