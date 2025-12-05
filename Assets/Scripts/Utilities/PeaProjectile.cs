using UnityEngine;
using Unity.Netcode;

public class PeaProjectile : NetworkBehaviour
{
    public enum PeaType
    {
        Normal,
        Fire,
        Snow
    }

    [Header("Projectile Settings")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private int damage = 1;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private PeaType peaType = PeaType.Normal;

    [Header("Fire Settings")]
    [SerializeField] private float fireDamageMultiplier = 2f;

    [Header("Snow Settings")]
    [SerializeField] private float slowPercentage = 0.5f;
    [SerializeField] private float slowDuration = 2f;

    private bool hasHit = false;

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
        if (!IsServer || hasHit)
            return;
        
        // Kiểm tra có phải zombie không
        ZombieBase zombie = collision.GetComponent<ZombieBase>();
        if (zombie != null)
        {
            hasHit = true;
            ApplyElementalEffect(zombie);
            DestroyProjectile();
        }
    }

    private void ApplyElementalEffect(ZombieBase zombie)
    {
        switch (peaType)
        {
            case PeaType.Fire:
                // Deal double damage
                int fireDamage = Mathf.RoundToInt(damage * fireDamageMultiplier);
                zombie.TakeDamage(fireDamage);
                Debug.Log($"Fire pea hit {zombie.name} for {fireDamage} damage");
                break;

            case PeaType.Snow:
                // Deal normal damage + apply slow
                zombie.TakeDamage(damage);
                zombie.ApplySlow(slowDuration, slowPercentage, "snowpea");
                Debug.Log($"Snow pea hit {zombie.name}, applied {slowPercentage * 100}% slow");
                break;

            case PeaType.Normal:
            default:
                // Normal damage only
                zombie.TakeDamage(damage);
                Debug.Log($"Normal pea hit {zombie.name} for {damage} damage");
                break;
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