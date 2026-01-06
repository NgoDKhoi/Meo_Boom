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
    public AudioClip drawCardSound;
    public AudioClip playCardSound;

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

        // KIỂM TRA: Nếu là nhạc Victory thì KHÔNG lặp, các nhạc khác thì CÓ lặp
        if (clip == victoryMusic)
        {
            musicSource.loop = false;
        }
        else
        {
            musicSource.loop = true;
        }

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