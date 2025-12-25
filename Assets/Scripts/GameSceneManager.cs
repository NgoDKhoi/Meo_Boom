using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class GameSceneManager : MonoBehaviour
{
    
    public GameTableLayout layoutManager;
    public Transform playerUIParent;
    public bool isLocalTestMode = false;

    private GameObject[] allPlayerUIs = new GameObject[4];
    private List<string> allPlayers;
    private string myUsername;
    private int myAbsoluteIndex = -1; // Vị trí tuyệt đối của người chơi hiện tại (0-3)
    private const int N = 4; 

    private bool isInitialized = false;

    void Start()
    {
        if (RoomManager.Instance == null || layoutManager == null)
        {
            Debug.LogError("Lỗi: Không tìm thấy RoomManager hoặc GameTableLayout.");
            return;
        }

        if (isLocalTestMode)
        {
            // BẮT ĐẦU CHẾ ĐỘ MÔ PHỎNG (Local Test)
            SimulateLocalGame();
        }
        else
        {
            // CHẾ ĐỘ THỰC (Firebase): Chờ đồng bộ dữ liệu
            StartCoroutine(WaitForDataSynchronization());
        }
    }

    /// ⭐ HÀM MÔ PHỎNG: Tạo dữ liệu giả lập cho 4 người chơi để test vị trí. ⭐
    private void SimulateLocalGame()
    {
        string localPlayerName = "tndk1603 ";

        // Danh sách 4 người chơi. Host là người đầu tiên trong danh sách (Index 0).
        List<string> mockPlayers = new List<string>
        {
            localPlayerName,
            "Bot 1 ",
            "Bot 2 ",
            "Bot 3 "
        };

        Debug.LogWarning("[GameSceneManager] Đang chạy CHẾ ĐỘ TEST CỤC BỘ. Host/ViewIndex 0: " + localPlayerName);
        InitializeGameUI(mockPlayers, localPlayerName);
    }
    /// Coroutine chờ RoomManager hoàn tất việc tải dữ liệu từ Firebase.
    /// (Chỉ chạy khi isLocalTestMode = false)
    private IEnumerator WaitForDataSynchronization()
    {
        float waitStartTime = Time.time;
        const float maxWaitTime = 10f;

        while ((RoomManager.Instance.currentRoomPlayers == null || RoomManager.Instance.currentRoomPlayers.Count == 0 ||
               string.IsNullOrEmpty(RoomManager.Instance.currentUsername) || RoomManager.Instance.currentUsername == "UserDefault")
               && Time.time - waitStartTime < maxWaitTime)
        {
            yield return new WaitForSeconds(0.1f);
            if (GameDataManager.Instance != null && !string.IsNullOrEmpty(GameDataManager.Instance.username) && GameDataManager.Instance.username != "UserDefault")
            {
                RoomManager.Instance.currentUsername = GameDataManager.Instance.username;

                if (RoomManager.Instance.currentRoomPlayers != null && RoomManager.Instance.currentRoomPlayers.Count > 0)
                {
                    break;
                }
            }
        }

        if (isInitialized) yield break;

        if (RoomManager.Instance.currentRoomPlayers != null && RoomManager.Instance.currentRoomPlayers.Count > 0 &&
            !string.IsNullOrEmpty(RoomManager.Instance.currentUsername) && RoomManager.Instance.currentUsername != "UserDefault")
        {
            InitializeGameUI(RoomManager.Instance.currentRoomPlayers, RoomManager.Instance.currentUsername);
        }
        else
        {
            Debug.LogError($"LỖI THỜI GIAN: Dữ liệu (RoomPlayers/Username) không được tải trong vòng {maxWaitTime} giây.");
        }
    }
    /// Khởi tạo giao diện UI sau khi dữ liệu phòng đã được đồng bộ.
    public void InitializeGameUI(List<string> roomPlayers, string currentUserName)
    {
        if (isInitialized) return;
        isInitialized = true;

        allPlayers = roomPlayers;
        myUsername = currentUserName;

        if (allPlayers == null || allPlayers.Count == 0)
        {
            Debug.LogError("Lỗi: Danh sách người chơi rỗng khi khởi tạo UI.");
            return;
        }

        if (allPlayers.Count > N) allPlayers.RemoveRange(N, allPlayers.Count - N);

        // Tìm vị trí tuyệt đối (index 0, 1, 2, 3) của người chơi hiện tại
        myAbsoluteIndex = allPlayers.IndexOf(myUsername);

        if (myAbsoluteIndex != -1)
        {
            // Logic chính để sắp xếp vị trí tương đối
            SetupPlayerPositions();
        }
        else
        {
            Debug.LogError($"Lỗi: Username '{myUsername}' không nằm trong danh sách phòng.");
        }
    }

    void SetupPlayerPositions()
    {
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in playerUIParent)
        {
            // Kiểm tra nếu tên của đối tượng có chứa "PlayerUI"
            if (child.gameObject.name.Contains("PlayerUI"))
            {
                toDestroy.Add(child.gameObject);
            }
        }

        foreach (GameObject obj in toDestroy)
        {
            Destroy(obj);
        }

        allPlayerUIs = new GameObject[N];

        for (int i = 0; i < allPlayers.Count; i++)
        {
            string playerName = allPlayers[i];
            int absoluteIndex = i;

            // TÍNH TOÁN VỊ TRÍ TƯƠNG ĐỐI
            // Người chơi hiện tại (myUsername) luôn được ánh xạ tới viewIndex = 0
            int viewIndex = (absoluteIndex - myAbsoluteIndex + N) % N;

            if (viewIndex >= 0 && viewIndex < layoutManager.absoluteSpots.Length)
            {
                GameTableLayout.PlayerSpot spot = layoutManager.absoluteSpots[viewIndex];
                GameObject playerUI = Instantiate(layoutManager.playerUIPrefab, playerUIParent);

                RectTransform rt = playerUI.GetComponent<RectTransform>();

                // ÁP DỤNG VỊ TRÍ (POSITION) VÀ GÓC XOAY (ROTATION)
                rt.anchoredPosition = spot.position;
                rt.localRotation = Quaternion.Euler(0, 0, spot.rotation);

                // Đặt tên mới để dễ quản lý trong Hierarchy
                playerUI.name = "PlayerUI_View" + viewIndex + "_" + playerName;

                allPlayerUIs[viewIndex] = playerUI;

                AssignPlayerInfo(playerUI, playerName, viewIndex);
            }
        }
    }
    /// Gán Username và CardCount bằng cách sử dụng logic tìm kiếm tên linh hoạt.
    void AssignPlayerInfo(GameObject playerUI, string playerName, int viewIndex)
    {
        TMP_Text usernameText = null;
        // Lấy tất cả các component TMP_Text trong Prefab con (Tìm kiếm sâu)
        TMP_Text[] allTexts = playerUI.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text textComponent in allTexts)
        {
            string name = textComponent.gameObject.name;

            // Tìm kiếm linh hoạt cho Username/PlayerName
            if (name.Contains("Username", System.StringComparison.OrdinalIgnoreCase))
            {
                usernameText = textComponent;
            }
        }

        if (usernameText != null)
        {
            usernameText.text = playerName;
            usernameText.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError($"LỖI UI: Không tìm thấy Text Component chứa 'Username' hoặc 'PlayerName' trong Prefab {playerUI.name}.");
        } 
    }
}