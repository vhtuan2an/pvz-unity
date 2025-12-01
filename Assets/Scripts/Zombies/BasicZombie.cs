using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(NetworkObject))]
public class BasicZombie : ZombieBase
{
    [Header("Combat")]
    [SerializeField] private float attackRate = 1f;
    private float attackTimer = 0f;

    [Header("Movement")]
    [SerializeField] private float startDelay = 0.5f; 

    [Header("Animation")]
    [SerializeField] private float dieAnimLength = 1.0f; 

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    protected override void Start()
    {
        base.Start();

        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (animator != null)
        {
            SetWalkingClientRpc(false);
            SetEatingClientRpc(false);
        }

        Invoke(nameof(StartWalking), startDelay);
    }

    private void StartWalking()
    {
        SetWalkingClientRpc(true);
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        attackTimer += Time.fixedDeltaTime;
        float speed = GetMoveSpeed();
        Vector2 movement = Vector2.left * speed * Time.fixedDeltaTime;
        float checkDistance = 0.01f;

        RaycastHit2D hit = Physics2D.BoxCast(
            rb.position,
            boxCollider.size,
            0f,
            Vector2.left,
            checkDistance,
            LayerMask.GetMask("Plant")
        );

        if (hit.collider == null)
        {
            rb.MovePosition(rb.position + movement);
            SetEatingClientRpc(false);
            SetWalkingClientRpc(true);
        }
        else
        {
            rb.MovePosition(rb.position);
            SetWalkingClientRpc(false);
            SetEatingClientRpc(true);

            if (attackTimer >= attackRate)
            {
                PlantBase plant = hit.collider.GetComponent<PlantBase>();
                if (plant != null)
                {
                    plant.TakeDamage(GetDamage());
                }
                attackTimer = 0f;
            }
        }
    }

    protected override void Die()
    {
        if (!IsServer) return;

        // Stop movement/attack animations
        SetWalkingClientRpc(false);
        SetEatingClientRpc(false);

        // Trigger die animation on clients
        TriggerDieAnimationClientRpc();

        // Despawn after dieAnimLength
        Invoke(nameof(DespawnZombie), dieAnimLength);
    }

    private void DespawnZombie()
    {
        if (!IsServer) return;

        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
        Destroy(gameObject);
    }

    [ClientRpc]
    private void SetWalkingClientRpc(bool isWalking)
    {
        if (animator != null)
        {
            animator.SetBool("isWalking", isWalking);
        }
    }

    [ClientRpc]
    private void SetEatingClientRpc(bool isEating)
    {
        if (animator != null)
        {
            animator.SetBool("isEating", isEating);
        }
    }

    [ClientRpc]
    private void TriggerDieAnimationClientRpc()
    {
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }
    }
}
