using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static DrawPileManager;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum PlayerType { Human, Bot }

    [System.Serializable]
    public class Player
    {
        public TextMeshProUGUI nameDisplaySource;
        [HideInInspector] public string name;
        public PlayerType type;

        // Dùng Enum từ DrawPileManager
        [HideInInspector] public List<DrawPileManager.CardType> hand = new List<DrawPileManager.CardType>();
        [HideInInspector] public bool isDead = false;

        public OpponentDisplay botDisplayUI;

        public void SyncNameFromUI()
        {
            if (nameDisplaySource != null)
            {
                // Lấy nội dung text đang hiện -> Gán vào biến name
                name = nameDisplaySource.text;
            }
            else if (type == PlayerType.Bot)
            {
                // Bot thì có thể đặt tên mặc định nếu không có UI source
                if (string.IsNullOrEmpty(name)) name = "Bot " + Random.Range(1, 100);
            }
        }
    }

    [Header("--- REFERENCES ---")]
    public DrawPileManager drawPileManager; // Kết nối với người chia bài

    [Header("--- PREFABS & UI ---")]
    public GameObject defuseCardPrefab;
    public GameObject explodeCardPrefab;
    public GameObject skipCardPrefab;
    public GameObject attackCardPrefab;
    public GameObject shuffleCardPrefab;
    public GameObject drawBottomPrefab;
    public GameObject seeFuturePrefab;
    public GameObject cardBackPrefab;

    public Transform playerHandArea;       // Khu vực tay người
    public Transform discardPileTransform; // Khu vực bài đánh ra
    public Button drawButton;              // Nút rút bài
    public Button playButton;              // Nút đánh bài
    public TextMeshProUGUI turnInfoText;   // Thông báo lượt

    [Header("--- UI VICTORY ---")]
    public GameObject victoryPanel;      // Panel_Victory 
    public TextMeshProUGUI winnerNameText; // text hiện tên người thắng

    [Header("--- GAME STATE ---")]
    public List<Player> players = new List<Player>();
    public int currentPlayerIndex = 0;

    [Header("--- BOMB LOGIC ---")]
    public bool IsDefusing { get; private set; } = false; // Trạng thái đang gỡ bom
    private GameObject pendingBombVisual; // Lưu visual quả bom đang treo giữa màn hình

    public Vector3 discardPileCardScale = new Vector3(0.5f, 0.5f, 0.5f);

    #region CORE GAMEPLAY
    // Bắt đầu game
    void StartGameSequence()
    {
        // 1. Reset biến toàn cục
        currentPlayerIndex = 0;
        IsDefusing = false;
        CardController.selectedCard = null;

        // Xóa visual bom nếu còn sót lại từ ván trước
        if (pendingBombVisual != null) Destroy(pendingBombVisual);

        // 2. Reset trạng thái người chơi
        foreach (var p in players)
        {
            p.hand.Clear();
            p.isDead = false;
            if (p.type == PlayerType.Bot && p.botDisplayUI != null)
                p.botDisplayUI.UpdateDisplay(p.name, 0, false);
        }

        // 3. Bảo DrawPileManager tạo bộ bài AN TOÀN (chưa có bom)
        drawPileManager.PrepareSafeDeck(players.Count);

        // 4. BẮT ĐẦU COROUTINE CHIA BÀI (Hàm này sẽ nhét bom và gọi StartTurn() sau khi xong)
        StartCoroutine(DealInitialCardsRoutine());
    }

    // Chia bài khởi đầu
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
                DrawPileManager.CardType drawnCard = drawPileManager.DrawCardData();
                p.hand.Add(drawnCard);

                // Dùng Coroutine chung cho 4 lá còn lại
                yield return StartCoroutine(AnimateCardDrawAndAddToHand(p, drawnCard, true));
            }

            yield return new WaitForSeconds(0.35f);
        }

        // Thêm bom vào
        drawPileManager.AddExplodingKittens();

        // Có thể thêm hiệu ứng trộn bom ở đây (tiếng xào bài chẳng hạn)
        // yield return new WaitForSeconds(1f);

        Debug.Log("GameManager: Gọi StartTurn()");
        StartTurn();
    }

    // Rút bài
    IEnumerator DrawCardRoutine()
    {
        {
            // Kiểm tra bộ rút, còn lá bài còn hay không, nếu không thì break   
            if (drawPileManager.GetRemainingCount() <= 0)
            {
                Debug.Log($"Bộ bài đã hết </color>");
                yield break;
            }

            Player currentP = players[currentPlayerIndex];

            // VÔ HIỆU HÓA nút rút bài của người chơi để tránh bấm nhiều lần
            if (currentP.type == PlayerType.Human && drawButton != null) drawButton.interactable = false;

            // 1. Lấy dữ liệu từ DrawPileManager
            DrawPileManager.CardType drawnCard = drawPileManager.DrawCardData();

            // 2. Xử lý logic Explode (Rút trúng bom)
            if (drawnCard == DrawPileManager.CardType.Explode)
            {
                Debug.Log($"<color=red>{currentP.name} RÚT TRÚNG BOM!</color>");

                // A. Hiện thị visual quả bom (Treo giữa màn hình chứ không bay về tay) 
                pendingBombVisual = Instantiate(explodeCardPrefab, CardController.canvasTransform);
                pendingBombVisual.transform.position = drawButton.transform.position;
                pendingBombVisual.transform.localScale = Vector3.one * 1.5f; // Phóng to cho sợ

                // Animation đưa bom ra giữa màn hình
                StartCoroutine(MoveToPosition(pendingBombVisual.transform, Vector3.zero, 0.5f));

                // B. Kiểm tra xem người chơi có Defuse không
                if (currentP.hand.Contains(DrawPileManager.CardType.Defuse))
                {
                    if (currentP.type == PlayerType.Human)
                    {
                        // LOGIC CHO NGƯỜI: Bật chế độ chờ bấm nút
                        IsDefusing = true;
                        if (turnInfoText != null) turnInfoText.text = "RÚT TRÚNG BOM! HÃY ĐÁNH DEFUSE!";

                        if (playButton != null) playButton.interactable = false;
                    }
                    else // LOGIC CHO BOT: Tự động gỡ bom
                    {
                        if (turnInfoText != null) turnInfoText.text = $"{currentP.name} ĐANG GỠ BOM...";

                        // 1. Chờ 3s để người chơi kịp nhìn thấy quả bom
                        yield return new WaitForSeconds(3f);

                        // 2. Trừ lá Defuse khỏi tay Bot
                        currentP.hand.Remove(DrawPileManager.CardType.Defuse);
                        drawPileManager.AddToDiscardPile(DrawPileManager.CardType.Defuse); // Thêm vào chồng bài bỏ

                        // 3. Cập nhật UI Bot (để số lưng bài giảm đi 1)
                        UpdateUIForBot(currentP);

                        // 4. Xóa visual quả bom
                        Destroy(pendingBombVisual);

                        // 5. Nhét bom lại vào bộ bài (Random vị trí)
                        int randomSlot = Random.Range(0, drawPileManager.GetRemainingCount());
                        drawPileManager.InsertCardToDeck(DrawPileManager.CardType.Explode, randomSlot);
                        Debug.Log($"{currentP.name} đã gỡ bom và nhét lại vào vị trí: {randomSlot}");

                        // 6. Kết thúc lượt của Bot
                        EndTurn();
                    }
                }
                else
                {
                    // KHÔNG CÓ DEFUSE -> CHẾT
                    yield return new WaitForSeconds(1f);
                    HandlePlayerDeath(currentP);
                }
                yield break; // Dừng Coroutine tại đây
            }

            // 3. Xử lý logic Rút bài thành công
            currentP.hand.Add(drawnCard);

            // 4. CHẠY ANIMATION BAY VÀ CẬP NHẬT UI
            yield return StartCoroutine(AnimateCardDrawAndAddToHand(currentP, drawnCard, true)); // Luôn là random card khi rút trong lượt

            // 6. Kết thúc lượt
            EndTurn();
        }
    }

    // Hàm xóa bài khỏi tay và thêm vào bộ bỏ (chỉ xử lý data)
    private void ProcessCardData(Player player, DrawPileManager.CardType cardType)
    {
        // 1. Xóa khỏi tay (Data)
        if (player.hand.Contains(cardType))
        {
            player.hand.Remove(cardType);
        }

        // 2. Thêm vào chồng bài bỏ (Data)
        drawPileManager.AddToDiscardPile(cardType);

        // 3. (Mở rộng sau này) Xử lý hiệu ứng bài tại đây
        // HandleCardEffect(cardType); 
        // Ví dụ: Nếu là Attack -> nextTurnCount = 2;
    }

    //Xử lý hiệu ứng bài
    public void HandleCardEffect(DrawPileManager.CardType cardType)
    {
        switch (cardType)
        {
            case DrawPileManager.CardType.Skip:
                Debug.Log("<color=cyan>Effect: SKIP TURN (Bỏ lượt)</color>");
                // Skip đơn giản là kết thúc lượt mà không cần rút bài
                EndTurn();
                break;

            case DrawPileManager.CardType.Shuffle:
                Debug.Log("<color=cyan>Effect: SHUFFLE (Xào bài)</color>");
                // Gọi hàm xào bài từ DrawPileManager
                drawPileManager.ShuffleDrawPile();

                // Lưu ý: Shuffle không kết thúc lượt, người chơi vẫn phải rút bài hoặc đánh tiếp
                // Nên ở đây ta KHÔNG gọi EndTurn()
                break;

            case DrawPileManager.CardType.Attack:
                Debug.Log("Effect: ATTACK (Chưa cài đặt)");
                // Logic Attack sẽ phức tạp hơn (bắt người sau đi 2 lượt), làm sau
                // Tạm thời cho nó giống Skip
                EndTurn();
                break;

            case DrawPileManager.CardType.SeeFuture:
            case DrawPileManager.CardType.DrawBottom:
                Debug.Log("CEffect: SeeFuture (Chưa cài đặt)");
                break;
        }
    }

    // Hàm xử lý người chơi bị loại
    public void HandlePlayerDeath(Player p)
    {
        // 1. Đánh dấu chết
        p.isDead = true;
        UpdateUIForBot(p);

        // 2. Xóa visual bom (nếu có)
        if (pendingBombVisual != null) Destroy(pendingBombVisual);

        Debug.Log($"{p.name} đã bị loại!");

        // 3. KIỂM TRA THẮNG THUA NGAY TẠI ĐÂY
        if (CheckForWinner())
        {
            // Nếu đã có người thắng, không làm gì thêm nữa
            return;
        }

        // 4. Nếu game chưa kết thúc, chuyển lượt cho người tiếp theo
        EndTurn();
    }

    // Hàm kiểm tra xem game đã kết thúc chưa
    bool CheckForWinner()
    {
        int aliveCount = 0;
        Player winner = null;

        // Đếm số người còn sống
        foreach (var p in players)
        {
            if (!p.isDead)
            {
                aliveCount++;
                winner = p;
            }
        }

        // Nếu chỉ còn lại 1 người (hoặc 0 nếu xui xẻo nổ hết)
        if (aliveCount <= 1)
        {
            Debug.Log("GAME OVER!");

            // 1. Hiện bảng chiến thắng
            if (victoryPanel != null) victoryPanel.SetActive(true);

            // 2. Hiện tên người thắng
            if (winner != null && winnerNameText != null)
            {
                winnerNameText.text = $"{winner.name} CHIẾN THẮNG!";
            }

            // 3. DỪNG GAME LOOP
            StopAllCoroutines(); // Dừng tất cả mọi hoạt động rút bài, bot suy nghĩ...

            return true; // Đã có người thắng
        }

        return false; // Chưa ai thắng, chơi tiếp
    }
    #endregion

    #region TURN LOGIC
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

    public void EndTurn()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count; // Giống bounded buffer
        StartTurn();
    }
    #endregion

    #region BOT LOGIC
    // Quản lý lượt chơi của Bot
    IEnumerator BotPlayRoutine()
    {
        // Bot sẽ chờ để 1 khoảng time ngẫu nhiên để "suy nghĩ"
        yield return new WaitForSeconds(Random.Range(1.5f, 3f));

        Player botPlayer = players[currentPlayerIndex];
        DrawPileManager.CardType cardToPlay = BotDecideBestCard(botPlayer);  // Bot kiểm tra bài trên tay để xem nên làm gì ?

        if (cardToPlay != DrawPileManager.CardType.None) // Nếu không rút bài
        {
            Debug.Log($"{botPlayer.name} quyết định đánh lá: {cardToPlay}");
            yield return StartCoroutine(BotPlayCardAction(botPlayer, cardToPlay));

            // Thoát Coroutine ngay, không rút bài nữa.
            if (cardToPlay == DrawPileManager.CardType.Skip || cardToPlay == DrawPileManager.CardType.Attack)
            {
                yield break;
            }


            // Gợi ý sau này phát triển, ta có thể cho Bot đệ quy gọi lại suy nghĩ hoặc đánh bài để EndTurn.
        }
         // === TRƯỜNG HỢP 2: KHÔNG CÓ BÀI NGON -> RÚT BÀI ===
        Debug.Log($"{botPlayer.name} quyết định RÚT BÀI");
        StartCoroutine(DrawCardRoutine());
    }

    // Thuật toán chọn bài cho bot
    DrawPileManager.CardType BotDecideBestCard(Player bot)
    {
        // Ưu tiên 1: Nếu có Attack -> Đánh luôn cho ngầu
        //if (bot.hand.Contains(DrawPileManager.CardType.Attack))
        //{
        //    return DrawPileManager.CardType.Attack;
        //}

        // Ưu tiên 2: Nếu có SeeFuture -> Đánh để soi
        // if (bot.hand.Contains(...SeeFuture...)) return ...

        // Ưu tiên 3: Đánh lá skip
        if (bot.hand.Contains(DrawPileManager.CardType.Skip))
        {
            return DrawPileManager.CardType.Skip;
        }

        // Mặc định: Không đánh gì cả (để đi Rút bài)
        return DrawPileManager.CardType.None; // Tạm quy ước Skip ở hàm này là "Bỏ qua việc đánh"
    }

    // Hành động đánh bài của bot   
    IEnumerator BotPlayCardAction(Player bot, DrawPileManager.CardType cardType)
    {
        // 1. Xóa bài khỏi tay Bot (xóa trong data)
        bot.hand.Remove(cardType);
        drawPileManager.AddToDiscardPile(cardType);

        // 2. Cập nhật UI (Xóa bớt 1 lưng bài)
        UpdateUIForBot(bot);

        // 3. Spawn Visual lá bài bay ra giữa bàn (cho người chơi thấy Bot vừa đánh gì)
        GameObject cardVisual = Instantiate(GetPrefabByType(cardType), bot.botDisplayUI.handArea.position, Quaternion.identity, CardController.canvasTransform);

        // ... Code Animation bay vào DiscardPile ...
        yield return new WaitForSeconds(1f); 
        cardVisual.transform.SetParent(discardPileTransform);
        cardVisual.transform.localPosition = Vector3.zero;
        cardVisual.transform.localRotation = Quaternion.identity;
        cardVisual.transform.localScale = Vector3.one;

        // 4. Xử lý hiệu ứng bài
        HandleCardEffect(cardType);
    }
    #endregion

    #region HUMAN INPUT

    // Hàm sự kiện của nút đánh bài
    public void OnPlayCardButtonPress()
    {
        if (CardController.selectedCard == null) return;

        CardController cardObj = CardController.selectedCard;
        Player currentP = players[currentPlayerIndex];
            

        // A. NẾU ĐANG TRÚNG BOOM
        if (IsDefusing)
        {
            // Chỉ chấp nhận thẻ Defuse
            if (cardObj.cardType == DrawPileManager.CardType.Defuse)
            {
                // 1. Xóa bài khỏi tay & bay vào Discard Pile
                ProcessCardData(currentP, cardObj.cardType);
                cardObj.PlayCard(discardPileTransform);

                // 2. Tắt chế độ nguy hiểm
                IsDefusing = false;

                // 3. Xử lý Quả Bom đang treo (pendingBombVisual)

                Destroy(pendingBombVisual); // Xóa visual bom cũ

                int randomSlot = Random.Range(0, drawPileManager.GetRemainingCount());
                drawPileManager.InsertCardToDeck(DrawPileManager.CardType.Explode, randomSlot);

                Debug.Log($"Đã gỡ bom thành công! Bom nằm ở vị trí: {randomSlot}");

                // 4. Update UI & End Turn
                UpdateUIForBot(currentP);
                EndTurn();
            }
            return; // Quan trọng: Return luôn, không chạy logic đánh bài thường bên dưới
        }

        // B. NẾU KHÔNG TRÚNG BOOM
        // 1. Xóa lá bài và đưa vào bộ bỏ (xử lý data0
        ProcessCardData(currentP, cardObj.cardType);

        // 2. KÍCH HOẠT ANIMATION BAY TỪ TAY ĐẾN BỘ BỎ
        cardObj.PlayCard(discardPileTransform);

        // 3. Reset trạng thái
        CardController.selectedCard = null; 
        UpdateUIForBot(currentP);

        // 4. Xử lý hiệu ứng lá bài
        HandleCardEffect(cardObj.cardType);
    }

    // Hàm sự kiện của nút rút bài
    public void OnDrawButtonPress()
    {
        // Cần phải kiểm tra xem có phải lượt của Human Player không (đảm bảo an toàn)
        Player currentP = players[currentPlayerIndex];
        if (currentP.type == PlayerType.Human)
        {
            // Bắt đầu Routine rút bài
            StartCoroutine(DrawCardRoutine());
        }
    }   
    #endregion

    #region UI LOGIC
    GameObject GetPrefabByType(DrawPileManager.CardType type)
    {
        switch (type)
        {
            case DrawPileManager.CardType.Defuse: return defuseCardPrefab;
            case DrawPileManager.CardType.Explode: return explodeCardPrefab;
            case DrawPileManager.CardType.Skip: return skipCardPrefab;
            case DrawPileManager.CardType.Attack: return attackCardPrefab;
            case DrawPileManager.CardType.Shuffle: return shuffleCardPrefab;
            case DrawPileManager.CardType.DrawBottom: return drawBottomPrefab;
            case DrawPileManager.CardType.SeeFuture: return seeFuturePrefab;
            
            default: return null;
        }
    }

    void UpdateUIForBot (Player p)
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
            UpdateUIForBot(p);
        }

        yield return new WaitForSeconds(0.1f); // Chờ một chút trước khi chuyển sang lá bài/người chơi tiếp theo
    }

    // Hàm phụ trợ di chuyển visual bom
    IEnumerator MoveToPosition(Transform obj, Vector3 target, float duration)
    {
        float time = 0;
        Vector3 start = obj.position;
        while (time < duration)
        {
            time += Time.deltaTime;
            obj.position = Vector3.Lerp(start, target, time / duration);
            yield return null;
        }
    }
    #endregion
}