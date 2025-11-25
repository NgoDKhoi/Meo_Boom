using UnityEngine;
using TMPro;
using System.Collections; 
using UnityEngine.UI; 

public class FindRoomUIHandler : MonoBehaviour
{
    // 1. Dữ liệu tìm phòng
    public TMP_InputField roomIDInputField;
    public Button findRoomButton;           

    // 2. Tham chiếu Status (Image Wrong ID và Text con)
    public GameObject statusMessagePanel; 
    public TMP_Text statusText;         

    // 3. Tham chiếu chuyển đổi Panel
    public GameObject panelMainUI;        
    public GameObject panelNhapID;        

    private Coroutine hideStatusCoroutine;

    // Tham chiếu đến Singleton RoomService và Username
    private JoinRoomManager roomService => JoinRoomManager.Instance;
    private string username => GameDataManager.Instance?.username;

    // --- LOGIC TÌM PHÒNG (GẮN VÀO NÚT TÌM) ---

    public void OnFindRoomButtonClick()
    {
        // 1. Dừng Coroutine cũ (nếu đang chạy)
        if (hideStatusCoroutine != null)
        {
            StopCoroutine(hideStatusCoroutine);
            hideStatusCoroutine = null;
        }

        // Ẩn thông báo cũ và bật lại tương tác trước khi xử lý
        SetStatus("");

        if (roomService == null)
        {
            ShowStatusWithTimeout("❌ Lỗi cấu hình: JoinRoomManager chưa khởi tạo.", 5f);
            return;
        }

        string roomID = roomIDInputField.text.Trim();

        if (string.IsNullOrEmpty(roomID) || roomID.Length != 6)
        {
            // 💡 HIỂN THỊ LỖI VÀ TỰ ẨN SAU 5 GIÂY
            ShowStatusWithTimeout("❌ Vui lòng nhập Room ID hợp lệ (6 ký tự).", 5f);
            return;
        }

        if (string.IsNullOrEmpty(username))
        {
            ShowStatusWithTimeout("❌ Lỗi: Username chưa được tải. Vui lòng đăng nhập lại.", 5f);
            return;
        }

        // Nếu không có lỗi, gọi dịch vụ Firebase
        roomService.JoinRoom(roomID, username, statusText);
    }

    // --- HÀM MỚI: HIỂN THỊ VÀ HẸN GIỜ TẮT ---
    public void ShowStatusWithTimeout(string message, float delay)
    {
        SetStatus(message); // Hiển thị thông báo ngay lập tức

        // Bắt đầu Coroutine để ẩn sau delay
        hideStatusCoroutine = StartCoroutine(HideStatusAfterDelay(delay));
    }

    /// Hàm này được gọi bởi logic Firebase HOẶC ShowStatusWithTimeout để cập nhật trạng thái UI.
    public void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        if (statusMessagePanel != null)
        {
            bool isActive = !string.IsNullOrEmpty(message);
            statusMessagePanel.SetActive(isActive);

            // 💡 ĐIỀU KHIỂN TƯƠNG TÁC
            SetInputInteractable(!isActive); // Nếu đang hiển thị (isActive=true) thì khóa (interactable=false)
        }
        Debug.Log(message);
    }

    // --- COROUTINE VÀ HÀM HỖ TRỢ ---

    IEnumerator HideStatusAfterDelay(float delay)
    {
        // Khóa tương tác đã được SetStatus() gọi

        yield return new WaitForSeconds(delay);

        // Ẩn thông báo và bật lại tương tác
        SetStatus("");
    }

    private void SetInputInteractable(bool interactable)
    {
        // Bật/Tắt tương tác của Input Field và Nút TÌM PHÒNG
        if (roomIDInputField != null)
        {
            roomIDInputField.interactable = interactable;
        }
        if (findRoomButton != null)
        {
            findRoomButton.interactable = interactable;
        }
    }

    // --- LOGIC THOÁT PANEL (GẮN VÀO NÚT THOÁT) ---

    public void OnBackButtonInJoinPanelClicked()
    {
        // Dừng Coroutine nếu người dùng thoát trong khi thông báo đang hiển thị
        if (hideStatusCoroutine != null)
        {
            StopCoroutine(hideStatusCoroutine);
            hideStatusCoroutine = null;
        }

        if (panelMainUI != null && panelNhapID != null)
        {
            panelNhapID.SetActive(false);
            panelMainUI.SetActive(true);

            // Đảm bảo trạng thái và tương tác được reset
            SetStatus("");
            SetInputInteractable(true);
        }
        else
        {
            Debug.LogError("❌ Lỗi: Chưa gán Panel Main UI hoặc Panel Nhập ID.");
        }
    }
}