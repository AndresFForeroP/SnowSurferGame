using UnityEngine;

/// <summary>
/// Smooth camera follow with dynamic zoom based on player speed.
/// Zoomed out for better visibility of the terrain and obstacles ahead.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    [SerializeField] private Vector3 offset = new Vector3(6f, 4f, -10f);
    [SerializeField] private float smoothSpeed = 5f;

    [Header("Dynamic Zoom")]
    [SerializeField] private bool enableDynamicZoom = true;
    [SerializeField] private float baseOrthoSize = 9f;
    [SerializeField] private float maxOrthoSize = 13f;
    [SerializeField] private float zoomSmoothSpeed = 2f;

    [Header("Look Ahead")]
    [SerializeField] private float lookAheadDistance = 4f;
    [SerializeField] private float lookAheadSmooth = 3f;

    private Transform target;
    private Camera cam;
    private SnowboarderController playerController;
    private bool isFollowing = true;
    private float currentLookAhead;

    private void Start()
    {
        cam = GetComponent<Camera>();
        playerController = FindFirstObjectByType<SnowboarderController>();

        if (playerController != null)
        {
            target = playerController.transform;
            playerController.OnDied += () => isFollowing = false;
        }

        // Set initial zoom
        if (cam != null && cam.orthographic)
            cam.orthographicSize = baseOrthoSize;
    }

    private void LateUpdate()
    {
        if (target == null || !isFollowing) return;

        // Look-ahead based on player speed
        float targetLookAhead = playerController != null ? playerController.NormalizedSpeed * lookAheadDistance : 0f;
        currentLookAhead = Mathf.Lerp(currentLookAhead, targetLookAhead, lookAheadSmooth * Time.deltaTime);

        // Calculate camera position with look-ahead
        Vector3 lookAheadOffset = Vector3.right * currentLookAhead;
        Vector3 desiredPos = target.position + offset + lookAheadOffset;

        // Smooth follow
        Vector3 smoothed = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);
        transform.position = smoothed;

        // Dynamic zoom based on speed
        if (enableDynamicZoom && cam != null && cam.orthographic && playerController != null)
        {
            float targetSize = Mathf.Lerp(baseOrthoSize, maxOrthoSize, playerController.NormalizedSpeed);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, zoomSmoothSpeed * Time.deltaTime);
        }
    }
}
