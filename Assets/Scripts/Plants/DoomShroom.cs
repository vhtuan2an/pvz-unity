using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class DoomShroom : PlantBase
{
    [Header("Explosion Settings")]
    [SerializeField] private int explosionDamage = 2200;
    [SerializeField] private Vector2 aoeScale = new Vector2(5f, 5f);
    [SerializeField] private Vector2 explosionOffset = Vector2.zero;
    [SerializeField] private Vector3 craterOffset = Vector3.zero;
    [SerializeField] private NetworkObject craterPrefab;

    [Header("Debug")]
    [SerializeField] private Color gizmoColor = new Color(0.5f, 0f, 0.5f, 0.5f);

    private bool hasExploded = false;
    private bool damageDealt = false;
    private Animator animator;

    // Parameters for animations
    private static readonly int BoomHash = Animator.StringToHash("Boom");

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();
    }

    // Override TakeDamage to handle explosion trigger instead of standard death
    public override void TakeDamage(int damage)
    {
        if (!IsServer) return;

        currentHealth -= damage;
        Debug.Log($"{gameObject.name} took {damage} damage. HP: {currentHealth}/{maxHealth}");

        // Boom when health is depleted to 1 (or less)
        if (currentHealth <= 1 && !hasExploded)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (!IsServer) return;
        if (hasExploded) return;
        
        hasExploded = true;
        
        // Trigger animation on all clients
        TriggerBoomAnimationClientRpc();
    }

    // Called via Animation Event
    private void DealExplosionDamage()
    {
        RequestExplosionDamageServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestExplosionDamageServerRpc()
    {
        if (damageDealt) return;
        damageDealt = true;

        NetworkGameManager.Instance.PlaySoundClientRpc("doomshroom");

        // Deal Damage
        Vector3 center = transform.position + (Vector3)explosionOffset;
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, aoeScale, 0f);

        foreach (Collider2D hit in hits)
        {
            ZombieBase zombie = hit.GetComponent<ZombieBase>();
            if (zombie != null)
            {
                zombie.TakeDamage(explosionDamage);
            }
        }
        
        // Spawn Crater immediately when damage happens (visual impact)
        if (craterPrefab != null)
        {
            Vector3 spawnPos = transform.position + craterOffset;
            NetworkObject craterInstance = Instantiate(craterPrefab, spawnPos, Quaternion.identity);
            craterInstance.Spawn(true);
        }

        // Release the tile so the Crater can occupy it
        if (occupiedTile != null)
        {
            occupiedTile.Clear();
            occupiedTile = null; // Ensure Die() doesn't try to clear it again
        }

        // Disable collider so zombies stop attacking the crater/plant
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Debug.Log("DoomShroom went BOOM! Damage dealt & Crater spawned.");
    }

    // Called via Animation Event at the end of the boom animation
    private void OnBoomAnimationEnd()
    {
        RequestDeathServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDeathServerRpc()
    {
        Die();
    }


    [ClientRpc]
    private void TriggerBoomAnimationClientRpc()
    {
        if (animator != null)
        {
            animator.SetTrigger(BoomHash);
            // Assuming Idle and Spawn are default states/transisions handled by Animator Controller
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = gizmoColor;
        Vector3 center = transform.position + (Vector3)explosionOffset;
        Gizmos.DrawWireCube(center, new Vector3(aoeScale.x, aoeScale.y, 1f));
        
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.2f);
        Gizmos.DrawCube(center, new Vector3(aoeScale.x, aoeScale.y, 1f));

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position + craterOffset, 0.2f);
    }
}
