using UnityEngine;
using System.Collections.Generic;

public class GameTableLayout : MonoBehaviour
{
    public GameObject playerUIPrefab;

    [System.Serializable]
    public struct PlayerSpot
    {
        public Vector2 position;
        public float rotation;
    }
    public PlayerSpot[] absoluteSpots;

    void Awake()
    {
        // ViewIndex 0 (Duoi, ben phai) - Client thứ nhat
        PlayerSpot spot0 = new PlayerSpot
        {
            position = new Vector2(700f, -450f),
            rotation = 0f // Không xoay
        };
        // ViewIndex 3 (Top, Bên Trái) - Client thứ ba
        PlayerSpot spot1 = new PlayerSpot
        {
            position = new Vector2(-700f, 500f),
            rotation = 0f
        };

        // ViewIndex 2 (Top, Trung tâm) - Client thứ hai
        PlayerSpot spot2 = new PlayerSpot
        {
            position = new Vector2(0f, 500f),
            rotation = 0f
        };

        // ViewIndex 1 (Top, Bên Phai) - Client thứ nhat
        PlayerSpot spot3 = new PlayerSpot
        {
            position = new Vector2(700f, 500f),
            rotation = 0f
        };

        absoluteSpots = new PlayerSpot[4] { spot0, spot1, spot2, spot3 };
    }
}