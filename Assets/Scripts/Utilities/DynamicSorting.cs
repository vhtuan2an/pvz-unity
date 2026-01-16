using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer))]
public class DynamicSorting : MonoBehaviour
{
    [Header("Sorting Settings")]
    [Tooltip("Base offset to start sorting from.")]
    [SerializeField] private int baseOrder = 5000;

    [Tooltip("Multiplier for Y sorting weight relative to X. Higher means Y is more important.")]
    [SerializeField] private float laneWeight = 1000f;

    // Static list to track all active sorters
    private static List<DynamicSorting> activeSorters = new List<DynamicSorting>();
    private static int lastUpdateFrame = -1;

    private SpriteRenderer sr;
    
    // Cached sort value to avoid recalculating transform multiple times during generic sort if needed
    // But direct transform access is usually fine for these counts.
    public float SortMetric { get; private set; } 

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        if (!activeSorters.Contains(this))
        {
            activeSorters.Add(this);
        }
    }

    void OnDisable()
    {
        activeSorters.Remove(this);
    }

    void LateUpdate()
    {
        // Run global sort ONE TIME per frame
        if (Time.frameCount != lastUpdateFrame)
        {
            lastUpdateFrame = Time.frameCount;
            ResolveGlobalSorting();
        }
    }

    private static void ResolveGlobalSorting()
    {
        // 1. Update Metrics
        // We want:
        // Lower Y = Higher Priority (Closer to camera) -> Higher Sorting Order
        // Higher X = Higher Priority (Slightly closer/right) -> Higher Sorting Order
        
        // Formula: -Y * 1000 + X
        // Example: 
        // A: Y=-4, X=0 -> 4000
        // B: Y=-3, X=0 -> 3000
        // A > B -> A is drawn ON TOP of B. Correct.
        
        foreach (var sorter in activeSorters)
        {
            if (sorter == null) continue;
            
            // Calculate metric
            // We use -y because lower Y is "front"
            sorter.SortMetric = (-sorter.transform.position.y * sorter.laneWeight) + sorter.transform.position.x;
        }

        // 2. Sort the list based on metric
        // Sort Ascending? 
        // If Metric A < Metric B, A comes first.
        // We want Higher Metric = Higher Sorting Order.
        // So simple ascending sort of the LIST means index 0 is lowest metric.
        // If we assign Order = Base + Index, then Higher Index = Higher Order = Higher Metric.
        // So Sort Ascending is correct.
        
        activeSorters.Sort(SortComparison);

        // 3. Assign unique orders
        for (int i = 0; i < activeSorters.Count; i++)
        {
            var sorter = activeSorters[i];
            if (sorter != null && sorter.sr != null)
            {
                sorter.sr.sortingOrder = sorter.baseOrder + i;
            }
        }
    }

    private static int SortComparison(DynamicSorting a, DynamicSorting b)
    {
        if (a.SortMetric < b.SortMetric) return -1;
        if (a.SortMetric > b.SortMetric) return 1;
        
        // Tie-breaker: InstanceID (stable random) to ensure strict uniqueness
        // We don't want them flickering if they overlap exactly
        return a.GetInstanceID().CompareTo(b.GetInstanceID());
    }
}
