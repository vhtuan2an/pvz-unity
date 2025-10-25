using UnityEngine;

public class ProjectileBase : MonoBehaviour
{
    public float speed = 5f;
    public int damage = 20;
    public bool isFire = false;        // x2 damage
    public bool isSlow = false;
    public bool isSplash = false;
    public float slowAmount = 0f;      // 0 means no slow
    public float splashRadius = 0f;    // 0 means no splash

    protected virtual void Update()
    {
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Zombie"))
        {
            ZombieBase zombie = other.GetComponent<ZombieBase>();
            if (zombie != null)
            {
                int finalDamage = damage;
                if (isFire) finalDamage *= 2;
                zombie.TakeDamage(finalDamage);

                if (isSlow && slowAmount > 0f)
                {
                    zombie.ApplySlow(slowAmount);
                }

                if (isSplash && splashRadius > 0f)
                {
                    // Splash damage to nearby zombies
                    Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, splashRadius);
                    foreach (var hit in hits)
                    {
                        ZombieBase otherZombie = hit.GetComponent<ZombieBase>();
                        if (otherZombie != null && otherZombie != zombie)
                        {
                            otherZombie.TakeDamage(damage);
                        }
                    }
                }
            }
            Destroy(gameObject);
        }
    }
}