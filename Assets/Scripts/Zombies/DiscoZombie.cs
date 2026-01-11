using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(NetworkObject))]
public class DiscoZombie : ZombieBase
{
    [Header("Combat")]
    [SerializeField] private float attackRate = 1f;
    private float attackTimer = 0f;

    [Header("Movement")]
    [SerializeField] private float startDelay = 0.5f;

    [Header("Summon Timing")]
    [SerializeField] private float summonCooldown = 1.2f;
    [SerializeField] private float summonDistance = 0.8f;
    [SerializeField] private GameObject discoSummonPrefab;

    private float summonTimer = 0f;

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    private bool isEating = false;
    private bool isWalking = false;

    // ===================== SUMMON TRACKING =====================
    private readonly List<NetworkObject> activeSummons = new List<NetworkObject>();

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
        isWalking = true;
        SetWalkingClientRpc(true);
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        attackTimer += Time.fixedDeltaTime;
        summonTimer += Time.fixedDeltaTime;

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

            isEating = false;
            isWalking = true;

            SetEatingClientRpc(false);
            SetWalkingClientRpc(true);

            TrySummon();
        }
        else
        {
            rb.MovePosition(rb.position);

            isEating = true;
            isWalking = false;

            SetWalkingClientRpc(false);
            SetEatingClientRpc(true);
        }

        if (isEating && attackTimer >= attackRate)
        {
            PlantBase plant = hit.collider.GetComponent<PlantBase>();
            if (plant != null)
            {
                plant.TakeDamage(GetDamage());
            }
            attackTimer = 0f;
        }
    }

    // ===================== SUMMON LOGIC =====================

    private void TrySummon()
    {
        if (!isWalking) return;
        if (summonTimer < summonCooldown) return;
        if (discoSummonPrefab == null) return;


        CleanupSummonList();
        if (activeSummons.Count > 0) return;

        SpawnDiscoSummons();
        summonTimer = 0f;
    }

    private void SpawnDiscoSummons()
    {
        Vector3 center = transform.position;

        Vector3[] directions =
        {
            Vector3.up,
            Vector3.down,
            Vector3.left,
            Vector3.right
        };

        foreach (Vector3 dir in directions)
        {
            Vector3 spawnPos = center + dir * summonDistance;

            GameObject summon = Instantiate(
                discoSummonPrefab,
                spawnPos,
                Quaternion.identity
            );

            NetworkObject netObj = summon.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                activeSummons.Add(netObj);
            }
        }
    }

    /// <summary>

    /// </summary>
    private void CleanupSummonList()
    {
        for (int i = activeSummons.Count - 1; i >= 0; i--)
        {
            NetworkObject obj = activeSummons[i];

            if (obj == null || !obj.IsSpawned)
            {
                activeSummons.RemoveAt(i);
            }
        }
    }

    // ===================== DIE =====================

    protected override void Die()
    {
        if (!IsServer) return;

        base.Die();

        enabled = false;
        activeSummons.Clear();

        if (rb != null) rb.simulated = false;
        if (boxCollider != null) boxCollider.enabled = false;

        // 🔥 TỰ DESPAWN SAU KHI CHẾT
        Invoke(nameof(ForceDespawn), 1.5f); // = thời gian clip die
    }

    private void ForceDespawn()
    {
        if (!IsServer) return;

        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
    }


    // ===================== ANIMATION =====================

    [ClientRpc]
    private void SetWalkingClientRpc(bool value)
    {
        if (animator != null)
        {
            animator.SetBool("isWalking", value);
        }
    }

    [ClientRpc]
    private void SetEatingClientRpc(bool value)
    {
        if (animator != null)
        {
            animator.SetBool("isEating", value);
        }
    }
}
