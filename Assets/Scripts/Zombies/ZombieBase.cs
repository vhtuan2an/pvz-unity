using UnityEngine;

public class ZombieBase : MonoBehaviour
{
    public int maxHealth = 100;
    protected int currentHealth;
    protected float slowMultiplier = 1f;
    
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

    public virtual void ApplySlow(float amount)
    {
        slowMultiplier = Mathf.Clamp(1f - amount, 0.1f, 1f);
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}