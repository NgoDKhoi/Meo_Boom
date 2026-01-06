using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("---- Audio Sources ----")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("---- Audio Clips ----")]
    public AudioClip themeMusic;
    public AudioClip battleMusic;
    public AudioClip victoryMusic; // Nhạc ăn mừng chiến thắng
    public AudioClip buttonClick;

    private void Awake()
    {
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
        PlayMusic(themeMusic);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.loop = true; // Đảm bảo luôn lặp lại (Yêu cầu 1 của bạn)
        musicSource.Play();
    }

    // Hàm mới để dừng nhạc nền khi cần thiết
    public void StopMusic()
    {
        musicSource.Stop();
    }

    public void PlaySFX(AudioClip clip)
    {
        sfxSource.PlayOneShot(clip);
    }
}