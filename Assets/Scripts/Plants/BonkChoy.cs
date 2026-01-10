using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BonkChoy : PlantBase
{
    private enum PunchSide { None, Left, Right }
    private PunchSide currentSide = PunchSide.None;

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
    private void TriggerPunchAnimationClientRpc()
    {
        if (currentSide == PunchSide.Left)
        {
            animator.SetTrigger("PunchLeft");
        }
        else if (currentSide == PunchSide.Right)
        {
            animator.SetTrigger("PunchRight");
        }
    }

    // This ClientRpc is called to update the punch animation state on all clients
    [ClientRpc]
    private void UpdatePunchAnimationClientRpc(PunchSide side, bool hasTargets)
    {
        currentSide = side;
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) return;

        if (side == PunchSide.Left)
        {
            animator.SetBool("HasTargetsLeft", hasTargets);
            animator.SetBool("HasTargetsRight", false);
        }
        else if (side == PunchSide.Right)
        {
            animator.SetBool("HasTargetsRight", hasTargets);
            animator.SetBool("HasTargetsLeft", false);
        }
        else
        {
            animator.SetBool("HasTargetsLeft", false);
            animator.SetBool("HasTargetsRight", false);
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
