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
        // Only server handles logic
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

        TriggerShootAnimationClientRpc();
    }

    // ⭐ FIXED VERSION USING RAYCAST2D ⭐
    private bool CheckForZombies()
    {
        if (shootPoint == null)
            shootPoint = transform;

        // Raycast về hướng phải
        RaycastHit2D hit = Physics2D.Raycast(shootPoint.position, Vector2.right, detectionRange, zombieLayer);

        // Debug: hiển thị ray trong Scene view
        Debug.DrawRay(shootPoint.position, Vector2.right * detectionRange, Color.red);

        if (hit.collider != null)
        {
            Debug.Log($"🎯 Zombie detected: {hit.collider.name}");
            return true;
        }

        return false;
    }

    private void SpawnPea()  // called by animation event
    {
        if (!IsServer)
            return;

        Debug.Log("📌 SpawnPea animation event");
        ShootProjectile();
    }

    private void ShootProjectile()
    {
        if (!IsServer)
            return;

        Debug.Log($"🎯 Peashooter SHOOTING from {transform.position}");

        if (projectilePrefab != null)
        {
            NetworkObject prefabNetObj = projectilePrefab.GetComponent<NetworkObject>();
            if (prefabNetObj == null)
            {
                Debug.LogError("⚠️ Projectile prefab missing NetworkObject component!");
                return;
            }

            Vector3 spawnPosition = shootPoint.position + new Vector3(0.5f, 0.3f, 0);
            GameObject pea = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);

            NetworkObject peaNetObj = pea.GetComponent<NetworkObject>();
            if (peaNetObj != null)
            {
                peaNetObj.Spawn(true);
                Debug.Log($"✅ Projectile spawned: {peaNetObj.NetworkObjectId}");
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
