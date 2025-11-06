using UnityEngine;

public class BasicZombie : ZombieBase
{
    protected override void Start()
    {
        base.Start();
    }

    void Update()
    {
        // Chỉ server mới di chuyển zombie
        if (!IsServer) // ✅ Dùng IsServer từ ZombieBase → NetworkBehaviour
            return;

        // Move zombie left
        float speed = GetMoveSpeed(); // Lấy speed từ base class
        transform.Translate(Vector2.left * speed * Time.deltaTime);
    }
}