using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Ham nay se duoc goi boi Button 'Danh Bai'
    public void PlaySelectedCard()
    {
        // Kiem tra xem co la bai nao dang duoc chon khong
        if (Card_Controller.selectedCard != null)
        {
            // Neu co, ra lenh cho la bai do tu "danh ra"
            Card_Controller.selectedCard.PlayCard();

            // Sau khi danh bai, dat lai de khong con la bai nao duoc chon
            Card_Controller.selectedCard = null;
        }
        else
        {
            // In ra console de biet rang chua co la bai nao duoc chon (tuy chon)
            Debug.Log("Chua chon la bai nao de danh!");
        }
    }
}
