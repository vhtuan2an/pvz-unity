using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class CherryBomb : PlantBase
{
    [Header("Cherry Bomb Settings")]
    [SerializeField] private int damage = 1800;
    [SerializeField] private Vector2 aoeScale = new Vector2(3.5f, 3.5f);
    [SerializeField] private Vector2 explosionOffset = Vector2.zero;


    [Header("Debug")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0f, 0.5f);

    private bool hasExploded = false;
    private Animator animator;

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();
    }

    public void Explode()
    {
        if (!IsServer) return;

        if (hasExploded) return;
        hasExploded = true;

        // Calculate center with offset
        Vector3 center = transform.position + (Vector3)explosionOffset;
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, aoeScale, 0f);

        foreach (Collider2D hit in hits)
        {
            ZombieBase zombie = hit.GetComponent<ZombieBase>();
            if (zombie != null)
            {
                zombie.TakeDamage(damage);
            }
        }

        StartCoroutine(DestroyAfterAnim());
    }

    private IEnumerator DestroyAfterAnim()
    {
        float delay = 1f;
        if (animator != null)
        {
            delay = animator.GetCurrentAnimatorStateInfo(0).length;
        }
        yield return new WaitForSeconds(delay);
        Die();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = gizmoColor;
        Vector3 center = transform.position + (Vector3)explosionOffset;
        Gizmos.DrawWireCube(center, new Vector3(aoeScale.x, aoeScale.y, 1f));
        
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.2f);
        Gizmos.DrawCube(center, new Vector3(aoeScale.x, aoeScale.y, 1f));
    }
}
