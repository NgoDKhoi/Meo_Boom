using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;   

public class RoomJoinManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_InputField inputRoomID;   
    public Button btnThamGiaPhong;
    public Button btnThoat;

    void Start()
    {
        // Gán sự kiện bấm nút
        btnThamGiaPhong.onClick.AddListener(OnJoinRoom);
        btnThoat.onClick.AddListener(OnExit);
    }

    void OnJoinRoom()
    {
        string roomID = inputRoomID.text;

        // Giả lập tham gia phòng (chưa có Firebase)
        Debug.Log("Giả lập tham gia phòng với ID: " + roomID);

        SceneManager.LoadScene("RoomScene");
    }

    void OnExit()
    {
        Debug.Log("Thoát ra menu chính (tạm thời chưa làm)");
        // SceneManager.LoadScene("MainMenu");
    }
}
