using UnityEngine;

public class UI_Manager : MonoBehaviour
{
    // Các Panel UI hiện có
    public GameObject panelMainLogin;
    public GameObject panelOptions;
    public GameObject panelEmailLogin;
    public GameObject panelNotification;

    // Các Panel UI mới được thêm
    public GameObject panelRegister;
    public GameObject panelForgotPassword;

    void Start()
    {
        ShowMainMenu();
    }

    // Hàm chung để ẩn tất cả các panel (giúp code sạch hơn)
    private void HideAllPanels()
    {
        panelMainLogin.SetActive(false);
        panelOptions.SetActive(false);
        panelEmailLogin.SetActive(false);
        panelNotification.SetActive(false); // Panel thông báo thường được quản lý bởi FirebaseAuthManager
        panelRegister.SetActive(false);
        panelForgotPassword.SetActive(false);
    }

    public void ShowMainMenu()
    {
        HideAllPanels();
        panelMainLogin.SetActive(true);
    }

    public void ShowLoginOptions()
    {
        HideAllPanels();
        panelOptions.SetActive(true);
    }

    // Hiển thị Panel Đăng nhập Email (dùng cho nút 'Xác nhận' Đăng nhập)
    public void ShowEmailLogin()
    {
        HideAllPanels();
        panelEmailLogin.SetActive(true);
    }

    // Hàm mới: Hiển thị Panel Đăng ký
    public void ShowRegister()
    {
        HideAllPanels();
        panelRegister.SetActive(true);
    }

    // Hàm mới: Hiển thị Panel Quên mật khẩu
    public void ShowForgotPassword()
    {
        HideAllPanels();
        panelForgotPassword.SetActive(true);
    }
}
