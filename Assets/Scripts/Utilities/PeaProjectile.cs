using UnityEngine;
using Unity.Netcode;

public class PeaProjectile : NetworkBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private int damage = 1;
    [SerializeField] private float lifetime = 5f;

    private void Start()
    {
        // Auto destroy sau lifetime
        if (IsServer)
        {
            Invoke(nameof(DestroyProjectile), lifetime);
        }
    }

    private void Update()
    {
        // Di chuyển sang phải
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Chỉ server xử lý collision
        if (!IsServer)
            return;

        // Kiểm tra có phải zombie không
        ZombieBase zombie = collision.GetComponent<ZombieBase>();
        if (zombie != null)
        {
            Debug.Log($"Projectile hit zombie: {collision.name}");
            
            // Gây damage
            zombie.TakeDamage(damage);
            
            // Destroy projectile
            DestroyProjectile();
        }
    }

    private void DestroyProjectile()
    {
        if (IsServer)
        {
            GetComponent<NetworkObject>().Despawn();
            Destroy(gameObject);
        }
    }
}