using UnityEngine;
using Firebase.Auth;
using UnityEngine.SceneManagement;

public class LogoutManager : MonoBehaviour
{
    private FirebaseAuth auth;

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
    }

    public void OnLogoutButtonClicked()
    {
        auth.SignOut();
        Debug.Log("Đã đăng xuất!");
        // Quay lại scene login
        SceneManager.LoadScene("LoginScene");
    }
}