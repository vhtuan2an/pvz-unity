using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BonkChoy : PlantBase
{
    [Header("Combat")]
    [SerializeField] private float attackRate = 0.33f; // Punches every 0.33 seconds
    [SerializeField] private int punchDamage = 15;
    [SerializeField] private float attackRange = 2.4f; // 2 tiles (assuming tile size is ~1.2 units)
    [SerializeField] private Transform punchPointLeft;
    [SerializeField] private Transform punchPointRight;

    private float attackTimer = 0f;
    private Animator animator;
    private Transform currentTarget; // Current side being attacked (left or right)
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
        // Only server handles combat logic
        if (!IsServer)
            return;

        attackTimer += Time.deltaTime;

        if (attackTimer >= attackRate)
        {
            // Check for zombies on both sides
            bool hasTargets = CheckForZombiesInRange();

            if (hasTargets)
            {
                PerformPunch();
            }
            else
            {
                SetIdleAnimationClientRpc();
            }

            attackTimer = 0f;
        }
    }

    private bool CheckForZombiesInRange()
    {
        // Check left side first (prioritize left)
        Collider2D[] leftTargets = Physics2D.OverlapCircleAll(punchPointLeft.position, attackRange, LayerMask.GetMask("Zombie"));
        if (leftTargets.Length > 0)
        {
            currentTarget = punchPointLeft;
            return true;
        }

        // Check right side if no zombies on left
        Collider2D[] rightTargets = Physics2D.OverlapCircleAll(punchPointRight.position, attackRange, LayerMask.GetMask("Zombie"));
        if (rightTargets.Length > 0)
        {
            currentTarget = punchPointRight;
            return true;
        }

        currentTarget = null;
        return false;
    }

    private void PerformPunch()
    {
        if (!IsServer)
            return;

        // Trigger punch animation on all clients
        // Damage will be dealt by Animation Event at the exact punch frame
        TriggerPunchAnimationClientRpc();
    }

    // Called by Animation Event at the exact frame when punch connects
    private void DealPunchDamage()
    {
        if (!IsServer)
            return;

        Debug.Log($"💥 DealPunchDamage called by Animation Event");
        DealAOEDamage();
    }

    private void DealAOEDamage()
    {
        if (!IsServer || currentTarget == null)
            return;

        string side = (currentTarget == punchPointLeft) ? "LEFT" : "RIGHT";
        
        // Get all zombies in the punch range for the current target side
        Collider2D[] targets = Physics2D.OverlapCircleAll(currentTarget.position, attackRange, LayerMask.GetMask("Zombie"));
        
        int hitCount = 0;
        foreach (var col in targets)
        {
            ZombieBase zombie = col.GetComponent<ZombieBase>();
            if (zombie != null)
            {
                Debug.Log($"💥 BonkChoy {side} punched {col.name} for {punchDamage} damage!");
                zombie.TakeDamage(punchDamage);
                hitCount++;
            }
        }

        if (hitCount > 0)
        {
            Debug.Log($"🥊 BonkChoy {side} punch hit {hitCount} zombie(s)!");
        }
    }

    [ClientRpc]
    private void TriggerPunchAnimationClientRpc()
    {
        if (animator != null)
        {
            // Determine which side is being punched
            if (currentTarget == punchPointRight)
            {
                // Right side = Attack-Front
                animator.SetBool("isIdle", false);
                animator.SetBool("isAttackFront", true);
                animator.SetBool("isAttackBack", false);
            }
            else if (currentTarget == punchPointLeft)
            {
                // Left side = Attack-Back
                animator.SetBool("isIdle", false);
                animator.SetBool("isAttackFront", false);
                animator.SetBool("isAttackBack", true);
            }
        }
    }

    [ClientRpc]
    private void SetIdleAnimationClientRpc()
    {
        if (animator != null)
        {
            animator.SetBool("isIdle", true);
            animator.SetBool("isAttackFront", false);
            animator.SetBool("isAttackBack", false);
        }
    }

    // Visualize attack ranges in editor
    private void OnDrawGizmosSelected()
    {
        if (punchPointLeft == null)
            punchPointLeft = transform;
        
        if (punchPointRight == null)
            punchPointRight = transform;

        // Draw left punch range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(punchPointLeft.position, attackRange);

        // Draw right punch range
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(punchPointRight.position, attackRange);
    }
}
