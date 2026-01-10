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

    [Header("Visuals")]
    [SerializeField] private GameObject targetMarkerPrefab;
    [SerializeField] private GameObject hitEffectPrefab;
    private GameObject instantiatedMarker;
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

        // Spawn Target Marker on Clients
        SpawnMarkerClientRpc(targetPos);
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

        // Visual Rotation (spin the target marker if it exists)
        if (instantiatedMarker != null)
        {
            instantiatedMarker.transform.Rotate(0, 0, 180f * Time.deltaTime); // Spin slower than apple? or same?
        }
    }

    void HitTarget()
    {
        isLaunched = false;

        // Visual effect or sound
        if (NetworkGameManager.Instance != null)
        {
             NetworkGameManager.Instance.PlaySoundClientRpc("splat"); // Reusing splat sound
        }

        // Destroy Marker
        DestroyMarkerClientRpc();
        
        // Spawn Hit Effect
        SpawnHitEffectClientRpc(transform.position);

        // Damage logic
        // 1. Try specific target first (if assigned)
        if (specificTarget != null)
        {
            PlantBase plant = specificTarget.GetComponent<PlantBase>();
            if (plant != null)
            {
                plant.TakeDamage(damage);
                Debug.Log($"Apple hit specific target {plant.name}");
            }
            else
            {
                CheckAreaDamage(targetPos);
            }
        }
        else
        {
            // If no specific target (Tile Targeting), always check area
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

    [ClientRpc]
    private void SpawnMarkerClientRpc(Vector3 pos)
    {
        if (targetMarkerPrefab != null)
        {
            instantiatedMarker = Instantiate(targetMarkerPrefab, pos, Quaternion.identity);
        }
    }

    [ClientRpc]
    private void DestroyMarkerClientRpc()
    {
        if (instantiatedMarker != null)
        {
            Destroy(instantiatedMarker);
        }
    }

    public float hitVfxDuration = 1.0f; // New: Configurable VFX duration
    
    [ClientRpc]
    private void SpawnHitEffectClientRpc(Vector3 pos)
    {
        if (hitEffectPrefab != null)
        {
            GameObject hitVFX = Instantiate(hitEffectPrefab, pos, Quaternion.identity);
            
            // If the prefab doesn't have auto-destroy, let's add it manually as a fallback
            AutoDestroyVFX autoDestroy = hitVFX.GetComponent<AutoDestroyVFX>();
            if (autoDestroy == null)
            {
                autoDestroy = hitVFX.AddComponent<AutoDestroyVFX>();
            }
            
            // Apply config duration
            autoDestroy.lifetime = hitVfxDuration;

            // Fix Sorting (prevent flickering/z-fighting)
            SpriteRenderer sr = hitVFX.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = 300; // High value to sit on top of everything
                // sr.sortingLayerName = "Projectiles"; // Optional, usually Default is fine if order is high
            }
        }
    }
    public override void OnDestroy()
    {
        base.OnDestroy();
        if (instantiatedMarker != null)
        {
            Destroy(instantiatedMarker);
        }
    }
}
