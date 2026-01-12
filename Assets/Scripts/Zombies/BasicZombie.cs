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
    // private float dieAnimLength = 1.0f; // Removed 

    // Audio
    [Header("Audio Settings")]
    [SerializeField] private float groanIntervalMin = 5f;
    [SerializeField] private float groanIntervalMax = 12f;
    private float nextGroanTime;
    private static float globalLastGroanTime;
    private const float GLOBAL_GROAN_COOLDOWN = 1.5f;

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private bool isGroaning = false;
    private bool isEatingSoundPlaying = false;
    private string eatSoundKey;




    protected override void Start()
    {
        base.Start();
        eatSoundKey = $"zombie_eat_{NetworkObjectId}";
        nextGroanTime = Time.time + Random.Range(groanIntervalMin, groanIntervalMax);

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

        bool isEating = hit.collider != null;

        if (!isEating)
        {
            // === WALK ===
            rb.MovePosition(rb.position + movement);

            SetWalkingClientRpc(true);
            SetEatingClientRpc(false);
            HandleGroan();


            if (isEatingSoundPlaying)
            {
                NetworkGameManager.Instance.StopSoundClientRpc(eatSoundKey);
                isEatingSoundPlaying = false;
            }
        }
        else
        {
            // === EAT ===
            rb.MovePosition(rb.position);

            SetWalkingClientRpc(false);
            SetEatingClientRpc(true);

            // Play eat sound ONCE
            if (!isEatingSoundPlaying)
            {
                NetworkGameManager.Instance.PlayLoopSoundClientRpc(
                    eatSoundKey,
                    "zombie_eat",
                    0.6f,
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



    private void HandleGroan()
    {
        if (isGroaning) return;

        if (Time.time >= nextGroanTime)
        {
            if (Time.time > globalLastGroanTime + GLOBAL_GROAN_COOLDOWN)
            {
                globalLastGroanTime = Time.time;
                isGroaning = true;

                NetworkGameManager.Instance.PlaySoundClientRpc("zombie_groan");
                StartCoroutine(ResetGroanFlag(2.5f));
            }

            nextGroanTime = Time.time + Random.Range(groanIntervalMin, groanIntervalMax);
        }
    }

    private System.Collections.IEnumerator ResetGroanFlag(float delay)
    {
        yield return new WaitForSeconds(delay);
        isGroaning = false;
    }

    protected override void Die()
    {   
        StopEatingSound();  
        base.Die();
        NetworkGameManager.Instance.StopSoundClientRpc(eatSoundKey);
        NetworkGameManager.Instance.PlaySoundClientRpc("zombie_die");
        // Stop this script to prevent FixedUpdate movement
        enabled = false;
        
        // Disable physics
        if (rb != null) rb.simulated = false;
        if (boxCollider != null) boxCollider.enabled = false;
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
    private System.Collections.IEnumerator EatSoundLoop()
    {
        while (isEatingSoundPlaying && IsServer)
        {
            yield return new WaitForSeconds(2.2f); // độ dài clip eat
            if (isEatingSoundPlaying)
            {
                NetworkGameManager.Instance.PlaySoundClientRpc("zombie_eat");
            }
        }
    }

    private void StopEatingSound()
    {
        isEatingSoundPlaying = false;
    }

}
