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
            transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
    }

    void OnMouseDown()
    {
        if (!IsLocalPlayerZombie()) return;
        TryCollect();
    }

    void TryCollect()
    {
        if (isCollected) return;
        
        // Gọi ServerRpc để đồng bộ việc collect
        RequestCollectServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCollectServerRpc(ServerRpcParams rpcParams = default)
    {
        if (isCollected) return;
        
        // Thông báo cho client nào đã collect để cộng điểm
        ulong clientId = rpcParams.Receive.SenderClientId;
        NotifyCollectedClientRpc(clientId);
        
        // Đánh dấu đã collect
        isCollected = true;
        
        // Fly animation trên tất cả client
        StartFlyAnimationClientRpc();
    }

    [ClientRpc]
    private void NotifyCollectedClientRpc(ulong collectorClientId)
    {
        // Chỉ client thu thập mới được cộng điểm
        if (NetworkManager.Singleton.LocalClientId == collectorClientId)
        {
            if (IsLocalPlayerZombie())
            {
                ZombieManager.Instance?.OnBrainCollected(brainValue);
            }
        }
    }

    [ClientRpc]
    private void StartFlyAnimationClientRpc()
    {
        isCollected = true;
        StartCoroutine(FlyAndDie());
    }

    bool IsLocalPlayerZombie()
    {
        return LobbyManager.Instance != null && LobbyManager.Instance.SelectedRole == PlayerRole.Zombie;
    }

    IEnumerator FlyAndDie()
    {
        // Disable collider để không collect 2 lần
        if (col != null)
            col.enabled = false;

        while (Vector3.Distance(transform.position, collectTarget) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, collectTarget, 40f * Time.deltaTime);
            yield return null;
        }

        // Server despawn object
        if (IsServer)
        {
            DespawnBrain();
        }
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
            DespawnBrain();
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