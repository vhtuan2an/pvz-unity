using UnityEngine;
using Unity.Netcode;

public class ZombieBase : NetworkBehaviour // Thay đổi từ MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] protected int maxHealth = 10;
    [SerializeField] protected float moveSpeed = 1f;
    [SerializeField] protected int damage = 1;
    
    protected NetworkVariable<int> currentHealth;
    protected float slowMultiplier = 1f;
    protected Animator animator;

    protected virtual void Awake()
    {
        currentHealth = new NetworkVariable<int>(
            maxHealth,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    }

    protected virtual void Start()
    {
        animator = GetComponent<Animator>();
        
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
    }

    public virtual void TakeDamage(int damage)
    {
        if (!IsServer)
            return;

        currentHealth.Value -= damage;
        Debug.Log($"{gameObject.name} took {damage} damage. HP: {currentHealth.Value}/{maxHealth}");

        if (currentHealth.Value <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        if (!IsServer)
            return;

        Debug.Log($"{gameObject.name} died!");
        
        TriggerDeathAnimationClientRpc();
        Invoke(nameof(DespawnZombie), 1f);
    }

    [ClientRpc]
    private void TriggerDeathAnimationClientRpc()
    {
        // Death animation trigger removed - add animator parameter "Die" if needed
    }

    private void DespawnZombie()
    {
        if (!IsServer)
            return;

        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
        Destroy(gameObject);
    }

    public void ApplySlow(float duration, float slowAmount)
    {
        if (!IsServer)
            return;

        StartCoroutine(SlowCoroutine(duration, slowAmount));
    }

    private System.Collections.IEnumerator SlowCoroutine(float duration, float slowAmount)
    {
        slowMultiplier = slowAmount;
        yield return new WaitForSeconds(duration);
        slowMultiplier = 1f;
    }

    // Getters
    public int GetCurrentHealth() => currentHealth.Value;
    public int GetMaxHealth() => maxHealth;
    public float GetMoveSpeed() => moveSpeed * slowMultiplier;
    public int GetDamage() => damage;
}