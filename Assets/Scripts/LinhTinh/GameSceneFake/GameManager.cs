using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static DrawPileManager;
using static GameManager;
using UnityEngine.SceneManagement; // Thư viện để chuyển cảnh

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

        public Vector3 GetEffectPosition()
        {
            if (type == PlayerType.Human) return GameManager.Instance.drawButton.transform.position;
            return botDisplayUI.handArea.position;
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
    public TextMeshProUGUI drawPileCountText; // Banner hiển thị số lá bài còn lại

    [Header("--- UI VICTORY ---")]
    public GameObject victoryPanel;      // Panel_Victory 
    public TextMeshProUGUI winnerNameText; // text hiện tên người thắng

    [Header("--- GAME STATE ---")]
    public List<Player> players = new List<Player>();
    public int currentPlayerIndex = 0;
    public int turnsRemaining = 1; // Số lượt rút còn lại của người hiện tại
    private int nextTurnStartingCount = 1; // Số lượt sẽ chuyển cho người kế tiếp (mặc định là 1)
    private bool isTurnActionInProgress = false; // biến mutex để loại trừ tương hỗ, giải quyết race condition
    private bool isBotThinking = false; // <-- THÊM BIẾN NÀY
    private bool da_chuyen_luot_sang_bot = false;

    [Header("--- BOMB LOGIC ---")]
    public bool IsDefusing { get; private set; } = false; // Trạng thái đang gỡ bom
    private GameObject pendingBombVisual; // Lưu visual quả bom đang treo giữa màn hình

    [Header("--- EFFECT PREFABS ---")]
    public GameObject attackEffectPrefab;
    public GameObject shuffleEffectPrefab;
    public GameObject seeTheFutureEffectPrefab;
    public GameObject defuseEffectPrefab;
    public GameObject drawBottomEffectPrefab;
    public GameObject skipEffectPrefab;
    public GameObject explodeMasterEffectPrefab;

    [Header("--- TIMER SYSTEM ---")]
    public TurnTimer turnTimer;
    public float defaultTurnTime = 10f; // Thời gian suy nghĩ: 10 giây
    public float defuseTurnTime = 3f;   // Thời gian gỡ bom: 3 giây

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

        if (turnTimer != null)
        {
            // Đăng ký hàm xử lý khi Timer hết giờ
            turnTimer.OnTimerTimeout.AddListener(HandleTimerTimeout);
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

    #region CORE GAMEPLAY
    // Bắt đầu game
    void StartGameSequence()
    {
        // 1. Reset biến toàn cục
        currentPlayerIndex = 0;
        IsDefusing = false;
        CardController.selectedCard = null;
        turnsRemaining = 1;
        nextTurnStartingCount = 1;

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

        UpdateDrawPileCountUI();

        // Có thể thêm hiệu ứng trộn bom ở đây (tiếng xào bài chẳng hạn)
        // yield return new WaitForSeconds(1f);

        Debug.Log("GameManager: Gọi StartTurn()");
        StartTurn();
    }

    // Rút bài
    public IEnumerator DrawCardRoutine(bool fromBottom = false, bool isForcedDraw = false)
    {
        // THÊM SAFETY TIMEOUT
        float startTime = Time.time;
        float maxWaitTime = 30f; // 30 giây timeout

        // CHỈ CHO PHÉP isForcedDraw VÀO KHI KHÔNG CÓ HÀNH ĐỘNG NÀO ĐANG CHẠY
        // Hoặc đang là lượt của Bot và bot đang chờ timeout
        if (isTurnActionInProgress)
        {
            if (!isForcedDraw)
            {
                Debug.LogWarning($"DrawCardRoutine bị từ chối cho {players[currentPlayerIndex].name}");
                yield break;
            }
            else
            {
                // ĐỢI TỐI ĐA 2 GIÂY CHO HÀNH ĐỘNG HIỆN TẠI
                Debug.Log($"DrawCardRoutine (forced): Đang đợi hành động hiện tại kết thúc...");
                float waitStartTime = Time.time;
                while (isTurnActionInProgress && (Time.time - waitStartTime) < 2f)
                {
                    yield return new WaitForSeconds(0.1f);
                }

                if (isTurnActionInProgress)
                {
                    Debug.LogError($"DrawCardRoutine: Timeout khi đợi hành động hiện tại! Force resetting...");
                    isTurnActionInProgress = false; // FORCE RESET
                }
            }
        }

        isTurnActionInProgress = true;

        try
        {
            if (Time.time - startTime > maxWaitTime)
            {
                Debug.LogError($"DrawCardRoutine timeout sau {maxWaitTime}s!");
                yield break;
            }

            // Kiểm tra bộ rút, còn lá bài còn hay không, nếu không thì break   
            if (drawPileManager.GetRemainingCount() <= 0)
            {
                Debug.Log($"<color=orange>Bộ bài đã hết!</color>");
                // Chuyển lượt khi hết bài
                EndTurn();
                yield break;
            }

            Player currentP = players[currentPlayerIndex];

            // Kiểm tra player có còn sống không
            if (currentP.isDead)
            {
                Debug.LogWarning($"DrawCardRoutine: {currentP.name} đã chết, bỏ qua.");
                EndTurn();
                yield break;
            }

            // VÔ HIỆU HÓA nút rút bài của người chơi để tránh bấm nhiều lần
            if (currentP.type == PlayerType.Human && drawButton != null)
                drawButton.interactable = false;

            // 1. Lấy dữ liệu từ DrawPileManager
            DrawPileManager.CardType drawnCard;
            if (fromBottom)
            {
                drawnCard = drawPileManager.DrawBottomCardData(); // Gọi hàm rút đáy
                Debug.Log($"{currentP.name} rút bài từ ĐÁY");
            }
            else
            {
                Debug.Log($"<color=yellow>{currentP.name} quyết định RÚT BÀI</color>");
                drawnCard = drawPileManager.DrawCardData(); // Gọi hàm rút thường
            }

            UpdateDrawPileCountUI();

            // DỪNG TIMER CHO CẢ HUMAN VÀ BOT KHI BẮT ĐẦU RÚT
            if (turnTimer != null)
            {
                Debug.Log($"{currentP.name} rút bài, dừng Timer!");
                turnTimer.StopTimer();
            }

            // 2. Xử lý logic Explode (Rút trúng bom)
            if (drawnCard == DrawPileManager.CardType.Explode)
            {
                Debug.Log($"<color=red>{currentP.name} RÚT TRÚNG BOM!</color>");

                // A. Hiện thị visual quả bom (Treo giữa màn hình chứ không bay về tay)
                if (explodeCardPrefab != null && CardController.canvasTransform != null)
                {
                    pendingBombVisual = Instantiate(explodeCardPrefab, CardController.canvasTransform);
                    pendingBombVisual.transform.position = drawButton != null ? drawButton.transform.position : Vector3.zero;
                    pendingBombVisual.transform.localScale = Vector3.one * 1.5f;

                    // Animation đưa bom ra giữa màn hình
                    StartCoroutine(MoveToPosition(pendingBombVisual.transform, Vector3.zero, 0.5f));
                }

                // B. Kiểm tra xem người chơi có Defuse không
                if (currentP.hand.Contains(DrawPileManager.CardType.Defuse))
                {
                    if (currentP.type == PlayerType.Human)
                    {
                        // LOGIC CHO NGƯỜI: Bật chế độ chờ bấm nút
                        IsDefusing = true;
                        if (turnInfoText != null)
                            turnInfoText.text = "RÚT TRÚNG BOM!\nHÃY ĐÁNH DEFUSE!";

                        if (playButton != null)
                            playButton.interactable = false;

                        if (turnTimer != null)
                            turnTimer.StartTimer(defuseTurnTime, true);

                        isTurnActionInProgress = false;
                        yield break; // Dừng Coroutine
                    }
                    else // LOGIC CHO BOT: Tự động gỡ bom
                    {
                        if (turnInfoText != null)
                            turnInfoText.text = $"{currentP.name}\nĐANG GỠ BOM...";

                        // 1. Chờ để người chơi kịp nhìn thấy quả bom
                        yield return new WaitForSeconds(1.5f);

                        float defuseDuration = 1.6f;
                        if (defuseEffectPrefab != null && pendingBombVisual != null)
                        {
                            Vector3 spawnPos = pendingBombVisual.transform.position;
                            GameObject defuseFX = Instantiate(defuseEffectPrefab, spawnPos, Quaternion.identity, CardController.canvasTransform);

                            EffectAnimation fxPlayer = defuseFX.GetComponent<EffectAnimation>();
                            if (fxPlayer != null)
                                fxPlayer.effectDuration = defuseDuration;

                            yield return new WaitForSeconds(defuseDuration);
                        }

                        // 2. Trừ lá Defuse khỏi tay Bot
                        currentP.hand.Remove(DrawPileManager.CardType.Defuse);
                        drawPileManager.AddToDiscardPile(DrawPileManager.CardType.Defuse);

                        // 3. Cập nhật UI Bot
                        UpdateUIForBot(currentP);

                        // 4. Xóa visual quả bom
                        if (pendingBombVisual != null)
                            Destroy(pendingBombVisual);

                        // 5. Nhét bom lại vào bộ bài (Random vị trí)
                        int randomSlot = Random.Range(0, Mathf.Max(1, drawPileManager.GetRemainingCount()));
                        drawPileManager.InsertCardToDeck(DrawPileManager.CardType.Explode, randomSlot);
                        Debug.Log($"{currentP.name} đã gỡ bom và nhét vào vị trí: {randomSlot}");

                        UpdateDrawPileCountUI();

                        // 6. Kết thúc lượt của Bot
                        turnsRemaining--;

                        isTurnActionInProgress = false;
                        isBotThinking = false;

                        if (turnsRemaining > 0)
                        {
                            // Vẫn còn lượt rút (do Attack)
                            // THAY VÌ GỌI TRỰC TIẾP CheckTurnStatus(), DÙNG Invoke
                            Invoke(nameof(DelayedCheckTurnStatus), 0.1f);
                        }
                        else
                        {
                            // Nếu hết lượt rút
                            EndTurn();
                        }
                        yield break;
                    }
                }
                else // KHÔNG CÓ DEFUSE -> CHẾT
                {
                    isBotThinking = false;
                    yield return new WaitForSeconds(1f);
                    yield return StartCoroutine(HandlePlayerDeathRoutine(currentP));
                    yield break;
                }
            }

            // 3. Xử lý logic Rút bài thành công (không phải bom)
            currentP.hand.Add(drawnCard);
            if (currentP.type == PlayerType.Bot) isBotThinking = false;

            // 4. CHẠY ANIMATION BAY VÀ CẬP NHẬT UI
            yield return StartCoroutine(AnimateCardDrawAndAddToHand(currentP, drawnCard, true));

            // 5. Kết thúc lượt
            turnsRemaining--;

            // chờ trước khi nhả khóa
            yield return new WaitForSeconds(0.5f);
            isTurnActionInProgress = false;

            if (turnsRemaining > 0)
            {
                // Vẫn còn lượt rút (do Attack)
                // THAY VÌ GỌI TRỰC TIẾP CheckTurnStatus(), DÙNG Invoke
                Invoke(nameof(DelayedCheckTurnStatus), 0.1f);
            }
            else
            {
                // Nếu hết lượt rút
                EndTurn();
            }
        }
        finally
        {
            // Debug log để theo dõi
            Debug.Log($"1 lần rút bài vừa được xử lý, isTurnActionInProgress = {isTurnActionInProgress}");
        }
    }

    // Hàm phụ trợ
    void DelayedCheckTurnStatus()
    {
        CheckTurnStatus();
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
    public IEnumerator HandleCardEffect(DrawPileManager.CardType cardType, Player player)
    {
        switch (cardType)
        {
            case DrawPileManager.CardType.Skip:
                Debug.Log($"<color=cyan>[{player.name}] kích hoạt Effect: SKIP (Giảm 1 lượt rút)</color>");

                float skipDuration = 0.75f; // Thời gian FX

                // GỌI HIỆU ỨNG SKIP
                if (skipEffectPrefab != null)
                {
                    Vector3 spawnPos = Vector3.zero; // Vị trí player đánh bài (DrawButton hoặc Hand Bot)
                    GameObject skipFX = Instantiate(skipEffectPrefab, spawnPos, Quaternion.identity, CardController.canvasTransform);
                    EffectAnimation fxPlayer = skipFX.GetComponent<EffectAnimation>();
                    if (fxPlayer != null) fxPlayer.effectDuration = skipDuration;
                }
                yield return new WaitForSeconds(skipDuration); // Chờ FX chạy
                turnsRemaining--;
                isBotThinking = false;
                CheckTurnStatus();
                break;

            case DrawPileManager.CardType.Attack:
                Debug.Log($"<color=orange>[{player.name}] kích hoạt Effect: ATTACK (Tấn công!)</color>");

                // 1. TÍNH TOÁN SỐ LƯỢT TẤN CÔNG
                int turnsToPass = (turnsRemaining > 1 ? turnsRemaining : 0) + 2;
                nextTurnStartingCount = turnsToPass;
                // 2. TÌM NGƯỜI CHƠI TIẾP THEO CÒN SỐNG
                int nextPlayerIndex = (currentPlayerIndex + 1) % players.Count;
                int startIndex = nextPlayerIndex;
                Player victim = null;

                do
                {
                    victim = players[nextPlayerIndex];
                    // Nếu tìm thấy người chơi còn sống và không phải là người đánh bài (chính mình)
                    if (!victim.isDead && nextPlayerIndex != currentPlayerIndex)
                    {
                        break; // Tìm thấy nạn nhân!
                    }
                    nextPlayerIndex = (nextPlayerIndex + 1) % players.Count;
                    // Trường hợp khẩn cấp: tất cả người khác đã chết
                    if (nextPlayerIndex == startIndex)
                    {
                        Debug.LogWarning("Không tìm thấy nạn nhân còn sống nào ngoài chính mình!");
                        victim = null; // Gán lại thành null để bỏ qua Attack
                        break;
                    }

                } while (nextPlayerIndex != startIndex);

                float attackDuration = 1.0f; // Thời gian FX

                if (victim != null)
                {
                    // 3. KHỞI TẠO HIỆU ỨNG TIA SÉT (Chuyển đến vị trí của Victim đã tìm được)
                    if (attackEffectPrefab != null)
                    {
                        // Sử dụng GetEffectPosition() của victim đã được tìm thấy
                        Vector3 spawnPos = victim.GetEffectPosition();
                        GameObject attackFX = Instantiate(attackEffectPrefab, spawnPos, Quaternion.identity, CardController.canvasTransform);
                        EffectAnimation fxPlayer = attackFX.GetComponent<EffectAnimation>();
                        if (fxPlayer != null) fxPlayer.effectDuration = attackDuration;
                    }

                    yield return new WaitForSeconds(attackDuration);

                    Debug.Log($"--> Người tiếp theo - ({victim.name}) - sẽ phải rút {nextTurnStartingCount} lá!");
                }
                else
                {
                    // Nếu không tìm thấy nạn nhân (ví dụ: chỉ còn 2 người, và người kia đã chết)
                    Debug.Log($"Attack không có tác dụng: Không tìm thấy nạn nhân còn sống nào.");
                    yield return new WaitForSeconds(attackDuration / 2);
                }

                turnsRemaining = 0;
                isBotThinking = false;
                CheckTurnStatus();
                break;

            case DrawPileManager.CardType.Shuffle:
                Debug.Log($"<color=cyan>[{player.name}] kích hoạt Effect: SHUFFLE (Xào bài)</color>");

                float shuffleDuration = 1.5f;

                if (shuffleEffectPrefab != null)
                {
                    Vector3 spawnPos = drawButton.transform.position;
                    GameObject shuffleFX = Instantiate(shuffleEffectPrefab, spawnPos, Quaternion.identity, CardController.canvasTransform);
                    EffectAnimation fxPlayer = shuffleFX.GetComponent<EffectAnimation>();
                    if (fxPlayer != null) fxPlayer.effectDuration = shuffleDuration;
                }

                yield return new WaitForSeconds(shuffleDuration);

                drawPileManager.ShuffleDrawPile();
                isBotThinking = false;
                UpdateDrawPileCountUI();

                break;

            case DrawPileManager.CardType.DrawBottom:
                // DrawBottom không có FX riêng mà chỉ gọi DrawCardRoutine
                Debug.Log($"<color=cyan>[{player.name}] kích hoạt Effect: DRAW BOTTOM</color>");

                float drawBottomDuration = 1.0f;

                if (drawBottomEffectPrefab != null)
                {
                    Vector3 spawnPos = Vector3.zero;
                    GameObject drawBottomFX = Instantiate(drawBottomEffectPrefab, spawnPos, Quaternion.identity, CardController.canvasTransform);
                    EffectAnimation fxPlayer = drawBottomFX.GetComponent<EffectAnimation>();
                    if (fxPlayer != null) fxPlayer.effectDuration = drawBottomDuration;
                }

                yield return new WaitForSeconds(drawBottomDuration);

                if (player.type == PlayerType.Bot || player.type == PlayerType.Human)
                {
                    isTurnActionInProgress = false;
                }

                // Rút bài từ đáy -> Gọi hàm với tham số true
                // Lưu ý: DrawCardRoutine đã là Coroutine nên dùng 'yield return' để chờ nó xong.
                isBotThinking = false;
                yield return StartCoroutine(DrawCardRoutine(fromBottom: true));
                break;

            case DrawPileManager.CardType.SeeFuture:
                Debug.Log($"<color=purple>[{player.name}] kích hoạt Effect: SEE FUTURE (Soi 3 lá đầu)</color>");

                float futureDuration = 2.0f; // Thời gian FX Mắt Thần

                // 1. GỌI HIỆU ỨNG MẮT THẦN (Giữ nguyên code Instantiate FX)
                if (seeTheFutureEffectPrefab != null)
                {
                    Vector3 spawnPos = Vector3.zero;
                    GameObject futureFX = Instantiate(seeTheFutureEffectPrefab, spawnPos, Quaternion.identity, CardController.canvasTransform);
                    EffectAnimation fxPlayer = futureFX.GetComponent<EffectAnimation>();
                    if (fxPlayer != null) fxPlayer.effectDuration = futureDuration;
                }

                if (player.type == PlayerType.Human)
                {
                    List<DrawPileManager.CardType> futureCards = drawPileManager.GetTopCards(3);
                    yield return StartCoroutine(ShowFutureCardsRoutine(futureCards));
                }
                else
                {
                    Debug.Log($"{player.name} đang tỏ ra nguy hiểm khi nhìn trộm tương lai...");
                    yield return new WaitForSeconds(2.0f);
                    // Bot đã tính toán xong logic ẩn, không cần chờ thêm
                }
                // Lưu ý: See Future không kết thúc lượt, người chơi sẽ tự quyết định làm gì tiếp theo
                isBotThinking = false;
                isTurnActionInProgress = false;
                break;
        }
    }

    // Coroutine hiển thị 3 lá tương lai
    IEnumerator ShowFutureCardsRoutine(List<DrawPileManager.CardType> cards)
    {
        yield return new WaitForSeconds(1.0f);

        // 1. Khóa nút bấm để người chơi tập trung xem
        if (drawButton != null) drawButton.interactable = false;
        if (playButton != null) playButton.interactable = false;

        if (turnInfoText != null) turnInfoText.text = "ĐANG SOI\nTƯƠNG LAI...";

        // 2. Tạo Visual cho các lá bài
        List<GameObject> tempCards = new List<GameObject>();
        float startX = -((cards.Count - 1) * 250f) / 2; // Căn giữa, khoảng cách 250 unit

        for (int i = 0; i < cards.Count; i++)
        {
            GameObject cardObj = Instantiate(GetPrefabByType(cards[i]), CardController.canvasTransform);

            // Đặt vị trí (Giữa màn hình + Offset theo chiều ngang)
            cardObj.transform.localPosition = new Vector3(startX + (i * 250f), 0, 0);
            cardObj.transform.localScale = Vector3.one * 1.5f; // Phóng to lên chút cho dễ nhìn

            // Xóa script điều khiển để không bấm vào được
            Destroy(cardObj.GetComponent<CardController>());
            Destroy(cardObj.GetComponent<Button>());

            tempCards.Add(cardObj);
        }

        // 3. Chờ 5 giây
        yield return new WaitForSeconds(5.0f);

        // 4. Xóa Visual
        foreach (var c in tempCards)
        {
            Destroy(c);
        }

        // 5. Trả lại quyền điều khiển (Mở lại nút)
        Player currentP = players[currentPlayerIndex];
        string turnDetail = turnsRemaining > 1 ? $" (\nPhải rút {turnsRemaining} lá)" : "";
        if (turnInfoText != null) turnInfoText.text = $"Lượt của:\n{currentP.name}{turnDetail}";

        if (drawButton != null) drawButton.interactable = true;

        // Cập nhật lại nút Play (nếu đang chọn bài thì sáng, không thì tắt)
        if (playButton != null) playButton.interactable = (CardController.selectedCard != null);
    }


    // Hàm xử lý người chơi bị loại
    public IEnumerator HandlePlayerDeathRoutine(Player p)
    {
        // THỜI GIAN CỐ ĐỊNH = 1.0s (dây cháy) + 1.5s (nổ) = 2.5s
        float totalExplodeDuration = 2.5f; // << GIỮ GIÁ TRỊ NÀY

        // 1. GỌI EXPLODE SEQUENCE MASTER
        if (pendingBombVisual != null && explodeMasterEffectPrefab != null)
        {
            Vector3 explosionPos = pendingBombVisual.transform.position;

            // TẠO MASTER FX
            GameObject explosionFX = Instantiate(explodeMasterEffectPrefab, explosionPos, Quaternion.identity, CardController.canvasTransform);

            // Chờ hiệu ứng nổ hoàn tất (2.5 giây)
            yield return new WaitForSeconds(totalExplodeDuration);
        }

        // 2. Xóa visual quả bom đang treo
        if (pendingBombVisual != null) Destroy(pendingBombVisual);

        // 3. Đánh dấu chết và cập nhật UI
        p.isDead = true;
        UpdateUIForBot(p); // Hàm này có thể cần được gọi ngay lập tức

        Debug.Log($"{p.name} đã bị loại!");

        // 4. KIỂM TRA THẮNG THUA NGAY TẠI ĐÂY
        if (CheckForWinner())
        {
            // Nếu có người thắng, hàm sẽ return true và StopAllCoroutines (trong CheckForWinner)
            yield break; // Dừng Coroutine tại đây
        }

        // 5. QUAN TRỌNG: Reset mutex trước khi chuyển lượt
        isTurnActionInProgress = false;
        IsDefusing = false;

        // 6. Nếu game chưa kết thúc, chuyển lượt cho người tiếp theo
        nextTurnStartingCount = 1;
        EndTurn(); // Kết thúc lượt (chuyển sang người tiếp theo)
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

            if (AudioManager.Instance != null)
            {
                // Phát nhạc chiến thắng (Nhạc này cũng sẽ loop nếu bạn dùng PlayMusic)
                AudioManager.Instance.PlayMusic(AudioManager.Instance.victoryMusic);
            }

            return true; // Đã có người thắng
        }

        return false; // Chưa ai thắng, chơi tiếp
    }
    #endregion

    #region TURN LOGIC
    // Bắt đầu lượt
    void StartTurn()
    {
        // ĐẢM BẢO MUTEX ĐƯỢC RESET TRƯỚC KHI BẮT ĐẦU LƯỢT MỚI
        if (isTurnActionInProgress)
        {
            Debug.LogWarning($"StartTurn: isTurnActionInProgress vẫn là true, resetting...");
            isTurnActionInProgress = false;
        }

        isBotThinking = false;

        Player currentP = players[currentPlayerIndex];
        if (currentP.isDead) { EndTurn(); return; }
        if (currentP.type == PlayerType.Human) da_chuyen_luot_sang_bot = false;

        if (turnInfoText != null) turnInfoText.text = $"Lượt của:\n{currentP.name}";

        // LUÔN DỪNG TIMER VÀO ĐẦU LƯỢT MỚI
        // if (turnTimer != null) turnTimer.StopTimer();

        // Kích hoạt Timer cho CẢ Human và Bot
        if (turnTimer != null) turnTimer.StartTimer(defaultTurnTime, false);

        if (currentP.type == PlayerType.Human)
        {
            if (drawButton != null) drawButton.interactable = true;
        }
        else
        {
            if (drawButton != null) drawButton.interactable = false;

            // ĐỢI MỘT CHÚT TRƯỚC KHI BOT BẮT ĐẦU SUY NGHĨ
            StartCoroutine(DelayedBotStart(0.5f));
        }
    }

    IEnumerator DelayedBotStart(float delay)
    {
        yield return new WaitForSeconds(delay);

        Player currentP = players[currentPlayerIndex];
        if (currentP.type == PlayerType.Bot && !currentP.isDead && !isBotThinking)
        {
            StartCoroutine(BotPlayRoutine());
        }
    }

    // Hàm kiểm tra trạng thái lượt
    private void CheckTurnStatus()
    {
        Debug.Log($"CheckTurnStatus: current={players[currentPlayerIndex].name}, turnsRemaining={turnsRemaining}, isTurnActionInProgress={isTurnActionInProgress}");

        Player currentP = players[currentPlayerIndex];
        if (turnsRemaining > 0)
        {
            Debug.Log($"[{currentP.name}] Vẫn còn {turnsRemaining} lượt rút nữa!");
            if (turnInfoText != null)
            {
                string turnDetail = turnsRemaining > 1 ? $"\n(Phải rút {turnsRemaining} lá)" : "";
                turnInfoText.text = $"Lượt của:\n{currentP.name}{turnDetail}";
            }

            // Nếu là Người, bật lại nút rút bài (vì nó bị tắt lúc bắt đầu DrawCardRoutine)
            if (currentP.type == PlayerType.Human)
            {
                if (drawButton != null) drawButton.interactable = true;
                if (turnTimer != null) turnTimer.StartTimer(defaultTurnTime, false);
            }
            else if (currentP.type == PlayerType.Bot)
            {
                if (turnTimer != null)
                {
                    // ✅ MỚI (CHỈ ĐẢM BẢO TIMER ĐANG CHẠY TỪ MỐC HIỆN TẠI NẾU CÒN LƯỢT)
                    // Việc Reset Timer về 10s chỉ nên xảy ra trong StartTurn().
                    turnTimer.StartTimer(defaultTurnTime, false);
                    Debug.Log($"<color=lime>{currentP.name} còn lượt. BẬT LẠI TIMER ĐẾM TIẾP từ {turnTimer.CurrentTimeValue:F1}s.</color>");
                }

                if (drawButton != null) drawButton.interactable = false;

                // CHỈ gọi BotPlayRoutine nếu bot chưa đang suy nghĩ
                //StartCoroutine(BotPlayRoutine());
                if (!isBotThinking && !isTurnActionInProgress)
                {
                    StartCoroutine(BotPlayRoutine());
                }
                else
                {
                    Debug.Log($"{currentP.name} đang suy nghĩ hoặc có hành động, không gọi lại BotPlayRoutine");
                }
            }

        }
        else
        {
            // Đã trả hết nợ -> Kết thúc lượt thực sự
            EndTurn();
        }
    }

    // Hàm phụ trợ cho Bot
    IEnumerator DelayedBotDraw(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Kiểm tra lại trạng thái sau khi chờ
        Player currentP = players[currentPlayerIndex];
        if (!currentP.isDead && turnsRemaining > 0 && !isTurnActionInProgress)
        {
            StartCoroutine(DrawCardRoutine());
        }
    }

    // Kết thúc lượt 
    public void EndTurn()
    {
        // QUAN TRỌNG: Reset mutex trước khi chuyển lượt
        isTurnActionInProgress = false;

        Player p = players[currentPlayerIndex];

        if (turnsRemaining > 0 && !p.isDead) // khi kết thúc lượt hiện tại, nếu vẫn còn lượt phải đi mà chưa chết thì phải gọi CheckTurnStatus() để xử lý nốt các lượt còn lại
        {
            CheckTurnStatus();
        }
        else
        {
            // Áp dụng bounded buffer để chuyển index
            currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;

            // Tìm người chơi tiếp theo CÒN SỐNG
            int originalIndex = currentPlayerIndex;
            while (players[currentPlayerIndex].isDead)
            {
                currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;

                // Tránh infinite loop nếu tất cả đều chết
                if (currentPlayerIndex == originalIndex)
                {
                    Debug.LogError("Tất cả người chơi đã chết!");
                    return;
                }
            }

            // Gán số lượt phải đi cho người mới
            turnsRemaining = nextTurnStartingCount;

            // Reset số lượt tiếp theo
            nextTurnStartingCount = 1;

            // Reset trạng thái defusing
            IsDefusing = false;

            da_chuyen_luot_sang_bot = true;

            StartTurn();
        }
    }

    public void HandleTimerTimeout()
    {
        Debug.Log("Hết thời gian! Timer dừng lại.");
        turnTimer.StopTimer();

        Player currentP = players[currentPlayerIndex];

        // 1. Kiểm tra player đã chết
        if (currentP.isDead)
        {
            Debug.Log($"HandleTimerTimeout: {currentP.name} đã chết, bỏ qua timeout.");
            isTurnActionInProgress = false; // Reset cho an toàn
            return;
        }

        // 2. Kiểm tra đang có hành động khác chạy
        if (isTurnActionInProgress)
        {
            Debug.LogWarning($"Timer timeout bị bỏ qua: Đang có hành động khác chạy cho {currentP.name}");
            return;
        }

        // 3. Xử lý DEFUSE timeout
        if (IsDefusing)
        {
            Debug.Log($"<color=red>HẾT GIỜ GỠ BOM! {currentP.name} CHẾT!</color>");
            IsDefusing = false;

            // FORCE DESELECT lá Defuse nếu đang chọn
            if (CardController.selectedCard != null)
            {
                CardController.selectedCard.ForceDeselect();
            }

            StartCoroutine(HandlePlayerDeathRoutine(currentP));
            return;
        }

        // 4. Xử lý RÚT BÀI timeout
        Debug.Log($"<color=orange>HẾT GIỜ! Tự động RÚT BÀI cho {currentP.name}.</color>");

        // FORCE DESELECT lá bài đang chọn (nếu có)
        if (CardController.selectedCard != null)
        {
            CardController.selectedCard.ForceDeselect();
            Debug.Log($"Đã force deselect lá bài {CardController.selectedCard?.cardType}");
        }

        // Reset nút chơi bài
        if (playButton != null)
        {
            playButton.interactable = false;
        }

        StartCoroutine(DrawCardRoutine(isForcedDraw: true));
    }

    // Hàm khẩn cấp reset trạng thái
    public void EmergencyReset()
    {
        Debug.LogError($"EMERGENCY RESET CALLED! isTurnActionInProgress={isTurnActionInProgress}, IsDefusing={IsDefusing}");

        isTurnActionInProgress = false;
        IsDefusing = false;

        StopAllCoroutines();

        // Reset timer
        if (turnTimer != null)
        {
            turnTimer.StopTimer();
            turnTimer.StartTimer(defaultTurnTime, false);
        }

        // Enable draw button nếu là human
        Player currentP = players[currentPlayerIndex];
        if (currentP.type == PlayerType.Human && drawButton != null)
        {
            drawButton.interactable = true;
        }

        Debug.Log($"Emergency reset completed. Current player: {currentP.name}");
    }
    #endregion

    #region BOT LOGIC
    // Quản lý lượt chơi của Bot
    IEnumerator BotPlayRoutine()
    {
        int myTurnIndex = currentPlayerIndex;
        Player botPlayer = players[currentPlayerIndex];

        // KIỂM TRA BAN ĐẦU
        if (currentPlayerIndex != myTurnIndex)
        {
            Debug.Log($"{botPlayer.name} không thể bắt đầu lượt vì lượt hiện tại không phải là của nó");
            yield break;
        }
        else if (botPlayer.isDead)
        {
            Debug.Log($"{botPlayer.name} không thể bắt đầu lượt vì nó đã chết");
            yield break;
        }
        else if (isBotThinking)
        {
            Debug.Log($"{botPlayer.name} không thể hành động vì nó đang suy nghĩ...");
            yield break;
        }

        isBotThinking = true; // ĐÁNH DẤU BOT ĐANG SUY NGHĨ

        try
        {
            // 1. Bot suy nghĩ
            float thinkTime = Random.Range(1f, 3f); // Giảm thời gian suy nghĩ
            Debug.Log($"{botPlayer.name} đang suy nghĩ trong {thinkTime:F1}s...");
            yield return new WaitForSeconds(thinkTime);

            // 2. Kiểm tra lại sau khi suy nghĩ
            if (currentPlayerIndex != myTurnIndex || botPlayer.isDead)
            {
                Debug.Log($"{botPlayer.name} không còn lượt sau khi suy nghĩ");
                yield break;
            }

            if (isTurnActionInProgress)
            {
                Debug.LogWarning($"Hành động chủ động của {botPlayer.name} (đánh bài/rút bài thủ công) vừa bị từ chối! Đang có hành động khác diễn ra!");
                yield break;
            }

            // 3. Quyết định đánh bài
            DrawPileManager.CardType cardToPlay = BotDecideBestCard(botPlayer);

            if (cardToPlay != DrawPileManager.CardType.None) // Nếu quyết định đánh bài
            {
                Debug.Log($"{botPlayer.name} quyết định đánh lá: {cardToPlay}");

                isTurnActionInProgress = true; // KHÓA HÀNH ĐỘNG

                try
                {
                    yield return StartCoroutine(BotPlayCardAction(botPlayer, cardToPlay));

                    // Kiểm tra lại sau khi đánh bài
                    if (currentPlayerIndex != myTurnIndex || botPlayer.isDead)
                    {
                        Debug.Log($"{botPlayer.name} đã mất lượt sau khi đánh bài");
                        yield break;
                    }

                    // Xử lý các lá bài kết thúc lượt
                    if (cardToPlay == DrawPileManager.CardType.Skip ||
                        cardToPlay == DrawPileManager.CardType.Attack ||
                        cardToPlay == DrawPileManager.CardType.DrawBottom)
                    {
                        Debug.Log($"{botPlayer.name} đánh {cardToPlay} -> Kết thúc lượt");
                        EndTurn();
                        yield break;
                    }

                    // Xử lý SeeFuture
                    if (cardToPlay == DrawPileManager.CardType.SeeFuture)
                    {
                        var topCards = drawPileManager.GetTopCards(3);
                        bool hasBomb = topCards.Contains(DrawPileManager.CardType.Explode);

                        if (hasBomb && HasEscapeCard(botPlayer))
                        {
                            Debug.Log($"<color=yellow>{botPlayer.name} thấy Bom! Tìm cách né...</color>");
                            yield return new WaitForSeconds(0.8f);

                            DrawPileManager.CardType escapeCard = FindBestEscapeCard(botPlayer);
                            if (escapeCard != DrawPileManager.CardType.None)
                            {
                                Debug.Log($"<color=green>{botPlayer.name} đánh {escapeCard} để né bom!</color>");
                                yield return StartCoroutine(BotPlayCardAction(botPlayer, escapeCard));

                                if (currentPlayerIndex != myTurnIndex) yield break;
                                if (IsTurnEndingCard(escapeCard))
                                {
                                    EndTurn();
                                    yield break;
                                }
                            }
                        }
                    }

                    // Nếu vẫn còn lượt sau khi đánh bài (Shuffle, SeeFuture không dẫn đến EndTurn)
                    if (turnsRemaining > 0 && currentPlayerIndex == myTurnIndex && !botPlayer.isDead)
                    {
                        isTurnActionInProgress = false; // <--- RESET MUTEX NGAY TẠI ĐÂY!

                        // === BẬT LẠI TIMER ĐẾM TIẾP (Mốc dừng) SAU KHI ĐÁNH BÀI ===
                        Debug.Log($"{botPlayer.name} đánh bài xong. Bật lại Timer đếm tiếp.");
                        if (turnTimer != null)
                            turnTimer.StartTimer(turnTimer.CurrentTimeValue, false);

                        Debug.Log($"{botPlayer.name} vẫn còn {turnsRemaining} lượt, tiếp tục chơi...");

                        // Đợi một chút trước khi đánh bài. Nếu Timer hết giờ trong lúc này, HandleTimerTimeout sẽ xử lý
                        yield return new WaitForSeconds(0.8f);

                        // Kiểm tra lại: Nếu Bot vẫn chưa bị Timeout (vẫn còn lượt), và Timer chưa chạy hết, thì Bot sẽ chơi bài tiếp theo logic này.

                        if (!isTurnActionInProgress) // Đảm bảo chưa có hành động nào khác (ví dụ: Timeout) chen vào
                        {
                            StartCoroutine(BotPlayRoutine());
                        }
                        else
                        {
                            Debug.LogWarning($"Đang có hành động khác diễn ra, {botPlayer.name} chưa thể bắt đầu vòng chơi mới");
                        }
                    }
                }
                finally { }
            }
            else // Không đánh bài -> Rút bài
            {
                // Đợi một chút cho tự nhiên
                yield return new WaitForSeconds(0.8f);

                if (!isTurnActionInProgress) // Check một lần cuối
                {
                    // Gọi DrawCardRoutine. Nó sẽ tự khóa và xử lý.
                    yield return StartCoroutine(DrawCardRoutine()); // <--- Dùng yield return để đợi
                }
                else
                {
                    Debug.LogWarning($"{botPlayer.name} bị từ chối rút bài thủ công vì đang có hành động khác diễn ra!");
                }

                // rút bài xong mà vẫn còn lượt
                if (turnsRemaining > 0 && currentPlayerIndex == myTurnIndex && !botPlayer.isDead)
                {
                    isTurnActionInProgress = false;

                    /*Debug.Log($"{botPlayer.name} rút bài xong. Reset Timer cho lượt tiếp theo.");
                    if (turnTimer != null)
                        turnTimer.StartTimer(defaultTurnTime, false);*/

                    Debug.Log($"{botPlayer.name} vẫn còn {turnsRemaining} lượt, tiếp tục chơi...");

                    yield return new WaitForSeconds(0.8f);

                    if (!isTurnActionInProgress) 
                    {
                        StartCoroutine(BotPlayRoutine());
                    }
                    else
                    {
                        Debug.LogWarning("Bỏ qua DrawCardRoutine vì hành động khác đã khóa (có thể là Timer Timeout).");
                    }
                }
            }
        }
        finally
        {
            // LUÔN ĐẢM BẢO RESET TRẠNG THÁI SUY NGHĨ
            isBotThinking = false;
            Debug.Log($"1 lần hành động của {botPlayer.name} vừa được xử lý, isBotThinking = {isBotThinking}");
        }
    }

    // Kiểm tra bot có bài để né bom không
    bool HasEscapeCard(Player bot)
    {
        return bot.hand.Contains(DrawPileManager.CardType.Skip) ||
               bot.hand.Contains(DrawPileManager.CardType.Attack) ||
               bot.hand.Contains(DrawPileManager.CardType.DrawBottom) ||
               bot.hand.Contains(DrawPileManager.CardType.Shuffle);
    }

    // Tìm bài né bom tốt nhất
    DrawPileManager.CardType FindBestEscapeCard(Player bot)
    {
        // Ưu tiên: Skip > Attack > DrawBottom > Shuffle
        if (bot.hand.Contains(DrawPileManager.CardType.Skip))
            return DrawPileManager.CardType.Skip;
        if (bot.hand.Contains(DrawPileManager.CardType.Attack))
            return DrawPileManager.CardType.Attack;
        if (bot.hand.Contains(DrawPileManager.CardType.DrawBottom))
            return DrawPileManager.CardType.DrawBottom;
        if (bot.hand.Contains(DrawPileManager.CardType.Shuffle))
            return DrawPileManager.CardType.Shuffle;

        return DrawPileManager.CardType.None;
    }

    // Kiểm tra lá bài có kết thúc lượt không
    bool IsTurnEndingCard(DrawPileManager.CardType cardType)
    {
        return cardType == DrawPileManager.CardType.Skip ||
               cardType == DrawPileManager.CardType.Attack ||
               cardType == DrawPileManager.CardType.DrawBottom;
    }

    // Thuật toán chọn bài cho bot
    DrawPileManager.CardType BotDecideBestCard(Player bot)
    {
        // Ưu tiên 1:
        // Nếu đang bị dính Attack (phải rút nhiều hơn 1 lá) -> Ưu tiên tìm Attack để phản đòn
        if (turnsRemaining > 1)
        {
            if (bot.hand.Contains(DrawPileManager.CardType.Attack)) return DrawPileManager.CardType.Attack;
        }

        // Ưu tiên 2: Đánh lá SeeFuture -> để soi
        if (bot.hand.Contains(DrawPileManager.CardType.SeeFuture) && Random.value > 0.2f)
            return DrawPileManager.CardType.SeeFuture;

        // Ngẫu hứng đánh lá Attack
        if (bot.hand.Contains(DrawPileManager.CardType.Attack) && Random.value > 0.7f)
            return DrawPileManager.CardType.Attack;

        // Ngẫu hứng đánh lá DrawBottom
        if (bot.hand.Contains(DrawPileManager.CardType.DrawBottom) && Random.value > 0.7f)
            return DrawPileManager.CardType.DrawBottom;

        // Ngẫu hứng đánh lá Shuffle
        if (bot.hand.Contains(DrawPileManager.CardType.Shuffle) && Random.value > 0.5f)
            return DrawPileManager.CardType.Shuffle;

        // Mặc định: Không đánh gì cả (để đi Rút bài)
        return DrawPileManager.CardType.None; // Tạm quy ước Skip ở hàm này là "Bỏ qua việc đánh"
    }

    // Hành động đánh bài của bot   
    IEnumerator BotPlayCardAction(Player bot, DrawPileManager.CardType cardType)
    {
        if (turnTimer != null) // dừng timer khi bot đánh bài
        {
            Debug.Log($"Quyết định đánh bài từ {bot.name}, dừng Timer!");
            turnTimer.StopTimer();

        }

        // THÊM NULL-CHECK QUAN TRỌNG
        if (bot == null || bot.botDisplayUI == null)
        {
            Debug.LogError($"BotPlayCardAction: Bot hoặc botDisplayUI bị null! Card: {cardType}");
            isTurnActionInProgress = false;
            yield break;
        }

        // 1. Xóa bài khỏi tay Bot (xóa trong data)
        bot.hand.Remove(cardType);
        drawPileManager.AddToDiscardPile(cardType);

        // 2. Cập nhật UI (Xóa bớt 1 lưng bài)
        UpdateUIForBot(bot);

        // 3. Spawn Visual lá bài bay ra giữa bàn
        if (bot.botDisplayUI == null || bot.botDisplayUI.handArea == null)
        {
            Debug.LogError($"LỖI {cardType} của {bot.name}: botDisplayUI hoặc handArea bị NULL. Bỏ qua animation.");
            // Tiếp tục logic xử lý data mà không cần animation
        }
        else
        {
            // 3. Spawn Visual lá bài bay ra giữa bàn
            GameObject prefabToSpawn = GetPrefabByType(cardType);
            GameObject cardVisual = Instantiate(prefabToSpawn, bot.botDisplayUI.handArea.position, Quaternion.identity, CardController.canvasTransform);

            yield return StartCoroutine(MoveToPosition(cardVisual.transform, discardPileTransform.position, 0.4f));

            // Xử lý Visual
            cardVisual.transform.SetParent(discardPileTransform);
            cardVisual.transform.localPosition = Vector3.zero;
            cardVisual.transform.localRotation = Quaternion.identity;
            cardVisual.transform.localScale = Vector3.one;
        }

        // 4. Xử lý hiệu ứng bài (HandleCardEffect sẽ xử lý logic kết thúc lượt)
        yield return StartCoroutine(HandleCardEffect(cardType, bot));

        // KHÔNG CẦN BẬT LẠI TIMER NGAY TẠI ĐÂY
        // Việc bật lại/reset Timer sẽ được xử lý ở cuối BotPlayRoutine
    }
    #endregion

    #region HUMAN INPUT
    public void OnPlayCardButtonClicked()
    {
        StartCoroutine(OnPlayCardButtonPress());
    }

    // Hàm sự kiện của nút đánh bài
    public IEnumerator OnPlayCardButtonPress()
    {
        if (CardController.selectedCard == null) yield break;

        // <<< BỔ SUNG: KIỂM TRA VÀ BẬT KHÓA NGAY LẬP TỨC >>>
        if (isTurnActionInProgress && !IsDefusing) // <<< THÊM: && !IsDefusing
        {
            Debug.LogWarning("Đánh bài bị từ chối: Đang có hành động khác chạy.");
            yield break;
        }
        if (!IsDefusing)
        {
            isTurnActionInProgress = true;
        }

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

                // --- 1.5. Đợi lá Defuse bay đi xong (0.4s) ---
                yield return new WaitForSeconds(0.4f);

                // --- 1.6. GỌI HIỆU ỨNG DEFUSE TẠI ĐÂY ---
                float defuseDuration = 1.6f; // Thời gian FX (đã thiết lập)
                if (defuseEffectPrefab != null && pendingBombVisual != null)
                {
                    // Vị trí xuất hiện: Ngay tại vị trí của quả bom đang treo
                    Vector3 spawnPos = pendingBombVisual.transform.position;
                    GameObject defuseFX = Instantiate(defuseEffectPrefab, spawnPos, Quaternion.identity, CardController.canvasTransform);

                    EffectAnimation fxPlayer = defuseFX.GetComponent<EffectAnimation>();
                    if (fxPlayer != null) fxPlayer.effectDuration = defuseDuration;

                    yield return new WaitForSeconds(defuseDuration);
                }

                // 2. Tắt chế độ nguy hiểm
                IsDefusing = false;

                // 3. Xử lý Quả Bom đang treo (pendingBombVisual)

                Destroy(pendingBombVisual); // Xóa visual bom cũ

                int randomSlot = Random.Range(0, drawPileManager.GetRemainingCount());
                drawPileManager.InsertCardToDeck(DrawPileManager.CardType.Explode, randomSlot);

                Debug.Log($"Đã gỡ bom thành công! Bom nằm ở vị trí: {randomSlot}");

                UpdateDrawPileCountUI();

                // 4. Update UI & End Turn
                UpdateUIForBot(currentP);
                turnsRemaining--;
                CheckTurnStatus();

                isTurnActionInProgress = false;
            }
            yield break; // Quan trọng: Return luôn, không chạy logic đánh bài thường bên dưới
        }

        // B. NẾU KHÔNG TRÚNG BOOM
        Debug.Log($"Bạn quyết định đánh lá: {cardObj.cardType}");
        // 1. Xóa lá bài và đưa vào bộ bỏ (xử lý data)
        ProcessCardData(currentP, cardObj.cardType);

        // TẮT TIMER NGAY KHI ĐÁNH BÀI
        if (turnTimer != null)
        {
            Debug.Log("Quyết định đánh bài từ Main Player, dừng Timer!");
            turnTimer.StopTimer();
        }

        // 2. KÍCH HOẠT ANIMATION BAY TỪ TAY ĐẾN BỘ BỎ
        cardObj.PlayCard(discardPileTransform);
        yield return new WaitForSeconds(0.4f);

        // 3. Reset trạng thái
        CardController.selectedCard = null;
        UpdateUIForBot(currentP);

        // 4. Xử lý hiệu ứng lá bài
        yield return StartCoroutine(HandleCardEffect(cardObj.cardType, currentP));

        // Nếu lượt chơi chưa kết thúc sau khi xử lý hiệu ứng (vì turnsRemaining > 0) VÀ là người chơi Human VÀ lượt chưa chuyển sang người tiếp theo, ta cần tiếp tục đếm ngược.
        if (turnsRemaining > 0 && currentP.type == PlayerType.Human && !da_chuyen_luot_sang_bot)
        {
            // BẬT LẠI TIMER TỪ MỐC ĐÃ DỪNG
            if (turnTimer != null) turnTimer.StartTimer(turnTimer.CurrentTimeValue, false);

            // Bật lại các nút tương tác
            if (drawButton != null) drawButton.interactable = true;
            // Nút Play sẽ được Update() xử lý.

            isTurnActionInProgress = false;
        }
    }

    // Hàm sự kiện của nút rút bài
    public void OnDrawButtonPress()
    {
        // Cần phải kiểm tra xem có phải lượt của Human Player không (đảm bảo an toàn)
        Player currentP = players[currentPlayerIndex];
        if (currentP.type == PlayerType.Human)
        {
            // <<< BỔ SUNG: KIỂM TRA KHÓA >>>
            if (isTurnActionInProgress)
            {
                Debug.LogWarning("Bấm Rút bài bị từ chối: Đang có hành động khác chạy.");
                return;
            }

            // Bắt đầu Routine rút bài
            StartCoroutine(DrawCardRoutine());
        }
    }

    public void CheckDefuseSelection(DrawPileManager.CardType type, bool isSelecting)
    {
        // Chỉ xử lý nếu đang trong chế độ gỡ bom VÀ là người chơi Human
        if (!IsDefusing || players[currentPlayerIndex].type != PlayerType.Human)
        {
            return;
        }

        if (type == DrawPileManager.CardType.Defuse)
        {
            if (isSelecting)
            {
                // DỪNG TIMER NGAY LẬP TỨC KHI CHỌN LÁ DEFUSE
                if (turnTimer != null) turnTimer.StopTimer();
                Debug.Log("<color=cyan>ĐÃ DỪNG TIMER 3S. Người chơi đang chọn Defuse.</color>");
            }
            else
            {
                // BẬT LẠI TIMER TỪ MỐC ĐÃ DỪNG KHI BỎ CHỌN LÁ DEFUSE
                if (turnTimer != null) turnTimer.StartTimer(turnTimer.CurrentTimeValue, true); // true = Defuse Mode
                Debug.Log("<color=red>BỎ CHỌN DEFUSE. KÍCH HOẠT LẠI TIMER 3S.</color>");
            }
        }
    }
    #endregion

    #region UI LOGIC

    public void UpdateDrawPileCountUI()
    {
        if (drawPileCountText != null && drawPileManager != null)
        {
            int count = drawPileManager.GetRemainingCount();
            drawPileCountText.text = $"Bộ bài còn: {count} lá";
        }
    }

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

    void UpdateUIForBot(Player p)
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

    public void OnBackToLobbyBtnClick()
    {
        // Reset lại trạng thái tĩnh nếu cần (ví dụ chọn bài)
        CardController.selectedCard = null;
    
        // Load về scene phòng chờ
        SceneManager.LoadScene("LoadRoomScene");

        if (AudioManager.Instance != null)
        {
            // Chuyển lại nhạc Theme ngay khi nhấn nút quay về
            AudioManager.Instance.PlayMusic(AudioManager.Instance.themeMusic);
        }
    }
    #endregion
}
