using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class YourMomZombie : ZombieBase
{
    public static YourMomZombie Instance { get; private set; }

    private enum BossState { Intro, Idle, Moving, Attacking, Summoning, GlobalAttack }
    private BossState currentState = BossState.Intro;

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
    public float minX = 1.0f;
    public float maxX = 3.4f;
    public float minY = -3.2f;
    public float maxY = 1.6f;
    private Vector3 spawnPoint = new Vector3(8.5f, 0.5f, 0f);

    private float patrolTimer;
    private float globalAttackTimer;
    private float normalAttackTimer;
    private Vector3 moveTarget;
    private bool isMovingToLane = false;
    
    // Intro Settings
    private Vector3 introTargetPos;

    // References
    // private Animator animator; // Removed: Hides base member

    // Lane Management
    private int currentLaneIndex = 0; // Need to track this roughly

    protected override void Awake()
    {
        base.Awake();
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    protected override void Start()
    {
        base.Start();
        // animator = GetComponent<Animator>(); // Already done in base.Start()

        // Start timers
        patrolTimer = Random.Range(patrolIntervalMin, patrolIntervalMax);
        globalAttackTimer = Random.Range(globalAttackIntervalMin, globalAttackIntervalMax);

        // Subscribe to spawn event
        if (ZombieManager.Instance != null)
        {
            ZombieManager.Instance.OnZombieSpawnEvent += TriggerSummonAnimation;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            // Set Start Position to specific off-screen point
            transform.position = spawnPoint; 
            
            // Pick random target in gameplay area
            introTargetPos = GetRandomPatrolPosition();
            
            currentState = BossState.Intro;
            SetStateClientRpc(BossState.Moving, false); // Look left walking
        }
    }

    private Vector3 GetRandomPatrolPosition()
    {
        float randomX = Random.Range(minX, maxX);
        float randomY = Random.Range(minY, maxY);
        return new Vector3(randomX, randomY, 0f);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (ZombieManager.Instance != null)
        {
            ZombieManager.Instance.OnZombieSpawnEvent -= TriggerSummonAnimation;
        }
    }

    protected override void Update()
    {
        base.Update();
        if (!IsServer) return; // Logic only runs on server
        if (GetCurrentHealth() <= 0) return; // Use public getter instead of direct field

        if (currentState == BossState.Intro)
        {
            float speed = currentSpeed > 0 ? currentSpeed : 1.0f; // Default to 1 if speed is 0 for some reason, or allow slow
            transform.position = Vector3.MoveTowards(transform.position, introTargetPos, speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, introTargetPos) < 0.1f)
            {
                currentState = BossState.Idle;
                SetStateClientRpc(BossState.Idle);
            }
            return;
        }

        HandleTimers();
        
        switch (currentState)
        {
            case BossState.Idle:
                // this.speed = 0; // Removing direct field access if private
                break;

            case BossState.Moving:
                MoveToTarget();
                break;

            case BossState.Attacking:
                break;
                
             case BossState.GlobalAttack:
                break;
        }
    }

    public override void TakeDamage(int damage)
    {
        base.TakeDamage(damage);
        
        if (GetCurrentHealth() <= 0)
        {
             if (IsServer && GameStateManager.Instance != null)
            {
                GameStateManager.Instance.EndGameServerRpc(PlayerRole.Plant);
                Debug.Log("BOSS DIED - PLANTS WIN!");
            }
        }
    }

    private void HandleTimers()
    {
        if (currentState != BossState.Idle && currentState != BossState.Moving) return;

        // Global Attack Timer
        globalAttackTimer -= Time.deltaTime;
        
        // Only attack if ready AND Idle (don't interrupt moving)
        if (globalAttackTimer <= 0 && currentState == BossState.Idle)
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
        // Pick anywhere in the defined box
        moveTarget = GetRandomPatrolPosition();
        
        // Start Moving
        currentState = BossState.Moving;
        // Animation (Force update on state switch)
        bool movingRight = moveTarget.x > transform.position.x;
        SetStateClientRpc(BossState.Moving, movingRight);
        lastMovingRight = movingRight;
    }

    private bool lastMovingRight = false; // Track previous direction to avoid spam

    private void MoveToTarget()
    {
        // Use currentSpeed inherited from ZombieBase (handles slows/status effects)
        transform.position = Vector3.MoveTowards(transform.position, moveTarget, currentSpeed * Time.deltaTime);

        // Face Logic (Left/Right)
        bool movingRight = moveTarget.x > transform.position.x;
        
        // Ensure scale is always 1 (animations handle facing)
        transform.localScale = Vector3.one;

        if (Vector3.Distance(transform.position, moveTarget) < 0.1f)
        {
            currentState = BossState.Idle;
            SetStateClientRpc(BossState.Idle, false); // Idle direction doesn't matter much or default left
        }
        else
        {
            // Only update animation if direction changes or if we weren't already moving
            // (Since this is called every frame in Moving state, avoid spamming CrossFade)
            if (movingRight != lastMovingRight)
            {
                SetStateClientRpc(BossState.Moving, movingRight);
                lastMovingRight = movingRight;
            }
        }
    }

    private IEnumerator PerformGlobalAttack()
    {
        currentState = BossState.GlobalAttack;
        SetStateClientRpc(BossState.GlobalAttack); // Play Throw prep animation

        // Wait for anim windup
        yield return new WaitForSeconds(1.0f);

        // Find Target (Random Tile)
        Tile[] allTiles = FindObjectsOfType<Tile>();
        if (allTiles.Length > 0)
        {
            Tile targetTile = allTiles[Random.Range(0, allTiles.Length)];
            if (targetTile != null)
            {
                // Spawn Projectile targeting the TILE position (no direct transform tracking)
                SpawnAppleProjectile(targetTile.PlantWorldPosition, null);
                Debug.Log($"Boss targeting tile: {targetTile.name} at {targetTile.PlantWorldPosition}");
            }
        }
        else
        {
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
    private void SetStateClientRpc(BossState state, bool isMovingRight = false)
    {
        // Visuals/Animations on Client
        if (animator != null)
        {
            // Ensure scale is normal (animations handle direction)
            transform.localScale = Vector3.one;

            // 0 = Idle, 1 = Left, 2 = Right
            int moveState = 0;
            if (state == BossState.Moving || state == BossState.Intro)
            {
                moveState = isMovingRight ? 2 : 1;
            }
            animator.SetInteger("isMoving", moveState);

            // Trigger One-Shot Animations
            switch (state)
            {
                case BossState.GlobalAttack:
                    animator.SetTrigger("Throw");
                    break;
                    
                case BossState.Summoning:
                    animator.SetTrigger("Summon");
                    break;
            }
        }
    }
}