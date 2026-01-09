using UnityEngine;
using Unity.Netcode;

public class AppleProjectile : NetworkBehaviour
{
    [Header("Settings")]
    public int damage = 20;
    public float speed = 10f;
    public float arcHeight = 3f;

    private Vector3 startPos;
    private Vector3 targetPos;
    private float flightDuration;
    private float flightTimer;
    private bool isLaunched = false;

    // Optional: Target object for direct hit check
    private Transform specificTarget;

    public void Launch(Vector3 targetPosition, Transform targetTransform = null)
    {
        startPos = transform.position;
        targetPos = targetPosition;
        specificTarget = targetTransform;

        float distance = Vector3.Distance(startPos, targetPos);
        flightDuration = distance / speed;
        flightTimer = 0f;
        isLaunched = true;
    }

    void Update()
    {
        if (!IsServer || !isLaunched) return;

        flightTimer += Time.deltaTime;
        float t = flightTimer / flightDuration;

        if (t >= 1f)
        {
            HitTarget();
            return;
        }

        // Parabolic movement
        Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
        currentPos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

        transform.position = currentPos;
        
        // Visual Rotation (spin the apple)
        transform.Rotate(0, 0, -360f * Time.deltaTime);
    }

    void HitTarget()
    {
        isLaunched = false;

        // Visual effect or sound
        if (NetworkGameManager.Instance != null)
        {
             NetworkGameManager.Instance.PlaySoundClientRpc("splat"); // Reusing splat sound
        }

        // Damage logic
        // 1. Try specific target first
        if (specificTarget != null)
        {
            // Try get PlantBase (or whatever takes damage, might be PlantBase logic)
            // Note: Plants usually don't have a specific 'TakeDamage' in the same way Zombies do in this codebase?
            // Need to check how zombies damage plants. Usually via 'Eat' or 'Projectile'.
            // Plants have 'TakeDamage'.
            
            PlantBase plant = specificTarget.GetComponent<PlantBase>();
            if (plant != null)
            {
                plant.TakeDamage(damage);
                Debug.Log($"Apple hit specific target {plant.name}");
            }
            else
            {
                // Should do a small area check if specific target is missing?
                CheckAreaDamage(targetPos);
            }
        }
        else
        {
            CheckAreaDamage(targetPos);
        }

        DespawnProjectile();
    }

    void CheckAreaDamage(Vector3 pos)
    {
        // Check for plants at this position
        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, 0.5f);
        foreach (var hit in hits)
        {
            PlantBase plant = hit.GetComponent<PlantBase>();
            if (plant != null)
            {
                plant.TakeDamage(damage);
                Debug.Log($"Apple hit area target {plant.name}");
                break; // Hit one plant
            }
        }
    }

    private void DespawnProjectile()
    {
        if (IsServer)
        {
            GetComponent<NetworkObject>().Despawn();
            Destroy(gameObject);
        }
    }
}
