using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class Sun : NetworkBehaviour
{
    [Header("Sun Settings")]
    public int sunValue = 50;
    public float lifetime = 8f;
    public float rotationSpeed = 50f;

    [Header("Collection")]
    public Vector2 collectTarget = new Vector2(-8.9f, 3.7f);
    public float collectSpeed = 25f;
    public float collectArrivalThreshold = 0.05f;

    [Header("Hover Collection")]
    public float hoverCollectRadius = 1.5f;

    private bool isCollected = false;
    private Collider2D col2d;
    private Rigidbody2D rb2d;
    private Coroutine autoDestroyRoutine;

    void Awake()
    {
        col2d = GetComponent<Collider2D>();
        rb2d = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        autoDestroyRoutine = StartCoroutine(AutoDestroyCoroutine());
        UpdateSunVisibility();
    }

    void Update()
    {
        if (!isCollected)
        {
            transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
            CheckHoverCollection();
        }
    }

    private void UpdateSunVisibility()
    {
        bool shouldShow = ShouldShowSun();

        if (col2d != null)
            col2d.enabled = shouldShow;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.enabled = shouldShow;
    }

    private bool ShouldShowSun()
    {
        if (LobbyManager.Instance != null)
            return LobbyManager.Instance.SelectedRole == PlayerRole.Plant;

        return true;
    }

    void CheckHoverCollection()
    {
        if (!ShouldShowSun()) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;

        if (Vector2.Distance(transform.position, mouseWorldPos) <= hoverCollectRadius)
            StartCollection();
    }

    void OnMouseDown()
    {
        if (!ShouldShowSun()) return;
        if (isCollected) return;
        StartCollection();
    }

    void StartCollection()
    {
        if (isCollected) return;
        isCollected = true;

        if (autoDestroyRoutine != null) StopCoroutine(autoDestroyRoutine);

        StartCoroutine(CollectRoutine());
    }

    IEnumerator CollectRoutine()
    {
        if (col2d != null) col2d.enabled = false;

        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
            rb2d.gravityScale = 0f;
            rb2d.bodyType = RigidbodyType2D.Kinematic;
        }

        while (Vector3.Distance(transform.position, collectTarget) > collectArrivalThreshold)
        {
            transform.position = Vector3.MoveTowards(transform.position, collectTarget, collectSpeed * Time.deltaTime);
            yield return null;
        }

        PlantManager.Instance?.AddSun(sunValue);
        
        // Request server to despawn this sun
        if (IsSpawned)
        {
            RequestDespawnServerRpc();
        }
        else
        {
            // Fallback if not spawned (shouldn't happen in normal gameplay)
            Destroy(gameObject);
        }
    }

    IEnumerator AutoDestroyCoroutine()
    {
        yield return new WaitForSeconds(lifetime);
        if (!isCollected)
        {
            // Request server to despawn this sun
            if (IsSpawned)
            {
                RequestDespawnServerRpc();
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDespawnServerRpc()
    {
        if (IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, hoverCollectRadius);
    }
}
