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

    [Header("--- UI References ---")]
    public TextMeshProUGUI turnInfoText;
    public Button playCardButton; // Kéo nút đánh bài vào đây

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

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                PointerEventData eventData = new PointerEventData(EventSystem.current);
                eventData.position = Input.mousePosition;
                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, results);

                foreach (var result in results)
                {
                    if (result.gameObject.name == "DrawPileDeck")
                    {
                        OnDeckClicked();
                        break;
                    }
                }
            }
        }
    }

    private void OnDeckClicked()
    {
        if (!IsMyTurn()) return;

        float timeSinceLastClick = Time.time - lastClickTime;
        if (timeSinceLastClick <= DOUBLE_CLICK_TIME)
        {
            RequestDrawCard();
        }
        lastClickTime = Time.time;
    }

    public void RequestDrawCard()
    {
        Dictionary<string, object> action = new Dictionary<string, object>();
        action["type"] = "DRAW";
        action["sender"] = RoomManager.Instance.currentUsername;
        roomRef.Child("actions").Push().SetValueAsync(action);
    }

    private void ListenToGameState()
    {
        roomRef.Child("gameData").Child("currentTurnIndex").ValueChanged += (s, e) => {
            if (e.Snapshot.Exists && e.Snapshot.Value != null)
            {
                currentTurnIndex = Convert.ToInt32(e.Snapshot.Value);
                UpdateTurnUI();
            }
        };
    }

    private void ListenToActions()
    {
        roomRef.Child("actions").ChildAdded += (s, e) => {
            if (!e.Snapshot.Exists) return;
            var actionData = e.Snapshot.Value as Dictionary<string, object>;
            if (actionData != null && isHost && actionData["type"].ToString() == "DRAW")
            {
                Host_HandleTurnTransition();
            }
        };
    }

    private void Host_HandleTurnTransition()
    {
        if (!isHost) return;
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
            turnInfoText.text = isMe ? "<color=yellow>LƯỢT CỦA BẠN</color>" : $"Lượt của: {activePlayer}";

        // Chỉ bật nút khi đến lượt VÀ đã chọn bài (logic này bổ sung ở handler nút)
        if (playCardButton != null) playCardButton.interactable = isMe && OnlineCardController.SelectedCard != null;
    }

    public bool IsMyTurn()
    {
        if (RoomManager.Instance == null || RoomManager.Instance.currentRoomPlayers == null) return false;
        if (currentTurnIndex < 0 || currentTurnIndex >= RoomManager.Instance.currentRoomPlayers.Count) return false;
        return RoomManager.Instance.currentUsername == RoomManager.Instance.currentRoomPlayers[currentTurnIndex];
    }
}