using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DrawPileManager;

public class GameManager : MonoBehaviour
{
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

    public Transform playerHandArea;       // Khu vực tay người
    public Transform discardPileTransform; // Khu vực bài đánh ra
    public Button drawButton;              // Nút rút bài
    public Button playButton;              // Nút đánh bài
    public TextMeshProUGUI turnInfoText;   // Thông báo lượt

    [Header("--- GAME STATE ---")]
    public List<Player> players = new List<Player>();
    public int currentPlayerIndex = 0;

    void Start()
    {
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

        // 3. GameManager thực hiện chia bài (Mỗi người 1 Defuse + 4 Random)
        DealInitialCards();

        // 4. Bảo DrawPileManager nhét BOM vào và xào lại
        deckManager.AddExplodingKittens();

        // 5. Bắt đầu lượt đầu tiên
        StartTurn();
    }

    void DealInitialCards()
    {
        foreach (var p in players)
        {
            // A. Tặng riêng mỗi người 1 lá Defuse
            p.hand.Add(DrawPileManager.CardType.Defuse);

            // B. Rút thêm 4 lá ngẫu nhiên từ bộ bài an toàn
            for (int k = 0; k < 4; k++)
            {
                // Gọi sang DrawPileManager để xin 1 lá
                DrawPileManager.CardType drawnCard = deckManager.DrawCardData();
                p.hand.Add(drawnCard);
            }

            // C. Cập nhật giao diện (Visual)
            UpdateUIForPlayer(p);
        }
        Debug.Log("GameManager: Đã chia xong (1 Defuse + 4 Random) cho mọi người!");
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
        yield return new WaitForSeconds(2f);
        OnDrawButtonPress(); // Bot tự bấm nút rút
    }

    public void EndTurn()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count; // Giống bounded buffer
        StartTurn();
    }

    // --- ACTION LOGIC ---
    // Hàm này gắn vào nút Rút Bài (Draw Button)
    public void OnDrawButtonPress()
    {
        if (deckManager.GetRemainingCount() <= 0) return;

        Player currentP = players[currentPlayerIndex];

        // 1. Lấy dữ liệu từ DrawPileManager
        DrawPileManager.CardType drawnCard = deckManager.DrawCardData(); // Tạo biến chứa bài đã rút

        // 2. Xử lý logic
        if (drawnCard == DrawPileManager.CardType.Explode)
        {
            Debug.Log($"<color=red>{currentP.name} RÚT TRÚNG BOM!</color>");
            currentP.isDead = true;
            UpdateUIForPlayer(currentP);
            EndTurn();
        }
        else
        {
            currentP.hand.Add(drawnCard);
            UpdateUIForPlayer(currentP);
            EndTurn();
        }
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

        // 3. Hiện visual ở giữa bàn
        GameObject prefab = GetPrefabByType(cardObj.cardType);
        if (prefab != null)
        {
            GameObject go = Instantiate(prefab, discardPileTransform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            Destroy(go.GetComponent<CardController>()); // Tắt script để không click được
        }

        // 4. Xóa visual trên tay
        Destroy(cardObj.gameObject);
        CardController.selectedCard = null;

        // Cập nhật lại UI tay (nếu cần) hoặc để Layout tự lo
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
        // ... (Logic cũ của bạn: Nếu là Human thì xóa HandArea rồi Instantiate lại, nếu là Bot thì update Text)
        // Lưu ý: Khi Instantiate nhớ gán: newCard.GetComponent<CardController>().cardType = c;
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
                    case CardType.Defuse: prefabToSpawn =defuseCardPrefab; break;
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
    }

}