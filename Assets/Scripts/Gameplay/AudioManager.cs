using UnityEngine;

/// <summary>
/// Manages background music volume based on game state and provides global mute toggle.
/// Binds to AudioSource on the same GameObject (e.g. GameManager).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private AudioSource bgmSource;
    private bool isMuted = false;

    private const float MENU_VOLUME = 0.25f;
    private const float PLAYING_VOLUME = 0.6f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        GameObject amObj = new GameObject("AudioManager");
        DontDestroyOnLoad(amObj);
        AudioManager am = amObj.AddComponent<AudioManager>();
        
        AudioSource source = amObj.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = true;
        source.volume = MENU_VOLUME;
        
        AudioClip clip = Resources.Load<AudioClip>("Above_the_Timberline");
        if (clip != null)
        {
            source.clip = clip;
            source.Play();
        }
        else
        {
            Debug.LogWarning("AudioManager: Could not load Above_the_Timberline from Resources.");
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        bgmSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (GameManager.Instance != null)
        {
            // Unsubscribe first to prevent duplicate event triggers on reload
            GameManager.Instance.OnGameStarted -= SetPlayingVolume;
            GameManager.Instance.OnGameOver -= SetMenuVolume;
            GameManager.Instance.OnGameWon -= SetMenuVolume;

            GameManager.Instance.OnGameStarted += SetPlayingVolume;
            GameManager.Instance.OnGameOver += SetMenuVolume;
            GameManager.Instance.OnGameWon += SetMenuVolume;
        }
        
        // Always reset to menu volume when a scene freshly loads (Main Menu or Restart)
        SetMenuVolume();
    }

    private void SetPlayingVolume()
    {
        if (bgmSource != null) bgmSource.volume = PLAYING_VOLUME;
    }

    private void SetMenuVolume()
    {
        if (bgmSource != null) bgmSource.volume = MENU_VOLUME;
    }

    public void ToggleMute()
    {
        isMuted = !isMuted;
        AudioListener.volume = isMuted ? 0f : 1f;
    }

    public bool IsMuted => isMuted;
}
