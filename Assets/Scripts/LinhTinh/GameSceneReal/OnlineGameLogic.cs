using UnityEngine;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class OnlineGameLogic : MonoBehaviour
{
    public static OnlineGameLogic Instance;

    private DatabaseReference roomRef;
    private string roomID;

    [Header("--- Trạng thái Game ---")]
    public int currentTurnIndex = -1;
    public bool isHost = false;
    // Biến để tạm dừng chuyển lượt (ví dụ: đang chờ xử lý Bom)
    public bool isTurnPaused = false;

    [Header("--- UI References ---")]
    public TextMeshProUGUI turnInfoText;
    public Button playCardButton;

    [Header("--- Double Click Settings ---")]
    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_TIME = 0.35f;

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
        }

        ListenToGameState();
        ListenToActions();

        if (isHost)
        {
            // Đợi một chút để các Manager khác khởi tạo xong bộ bài
            Invoke("CheckAndInitializeTurn", 2.0f);
        }
    }

    private void CheckAndInitializeTurn()
    {
        if (currentTurnIndex == -1)
        {
            roomRef.Child("gameData").Child("currentTurnIndex").SetValueAsync(0);
        }
    }

    // Lắng nghe trạng thái Game từ Firebase
    private void ListenToGameState()
    {
        // 1. Theo dõi lượt chơi
        roomRef.Child("gameData/currentTurnIndex").ValueChanged += (s, e) => {
            if (e.Snapshot.Exists && e.Snapshot.Value != null)
            {
                int newIdx = Convert.ToInt32(e.Snapshot.Value);
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    currentTurnIndex = newIdx;
                    // Khi chuyển lượt mới, mặc định mở khóa tạm dừng
                    isTurnPaused = false;
                    UpdateTurnUI();
                });
            }
        };

        // 2. Theo dõi biến Pause (nếu muốn đồng bộ trạng thái tạm dừng từ Host)
        roomRef.Child("gameData/isTurnPaused").ValueChanged += (s, e) => {
            if (e.Snapshot.Exists)
            {
                bool paused = (bool)e.Snapshot.Value;
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    isTurnPaused = paused;
                    UpdateTurnUI();
                });
            }
        };
    }

    private void ListenToActions()
    {
        roomRef.Child("actions").ChildAdded += (s, e) => {
            if (!e.Snapshot.Exists) return;
            var data = e.Snapshot.Value as Dictionary<string, object>;
            if (data == null) return;

            string type = data.ContainsKey("type") ? data["type"].ToString() : "";

            // CHỈ HOST: Xử lý logic chuyển lượt
            if (isHost && type == "DRAW_CONFIRMED")
            {
                string cardType = data.ContainsKey("cardType") ? data["cardType"].ToString() : "";

                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    // Nếu rút phải Bom, KHÔNG chuyển lượt, chuyển sang trạng thái chờ Defuse
                    if (cardType == "ExplodingKitten")
                    {
                        SetTurnPause(true);
                        Debug.Log("<color=red>[Logic]</color> Rút phải BOM! Tạm dừng chuyển lượt.");
                    }
                    else
                    {
                        // Nếu không phải bom và không bị tạm dừng bởi Card Effect khác
                        if (!isTurnPaused)
                        {
                            Host_HandleTurnTransition();
                        }
                    }
                });
            }
        };
    }

    // Hàm Host dùng để thay đổi trạng thái Pause trên Firebase
    public void SetTurnPause(bool pause)
    {
        if (!isHost) return;
        roomRef.Child("gameData/isTurnPaused").SetValueAsync(pause);
    }

    private void Host_HandleTurnTransition()
    {
        if (!isHost || isTurnPaused) return;

        int totalPlayers = RoomManager.Instance.currentRoomPlayers.Count;
        int nextTurn = (currentTurnIndex + 1) % totalPlayers;

        roomRef.Child("gameData").Child("currentTurnIndex").SetValueAsync(nextTurn);
    }

    public void UpdateTurnUI()
    {
        if (RoomManager.Instance == null || RoomManager.Instance.currentRoomPlayers == null) return;
        if (currentTurnIndex < 0 || currentTurnIndex >= RoomManager.Instance.currentRoomPlayers.Count) return;

        string activePlayer = RoomManager.Instance.currentRoomPlayers[currentTurnIndex];
        bool isMe = (activePlayer == RoomManager.Instance.currentUsername);

        if (turnInfoText != null)
        {
            if (isTurnPaused)
                turnInfoText.text = isMe ? "<color=red>BẠN ĐANG GỠ BOM!</color>" : $"<color=red>{activePlayer} đang gặp Bom!</color>";
            else
                turnInfoText.text = isMe ? "<color=yellow>LƯỢT CỦA BẠN</color>" : $"Lượt của: {activePlayer}";
        }

        // Cập nhật trạng thái nút đánh bài
        if (playCardButton != null)
            playCardButton.interactable = isMe && OnlineCardController.SelectedCard != null;
    }

    public bool IsMyTurn()
    {
        if (RoomManager.Instance == null || RoomManager.Instance.currentRoomPlayers == null) return false;
        if (currentTurnIndex < 0 || currentTurnIndex >= RoomManager.Instance.currentRoomPlayers.Count) return false;
        return RoomManager.Instance.currentUsername == RoomManager.Instance.currentRoomPlayers[currentTurnIndex];
    }
}