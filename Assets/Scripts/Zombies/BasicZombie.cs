using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class BasicZombie : ZombieBase
{
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    protected override void Start()
    {
        base.Start();

        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
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
            // Attack animation removed - add animator parameter if needed
        }
    }


    private void LateUpdate()
    {
        // Attack animation handling removed
    }
}
