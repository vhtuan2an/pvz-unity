using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(NetworkObject))]
public class TankZombie : ZombieBase
{
    [Header("Movement")]
    [SerializeField] private float startDelay = 0.5f;

    [Header("Animation")]
    [SerializeField] private float dieAnimLength = 1.0f;

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private bool isBlockedByPlant = false;

    protected override void Start()
    {
        base.Start();

        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        SetWalkingClientRpc(false);

        Invoke(nameof(StartWalking), startDelay);
    }

    private void StartWalking()
    {
        if (!IsServer) return;
        isBlockedByPlant = false;
        SetWalkingClientRpc(true);
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        float checkDistance = 0.05f;

        RaycastHit2D hit = Physics2D.BoxCast(
            rb.position,
            boxCollider.size,
            0f,
            Vector2.left,
            checkDistance,
            LayerMask.GetMask("Plant")
        );

        if (hit.collider != null)
        {
            // GẶP CÂY → DỪNG
            isBlockedByPlant = true;
            SetWalkingClientRpc(false);
            rb.MovePosition(rb.position);
            return;
        }

        // KHÔNG CÓ CÂY → ĐI
        isBlockedByPlant = false;
        float speed = GetMoveSpeed();
        Vector2 movement = Vector2.left * speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + movement);
        SetWalkingClientRpc(true);
    }

    protected override void Die()
    {
        if (!IsServer) return;

        SetWalkingClientRpc(false);
        TriggerDieAnimationClientRpc();

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

    // ================= RPC =================

    [ClientRpc]
    private void SetWalkingClientRpc(bool isWalking)
    {
        if (animator != null)
        {
            animator.SetBool("isWalking", isWalking);
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
