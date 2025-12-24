using UnityEngine;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System;
using System.Linq;
using TMPro;
using UnityEngine.UI;

public class OnlineGameLogic : MonoBehaviour
{
    public static OnlineGameLogic Instance;

    private DatabaseReference roomRef;
    private string roomID;

    [Header("--- Trạng thái Game ---")]
    public int currentTurnIndex = -1;
    public bool isHost = false;
    public bool isWaitingForDefuse = false;
    public bool isGameOver = false;

    // Danh sách lưu trữ trạng thái sống sót của từng người chơi
    public Dictionary<string, bool> playerLifeStatus = new Dictionary<string, bool>();

    [Header("--- UI References ---")]
    public TextMeshProUGUI turnInfoText;
    public Button playCardButton;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (playCardButton != null) playCardButton.interactable = false;
        StartCoroutine(WaitForRoomData());
    }

    private System.Collections.IEnumerator WaitForRoomData()
    {
        while (RoomManager.Instance == null || string.IsNullOrEmpty(RoomManager.Instance.currentRoomID))
        {
            yield return new WaitForSeconds(0.5f);
        }

        roomID = RoomManager.Instance.currentRoomID;
        roomRef = FirebaseManager.Instance.Database.RootReference.Child("rooms").Child(roomID);

        var players = RoomManager.Instance.currentRoomPlayers;
        if (players != null && players.Count > 0)
        {
            isHost = (RoomManager.Instance.currentUsername == players[0]);
            // Khởi tạo trạng thái ban đầu: tất cả đều sống
            foreach (var p in players) playerLifeStatus[p] = true;
        }

        ListenToGameState();
        ListenToPlayersLife();
        ListenToWinner();
    }

    private void ListenToGameState()
    {
        // 1. Theo dõi lượt chơi
        roomRef.Child("gameData/currentTurnIndex").ValueChanged += (s, e) => {
            if (e.Snapshot.Exists && e.Snapshot.Value != null)
            {
                int newIdx = Convert.ToInt32(e.Snapshot.Value);
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    currentTurnIndex = newIdx;
                    UpdateTurnUI();
                });
            }
        };

        // 2. Theo dõi biến isWaitingForDefuse
        roomRef.Child("gameData/isWaitingForDefuse").ValueChanged += (s, e) => {
            bool waiting = false;
            if (e.Snapshot.Exists && e.Snapshot.Value != null)
            {
                waiting = (bool)e.Snapshot.Value;
            }

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                isWaitingForDefuse = waiting;
                UpdateTurnUI();
            });
        };
    }

    private void ListenToPlayersLife()
    {
        // Lắng nghe thay đổi trạng thái isDead của từng người chơi
        roomRef.Child("players").ChildAdded += HandlePlayerLifeChange;
        roomRef.Child("players").ChildChanged += HandlePlayerLifeChange;
    }

    private void HandlePlayerLifeChange(object sender, ChildChangedEventArgs e)
    {
        if (e.Snapshot.Exists)
        {
            string pName = e.Snapshot.Key;
            bool isDead = false;
            if (e.Snapshot.HasChild("isDead"))
            {
                isDead = (bool)e.Snapshot.Child("isDead").Value;
            }

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                playerLifeStatus[pName] = !isDead; // Sống = !isDead
                UpdateTurnUI();

                // Nếu mình vừa bị đánh dấu là Dead
                if (isDead && pName == RoomManager.Instance.currentUsername)
                {
                    Debug.Log("<color=red>BẠN ĐÃ BỊ LOẠI KHỎI CUỘC CHƠI!</color>");
                }

                // Kiểm tra điều kiện thắng (Chỉ Host thực thi để tránh ghi đè dữ liệu)
                if (isHost && !isGameOver)
                {
                    CheckWinCondition();
                }
            });
        }
    }

    private void CheckWinCondition()
    {
        // Lọc danh sách những người còn sống
        var survivors = playerLifeStatus.Where(p => p.Value == true).Select(p => p.Key).ToList();

        // Nếu chỉ còn đúng 1 người sống sót (và ván đấu có từ 2 người trở lên)
        if (survivors.Count == 1 && playerLifeStatus.Count >= 2)
        {
            string winnerName = survivors[0];
            // Cập nhật người thắng lên Firebase
            roomRef.Child("gameData/winner").SetValueAsync(winnerName);
        }
    }

    private void ListenToWinner()
    {
        roomRef.Child("gameData/winner").ValueChanged += (s, e) => {
            if (e.Snapshot.Exists && e.Snapshot.Value != null)
            {
                string winner = e.Snapshot.Value.ToString();
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    isGameOver = true;
                    if (turnInfoText != null)
                    {
                        turnInfoText.text = (winner == RoomManager.Instance.currentUsername) ?
                            "<color=yellow>★ BẠN LÀ NGƯỜI CHIẾN THẮNG! ★</color>" :
                            $"<color=green>{winner} đã chiến thắng!</color>";
                    }
                    if (playCardButton != null) playCardButton.interactable = false;
                });
            }
        };
    }

    public void UpdateTurnUI()
    {
        if (isGameOver) return;
        if (RoomManager.Instance == null || RoomManager.Instance.currentRoomPlayers == null) return;
        if (currentTurnIndex < 0 || currentTurnIndex >= RoomManager.Instance.currentRoomPlayers.Count) return;

        string activePlayer = RoomManager.Instance.currentRoomPlayers[currentTurnIndex];
        string myName = RoomManager.Instance.currentUsername;
        bool isMe = (activePlayer == myName);

        // Kiểm tra xem người chơi hiện tại còn sống không
        bool isActivePlayerAlive = playerLifeStatus.ContainsKey(activePlayer) ? playerLifeStatus[activePlayer] : true;

        if (turnInfoText != null)
        {
            if (!isActivePlayerAlive)
            {
                turnInfoText.text = $"<color=red>{activePlayer} đã bay màu.</color>";
            }
            else if (isWaitingForDefuse)
            {
                turnInfoText.text = isMe ?
                    "<color=red>⚠ BẠN DÍNH BOM! ⚠</color>" :
                    $"<color=orange>{activePlayer} đang gỡ bom...</color>";
            }
            else
            {
                turnInfoText.text = isMe ?
                    "<color=yellow>LƯỢT CỦA BẠN</color>" :
                    $"Lượt của: {activePlayer}";
            }
        }

        // Cập nhật nút Play
        if (playCardButton != null)
        {
            // Chỉ tương tác nếu là lượt mình VÀ mình còn sống
            bool amIAlive = playerLifeStatus.ContainsKey(myName) ? playerLifeStatus[myName] : true;

            if (!isMe || !amIAlive)
            {
                playCardButton.interactable = false;
            }
            else
            {
                var selected = OnlineCardController.SelectedCard;
                if (selected == null)
                {
                    playCardButton.interactable = false;
                }
                else
                {
                    if (isWaitingForDefuse)
                    {
                        playCardButton.interactable = (selected.cardType == DrawPileManager.CardType.Defuse);
                    }
                    else
                    {
                        playCardButton.interactable = true;
                    }
                }
            }
        }
    }

    public bool IsMyTurn()
    {
        if (isGameOver) return false;
        if (RoomManager.Instance == null || RoomManager.Instance.currentRoomPlayers == null) return false;
        if (currentTurnIndex < 0 || currentTurnIndex >= RoomManager.Instance.currentRoomPlayers.Count) return false;

        string activePlayer = RoomManager.Instance.currentRoomPlayers[currentTurnIndex];
        bool amIAlive = playerLifeStatus.ContainsKey(RoomManager.Instance.currentUsername) ? playerLifeStatus[RoomManager.Instance.currentUsername] : true;

        return (RoomManager.Instance.currentUsername == activePlayer) && amIAlive;
    }

    public void OnCardSelectionChanged()
    {
        UpdateTurnUI();
    }
}