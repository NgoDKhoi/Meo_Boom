using UnityEngine;
using UnityEngine.SceneManagement;
public class CodeMenu : MonoBehaviour
{
    public void useButtonPlayGame()
    {
        SceneManager.LoadScene("LoadRoomScene");
    }
    public void useButtonQuitGame() {
        Application.Quit();
    }
}
