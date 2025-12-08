using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Header("Plant Prefabs - Assign by name")]
    [SerializeField] private List<PlantPrefabMapping> plantPrefabs = new List<PlantPrefabMapping>();

    [Header("Zombie Prefabs - Assign by name")]
    [SerializeField] private List<ZombiePrefabMapping> zombiePrefabs = new List<ZombiePrefabMapping>();

    private PlayerRole localPlayerRole = PlayerRole.None;

    [System.Serializable]
    public class PlantPrefabMapping
    {
        public string plantName;
        public GameObject prefab;
    }

    [System.Serializable]
    public class ZombiePrefabMapping
    {
        public string zombieName;
        public GameObject prefab;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (LobbyManager.Instance != null)
        {
            localPlayerRole = LobbyManager.Instance.SelectedRole;
            Debug.Log($"NetworkGameManager: Local player role set to {localPlayerRole}");
        }

        if (IsServer) Debug.Log("Server started!");
        if (IsClient) Debug.Log("Client connected!");
    }

    private void Update()
    {
        // Auto-sync localPlayerRole from LobbyManager if still None
        // This handles test mode where OnNetworkSpawn might have race conditions
        if (IsSpawned && localPlayerRole == PlayerRole.None && LobbyManager.Instance != null)
        {
            PlayerRole lobbyRole = LobbyManager.Instance.SelectedRole;
            if (lobbyRole != PlayerRole.None)
            {
                localPlayerRole = lobbyRole;
                Debug.Log($"[AUTO-SYNC] NetworkGameManager synced role from LobbyManager: {localPlayerRole}");
            }
        }
    }

    /// <summary>
    /// Gets the effective player role with fallback logic:
    /// 1. Use localPlayerRole if not None
    /// 2. Fall back to LobbyManager.Instance.SelectedRole if available
    /// 3. Return None if neither is available
    /// </summary>
    private PlayerRole GetEffectivePlayerRole()
    {
        if (localPlayerRole != PlayerRole.None)
            return localPlayerRole;

        if (LobbyManager.Instance != null)
            return LobbyManager.Instance.SelectedRole;

        return PlayerRole.None;
    }

    // ===================== PLANT =====================
    public void SpawnPlantAtPosition(Vector3 position, string plantName)
    {
        PlayerRole effectiveRole = GetEffectivePlayerRole();
        if (effectiveRole != PlayerRole.Plant)
        {
            Debug.LogWarning($"Only Plant player can spawn plants! (Current role: {effectiveRole})");
            return;
        }

        Debug.Log($"Requesting spawn {plantName} at {position}");
        RequestSpawnPlantServerRpc(position, plantName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnPlantServerRpc(Vector3 position, string plantName, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"Server: Spawning {plantName} for client {clientId} at {position}");

        GameObject prefab = plantPrefabs.Find(p => p.plantName == plantName)?.prefab;

        if (prefab == null)
        {
            Debug.LogError($"Plant prefab '{plantName}' not found!");
            return;
        }

        GameObject plant = Instantiate(prefab, position, Quaternion.identity);
        NetworkObject networkObject = plant.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            networkObject.Spawn();
            Debug.Log($"{plantName} spawned at {position} (Server-owned)");
            NotifyPlantSpawnedClientRpc(networkObject.NetworkObjectId, position);
        }
        else
        {
            Debug.LogError($"{plantName} prefab missing NetworkObject component!");
            Destroy(plant);
        }
    }

    [ClientRpc]
    private void NotifyPlantSpawnedClientRpc(ulong networkObjectId, Vector3 position)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
        {
            GameObject plant = netObj.gameObject;
            Tile tile = FindTileAtPosition(position);

            if (tile != null && PlantManager.Instance != null)
            {
                PlantManager.Instance.OnPlantSpawned(plant, tile);
            }
        }
    }

    // ===================== ZOMBIE =====================
    public void SpawnZombieAtPosition(Vector3 position, string zombieName, ulong ownerClientId)
    {
        PlayerRole effectiveRole = GetEffectivePlayerRole();
        
        // Enhanced debugging for intermittent spawn issues
        Debug.Log($"🧟 ZOMBIE SPAWN ATTEMPT: " +
                  $"localPlayerRole={localPlayerRole}, " +
                  $"LobbyRole={LobbyManager.Instance?.SelectedRole}, " +
                  $"effectiveRole={effectiveRole}, " +
                  $"zombie={zombieName}, " +
                  $"IsSpawned={IsSpawned}, " +
                  $"IsServer={IsServer}, " +
                  $"IsClient={IsClient}");
        
        if (effectiveRole != PlayerRole.Zombie)
        {
            Debug.LogWarning($"❌ ZOMBIE SPAWN BLOCKED: Current role is {effectiveRole}, not Zombie!");
            return;
        }

        Debug.Log($"✅ ZOMBIE SPAWN APPROVED: Sending ServerRpc for {zombieName}");
        RequestSpawnZombieServerRpc(position, zombieName, ownerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnZombieServerRpc(Vector3 position, string zombieName, ulong ownerClientId, ServerRpcParams rpcParams = default)
    {
        GameObject prefab = zombiePrefabs.Find(z => z.zombieName == zombieName)?.prefab;

        if (prefab == null)
        {
            Debug.LogError($"Zombie prefab '{zombieName}' not found!");
            return;
        }

        GameObject zombie = Instantiate(prefab, position, Quaternion.identity);
        NetworkObject networkObject = zombie.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            networkObject.SpawnWithOwnership(ownerClientId);
            Debug.Log($"Zombie '{zombieName}' spawned at {position} (Server-owned for client {ownerClientId})");
        }
        else
        {
            Debug.LogError($"Zombie prefab '{zombieName}' missing NetworkObject component!");
            Destroy(zombie);
        }
    }

    // ===================== DESPAWN =====================
    public void DespawnPlantByNetworkId(ulong networkObjectId)
    {
        PlayerRole effectiveRole = GetEffectivePlayerRole();
        if (effectiveRole != PlayerRole.Plant)
        {
            Debug.LogWarning($"Only Plant player can despawn plants! (Current role: {effectiveRole})");
            return;
        }

        RequestDespawnPlantServerRpc(networkObjectId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDespawnPlantServerRpc(ulong networkObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
        {
            Debug.Log($"Server despawning plant: {netObj.gameObject.name} (NetworkId: {networkObjectId})");
            netObj.Despawn();
        }
        else
        {
            Debug.LogWarning($"Cannot despawn: NetworkObject {networkObjectId} not found!");
        }
    }

    // ===================== HELPERS =====================
    private Tile FindTileAtPosition(Vector3 position)
    {
        Tile[] tiles = FindObjectsByType<Tile>(FindObjectsSortMode.None);
        Tile closest = null;
        float minDist = float.MaxValue;

        foreach (var tile in tiles)
        {
            float dist = Vector3.Distance(tile.PlantWorldPosition, position);
            if (dist < minDist && dist < 0.5f)
            {
                minDist = dist;
                closest = tile;
            }
        }

        return closest;
    }
}
