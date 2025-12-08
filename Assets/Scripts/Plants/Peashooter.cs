using UnityEngine;
using Unity.Netcode;
using System.Collections;

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

    [Header("Burst Settings")]
    [SerializeField] private int peaAmount = 1;
    [SerializeField] private float burstDelay = 0.15f; // Delay between shots

    private float attackTimer = 0f;
    private Animator animator;
    private bool isShooting = false;

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();

        if (shootPoint == null)
            shootPoint = transform;

        Debug.Log($"Peashooter Start: HP={currentHealth}, projectilePrefab={projectilePrefab != null}, peaAmount={peaAmount}");
    }

    private void Update()
    {
        if (!IsServer)
            return;

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
                if (zombie.transform.position.x > detectionOrigin.x)
                {
                    float yDiff = Mathf.Abs(zombie.transform.position.y - detectionOrigin.y);
                    
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

    // ⭐ Called by Animation Event - spawns peas based on peaAmount
    private void SpawnPea()
    {
        if (!IsServer) return;
        
        Debug.Log($"📹 SpawnPea animation event called (peaAmount={peaAmount})");
        
        if (peaAmount == 1)
        {
            // Peashooter: Shoot 1 pea immediately
            ShootProjectile();
        }
        else
        {
            // Repeater/Multi-shot: Shoot multiple peas with burst delay
            StartCoroutine(ShootBurst());
        }
    }

    // Shoot multiple peas with delay between each
    private IEnumerator ShootBurst()
    {
        for (int i = 0; i < peaAmount; i++)
        {
            ShootProjectile();
            
            // Wait between shots (except for last shot)
            if (i < peaAmount - 1)
            {
                yield return new WaitForSeconds(burstDelay);
            }
        }
        
        Debug.Log($"✅ Burst complete: fired {peaAmount} peas");
    }

    private void ShootProjectile()
    {
        if (!IsServer)
            return;

        Debug.Log($"🎯 Peashooter SHOOTING pea from {transform.position}");

        if (projectilePrefab != null)
        {
            NetworkObject prefabNetObj = projectilePrefab.GetComponent<NetworkObject>();
            if (prefabNetObj == null)
            {
                Debug.LogError("⚠️ Projectile prefab missing NetworkObject component!");
                ResetShootingState();
                return;
            }

            Vector3 spawnPosition = transform.position + detectionOffset + new Vector3(0.5f, 0f, 0);
            GameObject pea = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);

            NetworkObject peaNetObj = pea.GetComponent<NetworkObject>();
            if (peaNetObj != null)
            {
                peaNetObj.Spawn(true);
                Debug.Log($"✅ Projectile spawned: NetworkObjectId={peaNetObj.NetworkObjectId}");
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
            animator.SetBool("isShooting", true);
    }

    [ClientRpc]
    private void SetIdleAnimationClientRpc()
    {
        if (animator != null)
            animator.SetBool("isShooting", false);
    }

    public override void TakeDamage(int damage)
    {
        base.TakeDamage(damage);

        if (animator != null)
            animator.SetTrigger("Hit");
    }

    private void OnDrawGizmosSelected()
    {
        if (shootPoint == null)
            shootPoint = transform;

        Vector3 detectionOrigin = transform.position + detectionOffset;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(detectionOrigin, Vector2.right * detectionRange);
        
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Vector3 boxCenter = detectionOrigin + new Vector3(detectionRange * 0.5f, 0f, 0f);
        Vector3 boxSize = new Vector3(detectionRange, laneHeight, 0.1f);
        
        Gizmos.DrawCube(boxCenter, boxSize);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(boxCenter, boxSize);
    }
}
