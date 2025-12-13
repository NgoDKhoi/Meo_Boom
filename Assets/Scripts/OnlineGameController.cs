using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OnlineGameController: cầu nối giữa RoomManager (RTDB) và GameManager (offline engine).
/// Mô hình: Host-authoritative.
/// - Host: nhận request -> thực thi GameManager -> broadcast full state snapshots / events.
/// - Client: gửi request -> apply visual updates khi nhận snapshot.
///
/// REQUIREMENTS:
/// - RoomManager.Instance must provide SendGameMessage(Dictionary) and RegisterGameMessageListener(Action<Dictionary<string,object>>)
/// - RoomManager.Instance.IsHost() must return true for host client.
/// </summary>
public class OnlineGameController : MonoBehaviour
{
    public static OnlineGameController Instance;

    // Throttling broadcast (avoid spamming RTDB)
    public float minBroadcastInterval = 0.15f;
    private float lastBroadcastTime = -10f;

    // A small cached representation of game state we broadcast
    [Serializable]
    public class PlayerState
    {
        public string username;
        public List<string> hand; // list of string names of card types
        public bool isDead;
    }

    [Serializable]
    public class GameStateSnapshot
    {
        public string type = "snapshot";
        public int currentPlayerIndex;
        public int turnsRemaining;
        public int nextTurnStartingCount;
        public List<PlayerState> players = new List<PlayerState>();
        public int drawPileCount;
        public string lastAction; // optional log
        public string sender; // who produced this snapshot (host)
        public double timestamp;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // register to incoming game messages
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.RegisterGameMessageListener(OnGameMessageReceived);
        }
        else
        {
            Debug.LogWarning("[OnlineGameController] RoomManager.Instance is null at Start()");
        }
    }

    void OnDestroy()
    {
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.UnregisterGameMessageListener(OnGameMessageReceived);
        }
    }

    // ---------------- Host-side helpers ----------------

    // Call this from host to broadcast the full authoritative snapshot
    public void BroadcastFullSnapshot(string optionalLog = "")
    {
        if (!IsHost()) return;

        if (Time.time - lastBroadcastTime < minBroadcastInterval)
        {
            // throttle
            return;
        }

        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning("[OnlineGameController] GameManager.Instance is null - cannot broadcast");
            return;
        }

        GameStateSnapshot snap = new GameStateSnapshot();
        snap.currentPlayerIndex = gm.currentPlayerIndex;
        snap.turnsRemaining = gm.turnsRemaining;
        snap.nextTurnStartingCount = (int)typeof(GameManager).GetField("nextTurnStartingCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(gm);
        snap.drawPileCount = gm.drawPileManager != null ? gm.drawPileManager.GetRemainingCount() : 0;
        snap.lastAction = optionalLog;
        snap.sender = RoomManager.Instance.currentUsername;
        snap.timestamp = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;

        foreach (var p in gm.players)
        {
            PlayerState ps = new PlayerState();
            ps.username = p.name;
            ps.isDead = p.isDead;
            ps.hand = new List<string>();
            foreach (var c in p.hand)
            {
                ps.hand.Add(c.ToString());
            }
            snap.players.Add(ps);
        }

        var dict = new Dictionary<string, object>();
        dict["type"] = snap.type;
        dict["payload"] = JsonUtility.ToJson(snap);

        RoomManager.Instance.SendGameMessage(dict);
        lastBroadcastTime = Time.time;
    }

    bool IsHost()
    {
        try
        {
            return RoomManager.Instance != null && RoomManager.Instance.IsHost();
        }
        catch
        {
            if (RoomManager.Instance == null) return false;
            var players = RoomManager.Instance.currentRoomPlayers;
            if (players == null || players.Count == 0) return false;
            return players[0] == RoomManager.Instance.currentUsername;
        }
    }

    // ---------------- Client-side incoming messages ----------------

    void OnGameMessageReceived(Dictionary<string, object> message)
    {
        if (message == null || !message.ContainsKey("type")) return;

        var type = message["type"] as string;
        if (type == "snapshot")
        {
            string json = null;
            if (message.ContainsKey("payload")) json = message["payload"] as string;
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var snap = JsonUtility.FromJson<GameStateSnapshot>(json);
                ApplySnapshotOnClient(snap);
            }
            catch (Exception ex)
            {
                Debug.LogError($"OnlineGameController: failed parse snapshot: {ex}");
            }
        }
        else if (type == "request_action")
        {
            if (!IsHost()) return;
            if (!message.ContainsKey("payload")) return;

            string json = message["payload"] as string;
            var req = JsonUtility.FromJson<DictionaryWrapper>(json).ToDictionary();
            ProcessClientRequest(req);
        }
    }

    [Serializable]
    class DictionaryWrapper
    {
        public List<string> keys = new List<string>();
        public List<string> values = new List<string>();
        public Dictionary<string, string> ToDictionary()
        {
            var d = new Dictionary<string, string>();
            for (int i = 0; i < Mathf.Min(keys.Count, values.Count); i++) d[keys[i]] = values[i];
            return d;
        }
        public static DictionaryWrapper FromDict(Dictionary<string, string> dd)
        {
            var w = new DictionaryWrapper();
            foreach (var kv in dd)
            {
                w.keys.Add(kv.Key);
                w.values.Add(kv.Value);
            }
            return w;
        }
    }

    void ApplySnapshotOnClient(GameStateSnapshot snap)
    {
        if (IsHost()) return;

        var gsm = FindObjectOfType<GameSceneManager>();
        if (gsm == null)
        {
            Debug.LogWarning("[OnlineGameController] GameSceneManager not found to apply snapshot.");
            return;
        }

        var gm = GameManager.Instance;
        if (gm != null)
        {
            for (int i = 0; i < snap.players.Count && i < gm.players.Count; i++)
            {
                var ps = snap.players[i];
                gm.players[i].name = ps.username;
                gm.players[i].isDead = ps.isDead;
                gm.UpdateUIForBot(gm.players[i]);
            }

            gm.currentPlayerIndex = snap.currentPlayerIndex;
            gm.turnsRemaining = snap.turnsRemaining;
            gm.UpdateDrawPileCountUI();
        }

        gsm.InitializeGameUI(RoomManager.Instance.currentRoomPlayers, RoomManager.Instance.currentUsername);

        for (int vi = 0; vi < gsm.playerUIParent.childCount; vi++)
        {
            var child = gsm.playerUIParent.GetChild(vi).gameObject;
            var txts = child.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var t in txts)
            {
                if (t.gameObject.name.IndexOf("Card", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    int pIndex = (snap.players.Count > vi) ? vi : -1;
                    if (pIndex >= 0)
                    {
                        t.text = $"{snap.players[pIndex].hand.Count} Lá";
                    }
                }
            }
        }
    }

    void ProcessClientRequest(Dictionary<string, string> req)
    {
        if (!IsHost()) return;
        if (req == null || !req.ContainsKey("action")) return;

        var gm = GameManager.Instance;
        string action = req["action"];
        string actor = req.ContainsKey("player") ? req["player"] : RoomManager.Instance.currentUsername;

        Debug.Log($"[OnlineGameController] Host processing request {action} from {actor}");

        switch (action)
        {
            case "request_draw":
                {
                    int playerIndex = gm.players.FindIndex(p => p.name == actor);
                    if (playerIndex == gm.currentPlayerIndex)
                    {
                        gm.StartCoroutine(gm.DrawCardRoutine(false, true));
                        StartCoroutine(DelayedBroadcast(0.5f));
                    }
                    break;
                }
            case "request_play":
                {
                    if (!req.ContainsKey("cardType")) return;
                    string cardTypeStr = req["cardType"];

                    int playerIndex = gm.players.FindIndex(p => p.name == actor);
                    if (playerIndex >= 0)
                    {
                        var player = gm.players[playerIndex];

                        DrawPileManager.CardType ct;
                        if (Enum.TryParse(cardTypeStr, out ct))
                        {
                            gm.ProcessCardData(player, ct);
                            gm.StartCoroutine(gm.HandleCardEffect(ct, player));
                            StartCoroutine(DelayedBroadcast(0.5f));
                        }
                    }
                    break;
                }
        }
    }

    IEnumerator DelayedBroadcast(float delay)
    {
        yield return new WaitForSeconds(delay);
        BroadcastFullSnapshot("delayed_broadcast");
    }

    // Clients call this to send action request to host
    public void ClientSendActionRequest(string action, Dictionary<string, string> payload)
    {
        if (IsHost())
        {
            Debug.LogWarning("[OnlineGameController] ClientSendActionRequest called on host. Execute locally instead.");
            var req = new Dictionary<string, string>(payload ?? new Dictionary<string, string>());
            req["action"] = action;
            ProcessClientRequest(req);
            return;
        }

        var wrapper = DictionaryWrapper.FromDict(payload ?? new Dictionary<string, string>());
        string json = JsonUtility.ToJson(wrapper);

        var msg = new Dictionary<string, object>();
        msg["type"] = "request_action";
        msg["payload"] = json;

        RoomManager.Instance.SendGameMessage(msg);
    }
}
