using UnityEngine;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("---- Audio Sources ----")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("---- Background Music (BGM) ----")]
    public AudioClip themeMusic;  // Nhạc nền chính (Login/Lobby)
    public AudioClip battleMusic; // Nhạc trong trận

    [Header("---- Special SFX ----")]
    public AudioClip victorySound;
    public AudioClip loseSound;

    private void Awake()
    {
        // Singleton để AudioManager không bị hủy khi chuyển Scene
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Vừa mở game là bật nhạc nền Main Menu liền
        PlayMusic(themeMusic);
    }

    // Hàm phát nhạc nền (Loop)
    public void PlayMusic(AudioClip clip)
    {
        if (musicSource.clip == clip) return; // Nếu đang phát đúng bài đó rồi thì thôi
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    // Hàm phát hiệu ứng âm thanh (Phát một lần)
    public void PlaySFX(AudioClip clip)
    {
        sfxSource.PlayOneShot(clip);
    }

    // Xử lý sau khi kết thúc trận đấu (Thắng/Thua)
    public void PlayEndGameResult(bool isWin)
    {
        StartCoroutine(EndGameRoutine(isWin));
    }

    private IEnumerator EndGameRoutine(bool isWin)
    {
        musicSource.Stop(); // Dừng nhạc trận đấu

        if (isWin) PlaySFX(victorySound);
        else PlaySFX(loseSound);

        // Chờ 5 giây như bạn yêu cầu
        yield return new WaitForSeconds(5f);

        // Quay lại nhạc nền chính
        PlayMusic(themeMusic);
    }
}