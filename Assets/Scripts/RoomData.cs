using System.Collections.Generic;

[System.Serializable]
public class RoomData
{
    public string hostName;
    public List<string> players;
    public bool Started = false;

    public RoomData(string hostUsername)
    {
        this.hostName = hostUsername;
        this.players = new List<string> { hostUsername };
    }
}