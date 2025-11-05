using System.Collections;
using UnityEngine;

public class Sunflower : PlantBase
{
    [Header("Sun Spawn")]
    public float minSpawnInterval = 4.5f;
    public float maxSpawnInterval = 6.5f;

    [Tooltip("Initial spawn position relative to the plant (where the sun appears).")]
    public Vector3 spawnOffset = new Vector3(0f, 0.9f, 0f);

    [Tooltip("Final landing position relative to the plant (slightly below the face).")]
    public Vector3 landingOffset = new Vector3(0f, 0.35f, 0f);

    [Header("Bounce/Fall animation")]
    public float bounceHeight = 0.25f;         // how much it bounces up from spawn point
    public float bounceUpDuration = 0.12f;     // time to move up
    public float fallDuration = 0.45f;         // time to fall to landing point

    public bool useSpawner = false;             // if true, call SunSpawner.SpawnSunAtWorldPosition (no animation)
                                               // if false (default), Sunflower will instantiate and animate the prefab itself
    public float dropSpeed = 1.5f;              // kept for compatibility when using SunSpawner falling

    Coroutine spawnRoutine;

    protected override void Start()
    {
        base.Start();
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            float wait = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(wait);

            if (useSpawner)
            {
                // Use central spawner (it will handle falling). We pass falling=false so spawner doesn't start an auto-fall.
                if (SunSpawner.Instance != null && SunSpawner.Instance.sunPrefab != null)
                {
                    Vector3 spawnPos = transform.position + spawnOffset;
                    SunSpawner.Instance.SpawnSunAtWorldPosition(spawnPos, false, dropSpeed);
                }
            }
            else
            {
                // Instantiate and animate locally so we can do the bounce + fall to landingOffset
                if (SunSpawner.Instance != null && SunSpawner.Instance.sunPrefab != null)
                {
                    GameObject prefab = SunSpawner.Instance.sunPrefab;
                    Vector3 spawnPos = transform.position + spawnOffset;
                    GameObject s = Instantiate(prefab, spawnPos, Quaternion.identity);
                    StartCoroutine(AnimateSpawnedSun(s));
                }
            }
        }
    }

    IEnumerator AnimateSpawnedSun(GameObject sunGO)
    {
        if (sunGO == null) yield break;

        Transform t = sunGO.transform;
        Rigidbody2D rb = sunGO.GetComponent<Rigidbody2D>();
        Collider2D col = sunGO.GetComponent<Collider2D>();

        // disable physics so we control movement
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // small safety: ensure collider is enabled so player can click it later
        if (col != null) col.enabled = true;

        Vector3 initial = t.position;
        Vector3 bounceTarget = initial + Vector3.up * bounceHeight;
        Vector3 landTarget = transform.position + landingOffset;
        float elapsed = 0f;

        // Bounce up (ease out)
        elapsed = 0f;
        while (elapsed < bounceUpDuration && t != null)
        {
            float k = elapsed / bounceUpDuration;
            // ease-out quad
            k = 1f - (1f - k) * (1f - k);
            t.position = Vector3.Lerp(initial, bounceTarget, k);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (t != null) t.position = bounceTarget;

        // Fall down to landing (ease in)
        elapsed = 0f;
        while (elapsed < fallDuration && t != null)
        {
            float k = elapsed / fallDuration;
            // ease-in quad
            k = k * k;
            t.position = Vector3.Lerp(bounceTarget, landTarget, k);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (t != null) t.position = landTarget;

        // ensure sun stops moving and remains clickable; keep Rigidbody kinematic so it doesn't start falling
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }
    }

    void OnDisable()
    {
        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);
    }

    void OnDestroy()
    {
        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);
    }
}