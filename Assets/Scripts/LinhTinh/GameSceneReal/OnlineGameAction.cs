using UnityEngine;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System;
using UnityEngine.UI;
using TMPro;

public class OnlineGameActionManager : MonoBehaviour
{
    public static OnlineGameActionManager Instance;
    private DatabaseReference roomRef;

    [Header("--- UI REFERENCES ---")]
    public GameObject seeFuturePanel;
    public Image[] futureCardSlots;
    public Image discardPileDisplay;
    public List<CardVisualData> cardVisuals;
    public TextMeshProUGUI bombTimerText;

    [Header("--- THỐNG KÊ BỘ BÀI (NEW) ---")]
    public TextMeshProUGUI deckCountText;
    public TextMeshProUGUI bombChanceText;

    [Header("--- TRẠNG THÁI VÒNG CHƠI ---")]
    public int turnsToDraw = 1;
    public bool isWaitingForDefuse = false;
    private string roomID;
    private bool isLocalProcessing = false;

    private Coroutine bombCountdownCoroutine;

    [Serializable]
    public struct CardVisualData
    {
        public DrawPileManager.CardType type;
        public Sprite cardSprite;
    }

    void Awake() => Instance = this;

    void Start()
    {
        if (seeFuturePanel != null) seeFuturePanel.SetActive(false);
        if (discardPileDisplay != null) discardPileDisplay.gameObject.SetActive(false);
        if (bombTimerText != null) bombTimerText.gameObject.SetActive(false);

        if (deckCountText != null) deckCountText.text = "Bài còn lại: --";
        if (bombChanceText != null) bombChanceText.text = "Tỉ lệ bom: --%";

        StartCoroutine(InitializeFirebase());
    }

    private System.Collections.IEnumerator InitializeFirebase()
    {
        while (RoomManager.Instance == null || string.IsNullOrEmpty(RoomManager.Instance.currentRoomID))
            yield return new WaitForSeconds(0.5f);

        roomID = RoomManager.Instance.currentRoomID;
        roomRef = FirebaseManager.Instance.Database.RootReference.Child("rooms").Child(roomID);

        ListenForGameActions();
        ListenForTurnData();
        ListenForDefuseStatus();
        ListenForDeckStats();
    }

    // ================================================================
    // LOGIC TÍNH TOÁN TỈ LỆ BOM VÀ SỐ LƯỢNG BÀI (CHỈ BIẾT DỮ LIỆU CHUNG)
    // ================================================================
    private void ListenForDeckStats()
    {
        roomRef.Child("gameData").Child("drawPile").ValueChanged += (s, e) => {
            int totalCards = 0;
            int bombCards = 0;

            if (e.Snapshot.Exists)
            {
                foreach (var cardSnap in e.Snapshot.Children)
                {
                    totalCards++;
                    if (cardSnap.Value.ToString() == DrawPileManager.CardType.Explode.ToString())
                    {
                        bombCards++;
                    }
                }
            }

            float chance = totalCards > 0 ? ((float)bombCards / totalCards) * 100f : 0f;
            int roundedChance = Mathf.CeilToInt(chance);

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (deckCountText != null)
                    deckCountText.text = $"Bài còn lại: <color=#FFD700>{totalCards}</color>";

                if (bombChanceText != null)
                {
                    bombChanceText.text = $"Tỉ lệ bom: <color=#FF4500>{roundedChance}%</color>";
                    bombChanceText.color = roundedChance > 40 ? Color.red : Color.white;
                }
            });
        };
    }

    // ================================================================
    // GỬI LỆNH TỪ NGƯỜI CHƠI (Hành động mù - không quan tâm bài người khác)
    // ================================================================

    private bool CheckIfItIsMyTurn()
    {
        if (OnlineDrawManager.Instance == null || RoomManager.Instance == null) return false;
        List<string> players = RoomManager.Instance.currentRoomPlayers;
        string myName = RoomManager.Instance.currentUsername;
        int currentTurnIndex = OnlineDrawManager.Instance.currentTurnIndex;
        if (players == null || currentTurnIndex < 0 || currentTurnIndex >= players.Count) return false;
        return players[currentTurnIndex] == myName;
    }

    public void RequestPlayCard(DrawPileManager.CardType cardType, string cardObjectID)
    {
        if (!CheckIfItIsMyTurn() || isLocalProcessing) return;

        if (isWaitingForDefuse && cardType != DrawPileManager.CardType.Defuse)
        {
            Debug.Log("<color=orange>BẠN ĐANG DÍNH BOM! CHỈ CÓ THỂ ĐÁNH DEFUSE!</color>");
            return;
        }

        if (cardType == DrawPileManager.CardType.Defuse && bombCountdownCoroutine != null)
        {
            StopCoroutine(bombCountdownCoroutine);
            if (bombTimerText != null) bombTimerText.gameObject.SetActive(false);
        }

        Dictionary<string, object> action = new Dictionary<string, object>();
        action["type"] = "PLAY_ACTION";
        action["sender"] = RoomManager.Instance.currentUsername;
        action["cardType"] = cardType.ToString();
        action["cardID"] = cardObjectID;

        roomRef.Child("actions").Push().SetValueAsync(action);
    }

    public void RequestDrawCard()
    {
        if (!CheckIfItIsMyTurn() || isLocalProcessing) return;
        if (isWaitingForDefuse) return;

        isLocalProcessing = true;
        Dictionary<string, object> action = new Dictionary<string, object>();
        action["type"] = "DRAW_REQUEST";
        action["sender"] = RoomManager.Instance.currentUsername;

        roomRef.Child("actions").Push().SetValueAsync(action).ContinueWithOnMainThread(t => {
            isLocalProcessing = false;
        });
    }

    public void RequestExplode()
    {
        if (!isWaitingForDefuse) return;
        if (bombTimerText != null) bombTimerText.gameObject.SetActive(false);

        Dictionary<string, object> action = new Dictionary<string, object>();
        action["type"] = "PLAYER_EXPLODED";
        action["sender"] = RoomManager.Instance.currentUsername;
        roomRef.Child("actions").Push().SetValueAsync(action);
    }

    // ================================================================
    // LẮNG NGHE VÀ XỬ LÝ (Sử dụng Action để cập nhật game thay vì State)
    // ================================================================

    private void ListenForGameActions()
    {
        roomRef.Child("actions").ChildAdded += (s, e) => {
            if (!e.Snapshot.Exists) return;
            var data = e.Snapshot.Value as Dictionary<string, object>;
            if (data == null || !data.ContainsKey("type")) return;

            string type = data["type"].ToString();
            string sender = data.ContainsKey("sender") ? data["sender"].ToString() : "";

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                HandleIncomingAction(type, sender, data, e.Snapshot);
            });
        };
    }

    private void HandleIncomingAction(string type, string sender, Dictionary<string, object> data, DataSnapshot snapshot)
    {
        switch (type)
        {
            case "PLAY_ACTION":
                if (Enum.TryParse(data["cardType"].ToString(), out DrawPileManager.CardType cardType))
                {
                    UpdateDiscardPileVisual(cardType);
                    ShowCardPlayedInDiscardPile(cardType);
                    // Chỉ Host mới thực thi logic bài, Client chỉ xem Visual
                    if (OnlineDrawManager.Instance.isHost)
                        ExecuteCardLogic(cardType, sender);
                }
                break;

            case "DRAW_REQUEST":
                if (OnlineDrawManager.Instance.isHost)
                    Host_ProcessDraw(sender);
                break;

            case "BOMB_TRAPPED":
                if (data["target"].ToString() == RoomManager.Instance.currentUsername)
                {
                    if (bombCountdownCoroutine != null) StopCoroutine(bombCountdownCoroutine);
                    bombCountdownCoroutine = StartCoroutine(BombCountdownTimer(5f));
                }
                break;

            case "PLAYER_EXPLODED":
                if (OnlineDrawManager.Instance.isHost) Host_HandlePlayerExploded(sender);
                break;

            case "FUTURE_DATA":
                if (data["receiver"].ToString() == RoomManager.Instance.currentUsername)
                {
                    string[] cards = data["data"].ToString().Split(',');
                    ShowSeeFutureUI(cards);
                }
                break;
        }

        // Host dọn dẹp Action cũ để tránh tràn dữ liệu
        if (OnlineDrawManager.Instance.isHost) snapshot.Reference.RemoveValueAsync();
    }

    private System.Collections.IEnumerator BombCountdownTimer(float duration)
    {
        float remaining = duration;
        if (bombTimerText != null) bombTimerText.gameObject.SetActive(true);

        while (remaining > 0)
        {
            if (bombTimerText != null) bombTimerText.text = $"BẠN DÍNH BOM! GỠ TRONG: <color=yellow>{Mathf.CeilToInt(remaining)}s</color>";
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
            if (!isWaitingForDefuse) break;
        }

        if (isWaitingForDefuse)
        {
            RequestExplode();
        }
        if (bombTimerText != null) bombTimerText.gameObject.SetActive(false);
    }

    private void ExecuteCardLogic(DrawPileManager.CardType type, string sender)
    {
        if (isWaitingForDefuse && type != DrawPileManager.CardType.Defuse) return;

        switch (type)
        {
            case DrawPileManager.CardType.Skip: HandleEndTurnLogic(); break;
            case DrawPileManager.CardType.Attack: HandleEndTurnLogic(true); break;
            case DrawPileManager.CardType.Shuffle:
                DrawPileManager.Instance.ShuffleDrawPile();
                SyncDeckAfterAction();
                break;
            case DrawPileManager.CardType.SeeFuture: HandleSeeFuture(sender); break;
            case DrawPileManager.CardType.DrawBottom: HandleDrawBottom(sender); break;
            case DrawPileManager.CardType.Defuse: Host_HandleDefuse(sender); break;
        }
    }

    // ================================================================
    // LOGIC DÀNH RIÊNG CHO HOST
    // ================================================================

    private void Host_ProcessDraw(string player)
    {
        DrawPileManager.CardType drawn = DrawPileManager.Instance.DrawCardData();
        SyncDeckAfterAction();

        if (drawn == DrawPileManager.CardType.Explode)
        {
            Dictionary<string, object> res = new Dictionary<string, object>();
            res["type"] = "BOMB_TRAPPED";
            res["target"] = player;
            roomRef.Child("actions").Push().SetValueAsync(res);
            roomRef.Child("gameData/isWaitingForDefuse").SetValueAsync(true);
        }
        else
        {
            Dictionary<string, object> res = new Dictionary<string, object>();
            res["type"] = "DRAW_CONFIRMED";
            res["cardType"] = drawn.ToString();
            res["target"] = player;
            roomRef.Child("actions").Push().SetValueAsync(res).ContinueWithOnMainThread(t => HandleEndTurnLogic());
        }
    }

    private void Host_HandleDefuse(string sender)
    {
        if (isWaitingForDefuse)
        {
            OnlineDrawManager.Instance.Host_InsertBombToFirebaseDeck();
            Invoke("Host_FinalizeDefuse", 1.0f);
        }
    }

    private void Host_FinalizeDefuse()
    {
        roomRef.Child("gameData/isWaitingForDefuse").SetValueAsync(false);
        HandleEndTurnLogic();
    }

    private void Host_HandlePlayerExploded(string player)
    {
        roomRef.Child("players").Child(player).Child("isDead").SetValueAsync(true);
        roomRef.Child("gameData/isWaitingForDefuse").SetValueAsync(false);
        HandleEndTurnLogic();
    }

    private void HandleEndTurnLogic(bool isAttackAction = false)
    {
        if (!OnlineDrawManager.Instance.isHost) return;
        int playersCount = RoomManager.Instance.currentRoomPlayers.Count;
        int nextTurn;

        if (turnsToDraw > 1 && !isAttackAction)
        {
            turnsToDraw--;
            nextTurn = OnlineDrawManager.Instance.currentTurnIndex;
        }
        else
        {
            nextTurn = (OnlineDrawManager.Instance.currentTurnIndex + 1) % playersCount;
            turnsToDraw = isAttackAction ? 2 : 1;
        }

        roomRef.Child("gameData/currentTurnIndex").SetValueAsync(nextTurn);
        roomRef.Child("gameData/turnsToDraw").SetValueAsync(turnsToDraw);
    }

    private void ListenForDefuseStatus()
    {
        roomRef.Child("gameData/isWaitingForDefuse").ValueChanged += (s, e) => {
            if (e.Snapshot.Exists)
            {
                isWaitingForDefuse = (bool)e.Snapshot.Value;
                if (!isWaitingForDefuse && bombTimerText != null)
                    bombTimerText.gameObject.SetActive(false);
            }
        };
    }

    private void SyncDeckAfterAction() => OnlineDrawManager.Instance?.UpdateDeckToFirebaseFromManager();

    private void HandleSeeFuture(string receiver)
    {
        List<DrawPileManager.CardType> top3 = DrawPileManager.Instance.GetTopCards(3);
        string top3Str = string.Join(",", top3);

        Dictionary<string, object> msg = new Dictionary<string, object>();
        msg["type"] = "FUTURE_DATA";
        msg["receiver"] = receiver;
        msg["data"] = top3Str;
        roomRef.Child("actions").Push().SetValueAsync(msg);
    }

    private void HandleDrawBottom(string receiver)
    {
        DrawPileManager.CardType bottomCard = DrawPileManager.Instance.DrawBottomCardData();
        SyncDeckAfterAction();

        if (bottomCard == DrawPileManager.CardType.Explode)
        {
            Dictionary<string, object> res = new Dictionary<string, object>();
            res["type"] = "BOMB_TRAPPED";
            res["target"] = receiver;
            roomRef.Child("actions").Push().SetValueAsync(res);
            roomRef.Child("gameData/isWaitingForDefuse").SetValueAsync(true);
        }
        else
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["type"] = "DRAW_CONFIRMED";
            result["target"] = receiver;
            result["cardType"] = bottomCard.ToString();

            roomRef.Child("actions").Push().SetValueAsync(result).ContinueWithOnMainThread(t => HandleEndTurnLogic());
        }
    }

    private void ListenForTurnData()
    {
        roomRef.Child("gameData/turnsToDraw").ValueChanged += (s, e) => {
            if (e.Snapshot.Exists) turnsToDraw = Convert.ToInt32(e.Snapshot.Value);
        };
    }

    private void ShowCardPlayedInDiscardPile(DrawPileManager.CardType type)
    {
        if (DrawPileManager.Instance != null)
            DrawPileManager.Instance.AddToDiscardPile(type);
    }

    private void UpdateDiscardPileVisual(DrawPileManager.CardType type)
    {
        if (discardPileDisplay == null) return;
        Sprite s = GetSpriteByType(type);
        if (s != null)
        {
            discardPileDisplay.gameObject.SetActive(true);
            discardPileDisplay.enabled = true;
            discardPileDisplay.sprite = s;
            discardPileDisplay.color = Color.white;
            discardPileDisplay.transform.SetAsLastSibling();
        }
    }

    private void ShowSeeFutureUI(string[] cardNames)
    {
        if (seeFuturePanel == null) return;
        seeFuturePanel.SetActive(true);
        for (int i = 0; i < futureCardSlots.Length; i++)
        {
            if (i < cardNames.Length)
            {
                if (Enum.TryParse(cardNames[i], out DrawPileManager.CardType type))
                {
                    futureCardSlots[i].sprite = GetSpriteByType(type);
                    futureCardSlots[i].gameObject.SetActive(true);
                }
            }
            else futureCardSlots[i].gameObject.SetActive(false);
        }
        CancelInvoke("HideSeeFutureUI");
        Invoke("HideSeeFutureUI", 5f);
    }

    public void HideSeeFutureUI() => seeFuturePanel.SetActive(false);

    private Sprite GetSpriteByType(DrawPileManager.CardType type)
    {
        if (cardVisuals == null) return null;
        foreach (var visual in cardVisuals)
        {
            if (visual.type == type) return visual.cardSprite;
        }
        return null;
    }
}