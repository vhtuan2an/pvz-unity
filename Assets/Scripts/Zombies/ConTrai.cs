using UnityEngine;
using Unity.Netcode;

public enum ConTraiState
{
    Flying,
    Ground
}

public enum ConTraiSpawnMode
{
    Normal,
    Cannon
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class ConTrai : ZombieBase
{
    [Header("State")]
    [SerializeField] private ConTraiState state = ConTraiState.Ground;

    [Header("Flying")]
    [SerializeField] private float flySpeed = 8f;
    [SerializeField] private Vector2 flyDirection = Vector2.left;

    [Header("Combat (Ground)")]
    [SerializeField] private float attackRate = 1f;

    private float attackTimer;
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    /* =========================
     * UNITY
     * ========================= */
    protected override void Start()
    {
        base.Start();

        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        /*if (IsServer)
        {
            Init(ConTraiSpawnMode.Cannon);
        }*/
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        if (state == ConTraiState.Flying)
        {
            FlyUpdate();
        }
        else
        {
            GroundUpdate();
        }
    }

    /* =========================
     * INIT
     * ========================= */
    public void Init(ConTraiSpawnMode mode)
    {
        if (!IsServer) return;

        if (mode == ConTraiSpawnMode.Cannon)
            EnterFlyingState();
        else
            EnterGroundState();
    }

    /* =========================
     * FLY
     * ========================= */
    private void EnterFlyingState()
    {
        state = ConTraiState.Flying;
        attackTimer = 0f;

        SetWalkingClientRpc(false);
        SetEatingClientRpc(false);
        SetFlyingClientRpc(true);
    }

    private void FlyUpdate()
    {
        float checkDistance = 0.05f;

        RaycastHit2D hit = Physics2D.BoxCast(
            rb.position,
            boxCollider.size,
            0f,
            flyDirection,
            checkDistance,
            LayerMask.GetMask("Plant")
        );

        if (hit.collider != null)
        {
            EnterGroundState(); 
            return;
        }

        rb.MovePosition(rb.position + flyDirection * flySpeed * Time.fixedDeltaTime);
    }

    /* =========================
     * GROUND
     * ========================= */
    private void EnterGroundState()
    {
        state = ConTraiState.Ground;
        attackTimer = 0f;

        SetFlyingClientRpc(false);
    }

    private void GroundUpdate()
    {
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
                    plant.TakeDamage(GetDamage());

                attackTimer = 0f;
            }
        }
    }

    /* =========================
     * ANIMATOR RPC
     * ========================= */
    [ClientRpc]
    private void SetWalkingClientRpc(bool value)
    {
        animator?.SetBool("isWalking", value);
    }

    [ClientRpc]
    private void SetEatingClientRpc(bool value)
    {
        animator?.SetBool("isEating", value);
    }

    [ClientRpc]
    private void SetFlyingClientRpc(bool value)
    {
        animator?.SetBool("isFlying", value);
    }
}
