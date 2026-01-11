using UnityEngine;
using Unity.Netcode;

public class BarrelZombie : ZombieBase
{
    [Header("Barrel")]
    [SerializeField] private GameObject barrelPrefab;
    [SerializeField] private float rollCooldown = 3f;
    [SerializeField] private float rollDuration = 0.6f;
    [SerializeField] private Transform barrelSpawnPoint;

    private bool isDead;

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();

        if (!IsServer) return;


        SetRollingClientRpc(false);

        InvokeRepeating(nameof(RollBarrel), 1f, rollCooldown);
    }

    private void RollBarrel()
    {
        if (isDead) return;

        SetRollingClientRpc(true);

        SpawnBarrel();

        Invoke(nameof(StopRolling), rollDuration);
    }

    private void StopRolling()
    {
        SetRollingClientRpc(false);
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
        if (!IsServer || isDead) return;

        isDead = true;
        CancelInvoke();


        DieClientRpc();

        base.Die();
        enabled = false;
    }

    // ================= RPC =================

    [ClientRpc]
    private void SetRollingClientRpc(bool isRolling)
    {
        if (animator != null)
        {
            animator.SetBool("isRolling", isRolling);
        }
    }

    [ClientRpc]
    private void DieClientRpc()
    {
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }
    }
}
