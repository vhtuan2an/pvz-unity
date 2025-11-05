using UnityEngine;

public class PlantBase : MonoBehaviour
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
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}