using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BonkChoy : PlantBase
{
    private enum PunchSide { None, Left, Right }
    
    // Animation type enum matching Animator parameter values
    private enum AttackType 
    { 
        Idle = 0,           // No attack
        PunchLeft = 1,      // Normal punch left
        PunchRight = 2,     // Normal punch right
        KillLeft = 3,       // Kill punch left
        KillRight = 4       // Kill punch right
    }
    
    private PunchSide currentSide = PunchSide.None;
    private AttackType currentAttackType = AttackType.Idle;

    [Header("Combat")]
    [SerializeField] private float attackRate = 0.33f; // Punches every 0.33 seconds
    [SerializeField] private int punchDamage = 15;
    [SerializeField] private float attackRange = 2.4f; // 2 tiles (assuming tile size is ~1.2 units)
    [SerializeField] private Transform punchPointLeft;
    [SerializeField] private Transform punchPointRight;
    [SerializeField] private Vector2 punchBoxSize = new Vector2(1.8f, 1.2f); // Width: 1.8 tiles, Height: 1.2 tiles (adjust as needed)
    [SerializeField] private float punchBoxDistance = 0.9f; // Distance from center (1.5 tiles from plant, adjust as needed)
    [SerializeField] private Vector2 punchBoxOffset = Vector2.zero; // Offset for detection/damage area

    private float attackTimer = 0f;
    private Animator animator;
    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();

        // Default punch points to the plant's position if not set
        if (punchPointLeft == null)
            punchPointLeft = transform;

        if (punchPointRight == null)
            punchPointRight = transform;

        Debug.Log($"BonkChoy Start: attackRate={attackRate}, damage={punchDamage}");
    }

    private void Update()
    {
        if (!IsServer)
            return;

        // Always check for zombies and update animation
        bool hasTargets = CheckForZombiesInRange();
        UpdatePunchAnimationClientRpc(currentSide, hasTargets);

        // Only deal damage at attack interval
        attackTimer += Time.deltaTime;
        if (attackTimer >= attackRate && hasTargets)
        {
            PerformPunch();
            attackTimer = 0f;
        }
    }

    private bool CheckForZombiesInRange()
    {
        // Check left side first
        Vector2 leftBoxCenter = (Vector2)transform.position + Vector2.left * punchBoxDistance + punchBoxOffset;
        Collider2D[] leftTargets = Physics2D.OverlapBoxAll(leftBoxCenter, punchBoxSize, 0f, LayerMask.GetMask("Zombie"));
        if (leftTargets.Length > 0)
        {
            currentSide = PunchSide.Left;
            return true;
        }

        // Check right side if no zombies on left
        Vector2 rightBoxCenter = (Vector2)transform.position + Vector2.right * punchBoxDistance + punchBoxOffset;
        Collider2D[] rightTargets = Physics2D.OverlapBoxAll(rightBoxCenter, punchBoxSize, 0f, LayerMask.GetMask("Zombie"));
        if (rightTargets.Length > 0)
        {
            currentSide = PunchSide.Right;
            return true;
        }

        currentSide = PunchSide.None;
        return false;
    }

    private void PerformPunch()
    {
        if (!IsServer)
            return;

        // Check if any zombie will die to determine attack type
        currentAttackType = DetermineAttackType();
        Debug.Log($"🥊 PerformPunch: Determined attack type = {currentAttackType}");
        
        // Set animation on all clients
        // Damage will be dealt by Animation Event at the exact punch frame
        SetAttackAnimationClientRpc(currentAttackType);
    }

    private AttackType DetermineAttackType()
    {
        if (!IsServer || currentSide == PunchSide.None)
            return AttackType.Idle;

        Vector2 boxCenter = (Vector2)transform.position +
            (currentSide == PunchSide.Left ? Vector2.left : Vector2.right) * punchBoxDistance +
            punchBoxOffset;

        Collider2D[] targets = Physics2D.OverlapBoxAll(boxCenter, punchBoxSize, 0f, LayerMask.GetMask("Zombie"));
        
        Debug.Log($"🔍 DetermineAttackType: Found {targets.Length} zombies, side={currentSide}");

        // Check if any zombie will die from this punch
        bool willKillZombie = false;
        foreach (var col in targets)
        {
            ZombieBase zombie = col.GetComponent<ZombieBase>();
            if (zombie != null)
            {
                int zombieHP = zombie.GetCurrentHealth();
                Debug.Log($"🧟 Zombie {col.name} HP: {zombieHP}, Will die: {zombieHP <= punchDamage}");
                
                if (zombieHP <= punchDamage)
                {
                    willKillZombie = true;
                    break; // Found at least one zombie that will die
                }
            }
        }

        // Determine attack type based on side and whether we'll kill
        if (willKillZombie)
        {
            AttackType killType = (currentSide == PunchSide.Right) ? AttackType.KillRight : AttackType.KillLeft;
            Debug.Log($"💀 KILL ATTACK: {killType}");
            return killType;
        }
        else
        {
            AttackType normalType = (currentSide == PunchSide.Right) ? AttackType.PunchRight : AttackType.PunchLeft;
            Debug.Log($"✊ Normal attack: {normalType}");
            return normalType;
        }
    }

    // Called by Animation Event at the exact frame when punch connects
    private void DealPunchDamage()
    {
        if (!IsServer)
            return;

        Debug.Log($"💥 DealPunchDamage called by Animation Event");
        
        // Play appropriate sound based on attack type
        PlayPunchSoundClientRpc(currentAttackType);
        
        DealAOEDamage();
    }

    private void DealAOEDamage()
    {
        if (!IsServer || currentSide == PunchSide.None)
            return;

        Vector2 boxCenter = (Vector2)transform.position +
            (currentSide == PunchSide.Left ? Vector2.left : Vector2.right) * punchBoxDistance +
            punchBoxOffset;

        Collider2D[] targets = Physics2D.OverlapBoxAll(boxCenter, punchBoxSize, 0f, LayerMask.GetMask("Zombie"));

        int hitCount = 0;
        foreach (var col in targets)
        {
            ZombieBase zombie = col.GetComponent<ZombieBase>();
            if (zombie != null)
            {
                Debug.Log($"💥 BonkChoy {currentSide} punched {col.name} for {punchDamage} damage!");
                zombie.TakeDamage(punchDamage);
                hitCount++;
            }
        }

        if (hitCount > 0)
        {
            Debug.Log($"🥊 BonkChoy {currentSide} punch hit {hitCount} zombie(s)!");
        }
    }

    [ClientRpc]
    private void SetAttackAnimationClientRpc(AttackType attackType)
    {
        if (animator == null) 
        {
            Debug.LogError("❌ Animator is null!");
            return;
        }
        
        // Store attack type on clients too for potential client-side logic
        currentAttackType = attackType;

        int attackValue = (int)attackType;
        // Set the AttackType integer parameter - Animator will handle transitions
        animator.SetInteger("AttackType", attackValue);
        
        // Verify it was set
        int currentValue = animator.GetInteger("AttackType");
        Debug.Log($"🎬 BonkChoy set AttackType = {attackType} (value: {attackValue}), verified current value: {currentValue}");
    }

    [ClientRpc]
    private void PlayPunchSoundClientRpc(AttackType attackType)
    {
        if (NetworkGameManager.Instance == null) return;
        
        // Play kill sound for kill attacks, punch sound for normal
        string soundName = (attackType == AttackType.KillRight || attackType == AttackType.KillLeft) 
            ? "bonk_choy/kill" 
            : "bonk_choy/punch";
            
        NetworkGameManager.Instance.PlaySoundClientRpc(soundName);
    }

    // This ClientRpc is called to update the animation state on all clients
    [ClientRpc]
    private void UpdatePunchAnimationClientRpc(PunchSide side, bool hasTargets)
    {
        currentSide = side;
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) return;

        // Set AttackType to Idle when no targets
        if (!hasTargets)
        {
            animator.SetInteger("AttackType", (int)AttackType.Idle);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw left punch box (red)
        Gizmos.color = Color.red;
        Vector3 leftBoxCenter = transform.position + (Vector3)Vector2.left * punchBoxDistance + (Vector3)punchBoxOffset;
        Gizmos.DrawWireCube(leftBoxCenter, punchBoxSize);

        // Draw right punch box (blue)
        Gizmos.color = Color.blue;
        Vector3 rightBoxCenter = transform.position + (Vector3)Vector2.right * punchBoxDistance + (Vector3)punchBoxOffset;
        Gizmos.DrawWireCube(rightBoxCenter, punchBoxSize);
    }
}
