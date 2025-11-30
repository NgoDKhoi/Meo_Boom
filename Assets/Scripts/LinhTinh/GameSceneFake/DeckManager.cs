using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class DeckManager : MonoBehaviour
{
    // --- Định nghĩa dữ liệu ---
    public enum CardType
    {
        Defuse,
        Explode,
        Skip,
        Attack
    }

    public enum PlayerType 
    { 
        Human,
        Bot
    }

    [System.Serializable]
    public class Player
    {
        public string name;
        public PlayerType type;

        [HideInInspector]
        public List<CardType> hand = new List<CardType>();

        [HideInInspector]
        public bool isDead = false;

        // UI hiển thị: Nếu là Human thì dùng area, nếu là Bot thì dùng script Display
        public OpponentDisplay botDisplayUI;
    }


    // --- Cấu hình game ---
    public List<Player> players = new List<Player>(); // Chứa 4 người chơi
    public int currentPlayerIndex = 0;

    // --- Cấu hình UI & Prefab ---
    public GameObject defuseCardPrefab; // Biến chứa Prefab lá defuse (Kéo từ Project vào đây)
    public GameObject explodeCardPrefab; // Biến chứa Prefab lá explode (Kéo từ Project vào đây)
    public GameObject skipCardPrefab; // Biến chứa Prefab lá skip (Kéo từ Project vào đây)
    public GameObject attackCardPrefab; // Biến chứa Prefab lá attack (Kéo từ Project vào đây)
    public Transform playerHandArea; // Biến chứa khu vực tay người chơi (Kéo PlayerHandArea từ Hierarchy vào đây)
    public Button btnDraw;
    public TextMeshProUGUI txtTurnInfo; // Text thông báo lượt (Hoặc TextMeshProUGUI)

    // --- Cấu hình bộ bài ---
    public int explodeCardCount = 3; 
    public int defuseCardCount = 5;
    public int attackCardCount = 20;
    public int skipCardCount = 20;

    public Transform discardPileLocation;


    void Start()
    {
        // 1. Kiểm tra và Reset dữ liệu người chơi
        if (players == null || players.Count == 0)
        {
            Debug.LogError("LỖI: Bạn chưa điền danh sách Players trong Inspector! Hãy xem hướng dẫn bên dưới.");
            return;
        }

        foreach (Player player in players)
        {
            player.hand.Clear();    // Xóa sạch bài trên tay (để chắc chắn)
            player.isDead = false;  // Hồi sinh người chơi

            // Cập nhật UI ban đầu cho Bot (Tên + 0 lá bài)
            if (player.type == PlayerType.Bot && player.botDisplayUI != null)
            {
                player.botDisplayUI.UpdateDisplay(player.name, 0, false);
            }
        }

        // 2. Khởi tạo bộ bài
        InitializeDeck();

        // 3. Bắt đầu game
        StartTurn();
    }



    // 1. Khởi tạo bộ bài
    private List<CardType> deck = new List<CardType>();
    void InitializeDeck()
    {
        // Xóa bài cũ nếu có
        deck.Clear();

        // Thêm lá skip vào bộ bài
        for (int i = 0; i < skipCardCount; i++) deck.Add(CardType.Skip);

        // Thêm lá attack vào bộ bài
        for (int i = 0; i < attackCardCount; i++) deck.Add(CardType.Attack);

        // Thêm lá defuse vào bộ bài
        for (int i = 0; i < defuseCardCount-4; i++) deck.Add(CardType.Defuse);

        // Xào bài
        ShuffleDeck();

        // Chia bài
        DealInitialCards();

        // Thêm lá bom vào bộ bài
        for (int i = 0; i < explodeCardCount; i++) deck.Add(CardType.Explode);

        // Xào bài lại tiếp
        ShuffleDeck();        
    }

    // 2. Bắt đầu lượt
    void StartTurn()
    {
        Player currentP = players[currentPlayerIndex];

        // Nếu người này đã chết -> Chuyển ngay sang người kế
        if (currentP.isDead)
        {
            EndTurn();
            return;
        }

        // Cập nhật txt_Info
        if (txtTurnInfo != null) txtTurnInfo.text = $"Lượt của: {currentP.name}";
        Debug.Log($"---> Lượt của: {currentP.name}");

        // Kiểm tra loại người chơi
        if (currentP.type == PlayerType.Human)
        {
            // Human: Mở khóa nút để bấm
            if (btnDraw != null) btnDraw.interactable = true;
        }
        else
        {
            // Bot: Khóa nút và tự động chơi
            if (btnDraw != null) btnDraw.interactable = false;
            StartCoroutine(BotPlayRoutine());  // Gọi hàm này để bot chơi chậm
        }
    }

    // 2a. Lượt bot
    IEnumerator BotPlayRoutine()
    {
        yield return new WaitForSeconds(3.5f); // Bot suy nghĩ 3.5s
        DrawCard(); // Bot tự gọi hàm rút bài
    }

    // 2b. Lượt người
    public void DrawCard()
    {
        // Kiểm tra xem còn bài không   
        if (deck.Count <= 0)
        {
            Debug.LogWarning("Bộ bài đã hết!");
            return;
        }

        Player currentP = players[currentPlayerIndex];

        // Rút 1 lá
        CardType drawnCard = deck[0];
        deck.RemoveAt(0);

        // Xử lý logic lá bài mình bốc trúng
        ProcessCard(currentP, drawnCard);

        Debug.Log($"Còn lại {deck.Count} lá trong chồng bài.");
    }

    // 3. Logic Bốc bài
    void ProcessCard(Player p, CardType card)
    {
        if (card == CardType.Explode)
        {
            Warning(p);
            UpdateUIForPlayer(p);
            EndTurn();
        }
        else
        {
            p.hand.Add(card);
            UpdateUIForPlayer(p);
            EndTurn();
        }     
    }
    void EndTurn()
    {
        // Chuyển lượt sang người kế tiếp (Vòng tròn)
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count; // Bounded buffer

        // Kiểm tra điều kiện thắng thua (Nếu chỉ còn 1 người sống) - Làm sau
        int deadCount = 0;
        foreach (Player p in players) 
        {
            if (p.isDead) deadCount++;
        }
        if (deadCount == 3)
        {
            Debug.Log("Bạn đã win");
        }

        // Bắt đầu lượt mới
        StartTurn();
    }


    // -- xu ly danh bai ---
    public void PlayCardAction(Card_Controller cardObj)
    {
        Player currentP = players[currentPlayerIndex];

        // 1. XỬ LÝ DATA: Xóa khỏi danh sách bài trên tay
        // Tìm và xóa lá bài có loại tương ứng trong List
        if (currentP.hand.Contains(cardObj.cardType))
        {
            currentP.hand.Remove(cardObj.cardType);
        }

        // 2. XỬ LÝ VISUAL (HIỆN UI): Tạo một lá bài nằm giữa bàn
        GameObject prefabToSpawn = null;
        switch (cardObj.cardType)
        {
            case CardType.Defuse: prefabToSpawn = defuseCardPrefab; break;
            case CardType.Explode: prefabToSpawn = explodeCardPrefab; break;
            case CardType.Skip: prefabToSpawn = skipCardPrefab; break;
            case CardType.Attack: prefabToSpawn = attackCardPrefab; break;
        }

        if (prefabToSpawn != null)
        {
            // Sinh ra lá bài tại vị trí Discard Pile
            GameObject playedCard = Instantiate(prefabToSpawn, discardPileLocation);

            // Reset scale và vị trí cho chuẩn
            playedCard.transform.localPosition = Vector3.zero;
            playedCard.transform.localScale = Vector3.one;

            // Xóa các script điều khiển trên lá bài ở giữa bàn (để nó nằm im, không click được nữa)
            Destroy(playedCard.GetComponent<Card_Controller>());
            Destroy(playedCard.GetComponent<BoxCollider2D>());
            // Nếu là UI Image thì giữ nguyên, nếu là Sprite thì chỉnh Order Layer cao lên
        }

        // 3. XỬ LÝ LOGIC GAME: (Ví dụ: Attack thì người sau bốc 2 lá...)
        Debug.Log($"Người chơi đã đánh lá: {cardObj.cardType}");
        // ProcessCardEffect(cardObj.cardType); // <--- Sau này viết hàm này sau

        // 4. CLEANUP: Xóa lá bài trên tay & Cập nhật lại UI Hand
        Destroy(cardObj.gameObject); // Xóa prefab trên tay

        // Cập nhật lại UI để các lá bài còn lại tự dồn hàng
        // (Lưu ý: Vì Destroy cần thời gian cuối frame mới mất, nên gọi UpdateUI ngay có thể bị lỗi hiển thị
        // Cách tốt nhất là chỉ cần Destroy, cái Layout Group sẽ tự lo phần dồn hàng)
    }


    // --- Các hàm sự kiện ---
    void Warning(Player p)
    {
        Debug.Log("<color=red>BOOM! Bạn đã rút phải lá Mèo Nổ!</color>");
        p.isDead = true;


        // Sau này có thể thêm code hiển thị UI thua cuộc hoặc dùng Defuse tại đây
        //if (p.hand.Count > 0)
        //{
        //    p.hand.RemoveAt(0); // Trừ 1 lá
        //    Debug.Log($"{p.name} dùng Gỡ Bom thoát chết! Còn {p.hand.Count} kit.");

        //    // (Nâng cao: Lẽ ra phải nhét lá Bom lại vào bộ bài, tạm thời bỏ qua)

        //    // Dù thoát chết thì vẫn hết lượt
        //    EndTurn();
        //}
    } // Cảnh báo
    
    void DealInitialCards()
    {
        // Chia tượng trưng bài vào tay (Logic chỉ thêm vào List, chưa hiện lên UI ngay)
        foreach (var p in players)
        {
            // Chia mỗi người 1 lá defuse
            p.hand.Add(CardType.Defuse);

            // Chia mỗi người 4 lá
            for (int k = 0; k < 4; k++)
            {
                CardType drawnCard = deck[0];
                deck.RemoveAt(0);
                p.hand.Add(drawnCard);
            }

            // Cập nhật UI ban đầu
            UpdateUIForPlayer(p);
        }
        Debug.Log("Đã chia bài!");
    } // Chia bài
    
    void ShuffleDeck()
    {
        for (int i = 0; i < deck.Count; i++)
        {
            CardType temp = deck[i];
            int randomIndex = Random.Range(i, deck.Count);
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
        Debug.Log("Đã xáo trộn bộ bài!");
    }  // Xào bài

    void UpdateUIForPlayer(Player p)
    {
        if (p.type == PlayerType.Human)
        {
            // Xóa bài cũ trên UI
            foreach (Transform child in playerHandArea) Destroy(child.gameObject);

            foreach (CardType c in p.hand)
            {
                GameObject prefabToSpawn = null;

                // Kiểm tra loại bài để chọn Prefab tương ứng
                switch (c)
                {
                    case CardType.Defuse: prefabToSpawn = defuseCardPrefab; break;
                    case CardType.Explode: prefabToSpawn = explodeCardPrefab; break;
                    case CardType.Skip: prefabToSpawn = skipCardPrefab; break;
                    case CardType.Attack: prefabToSpawn = attackCardPrefab; break;
                }

                // Chỉ tạo ra nếu có prefab
                if (prefabToSpawn != null)
                {
                    GameObject newCard = Instantiate(prefabToSpawn, playerHandArea);
                    newCard.transform.localScale = Vector3.one;
                }
            }
        }
        else
        {
            // Cập nhật số lượng bài cho Bot (Thông qua script OpponentDisplay)
            if (p.botDisplayUI != null)
            {
                p.botDisplayUI.UpdateDisplay(p.name, p.hand.Count, p.isDead);
            }
        }
    } // Cập nhật UI cho Player




    // Update is called once per frame
    void Update()
    {
        
    }
}
