using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class ZombieBase : NetworkBehaviour
{
    public enum VFXTargetType
    {
        Feet,
        Head
    }

    [Header("Stats")]
    [SerializeField] protected int maxHealth = 10;
    [SerializeField] protected float moveSpeed = 1f;
    [SerializeField] protected int damage = 1;
    [SerializeField] public float cooldown = 7.5f;
    [SerializeField] public Sprite packetImage;    

    [Header("Spawn Cost")]
    [SerializeField] private int brainCost = 50;
    private static int globalSpawnOrder = 0;

    public int GetBrainCost() => brainCost;
    
    // Check if 100% slowed (frozen/stunned)
    public bool IsFrozen => currentSlowMultiplier <= 0f;

    protected NetworkVariable<int> currentHealth;
    protected Animator animator;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    // Slow effect tracking
    private Dictionary<string, SlowEffect> activeSlows = new Dictionary<string, SlowEffect>();
    private string currentVFXSource = null;
    protected float currentSlowMultiplier = 1f;

    private class SlowEffect
    {
        public float slowAmount;
        public float endTime;
        public string sourceId;
        public bool appliesTint = true;
    }

    protected virtual void Awake()
    {
        currentHealth = new NetworkVariable<int>(
            maxHealth,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    }

    protected float currentSpeed => moveSpeed * currentSlowMultiplier;

    protected virtual void Start()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            if (gameObject.GetComponent<DynamicSorting>() == null)
            {
                gameObject.AddComponent<DynamicSorting>();
            }
        }
        
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
    }

    protected virtual void Update()
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
        // Recalculate to reset logic stats (speed, damage) to normal
        RecalculateSlowMultiplier();
        
        // Force visual cleanup on clients
        ClearStatusEffectsClientRpc();

        // Stress test: FORCE disable everything
        enabled = false;
        if (TryGetComponent<Rigidbody2D>(out var rb)) rb.simulated = false;
        if (TryGetComponent<Collider2D>(out var col)) col.enabled = false;

        // Trigger on clients (and host)
        TriggerDeathAnimationClientRpc();
        StartCoroutine(ForceDespawnRoutine());
    }
    private System.Collections.IEnumerator ForceDespawnRoutine()
    {
        yield return new WaitForSeconds(1.5f); // = độ dài clip Die
        DespawnZombie();
    }

    // Called via Animation Event on the last frame of the Death Clip
    public void OnDeathAnimationEnd()
    {
        if (!IsServer) return; // Only server controls despawn
        
        StartCoroutine(DeathHoldRoutine());
    }

    private System.Collections.IEnumerator DeathHoldRoutine()
    {
        // Hold on the last frame for 1 second
        yield return new WaitForSeconds(1.0f);
        DespawnZombie();
    }

    [ClientRpc]
    private void TriggerDeathAnimationClientRpc()
    {
        // 1. Force Local Cleanup (Sync with Death Anim)
        if (animator != null) 
        {
            animator.SetTrigger("Die");
            animator.speed = 1f; // Ensure death plays at normal speed
        }

        if (spriteRenderer != null && originalColor != Color.clear)
        {
            spriteRenderer.color = originalColor;
        }

        var effects = GetComponentsInChildren<AutoDestroyVFX>();
        foreach (var effect in effects)
        {
            if (effect != null) Destroy(effect.gameObject);
        }
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

    // Full version with optional freeze VFX (wintermint/butter)
    public void ApplySlow(float duration, float slowAmount, string sourceId, string freezeVFXPrefabName, float vfxDuration, bool applyTint = true, VFXTargetType vfxTarget = VFXTargetType.Feet)
    {
        if (!IsServer)
            return;

        // Apply stun (100% slow)
        if (slowAmount >= 1f)
        {
            currentSlowMultiplier = 0f;
            Debug.Log($"{gameObject.name} stunned (100% slow) by {sourceId} for {duration}s");
            
            // Apply blue tint ONLY if requested
            if (applyTint)
            {
                ApplyColorTintClientRpc(slowAmount);
            }
            
            // Apply animation freeze
            ApplyAnimationSpeedClientRpc(0f);
            
            // Spawn freeze VFX if prefab name provided
            {
                SpawnFreezeVFXClientRpc(sourceId, freezeVFXPrefabName, vfxDuration, vfxTarget);
            }

            // Play frozen sound (handled on client via RPC if this was called from server, 
            // but since ApplySlow runs on server, we need a ClientRpc for sound)
            PlayFreezeSoundClientRpc();
            
            activeSlows[sourceId] = new SlowEffect
            {
                slowAmount = slowAmount,
                endTime = Time.time + duration,
                sourceId = sourceId,
                appliesTint = applyTint
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
                sourceId = sourceId,
                appliesTint = applyTint
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
        
        // Calculate max tint amount based ONLY on slows that apply tint
        float maxTintAmount = 0f;
        foreach (var slow in activeSlows.Values)
        {
            if (slow.appliesTint)
            {
                maxTintAmount = Mathf.Max(maxTintAmount, slow.slowAmount);
            }
        }
        
        currentSlowMultiplier = multiplier;
        ApplyColorTintClientRpc(maxTintAmount);
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
    private void SpawnFreezeVFXClientRpc(string sourceId, string freezeVFXPrefabName, float vfxDuration, VFXTargetType targetType)
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
        
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        // Adjust position based on target type
        vfxInstance.transform.localPosition = GetVFXOffset(targetType);
        
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

    protected virtual Vector3 GetVFXOffset(VFXTargetType targetType)
    {
        switch (targetType)
        {
            case VFXTargetType.Head:
                if (spriteRenderer != null)
                {
                    // Calculate local offset to top of sprite
                    float yOffset = spriteRenderer.bounds.max.y - transform.position.y;
                    return new Vector3(-0.4f, yOffset - 0.2f, 0);
                }
                else
                {
                    return new Vector3(0, 1.3f, 0);
                }

            case VFXTargetType.Feet:
            default:
                return new Vector3(0, -0.7f, 0);
        }
    }

    // Getters
    public int GetCurrentHealth() => currentHealth.Value;
    public int GetMaxHealth() => maxHealth;
    public float GetMoveSpeed() => moveSpeed * currentSlowMultiplier;
    public int GetDamage() => Mathf.FloorToInt(damage * currentSlowMultiplier);

    [ClientRpc]
    private void PlayFreezeSoundClientRpc()
    {
         if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySound("frozen");
        }
    }
    [ClientRpc]
    private void ClearStatusEffectsClientRpc()
    {
        // 1. Reset Color & Speed explicitly
        ResetColorClientRpc();
        ApplyAnimationSpeedClientRpc(1f);
        
        // 2. Destroy attached VFX (Butter, Ice Block)
        // Find all children with AutoDestroyVFX
        var effects = GetComponentsInChildren<AutoDestroyVFX>();
        foreach (var effect in effects)
        {
            Destroy(effect.gameObject);
        }
        
        Debug.Log("Cleared all status effects/VFX on death.");
    }
}

