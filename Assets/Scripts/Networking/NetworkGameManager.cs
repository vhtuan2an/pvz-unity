using Unity.Netcode;
using UnityEngine;
using Unity.Services.Authentication;
using System.Collections.Generic;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Header("Plant Prefabs - Assign by name")]
    [SerializeField] private List<PlantPrefabMapping> plantPrefabs = new List<PlantPrefabMapping>();

    [Header("Zombie Prefabs")]
    [SerializeField] private GameObject basicZombiePrefab;

    [Header("Spawn Settings")]
    [SerializeField] private Transform zombieSpawnPoint;

    private PlayerRole localPlayerRole = PlayerRole.None;

    [System.Serializable]
    public class PlantPrefabMapping
    {
        public string plantName; // "Peashooter", "Wallnut", etc.
        public GameObject prefab;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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

        if (IsServer)
        {
            Debug.Log("Server started!");
        }

        if (IsClient)
        {
            Debug.Log("Client connected!");
        }
    }

    // Plant Player gọi hàm này khi click vào Tile
    public void SpawnPlantAtPosition(Vector3 position, string plantName)
    {
        if (localPlayerRole != PlayerRole.Plant)
        {
            Debug.LogWarning("Only Plant player can spawn plants!");
            return;
        }

        Debug.Log($"Requesting spawn {plantName} at {position}");
        RequestSpawnPlantServerRpc(position, plantName, AuthenticationService.Instance.PlayerId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnPlantServerRpc(Vector3 position, string plantName, string playerId, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"Server: Spawning {plantName} for player {playerId} at {position}");

        // Find prefab by name
        GameObject prefab = plantPrefabs.Find(p => p.plantName == plantName)?.prefab;

        if (prefab == null)
        {
            Debug.LogError($"Plant prefab '{plantName}' not found in NetworkGameManager!");
            return;
        }

        // Spawn plant
        GameObject plant = Instantiate(prefab, position, Quaternion.identity);
        NetworkObject networkObject = plant.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            // ✅ SERVER ownership thay vì client ownership
            networkObject.Spawn(); // Server owns the plant
            Debug.Log($"{plantName} spawned at {position} (Server-owned)");

            // Notify all clients để update tile state
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
        // Find NetworkObject by ID
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
        {
            GameObject plant = netObj.gameObject;

            // Find tile at position
            Tile tile = FindTileAtPosition(position);

            if (tile != null && PlantManager.Instance != null)
            {
                PlantManager.Instance.OnPlantSpawned(plant, tile);
            }
        }
    }

    // Zombie Player spawn zombies (từ UI hoặc button)
    public void SpawnZombie()
    {
        if (localPlayerRole != PlayerRole.Zombie)
        {
            Debug.LogWarning("Only Zombie player can spawn zombies!");
            return;
        }

        Vector3 spawnPos = zombieSpawnPoint != null ? zombieSpawnPoint.position : new Vector3(10f, 0f, 0f);
        RequestSpawnZombieServerRpc(spawnPos, AuthenticationService.Instance.PlayerId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnZombieServerRpc(Vector3 position, string playerId, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"Server: Spawning Zombie for player {playerId} at {position}");

        if (basicZombiePrefab == null)
        {
            Debug.LogError("Zombie prefab not assigned!");
            return;
        }

        GameObject zombie = Instantiate(basicZombiePrefab, position, Quaternion.identity);
        NetworkObject networkObject = zombie.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            networkObject.SpawnWithOwnership(rpcParams.Receive.SenderClientId);
            Debug.Log($"Zombie spawned at {position}");
        }
        else
        {
            Debug.LogError("Zombie prefab missing NetworkObject component!");
            Destroy(zombie);
        }
    }

    // Utility: Tìm tile gần position nhất
    private Tile FindTileAtPosition(Vector3 position)
    {
        Tile[] tiles = FindObjectsOfType<Tile>();
        Tile closest = null;
        float minDist = float.MaxValue;

        foreach (var tile in tiles)
        {
            float dist = Vector3.Distance(tile.PlantWorldPosition, position);
            if (dist < minDist && dist < 0.5f) // threshold 0.5 units
            {
                minDist = dist;
                closest = tile;
            }
        }

        return closest;
    }
}