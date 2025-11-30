using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    public string currentRoomID;
    public string currentUsername;
    public List<string> currentRoomPlayers = new List<string>();

    // LƯU Ý: Bạn cần có logic Firebase trong RoomManager
    // để gán giá trị cho currentRoomPlayers và currentRoomID

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // ⭐ Đảm bảo username được lấy từ GameDataManager ⭐
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

    // ⭐ HÀM PHẢI ĐƯỢC GỌI KHI DỮ LIỆU PHÒNG ĐƯỢC TẢI XONG (TỪ CALLBACK FIREBASE) ⭐
    public void NotifyRoomDataLoaded(List<string> players)
    {
        currentRoomPlayers = players;

        // Tìm và gọi GameSceneManager nếu scene đã được tải
        GameSceneManager gameManager = FindObjectOfType<GameSceneManager>();

        if (gameManager != null)
        {
            gameManager.InitializeGameUI(currentRoomPlayers, currentUsername);
            Debug.Log("✅ RoomManager đã gọi InitializeGameUI.");
        }
        else
        {
            Debug.LogWarning("⚠️ GameSceneManager chưa tồn tại. Sẽ cần gọi lại sau khi Scene tải.");
        }
    }
}