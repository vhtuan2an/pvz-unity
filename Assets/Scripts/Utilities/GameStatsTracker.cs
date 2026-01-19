using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class GameStatsTracker : NetworkBehaviour
{
    public static GameStatsTracker Instance { get; private set; }

    [Header("Settings - Thresholds")]
    public float unitRatioThreshold = 2f;      // Double the units = losing
    public float resourceRatioThreshold = 3f;  // Triple the resources = losing
    public float plantHouseX = -8.5f;          // Panic threshold for plants

    [Header("Results - Resource Refunds")]
    [Range(0f, 1f)] public float plantRefundPercent = 0.25f;
    [Range(0f, 1f)] public float zombieRefundPercent = 0.20f;

    [Header("Results - Passive Income")]
    public int bonusIncomeAmount = 25;
    public float bonusIncomeInterval = 2.0f;

    [Header("Results - Cooldown Reductions")]
    public float comebackCDR = 0.85f;          // 15% reduction
    public float zombieHeavyOutnumberedCDR = 0.8f; // 20% reduction (for 10:1 ratio)

    private float statsLogTimer = 0f;

    // Current State
    private List<PlantBase> activePlants = new List<PlantBase>();
    private List<ZombieBase> activeZombies = new List<ZombieBase>();

    public int PlantCount => activePlants.Count;
    public int ZombieCount => activeZombies.Count;

    public int TotalPlantValue
    {
        get
        {
            // Clean up null entries first
            activePlants.RemoveAll(p => p == null);
            
            int total = 0;
            foreach (var p in activePlants)
            {
                if (p != null) total += p.sunCost;
            }
            return total;
        }
    }

    public int TotalZombieValue
    {
        get
        {
            // Clean up null entries first
            activeZombies.RemoveAll(z => z == null);
            
            int total = 0;
            foreach (var z in activeZombies)
            {
                if (z != null) total += z.GetBrainCost();
            }
            return total;
        }
    }

    // Comeback Flags (Weighted by resource value)
    public bool IsPlantLosingUnits => TotalZombieValue > 0 && (float)TotalPlantValue / TotalZombieValue < (1f / unitRatioThreshold);
    public bool IsZombieLosingUnits => TotalPlantValue > 0 && (float)TotalZombieValue / TotalPlantValue < (1f / unitRatioThreshold);
    
    // 10:1 ratio for zombie desolation boost as requested
    public bool IsZombieHeavilyOutnumbered => TotalPlantValue > 0 && (float)TotalZombieValue / TotalPlantValue < 0.1f;

    public bool IsHouseThreatened
    {
        get
        {
            foreach (var z in activeZombies)
            {
                if (z != null && z.transform.position.x < plantHouseX + 2f) return true;
            }
            return false;
        }
    }

    public bool IsPlantBroke => PlantManager.Instance != null && ZombieManager.Instance != null && 
                                PlantManager.Instance.currentSun.Value < (ZombieManager.Instance.currentBrains.Value / resourceRatioThreshold);
                               
    public bool IsZombieBroke => PlantManager.Instance != null && ZombieManager.Instance != null && 
                                 ZombieManager.Instance.currentBrains.Value < (PlantManager.Instance.currentSun.Value / resourceRatioThreshold);

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        if (!IsServer) return;

        statsLogTimer += Time.deltaTime;
        if (statsLogTimer >= 10f)
        {
            statsLogTimer = 0f;
            Debug.Log($"<color=orange>[STATS]</color> Field Value - Plants: {TotalPlantValue} | Zombies: {TotalZombieValue} (Ratio: {(TotalZombieValue > 0 ? ((float)TotalPlantValue / TotalZombieValue).ToString("F1") : "Inf")})");
        }
    }

    // --- Registration ---
    public void RegisterPlant(PlantBase plant)
    {
        if (!activePlants.Contains(plant))
        {
            activePlants.Add(plant);
            Debug.Log($"<color=cyan>[STATS]</color> Registered Plant: {plant.name}. New Count: {PlantCount}");
        }
    }

    public void UnregisterPlant(PlantBase plant)
    {
        if (activePlants.Remove(plant))
        {
            Debug.Log($"<color=cyan>[STATS]</color> Unregistered Plant: {plant.name}. New Count: {PlantCount}");
        }
    }

    public void RegisterZombie(ZombieBase zombie)
    {
        if (!activeZombies.Contains(zombie))
        {
            activeZombies.Add(zombie);
            Debug.Log($"<color=green>[STATS]</color> Registered Zombie: {zombie.name}. New Count: {ZombieCount}");
        }
    }

    public void UnregisterZombie(ZombieBase zombie)
    {
        if (activeZombies.Remove(zombie))
        {
            Debug.Log($"<color=green>[STATS]</color> Unregistered Zombie: {zombie.name}. New Count: {ZombieCount}");
        }
    }
}
