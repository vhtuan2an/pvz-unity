using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class BasicZombie : ZombieBase
{
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

        float speed = GetMoveSpeed();
        Vector2 movement = Vector2.left * speed * Time.fixedDeltaTime;

        // Kiểm tra va chạm với plant bằng BoxCast
        RaycastHit2D hit = Physics2D.BoxCast(
            rb.position,
            boxCollider.size,
            0f,
            Vector2.left,
            movement.magnitude,
            LayerMask.GetMask("Plant") // layer plant
        );

        if (hit.collider == null)
        {
            // Không có vật cản → di chuyển bình thường
            rb.MovePosition(rb.position + movement);
        }
        else
        {
            // Có vật cản → dừng lại, trigger animation nếu cần
            rb.MovePosition(rb.position);
            if(animator != null)
            {
                animator.SetBool("isAttacking", true);
            }
        }
    }


    private void LateUpdate()
    {
        if(animator != null && rb != null)
        {
            RaycastHit2D hit = Physics2D.BoxCast(
                rb.position,
                boxCollider.size,
                0f,
                Vector2.left,
                0.01f,
                LayerMask.GetMask("Plant")
            );
            if(hit.collider == null)
                animator.SetBool("isAttacking", false);
        }
    }
}
