using UnityEngine;

/// <summary>
/// ScoreManager tracks all scoring: distance-based, trick-based, and collectibles.
/// Singleton pattern for global access. Emits events for UI updates.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Distance Scoring")]
    [SerializeField] private float distanceScoreMultiplier = 0.5f;
    [SerializeField] private float distanceMilestoneInterval = 100f; // every N meters

    [Header("High Score")]
    [SerializeField] private string highScoreKey = "SnowSurfer_HighScore";

    // Score tracking
    private float distanceScore;
    private int trickScore;
    private int collectibleScore;
    private Transform playerTransform;
    private float startX;
    private int lastMilestone;

    // Public API
    public int TotalScore => Mathf.RoundToInt(distanceScore) + trickScore + collectibleScore;
    public int TrickScore => trickScore;
    public float DistanceScore => distanceScore;
    public int CollectibleScore => collectibleScore;
    public float DistanceTraveled => playerTransform != null ? playerTransform.position.x - startX : 0f;
    public int HighScore => PlayerPrefs.GetInt(highScoreKey, 0);

    // Events
    public System.Action<int> OnScoreChanged;
    public System.Action<TrickSystem.TrickResult> OnTrickScored;
    public System.Action<int> OnCollectibleCollected; // points
    public System.Action<int> OnMilestoneReached; // meters

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        SnowboarderController player = FindFirstObjectByType<SnowboarderController>();
        if (player != null)
        {
            playerTransform = player.transform;
            startX = playerTransform.position.x;

            TrickSystem tricks = player.GetComponent<TrickSystem>();
            if (tricks != null)
                tricks.OnTrickCompleted += OnTrickCompleted;

            // Listen for game over to save high score
            player.OnDied += SaveHighScore;
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
            return;

        if (playerTransform != null)
        {
            float traveled = playerTransform.position.x - startX;
            distanceScore = Mathf.Max(0, traveled * distanceScoreMultiplier);
            OnScoreChanged?.Invoke(TotalScore);

            // Check distance milestones
            int currentMilestone = Mathf.FloorToInt(traveled / distanceMilestoneInterval);
            if (currentMilestone > lastMilestone)
            {
                lastMilestone = currentMilestone;
                OnMilestoneReached?.Invoke(currentMilestone * (int)distanceMilestoneInterval);
            }
        }
    }

    private void OnTrickCompleted(TrickSystem.TrickResult result)
    {
        trickScore += result.Points;
        OnTrickScored?.Invoke(result);
        OnScoreChanged?.Invoke(TotalScore);
    }

    /// <summary>
    /// Add points from collectible pickups (called by Collectible component).
    /// </summary>
    public void AddCollectiblePoints(int points)
    {
        collectibleScore += points;
        OnCollectibleCollected?.Invoke(points);
        OnScoreChanged?.Invoke(TotalScore);
    }

    /// <summary>
    /// Save high score to PlayerPrefs on game over.
    /// </summary>
    private void SaveHighScore()
    {
        int current = TotalScore;
        int saved = PlayerPrefs.GetInt(highScoreKey, 0);
        if (current > saved)
        {
            PlayerPrefs.SetInt(highScoreKey, current);
            PlayerPrefs.Save();
        }
    }
}
