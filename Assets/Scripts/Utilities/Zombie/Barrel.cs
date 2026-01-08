using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Animator))]
public class Barrel : ZombieBase
{
    [Header("Barrel Settings")]

    [SerializeField] private int barrelDamage = 3;        
    [SerializeField] private float explodeDelay = 0.5f;  
    [SerializeField] private float barrelMoveSpeed = 3f; 

    private bool exploded = false;
    private Rigidbody2D rb;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        rb = GetComponent<Rigidbody2D>();

        if (!IsServer) return;

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void FixedUpdate()
    {
        if (!IsServer || exploded) return;

        // Di chuyển barrel sang trái
        rb.MovePosition(rb.position + Vector2.left * barrelMoveSpeed * Time.fixedDeltaTime);
    }

    // ================= COLLISION =================
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer || exploded) return;

        // Va chạm trực tiếp với plant
        PlantBase plant = collision.GetComponent<PlantBase>();
        if (plant != null)
        {
            plant.TakeDamage(barrelDamage);
            ExplodeServerRpc();
            return;
        }

        // Va chạm với projectile / zombie (mọi object kế thừa ZombieBase)
        ZombieBase projZombie = collision.GetComponent<ZombieBase>();
        if (projZombie != null)
        {
            TakeDamage(projZombie.GetDamage()); // barrel nhận damage
        }
    }

    // ================= EXPLODE =================
    [ServerRpc(RequireOwnership = false)]
    private void ExplodeServerRpc()
    {
        if (exploded) return;
        exploded = true;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        TriggerExplodeClientRpc();

        Invoke(nameof(Despawn), explodeDelay);
    }

    [ClientRpc]
    private void TriggerExplodeClientRpc()
    {
        if (animator != null)
        {
            animator.SetTrigger("Explode");
        }
    }

    private void Despawn()
    {
        if (IsServer && GetComponent<NetworkObject>() != null)
        {
            GetComponent<NetworkObject>().Despawn();
        }

        Destroy(gameObject);
    }

    public override void TakeDamage(int damage)
    {
        base.TakeDamage(damage);
    }
}
