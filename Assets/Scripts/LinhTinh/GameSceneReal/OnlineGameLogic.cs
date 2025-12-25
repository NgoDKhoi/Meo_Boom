using UnityEngine;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class OnlineGameLogic : MonoBehaviour
{
    public static OnlineGameLogic Instance;

    private DatabaseReference roomRef;
    private DatabaseReference usersRef;
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

    [Header("--- Victory UI (Mới) ---")]
    public GameObject victoryPanel;          // Kéo thả Panel chúc mừng vào đây
    public TextMeshProUGUI winnerNameText;    // Text hiển thị tên người thắng trên Panel
    public Button confirmVictoryButton;      // Nút để thoát về LoadRoomScene
    public GameObject Panel_GamePlay;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (playCardButton != null) playCardButton.interactable = false;
        if (victoryPanel != null) victoryPanel.SetActive(false);

        // Gán sự kiện cho nút xác nhận nếu có
        if (confirmVictoryButton != null)
            confirmVictoryButton.onClick.AddListener(ReturnToLoadRoom);

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
        usersRef = FirebaseManager.Instance.Database.RootReference.Child("users");

        var players = RoomManager.Instance.currentRoomPlayers;
        if (players != null && players.Count > 0)
        {
            isHost = (RoomManager.Instance.currentUsername == players[0]);
            foreach (var p in players) playerLifeStatus[p] = true;
        }

        ListenToGameState();
        ListenToPlayersLife();
        ListenToWinner();
    }

    private void ListenToGameState()
    {
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
                playerLifeStatus[pName] = !isDead;
                UpdateTurnUI();

                if (isDead && pName == RoomManager.Instance.currentUsername)
                {
                    Debug.Log("<color=red>BẠN ĐÃ BỊ LOẠI KHỎI CUỘC CHƠI!</color>");
                }

                if (isHost && !isGameOver)
                {
                    CheckWinCondition();
                }
            });
        }
    }

    private void CheckWinCondition()
    {
        var survivors = playerLifeStatus.Where(p => p.Value == true).Select(p => p.Key).ToList();

        if (survivors.Count == 1 && playerLifeStatus.Count >= 2)
        {
            string winnerName = survivors[0];
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
                    if (isGameOver) return;

                    isGameOver = true;
                    ShowVictoryUI(winner);

                    if (playCardButton != null) playCardButton.interactable = false;

                    if (isHost)
                    {
                        AddPointToWinnerUID(winner);
                    }
                });
            }
        };
    }

    private void ShowVictoryUI(string winnerName)
    {
        if (turnInfoText != null)
        {
            turnInfoText.text = (winnerName == RoomManager.Instance.currentUsername) ?
                "<color=yellow>★ BẠN LÀ NGƯỜI CHIẾN THẮNG! ★</color>" :
                $"<color=green>{winnerName} đã chiến thắng!</color>";
        }

        // Hiện Panel Victory
        if (victoryPanel != null)
        {
            Panel_GamePlay.SetActive(false);
            victoryPanel.SetActive(true);
            if (winnerNameText != null)
            {
                winnerNameText.text = (winnerName == RoomManager.Instance.currentUsername) ?
                    "<color=yellow>BẠN ĐÃ CHIẾN THẮNG! </color>" :
                    $"<color=yellow>{winnerName}   ĐÃ CHIẾN THẮNG!</color>";
            }
        }
    }

    public void ReturnToLoadRoom()
    {
        // Xóa ID phòng hiện tại để tránh lỗi logic khi vào lại
        if (RoomManager.Instance != null)
        {
            // RoomManager.Instance.currentRoomID = ""; // Tùy chọn nếu muốn reset hoàn toàn
        }
        SceneManager.LoadScene("LoadRoomScene");
    }

    private void AddPointToWinnerUID(string winnerUsername)
    {
        usersRef.GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsFaulted || task.IsCanceled) return;

            DataSnapshot snapshot = task.Result;
            string targetUID = "";

            foreach (var userSnap in snapshot.Children)
            {
                if (userSnap.Child("username").Value != null &&
                    userSnap.Child("username").Value.ToString() == winnerUsername)
                {
                    targetUID = userSnap.Key;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(targetUID))
            {
                DatabaseReference scoreRef = usersRef.Child(targetUID).Child("score");
                scoreRef.RunTransaction(mutableData => {
                    long currentScore = 0;
                    if (mutableData.Value != null)
                    {
                        currentScore = Convert.ToInt64(mutableData.Value);
                    }
                    mutableData.Value = currentScore + 1;
                    return TransactionResult.Success(mutableData);
                }).ContinueWithOnMainThread(t => {
                    if (t.IsCompleted)
                        Debug.Log($"<color=cyan>Đã cộng 1 điểm cho {winnerUsername} (UID: {targetUID})</color>");
                });
            }
        });
    }

    public void UpdateTurnUI()
    {
        if (isGameOver) return;
        if (RoomManager.Instance == null || RoomManager.Instance.currentRoomPlayers == null) return;
        if (currentTurnIndex < 0 || currentTurnIndex >= RoomManager.Instance.currentRoomPlayers.Count) return;

        string activePlayer = RoomManager.Instance.currentRoomPlayers[currentTurnIndex];
        string myName = RoomManager.Instance.currentUsername;
        bool isMe = (activePlayer == myName);

        bool isActivePlayerAlive = playerLifeStatus.ContainsKey(activePlayer) ? playerLifeStatus[activePlayer] : true;

        if (turnInfoText != null)
        {
            if (!isActivePlayerAlive)
                turnInfoText.text = $"<color=red>{activePlayer} đã bay màu.</color>";
            else if (isWaitingForDefuse)
                turnInfoText.text = isMe ? "<color=red>⚠ BẠN DÍNH BOM! ⚠</color>" : $"<color=orange>{activePlayer} đang gỡ bom...</color>";
            else
                turnInfoText.text = isMe ? "<color=yellow>LƯỢT CỦA BẠN</color>" : $"Lượt của: {activePlayer}";
        }

        if (playCardButton != null)
        {
            bool amIAlive = playerLifeStatus.ContainsKey(myName) ? playerLifeStatus[myName] : true;
            if (!isMe || !amIAlive)
            {
                playCardButton.interactable = false;
            }
            else
            {
                var selected = OnlineCardController.SelectedCard;
                if (selected == null) playCardButton.interactable = false;
                else
                {
                    if (isWaitingForDefuse)
                        playCardButton.interactable = (selected.cardType == DrawPileManager.CardType.Defuse);
                    else
                        playCardButton.interactable = true;
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