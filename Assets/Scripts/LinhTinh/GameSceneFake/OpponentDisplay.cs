using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OpponentDisplay : MonoBehaviour
{
    public TextMeshProUGUI userNameText;

    public Transform handArea;


    public void UpdateDisplay(string name, int count, bool isDead)
    {
        userNameText.text = name;

        if (isDead)
        {
            userNameText.text = $"{name} (ĐÃ NỔ)";
            userNameText.color = Color.red;
        }
        else
        {
            userNameText.color = Color.white;
        }
    }
}