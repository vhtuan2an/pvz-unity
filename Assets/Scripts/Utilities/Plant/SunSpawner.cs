using System.Collections;
using UnityEngine;

public class SunSpawner : MonoBehaviour
{
    public static SunSpawner Instance { get; private set; }

    [Header("References")]
    public GameObject sunPrefab;

    [Header("Timing")]
    public float initialDelay = 1f;
    public float spawnInterval = 10f;

    [Header("Spawn Area (viewport)")]
    [Range(0f, 1f)] public float minViewportX = 0.05f;
    [Range(0f, 1f)] public float maxViewportX = 0.95f;
    public float spawnViewportY = 1.15f;

    [Header("Falling")]
    public float fallSpeed = 2f;
    public float minFallDuration = 0.8f;
    public float maxFallDuration = 1.8f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (sunPrefab == null) return;
        InvokeRepeating(nameof(SpawnSunFromSky), initialDelay, spawnInterval);
    }

    void SpawnSunFromSky()
    {
        if (sunPrefab == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        float randX = Random.Range(minViewportX, maxViewportX);
        float z = -cam.transform.position.z;
        Vector3 spawnPos = cam.ViewportToWorldPoint(new Vector3(randX, spawnViewportY, z));
        spawnPos.z = 0f;

        GameObject s = Instantiate(sunPrefab, spawnPos, Quaternion.identity);

        float stopAfter = Random.Range(minFallDuration, maxFallDuration);

        Rigidbody2D rb = s.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.down * fallSpeed;
            StartCoroutine(StopAfter(rb, stopAfter));
        }
        else
        {
            StartCoroutine(FallRoutine(s.transform, fallSpeed, stopAfter));
        }
    }

    public void SpawnSunAtWorldPosition(Vector3 worldPos, bool falling = false, float customFallSpeed = 0f)
    {
        if (sunPrefab == null) return;

        GameObject s = Instantiate(sunPrefab, worldPos, Quaternion.identity);

        float speed = customFallSpeed > 0f ? customFallSpeed : fallSpeed;
        if (falling)
        {
            float stopAfter = Random.Range(minFallDuration, maxFallDuration);
            Rigidbody2D rb = s.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.down * speed;
                StartCoroutine(StopAfter(rb, stopAfter));
            }
            else
            {
                StartCoroutine(FallRoutine(s.transform, speed, stopAfter));
            }
        }
    }

    IEnumerator FallRoutine(Transform t, float speed, float duration)
    {
        float elapsed = 0f;
        while (t != null && elapsed < duration)
        {
            t.Translate(Vector3.down * speed * Time.deltaTime, Space.World);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator StopAfter(Rigidbody2D rb, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (rb == null) yield break;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }
}
