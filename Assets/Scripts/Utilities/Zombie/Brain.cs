using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class Brain : NetworkBehaviour
{
    [Header("Settings")]
    public int brainValue = 25;
    public float lifetime = 10f;
    public float rotationSpeed = 50f;
    public float hoverCollectRadius = 1.5f;
    public Vector2 collectTarget = new Vector2(8.9f, 3.7f);

    private bool isCollected = false;
    private SpriteRenderer sr;
    private Collider2D col;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    void Start()
    {
        Invoke(nameof(AutoDespawn), lifetime);
        UpdateBrainVisibility();
    }

    void Update()
    {
        if (!isCollected)
        {
            transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
            CheckHoverCollection();
        }
    }

    void CheckHoverCollection()
    {
        if (!ShouldShowBrain()) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;

        if (Vector2.Distance(transform.position, mouseWorldPos) <= hoverCollectRadius)
        {
            TryCollect();
        }
    }

    void OnMouseDown()
    {
        if (!IsLocalPlayerZombie()) return;
        TryCollect();
    }

    void TryCollect()
    {
        if (isCollected) return;
        isCollected = true;
        
        // 1. Play Sound Locally
        SoundManager.Instance.PlaySound("collect");

        // 2. Add Score Locally
        

        // 3. Start Animation locally
        CancelInvoke(nameof(AutoDespawn));
        StartCoroutine(FlyAndDie());
    }

    // Removed RequestCollectServerRpc, NotifyCollectedClientRpc, StartFlyAnimationClientRpc
    // as they are no longer needed for local-first collection.
    
    bool IsLocalPlayerZombie()
    {
        return LobbyManager.Instance != null && LobbyManager.Instance.SelectedRole == PlayerRole.Zombie;
    }
    
    // ...

    IEnumerator FlyAndDie()
    {
        // Disable collider 
        if (col != null) col.enabled = false;

        // Fly to target
        while (Vector3.Distance(transform.position, collectTarget) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, collectTarget, 40f * Time.deltaTime);
            yield return null;
        }

        // On finished, tell server to destroy this object (collected = true)
        RequestDespawnServerRpc(true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDespawnServerRpc(bool wasCollected)
    {
        if (!IsServer) return;
        
        // Only add brains if it was collected
        if (wasCollected)
        {
            ZombieManager.Instance?.AddBrains(brainValue);
        }
        
        DespawnBrain();
    }

    private void UpdateBrainVisibility()
    {
        bool shouldShow = ShouldShowBrain();

        if (col != null)
            col.enabled = shouldShow;

        if (sr != null)
            sr.enabled = shouldShow;
    }

    private bool ShouldShowBrain()
    {
        if (LobbyManager.Instance != null)
            return LobbyManager.Instance.SelectedRole == PlayerRole.Zombie;

        return true;
    }

    void AutoDespawn()
    {
        if (!isCollected && IsServer)
        {
            // Brain expired without being collected (wasCollected = false)
            RequestDespawnServerRpc(false);
        }
    }

    private void DespawnBrain()
    {
        NetworkObject netObj = GetComponent<NetworkObject>();
        
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
        
        Destroy(gameObject);
    }
}