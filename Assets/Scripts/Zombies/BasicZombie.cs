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
    private Animator animator;

    protected override void Start()
    {
        base.Start();

        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;


        SetWalkingClientRpc(false);
        SetEatingClientRpc(false);


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
            SetEatingClientRpc(true);
            SetWalkingClientRpc(false);
            if (attackTimer >= attackRate)
            {
                PlantBase plant = hit.collider.GetComponent<PlantBase>();
                if (plant != null)
                    plant.TakeDamage(GetDamage());

                attackTimer = 0f;
            }
        }
    }


    protected override void Die()
    {
        if (!IsServer) return;
        TriggerDieClientRpc();
        rb.simulated = false;
        boxCollider.enabled = false;
        Invoke(nameof(DespawnZombie), dieAnimLength);
    }

    private void DespawnZombie()
    {
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true); 
        }
    }

    // ===================== Client RPCs =====================
    [ClientRpc]
    private void SetEatingClientRpc(bool value)
    {
        if (animator != null)
            animator.SetBool("isEating", value);
    }

    [ClientRpc]
    private void SetWalkingClientRpc(bool value)
    {
        if (animator != null)
            animator.SetBool("isWalking", value);
    }

    [ClientRpc]
    private void TriggerDieClientRpc()
    {
        if (animator != null)
        {
            animator.SetTrigger("Die");
            animator.SetBool("isWalking", false);
            animator.SetBool("isEating", false);
        }
    }
}
