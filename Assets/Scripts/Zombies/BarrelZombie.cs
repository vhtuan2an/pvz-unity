using UnityEngine;
using Unity.Netcode;

public class BarrelZombie : ZombieBase
{
    [Header("Barrel")]
    [SerializeField] private GameObject barrelPrefab;
    [SerializeField] private float rollCooldown = 3f;
    [SerializeField] private Transform barrelSpawnPoint;

    private bool isDead;

    protected override void Start()
    {
        base.Start();
        if (!IsServer) return;

        animator.SetBool("isRolling", false);
        InvokeRepeating(nameof(RollBarrel), 1f, rollCooldown);
    }

    private void RollBarrel()
    {
        if (isDead) return;

        animator.SetBool("isRolling", true);
        SpawnBarrel();
        Invoke(nameof(StopRolling), 0.5f); 
    }

    private void StopRolling()
    {
        animator.SetBool("isRolling", false);
    }

    private void SpawnBarrel()
    {
        GameObject barrel = Instantiate(
            barrelPrefab,
            barrelSpawnPoint.position,
            Quaternion.identity
        );

        barrel.GetComponent<NetworkObject>().Spawn();
    }

    protected override void Die()
    {
        base.Die();

        if (!IsServer || isDead) return;

        isDead = true;
        CancelInvoke();
        
        // Stop logic
        enabled = false;
    }
}
