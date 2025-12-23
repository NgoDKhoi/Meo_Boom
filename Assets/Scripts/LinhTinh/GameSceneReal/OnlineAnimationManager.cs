using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class OnlineAnimationManager : MonoBehaviour
{
    public static OnlineAnimationManager Instance;

    [Header("--- Prefabs & Visuals ---")]
    public GameObject cardBackPrefab;      // Prefab mặt sau lá bài
    public GameObject explosionFX;         // Bom nổ
    public GameObject defuseFX;            // Gỡ bom
    public GameObject seeFutureFX;         // Nhìn thấu tương lai
    public GameObject shuffleFX;           // Xào bài
    public GameObject attackFX;            // Tấn công
    public GameObject skipFX;              // Qua lượt (Skip)
    public GameObject drawBottomFX;        // Rút lá đít

    [Header("--- Positions ---")]
    public Transform drawPilePos;          // Chồng bài rút
    public Transform centerPlaySlot;       // Vị trí giữa màn hình để hiện lá bài đang đánh

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    // ================================================================
    // 1. ANIMATION: RÚT BÀI (THƯỜNG & ĐÁY)
    // ================================================================
    public void PlayDrawCardAnimation(string receiverName, Transform targetHandArea, bool isBottom = false)
    {
        StartCoroutine(DrawCardRoutine(receiverName, targetHandArea, isBottom));
    }

    private IEnumerator DrawCardRoutine(string receiverName, Transform targetHandArea, bool isBottom)
    {
        if (drawPilePos == null || targetHandArea == null) yield break;

        // Nếu rút đáy, hiện FX tại vị trí bộ bài trước
        if (isBottom) SpawnFX(drawBottomFX, drawPilePos.position);

        GameObject flyCard = Instantiate(cardBackPrefab, drawPilePos.position, Quaternion.identity);

        float duration = 0.4f;
        float elapsed = 0f;
        Vector3 startPos = drawPilePos.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            flyCard.transform.position = Vector3.Lerp(startPos, targetHandArea.position, elapsed / duration);
            yield return null;
        }

        Destroy(flyCard);
    }

    // ================================================================
    // 2. ANIMATION: CÁC LÁ BÀI ĐẶC BIỆT (CENTER FOCUS)
    // ================================================================
    public void PlayCardEffectAnimation(string senderName, DrawPileManager.CardType type)
    {
        Transform senderPos = GetPlayerTransform(senderName);
        if (senderPos == null) return;

        // Kích hoạt logic dựa trên loại thẻ
        switch (type)
        {
            case DrawPileManager.CardType.Explode:
                // Bom nổ ngay tại vị trí người chơi
                SpawnFX(explosionFX, senderPos.position);
                break;

            case DrawPileManager.CardType.Defuse:
                // Hiệu ứng gỡ bom
                SpawnFX(defuseFX, senderPos.position);
                break;

            case DrawPileManager.CardType.SeeFuture:
                // Thường hiện ở giữa màn hình
                SpawnFX(seeFutureFX, centerPlaySlot != null ? centerPlaySlot.position : Vector3.zero);
                break;

            case DrawPileManager.CardType.Shuffle:
                // Hiệu ứng xào bài tại chồng bài rút
                SpawnFX(shuffleFX, drawPilePos.position);
                break;

            case DrawPileManager.CardType.Attack:
                // Tấn công: hiện FX tại người đánh
                SpawnFX(attackFX, senderPos.position);
                break;

            case DrawPileManager.CardType.Skip:
                // Qua lượt
                SpawnFX(skipFX, senderPos.position);
                break;

            case DrawPileManager.CardType.DrawBottom:
                // Đánh lá rút đáy: hiện FX tại người đánh báo hiệu hành động
                SpawnFX(drawBottomFX, senderPos.position);
                break;
        }
    }

    // ================================================================
    // HELPER FUNCTIONS
    // ================================================================

    private void SpawnFX(GameObject prefab, Vector3 pos)
    {
        if (prefab == null) return;
        GameObject fx = Instantiate(prefab, pos, Quaternion.identity);

        // Nếu prefab có script EffectAnimation, nó sẽ tự hủy dựa trên effectDuration
        // Nếu không, ta có thể Destroy thủ công sau 2 giây
        EffectAnimation ea = fx.GetComponent<EffectAnimation>();
        if (ea == null) Destroy(fx, 2.0f);
    }

    private Transform GetPlayerTransform(string username)
    {
        if (username == RoomManager.Instance.currentUsername)
            return OnlineDrawManager.Instance.playerHandArea;

        return OnlineDrawManager.Instance.GetOpponentArea(username);
    }
}