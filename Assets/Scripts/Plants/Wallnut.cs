using UnityEngine;

public class Wallnut : PlantBase
{
    [Header("Wallnut Settings")]
    public int armor = 0;                               // plant food

    protected override void Start()
    {
        base.Start();
    }

    public override void TakeDamage(int damage)
    {
        int effective = Mathf.Max(0, damage - armor);
        base.TakeDamage(effective);
    }

    protected override void Die()
    {
        base.Die();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(1f, 1f, 0f));
    }
}