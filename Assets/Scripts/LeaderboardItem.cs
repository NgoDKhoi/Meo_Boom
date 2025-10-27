using UnityEngine;
using TMPro; 

public class LeaderboardItem : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI Text_Rank;

    [SerializeField]
    private TextMeshProUGUI Text_Username;

    [SerializeField]
    private TextMeshProUGUI Text_Score;

    public void SetData(int rank, string username, int score)
    {

        if (Text_Rank == null || Text_Username == null || Text_Score == null)
        {
            Debug.LogError("LỖI CẤU HÌNH NGHIÊM TRỌNG: Text Component bị hủy trong Prefab! Vui lòng tạo lại các Text con.");
            return;
        }

        if (rank == 0 && username == ".........")
            Text_Rank.text = "";
        else
            Text_Rank.text = rank.ToString();

        Text_Username.text = username;

        if (score == 0 && username == ".........")
            Text_Score.text = "";
        else
            Text_Score.text = score.ToString();
    }
}