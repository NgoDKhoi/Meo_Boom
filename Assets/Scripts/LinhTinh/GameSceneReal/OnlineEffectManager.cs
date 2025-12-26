using UnityEngine;
using System.Collections.Generic;

public class OnlineEffectManager : MonoBehaviour
{
    public static OnlineEffectManager Instance;

    [Header("--- Prefabs & Thời gian sống ---")]
    public EffectData attackEff;
    public EffectData defuseEff;
    public EffectData drawBottomEff;
    public EffectData explodeMaster;
    public EffectData seeFutureEff;
    public EffectData shuffleEff;
    public EffectData skipEff;

    [Header("--- Tham chiếu Vị trí ---")]
    public Transform drawPileTransform;
    public GameSceneManager gameSceneManager;

    [System.Serializable]
    public struct EffectData
    {
        public GameObject prefab;
        public float lifeTime;
    }

    void Awake() => Instance = this;

    private void SpawnEffect(EffectData data, Vector3 position)
    {
        // Tạo hiệu ứng
        GameObject eff = Instantiate(data.prefab, transform);

        // SỬA TẠI ĐÂY: Sử dụng .position thay vì .localPosition để khớp với tọa độ các Spot
        eff.transform.position = position;

        // Đảm bảo Z = 0 để không bị Camera che mất (do hiệu ứng thường là 2D/UI)
        Vector3 currentPos = eff.transform.localPosition;
        eff.transform.localPosition = new Vector3(currentPos.x, currentPos.y, 0);

        // Cố định Scale
        eff.transform.localScale = Vector3.one;

        Destroy(eff, data.lifeTime);
    }

    public void PlayEffect(DrawPileManager.CardType type, string senderName)
    {
        EffectData data = GetEffectDataByType(type);
        if (data.prefab == null) return;

        Vector3 spawnPos = Vector3.zero;

        if (type == DrawPileManager.CardType.Attack)
        {
            int victimViewIdx = GetNextAlivePlayerViewIndex(senderName);
            // Lấy tọa độ World của Spot nạn nhân
            spawnPos = GetPositionByViewIndex(victimViewIdx);
        }
        else if (type == DrawPileManager.CardType.Shuffle)
        {
            // Lấy tọa độ World của chồng bài
            spawnPos = drawPileTransform != null ? drawPileTransform.position : Vector3.zero;
        }
        else if (type == DrawPileManager.CardType.Explode)
        {
            // Hiệu ứng nổ cũng nên xuất hiện tại vị trí người bị nổ
            int senderViewIdx = GetViewIndexByPlayerName(senderName);
            spawnPos = GetPositionByViewIndex(senderViewIdx);
        }

        SpawnEffect(data, spawnPos);
    }

    // Hàm bổ trợ để tìm ViewIndex của một người chơi bất kỳ qua tên
    private int GetViewIndexByPlayerName(string playerName)
    {
        var players = RoomManager.Instance.currentRoomPlayers;
        int absIdx = players.IndexOf(playerName);
        int myAbsIdx = players.IndexOf(RoomManager.Instance.currentUsername);
        int n = players.Count;
        return (absIdx - myAbsIdx + n) % n;
    }

    private EffectData GetEffectDataByType(DrawPileManager.CardType type)
    {
        switch (type)
        {
            case DrawPileManager.CardType.Attack: return attackEff;
            case DrawPileManager.CardType.Shuffle: return shuffleEff;
            case DrawPileManager.CardType.SeeFuture: return seeFutureEff;
            case DrawPileManager.CardType.Skip: return skipEff;
            case DrawPileManager.CardType.DrawBottom: return drawBottomEff;
            case DrawPileManager.CardType.Defuse: return defuseEff;
            case DrawPileManager.CardType.Explode: return explodeMaster;
            default: return new EffectData();
        }
    }

    private int GetNextAlivePlayerViewIndex(string senderName)
    {
        var players = RoomManager.Instance.currentRoomPlayers;
        var lifeStatus = OnlineGameLogic.Instance.playerLifeStatus;
        int senderIdx = players.IndexOf(senderName);
        int n = players.Count;

        for (int i = 1; i < n; i++)
        {
            int nextIdx = (senderIdx + i) % n;
            string nextPlayerName = players[nextIdx]; // Lấy tên người chơi (string)

            // SỬA LỖI 1: Truy cập bằng tên người chơi thay vì index
            if (lifeStatus.ContainsKey(nextPlayerName) && lifeStatus[nextPlayerName])
            {
                int myAbsIdx = players.IndexOf(RoomManager.Instance.currentUsername);
                return (nextIdx - myAbsIdx + n) % n;
            }
        }
        return 0;
    }

    private Vector3 GetPositionByViewIndex(int viewIndex)
    {
        if (gameSceneManager != null && gameSceneManager.layoutManager != null)
        {
            // Vì absoluteSpots[viewIndex].position đã là Vector2 (tọa độ)
            // nên ta chỉ việc ép kiểu nó sang Vector3 để trả về.
            Vector2 spotPos = gameSceneManager.layoutManager.absoluteSpots[viewIndex].position;
            return new Vector3(spotPos.x, spotPos.y, 0);
        }
        return Vector3.zero;
    }
}