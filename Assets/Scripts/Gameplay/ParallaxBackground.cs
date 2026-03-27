using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    [SerializeField] private Vector2 parallaxOffset;
    private Transform camTransform;
    private Vector3 lastCameraPos;
    
    private void Start()
    {
        camTransform = Camera.main.transform;
        lastCameraPos = camTransform.position;
    }

    private void LateUpdate()
    {
        Vector3 delta = camTransform.position - lastCameraPos;
        transform.position += new Vector3(delta.x * parallaxOffset.x, delta.y * parallaxOffset.y, 0f);
        lastCameraPos = camTransform.position;
    }
}
