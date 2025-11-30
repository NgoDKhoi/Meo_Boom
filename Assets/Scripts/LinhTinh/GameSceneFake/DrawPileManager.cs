using System.Collections.Generic;
using UnityEngine;

public class DrawPileManager : MonoBehaviour
{
    // --- KHAI BÁO ENUM (Giữ lại ở đây để các class khác dùng chung) ---
    public enum CardType { Defuse, Explode, Skip, Attack }

    [Header("--- DATA ---")]
    private List<CardType> drawPile = new List<CardType>(); // chồng bài rút
    private List<CardType> discardPile = new List<CardType>(); // chồng bài đã đánh

    [Header("--- CONFIG CARD COUNT---")]
    public int explodeCardCount = 3;
    public int defuseCardCount = 6;
    public int attackCardCount = 15;
    public int skipCardCount = 15;

    // Khởi tạo bộ bài không có BOOM
    public void PrepareSafeDeck(int playerCount)
    {
        drawPile.Clear();
        discardPile.Clear();

        // Thêm lá chức năng
        for (int i = 0; i < skipCardCount; i++) drawPile.Add(CardType.Skip);
        for (int i = 0; i < attackCardCount; i++) drawPile.Add(CardType.Attack);
        
        // Thêm lá defuse dư
        int defuseForDeck = defuseCardCount - playerCount;
        if (defuseForDeck > 0)
        {
            for (int i = 0; i < defuseForDeck; i++) drawPile.Add(CardType.Defuse);
        }

        ShuffleDrawPile();
        Debug.Log("DrawPileManager: Đã tạo bộ bài an toàn (Chưa có bom).");
    }

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

    // --- CÁC HÀM KHÁC ---

    // Hàm xào bộ bài rút
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