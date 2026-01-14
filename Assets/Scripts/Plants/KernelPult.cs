using UnityEngine;
using Unity.Netcode;

public class KernelPult : PlantBase
{
    [Header("Combat")]
    [SerializeField] private GameObject kernelProjectilePrefab;
    [SerializeField] private GameObject butterProjectilePrefab;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private float attackRate = 2.9f; // Slower than peashooter usually
    [SerializeField] private float detectionRange = 12f;
    [SerializeField] private float laneHeight = 1.3f;
    [SerializeField] private Vector3 detectionOffset = new Vector3(-0.1f, 0.75f, 0f); // Adjust as needed
    
    [Header("Kernel Pult Settings")]
    [SerializeField] private float butterChance = 0.25f; // 25% chance

    private float attackTimer = 0f;
    private Animator animator;
    private bool isAttacking = false;
    private GameObject currentTargetZombie;

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();
        if (shootPoint == null) shootPoint = transform;
    }

    private void Update()
    {
        if (!IsServer) return;

        if (!isAttacking)
        {
            attackTimer += Time.deltaTime;

            if (attackTimer >= attackRate)
            {
                currentTargetZombie = FindTargetZombie();
                if (currentTargetZombie != null)
                {
                    TriggerShoot();
                }
                else
                {
                    attackTimer = 0f; // Keep resetting if no zombie found? Or wait at full charge?
                    // Usually wait at full charge is better but let's stick to simple timer
                }
            }
        }
    }

    private GameObject FindTargetZombie()
    {
        // Simple linecast/boxcast logic similar to Peashooter but maybe targeting specific zombie
        // For lobbed plants, they can shoot over shields but we just need a target reference
        GameObject[] zombies = GameObject.FindGameObjectsWithTag("Zombie");
        GameObject closestZombie = null;
        float minDistance = Mathf.Infinity;

        Vector3 detectionOrigin = transform.position + detectionOffset;

        foreach (var zombie in zombies)
        {
            // Simple lane check
            if (Mathf.Abs(zombie.transform.position.y - detectionOrigin.y) <= (laneHeight * 0.5f) + 0.05f)
            {
                // Must be to the right
                if (zombie.transform.position.x > detectionOrigin.x)
                {
                    float distance = zombie.transform.position.x - detectionOrigin.x;
                    if (distance <= detectionRange && distance < minDistance)
                    {
                        minDistance = distance;
                        closestZombie = zombie;
                    }
                }
            }
        }
        return closestZombie;
    }

    private bool nextShotIsButter = false;

    private void TriggerShoot()
    {
        if (!IsServer || isAttacking) return;

        isAttacking = true;
        
        // Decide ammo type on Server before animation starts
        nextShotIsButter = Random.value < butterChance;
        
        // Trigger exclusive animation on clients via Bools
        // if nextShotIsButter is true -> isAttacking=false, isButtering=true
        // if nextShotIsButter is false -> isAttacking=true, isButtering=false
        UpdateAnimationStateClientRpc(!nextShotIsButter, nextShotIsButter);
    }

    // Called by Animation Event "Shoot"
    public void LaunchProjectile()
    {
        if (!IsServer) return;

        // Re-verify target existence (it might have died during animation)
        if (currentTargetZombie == null)
        {
            ResetAttackState();
            return;
        }

        // Use the pre-decided ammo type so it matches the animation
        GameObject prefabToUse = nextShotIsButter ? butterProjectilePrefab : kernelProjectilePrefab;

        if (prefabToUse != null)
        {
             // Spawn projectile
            if (NetworkGameManager.Instance != null)
                NetworkGameManager.Instance.PlaySoundClientRpc("kernelpult");

            Vector3 spawnPos = shootPoint.position;
            GameObject projObj = Instantiate(prefabToUse, spawnPos, Quaternion.identity);
            
            NetworkObject netObj = projObj.GetComponent<NetworkObject>();
            if (netObj != null) 
            {
                netObj.Spawn(true);
                
                LobProjectile lobProj = projObj.GetComponent<LobProjectile>();
                if (lobProj != null)
                {
                    // Target the zombie's visual center (Sprite center)
                    Vector3 targetPos = currentTargetZombie.transform.position;
                    
                    // Try to get sprite bounds top (Head/Outer Line)
                    SpriteRenderer sr = currentTargetZombie.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        // Target X center, but Y at the top (outer line)
                        // Subtracting a tiny bit to ensure it overlaps slightly
                        targetPos = new Vector3(sr.bounds.min.x, sr.bounds.max.y - 0.2f, 0f);
                    }
                    else
                    {
                        // Fallback: assume visual center is ~1.0 unit up (Head approx)
                        targetPos += new Vector3(0, 1.0f, 0); 
                    }
                    
                    lobProj.Initialize(currentTargetZombie.transform, targetPos);
                }
                else
                {
                    Debug.LogWarning("KernelPult projectile missing LobProjectile script!");
                }
            }
        }
    }
    
    // Called by Animation Event "ShootEnd" or similar
    public void OnAttackAnimationComplete()
    {
        if (!IsServer) return;
        ResetAttackState();
    }

    private void ResetAttackState()
    {
        isAttacking = false;
        attackTimer = 0f;
        UpdateAnimationStateClientRpc(false, false);
    }

    [ClientRpc]
    private void UpdateAnimationStateClientRpc(bool isAttacking, bool isButtering)
    {
        if (animator != null)
        {
            animator.SetBool("isAttacking", isAttacking);
            animator.SetBool("isButtering", isButtering);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 detectionOrigin = transform.position + detectionOffset;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(detectionOrigin + Vector3.right * (detectionRange * 0.5f), new Vector3(detectionRange, laneHeight, 1f));
    }
}
