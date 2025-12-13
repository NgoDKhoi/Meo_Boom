using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Database;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    [Header("Room Info")]
    public string currentRoomID;
    public string currentUsername;
    public List<string> currentRoomPlayers = new List<string>();

    [Header("Firebase")]
    private DatabaseReference roomRef;
    private DatabaseReference gameMessageRef;

    private event Action<Dictionary<string, object>> onGameMessageReceived;
    private string lastProcessedMessageKey = "";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // ⭐ GIỮ LẠI LOGIC CŨ ⭐
            if (GameDataManager.Instance != null)
            {
                currentUsername = GameDataManager.Instance.username;
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // =====================================================
    // INIT ROOM (GỌI SAU KHI CREATE / JOIN ROOM)
    // =====================================================
    public void InitRoom(string roomID)
    {
        currentRoomID = roomID;

        roomRef = FirebaseManager.Instance.Database
            .RootReference
            .Child("rooms")
            .Child(currentRoomID);

        gameMessageRef = roomRef.Child("gameMessages");

        ListenForPlayerList();
        ListenForGameMessages();
    }

    // =====================================================
    // HOST CHECK
    // =====================================================
    public bool IsHost()
    {
        if (currentRoomPlayers == null || currentRoomPlayers.Count == 0)
            return false;

        return currentRoomPlayers[0] == currentUsername;
    }

    // =====================================================
    // PLAYER LIST LISTENER
    // =====================================================
    private void ListenForPlayerList()
    {
        roomRef.Child("players").ValueChanged += (sender, args) =>
        {
            if (!args.Snapshot.Exists) return;

            currentRoomPlayers.Clear();
            foreach (var child in args.Snapshot.Children)
            {
                currentRoomPlayers.Add(child.Value.ToString());
            }

            // ⭐ GIỮ LẠI HÀNH VI CŨ ⭐
            NotifyRoomDataLoaded(currentRoomPlayers);
        };
    }

    // =====================================================
    // MESSAGE SYSTEM (ONLINE TURN BASED)
    // =====================================================
    public void SendGameMessage(Dictionary<string, object> message)
    {
        if (gameMessageRef == null) return;
        gameMessageRef.Push().SetValueAsync(message);
    }

    public void RegisterGameMessageListener(Action<Dictionary<string, object>> callback)
    {
        onGameMessageReceived += callback;
    }

    public void UnregisterGameMessageListener(Action<Dictionary<string, object>> callback)
    {
        onGameMessageReceived -= callback;
    }

    private void ListenForGameMessages()
    {
        gameMessageRef.ChildAdded += (sender, args) =>
        {
            if (!args.Snapshot.Exists) return;

            if (args.Snapshot.Key == lastProcessedMessageKey) return;
            lastProcessedMessageKey = args.Snapshot.Key;

            var data = args.Snapshot.Value as Dictionary<string, object>;
            if (data == null) return;

            onGameMessageReceived?.Invoke(data);
        };
    }

    // =====================================================
    // ⭐ LOGIC CŨ CỦA BẠN – GIỮ NGUYÊN ⭐
    // =====================================================
    public void NotifyRoomDataLoaded(List<string> players)
    {
        currentRoomPlayers = players;

        GameSceneManager gameManager = FindObjectOfType<GameSceneManager>();
        if (gameManager != null)
        {
            gameManager.InitializeGameUI(currentRoomPlayers, currentUsername);
            Debug.Log("✅ InitializeGameUI được gọi từ RoomManager.");
        }
    }
}
