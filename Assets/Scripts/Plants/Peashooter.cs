using UnityEngine;
using Unity.Netcode;

public class Peashooter : PlantBase
{
    [Header("Combat")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private float attackRate = 1.5f;
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private LayerMask zombieLayer;

    private float attackTimer = 0f;
    private Animator animator;

    protected override void Start()
    {
        base.Start(); // Khởi tạo currentHealth từ PlantBase
        animator = GetComponent<Animator>();

        if (shootPoint == null)
            shootPoint = transform;

        Debug.Log($"Peashooter Start: HP={currentHealth}, projectilePrefab={projectilePrefab != null}");
    }

    private void Update()
    {
        if (!IsServer) return; // Chỉ server xử lý logic bắn

        attackTimer += Time.deltaTime;

        if (attackTimer >= attackRate)
        {
            if (CheckForZombies())
                TriggerShoot();
            else
                SetIdleAnimationClientRpc();

            attackTimer = 0f;
        }
    }

    private bool CheckForZombies()
    {
        if (shootPoint == null)
            shootPoint = transform;

        RaycastHit2D hit = Physics2D.Raycast(shootPoint.position, Vector2.right, detectionRange, zombieLayer);
        Debug.DrawRay(shootPoint.position, Vector2.right * detectionRange, Color.red);
        return hit.collider != null;
    }

    private void TriggerShoot()
    {
        if (!IsServer) return;

        TriggerShootAnimationClientRpc();
    }

    private void SpawnPea() // gọi bởi animation event
    {
        if (!IsServer) return;

        if (projectilePrefab == null) return;

        Vector3 spawnPosition = shootPoint.position + new Vector3(0.5f, 0.3f, 0);
        GameObject pea = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);

        NetworkObject netObj = pea.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn(true);
        else
            Destroy(pea);
    }

    [ClientRpc]
    private void TriggerShootAnimationClientRpc()
    {
        if (animator != null)
            animator.SetBool("isShooting", true);
    }

    [ClientRpc]
    private void SetIdleAnimationClientRpc()
    {
        if (animator != null)
            animator.SetBool("isShooting", false);
    }

    // Override TakeDamage để thêm animation hit, dùng currentHealth từ PlantBase
    public override void TakeDamage(int damage)
    {
        base.TakeDamage(damage);

        if (animator != null)
            animator.SetTrigger("Hit"); // optional
    }

    private void OnDrawGizmosSelected()
    {
        if (shootPoint == null)
            shootPoint = transform;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(shootPoint.position, Vector2.right * detectionRange);
    }
}
