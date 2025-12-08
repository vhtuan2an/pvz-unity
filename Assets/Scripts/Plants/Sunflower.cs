using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Sunflower : PlantBase
{
    [Header("Sun Production")]
    [SerializeField] private float sunProductionInterval = 24f;
    [SerializeField] private float sunBounceHeight = 0.5f;
    [SerializeField] private float sunBounceDuration = 0.3f;
    [SerializeField] private float sunDropDistance = 0.3f;
    [SerializeField] private float sunDropDuration = 0.2f;

    [Header("Animation")]
    [SerializeField] private float minBlinkInterval = 5f;
    [SerializeField] private float maxBlinkInterval = 15f;

    private Animator animator;
    private float productionTimer = 0f;
    private float nextBlinkTime;
    private bool isProducing = false;

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();
        
        // Schedule first blink
        ScheduleNextBlink();
        
        Debug.Log($"Sunflower Start: interval={sunProductionInterval}s");
    }

    private void Update()
    {
        if (!IsServer) return;

        // Sun production timer
        productionTimer += Time.deltaTime;

        if (productionTimer >= sunProductionInterval && !isProducing)
        {
            ProduceSun();
            productionTimer = 0f;
        }

        // Blink animation check - ONLY if not producing sun
        if (!isProducing && Time.time >= nextBlinkTime)
        {
            TriggerBlinkClientRpc();
            ScheduleNextBlink();
        }
    }

    private void ProduceSun()
    {
        if (!IsServer) return;

        isProducing = true;
        TriggerProduceAnimationClientRpc(); 
    }

    private void SpawnSun()
    {
        Debug.Log($"☀️ SpawnSun animation event called (IsServer={IsServer})");
        
        // Request server to spawn sun
        RequestSpawnSunServerRpc();
    }

    // Server spawns the sun
    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnSunServerRpc()
    {
        Debug.Log($"☀️ SERVER: RequestSpawnSunServerRpc called");
        
        if (SunSpawner.Instance == null || SunSpawner.Instance.sunPrefab == null)
        {
            Debug.LogWarning("SunSpawner not found or sunPrefab not assigned!");
            isProducing = false;
            SetIdleAnimationClientRpc();
            return;
        }
        
        Vector3 spawnPos = transform.position;
        GameObject sun = Instantiate(SunSpawner.Instance.sunPrefab, spawnPos, Quaternion.identity);
        
        NetworkObject sunNetObj = sun.GetComponent<NetworkObject>();
        if (sunNetObj != null)
        {
            sunNetObj.Spawn(true);
            Debug.Log($"✅ Sun spawned: NetworkObjectId={sunNetObj.NetworkObjectId}");
            
            // Trigger bounce animation on all clients
            TriggerSunBounceClientRpc(sunNetObj.NetworkObjectId, spawnPos);
        }
        else
        {
            Debug.LogWarning("⚠️ Sun prefab missing NetworkObject component!");
            Destroy(sun);
        }
        
        isProducing = false;
        SetIdleAnimationClientRpc();
    }

    // Trigger bounce animation on all clients
    [ClientRpc]
    private void TriggerSunBounceClientRpc(ulong sunNetworkObjectId, Vector3 startPos)
    {
        Debug.Log($"☀️ CLIENT: TriggerSunBounceClientRpc for sun {sunNetworkObjectId}");
        
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(sunNetworkObjectId, out NetworkObject sunNetObj))
        {
            StartCoroutine(SunBounce(sunNetObj.transform, startPos));
        }
        else
        {
            Debug.LogWarning($"⚠️ Could not find sun {sunNetworkObjectId}");
        }
    }

    private IEnumerator SunBounce(Transform sunTransform, Vector3 startPos)
    {
        if (sunTransform == null) yield break;

        // Disable sun rigidbody during animation
        Rigidbody2D rb = sunTransform.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        // Bounce up
        Vector3 peakPos = startPos + Vector3.up * sunBounceHeight;
        float elapsedTime = 0f;

        while (elapsedTime < sunBounceDuration && sunTransform != null)
        {
            float t = elapsedTime / sunBounceDuration;
            float easeT = 1f - Mathf.Pow(1f - t, 2f);
            sunTransform.position = Vector3.Lerp(startPos, peakPos, easeT);
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (sunTransform == null) yield break;

        // Drop down slightly
        Vector3 finalPos = peakPos + Vector3.down * sunDropDistance;
        elapsedTime = 0f;

        while (elapsedTime < sunDropDuration && sunTransform != null)
        {
            float t = elapsedTime / sunDropDuration;
            float easeT = t * t;
            sunTransform.position = Vector3.Lerp(peakPos, finalPos, easeT);
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (sunTransform == null) yield break;

        // Stay at final position
        sunTransform.position = finalPos;
        Debug.Log($"☀️ Sun bounce complete at {finalPos}");
    }

    private void ScheduleNextBlink()
    {
        nextBlinkTime = Time.time + Random.Range(minBlinkInterval, maxBlinkInterval);
        Debug.Log($"🌻 Next blink scheduled in {nextBlinkTime - Time.time:F1}s");
    }

    [ClientRpc]
    private void TriggerBlinkClientRpc()
    {
        if (animator != null)
        {
            animator.SetTrigger("Blink");
            Debug.Log("🌻 Sunflower blink animation triggered");
        }
    }

    [ClientRpc]
    private void TriggerProduceAnimationClientRpc()
    {
        if (animator != null)
        {
            animator.SetBool("isProducing", true);
        }

    }
    
    [ClientRpc]
    private void SetIdleAnimationClientRpc()
    {
        if (animator != null)
        {
            animator.SetBool("isProducing", false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 spawnPos = transform.position;
        Gizmos.DrawWireSphere(spawnPos, 0.2f);
        
        // Use custom orange color (RGB: 255, 165, 0)
        Gizmos.color = new Color(1f, 0.65f, 0f);
        Vector3 peakPos = spawnPos + Vector3.up * sunBounceHeight;
        Gizmos.DrawWireSphere(peakPos, 0.15f);
        
        Gizmos.color = Color.red;
        Vector3 finalPos = peakPos + Vector3.down * sunDropDistance;
        Gizmos.DrawWireSphere(finalPos, 0.15f);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(spawnPos, peakPos);
        Gizmos.DrawLine(peakPos, finalPos);
    }
}