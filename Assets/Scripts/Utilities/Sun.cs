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
        if (IsServer)
        {
            autoDestroyRoutine = StartCoroutine(AutoDestroyCoroutine());
        }
    }

    void Update()
    {
        // ✅ Chỉ Server mới update rotation/position
        if (IsServer && !isCollected)
        {
            transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
        }
        
        // ✅ Client chỉ check hover (input handling)
        if (!isCollected)
        {
            CheckHoverCollection();
        }
    }

    void CheckHoverCollection()
    {
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;

        float distance = Vector2.Distance(transform.position, mouseWorldPos);
        
        if (distance <= hoverCollectRadius)
        {
            StartCollection();
        }
    }

    void OnMouseDown()
    {
        if (isCollected) return;
        StartCollection();
    }

    void StartCollection()
    {
        if (isCollected) return;
        
        // ✅ Request server to collect
        RequestCollectServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCollectServerRpc(ServerRpcParams rpcParams = default)
    {
        if (isCollected) return;
        
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        if (autoDestroyRoutine != null) StopCoroutine(autoDestroyRoutine);
        
        // ✅ Notify all clients to play collect animation
        CollectClientRpc(clientId);
        
        // ✅ Add sun value on server
        if (PlantManager.Instance != null)
        {
            PlantManager.Instance.AddSun(sunValue);
        }
    }

    [ClientRpc]
    private void CollectClientRpc(ulong collectingClientId)
    {
        if (isCollected) return;
        
        StartCoroutine(CollectRoutine(collectingClientId));
    }

    Vector3 GetWorldPositionFromUI(RectTransform uiRect)
    {
        if (uiRect == null)
            return new Vector3(collectTarget.x, collectTarget.y, 0f);

        Canvas canvas = uiRect.GetComponentInParent<Canvas>();
        Camera uiCam = null;
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                uiCam = canvas.worldCamera;
            else if (canvas.renderMode == RenderMode.WorldSpace)
                return uiRect.position;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCam, uiRect.position);
        Camera worldCam = uiCam != null ? uiCam : Camera.main;
        if (worldCam == null)
            return new Vector3(collectTarget.x, collectTarget.y, 0f);

        float camZ = Mathf.Abs(worldCam.transform.position.z);
        Vector3 worldPoint = worldCam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, camZ));
        worldPoint.z = 0f;
        return worldPoint;
    }

    IEnumerator CollectRoutine(ulong collectingClientId)
    {
        isCollected = true;

        if (col2d != null) col2d.enabled = false;

        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
            rb2d.gravityScale = 0f;
            rb2d.bodyType = RigidbodyType2D.Kinematic;
        }

        RectTransform uiTargetRect = null;
        if (PlantManager.Instance != null && PlantManager.Instance.countText != null)
            uiTargetRect = PlantManager.Instance.countText.GetComponent<RectTransform>();
        else
        {
            var go = GameObject.Find("CountText") ?? GameObject.Find("SunCounter");
            if (go != null) uiTargetRect = go.GetComponent<RectTransform>();
        }

        while (gameObject != null)
        {
            Vector3 targetWorld = GetWorldPositionFromUI(uiTargetRect);
            if (Vector3.Distance(transform.position, targetWorld) <= collectArrivalThreshold)
                break;

            transform.position = Vector3.MoveTowards(transform.position, targetWorld, collectSpeed * Time.deltaTime);
            yield return null;
        }

        // ✅ Only server despawns
        if (IsServer)
        {
            DespawnSun();
        }
    }

    IEnumerator AutoDestroyCoroutine()
    {
        yield return new WaitForSeconds(lifetime);
        if (!isCollected && IsServer)
        {
            DespawnSun();
        }
    }

    private void DespawnSun()
    {
        if (!IsServer) return;
        
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, hoverCollectRadius);
    }
}