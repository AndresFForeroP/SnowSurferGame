using UnityEngine;

/// <summary>
/// Speed boost powerup. Uses the Gift Bag sprite.
/// When player touches it, grants a temporary speed increase.
/// Has a visual bob animation.
/// </summary>
public class SpeedBoost : MonoBehaviour
{
    [Header("Boost Settings")]
    [SerializeField] private float speedMultiplier = 1.6f;
    [SerializeField] private float boostDuration = 4f;

    [Header("Visual")]
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobHeight = 0.3f;

    private Vector3 startPos;
    private bool collected;

    private void Start()
    {
        startPos = transform.position;
    }

    private void Update()
    {
        if (collected) return;

        // Bob up and down
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(startPos.x, newY, startPos.z);

        // Gentle rotation
        transform.Rotate(0, 0, 30f * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;

        SnowboarderController player = other.GetComponentInParent<SnowboarderController>();
        if (player != null)
        {
            collected = true;
            player.ApplySpeedBoost(speedMultiplier, boostDuration);
            
            UIManager ui = FindFirstObjectByType<UIManager>();
            if (ui != null)
            {
                ui.ShowHUDMessage("SPEED BOOST!", 1.5f);
            }

            // Visual feedback: scale up briefly then destroy
            Destroy(gameObject);
        }
    }
}
