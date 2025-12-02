using System.Collections.Generic;
using UnityEngine;

public class DrawPileManager : MonoBehaviour
{
    // --- KHAI BÁO ENUM (Giữ lại ở đây để các class khác dùng chung) ---
    public enum CardType { None, Defuse, Explode, Skip, Attack, Shuffle, DrawBottom,  SeeFuture }

    [Header("--- DATA ---")]
    private List<CardType> drawPile = new List<CardType>(); // chồng bài rút
    private List<CardType> discardPile = new List<CardType>(); // chồng bài đã đánh

    [Header("--- CONFIG CARD COUNT---")]
    // Các lá nãy sẽ được cấu hình ở inspector của DrawPileManager
    public int explodeCardCount;
    public int defuseCardCount;
    public int skipCardCount;
    public int attackCardCount;
    public int shuffleCardCount;
    public int drawBottomCardCount;
    public int seeFutureCardCount;


    // Khởi tạo bộ bài không có BOOM
    public void PrepareSafeDeck(int playerCount)
    {
        drawPile.Clear(); 
        discardPile.Clear();

        // Thêm lá chức năng
        for (int i = 0; i < skipCardCount; i++) drawPile.Add(CardType.Skip);
        for (int i = 0; i < attackCardCount; i++) drawPile.Add(CardType.Attack);
        for (int i = 0; i < shuffleCardCount; i++) drawPile.Add(CardType.Shuffle);
        for (int i = 0; i < drawBottomCardCount; i++) drawPile.Add(CardType.DrawBottom);
        for (int i = 0; i < seeFutureCardCount; i++) drawPile.Add(CardType.SeeFuture);

        // Thêm lá defuse dư
        int defuseForDrawPile = defuseCardCount - playerCount;
        if (defuseForDrawPile > 0)
        {
            for (int i = 0; i < defuseForDrawPile; i++) drawPile.Add(CardType.Defuse);
        }

        ShuffleDrawPile();
        Debug.Log("DrawPileManager: Đã tạo bộ bài an toàn (Chưa có bom).");
    }

    // Thêm bom vô bộ bài
    public void AddExplodingKittens()
    {
        for (int i = 0; i < explodeCardCount; i++)
        {
            drawPile.Add(CardType.Explode);
        }

        // Xào bài lần 2 (Lần này mới thực sự nguy hiểm)
        ShuffleDrawPile();
        Debug.Log($"DrawPileManager: Đã thêm {explodeCardCount} lá Bom và xào lại bài!");
    }

    // Hàm nhét bài và vào vị trí cụ thể
    public void InsertCardToDeck(CardType card, int indexFromTop)
    {      
        int index = Mathf.Clamp(indexFromTop, 0, drawPile.Count); // Kiểm tra index hợp lệ để tránh lỗi
        drawPile.Insert(index, card);
        Debug.Log($"DrawPileManager: Đã nhét {card} vào vị trí {index}");
    }

    // Hàm xào bộ bài
    public void ShuffleDrawPile()
    {
        for (int i = 0; i < drawPile.Count; i++)
        {
            CardType temp = drawPile[i];
            int r = Random.Range(i, drawPile.Count);
            drawPile[i] = drawPile[r];
            drawPile[r] = temp;
        }
    }

    // Hàm rút bài trả về giá trị thẻ (Không xử lý logic game ở đây)
    public CardType DrawCardData()
    {
        if (drawPile.Count <= 0) return CardType.Skip; // Hết bài thì trả về rác

        CardType c = drawPile[0];
        drawPile.RemoveAt(0);
        return c;
    }

    // Hàm thêm bài đã đánh vào chồng bài bỏ
    public void AddToDiscardPile(CardType card)
    {
        discardPile.Add(card);
    }

    // Hàm kiểm tra số lượng bài còn lại
    public int GetRemainingCount() => drawPile.Count;
}