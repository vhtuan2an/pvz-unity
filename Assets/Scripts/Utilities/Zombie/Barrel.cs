using UnityEngine;
using Unity.Netcode;
using System.Collections;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Animator))]
public class Barrel : ZombieBase
{
    [Header("Barrel Settings")]
    [SerializeField] private int barrelDamage = 3;
    [SerializeField] private float barrelMoveSpeed = 3f;

    private bool exploded = false;
    private Rigidbody2D rb;

    private static readonly int ExplodeHash = Animator.StringToHash("Explode");

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        if (!IsServer) return;

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void FixedUpdate()
    {
        if (!IsServer || exploded) return;

        rb.MovePosition(
            rb.position + Vector2.left * barrelMoveSpeed * Time.fixedDeltaTime
        );
    }

    // ================= COLLISION =================
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer || exploded) return;

        PlantBase plant = collision.GetComponent<PlantBase>();
        if (plant != null)
        {
            plant.TakeDamage(barrelDamage);
            ExplodeInternal();
            return;
        }

        ZombieBase projectile = collision.GetComponent<ZombieBase>();
        if (projectile != null)
        {
            TakeDamage(projectile.GetDamage());
        }
    }

    // ================= DAMAGE =================
    public override void TakeDamage(int damage)
    {
        if (!IsServer || exploded) return;

        base.TakeDamage(damage);

        if (currentHealth.Value <= 0)
        {
            ExplodeInternal();
        }
    }

    // ================= EXPLODE =================
    private void ExplodeInternal()
    {
        if (exploded) return;
        exploded = true;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;


        TriggerExplodeClientRpc();


        if (IsServer)
        {
            StartCoroutine(WaitExplodeAndDespawn());
        }
    }

    [ClientRpc]
    private void TriggerExplodeClientRpc()
    {
        if (animator != null)
        {
            animator.SetTrigger(ExplodeHash);
        }
    }

    // ================= COROUTINE =================
    private IEnumerator WaitExplodeAndDespawn()
    {

        yield return null;

        float waitTime = 0.3f; 

        if (animator != null)
        {

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Explode"))
            {
                waitTime = state.length;
            }
            else
            {

                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName("Explode"))
                {
                    waitTime = next.length;
                }
            }
        }

        yield return new WaitForSeconds(waitTime);

        NetworkObject no = GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned)
        {
            no.Despawn();
        }

        Destroy(gameObject);
    }
}
