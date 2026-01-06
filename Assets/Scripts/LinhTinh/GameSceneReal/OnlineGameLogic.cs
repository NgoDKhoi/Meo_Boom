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

    private const string DatabaseUrl = "https://mygametest2-default-rtdb.asia-southeast1.firebasedatabase.app";

    [Header("--- Trạng thái Game ---")]
    public int currentTurnIndex = -1;
    public bool isHost = false;
    public bool isWaitingForDefuse = false;
    public bool isGameOver = false;

    // Dictionary lưu trữ: Key = Username, Value = IsAlive (true là còn sống)
    public Dictionary<string, bool> playerLifeStatus = new Dictionary<string, bool>();

    [Header("--- UI References ---")]
    public TextMeshProUGUI turnInfoText;
    public Button playCardButton;

    [Header("--- Victory UI ---")]
    public GameObject victoryPanel;
    public TextMeshProUGUI winnerNameText;
    public Button confirmVictoryButton;
    public GameObject Panel_GamePlay;

    // Lưu trữ các Event Handler để gỡ bỏ khi OnDestroy
    private EventHandler<ValueChangedEventArgs> turnHandler;
    private EventHandler<ValueChangedEventArgs> waitingHandler;
    private EventHandler<ValueChangedEventArgs> winnerHandler;
    private EventHandler<ChildChangedEventArgs> lifeAddedHandler;
    private EventHandler<ChildChangedEventArgs> lifeChangedHandler;

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
            confirmVictoryButton.onClick.AddListener(OnConfirmVictoryClick);

        StartCoroutine(WaitForRoomData());
    }

    private System.Collections.IEnumerator WaitForRoomData()
    {
        while (RoomManager.Instance == null || string.IsNullOrEmpty(RoomManager.Instance.currentRoomID))
        {
            yield return new WaitForSeconds(0.5f);
        }

        roomID = RoomManager.Instance.currentRoomID;
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
                    playerLifeStatus[p] = true; // Mặc định ban đầu đều sống
            }
        }

        ListenToGameState();
        ListenToPlayersLife();
        ListenToWinner();
    }

    private void ListenToGameState()
    {
        turnHandler = (s, e) => {
            if (e.Snapshot.Exists && e.Snapshot.Value != null)
            {
                int newIdx = Convert.ToInt32(e.Snapshot.Value);
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    currentTurnIndex = newIdx;
                    UpdateTurnUI();
                });
            }
        };
        roomRef.Child("gameData/currentTurnIndex").ValueChanged += turnHandler;

        waitingHandler = (s, e) => {
            bool waiting = e.Snapshot.Exists && (bool)e.Snapshot.Value;
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                isWaitingForDefuse = waiting;
                UpdateTurnUI();
            });
        };
        roomRef.Child("gameData/isWaitingForDefuse").ValueChanged += waitingHandler;
    }

    private void ListenToPlayersLife()
    {
        // Lắng nghe nhánh playersStatus (nơi ActionManager cập nhật isDead)
        lifeAddedHandler = HandlePlayerLifeChange;
        lifeChangedHandler = HandlePlayerLifeChange;

        roomRef.Child("playersStatus").ChildAdded += lifeAddedHandler;
        roomRef.Child("playersStatus").ChildChanged += lifeChangedHandler;
    }

    private void HandlePlayerLifeChange(object sender, ChildChangedEventArgs e)
    {
        if (e.Snapshot.Exists)
        {
            string pName = e.Snapshot.Key;
            bool isDead = false;
            if (e.Snapshot.HasChild("isDead"))
            {
                isDead = Convert.ToBoolean(e.Snapshot.Child("isDead").Value);
            }

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                playerLifeStatus[pName] = !isDead;
                Debug.Log($"[Logic] Cập nhật trạng thái: {pName} - {(isDead ? "ĐÃ CHẾT" : "CÒN SỐNG")}");

                // --- PHẦN SỬA ĐỔI: NHẢY LƯỢT TỰ ĐỘNG ---
                // Nếu người vừa chết là người đang tới lượt, Host phải thực hiện chuyển lượt ngay
                if (isHost && isDead && !isGameOver)
                {
                    string currentPlayer = RoomManager.Instance.currentRoomPlayers[currentTurnIndex];
                    if (pName == currentPlayer)
                    {
                        Debug.Log($"[Logic] Người chơi {pName} chết trong lượt. Host đang chuyển lượt...");
                        OnlineGameActionManager.Instance.HandleEndTurnLogic();
                    }
                }
                // ---------------------------------------

                UpdateTurnUI();

                if (isHost && !isGameOver)
                {
                    CheckWinCondition();
                }
            });
        }
    }

    private void CheckWinCondition()
    {
        // Kiểm tra logic Victory: Chỉ còn 1 người sống sót
        if (isGameOver || playerLifeStatus.Count < 1) return;

        var survivors = playerLifeStatus.Where(p => p.Value == true).Select(p => p.Key).ToList();

        // Ngay cả khi phòng chưa đủ 4 người, chỉ cần survivors còn 1 là thắng
        if (survivors.Count == 1)
        {
            string winnerName = survivors[0];
            isGameOver = true; // Chặn kiểm tra nhiều lần
            Debug.Log($"[Logic] Tìm thấy người chiến thắng cuối cùng: {winnerName}");

            // 1. Cập nhật tên winner lên gameData để mọi người cùng thấy
            roomRef.Child("gameData/winner").SetValueAsync(winnerName);

            // 2. Thêm nhánh winner vào playersStatus của người đó (giống isDead)
            roomRef.Child("playersStatus").Child(winnerName).Child("isWinner").SetValueAsync(true);
        }
    }

    private void ListenToWinner()
    {
        winnerHandler = (s, e) => {
            if (e.Snapshot.Exists && e.Snapshot.Value != null)
            {
                string winner = e.Snapshot.Value.ToString();
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    // Nếu đã hiện VictoryPanel rồi thì không hiện lại
                    if (victoryPanel.activeSelf) return;

                    isGameOver = true;
                    StartCoroutine(ShowVictoryUI(winner));

                    // CHỈ HOST MỚI THỰC HIỆN CỘNG ĐIỂM VÀO DATABASE
                    if (isHost)
                    {
                        Debug.Log($"[Game] Host xác nhận {winner} thắng. Đang cộng 1 điểm...");
                        AddPointToWinnerUID(winner);
                    }
                });
            }
        };
        roomRef.Child("gameData/winner").ValueChanged += winnerHandler;
    }

    private void AddPointToWinnerUID(string winnerUsername)
    {
        usersRef.OrderByChild("username").EqualTo(winnerUsername).GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsFaulted || !task.Result.Exists)
            {
                Debug.LogError("Lỗi truy vấn Database hoặc không tìm thấy User để cộng điểm.");
                return;
            }

            DataSnapshot userSnapshot = task.Result.Children.First();
            string targetUID = userSnapshot.Key;
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
                    Debug.Log($"<color=green>Đã cộng +1 điểm thành công cho {winnerUsername}!</color>");
            });
        });
    }

    private System.Collections.IEnumerator ShowVictoryUI(string winnerName)
    {
        yield return new WaitForSeconds(2.5f);

        if (AudioManager.Instance != null)
        {
            // Phát nhạc chiến thắng (Nhạc này cũng sẽ loop nếu bạn dùng PlayMusic)
            AudioManager.Instance.PlayMusic(AudioManager.Instance.victoryMusic);
        }

        string myName = RoomManager.Instance.currentUsername;
        bool amIWinner = (winnerName == myName);

        if (victoryPanel != null)
        {
            if (Panel_GamePlay != null) Panel_GamePlay.SetActive(false); // Ẩn UI chơi game
            victoryPanel.SetActive(true);

            if (winnerNameText != null)
                winnerNameText.text = amIWinner ? "<color=yellow>BẠN ĐÃ CHIẾN THẮNG!</color>" : $"<color=green>{winnerName}</color> ĐÃ CHIẾN THẮNG!";
        }
    }

    public void OnConfirmVictoryClick()
    {
        if (AudioManager.Instance != null)
        {
            // Chuyển lại nhạc Theme ngay khi nhấn nút quay về
            AudioManager.Instance.PlayMusic(AudioManager.Instance.themeMusic);
        }

        // Khi bấm xác nhận, người chơi thoát khỏi danh sách "players" của phòng để dọn dẹp phòng
        string myName = RoomManager.Instance.currentUsername;
        roomRef.Child("players").Child(myName).RemoveValueAsync().ContinueWithOnMainThread(t => {
            ReturnToLoadRoom();
        });
    }

    private void ReturnToLoadRoom()
    {
        // Dọn dẹp dữ liệu Manager trước khi chuyển Scene
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.currentRoomID = "";
            if (RoomManager.Instance.currentRoomPlayers != null)
                RoomManager.Instance.currentRoomPlayers.Clear();
        }

        Debug.Log("Quay lại LoadRoomScene - UI Bảng xếp hạng sẽ tự động cập nhật dữ liệu mới.");
        SceneManager.LoadScene("LoadRoomScene");
    }

    public void UpdateTurnUI()
    {
        if (isGameOver || RoomManager.Instance == null || RoomManager.Instance.currentRoomPlayers == null) return;
        if (currentTurnIndex < 0 || currentTurnIndex >= RoomManager.Instance.currentRoomPlayers.Count) return;

        string activePlayer = RoomManager.Instance.currentRoomPlayers[currentTurnIndex];
        string myName = RoomManager.Instance.currentUsername;
        bool isMe = (activePlayer == myName);

        bool isActivePlayerAlive = playerLifeStatus.ContainsKey(activePlayer) && playerLifeStatus[activePlayer];
        bool amIAlive = playerLifeStatus.ContainsKey(myName) && playerLifeStatus[myName];

        if (turnInfoText != null)
        {
            if (!isActivePlayerAlive)
                turnInfoText.text = $"<color=red>{activePlayer} đã bị loại.</color>";
            else if (isWaitingForDefuse)
                turnInfoText.text = isMe ? "<color=red>⚠ DÍNH BOM! GỠ NGAY! ⚠</color>" : $"<color=orange>{activePlayer} đang gỡ...</color>";
            else
                turnInfoText.text = isMe ? "<color=yellow>LƯỢT CỦA BẠN</color>" : $"Lượt: {activePlayer}";
        }

        if (playCardButton != null)
        {
            // Chỉ cho phép bấm nút nếu là lượt mình VÀ mình còn sống
            if (!isMe || !amIAlive)
            {
                playCardButton.interactable = false;
            }
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
        if (isGameOver || RoomManager.Instance == null || RoomManager.Instance.currentRoomPlayers == null) return false;
        if (currentTurnIndex < 0 || currentTurnIndex >= RoomManager.Instance.currentRoomPlayers.Count) return false;

        string activePlayer = RoomManager.Instance.currentRoomPlayers[currentTurnIndex];
        string myName = RoomManager.Instance.currentUsername;
        bool amIAlive = playerLifeStatus.ContainsKey(myName) && playerLifeStatus[myName];

        return (myName == activePlayer) && amIAlive;
    }

    private void OnDestroy()
    {
        // Gỡ bỏ tất cả các Listener để tránh rò rỉ bộ nhớ hoặc lỗi logic khi chơi ván mới
        if (roomRef != null)
        {
            roomRef.Child("gameData/currentTurnIndex").ValueChanged -= turnHandler;
            roomRef.Child("gameData/isWaitingForDefuse").ValueChanged -= waitingHandler;
            roomRef.Child("gameData/winner").ValueChanged -= winnerHandler;
            roomRef.Child("playersStatus").ChildAdded -= lifeAddedHandler;
            roomRef.Child("playersStatus").ChildChanged -= lifeChangedHandler;
        }
    }

    public void OnCardSelectionChanged() => UpdateTurnUI();
}
