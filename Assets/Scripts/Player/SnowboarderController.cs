using UnityEngine;

/// <summary>
/// Core snowboarder physics controller.
/// - Single jump only (no infinite jumping)
/// - Auto-rotates forward in the air (visible frontflip)
/// - Landing at bad angle = crash
/// - Supports speed boost powerup
/// - Decelerates to stop after finish line
/// </summary>
public class SnowboarderController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float baseSpeed = 40f;
    [SerializeField] private float maxSpeed = 95f;
    [SerializeField] private float slopeBoostFactor = 15.0f;
    [SerializeField] private float speedSmoothTime = 0.5f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 18f;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer = 1;

    [Header("Air Rotation")]
    [SerializeField] private float autoRotationSpeed = 650f; // degrees/sec flip in air

    [Header("Landing")]
    [SerializeField] private float crashAngleThreshold = 60f;
    [SerializeField] private float airTimeBeforeCrashCheck = 0.15f;

    [Header("Visuals (DOTween)")]
    [SerializeField] private float squashAmount = 0.3f;   // 30% deformation
    [SerializeField] private float tweenDuration = 0.15f;

    // Internal state
    private Rigidbody2D rb;
    private bool isGrounded;
    private bool isAlive = true;
    private float currentSpeed;
    private float speedVelocity;
    private Vector3 originalScale;
    private Coroutine activeTween;
    private float distanceTraveled;
    private float startX;
    private float airTimer;
    private bool hasJumped;
    private float jumpGraceTimer;
    private float speedBoostTimer;
    private float speedBoostMultiplier = 1f;
    private bool isDecelerating; // after finish line
    
    // Target rotation state
    private float targetAirRotation = 0f;
    private float currentAirRotation = 0f;

    private const float JUMP_GRACE_TIME = 0.35f;

    // Public API
    public bool IsGrounded => isGrounded;
    public bool IsAlive => isAlive;
    public float CurrentSpeed => currentSpeed;
    public float NormalizedSpeed => Mathf.InverseLerp(0, maxSpeed, currentSpeed);
    public float DistanceTraveled => distanceTraveled;
    public bool HasSpeedBoost => speedBoostTimer > 0f;

    // Events
    public System.Action OnJumped;
    public System.Action OnLanded;
    public System.Action OnDied;
    public System.Action<float> OnSpeedChanged;

    private Collider2D basePhysicsCollider;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalScale = transform.localScale;
        startX = transform.position.x;

        if (GetComponent<SnowEffects>() == null)
        {
            gameObject.AddComponent<SnowEffects>();
        }

        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Note: The main physics collider is left at its original size (radius ~0.4) 
        // to prevent high-speed tunneling through the EdgeCollider2D terrain.
        // It does NOT trigger death upon hitting trees/rocks anymore, so a larger size is perfectly safe.

        // Create the "Hitbox" specifically for Obstacles and Powerups
        GameObject headObj = new GameObject("HeadHitbox");
        headObj.transform.parent = transform;
        headObj.transform.localPosition = new Vector3(0f, 0.2f, 0f); // Center of body
        
        CircleCollider2D headCol = headObj.AddComponent<CircleCollider2D>();
        headCol.isTrigger = true;
        headCol.radius = 0.6f; // Large enough to cover the snowboarder and easily grab powerups

        PlayerHitbox hitbox = headObj.AddComponent<PlayerHitbox>();
        hitbox.controller = this;
    }

    private void Update()
    {
        if (!isAlive) return;

        distanceTraveled = transform.position.x - startX;

        // Speed Boost decay
        if (speedBoostTimer > 0f)
        {
            speedBoostTimer -= Time.deltaTime;
            if (speedBoostTimer <= 0f)
            {
                speedBoostMultiplier = 1f;
            }
        }

        // Target speed calculation based on slope
        float targetSpeed = baseSpeed;
        if (isGrounded && !isDecelerating)
        {
            float zRot = transform.eulerAngles.z;
            if (zRot > 180f) zRot -= 360f;
            
            // Speed up on downhill (-Z rotation), slow down on uphill (+Z)
            if (zRot < 0)
            {
                targetSpeed += Mathf.Abs(zRot) * 0.1f * slopeBoostFactor;
            }
            else if (zRot > 0)
            {
                targetSpeed -= zRot * 0.1f;
            }
        }

        if (isDecelerating)
        {
            targetSpeed = 0f;
            speedSmoothTime = 1.5f; // Slower stop
        }

        // Apply speed limits
        targetSpeed = Mathf.Clamp(targetSpeed, 0f, maxSpeed);
        targetSpeed *= speedBoostMultiplier;

        // Smoothly adjust current speed
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime / speedSmoothTime);
        OnSpeedChanged?.Invoke(currentSpeed / (maxSpeed * 1.6f));

        // Horizontal velocity
        rb.linearVelocity = new Vector2(currentSpeed, rb.linearVelocity.y);

        // Ground check
        UpdateGroundCheck();

        // Jump & Mid-Air Flip input
        if (GetJumpInput())
        {
            if (isGrounded && !hasJumped && !isDecelerating)
            {
                PerformJump();
                targetAirRotation -= 360f; // 1 full flip
            }
            else if (!isGrounded && hasJumped && jumpGraceTimer <= 0f)
            {
                targetAirRotation -= 360f; // Add another flip mid-air!
            }
        }

        // Air behavior: auto-rotate ONLY if intentionally jumped
        if (!isGrounded)
        {
            airTimer += Time.deltaTime;
            
            if (hasJumped)
            {
                // Smoothly rotate towards the target flips
                currentAirRotation = Mathf.MoveTowards(currentAirRotation, targetAirRotation, autoRotationSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0, 0, currentAirRotation);
            }
        }
        else
        {
            // Leaning system when grounded
            Vector2 checkPos = (Vector2)transform.position + Vector2.down * 0.55f;
            RaycastHit2D hit = Physics2D.Raycast(checkPos, Vector2.down, 1.0f, groundLayer);
            if (hit.collider != null)
            {
                // Align visual rotation to the ground normal
                float targetAngle = Mathf.Atan2(hit.normal.x, hit.normal.y) * -Mathf.Rad2Deg;
                
                // Prevent micro-rotations and jittering on flat terrain by snapping
                if (Mathf.Abs(targetAngle) < 1.5f)
                {
                    targetAngle = 0f;
                }

                if (Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.z, targetAngle)) > 0.1f)
                {
                    transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, 0, targetAngle), Time.deltaTime * 15f);
                }
                else
                {
                    transform.rotation = Quaternion.Euler(0, 0, targetAngle);
                }
            }
            else
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, 0, 0), Time.deltaTime * 10f);
            }
            
            // Reset rotation targets for the next jump from current slope rotation
            float currentAngle = transform.eulerAngles.z;
            if (currentAngle > 180f) currentAngle -= 360f;
            currentAirRotation = currentAngle;
            targetAirRotation = currentAngle;
        }

        // Jump grace timer
        if (jumpGraceTimer > 0f)
            jumpGraceTimer -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (!isAlive) return;

        if (isDecelerating)
        {
            // Decelerate to stop after finish
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, 4f * Time.fixedDeltaTime);
            Vector2 vel = rb.linearVelocity;
            vel.x = currentSpeed;
            rb.linearVelocity = vel;
            OnSpeedChanged?.Invoke(NormalizedSpeed);
            return;
        }

        float speedModifier = 1f;
        if (isGrounded)
        {
            float yVel = rb.linearVelocity.y;
            speedModifier = 1f + (-yVel / 10f) * slopeBoostFactor;
        }

        float effectiveMaxSpeed = maxSpeed * speedBoostMultiplier;
        float effectiveBaseSpeed = baseSpeed * speedBoostMultiplier;
        float targetSpeed = Mathf.Clamp(effectiveBaseSpeed * speedModifier, effectiveBaseSpeed * 0.4f, effectiveMaxSpeed);
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVelocity, speedSmoothTime);

        Vector2 vel2 = rb.linearVelocity;
        vel2.x = currentSpeed;
        rb.linearVelocity = vel2;

        OnSpeedChanged?.Invoke(NormalizedSpeed);
    }

    private void UpdateGroundCheck()
    {
        // SKIP ground check during jump grace period
        if (jumpGraceTimer > 0f)
            return;

        Vector2 checkPos = (Vector2)transform.position + Vector2.down * 0.55f;
        bool wasGrounded = isGrounded;
        
        isGrounded = false;
        Collider2D[] hits = Physics2D.OverlapCircleAll(checkPos, groundCheckRadius, groundLayer);
        foreach (var hit in hits)
        {
            if (hit.transform.root != transform.root && !hit.isTrigger)
            {
                isGrounded = true;
                break;
            }
        }

        // LANDING (was in air, now touching ground)
        if (!wasGrounded && isGrounded)
        {
            // Crash check: only after significant air time
            if (airTimer >= airTimeBeforeCrashCheck)
            {
                float landingAngle = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.z, 0f));
                if (landingAngle > crashAngleThreshold)
                {
                    Die();
                    return;
                }
            }

            // Successful landing!
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.angularVelocity = 0f;
            hasJumped = false;
            airTimer = 0f;

            // Native Landing Squash (flatten/widen)
            if (activeTween != null) StopCoroutine(activeTween);
            transform.localScale = originalScale;
            activeTween = StartCoroutine(AnimateScale(new Vector3(
                originalScale.x * (1f + squashAmount), 
                originalScale.y * (1f - squashAmount), 
                originalScale.z
            ), tweenDuration));

            OnLanded?.Invoke();
        }

        // LEAVING GROUND (was grounded, now in air — going off a hill edge)
        if (wasGrounded && !isGrounded)
        {
            rb.constraints = RigidbodyConstraints2D.None;
            airTimer = 0f;
        }
    }

    private void PerformJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        isGrounded = false;
        hasJumped = true;

        // Unlock rotation for air tricks
        rb.constraints = RigidbodyConstraints2D.None;
        rb.angularVelocity = 0f;
        airTimer = 0f;

        // Native Jump Stretch (thin/tall)
        if (activeTween != null) StopCoroutine(activeTween);
        transform.localScale = originalScale;
        activeTween = StartCoroutine(AnimateScale(new Vector3(
            originalScale.x * (1f - squashAmount * 0.5f), 
            originalScale.y * (1f + squashAmount), 
            originalScale.z
        ), tweenDuration));

        // Grace period prevents instant re-grounding
        jumpGraceTimer = JUMP_GRACE_TIME;

        OnJumped?.Invoke();
    }

    public void Die()
    {
        if (!isAlive) return;
        isAlive = false;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Static;
        OnDied?.Invoke();
    }

    /// <summary>
    /// Start decelerating after finish line.
    /// </summary>
    public void StartDeceleration()
    {
        isDecelerating = true;
    }

    /// <summary>
    /// Apply a temporary speed boost (from powerup).
    /// </summary>
    public void ApplySpeedBoost(float multiplier, float duration)
    {
        speedBoostMultiplier = multiplier;
        speedBoostTimer = duration;
        currentSpeed = Mathf.Min(currentSpeed * multiplier, maxSpeed * multiplier);
    }

    // Handled by the PlayerHitbox script now, EXCEPT for cliff fall zones.
    // If the base collider drops into a cliff gap, it should count as an instant death.
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only KillZones trigger from the base collider to instantly detect pit falls. 
        // Trees/Rocks only trigger from the Head Hitbox.
        if (other.name.Contains("KillZone"))
        {
            Die();
        }
    }

    private bool GetJumpInput()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;
        return kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame;
    }

    private System.Collections.IEnumerator AnimateScale(Vector3 targetScale, float halfDuration)
    {
        float t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            // Ease Out Quad approximation (smooth fast start, slow down at end)
            float step = 1f - Mathf.Pow(1f - (t / halfDuration), 2f);
            transform.localScale = Vector3.Lerp(originalScale, targetScale, step);
            yield return null;
        }

        t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            // Smoothly return
            transform.localScale = Vector3.Lerp(targetScale, originalScale, t / halfDuration);
            yield return null;
        }
        
        transform.localScale = originalScale;
        activeTween = null;
    }
}

/// <summary>
/// Attached to the head hitbox to specifically catch upper-body collisions 
/// and powerups without snapping the base of the snowboard.
/// </summary>
public class PlayerHitbox : MonoBehaviour
{
    public SnowboarderController controller;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Obstacle"))
        {
            controller.Die();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            controller.Die();
        }
    }
}
