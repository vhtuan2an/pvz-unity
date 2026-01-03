using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class TwinSunflower : PlantBase
{
    [Header("Sun Production")]
    [SerializeField] private float sunProductionInterval = 24f;
    [SerializeField] private float sunBounceHeight = 0.5f;
    [SerializeField] private float sunBounceDuration = 0.3f;
    [SerializeField] private float sunDropDistance = 0.3f;
    [SerializeField] private float sunDropDuration = 0.2f;

    [Header("Twin Settings")]

    [SerializeField] private Vector3 sun1Offset = new Vector3(-0.3f, 0.2f, 0f);
    [SerializeField] private Vector3 sun2Offset = new Vector3(0.3f, 0.2f, 0f);




    private Animator animator;
    private float productionTimer = 0f;
    private bool isProducing = false;

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();
        
        Debug.Log($"TwinSunflower Start: interval={sunProductionInterval}s");
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
    }

    private void ProduceSun()
    {
        if (!IsServer) return;

        isProducing = true;
        TriggerProduceAnimationClientRpc(); 
    }

    // Called via Animation Event
    private void SpawnSun()
    {
        Debug.Log($"☀️ TwinSunflower SpawnSun animation event called (IsServer={IsServer})");
        RequestSpawnTwinSunServerRpc();
    }

    // Server spawns the suns
    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnTwinSunServerRpc()
    {
        if (SunSpawner.Instance == null || SunSpawner.Instance.sunPrefab == null)
        {
            Debug.LogWarning("SunSpawner not found or sunPrefab not assigned!");
            isProducing = false;
            SetIdleAnimationClientRpc();
            return;
        }

        SpawnSingleSun(sun1Offset);
        SpawnSingleSun(sun2Offset);

        // Finish
        isProducing = false;
        SetIdleAnimationClientRpc();
    }

    private void SpawnSingleSun(Vector3 localOffset)
    {
        // Calculate spawn position relative to plant
        Vector3 spawnPos = transform.position + localOffset;
        
        GameObject sun = Instantiate(SunSpawner.Instance.sunPrefab, spawnPos, Quaternion.identity);
        
        NetworkObject sunNetObj = sun.GetComponent<NetworkObject>();
        if (sunNetObj != null)
        {
            sunNetObj.Spawn(true);
            TriggerSunBounceClientRpc(sunNetObj.NetworkObjectId, spawnPos);
        }
        else
        {
            Destroy(sun);
        }
    }

    // Trigger bounce animation on all clients
    [ClientRpc]
    private void TriggerSunBounceClientRpc(ulong sunNetworkObjectId, Vector3 startPos)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(sunNetworkObjectId, out NetworkObject sunNetObj))
        {
            StartCoroutine(SunBounce(sunNetObj.transform, startPos));
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
        Gizmos.DrawWireSphere(transform.position + sun1Offset, 0.2f);
        Gizmos.DrawWireSphere(transform.position + sun2Offset, 0.2f);
    }
}
