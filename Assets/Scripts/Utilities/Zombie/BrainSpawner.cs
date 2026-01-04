using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class BrainSpawner : NetworkBehaviour
{
    public static BrainSpawner Instance { get; private set; }

    [Header("References")]
    public NetworkObject brainPrefab;

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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Chỉ server mới spawn brain
        if (!IsServer) return;

        StartCoroutine(InitializeSpawner());
    }

    IEnumerator InitializeSpawner()
    {
        // Wait for GameStateManager
        while (GameStateManager.Instance == null) yield return null;
        
        // Subscribe to game state changes
        GameStateManager.Instance.OnStateChanged += OnGameStateChanged;

        // Check initial state
        if (GameStateManager.Instance.CurrentState.Value == GameStateManager.GameState.Playing)
        {
            StartSpawning();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        StopSpawning();
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        if (!IsServer) return;

        if (newState == GameStateManager.GameState.Playing)
        {
            StartSpawning();
        }
        else
        {
            StopSpawning();
        }
    }

    private void StartSpawning()
    {
        if (!IsInvoking(nameof(SpawnBrainFromSky)))
        {
            Debug.Log("BrainSpawner: Starting brain spawning");
            InvokeRepeating(nameof(SpawnBrainFromSky), initialDelay, spawnInterval);
        }
    }

    private void StopSpawning()
    {
        if (IsInvoking(nameof(SpawnBrainFromSky)))
        {
            Debug.Log("BrainSpawner: Stopping brain spawning");
            CancelInvoke(nameof(SpawnBrainFromSky));
        }
    }

    void SpawnBrainFromSky()
    {
        if (!IsServer) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        float randX = Random.Range(minViewportX, maxViewportX);
        float z = -cam.transform.position.z;
        Vector3 spawnPos = cam.ViewportToWorldPoint(new Vector3(randX, spawnViewportY, z));
        spawnPos.z = 0f;

        float stopAfter = Random.Range(minFallDuration, maxFallDuration);

        // Spawn brain qua network
        SpawnBrainAtPosition(spawnPos, true, fallSpeed, stopAfter);
    }
    
    public void SpawnBrainAtWorldPosition(
        Vector3 worldPos,
        bool falling = false,
        float customFallSpeed = 0f)
    {
        if (!IsServer) return;

        float speed = customFallSpeed > 0f ? customFallSpeed : fallSpeed;
        float stopAfter = falling ? Random.Range(minFallDuration, maxFallDuration) : 0f;

        SpawnBrainAtPosition(worldPos, falling, speed, stopAfter);
    }

    private void SpawnBrainAtPosition(Vector3 position, bool falling, float speed, float stopAfter)
    {
        if (!IsServer || brainPrefab == null) return;

        NetworkObject brainInstance = Instantiate(brainPrefab, position, Quaternion.identity);
        brainInstance.Spawn(true);

        Debug.Log($"✅ Brain spawned by BrainSpawner: NetworkObjectId={brainInstance.NetworkObjectId}");

        // Gọi ClientRpc để setup falling animation trên tất cả client
        if (falling)
        {
            SetupBrainFallingClientRpc(brainInstance.NetworkObjectId, speed, stopAfter);
        }
    }

    [ClientRpc]
    private void SetupBrainFallingClientRpc(ulong brainId, float speed, float duration)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(brainId, out NetworkObject brainObj))
        {
            Rigidbody2D rb = brainObj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.down * speed;
                StartCoroutine(StopAfter(rb, duration));
            }
            else
            {
                StartCoroutine(FallRoutine(brainObj.transform, speed, duration));
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ Brain object not found for NetworkObjectId {brainId}");
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