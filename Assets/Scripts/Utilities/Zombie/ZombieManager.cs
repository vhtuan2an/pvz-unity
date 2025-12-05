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

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        UpdateBrainUI();
    }

    //==============================
    //  Resource Functions
    //==============================
    public void AddBrains(int amount)
    {
        currentBrains += amount;
        UpdateBrainUI();
    }

    public void SpendBrains(int amount)
    {
        currentBrains -= amount;
        if (currentBrains < 0) currentBrains = 0;
        UpdateBrainUI();
    }

    private void UpdateBrainUI()
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
    public void SelectZombie(ZombieBase zombie, ZombiePacket packet = null)
    {
        selectedZombie = zombie;
        selectedPacket = packet;
        if (selectedZombie != null)
        {
            Debug.Log($"🧟 Selected zombie: {selectedZombie.name}, Cost: {selectedZombie.GetBrainCost()}");
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
    }

    //==============================
    //  Spawning
    //==============================

    public void TrySpawnZombieOnLane(Transform laneSpawnPoint)
    {
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

        // Gọi network spawn với 3 tham số
        NetworkGameManager.Instance.SpawnZombieAtPosition(pos, selectedZombie.name, clientId);

        SpendBrains(cost);
        selectedPacket?.StartCooldown();
        ClearSelection();

        Debug.Log("🧟 Spawn request sent!");
    }

    public void OnZombieSpawned(GameObject zombieObject)
    {
        Debug.Log($"🧟 Zombie spawned: {zombieObject.name}");
    }

    //  CLICK LANE TO SPAWN 
    void Update()
    {
        if (LobbyManager.Instance == null || LobbyManager.Instance.SelectedRole != PlayerRole.Zombie)
            return;

        if (selectedZombie == null)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 pos2D = new Vector2(worldPos.x, worldPos.y);

            RaycastHit2D hit = Physics2D.Raycast(pos2D, Vector2.zero);

            if (hit.collider != null)
            {
                ZombieLaneClick lane = hit.collider.GetComponent<ZombieLaneClick>();

                if (lane != null)
                {
                    lane.RequestSpawnZombieOnLane(); // hàm public đã sửa
                }
            }
        }
    }
}
