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

    private float attackTimer = 0f;
    private Animator animator;

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
        // ✅ CHỈ SERVER mới kiểm tra zombie và bắn
        if (!IsServer)
            return;

        attackTimer += Time.deltaTime;

        if (attackTimer >= attackRate)
        {
            if (CheckForZombies())
            {
                TriggerShoot();
            }
            else
            {
                SetIdleAnimationClientRpc();
            }
            attackTimer = 0f;
        }
    }

    private void TriggerShoot()
    {
        if (!IsServer)
            return;

        // Trigger animation on all clients
        TriggerShootAnimationClientRpc();
    }

    private bool CheckForZombies()
    {
        GameObject[] zombies = GameObject.FindGameObjectsWithTag("Zombie");

        if (zombies.Length > 0)
        {
            foreach (var zombie in zombies)
            {
                if (zombie.transform.position.x > shootPoint.position.x && zombie.transform.position.y == this.transform.position.y)
                {
                    float distance = zombie.transform.position.x - shootPoint.position.x;
                    if (distance <= detectionRange)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    // Called by Animation Event at the exact frame when pea should spawn
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
            // ✅ Check if prefab has NetworkObject
            NetworkObject prefabNetObj = projectilePrefab.GetComponent<NetworkObject>();
            if (prefabNetObj == null)
            {
                Debug.LogError("⚠️ Projectile prefab missing NetworkObject component!");
                return;
            }

            // Offset spawn position (adjust values as needed)
            Vector3 spawnPosition = shootPoint.position + new Vector3(0.5f, 0.3f, 0);
            GameObject pea = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);

            NetworkObject peaNetObj = pea.GetComponent<NetworkObject>();
            if (peaNetObj != null)
            {
                // ✅ Spawn với Server ownership - sẽ sync tới TẤT CẢ clients
                peaNetObj.Spawn(true); // true = destroy with scene
                Debug.Log($"✅ Projectile spawned: NetworkObjectId={peaNetObj.NetworkObjectId}, IsSpawned={peaNetObj.IsSpawned}");
            }
            else
            {
                Debug.LogWarning("⚠️ Projectile instance missing NetworkObject component!");
                Destroy(pea);
            }
        }
        else
        {
            Debug.LogError("⚠️ Projectile prefab is null!");
        }
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

        Gizmos.color = Color.red;
        Gizmos.DrawRay(shootPoint.position, Vector2.right * detectionRange);
    }
}