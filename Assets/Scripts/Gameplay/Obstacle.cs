using UnityEngine;

public class Obstacle : MonoBehaviour
{
    private void OnCollisionEnter2D(Collision2D collision)
    {
        SnowboarderController player = collision.gameObject.GetComponent<SnowboarderController>();
        if (player != null)
        {
            player.Die();
        }
    }
}
