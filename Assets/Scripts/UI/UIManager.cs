using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UIManager bridges the game systems to the UI Toolkit HUD.
/// Displays score, distance, speed, trick names, combo counter, and game over screen.
/// Uses USS transitions for smooth animated feedback.
/// </summary>
public class UIManager : MonoBehaviour
{
    private UIDocument uiDocument;

    // HUD elements
    private Label scoreLabel;
    private Label distanceLabel;
    private Label trickLabel;
    private Label comboLabel;
    private Label trickNameLabel;
    private VisualElement speedBarFill;
    private Label messagePanel;     // Added for Powerup

    // Audio & Mute
    private Button muteButton;

    // Game Over elements
    private VisualElement gameOverPanel;
    private Label gameOverTitle;
    private Label finalScoreLabel;
    private Label highScoreLabel;
    private Button restartButton;

    // Menu elements
    private VisualElement mainMenuPanel;
    private VisualElement charSelectPanel;
    private Button playButton;
    private Button btnFrog;
    private Button btnDino;
    private Button btnMage;

    // Animation state
    private float trickDisplayTimer;
    private float comboDisplayTimer;
    private float messageDisplayTimer;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        // Query HUD elements
        scoreLabel = root.Q<Label>("score-label");
        distanceLabel = root.Q<Label>("distance-label");
        trickLabel = root.Q<Label>("trick-label");
        comboLabel = root.Q<Label>("combo-label");
        trickNameLabel = root.Q<Label>("trick-name-label");
        speedBarFill = root.Q<VisualElement>("speed-bar-fill");
        messagePanel = root.Q<Label>("message-panel");

        // Query Game Over elements
        gameOverPanel = root.Q<VisualElement>("game-over-panel");
        gameOverTitle = root.Q<Label>("game-over-title");
        finalScoreLabel = root.Q<Label>("final-score-label");
        highScoreLabel = root.Q<Label>("high-score-label");
        restartButton = root.Q<Button>("restart-button");

        // Query Menu elements
        mainMenuPanel = root.Q<VisualElement>("main-menu-panel");
        charSelectPanel = root.Q<VisualElement>("char-select-panel");
        playButton = root.Q<Button>("play-button");
        btnFrog = root.Q<Button>("btn-frog");
        btnDino = root.Q<Button>("btn-dino");
        btnMage = root.Q<Button>("btn-mage");
        
        // Query Audio
        muteButton = root.Q<Button>("mute-button");

        // Initialize visibility
        if (gameOverPanel != null) gameOverPanel.style.display = DisplayStyle.None;
        if (trickLabel != null) trickLabel.style.opacity = 0;
        if (comboLabel != null) comboLabel.style.opacity = 0;
        if (trickNameLabel != null) trickNameLabel.style.opacity = 0;
        if (messagePanel != null) messagePanel.style.opacity = 0;

        // Ensure Menu Panel visibility based on state
        if (mainMenuPanel != null) mainMenuPanel.style.display = DisplayStyle.Flex;
        if (charSelectPanel != null) charSelectPanel.style.display = DisplayStyle.None;

        if (restartButton != null) restartButton.clicked += OnRestartClicked;
        if (playButton != null) playButton.clicked += OnPlayClicked;
        if (btnFrog != null) { btnFrog.clicked += () => OnCharacterSelected("Snowboarder Frog"); }
        if (btnDino != null) { btnDino.clicked += () => OnCharacterSelected("Snowboarding Dino"); }
        if (btnMage != null) { btnMage.clicked += () => OnCharacterSelected("MagaSnowboard"); }
        if (muteButton != null) { muteButton.clicked += OnMuteClicked; }
    }

    private void Start()
    {
        // Subscribe to game events
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged += UpdateScore;
            ScoreManager.Instance.OnTrickScored += ShowTrickPopup;
            ScoreManager.Instance.OnCollectibleCollected += ShowCollectiblePopup;
            ScoreManager.Instance.OnMilestoneReached += ShowMilestone;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += ShowGameOver;
            GameManager.Instance.OnGameWon += ShowWin;
            
            // Check if not coming from a fresh start (e.g. restart)
            if (GameManager.Instance.CurrentState != GameManager.GameState.MainMenu)
            {
                if (mainMenuPanel != null) mainMenuPanel.style.display = DisplayStyle.None;
                if (charSelectPanel != null) charSelectPanel.style.display = DisplayStyle.None;
            }
        }

        // Subscribe to player speed for speed bar
        SnowboarderController player = FindFirstObjectByType<SnowboarderController>();
        if (player != null)
        {
            player.OnSpeedChanged += UpdateSpeedBar;
        }
    }

    private void OnPlayClicked()
    {
        if (mainMenuPanel != null) mainMenuPanel.style.display = DisplayStyle.None;
        if (charSelectPanel != null) charSelectPanel.style.display = DisplayStyle.Flex;
    }

    private void OnCharacterSelected(string spriteName)
    {
        if (charSelectPanel != null) charSelectPanel.style.display = DisplayStyle.None;

        // Load sprite from Resources and assign to player
        Sprite characterSprite = Resources.Load<Sprite>(spriteName);
        if (characterSprite != null)
        {
            SnowboarderController player = FindFirstObjectByType<SnowboarderController>();
            if (player != null)
            {
                SpriteRenderer sr = player.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = characterSprite;
            }
        }
        else
        {
            Debug.LogWarning("Character sprite not found in Resources: " + spriteName);
        }

        // Tell Game Manager to start the game
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGameFromMenu();
        }
    }

    private void OnMuteClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.ToggleMute();
            if (muteButton != null)
            {
                muteButton.text = AudioManager.Instance.IsMuted ? "🔇" : "🔊";
            }
        }
    }

    public void ShowHUDMessage(string message, float duration = 2f)
    {
        if (messagePanel != null)
        {
            messagePanel.text = message;
            messagePanel.style.opacity = 1;
            messageDisplayTimer = duration;

            // Flash effect
            messagePanel.AddToClassList("pop-animation");
            messagePanel.schedule.Execute(() => messagePanel.RemoveFromClassList("pop-animation")).ExecuteLater(300);
        }
    }

    private void Update()
    {
        // Hide message panel timer
        if (messageDisplayTimer > 0)
        {
            messageDisplayTimer -= Time.unscaledDeltaTime; // Use unscaled incase TimeScale is 0
            if (messageDisplayTimer <= 0 && messagePanel != null)
            {
                messagePanel.style.opacity = 0;
            }
        }

        // Update distance display
        if (distanceLabel != null && ScoreManager.Instance != null)
        {
            float dist = ScoreManager.Instance.DistanceTraveled;
            TerrainGenerator terrain = FindFirstObjectByType<TerrainGenerator>();
            if (terrain != null)
            {
                float progress = Mathf.Clamp01(dist / terrain.TrackLength) * 100f;
                distanceLabel.text = $"{dist:F0}m ({progress:F0}%)";
            }
            else
            {
                distanceLabel.text = $"{dist:F0}m";
            }
        }

        // Trick popup timer
        if (trickDisplayTimer > 0)
        {
            trickDisplayTimer -= Time.deltaTime;
            if (trickDisplayTimer <= 0)
            {
                if (trickLabel != null) trickLabel.style.opacity = 0;
                if (trickNameLabel != null) trickNameLabel.style.opacity = 0;
            }
        }

        // Combo display timer
        if (comboDisplayTimer > 0)
        {
            comboDisplayTimer -= Time.deltaTime;
            if (comboDisplayTimer <= 0 && comboLabel != null)
                comboLabel.style.opacity = 0;
        }

        // Show active trick name while in air
        TrickSystem tricks = FindFirstObjectByType<TrickSystem>();
        if (tricks != null && trickNameLabel != null)
        {
            string name = tricks.CurrentTrickName;
            if (!string.IsNullOrEmpty(name) && trickDisplayTimer <= 0)
            {
                trickNameLabel.text = name;
                trickNameLabel.style.opacity = 1;
            }
        }
    }

    private void UpdateScore(int score)
    {
        if (scoreLabel != null)
            scoreLabel.text = score.ToString("N0");
    }

    private void UpdateSpeedBar(float normalizedSpeed)
    {
        if (speedBarFill != null)
        {
            speedBarFill.style.width = Length.Percent(normalizedSpeed * 100f);

            // Color the bar from blue (slow) to red (fast)
            Color barColor = Color.Lerp(
                new Color(0.3f, 0.7f, 1f),
                new Color(1f, 0.3f, 0.2f),
                normalizedSpeed
            );
            speedBarFill.style.backgroundColor = barColor;
        }
    }

    private void ShowTrickPopup(TrickSystem.TrickResult result)
    {
        if (trickLabel != null)
        {
            string comboText = result.Combo > 1 ? $" x{result.Combo}" : "";
            trickLabel.text = $"+{result.Points}{comboText}";
            trickLabel.style.opacity = 1;
            
            // Pop animation
            trickLabel.AddToClassList("pop-animation");
            trickLabel.schedule.Execute(() => trickLabel.RemoveFromClassList("pop-animation")).ExecuteLater(300);
        }

        if (trickNameLabel != null)
        {
            trickNameLabel.text = result.TrickName;
            trickNameLabel.style.opacity = 1;
            
            // Pop animation
            trickNameLabel.AddToClassList("pop-animation");
            trickNameLabel.schedule.Execute(() => trickNameLabel.RemoveFromClassList("pop-animation")).ExecuteLater(300);
        }

        if (comboLabel != null && result.Combo > 1)
        {
            comboLabel.text = $"COMBO x{result.Combo}!";
            comboLabel.style.opacity = 1;
            comboDisplayTimer = 2.5f;
            
            comboLabel.AddToClassList("pop-animation");
            comboLabel.schedule.Execute(() => comboLabel.RemoveFromClassList("pop-animation")).ExecuteLater(300);
        }

        // Also flash main score
        if (scoreLabel != null)
        {
            scoreLabel.AddToClassList("score-flash");
            scoreLabel.schedule.Execute(() => scoreLabel.RemoveFromClassList("score-flash")).ExecuteLater(300);
        }

        trickDisplayTimer = 2.5f;
    }

    private void ShowCollectiblePopup(int points)
    {
        // Brief flash on the score label for collectible feedback
        if (scoreLabel != null)
        {
            scoreLabel.AddToClassList("score-flash");
            scoreLabel.schedule.Execute(() => scoreLabel.RemoveFromClassList("score-flash")).ExecuteLater(300);
        }
    }

    private void ShowMilestone(int meters)
    {
        if (trickLabel != null)
        {
            trickLabel.text = $"{meters}m!";
            trickLabel.style.opacity = 1;
            trickDisplayTimer = 2f;
        }
    }

    private void ShowGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.style.display = DisplayStyle.Flex;

        if (finalScoreLabel != null && ScoreManager.Instance != null)
            finalScoreLabel.text = $"SCORE: {ScoreManager.Instance.TotalScore:N0}";

        if (highScoreLabel != null && ScoreManager.Instance != null)
        {
            int high = ScoreManager.Instance.HighScore;
            bool isNew = ScoreManager.Instance.TotalScore >= high;
            highScoreLabel.text = isNew ? "NEW HIGH SCORE!" : $"BEST: {high:N0}";
        }
    }

    private void ShowWin()
    {
        if (gameOverPanel != null)
            gameOverPanel.style.display = DisplayStyle.Flex;

        if (gameOverTitle != null)
            gameOverTitle.text = "\u2B50 YOU WIN! \u2B50";

        if (finalScoreLabel != null && ScoreManager.Instance != null)
            finalScoreLabel.text = $"FINAL SCORE: {ScoreManager.Instance.TotalScore:N0}";

        if (highScoreLabel != null)
            highScoreLabel.text = "Press R to restart";
    }

    private void OnRestartClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RestartGame();
    }
}
