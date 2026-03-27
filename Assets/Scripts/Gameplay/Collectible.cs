using UnityEngine;

/// <summary>
/// Collectible pickup behavior. Destroys itself when the player touches it
/// and notifies the ScoreManager to award bonus points.
/// Includes a floating bob animation and spin for visual appeal.
/// </summary>
public class Collectible : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private float bobSpeed = 3f;
    [SerializeField] private float bobHeight = 0.3f;
    [SerializeField] private float spinSpeed = 180f;

    [Header("Points")]
    [SerializeField] private int pointValue = 25;

    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        // Floating bob animation
        float yOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = startPosition + Vector3.up * yOffset;

        // Spin animation
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if player touched the collectible (either root or HeadHitbox)
        SnowboarderController player = other.GetComponentInParent<SnowboarderController>();
        if (player != null)
        {
            // Award points through ScoreManager
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.AddCollectiblePoints(pointValue);

            // Destroy with a brief delay (allows VFX if added later)
            Destroy(gameObject);
        }
    }
}
