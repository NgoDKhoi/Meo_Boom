using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class DeckManager : MonoBehaviour
{
    // --- Định nghĩa dữ liệu        ---
    public enum CardType
    {
        Defuse, // Lá gỡ bom
        Explode // Lá nổ (Exploding)
    }

    public enum PlayerType 
    { 
        Human,
        Bot
    }

    public class Player
    {
        public string name;
        public PlayerType type;
        public List<CardType> hand = new List<CardType>();
        public bool isDead = false;
        public int defuseCount = 1;

        // UI hiển thị: Nếu là Human thì dùng area, nếu là Bot thì dùng script Display
        public OpponentDisplay botDisplayUI;
    }


    // --- Khởi tạo game ---
    public List<Player> players = new List<Player>(); // Chứa 4 người chơi
    public int currentPlayerIndex = 0;

    // --- Cấu hình UI & Prefab ---
    public GameObject defuseCardPrefab; // Biến chứa Prefab lá defuse (Kéo từ Project vào đây)
    public Transform playerHandArea; // Biến chứa khu vực tay người chơi (Kéo PlayerHandArea từ Hierarchy vào đây)
    public Button btnDraw;
    public TextMeshProUGUI txtTurnInfo; // Text thông báo lượt (Hoặc TextMeshProUGUI)

    // --- Cấu hình bộ bài ---
    public int explodeCardCount = 3; // 3 lá bom
    public int defuseCardCount = 32; // 32 lá gỡ bom

    
    // 3. List cho bộ bài
    private List<CardType> deck = new List<CardType>();

    void Start()
    {
        InitializeDeck();
    }

    // 1. Khởi tạo bộ bài
    void InitializeDeck()
    {
        // Xóa bài cũ nếu có
        deck.Clear(); 

        // Thêm lá bom vào bộ bài
        for (int i = 0; i < explodeCardCount; i++) deck.Add(CardType.Explode);

        // Thêm lá gỡ bom vào bộ bài
        for (int i = 0; i < defuseCardCount; i++) deck.Add(CardType.Defuse);

        // Xáo trộn ngay khi khởi tạo
        ShuffleDeck();

        Debug.Log($"Đã tạo bộ bài với tổng số lá: {deck.Count}");
    }

    // 2. Thuật toán xáo bài (Fisher-Yates Shuffle)
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
    }

    // 3. Ham nay se duoc goi khi Click vào Deck
    public void DrawCard()
    {
        // Kiểm tra xem còn bài không
        if (deck.Count <= 0)
        {
            Debug.LogWarning("Bộ bài đã hết!");
            return;
        }
      
        CardType drawnCard = deck[0]; // Rút lá trên cùng (index 0)
        deck.RemoveAt(0); // Loại bỏ lá vừa rút khỏi bộ bài

        ProcessCard(drawnCard); // Xử lý logic lá bài mình bốc trúng

        Debug.Log($"Còn lại {deck.Count} lá trong chồng bài.");
    }   

    // 4. Xử lý logic trúng bài
    void ProcessCard(CardType card)
    {
        if (card == CardType.Explode)
        {
            Warning();
        }
        else if (card == CardType.Defuse)
        {
            AddToHand();
        }
    }

    // --- Các hàm sự kiện ---
    void Warning()
    {
        Debug.Log("<color=red>BOOM! Bạn đã rút phải lá Mèo Nổ!</color>");
        // Sau này có thể thêm code hiển thị UI thua cuộc hoặc dùng Defuse tại đây
    }

    void AddToHand()
    {
        // Kiểm tra an toàn để tránh lỗi Null
        if (defuseCardPrefab == null || playerHandArea == null)
        {
            Debug.LogError("Chưa gán Prefab hoặc PlayerHandArea trong Inspector!");
            return;
        }

        // Tạo ra một bản sao của lá bài
        GameObject newCard = Instantiate(defuseCardPrefab, playerHandArea);

        // Đảm bảo lá bài mới sinh ra nằm đúng tỉ lệ (đôi khi Unity bị lỗi scale khi Instantiate UI)
        newCard.transform.localScale = Vector3.one;

        Debug.Log("<color=white>Đã thêm lá bài vào tay!</color>");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
