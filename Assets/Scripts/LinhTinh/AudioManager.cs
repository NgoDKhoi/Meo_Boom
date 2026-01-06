using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("---- Audio Sources ----")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("---- Audio Clips ----")]
    public AudioClip themeMusic;  // Dành cho Login, Lobby
    public AudioClip battleMusic; // Dành cho trận đấu
    public AudioClip buttonClick; // Tiếng click nút

    private void Awake()
    {
        // QUAN TRỌNG: Giữ AudioManager tồn tại qua mọi Scene
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Vừa vào game là phát nhạc Theme ngay
        PlayMusic(themeMusic);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource.clip == clip) return; // Nếu đang phát bài này rồi thì không phát lại
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        sfxSource.PlayOneShot(clip);
    }
}