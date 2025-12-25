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

    // Danh sách lưu trữ trạng thái sống sót của từng người chơi: <Tên người chơi, Còn sống hay không>
    public Dictionary<string, bool> playerLifeStatus = new Dictionary<string, bool>();

    [Header("--- UI References ---")]
    public TextMeshProUGUI turnInfoText;
    public Button playCardButton;

    [Header("--- Victory UI ---")]
    public GameObject victoryPanel;          // Panel thông báo chiến thắng
    public TextMeshProUGUI winnerNameText;    // Text hiển thị tên người thắng
    public Button confirmVictoryButton;      // Nút xác nhận để thoát
    public GameObject Panel_GamePlay;         // Panel chứa bàn chơi chính để ẩn đi khi kết thúc

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Khởi tạo trạng thái UI ban đầu
        if (playCardButton != null) playCardButton.interactable = false;
        if (victoryPanel != null) victoryPanel.SetActive(false);

        if (confirmVictoryButton != null)
            confirmVictoryButton.onClick.AddListener(ReturnToLoadRoom);

        StartCoroutine(WaitForRoomData());
    }

    private System.Collections.IEnumerator WaitForRoomData()
    {
        // Đợi cho đến khi RoomManager đã sẵn sàng dữ liệu phòng
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
            // Xác định xem mình có phải chủ phòng không (thường là người ở vị trí index 0)
            isHost = (RoomManager.Instance.currentUsername == players[0]);

            // Khởi tạo trạng thái sống cho tất cả người chơi
            foreach (var p in players)
            {
                if (!playerLifeStatus.ContainsKey(p))
                    playerLifeStatus[p] = true;
            }
        }

        ListenToGameState();
        ListenToPlayersLife();
        ListenToWinner();
    }

    private void ListenToGameState()
    {
        // Theo dõi chỉ số lượt đi
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

        // Theo dõi trạng thái đang chờ gỡ bom
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
        // Theo dõi khi có thay đổi về tình trạng sống/chết của người chơi trong node "players"
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
                    Debug.Log("<color=red>[Game] Bạn đã bị nổ tung và loại khỏi cuộc chơi!</color>");
                }

                // Chỉ Host mới thực hiện kiểm tra điều kiện thắng để tránh xung đột ghi đè dữ liệu
                if (isHost && !isGameOver)
                {
                    CheckWinCondition();
                }
            });
        }
    }

    private void CheckWinCondition()
    {
        // Đếm số người còn sống
        var survivors = playerLifeStatus.Where(p => p.Value == true).Select(p => p.Key).ToList();

        // Nếu chỉ còn 1 người sống sót (trong trận đấu ít nhất 2 người)
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

                    // Host thực hiện cộng điểm cho người thắng vào Database
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
        string myName = RoomManager.Instance.currentUsername;
        bool amIWinner = (winnerName == myName);

        if (turnInfoText != null)
        {
            turnInfoText.text = amIWinner ?
                "<color=yellow>★ BẠN LÀ NGƯỜI CHIẾN THẮNG! ★</color>" :
                $"<color=green>{winnerName} đã chiến thắng!</color>";
        }

        if (victoryPanel != null)
        {
            if (Panel_GamePlay != null) Panel_GamePlay.SetActive(false);

            victoryPanel.SetActive(true);

            if (winnerNameText != null)
            {
                winnerNameText.text = amIWinner ?
                    "<color=yellow>BẠN ĐÃ CHIẾN THẮNG!</color>" :
                    $"<color=yellow>{winnerName} ĐÃ CHIẾN THẮNG!</color>";
            }
        }
    }

    public void ReturnToLoadRoom()
    {
        // Quay lại màn hình chọn phòng
        SceneManager.LoadScene("LoadRoomScene");
    }

    private void AddPointToWinnerUID(string winnerUsername)
    {
        // Tìm UID dựa trên Username để cộng điểm (Score)
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
                        Debug.Log($"<color=cyan>[Firebase] Đã cộng 1 điểm cho {winnerUsername}</color>");
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

        // Cập nhật text trạng thái lượt
        if (turnInfoText != null)
        {
            if (!isActivePlayerAlive)
                turnInfoText.text = $"<color=red>{activePlayer} đã bị loại.</color>";
            else if (isWaitingForDefuse)
                turnInfoText.text = isMe ? "<color=red>⚠ BẠN DÍNH BOM! GỠ NGAY! ⚠</color>" : $"<color=orange>{activePlayer} đang gỡ bom...</color>";
            else
                turnInfoText.text = isMe ? "<color=yellow>LƯỢT CỦA BẠN</color>" : $"Lượt của: {activePlayer}";
        }

        // Cập nhật trạng thái nút "Đánh bài"
        if (playCardButton != null)
        {
            bool amIAlive = playerLifeStatus.ContainsKey(myName) ? playerLifeStatus[myName] : true;

            // Nếu không phải lượt của mình hoặc mình đã chết thì tắt nút
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
                    // Nếu đang dính bom, chỉ cho phép nhấn nút nếu đang chọn lá Defuse
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