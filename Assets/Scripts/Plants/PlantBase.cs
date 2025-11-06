using UnityEngine;
using Unity.Netcode;

public class PlantBase : NetworkBehaviour // Thay đổi từ MonoBehaviour
{
    [Header("Plant Settings")]
    [SerializeField] public int maxHealth = 1;
    [SerializeField] public int sunCost = 0;
    [SerializeField] public float cooldown = 0.5f;
    public Sprite packetImage;

    protected int currentHealth;

    protected virtual void Start()
    {
        currentHealth = maxHealth;
    }

    public virtual void TakeDamage(int damage)
    {
        // Chỉ server xử lý damage
        if (!IsServer)
            return;

        currentHealth -= damage;
        Debug.Log($"{gameObject.name} took {damage} damage. HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        // Chỉ server xử lý death
        if (!IsServer)
            return;

        Debug.Log($"{gameObject.name} died!");

        // Despawn từ network trước khi destroy
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }

        Destroy(gameObject);
    }
}