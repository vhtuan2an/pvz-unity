using UnityEngine;
using Unity.Netcode;

public class Peashooter : PlantBase
{
    [Header("Combat")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private float attackRate = 1.5f;
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private LayerMask zombieLayer;
    [SerializeField] private float laneHeight = 1.3f;
    [SerializeField] private Vector3 detectionOffset = new Vector3(-0.1f, 0.75f, 0f);

    private float attackTimer = 0f;
    private Animator animator;
    private bool isShooting = false;

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();

        if (shootPoint == null)
        {
            shootPoint = transform;
        }

        Debug.Log($"Peashooter Start: projectilePrefab={projectilePrefab != null}, shootPoint={shootPoint != null}");
    }

    private void Update()
    {
        // Only server checks for zombies and shoots
        if (!IsServer)
            return;

        // Only increment timer when not shooting
        if (!isShooting)
        {
            attackTimer += Time.deltaTime;

            if (attackTimer >= attackRate)
            {
                if (CheckForZombies())
                {
                    TriggerShoot();
                }
                else
                {
                    // Reset timer to keep checking even with no zombies
                    attackTimer = 0f;
                }
            }
        }
    }

    private void TriggerShoot()
    {
        if (!IsServer || isShooting)
            return;
        isShooting = true;
        // Trigger animation on all clients
        TriggerShootAnimationClientRpc();
    }

    private bool CheckForZombies()
    {
        GameObject[] zombies = GameObject.FindGameObjectsWithTag("Zombie");

        if (zombies.Length > 0)
        {
            Vector3 detectionOrigin = transform.position + detectionOffset;
            
            foreach (var zombie in zombies)
            {
                // Check if zombie is to the right of detection origin
                if (zombie.transform.position.x > detectionOrigin.x)
                {
                    float yDiff = Mathf.Abs(zombie.transform.position.y - detectionOrigin.y);
                    
                    // If zombie is within half lane height, consider it on same lane
                    if (yDiff < (laneHeight * 0.5f))
                    {
                        float distance = zombie.transform.position.x - detectionOrigin.x;
                        if (distance <= detectionRange)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    // Called by Animation Event
    private void SpawnPea()
    {
        if (!IsServer)
            return;

        Debug.Log($"📌 SpawnPea called by Animation Event");
        ShootProjectile();
    }

    private void ShootProjectile()
    {
        if (!IsServer)
            return;

        Debug.Log($"🎯 Peashooter SHOOTING from {transform.position}");

        if (projectilePrefab != null)
        {
            // Check if prefab has NetworkObject
            NetworkObject prefabNetObj = projectilePrefab.GetComponent<NetworkObject>();
            if (prefabNetObj == null)
            {
                Debug.LogError("⚠️ Projectile prefab missing NetworkObject component!");
                ResetShootingState();
                return;
            }

            // Spawn pea from detection origin position
            Vector3 spawnPosition = transform.position + detectionOffset + new Vector3(0.5f, 0f, 0);
            GameObject pea = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);

            NetworkObject peaNetObj = pea.GetComponent<NetworkObject>();
            if (peaNetObj != null)
            {
                // Spawn with server ownership to sync to all clients
                peaNetObj.Spawn(true);
                Debug.Log($"✅ Projectile spawned: NetworkObjectId={peaNetObj.NetworkObjectId}, IsSpawned={peaNetObj.IsSpawned}");
            }
            else
            {
                Debug.LogWarning("⚠️ Projectile instance missing NetworkObject component!");
                Destroy(pea);
                ResetShootingState();
            }
        }
        else
        {
            Debug.LogError("⚠️ Projectile prefab is null!");
            ResetShootingState();
        }
    }
    
    // Called at end of shoot animation via Animation Event
    private void OnShootAnimationComplete()
    {
        if (!IsServer)
            return;        
        Debug.Log("🎬 Shoot animation complete");
        ResetShootingState();
    }

    private void ResetShootingState()
    {
        isShooting = false;
        attackTimer = 0f;
        SetIdleAnimationClientRpc();
    }

    [ClientRpc]
    private void TriggerShootAnimationClientRpc()
    {
        if (animator != null)
        {
            animator.SetBool("isShooting", true);
        }
    }

    [ClientRpc]
    private void SetIdleAnimationClientRpc()
    {
        if (animator != null)
        {
            animator.SetBool("isShooting", false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (shootPoint == null)
            shootPoint = transform;

        // Use detection offset for visualization
        Vector3 detectionOrigin = transform.position + detectionOffset;

        // Draw detection ray from visual center
        Gizmos.color = Color.red;
        Gizmos.DrawRay(detectionOrigin, Vector2.right * detectionRange);
        
        // Draw lane detection zone - starts from visual center
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Vector3 boxCenter = detectionOrigin + new Vector3(detectionRange * 0.5f, 0f, 0f);
        Vector3 boxSize = new Vector3(detectionRange, laneHeight, 0.1f);
        
        Gizmos.DrawCube(boxCenter, boxSize);
        
        // Draw wireframe for clarity
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(boxCenter, boxSize);
    }
}