using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class PotatoMine : PlantBase
{
    [Header("Potato Mine Settings")]
    [SerializeField] private float burrowTime = 5f;
    [SerializeField] private int explosionDamage = 1800;
    [SerializeField] private float explosionRadius = 1.5f; // Width of explosion area
    [SerializeField] private float explosionHeight = 1.2f; // Height of explosion area

    private Animator animator;
    private bool isArmed = false;
    private bool isExploding = false;

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();
        animator.SetBool("IsArmed", false);
        animator.SetBool("IsExploding", false);
        StartCoroutine(BurrowSequence());
    }

    private IEnumerator BurrowSequence()
    {
        // Start burrow animation (Idle2)
        animator.SetBool("IsArmed", false);

        yield return new WaitForSeconds(burrowTime);

        // Trigger Grow animation
        animator.SetTrigger("Grow");

        // Wait for grow animation to finish (use animation event or set a fixed time)
        yield return new WaitForSeconds(1.0f); // Replace with actual grow anim length if needed

        // Armed state
        animator.SetBool("IsArmed", true);
        isArmed = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryExplode(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryExplode(other);
    }

    private void TryExplode(Collider2D other)
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
        TriggerExplodeAnimationClientRpc();
        yield return null;
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName("Explode"))
            yield return null;
        // Wait for explosion animation to finish
        float explodeLength = animator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(explodeLength * 0.5f);
        Die();
    }

    [ClientRpc]
    private void TriggerExplodeAnimationClientRpc()
    {
        if (animator != null)
        {
            animator.SetBool("IsExploding", true);
            animator.SetBool("IsArmed", false);
        }
    }

    private void DealExplosionDamage()
    {
        Vector2 center = transform.position;
        Vector2 size = new Vector2(explosionRadius * 2f, explosionHeight); // Use the field

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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(explosionRadius * 2f, explosionHeight, 0f));
    }
}