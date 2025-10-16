using UnityEngine;

public class UI_Manager : MonoBehaviour
{
    public GameObject panelMainLogin;
    public GameObject panelOptions;
    public GameObject panelEmailLogin;

    void Start()
    {
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        panelMainLogin.SetActive(true);
        panelOptions.SetActive(false);
        panelEmailLogin.SetActive(false);
    }

    public void ShowLoginOptions()
    {
        panelMainLogin.SetActive(false);
        panelOptions.SetActive(true);
        panelEmailLogin.SetActive(false);
    }

    public void ShowEmailLogin()
    {
        panelMainLogin.SetActive(false);
        panelOptions.SetActive(false);
        panelEmailLogin.SetActive(true);
    }
}
