using UnityEngine;
using System.Collections;

public class Peashooter : PlantBase
{
    [Header("Shooting Settings")]
    public GameObject peaPrefab;
    public Transform shootPoint;
    public float shootInterval = 1.425f;
    public float detectionRange = 15f;
    
    [Header("Layers")]
    public LayerMask zombieLayer;
    
    private float lastShootTime;

    protected override void Start()
    {
        maxHealth = 300;
        base.Start();
        lastShootTime = Time.time;

        // Auto-create ShootPoint if not assigned
        if (shootPoint == null)
        {
            GameObject sp = new GameObject("ShootPoint");
            sp.transform.parent = transform;
            sp.transform.localPosition = new Vector3(0.4f, 0.2f, 0f); // Set to mouth/cannon offset
            shootPoint = sp.transform;
        }
    }

    void Update()
    {
        if (IsZombieInRange() && Time.time - lastShootTime >= shootInterval)
        {
            Shoot();
            lastShootTime = Time.time;
        }
    }

    private void GetDetectionBox(out Vector2 center, out Vector2 size)
    {
        center = (Vector2)transform.position + Vector2.right * (detectionRange / 2f);
        size = new Vector2(detectionRange, 1f); // 1f is the height, adjust as needed
    }

    bool IsZombieInRange()
    {
        GetDetectionBox(out Vector2 boxCenter, out Vector2 boxSize);
        Collider2D hit = Physics2D.OverlapBox(boxCenter, boxSize, 0f, zombieLayer);
        return hit != null;
    }

    void Shoot()
    {
        if (peaPrefab != null && shootPoint != null)
        {
            Instantiate(peaPrefab, shootPoint.position, Quaternion.identity);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        GetDetectionBox(out Vector2 boxCenter, out Vector2 boxSize);
        Gizmos.DrawWireCube(boxCenter, boxSize);
    }
}