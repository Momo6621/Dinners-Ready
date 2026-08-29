using UnityEngine;

public class Respawn : MonoBehaviour
{
    public Transform respawnPoint;
    public float fallY = -10f;
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (transform.position.y < fallY)
        {
            RespawnPlayer();
        }
    }

    public void RespawnPlayer()
    {
        transform.position = respawnPoint.position;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Hazard"))
        {
            RespawnPlayer();
        }
    }
}