using UnityEngine;

public class CardMove : MonoBehaviour
{   
    //bool Xoay = false;
    //void Update()
    //{
    //    Move();
    //}
    //void Move()
    //{   
       
    //    GameObject Card = GameObject.Find("Card");
    //    GameObject PlayArea = GameObject.Find("PlayArea");
    //    Card.transform.position = PlayArea.transform.position;
    //    if (!Xoay)
    //    {
    //        Card.transform.Rotate(0f, 120f, 0f);
    //        Xoay = true;
    //    }
    //}
    bool isPlayed = false;
    void OnMouseDown()
    {
        if (!isPlayed)
        {
            GameObject Card = GameObject.Find("Card");
            GameObject PlayArea = GameObject.Find("PlayArea");
            if (Card != null)
            {
                Card.transform.position = PlayArea.transform.position;
                isPlayed = true;
            }
        }
    }
    
}
