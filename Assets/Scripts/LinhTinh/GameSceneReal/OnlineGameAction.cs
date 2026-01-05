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
    public Image discardPileDisplay; // Đây là Image hiển thị lá bài vừa đánh trên bàn
    public List<CardVisualData> cardVisuals;
    public TextMeshProUGUI bombTimerText;

    [Header("--- THỐNG KÊ BỘ BÀI ---")]
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

        // Khởi tạo trạng thái ban đầu cho Discard Pile (Mộ bài trên bàn)
        if (discardPileDisplay != null)
        {
            discardPileDisplay.gameObject.SetActive(false);
        }

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
                        bombCards++;
                }
            }

            float chance = totalCards > 0 ? ((float)bombCards / totalCards) * 100f : 0f;
            int roundedChance = Mathf.CeilToInt(chance);

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (deckCountText != null)
                    deckCountText.text = $"Bài còn lại: <color=#FFD700>{totalCards}</color>";

                if (bombChanceText != null)
                {
                    bombChanceText.text = $"Tỉ lệ bom: <color=#FFD700>{roundedChance}%</color>";
                    bombChanceText.color = roundedChance > 40 ? Color.yellow : Color.white;
                }
            });
        };
    }

    // ================================================================
    // GỬI LỆNH TỪ NGƯỜI CHƠI
    // ================================================================

    public void RequestPlayCard(DrawPileManager.CardType cardType, string cardObjectID)
    {
        if (OnlineGameLogic.Instance == null || !OnlineGameLogic.Instance.IsMyTurn() || isLocalProcessing) return;
        if (isWaitingForDefuse && cardType != DrawPileManager.CardType.Defuse) return;

        isLocalProcessing = true;

        if (cardType == DrawPileManager.CardType.Defuse && bombCountdownCoroutine != null)
        {
            StopCoroutine(bombCountdownCoroutine);
            if (bombTimerText != null) bombTimerText.gameObject.SetActive(false);
        }

        Dictionary<string, object> action = new Dictionary<string, object>
        {
            ["type"] = "PLAY_ACTION",
            ["sender"] = RoomManager.Instance.currentUsername,
            ["cardType"] = cardType.ToString(),
            ["cardID"] = cardObjectID
        };

        roomRef.Child("actions").Push().SetValueAsync(action).ContinueWithOnMainThread(t => isLocalProcessing = false);
    }

    public void RequestDrawCard()
    {
        if (OnlineGameLogic.Instance == null || !OnlineGameLogic.Instance.IsMyTurn() || isLocalProcessing || isWaitingForDefuse) return;

        isLocalProcessing = true;
        Dictionary<string, object> action = new Dictionary<string, object>
        {
            ["type"] = "DRAW_REQUEST",
            ["sender"] = RoomManager.Instance.currentUsername
        };

        roomRef.Child("actions").Push().SetValueAsync(action).ContinueWithOnMainThread(t => isLocalProcessing = false);
    }

    public void RequestExplode()
    {
        if (!isWaitingForDefuse) return;
        Dictionary<string, object> action = new Dictionary<string, object>
        {
            ["type"] = "PLAYER_EXPLODED",
            ["sender"] = RoomManager.Instance.currentUsername
        };
        roomRef.Child("actions").Push().SetValueAsync(action);
    }

    // ================================================================
    // LẮNG NGHE VÀ XỬ LÝ
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
        if (OnlineGameLogic.Instance != null && OnlineGameLogic.Instance.isGameOver) return;

        switch (type)
        {
            case "PLAY_ACTION":
                if (Enum.TryParse(data["cardType"].ToString(), out DrawPileManager.CardType cardType))
                {
                    // CẬP NHẬT HÌNH ẢNH LÊN BÀN CHO TẤT CẢ MỌI NGƯỜI
                    UpdateDiscardPileVisual(cardType);

                    // Logic xử lý của Host
                    if (OnlineDrawManager.Instance.isHost) ExecuteCardLogic(cardType, sender);
                }
                break;

            case "DRAW_REQUEST":
                if (OnlineDrawManager.Instance.isHost) Host_ProcessDraw(sender);
                break;

            case "BOMB_TRAPPED":
                if (data["target"].ToString() == RoomManager.Instance.currentUsername)
                {
                    if (bombCountdownCoroutine != null) StopCoroutine(bombCountdownCoroutine);
                    bombCountdownCoroutine = StartCoroutine(BombCountdownTimer(6f));
                }
                break;

            case "PLAYER_EXPLODED":
                if (OnlineDrawManager.Instance.isHost) Host_HandlePlayerExploded(sender);
                break;

            case "FUTURE_DATA":
                if (data["receiver"].ToString() == RoomManager.Instance.currentUsername)
                    ShowSeeFutureUI(data["data"].ToString().Split(','));
                break;
        }

        if (OnlineDrawManager.Instance.isHost) snapshot.Reference.RemoveValueAsync();
    }

    private System.Collections.IEnumerator BombCountdownTimer(float duration)
    {
        float remaining = duration;
        if (bombTimerText != null) bombTimerText.gameObject.SetActive(true);

        while (remaining > 0 && isWaitingForDefuse)
        {
            bombTimerText.text = $"DÍNH BOM! GỠ TRONG: <color=yellow>{Mathf.CeilToInt(remaining)}s</color>";
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        if (isWaitingForDefuse) RequestExplode();
        if (bombTimerText != null) bombTimerText.gameObject.SetActive(false);
    }

    private void ExecuteCardLogic(DrawPileManager.CardType type, string sender)
    {
        switch (type)
        {
            case DrawPileManager.CardType.Skip: HandleEndTurnLogic(); break;
            case DrawPileManager.CardType.Attack: HandleEndTurnLogic(true); break;
            case DrawPileManager.CardType.Shuffle:
                DrawPileManager.Instance.ShuffleDrawPile();
                SyncDeckAfterAction();
                break;
            case DrawPileManager.CardType.SeeFuture: HandleSeeFuture(sender); break;
            case DrawPileManager.CardType.DrawBottom: Host_ProcessDrawBottom(sender); break;
            case DrawPileManager.CardType.Defuse: Host_HandleDefuse(sender); break;
        }
    }

    // ================================================================
    // HOST LOGIC 
    // ================================================================

    private void Host_ProcessDraw(string player)
    {
        DrawPileManager.CardType drawn = DrawPileManager.Instance.DrawCardData();
        SyncDeckAfterAction();
        ProcessDrawnCardResult(player, drawn);
    }

    private void Host_ProcessDrawBottom(string player)
    {
        DrawPileManager.CardType drawn = DrawPileManager.Instance.DrawBottomCardData();
        SyncDeckAfterAction();
        ProcessDrawnCardResult(player, drawn);
    }

    private void ProcessDrawnCardResult(string player, DrawPileManager.CardType drawn)
    {
        if (drawn == DrawPileManager.CardType.Explode)
        {
            Debug.Log($"[Action] {player} rút trúng BOM!");
            roomRef.Child("gameData/isWaitingForDefuse").SetValueAsync(true);

            // Gửi lệnh thông báo cho client của người đó hiện Timer
            roomRef.Child("actions").Push().SetValueAsync(new Dictionary<string, object>
            {
                ["type"] = "BOMB_TRAPPED",
                ["target"] = player,
                ["sender"] = "SYSTEM"
            });
        }
        else
        {
            // Rút bài bình thường: Gửi xác nhận về cho người chơi đó nhận bài
            roomRef.Child("actions").Push().SetValueAsync(new Dictionary<string, object>
            {
                ["type"] = "DRAW_CONFIRMED",
                ["target"] = player,
                ["cardType"] = drawn.ToString()
            });

            // Quan trọng: Chỉ chuyển lượt nếu người đó đã rút hết số lượt cần rút (turnsToDraw)
            HandleEndTurnLogic();
        }
    }

    private void Host_HandleDefuse(string sender)
    {
        if (isWaitingForDefuse)
        {
            Debug.Log($"[Action] {sender} đã gỡ bom thành công!");

            // 1. Trộn lại bom vào bộ bài
            OnlineDrawManager.Instance.Host_InsertBombToFirebaseDeck();

            // 2. Tắt trạng thái chờ
            roomRef.Child("gameData/isWaitingForDefuse").SetValueAsync(false);

            // 3. Kết thúc lượt của người vừa gỡ (vì gỡ bom xong coi như xong lượt)
            // Dùng Invoke để đảm bảo Firebase Deck đã kịp cập nhật xong trước khi chuyển lượt
            Invoke("CallEndTurnFromDefuse", 0.5f);
        }
    }

    private void Host_HandlePlayerExploded(string player)
    {
        Debug.Log($"[Action] Host xác nhận {player} đã nổ tung!");

        // Ghi vào playersStatus để OnlineGameLogic nhận được Event ChildChanged
        roomRef.Child("playersStatus").Child(player).Child("isDead").SetValueAsync(true)
            .ContinueWithOnMainThread(t => {
                // Sau khi xác nhận chết, tắt trạng thái chờ Defuse và chuyển lượt
                roomRef.Child("gameData/isWaitingForDefuse").SetValueAsync(false);
                HandleEndTurnLogic();
            });
    }

    public void HandleEndTurnLogic(bool isAttackAction = false)
    {
        if (!OnlineDrawManager.Instance.isHost) return;

        List<string> players = RoomManager.Instance.currentRoomPlayers;
        int nextTurn = OnlineDrawManager.Instance.currentTurnIndex;

        // Nếu đang trong trạng thái cộng dồn lượt (Attack) và không phải là đánh lá Attack tiếp
        if (turnsToDraw > 1 && !isAttackAction)
        {
            turnsToDraw--;
            Debug.Log($"[Action] Người chơi vẫn còn {turnsToDraw} lượt rút nữa.");
        }
        else
        {
            // TÌM NGƯỜI CHƠI TIẾP THEO CÒN SỐNG
            int attempts = 0;
            do
            {
                nextTurn = (nextTurn + 1) % players.Count;
                attempts++;
                string pName = players[nextTurn];

                // Kiểm tra trạng thái sống từ Dictionary của OnlineGameLogic
                if (OnlineGameLogic.Instance.playerLifeStatus.ContainsKey(pName) &&
                    OnlineGameLogic.Instance.playerLifeStatus[pName])
                {
                    break; // Tìm thấy người còn sống
                }
            } while (attempts < players.Count);

            // Nếu là lá bài Attack, lượt tiếp theo phải rút 2 lần
            turnsToDraw = isAttackAction ? 2 : 1;
        }

        // Cập nhật lên Firebase
        roomRef.Child("gameData/currentTurnIndex").SetValueAsync(nextTurn);
        roomRef.Child("gameData/turnsToDraw").SetValueAsync(turnsToDraw);
    }

    // ================================================================
    // SYNC & VISUALS
    // ================================================================

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

    private void ListenForTurnData()
    {
        roomRef.Child("gameData/turnsToDraw").ValueChanged += (s, e) => {
            if (e.Snapshot.Exists) turnsToDraw = Convert.ToInt32(e.Snapshot.Value);
        };
    }

    private void SyncDeckAfterAction() => OnlineDrawManager.Instance?.UpdateDeckToFirebaseFromManager();

    private void HandleSeeFuture(string receiver)
    {
        var top3 = DrawPileManager.Instance.GetTopCards(3);
        roomRef.Child("actions").Push().SetValueAsync(new Dictionary<string, object>
        {
            ["type"] = "FUTURE_DATA",
            ["receiver"] = receiver,
            ["data"] = string.Join(",", top3)
        });
    }

    // ĐÂY LÀ HÀM QUAN TRỌNG ĐÃ ĐƯỢC CẬP NHẬT TỪ SCRIPT COMMENT
    private void UpdateDiscardPileVisual(DrawPileManager.CardType type)
    {
        if (discardPileDisplay == null) return;

        Sprite s = GetSpriteByType(type);
        if (s != null)
        {
            // Bật Object lên
            discardPileDisplay.gameObject.SetActive(true);
            discardPileDisplay.enabled = true;

            // Gán hình ảnh lá bài
            discardPileDisplay.sprite = s;

            // Đảm bảo Alpha là 1 (không bị trong suốt)
            discardPileDisplay.color = Color.white;

            // Đưa lá bài lên trên cùng để không bị che khuất bởi UI khác
            discardPileDisplay.transform.SetAsLastSibling();

            // Cập nhật logic vào Manager cục bộ (nếu cần để đồng bộ âm thanh/hiệu ứng)
            if (DrawPileManager.Instance != null)
                DrawPileManager.Instance.AddToDiscardPile(type);
        }
    }

    private void ShowSeeFutureUI(string[] cardNames)
    {
        if (seeFuturePanel == null) return;
        seeFuturePanel.SetActive(true);
        for (int i = 0; i < futureCardSlots.Length; i++)
        {
            if (i < cardNames.Length && Enum.TryParse(cardNames[i], out DrawPileManager.CardType t))
            {
                futureCardSlots[i].sprite = GetSpriteByType(t);
                futureCardSlots[i].gameObject.SetActive(true);
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
        foreach (var v in cardVisuals) if (v.type == type) return v.cardSprite;
        return null;
    }
    private void CallEndTurnFromDefuse()
    {
        HandleEndTurnLogic(false);
    }
}
