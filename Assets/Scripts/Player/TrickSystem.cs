using UnityEngine;

/// <summary>
/// Trick detection system that tracks air rotation and awards points.
/// Supports named tricks, combo multipliers, and grab tricks.
/// Designed to be extensible — add new trick types to expand the system.
/// </summary>
public class TrickSystem : MonoBehaviour
{
    [Header("Trick Detection")]
    [SerializeField] private float minRotationForTrick = 180f;
    [SerializeField] private int basePointsPerRotation = 100;

    [Header("Combo")]
    [SerializeField] private float comboTimeWindow = 2.5f;
    [SerializeField] private int maxComboMultiplier = 10;

    [Header("Grab Tricks")]
    [SerializeField] private int grabBonusPoints = 50;
    [SerializeField] private float minGrabTime = 0.3f;

    // Internal state
    private SnowboarderController playerController;
    private float totalAirRotation;
    private float lastAngle;
    private int currentCombo;
    private float comboTimer;
    private bool wasInAir;
    private bool isGrabbing;
    private float grabTimer;
    private int rotationDirection; // +1 frontside, -1 backside
    private float consecutiveRotation; // signed rotation for direction

    // Public API
    public int CurrentCombo => currentCombo;
    public float TotalAirRotation => totalAirRotation;
    public bool IsGrabbing => isGrabbing;
    public string CurrentTrickName => GetTrickName();

    // Events
    public System.Action<TrickResult> OnTrickCompleted;
    public System.Action OnGrabStarted;
    public System.Action OnGrabEnded;

    /// <summary>
    /// Data structure for trick completion results.
    /// </summary>
    [System.Serializable]
    public struct TrickResult
    {
        public string TrickName;
        public int Points;
        public int Combo;
        public int Rotations;
        public bool IncludedGrab;
    }

    private void Awake()
    {
        playerController = GetComponent<SnowboarderController>();
    }

    private void OnEnable()
    {
        if (playerController != null)
            playerController.OnLanded += OnPlayerLanded;
    }

    private void OnDisable()
    {
        if (playerController != null)
            playerController.OnLanded -= OnPlayerLanded;
    }

    private void Update()
    {
        if (playerController == null || !playerController.IsAlive) return;

        // Combo timer decay
        if (comboTimer > 0f)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f)
                currentCombo = 0;
        }

        // Airborne trick tracking
        if (!playerController.IsGrounded)
        {
            TrackRotation();
            HandleGrabInput();
            wasInAir = true;
        }
        else
        {
            if (!wasInAir)
                lastAngle = transform.eulerAngles.z;
            wasInAir = false;

            // End grab on landing
            if (isGrabbing)
            {
                isGrabbing = false;
                OnGrabEnded?.Invoke();
            }
        }
    }

    /// <summary>
    /// Track cumulative rotation while airborne.
    /// Tracks both absolute and directional rotation.
    /// </summary>
    private void TrackRotation()
    {
        float currentAngle = transform.eulerAngles.z;
        float delta = Mathf.DeltaAngle(lastAngle, currentAngle);
        totalAirRotation += Mathf.Abs(delta);
        consecutiveRotation += delta;
        lastAngle = currentAngle;

        // Determine dominant rotation direction
        if (consecutiveRotation > 90f) rotationDirection = 1;
        else if (consecutiveRotation < -90f) rotationDirection = -1;
    }

    /// <summary>
    /// Handle grab trick input (hold S or Down Arrow while airborne).
    /// </summary>
    private void HandleGrabInput()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;

        bool grabPressed = kb.sKey.isPressed || kb.downArrowKey.isPressed;

        if (grabPressed && !isGrabbing)
        {
            isGrabbing = true;
            grabTimer = 0f;
            OnGrabStarted?.Invoke();
        }
        else if (grabPressed && isGrabbing)
        {
            grabTimer += Time.deltaTime;
        }
        else if (!grabPressed && isGrabbing)
        {
            isGrabbing = false;
            OnGrabEnded?.Invoke();
        }
    }

    /// <summary>
    /// Called when the player lands. Evaluates the trick and awards points.
    /// </summary>
    private void OnPlayerLanded()
    {
        if (totalAirRotation >= minRotationForTrick)
        {
            int rotations = Mathf.FloorToInt(totalAirRotation / 360f);
            if (rotations < 1) rotations = 1;

            // Increment combo
            currentCombo = Mathf.Min(currentCombo + 1, maxComboMultiplier);
            comboTimer = comboTimeWindow;

            // Calculate points
            int points = rotations * basePointsPerRotation * currentCombo;
            bool grabbedDuringTrick = grabTimer >= minGrabTime;

            // Bonus for grab tricks
            if (grabbedDuringTrick)
                points += grabBonusPoints * currentCombo;

            // Package result
            TrickResult result = new TrickResult
            {
                TrickName = GetTrickNameForRotations(rotations, grabbedDuringTrick),
                Points = points,
                Combo = currentCombo,
                Rotations = rotations,
                IncludedGrab = grabbedDuringTrick
            };

            OnTrickCompleted?.Invoke(result);
        }

        // Reset for next jump
        totalAirRotation = 0f;
        consecutiveRotation = 0f;
        rotationDirection = 0;
        grabTimer = 0f;
    }

    /// <summary>
    /// Generate a trick name based on rotations and modifiers.
    /// </summary>
    private string GetTrickNameForRotations(int rotations, bool hasGrab)
    {
        string name = rotations switch
        {
            1 => "360",
            2 => "720",
            3 => "1080",
            4 => "1440",
            _ => $"{rotations * 360}"
        };

        string direction = rotationDirection > 0 ? "Frontside" : "Backside";
        string grab = hasGrab ? " Grab" : "";
        return $"{direction} {name}{grab}";
    }

    /// <summary>
    /// Get the active trick name while in the air (for HUD display).
    /// </summary>
    private string GetTrickName()
    {
        if (playerController == null || playerController.IsGrounded || totalAirRotation < 90f)
            return "";

        int rotations = Mathf.FloorToInt(totalAirRotation / 360f);
        if (rotations < 1) return isGrabbing ? "GRAB!" : "SPINNING...";

        return GetTrickNameForRotations(rotations, isGrabbing);
    }
}
