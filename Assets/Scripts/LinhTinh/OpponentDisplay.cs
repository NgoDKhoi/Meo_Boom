using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OpponentDisplay : MonoBehaviour
{
    public TextMeshProUGUI userNameText;      // Kéo UI txt_UserName vào đây
    public TextMeshProUGUI cardCountText; // Kéo UI txt_CardCount vào đây

    // Hàm này sẽ được PackManager gọi khi số bài thay đổi
    public void UpdateDisplay(string name, int count, bool isDead)
    {
        userNameText.text = name;

        if (isDead)
        {
            cardCountText.text = "ĐÃ NỔ";
            cardCountText.color = Color.red;
        }
        else
        {
            cardCountText.text = $"Đang có {count} lá";
            cardCountText.color = Color.white;
        }
    }
}
