using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomWaitingUI : MonoBehaviour
{
    // Hàm này gắn cho nút Bắt đầu
    public void OnStartButton()
    {
        SceneManager.LoadScene("GameScene");
    }

    // Hàm này gắn cho nút Thoát
    public void OnExitButton()
    {
        SceneManager.LoadScene("LoadRoomScene");
    }
}
