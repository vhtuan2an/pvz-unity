using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class PotatoMine : PlantBase
{
    [Header("Potato Mine Settings")]
    [SerializeField] private float burrowTime = 5f; // Time underground before arming
    [SerializeField] private int explosionDamage = 1800;
    [SerializeField] private float explosionRadius = 1.5f; // For visual effect, not used in tile logic

    private Animator animator;
    private bool isArmed = false;
    private bool isExploding = false;

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();
        StartCoroutine(BurrowSequence());
    }

    private IEnumerator BurrowSequence()
    {
        animator.Play("Idle2");
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);

        // Pause on last unarmed frame for burrowTime
        animator.speed = 0f;
        yield return new WaitForSeconds(burrowTime);

        // Play Grow animation
        animator.speed = 1f;
        animator.Play("Grow");
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);

        // Loop armed
        animator.Play("Idle1");
        isArmed = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isArmed || isExploding || !IsServer) return;

        if (other.CompareTag("Zombie"))
        {
            StartCoroutine(ExplodeSequence());
        }
    }

    private IEnumerator ExplodeSequence()
    {
        isExploding = true;
        isArmed = false;

        // 5. Play Pre-Explore animation
        animator.Play("Pre-Explore");
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);

        // 6. Deal damage to tile, left tile, right tile
        DealExplosionDamage();

        // 7. Play Explore3 animation (explosion)
        animator.Play("Explore3");
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);

        // 8. Destroy self
        Die();
    }

    private void DealExplosionDamage()
    {
        // Center the AOE on the mine's position
        Vector2 center = transform.position;
        Vector2 size = new Vector2(3f, 1.2f); // 3 tiles wide, 1 tile tall (adjust as needed)

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, LayerMask.GetMask("Zombie"));
        foreach (var hit in hits)
        {
            ZombieBase zombie = hit.GetComponent<ZombieBase>();
            if (zombie != null)
            {
                zombie.TakeDamage(explosionDamage);
            }
        }
    }

    // For debugging, visualize the AOE in the editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(3f, 1.2f, 0f));
    }
}