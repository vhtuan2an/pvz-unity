using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Peashooter : PlantBase
{
    [Header("Combat")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private float attackRate = 1.5f;
    [SerializeField] private float detectionRange = 12f;
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
                    
                    if (yDiff <= (laneHeight * 0.5f) + 0.05f)
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
        if (!IsServer) return;

        StartCoroutine(ShootBurst());

        IEnumerator ShootBurst()
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
        }
    }

    private void ShootProjectile()
    {
        if (!IsServer)
            return;

        if (projectilePrefab != null)
        {
            NetworkObject prefabNetObj = projectilePrefab.GetComponent<NetworkObject>();
            if (prefabNetObj == null)
            {
                Debug.LogError("⚠️ Projectile prefab missing NetworkObject component!");
                ResetShootingState();
                return;
            }

            Vector3 spawnPosition = shootPoint != null ? shootPoint.position : (transform.position + detectionOffset + new Vector3(0.5f, 0f, 0));
            GameObject pea = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);

            NetworkObject peaNetObj = pea.GetComponent<NetworkObject>();
            if (peaNetObj != null)
            {
                peaNetObj.Spawn(true);
                NetworkGameManager.Instance.PlaySoundClientRpc("pea_shoot");
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