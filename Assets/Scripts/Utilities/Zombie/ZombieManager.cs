using UnityEngine;
using TMPro;
using Unity.Netcode;

public class ZombieManager : MonoBehaviour
{
    public static ZombieManager Instance { get; private set; }

    [Header("Brains Resource")]
    public int currentBrains = 50;


    [Header("UI")]
    public TextMeshProUGUI brainCounterText;

    // --- Selection ---
    public ZombieBase selectedZombie;      // zombie đang được chọn từ ZombiePacket
    private ZombiePacket selectedPacket;   // packet UI để gọi cooldown

    // --- Events ---
    public event System.Action OnZombieSpawnEvent;

    // --- Packet Management ---
    private System.Collections.Generic.List<ZombiePacket> allZombiePackets = new System.Collections.Generic.List<ZombiePacket>();

    // --- Preview System ---
    private GameObject previewObject;
    private SpriteRenderer previewRenderer;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        UpdateBrainsUI();
        CreatePreviewObject();
    }

    private void CreatePreviewObject()
    {
        previewObject = new GameObject("ZombiePreview");
        previewRenderer = previewObject.AddComponent<SpriteRenderer>();
        previewRenderer.sortingOrder = 100; // High sorting order to be visible
        // Set a default semi-transparent material or color if needed
        previewRenderer.color = new Color(1f, 1f, 1f, 0.6f); 
        previewObject.SetActive(false);
    }

    //==============================
    //  Resource Functions
    //==============================
    public void AddBrains(int amount)
    {
        currentBrains += amount;
        UpdateBrainsUI();
    }

    public void SpendBrains(int amount)
    {
        currentBrains -= amount;
        if (currentBrains < 0) currentBrains = 0;
        UpdateBrainsUI();
    }

    private void UpdateBrainsUI()
    {
        if (brainCounterText != null)
            brainCounterText.text = currentBrains.ToString();
    }

    public void OnBrainCollected(int value)
    {
        AddBrains(value);
    }

    //==============================
    //  Zombie Selection
    //==============================
    public void RegisterZombiePacket(ZombiePacket packet)
    {
        if (!allZombiePackets.Contains(packet))
        {
            allZombiePackets.Add(packet);
        }
    }

    public void SelectZombie(ZombieBase zombie, ZombiePacket packet = null)
    {
        selectedZombie = zombie;
        selectedPacket = packet;
        if (selectedZombie != null)
        {
            Debug.Log($"🧟 Selected zombie: {selectedZombie.name}, Cost: {selectedZombie.GetBrainCost()}");
            SetPreviewSprite(zombie.gameObject);
            
            // Dim other packets
            foreach (var p in allZombiePackets)
            {
                p.SetDimmed(p != packet);
            }
        }
    }

    public void SelectZombie(GameObject prefab, int cost, ZombiePacket packet = null)
    {
        if (prefab == null)
        {
            Debug.LogWarning("SelectZombie called with null prefab");
            return;
        }

        var zb = prefab.GetComponent<ZombieBase>();
        if (zb == null)
        {
            Debug.LogWarning($"SelectZombie: prefab {prefab.name} does not contain ZombieBase");
            return;
        }

        SelectZombie(zb, packet);
    }

    public void ClearSelection()
    {
        selectedZombie = null;
        selectedPacket = null;
        
        // Hide preview
        if (previewObject != null) previewObject.SetActive(false);
        
        // Undim all packets
        foreach (var p in allZombiePackets)
        {
            p.SetDimmed(false);
        }
    }

    //==============================
    //  Spawning
    //==============================

    public void TrySpawnZombieOnLane(Transform laneSpawnPoint)
    {
        if (GameStateManager.Instance != null && !GameStateManager.Instance.IsGameStarted) return;

        if (selectedZombie == null)
        {
            Debug.LogWarning("❌ No zombie selected!");
            return;
        }

        // Check role
        if (LobbyManager.Instance == null || LobbyManager.Instance.SelectedRole != PlayerRole.Zombie)
        {
            Debug.LogWarning("❌ Only Zombie player can spawn zombies!");
            return;
        }

        int cost = selectedZombie.GetBrainCost();

        if (currentBrains < cost)
        {
            Debug.LogWarning($"❌ Not enough brains! Current: {currentBrains}, need: {cost}");
            return;
        }

        if (NetworkGameManager.Instance == null)
        {
            Debug.LogError("❌ NetworkGameManager missing!");
            return;
        }

        Vector3 pos = laneSpawnPoint.position;
        ulong clientId = NetworkManager.Singleton.LocalClientId;

        // Debug logging for spawn attempt
        Debug.Log($"🧟 ZombieManager: Requesting spawn of {selectedZombie.name} at {pos} for client {clientId}");
        
        // Gọi network spawn với 3 tham số
        NetworkGameManager.Instance.SpawnZombieAtPosition(pos, selectedZombie.name, clientId);

        // Trừ brain
        currentBrains -= cost;
        UpdateBrainsUI();
        
        // Start cooldown
        if (selectedPacket != null)
        {
            selectedPacket.StartCooldown();
        }

        Debug.Log($"✅ Zombie spawn requested! Brains remaining: {currentBrains}");

        // Trigger Spawn Event (for Boss reaction)
        OnZombieSpawnEvent?.Invoke();
        
        // Clear selection after spawn (optional, keep selected for multi-spawn? usually clear)
        ClearSelection();
    }

    public void OnZombieSpawned(GameObject zombieObject)
    {
        Debug.Log($"🧟 Zombie spawned: {zombieObject.name}");
    }

    //  CLICK LANE TO SPAWN 
    void Update()
    {
        // 1. Check Game State (Must be Playing)
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState.Value != GameStateManager.GameState.Playing)
            return;

        if (LobbyManager.Instance == null || LobbyManager.Instance.SelectedRole != PlayerRole.Zombie)
            return;

        UpdatePreview();

        if (selectedZombie == null)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            // Block if mouse is over UI
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 pos2D = new Vector2(worldPos.x, worldPos.y);

            // Use RaycastAll to filter through any blocking colliders
            RaycastHit2D[] hits = Physics2D.RaycastAll(pos2D, Vector2.zero);

            foreach (var hit in hits)
            {
                ZombieLaneClick lane = hit.collider.GetComponent<ZombieLaneClick>();

                if (lane != null)
                {
                    lane.RequestSpawnZombieOnLane();
                    return; // Stop after finding the first valid lane
                }
            }
        }
    }
    
    private void UpdatePreview()
    {
        if (selectedZombie == null)
        {
            if (previewObject.activeSelf) previewObject.SetActive(false);
            return;
        }

        // Raycast to find lane under mouse
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 pos2D = new Vector2(worldPos.x, worldPos.y);
        RaycastHit2D[] hits = Physics2D.RaycastAll(pos2D, Vector2.zero);
        
        ZombieLaneClick lane = null;
        foreach (var hit in hits)
        {
            var l = hit.collider.GetComponent<ZombieLaneClick>();
            if (l != null)
            {
                lane = l;
                break;
            }
        }

        if (lane != null && lane.spawnPoint != null)
        {
            if (!previewObject.activeSelf) previewObject.SetActive(true);
            
            // Set position to lane's spawn point
            previewObject.transform.position = lane.spawnPoint.position;
            
            // Update Color based on cost
            int cost = selectedZombie.GetBrainCost();
            bool enoughBrains = currentBrains >= cost;
            previewRenderer.color = enoughBrains ? new Color(1f, 1f, 1f, 0.6f) : new Color(1f, 0.3f, 0.3f, 0.6f);
        }
        else
        {
            if (previewObject.activeSelf) previewObject.SetActive(false);
        }
    }

    private void SetPreviewSprite(GameObject prefab)
    {
        if (previewRenderer == null || prefab == null) return;
        
        // Try to get sprite from SpriteRenderer
        SpriteRenderer sr = prefab.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            previewRenderer.sprite = sr.sprite;
            // Also match scale
            previewObject.transform.localScale = prefab.transform.localScale;
            return;
        }
        
        // If animated, try to get first frame (simple approach) or sprite from child
        sr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            previewRenderer.sprite = sr.sprite;
            previewObject.transform.localScale = prefab.transform.localScale;
        }
    }
}
