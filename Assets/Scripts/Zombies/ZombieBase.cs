using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class ZombieBase : NetworkBehaviour
{
    [Header("Stats")]
    [SerializeField] protected int maxHealth = 10;
    [SerializeField] protected float moveSpeed = 1f;
    [SerializeField] protected int damage = 1;
    [SerializeField] public float cooldown = 7.5f;
    [SerializeField] public Sprite packetImage;    

    [Header("Spawn Cost")]
    [SerializeField] private int brainCost = 50;

    public int GetBrainCost() => brainCost;
    
    protected NetworkVariable<int> currentHealth;
    protected Animator animator;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    // Slow effect tracking
    private Dictionary<string, SlowEffect> activeSlows = new Dictionary<string, SlowEffect>();
    private string currentVFXSource = null;
    private float currentSlowMultiplier = 1f;

    private class SlowEffect
    {
        public float slowAmount;
        public float endTime;
        public string sourceId;
    }

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
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
    }

    private void Update()
    {
        if (IsServer)
        {
            UpdateSlowEffects();
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
        
        // Clear slow effects
        activeSlows.Clear();

        TriggerDeathAnimationClientRpc();
        Invoke(nameof(DespawnZombie), 1f);
    }

    [ClientRpc]
    private void TriggerDeathAnimationClientRpc()
    {
        // Death animation trigger - add animator parameter "Die" if needed
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

    // Overload for color tint only (snow pea)
    public void ApplySlow(float duration, float slowAmount, string sourceId)
    {
        ApplySlow(duration, slowAmount, sourceId, null, 0f);
    }

    // Full version with optional freeze VFX (wintermint)
    public void ApplySlow(float duration, float slowAmount, string sourceId, string freezeVFXPrefabName, float vfxDuration)
    {
        if (!IsServer)
            return;

        // Apply stun (100% slow)
        if (slowAmount >= 1f)
        {
            currentSlowMultiplier = 0f;
            Debug.Log($"{gameObject.name} stunned (100% slow) by {sourceId} for {duration}s");
            
            // Always apply blue tint
            ApplyColorTintClientRpc(slowAmount);
            
            // Apply animation freeze
            ApplyAnimationSpeedClientRpc(0f);
            
            // Spawn freeze VFX if prefab name provided
            if (!string.IsNullOrEmpty(freezeVFXPrefabName))
            {
                SpawnFreezeVFXClientRpc(sourceId, freezeVFXPrefabName, vfxDuration);
            }
            
            activeSlows[sourceId] = new SlowEffect
            {
                slowAmount = slowAmount,
                endTime = Time.time + duration,
                sourceId = sourceId
            };
            currentVFXSource = sourceId;
            return;
        }

        // Normal slow handling (color tint only, no VFX)
        if (activeSlows.ContainsKey(sourceId))
        {
            var existing = activeSlows[sourceId];
            existing.slowAmount = slowAmount;
            existing.endTime = Time.time + duration;
        }
        else
        {
            activeSlows[sourceId] = new SlowEffect
            {
                slowAmount = slowAmount,
                endTime = Time.time + duration,
                sourceId = sourceId
            };
        }

        RecalculateSlowMultiplier();
        Debug.Log($"{gameObject.name} slowed by {slowAmount * 100}% from {sourceId} for {duration}s (total multiplier: {currentSlowMultiplier})");
    }

    private void UpdateSlowEffects()
    {
        List<string> expiredSlows = new List<string>();
        
        // Check for expired slows
        foreach (var kvp in activeSlows)
        {
            if (Time.time >= kvp.Value.endTime)
            {
                expiredSlows.Add(kvp.Key);
            }
        }

        // Remove expired slows
        foreach (var sourceId in expiredSlows)
        {
            activeSlows.Remove(sourceId);
            Debug.Log($"{gameObject.name} slow from {sourceId} expired");
            
            if (currentVFXSource == sourceId)
            {
                currentVFXSource = null;
            }
        }


        // Recalculate multiplier and color if any slows expired
        if (expiredSlows.Count > 0)
        {
            RecalculateSlowMultiplier();
            UpdateColorBasedOnSlows();
        }
    }

    private void RecalculateSlowMultiplier()
    {
        if (activeSlows.Count == 0)
        {
            currentSlowMultiplier = 1f;
            ResetColorClientRpc();
            ApplyAnimationSpeedClientRpc(1f);
            return;
        }

        // Check for 100% slow (freeze)
        float maxSlowAmount = 0f;
        foreach (var slow in activeSlows.Values)
        {
            if (slow.slowAmount >= 1f)
            {
                currentSlowMultiplier = 0f;
                ApplyColorTintClientRpc(1f);
                ApplyAnimationSpeedClientRpc(0f);
                return;
            }
            maxSlowAmount = Mathf.Max(maxSlowAmount, slow.slowAmount);
        }

        // Stack slows multiplicatively
        float multiplier = 1f;
        foreach (var slow in activeSlows.Values)
        {
            multiplier *= (1f - slow.slowAmount);
        }
        
        currentSlowMultiplier = multiplier;
        ApplyColorTintClientRpc(maxSlowAmount);
        ApplyAnimationSpeedClientRpc(currentSlowMultiplier); // ⭐ Apply animation speed
    }

    private void UpdateColorBasedOnSlows()
    {
        if (activeSlows.Count == 0)
        {
            ResetColorClientRpc();
            return;
        }

        // Find strongest slow amount
        float maxSlowAmount = 0f;
        foreach (var slow in activeSlows.Values)
        {
            maxSlowAmount = Mathf.Max(maxSlowAmount, slow.slowAmount);
        }

        ApplyColorTintClientRpc(maxSlowAmount);
    }

    [ClientRpc]
    private void ApplyColorTintClientRpc(float slowAmount)
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) return;
        }

        // Calculate blue tint based on slow amount
        Color blueShade = new Color(
            1f - (slowAmount * 0.4f),  // Reduce red
            1f - (slowAmount * 0.3f),  // Reduce green
            1f + (slowAmount * 0.2f)   // Boost blue
        );
        
        // Apply tint by multiplying original color
        Color targetColor = originalColor * blueShade;
        spriteRenderer.color = targetColor;
        
        Debug.Log($"Applied blue tint: slowAmount={slowAmount}, color={targetColor}");
    }

    [ClientRpc]
    private void ResetColorClientRpc()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) return;
        }

        spriteRenderer.color = originalColor;
        Debug.Log($"Reset to original color: {originalColor}");
    }

    [ClientRpc]
    private void ApplyAnimationSpeedClientRpc(float speedMultiplier)
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) return;
        }

        animator.speed = speedMultiplier;
        Debug.Log($"Animation speed set to: {speedMultiplier}");
    }

    [ClientRpc]
    private void SpawnFreezeVFXClientRpc(string sourceId, string freezeVFXPrefabName, float vfxDuration)
    {
        Debug.Log($"Client: Spawning freeze VFX '{freezeVFXPrefabName}' for {sourceId}, duration: {vfxDuration}s");
        
        // Load freeze VFX from Resources
        GameObject vfxPrefab = Resources.Load<GameObject>($"VFX/Prefabs/{freezeVFXPrefabName}");
        
        if (vfxPrefab == null)
        {
            Debug.LogError($"Failed to load Resources/VFX/Prefabs/{freezeVFXPrefabName}.prefab");
            return;
        }

        // Spawn freeze VFX as child
        GameObject vfxInstance = Instantiate(vfxPrefab, transform);
        
        // Set sorting layer to render above zombie
        SpriteRenderer vfxSprite = vfxInstance.GetComponent<SpriteRenderer>();
        if (vfxSprite != null)
        {
            vfxSprite.sortingLayerName = "TransparentFX";
            vfxSprite.sortingOrder = 10;
        }

        // Add auto-destroy component
        AutoDestroyVFX autoDestroy = vfxInstance.AddComponent<AutoDestroyVFX>();
        autoDestroy.lifetime = vfxDuration;
        
        Debug.Log($"Freeze VFX spawned for {sourceId}, will auto-destroy in {vfxDuration}s");
    }

    // Getters
    public int GetCurrentHealth() => currentHealth.Value;
    public int GetMaxHealth() => maxHealth;
    public float GetMoveSpeed() => moveSpeed * currentSlowMultiplier;
    public int GetDamage() => damage;
}

public class AutoDestroyVFX : MonoBehaviour
{
    public float lifetime = 5f;
    private float spawnTime;

    private void Start()
    {
        spawnTime = Time.time;
    }

    private void Update()
    {
        if (Time.time >= spawnTime + lifetime)
        {
            Debug.Log($"Auto-destroying VFX after {lifetime}s");
            Destroy(gameObject);
        }
    }
}