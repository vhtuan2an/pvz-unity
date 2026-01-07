using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Animator))]
public class Cannon : ZombieBase
{
    [Header("Cannon")]
    [SerializeField] private NetworkObject contraiPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireCooldown = 7.5f;

    [Header("Fire Timing")]
    [Range(0f, 1f)]
    [SerializeField] private float fireSpawnNormalizedTime = 0.5f; 


    private float fireTimer;
    private bool hasSpawnedThisFire;

    protected override void Start()
    {
        base.Start();
        fireTimer = fireCooldown;
    }

    private void Update()
    {
        if (!IsServer) return;

        fireTimer -= Time.deltaTime;

        if (fireTimer <= 0f)
        {
            StartFire();
            fireTimer = fireCooldown;
        }

        HandleFireAnimation();
    }

    /* =========================
     * FIRE LOGIC
     * ========================= */

    private void StartFire()
    {
        hasSpawnedThisFire = false;
        FireClientRpc();
    }

    private void HandleFireAnimation()
    {
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        if (info.IsName("Fire"))
        {

            if (!hasSpawnedThisFire && info.normalizedTime >= fireSpawnNormalizedTime)
            {
                SpawnConTrai();
                hasSpawnedThisFire = true;
            }


            if (info.normalizedTime >= 1f)
            {
                EndFireClientRpc();
            }
        }
    }

    private void SpawnConTrai()
    {
        if (!IsServer) return;

        NetworkObject z = Instantiate(
            contraiPrefab,
            firePoint.position,
            Quaternion.identity
        );

        z.Spawn();

        ConTrai contrai = z.GetComponent<ConTrai>();
        if (contrai != null)
        {
            contrai.Init(ConTraiSpawnMode.Cannon);
        }
    }

    /* =========================
     * RPC
     * ========================= */

    [ClientRpc]
    private void FireClientRpc()
    {
        if (animator != null)
            animator.SetBool("isFiring", true);
    }

    [ClientRpc]
    private void EndFireClientRpc()
    {
        if (animator != null)
            animator.SetBool("isFiring", false);
    }
}
