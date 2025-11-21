// File: CreateRoomID.cs (hoặc CreateIDRoom.cs)
using System.Text;
using System;

namespace GameUtilities
{
    public static class CreateRoomID
    {
        public static string GenerateRoomID()
        {
            System.Random random = new System.Random();
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < 6; i++)
            {
                sb.Append(random.Next(0, 10).ToString());
            }

            return sb.ToString();
        }
    }
}