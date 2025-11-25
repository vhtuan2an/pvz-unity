using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class BasicZombie : ZombieBase
{
    [Header("Combat")]
    [SerializeField] private float attackRate = 1f;
    private float attackTimer = 0f;

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
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        attackTimer += Time.fixedDeltaTime;

        float speed = GetMoveSpeed();
        Vector2 movement = Vector2.left * speed * Time.fixedDeltaTime;

        // BoxCast kiểm tra phía trước có plant không
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
            // Không có plant → di chuyển
            rb.MovePosition(rb.position + movement);
            if (animator != null)
                animator.SetBool("isAttacking", false);
        }
        else
        {
            // Có plant → đứng yên, attack
            rb.MovePosition(rb.position);
            if (animator != null)
                animator.SetBool("isAttacking", true);

            if (attackTimer >= attackRate)
            {
                // Gọi TakeDamage trên plant nếu có
                PlantBase plant = hit.collider.GetComponent<PlantBase>();
                if (plant != null)
                    plant.TakeDamage(GetDamage());

                attackTimer = 0f;
            }
        }
    }
}
