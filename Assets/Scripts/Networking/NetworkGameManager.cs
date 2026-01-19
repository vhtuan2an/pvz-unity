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

    // Events
    public System.Action OnZombieSpawnedServer;

    [System.Serializable]
    public class ZombiePrefabMapping
    {
        public string zombieName;
        public GameObject prefab;
    }

    private void Awake()
    {
        Debug.Log($"NetworkGameManager: Awake called on {gameObject.name} (ID: {GetInstanceID()})");
        if (Instance != null && Instance != this)
        {
             Debug.LogWarning($"NetworkGameManager: Duplicate instance found. Destroying new one on {gameObject.name}.");
             Destroy(gameObject);
             return;
        }
        
        Instance = this;
        // REMOVED DontDestroyOnLoad - this manager should live and die with the GameScene 100% thinhdeptrai coded no ai
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
        // Block spawning if game hasn't started (e.g. during countdown)
        if (GameStateManager.Instance != null && !GameStateManager.Instance.IsGameStarted)
        {
             Debug.LogWarning("Cannot spawn plant yet! Game has not started.");
             return;
        }

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

        // Get sun cost and consume sun on server
        PlantBase plantBase = prefab.GetComponent<PlantBase>();
        if (plantBase != null && PlantManager.Instance != null)
        {
            int sunCost = plantBase.sunCost;
            PlantManager.Instance.SpendSun(sunCost);
            Debug.Log($"Server consumed {sunCost} sun for plant spawn");
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
        // Block spawning if game hasn't started (e.g. during countdown)
        if (GameStateManager.Instance != null && !GameStateManager.Instance.IsGameStarted)
        {
             Debug.LogWarning("Cannot spawn zombie yet! Game has not started.");
             return;
        }

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

        // Randomly play groan sound (e.g. 30% chance)
        if (Random.value < 0.3f) 
        {
            PlaySoundClientRpc("zombie_groan");
        }
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

        // Get brain cost and consume brains on server
        ZombieBase zombieBase = prefab.GetComponent<ZombieBase>();
        if (zombieBase != null && ZombieManager.Instance != null)
        {
            int brainCost = zombieBase.GetBrainCost();
            ZombieManager.Instance.SpendBrains(brainCost);
            Debug.Log($"Server consumed {brainCost} brains for zombie spawn");
        }

        GameObject zombie = Instantiate(prefab, position, Quaternion.identity);
        NetworkObject networkObject = zombie.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            networkObject.SpawnWithOwnership(ownerClientId);
            Debug.Log($"Zombie '{zombieName}' spawned at {position} (Server-owned for client {ownerClientId})");
            
            // Notify System (e.g. Boss)
            OnZombieSpawnedServer?.Invoke();
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
    public void OnZombieWin(NetworkObject winner)
    {
        if (!IsServer) return;

        Debug.Log($"🧟 Zombies win! Winner: {winner.name} ({winner.NetworkObjectId})");

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.EndGameServerRpc(PlayerRole.Zombie, winner.NetworkObjectId);
        }
    }

    public void OnPlantWin(NetworkObject winner)
    {
        if (!IsServer) return;

        Debug.Log($"🌱 Plants win! Winner: {winner.name} ({winner.NetworkObjectId})");

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.EndGameServerRpc(
                PlayerRole.Plant,
                winner.NetworkObjectId
            );
        }
    }


    [ClientRpc]
    private void ShowZombieWinClientRpc()
    {
        Debug.Log("🧟 [CLIENT] Zombies win! Showing Game Over screen.");
        
        if (ZombieWinUI.Instance != null)
        {
            ZombieWinUI.Instance.ShowZombieWin();
        }
        else
        {
            Debug.LogWarning("ZombieWinUI.Instance is null! Make sure ZombieWinUI is in the scene.");
        }
    }

    // ===================== HELPERS =====================
    // ===================== SOUND =====================
    [ClientRpc]
    public void PlaySoundClientRpc(string soundName, float volume = 1f, float pitch = 1f, bool ignorePause = false)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySound(soundName, volume, pitch, ignorePause);
        }
    }

    [ClientRpc]
    public void PlayLoopSoundClientRpc(string key, string clip, float volume, float pitch, bool excludeOwner = false)
    {
        // Removed IsHost check so Host player can hear sounds too
        SoundManager.Instance.PlayLoop(key, clip, volume, pitch);
    }

    [ClientRpc]
    public void StopSoundClientRpc(string key)
    {
        SoundManager.Instance.Stop(key);
    }


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

    // Disco Zombie: only 1 disco 
    public int DiscoAliveCount { get; private set; } = 0;


    public void RegisterDiscoZombie()
    {
        DiscoAliveCount++;

        if (DiscoAliveCount == 1)
        {
            PlayLoopSoundClientRpc("GLOBAL_DISCO", "disco", 0.7f, 1f, excludeOwner: true);
        }
    }


    public void UnregisterDiscoZombie()
    {
        DiscoAliveCount = Mathf.Max(0, DiscoAliveCount - 1);

        if (DiscoAliveCount == 0)
        {
            StopSoundClientRpc("GLOBAL_DISCO");
        }
    }



}
