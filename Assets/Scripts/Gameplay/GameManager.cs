using UnityEngine;

/// <summary>
/// Enhanced GameManager with game states: Countdown → Playing → Won / GameOver.
/// Supports finish line victory condition via TerrainGenerator event.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { MainMenu, Countdown, Playing, Won, GameOver }
    public GameState CurrentState { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private float countdownDuration = 0f;
    [SerializeField] private float gameOverDelay = 1.5f;

    // Events for UI and other systems
    public System.Action OnGameStarted;
    public System.Action OnGameOver;
    public System.Action OnGameWon;
    public System.Action OnGameRestart;
    public System.Action<int> OnCountdownTick;

    private SnowboarderController player;
    private TerrainGenerator terrain;
    private float countdownTimer;
    private float gameOverTimer;
    private bool gameOverTriggered;

    private void Awake()
    {
        Application.runInBackground = true;
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        player = FindFirstObjectByType<SnowboarderController>();
        terrain = FindFirstObjectByType<TerrainGenerator>();

        if (player != null)
            player.OnDied += OnPlayerDied;

        if (terrain != null)
            terrain.OnFinishLineReached += OnPlayerFinished;

        // Start in MainMenu state, pause game
        CurrentState = GameState.MainMenu;
        Time.timeScale = 0f; 
    }

    public void StartGameFromMenu()
    {
        Time.timeScale = 1f;
        if (countdownDuration > 0f)
        {
            CurrentState = GameState.Countdown;
            countdownTimer = countdownDuration;
        }
        else
        {
            CurrentState = GameState.Playing;
            OnGameStarted?.Invoke();
        }
    }

    private void Update()
    {
        switch (CurrentState)
        {
            case GameState.Countdown:
                UpdateCountdown();
                break;
            case GameState.GameOver:
                if (gameOverTriggered)
                {
                    gameOverTimer -= Time.deltaTime;
                    if (gameOverTimer <= 0f)
                    {
                        gameOverTriggered = false;
                        OnGameOver?.Invoke();
                    }
                }
                break;
        }

        // Restart on R key press in GameOver or Won states
        if (CurrentState == GameState.GameOver || CurrentState == GameState.Won)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                RestartGame();
            }
        }
    }

    private void UpdateCountdown()
    {
        countdownTimer -= Time.deltaTime;
        int secondsLeft = Mathf.CeilToInt(countdownTimer);
        OnCountdownTick?.Invoke(secondsLeft);

        if (countdownTimer <= 0f)
        {
            CurrentState = GameState.Playing;
            OnGameStarted?.Invoke();
        }
    }

    private void OnPlayerDied()
    {
        if (CurrentState == GameState.GameOver || CurrentState == GameState.Won) return;
        CurrentState = GameState.GameOver;
        gameOverTriggered = true;
        gameOverTimer = gameOverDelay;
    }

    private void OnPlayerFinished()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState = GameState.Won;

        // Decelerate the player smoothly to a stop on the flat post-finish terrain
        if (player != null)
        {
            player.StartDeceleration();
        }

        OnGameWon?.Invoke();
    }

    /// <summary>
    /// Restart the game by reloading the active scene.
    /// </summary>
    public void RestartGame()
    {
        OnGameRestart?.Invoke();
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}
