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

    // URL Database đồng bộ với LoadRoomManager
    private const string DatabaseUrl = "https://mygametest2-default-rtdb.asia-southeast1.firebasedatabase.app";

    [Header("--- Trạng thái Game ---")]
    public int currentTurnIndex = -1;
    public bool isHost = false;
    public bool isWaitingForDefuse = false;
    public bool isGameOver = false;

    public Dictionary<string, bool> playerLifeStatus = new Dictionary<string, bool>();

    [Header("--- UI References ---")]
    public TextMeshProUGUI turnInfoText;
    public Button playCardButton;

    [Header("--- Victory UI ---")]
    public GameObject victoryPanel;
    public TextMeshProUGUI winnerNameText;
    public Button confirmVictoryButton;
    public GameObject Panel_GamePlay;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (playCardButton != null) playCardButton.interactable = false;
        if (victoryPanel != null) victoryPanel.SetActive(false);

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
        // Sử dụng DatabaseUrl cụ thể để đảm bảo an toàn
        var dbInstance = FirebaseDatabase.GetInstance(DatabaseUrl);
        roomRef = dbInstance.RootReference.Child("rooms").Child(roomID);
        usersRef = dbInstance.RootReference.Child("users");

        var players = RoomManager.Instance.currentRoomPlayers;
        if (players != null && players.Count > 0)
        {
            isHost = (RoomManager.Instance.currentUsername == players[0]);
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
            bool waiting = e.Snapshot.Exists && (bool)e.Snapshot.Value;
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
            bool isDead = e.Snapshot.HasChild("isDead") && (bool)e.Snapshot.Child("isDead").Value;

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                playerLifeStatus[pName] = !isDead;
                UpdateTurnUI();

                if (isHost && !isGameOver) CheckWinCondition();
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
    private void AddPointToWinnerUID(string winnerUsername)
    {
        Debug.Log($"<color=cyan>[Game] Đang tìm kiếm tài khoản của {winnerUsername} để cộng điểm...</color>");

        // Truy vấn tối ưu: Chỉ lấy user có username khớp với người thắng
        usersRef.OrderByChild("username").EqualTo(winnerUsername).GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsFaulted || !task.Result.Exists)
            {
                Debug.LogWarning("Không tìm thấy dữ liệu người chơi để cộng điểm.");
                return;
            }

            // Lấy UID đầu tiên khớp kết quả (thường chỉ có 1)
            DataSnapshot userSnapshot = task.Result.Children.First();
            string targetUID = userSnapshot.Key;

            // Tiến hành cộng 1 điểm vào score bằng Transaction để tránh xung đột
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
                    Debug.Log($"<color=cyan>[Firebase] Đã cộng 1 điểm thành công cho {winnerUsername}. Điểm mới sẽ tự cập nhật trên BXH.</color>");
            });
        });
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

                    // Chỉ chủ phòng thực hiện ghi nhận điểm số lên server
                    if (isHost) AddPointToWinnerUID(winner);
                });
            }
        };
    }

    

    private void ShowVictoryUI(string winnerName)
    {
        string myName = RoomManager.Instance.currentUsername;
        bool amIWinner = (winnerName == myName);

        if (turnInfoText != null)
            turnInfoText.text = amIWinner ? "<color=yellow>★ CHIẾN THẮNG! ★</color>" : $"<color=green>{winnerName} thắng!</color>";

        if (victoryPanel != null)
        {
            if (Panel_GamePlay != null) Panel_GamePlay.SetActive(false);
            victoryPanel.SetActive(true);
            if (winnerNameText != null)
                winnerNameText.text = amIWinner ? "BẠN ĐÃ CHIẾN THẮNG!" : $"{winnerName} ĐÃ CHIẾN THẮNG!";
        }
    }

    public void ReturnToLoadRoom()
    {
        SceneManager.LoadScene("LoadRoomScene");
    }

    public void UpdateTurnUI()
    {
        if (isGameOver) return;
        if (RoomManager.Instance == null || RoomManager.Instance.currentRoomPlayers == null) return;

        string activePlayer = RoomManager.Instance.currentRoomPlayers[currentTurnIndex];
        string myName = RoomManager.Instance.currentUsername;
        bool isMe = (activePlayer == myName);
        bool isActivePlayerAlive = playerLifeStatus.ContainsKey(activePlayer) && playerLifeStatus[activePlayer];

        if (turnInfoText != null)
        {
            if (!isActivePlayerAlive) turnInfoText.text = $"<color=red>{activePlayer} đã loại.</color>";
            else if (isWaitingForDefuse) turnInfoText.text = isMe ? "<color=red>⚠ DÍNH BOM! GỠ NGAY! ⚠</color>" : $"<color=orange>{activePlayer} đang gỡ...</color>";
            else turnInfoText.text = isMe ? "<color=yellow>LƯỢT CỦA BẠN</color>" : $"Lượt: {activePlayer}";
        }

        if (playCardButton != null)
        {
            bool amIAlive = playerLifeStatus.ContainsKey(myName) && playerLifeStatus[myName];
            if (!isMe || !amIAlive) playCardButton.interactable = false;
            else
            {
                var selected = OnlineCardController.SelectedCard;
                if (selected == null) playCardButton.interactable = false;
                else playCardButton.interactable = !isWaitingForDefuse || (selected.cardType == DrawPileManager.CardType.Defuse);
            }
        }
    }

    public bool IsMyTurn()
    {
        if (isGameOver) return false;
        string activePlayer = RoomManager.Instance.currentRoomPlayers[currentTurnIndex];
        bool amIAlive = playerLifeStatus.ContainsKey(RoomManager.Instance.currentUsername) && playerLifeStatus[RoomManager.Instance.currentUsername];
        return (RoomManager.Instance.currentUsername == activePlayer) && amIAlive;
    }

    public void OnCardSelectionChanged() => UpdateTurnUI();
}