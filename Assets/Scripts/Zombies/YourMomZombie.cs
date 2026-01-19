using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class YourMomZombie : ZombieBase
{
    public static YourMomZombie Instance { get; private set; }

    private enum BossState { Intro, Idle, Moving, Attacking, Summoning, GlobalAttack, Waiting, Death }
    private BossState currentState = BossState.Waiting;
    public bool isDead = false;

    [Header("Boss Settings")]
    public float patrolIntervalMin = 5f;
    public float patrolIntervalMax = 10f;
    public float attackRange = 3f;     // Range to trigger normal attack
    public float appleCooldown = 2f;   // Cooldown for normal attack
    
    [Header("Global Attack Settings")]
    public float globalAttackIntervalMin = 30f;
    public float globalAttackIntervalMax = 60f;
    public GameObject appleProjectilePrefab;
    public Transform shootPoint;
    public GameObject targetMarkerPrefab;
    public float telegraphDuration = 1.0f;
    
    [Header("Death Settings")]
    [SerializeField] private float deathDelay = 0.75f; // = length anim Die

    [Header("VFX Settings")]
    public Vector3 headVFXOffset = new Vector3(0f, 2.5f, 0f);
    public Vector3 feetVFXOffset = new Vector3(0f, -0.5f, 0f);

    private GameObject activeTelegraphMarker;

    private Vector3 pendingTargetPos;

    // Get dynamic attack interval based on zombie team status
    private float GetDynamicGlobalAttackInterval()
    {
        if (GameStatsTracker.Instance == null)
        {
            return globalAttackIntervalMax; // Default to max if no tracker
        }

        // If zombies are heavily outnumbered (10:1) - fastest attacks
        if (GameStatsTracker.Instance.IsZombieHeavilyOutnumbered)
        {
            return globalAttackIntervalMin; // 45s (fastest)
        }
        // If zombies are losing units or broke - medium speed
        else if (GameStatsTracker.Instance.IsZombieLosingUnits || GameStatsTracker.Instance.IsZombieBroke)
        {
            return Mathf.Lerp(globalAttackIntervalMin, globalAttackIntervalMax, 0.33f); // ~50s
        }
        // If zombies are winning - slowest attacks
        else
        {
            return globalAttackIntervalMax; // 60s (slowest)
        }
    }

    private IEnumerator PerformGlobalAttack()
    {
        currentState = BossState.GlobalAttack;
        // Do NOT trigger animation yet. Just prepare.

        // Wait for anim windup (removed, we control flow now)
        // yield return new WaitForSeconds(0.5f); 

        // Find Target (Tile with Plant, prioritize backline)
        Tile targetTile = FindBestPlantTile();
        if (targetTile != null)
        {
            pendingTargetPos = targetTile.PlantWorldPosition;

            // 1. Telegraph Phase
            SpawnTelegraphMarkerClientRpc(pendingTargetPos);
            Debug.Log($"Boss targeting tile: {targetTile.name} (Telegraphing...)");

            yield return new WaitForSeconds(telegraphDuration);

            // 2. Cleanup Marker
            DestroyTelegraphMarkerClientRpc();

            // Check Frozen AGAIN after waiting
            if (currentSlowMultiplier <= 0f)
            {
                Debug.Log("Boss frozen during telegraph - cancelling throw!");
                currentState = BossState.Idle;
                SetStateClientRpc(BossState.Idle);
                yield break;
            }

            // 3. Trigger Animation (The EVENT will spawn the apple)
            SetStateClientRpc(BossState.GlobalAttack);
        }
        else
        {
            // No valid targets - skip attack
            Debug.Log("Boss found no plant targets - skipping attack");
            currentState = BossState.Idle;
            SetStateClientRpc(BossState.Idle);
            yield break;
        }

        // Wait for Animation to finish (and Event to fire)
        // Adjust this recovery time based on animation length if needed
        yield return new WaitForSeconds(1.0f); 
        
        currentState = BossState.Idle;
        SetStateClientRpc(BossState.Idle);
    }

    // Find tile with plant, using weighted random (backline plants have higher chance)
    private Tile FindBestPlantTile()
    {
        Tile[] allTiles = FindObjectsOfType<Tile>();
        System.Collections.Generic.List<Tile> occupiedTiles = new System.Collections.Generic.List<Tile>();
        System.Collections.Generic.List<float> weights = new System.Collections.Generic.List<float>();

        // Find all occupied tiles and calculate weights (lower X = higher weight)
        float maxX = float.MinValue;
        foreach (Tile tile in allTiles)
        {
            if (tile != null && tile.IsOccupied)
            {
                occupiedTiles.Add(tile);
                float tileX = tile.PlantWorldPosition.x;
                if (tileX > maxX) maxX = tileX;
            }
        }

        if (occupiedTiles.Count == 0) return null;

        // Calculate weights: backline (low X) gets higher weight
        float totalWeight = 0f;
        foreach (Tile tile in occupiedTiles)
        {
            // Weight = (maxX - currentX + 1) to favor lower X values
            // +1 ensures no zero weights
            float weight = maxX - tile.PlantWorldPosition.x + 1f;
            
            // Extra priority for Sunflowers/TwinSunflowers (2x weight)
            GameObject occupant = tile.GetOccupyingPlant();
            if (occupant != null)
            {
                PlantBase plant = occupant.GetComponent<PlantBase>();
                if (plant != null)
                {
                    string plantName = plant.GetType().Name;
                    if (plantName == "Sunflower" || plantName == "TwinSunflower")
                    {
                        weight *= 2f; // Double the weight for economy plants
                    }
                }
            }
            
            weights.Add(weight);
            totalWeight += weight;
        }

        // Weighted random selection
        float randomValue = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < occupiedTiles.Count; i++)
        {
            cumulative += weights[i];
            if (randomValue <= cumulative)
            {
                return occupiedTiles[i];
            }
        }

        // Fallback (should never reach here)
        return occupiedTiles[occupiedTiles.Count - 1];
    }

    // Called via Animation Event
    public void AnimEvent_ThrowApple()
    {
        if (!IsServer) return;
        if (GlobalAttackCancelled()) return;

        Debug.Log("AnimEvent: Throwing Apple!");
        SpawnAppleProjectile(pendingTargetPos, null);
    }
    
    // Safety check if needed
    private bool GlobalAttackCancelled() => currentState != BossState.GlobalAttack;

    // Custom VFX Offsets
    protected override Vector3 GetVFXOffset(VFXTargetType targetType)
    {
        switch (targetType)
        {
            case VFXTargetType.Head:
                return headVFXOffset;
            case VFXTargetType.Feet:
            default:
                return feetVFXOffset;
        }
    }

    [ClientRpc]
    private void SpawnTelegraphMarkerClientRpc(Vector3 pos)
    {
        if (targetMarkerPrefab != null)
        {
            if (activeTelegraphMarker != null) Destroy(activeTelegraphMarker);
            activeTelegraphMarker = Instantiate(targetMarkerPrefab, pos, Quaternion.identity);
        }
    }

    [ClientRpc]
    private void DestroyTelegraphMarkerClientRpc()
    {
         if (activeTelegraphMarker != null)
         {
             Destroy(activeTelegraphMarker);
             activeTelegraphMarker = null;
         }
    }

    private void SpawnAppleProjectile(Vector3 targetPos, Transform targetTrans)
    {
        if (appleProjectilePrefab != null)
        {
            // Use shootPoint if assigned, else fallback
            Vector3 spawnPos = shootPoint != null ? shootPoint.position : (transform.position + Vector3.up);
            GameObject apple = Instantiate(appleProjectilePrefab, spawnPos, Quaternion.identity);
            apple.GetComponent<NetworkObject>().Spawn();
            AppleProjectile proj = apple.GetComponent<AppleProjectile>();
            if (proj != null)
            {
                proj.Launch(targetPos, targetTrans);
            }
        }
    }
    public float minX = 1.0f;
    public float maxX = 3.4f;
    public float minY = -3.2f;
    public float maxY = 1.6f;
    private Vector3 spawnPoint = new Vector3(15f, 0.5f, 0f);

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
        // Start timers
        patrolTimer = Random.Range(patrolIntervalMin, patrolIntervalMax);
        globalAttackTimer = GetDynamicGlobalAttackInterval();
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

            // Check Game State
            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState.Value == GameStateManager.GameState.Playing)
            {
                StartIntroWalk();
            }
            else
            {
                currentState = BossState.Waiting;
                // Subscribe to state change
                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
                }
            }

            // Subscribe to Zombie Spawn (Server Side)
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnZombieSpawnedServer += TriggerSummonAnimation;
            }
        }
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        if (newState == GameStateManager.GameState.Playing)
        {
            if (currentState == BossState.Waiting)
            {
                StartIntroWalk();
            }
            
            // Unsubscribe
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
            }
        }
    }

    private void StartIntroWalk()
    {
        currentState = BossState.Intro;
        SetStateClientRpc(BossState.Moving, false); // Look left walking
    }

    private Vector3 GetRandomPatrolPosition()
    {
        float randomX = Random.Range(minX, maxX);
        float randomY = Random.Range(minY, maxY);
        return new Vector3(randomX, randomY, 0f);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();


        if (IsServer && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        if (IsServer && NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnZombieSpawnedServer -= TriggerSummonAnimation;
        }
    }

    protected override void Update()
    {
        base.Update();
        if (!IsServer) return; // Logic only runs on server
        if (currentState == BossState.Death) return;
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
            case BossState.Waiting:
                break;

            case BossState.Idle:
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
        // Boss-specific TakeDamage logic if any (e.g. anger phases)
        // Death is now handled by overriding Die()
    }

    protected override void Die()
    {
        // Check if already dead to prevent spam
        if (isDead) return;
        
        // We override ZombieBase.Die completely to handle Boss Death
        HandleDeath();
    }

    private void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        currentState = BossState.Death;
        StopAllCoroutines();
        
        // Clear status effects like freezes/slows so animation plays clearly
        activeSlows.Clear();
        ClearStatusEffectsClientRpc();

        PlayDeathClientRpc();

        if (IsServer)
        {
            StartCoroutine(DeathRoutine());
        }
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(deathDelay);

        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnPlantWin(NetworkObject);
            Debug.Log("🌱 Boss dead → Plants win!");
        }
    }

    [ClientRpc]
    private void PlayDeathClientRpc()
    {
        if (animator != null)
        {
            animator.ResetTrigger("Throw");
            animator.ResetTrigger("Summon");
            animator.SetInteger("isMoving", 0);
            animator.SetTrigger("Die"); 
        }
    }


    /// <summary>
    /// Returns the boss's current health as a percentage (0.0 to 1.0)
    /// </summary>
    public float GetHealthPercentage()
    {
        int currentHealth = GetCurrentHealth();
        int maxHealth = GetMaxHealth();
        
        if (maxHealth <= 0) return 0f;
        
        return (float)currentHealth / maxHealth;
    }

    private void HandleTimers()
    {
        if (currentState != BossState.Idle && currentState != BossState.Moving) return;

        // Global Attack Timer
        globalAttackTimer -= Time.deltaTime;
        
        // Only attack if ready AND Idle (don't interrupt moving) AND Not Frozen
        if (globalAttackTimer <= 0 && currentState == BossState.Idle)
        {
            if (currentSlowMultiplier > 0f) 
            {
                StartCoroutine(PerformGlobalAttack());
                globalAttackTimer = GetDynamicGlobalAttackInterval(); // Dynamic interval based on losing/winning
            }
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