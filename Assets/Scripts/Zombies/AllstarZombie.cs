using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(NetworkObject))]
public class AllStarZombie : ZombieBase
{
    [Header("Combat")]
    [SerializeField] private float attackRate = 1f;
    private float attackTimer = 0f;

    [Header("Movement")]
    [SerializeField] private float startDelay = 0.5f; 

    [Header("Animation")]
    [SerializeField] private float dieAnimLength = 1.0f; 

    [Header("Audio")]
    private string eatSoundKey;
    private bool isEatingSoundPlaying = false;


    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    protected override void Start()
    {
        base.Start();
        eatSoundKey = $"allstar_eat_{NetworkObjectId}";
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (animator != null)
        {
            SetRunningClientRpc(false);
            SetEatingClientRpc(false);
        }

        Invoke(nameof(StartRunning), startDelay);
    }

    private void StartRunning()
    {
        SetRunningClientRpc(true);
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

        bool isEating = hit.collider != null;

        if (!isEating)
        {
            // ===== RUN =====
            rb.MovePosition(rb.position + movement);

            SetRunningClientRpc(true);
            SetEatingClientRpc(false);

            // Stop eat sound nếu đang ăn
            if (isEatingSoundPlaying)
            {
                NetworkGameManager.Instance.StopSoundClientRpc(eatSoundKey);
                isEatingSoundPlaying = false;
            }
        }
        else
        {
            // ===== EAT =====
            rb.MovePosition(rb.position);

            SetRunningClientRpc(false);
            SetEatingClientRpc(true);

            // Play eat loop sound ONCE
            if (!isEatingSoundPlaying)
            {
                NetworkGameManager.Instance.PlayLoopSoundClientRpc(
                    eatSoundKey,
                    "zombie_eat",
                    0.7f,
                    Random.Range(0.95f, 1.05f)
                );
                isEatingSoundPlaying = true;
            }

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
        StopEatingSound();
        base.Die();

        if (!IsServer) return;

        NetworkGameManager.Instance.StopSoundClientRpc(eatSoundKey);
        NetworkGameManager.Instance.PlaySoundClientRpc("zombie_die");

        SetRunningClientRpc(false);
        SetEatingClientRpc(false);

        enabled = false;
        if (rb != null) rb.simulated = false;
        if (boxCollider != null) boxCollider.enabled = false;
    }


    [ClientRpc]
    private void SetRunningClientRpc(bool isRunning)
    {
        if (animator != null)
        {
            animator.SetBool("isRunning", isRunning);
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

    private void StopEatingSound()
    {
        isEatingSoundPlaying = false;
    }


}
