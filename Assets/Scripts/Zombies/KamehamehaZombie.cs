using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(NetworkObject))]
public class KamehamehaZombie : ZombieBase
{
    [Header("Combat")]
    [SerializeField] private float attackRate = 1f;
    private float attackTimer = 0f;

    [Header("Movement")]
    [SerializeField] private float startDelay = 0.5f;

    [Header("Animation")]
    [SerializeField] private float dieAnimLength = 1.0f;

    [Header("Attack Animation")]
    [SerializeField] private float attackAnimDuration = 1.62f; 

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    private bool isAttackLocked = false;
    private PlantBase pendingTarget; 

    protected override void Start()
    {
        base.Start();

        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        SetWalkingClientRpc(false);
        SetAttackingClientRpc(false);

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

        RaycastHit2D hit = Physics2D.BoxCast(
            rb.position,
            boxCollider.size,
            0f,
            Vector2.left,
            0.05f,
            LayerMask.GetMask("Plant")
        );


        if (hit.collider == null)
        {
            if (!isAttackLocked)
            {
                rb.MovePosition(rb.position + movement);
                SetWalkingClientRpc(true);
                SetAttackingClientRpc(false);
            }
            return;
        }


        rb.MovePosition(rb.position);
        SetWalkingClientRpc(false);


        if (isAttackLocked) return;

        if (attackTimer >= attackRate)
        {
            isAttackLocked = true;
            attackTimer = 0f;

            SetAttackingClientRpc(true);


            pendingTarget = hit.collider.GetComponent<PlantBase>();

            Invoke(nameof(ApplyDelayedDamage), attackAnimDuration);
            Invoke(nameof(EndAttackAnimation), attackAnimDuration);
        }
    }


    private void ApplyDelayedDamage()
    {
        if (!IsServer) return;

        if (pendingTarget != null)
        {
            pendingTarget.TakeDamage(GetDamage()); 
            pendingTarget = null;
        }
    }

    private void EndAttackAnimation()
    {
        isAttackLocked = false;
        SetAttackingClientRpc(false);
    }
    // =======================================================

    protected override void Die()
    {
        base.Die();
        
        if (!IsServer) return;

        isAttackLocked = false;
        pendingTarget = null;
        
        NetworkGameManager.Instance.PlaySoundClientRpc("zombie_die");

        SetWalkingClientRpc(false);
        SetAttackingClientRpc(false);
        
        enabled = false;
        if (rb != null) rb.simulated = false;
        if (boxCollider != null) boxCollider.enabled = false;
    }

    [ClientRpc]
    private void SetWalkingClientRpc(bool isWalking)
    {
        animator?.SetBool("isWalking", isWalking);
    }

    [ClientRpc]
    private void SetAttackingClientRpc(bool isAttacking)
    {
        animator?.SetBool("isAttacking", isAttacking);
    }

}
