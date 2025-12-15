using UnityEngine;
using Unity.Netcode;

public class Wallnut : PlantBase
{
    private Animator animator;
    [Header("Wallnut Settings")]
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();
        
        if (IsServer)
        {
            // Start with full health animation
            UpdateHealthAnimation();
        }
    }

    public override void TakeDamage(int damage)
    {
        if (!IsServer)
            return;

        currentHealth -= damage;
        Debug.Log($"Wallnut took {damage} damage. HP: {currentHealth}/{maxHealth}");

        // Update animation based on health
        UpdateHealthAnimation();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // Restore health when another wallnut is placed on top
    public void RestoreHealth()
    {
        if (!IsServer)
            return;

        currentHealth = maxHealth;
        Debug.Log($"Wallnut restored to full health! HP: {currentHealth}/{maxHealth}");
        
        UpdateHealthAnimation();
    }

    private void UpdateHealthAnimation()
    {
        if (!IsServer)
            return;

        float healthPercent = (float)currentHealth / maxHealth;
        string animationName = GetAnimationForHealth(healthPercent);
        Debug.Log($"Wallnut health: {healthPercent * 100:F0}% -> Playing: {animationName}");
        PlayAnimationClientRpc(animationName);
    }

    private string GetAnimationForHealth(float healthPercent)
    {
        if (healthPercent > 0.75f)
            return "Idle";
        else if (healthPercent > 0.5f)
            return "Degrade1";
        else if (healthPercent > 0.25f)
            return "Degrade2";
        else
            return "Degrade3";
    }

    [ClientRpc]
    private void PlayAnimationClientRpc(string animationName)
    {
        if (animator != null)
        {
            animator.Play(animationName);
            Debug.Log($"[Client] Playing animation: {animationName}");
        }
    }
}