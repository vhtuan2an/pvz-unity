using UnityEngine;

public class Wallnut : PlantBase
{
    [Header("Wallnut Settings")]
    // default values can be adjusted per-prefab in the Inspector if needed
    public int defaultHealth = 4000;

    protected override void Start()
    {
        // override base health for Wallnut
        maxHealth = defaultHealth;
        base.Start();
    }

    // Optional hooks for visual/animation updates or interactions can be added here.
}