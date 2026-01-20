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
    protected Dictionary<string, SlowEffect> activeSlows = new Dictionary<string, SlowEffect>();
    private string currentVFXSource = null;
    private List<GameObject> activeVFXInstances = new List<GameObject>();
    private List<NetworkObject> serverSpawnedVFX = new List<NetworkObject>();
    protected float currentSlowMultiplier = 1f;

    protected class SlowEffect
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
                var sorting = gameObject.AddComponent<DynamicSorting>();
                sorting.group = DynamicSorting.SortGroup.Zombie;
            }
        }
        
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }

        if (GameStatsTracker.Instance != null) GameStatsTracker.Instance.RegisterZombie(this);
    }

    protected virtual void OnDestroy()
    {
        if (GameStatsTracker.Instance != null) GameStatsTracker.Instance.UnregisterZombie(this);
        CleanupLocalVFX();
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

        // Direct refund if losing
        if (GameStatsTracker.Instance != null && GameStatsTracker.Instance.IsZombieLosingUnits)
        {
            int rawRefund = Mathf.RoundToInt(GetBrainCost() * GameStatsTracker.Instance.zombieRefundPercent);
            int refund = RoundToNearestMultipleOf5(rawRefund);
            if (refund > 0)
            {
                ZombieManager.Instance?.AddBrainsDirectlyClientRpc(refund);
                Debug.Log($"[COMEBACK] Zombie {name} triggered {refund} brain refund RPC (losing).");
            }
        }

        // 1. Cleanup Server-Spawned VFX
        foreach (var netVFX in serverSpawnedVFX)
        {
            if (netVFX != null && netVFX.IsSpawned)
            {
                netVFX.Despawn();
            }
        }
        serverSpawnedVFX.Clear();
        
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

    private void CleanupLocalVFX()
    {
        // 1. Destroy explicitly tracked VFX
        foreach (var vfx in activeVFXInstances)
        {
            if (vfx != null) Destroy(vfx);
        }
        activeVFXInstances.Clear();

        // 2. Fallback: Find children (just in case)
        var effects = GetComponentsInChildren<AutoDestroyVFX>(true);
        foreach (var effect in effects)
        {
            if (effect != null) Destroy(effect.gameObject);
        }
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

        CleanupLocalVFX();
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



    // ✅ Overload: Simple slow (no VFX)
    public void ApplySlow(float duration, float slowAmount, string sourceId)
    {
        if (!IsServer) return;
        ApplyNormalSlow(duration, slowAmount, sourceId, true);
    }

    // ✅ Overload: String VFX (Resources)
    public void ApplySlow(float duration, float slowAmount, string sourceId, string freezeVFXPrefabName, float vfxDuration, bool applyTint = true, VFXTargetType vfxTarget = VFXTargetType.Feet)
    {
        if (!IsServer) return;

        bool isStun = slowAmount >= 1f;
        if (isStun)
        {
            currentSlowMultiplier = 0f;
            Debug.Log($"{gameObject.name} stunned (100% slow) by {sourceId} for {duration}s");
            
            if (applyTint) ApplyColorTintClientRpc(slowAmount);
            ApplyAnimationSpeedClientRpc(0f);
            
            if (!string.IsNullOrEmpty(freezeVFXPrefabName))
            {
                // Trigger client-side VFX spawning as child
                SpawnStatusVFXClientRpc(sourceId, freezeVFXPrefabName, vfxDuration, vfxTarget);
            }

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
        
        ApplyNormalSlow(duration, slowAmount, sourceId, applyTint);
    }

    // ✅ Overload: GameObject VFX (Server Spawning)
    public void ApplySlow(float duration, float slowAmount, string sourceId, GameObject vfxPrefab, float vfxDuration, bool applyTint = true, VFXTargetType vfxTarget = VFXTargetType.Feet)
    {
        if (!IsServer) return;

        bool isStun = slowAmount >= 1f;
        if (isStun)
        {
            currentSlowMultiplier = 0f;
            Debug.Log($"{gameObject.name} stunned (100% slow) by {sourceId} for {duration}s");
            
            if (applyTint) ApplyColorTintClientRpc(slowAmount);
            ApplyAnimationSpeedClientRpc(0f);
            
            // Spawn Networked VFX (Server side)
            if (vfxPrefab != null)
            {
               Vector3 spawnPos = transform.position + GetVFXOffset(vfxTarget);
               GameObject vfxInstance = Instantiate(vfxPrefab, spawnPos, Quaternion.identity);
               
               NetworkObject netObj = vfxInstance.GetComponent<NetworkObject>();
               if (netObj != null)
               {
                   netObj.Spawn();
                   // FORCE PARENTING: Use NetworkObject.TrySetParent for networked parent-child
                   netObj.TrySetParent(transform, true);
                   serverSpawnedVFX.Add(netObj); 
                   StartCoroutine(DespawnVFXRoutine(netObj, vfxDuration));
               }
            }

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

        ApplyNormalSlow(duration, slowAmount, sourceId, applyTint);
    }

    private void ApplyNormalSlow(float duration, float slowAmount, string sourceId, bool applyTint)
    {
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
        Debug.Log($"{gameObject.name} slowed by {slowAmount * 100}% from {sourceId} for {duration}s");
    }

    private System.Collections.IEnumerator DespawnVFXRoutine(NetworkObject netObj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (netObj != null && netObj.IsSpawned) netObj.Despawn();
    }

    private void UpdateSlowEffects()
    {
        List<string> expiredSlows = new List<string>();
        foreach (var kvp in activeSlows)
        {
            if (Time.time >= kvp.Value.endTime) expiredSlows.Add(kvp.Key);
        }

        foreach (var sourceId in expiredSlows)
        {
            activeSlows.Remove(sourceId);
            if (currentVFXSource == sourceId) currentVFXSource = null;
        }

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

        float multiplier = 1f;
        float maxTintAmount = 0f;
        foreach (var slow in activeSlows.Values)
        {
            multiplier *= (1f - slow.slowAmount);
            if (slow.appliesTint) maxTintAmount = Mathf.Max(maxTintAmount, slow.slowAmount);
        }
        
        currentSlowMultiplier = multiplier;
        ApplyColorTintClientRpc(maxTintAmount);
        ApplyAnimationSpeedClientRpc(currentSlowMultiplier);
    }

    private void UpdateColorBasedOnSlows()
    {
        if (activeSlows.Count == 0)
        {
            ResetColorClientRpc();
            return;
        }

        float maxSlowAmount = 0f;
        foreach (var slow in activeSlows.Values) maxSlowAmount = Mathf.Max(maxSlowAmount, slow.slowAmount);
        ApplyColorTintClientRpc(maxSlowAmount);
    }

    [ClientRpc]
    private void ApplyColorTintClientRpc(float slowAmount)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        Color blueShade = new Color(
            1f - (slowAmount * 0.4f), 
            1f - (slowAmount * 0.3f), 
            1f + (slowAmount * 0.2f)  
        );
        
        spriteRenderer.color = originalColor * blueShade;
    }

    [ClientRpc]
    private void ResetColorClientRpc()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;
        spriteRenderer.color = originalColor;
    }

    [ClientRpc]
    private void ApplyAnimationSpeedClientRpc(float speedMultiplier)
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) return;
        animator.speed = speedMultiplier;
    }

    [ClientRpc]
    private void SpawnStatusVFXClientRpc(string sourceId, string vfxPrefabName, float vfxDuration, VFXTargetType targetType)
    {
        GameObject vfxPrefab = Resources.Load<GameObject>($"VFX/Prefabs/{vfxPrefabName}");
        if (vfxPrefab == null) return;

        // Spawn as child strictly
        GameObject vfxInstance = Instantiate(vfxPrefab, transform);
        vfxInstance.transform.localPosition = GetVFXOffset(targetType);
        
        SpriteRenderer vfxSprite = vfxInstance.GetComponent<SpriteRenderer>();
        if (vfxSprite != null) vfxSprite.sortingLayerName = "TransparentFX";

        AutoDestroyVFX autoDestroy = vfxInstance.GetComponent<AutoDestroyVFX>();
        if (autoDestroy == null) autoDestroy = vfxInstance.AddComponent<AutoDestroyVFX>();
        autoDestroy.lifetime = vfxDuration;
        
        activeVFXInstances.Add(vfxInstance);
    }

    protected virtual Vector3 GetVFXOffset(VFXTargetType targetType)
    {
        switch (targetType)
        {
            case VFXTargetType.Head:
                if (spriteRenderer != null)
                {
                    float yOffset = spriteRenderer.bounds.max.y - transform.position.y;
                    return new Vector3(-0.4f, yOffset - 0.2f, 0);
                }
                return new Vector3(0, 1.3f, 0);

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
    protected void ClearStatusEffectsClientRpc()
    {
        // 1. Reset Color & Speed explicitly
        ResetColorClientRpc();
        ApplyAnimationSpeedClientRpc(1f);
        
        // 2. Destroy attached VFX (Butter, Ice Block)
        CleanupLocalVFX();
        
        Debug.Log("Cleared all status effects/VFX on death.");
    }

    // Utility: Round to nearest multiple of 5
    private int RoundToNearestMultipleOf5(int value)
    {
        return Mathf.RoundToInt(value / 5f) * 5;
    }
}

