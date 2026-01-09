/*using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class ZombieBoss : ZombieBase
{
    private enum BossState { Idle, Moving, Attacking, Summoning, GlobalAttack }
    private BossState currentState = BossState.Idle;

    [Header("Boss Settings")]
    public float patrolIntervalMin = 5f;
    public float patrolIntervalMax = 10f;
    public float attackRange = 3f;     // Range to trigger normal attack
    public float appleCooldown = 2f;   // Cooldown for normal attack
    
    [Header("Global Attack Settings")]
    public float globalAttackIntervalMin = 45f;
    public float globalAttackIntervalMax = 60f;
    public GameObject appleProjectilePrefab;

    [Header("Patrol Area")]
    public int minCol = 7;
    public int maxCol = 9;

    private float patrolTimer;
    private float globalAttackTimer;
    private float normalAttackTimer;
    private Vector3 moveTarget;
    private bool isMovingToLane = false;

    // References
    private Animator animator;

    // Lane Management
    private int currentLaneIndex = 0; // Need to track this roughly

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();

        // Start timers
        patrolTimer = Random.Range(patrolIntervalMin, patrolIntervalMax);
        globalAttackTimer = Random.Range(globalAttackIntervalMin, globalAttackIntervalMax);

        // Subscribe to spawn event
        if (ZombieManager.Instance != null)
        {
            ZombieManager.Instance.OnZombieSpawnEvent += TriggerSummonAnimation;
        }

        // Determine initial lane based on Y position (rough approximation for now)
        // Ideally we snap to nearest lane on spawn
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (ZombieManager.Instance != null)
        {
            ZombieManager.Instance.OnZombieSpawnEvent -= TriggerSummonAnimation;
        }
    }

    protected override void Update()
    {
        if (!IsServer) return; // Logic only runs on server
        if (isDead) return;

        // Base Update might move zombie left, we need to override or manage movement manually
        // Since ZombieBase moves in Update, we might need to set 'speed' to 0 or override Move() if virtual.
        // ZombieBase.Move() is likely private or non-virtual, so we might fight it.
        // CHECK ZombieBase.cs content later. Assuming we can control speed/movement.
        
        // Timer Logic
        HandleTimers();

        // State Machine
        switch (currentState)
        {
            case BossState.Idle:
                // Just wait, maybe play idle anim
                // Base class might accept speed = 0
                this.speed = 0; // Stop base movement
                break;

            case BossState.Moving:
                // Move towards target
                MoveToTarget();
                break;

            case BossState.Attacking:
                // Facing plant, throwing
                // Controlled by animation events usually
                break;
                
             case BossState.GlobalAttack:
                // Performing global attack
                break;
        }
    }

    private void HandleTimers()
    {
        if (currentState != BossState.Idle && currentState != BossState.Moving) return;

        // Global Attack Timer
        globalAttackTimer -= Time.deltaTime;
        if (globalAttackTimer <= 0)
        {
            StartCoroutine(PerformGlobalAttack());
            globalAttackTimer = Random.Range(globalAttackIntervalMin, globalAttackIntervalMax);
            return;
        }

        // Patrol Timer (Switch Lane)
        patrolTimer -= Time.deltaTime;
        if (patrolTimer <= 0 && currentState == BossState.Idle)
        {
            PickNewPatrolTarget();
            patrolTimer = Random.Range(patrolIntervalMin, patrolIntervalMax);
        }
    }

    private void PickNewPatrolTarget()
    {
        // 1. Pick a random lane (Y pos)
        // 2. Pick a random Col (X pos)
        
        // Find all lane clickers to get valid Y positions
        var lanes = FindObjectsOfType<ZombieLaneClick>();
        if (lanes.Length == 0) return;

        ZombieLaneClick randomLane = lanes[Random.Range(0, lanes.Length)];
        float targetY = randomLane.spawnPoint.position.y;
        
        // X position: within backlines
        // Assume grid size ~1 unit? Need to check Grid logic. 
        // Let's use current X +/- random range, bounded by min/max world X logic or simply hardcoded range
        // For now, let's keep X relatively stable or slight variations.
        // PlantManager tile logic: Tiles are usually spaced regularly.
        // Let's assume X = 7.5 to 9.5 roughly for backlines logic if 0 is left.
        
        float randomX = Random.Range(6.0f, 9.0f); // Adjust based on board coordinates
        
        moveTarget = new Vector3(randomX, targetY, 0);
        
        // Start Moving
        currentState = BossState.Moving;
        // Animation
        SetStateClientRpc(BossState.Moving);
    }

    private void MoveToTarget()
    {
        float moveSpeed = 1.0f; // Boss walk speed
        transform.position = Vector3.MoveTowards(transform.position, moveTarget, moveSpeed * Time.deltaTime);

        // Face Logic (Left/Right)
        if (moveTarget.x > transform.position.x) transform.localScale = new Vector3(-1, 1, 1); // Face Right
        else transform.localScale = new Vector3(1, 1, 1); // Face Left

        if (Vector3.Distance(transform.position, moveTarget) < 0.1f)
        {
            currentState = BossState.Idle;
            SetStateClientRpc(BossState.Idle);
            
            // Revert facing to Left (default threat direction)
            transform.localScale = new Vector3(1, 1, 1);
        }
    }

    private IEnumerator PerformGlobalAttack()
    {
        currentState = BossState.GlobalAttack;
        SetStateClientRpc(BossState.GlobalAttack); // Play Throw prep animation

        // Wait for anim windup
        yield return new WaitForSeconds(1.0f);

        // Find Target
        PlantBase[] allPlants = FindObjectsOfType<PlantBase>();
        if (allPlants.Length > 0)
        {
            PlantBase target = allPlants[Random.Range(0, allPlants.Length)];
            if (target != null)
            {
                // Spawn Projectile
                SpawnAppleProjectile(target.transform.position, target.transform);
            }
        }
        else
        {
            // Attack random tile if no plants? Or skip.
            // Just skip for now.
        }

        // Cooldown/Finish
        yield return new WaitForSeconds(1.0f); // Recover
        currentState = BossState.Idle;
        SetStateClientRpc(BossState.Idle);
    }
    
    private void TriggerSummonAnimation()
    {
        if (currentState != BossState.Idle) return; // Don't interrupt attacks?
        
        StartCoroutine(SummonRoutine());
    }

    private IEnumerator SummonRoutine()
    {
        BossState prevState = currentState;
        currentState = BossState.Summoning;
        SetStateClientRpc(BossState.Summoning);
        
        yield return new WaitForSeconds(1.5f); // Duration of summon anim
        
        currentState = prevState; // Return to previous state (usually Idle)
        if (currentState == BossState.Idle) SetStateClientRpc(BossState.Idle);
        else if (currentState == BossState.Moving) SetStateClientRpc(BossState.Moving);
    }

    private void SpawnAppleProjectile(Vector3 targetPos, Transform targetTrans)
    {
        if (appleProjectilePrefab != null)
        {
            GameObject apple = Instantiate(appleProjectilePrefab, transform.position + Vector3.up, Quaternion.identity);
            apple.GetComponent<NetworkObject>().Spawn();
            AppleProjectile proj = apple.GetComponent<AppleProjectile>();
            if (proj != null)
            {
                proj.Launch(targetPos, targetTrans);
            }
        }
    }

    [ClientRpc]
    private void SetStateClientRpc(BossState state)
    {
        // Visuals/Animations on Client
        if (animator != null)
        {
            // Reset triggers
            animator.ResetTrigger("Attack"); // assuming parameter names
            animator.SetBool("Walking", false);
            
            switch (state)
            {
                case BossState.Idle:
                    animator.Play("Idle");
                    break;
                case BossState.Moving:
                    animator.SetBool("Walking", true); 
                    // animator.Play("Walking");
                    break;
                case BossState.GlobalAttack:
                    animator.SetTrigger("Attack"); // Or "Throw"
                    // animator.Play("Throw");
                    break;
                case BossState.Summoning:
                     animator.SetTrigger("Summon");
                    // animator.Play("Summon");
                    break;
            }
        }
    }

    public override void TakeDamage(int damage, bool isCritical = false)
    {
        base.TakeDamage(damage, isCritical);
        
        if (CurrentHealth <= 0)
        {
            // Boss Died! Plants Win!
             if (IsServer && GameStateManager.Instance != null)
            {
                GameStateManager.Instance.PlantWin(); // Assuming PlantWin exists or similar
                // If not, trigger State Playing -> GameOver
                Debug.Log("BOSS DIED - PLANTS WIN!");
            }
        }
    }
}
*/