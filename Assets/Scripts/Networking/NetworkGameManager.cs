using Unity.Netcode;
using UnityEngine;
using Unity.Services.Authentication;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private GameObject peashooterPrefab;
    [SerializeField] private GameObject zombiePrefab;

    [Header("Spawn Settings")]
    [SerializeField] private Transform plantSpawnPoint;
    [SerializeField] private Transform zombieSpawnPoint;

    private PlayerRole localPlayerRole = PlayerRole.None;

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
        
        if (IsServer)
        {
            Debug.Log("Server started!");
        }
        
        if (IsClient)
        {
            Debug.Log("Client connected!");
            // Lấy role từ LobbyManager
            if (LobbyManager.Instance != null)
            {
                localPlayerRole = LobbyManager.Instance.SelectedRole;
                Debug.Log($"Local player role: {localPlayerRole}");
            }
        }
    }

    // Gọi hàm này khi player muốn spawn unit
    public void SpawnUnit()
    {
        if (localPlayerRole == PlayerRole.None)
        {
            Debug.LogError("Player role not set!");
            return;
        }

        if (localPlayerRole == PlayerRole.Plant)
        {
            RequestSpawnPlantServerRpc(AuthenticationService.Instance.PlayerId);
        }
        else if (localPlayerRole == PlayerRole.Zombie)
        {
            RequestSpawnZombieServerRpc(AuthenticationService.Instance.PlayerId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnPlantServerRpc(string playerId, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"Server: Spawning Peashooter for player {playerId}");
        
        Vector3 spawnPosition = plantSpawnPoint != null 
            ? plantSpawnPoint.position 
            : new Vector3(-5f, 0f, 0f);

        GameObject plant = Instantiate(peashooterPrefab, spawnPosition, Quaternion.identity);
        NetworkObject networkObject = plant.GetComponent<NetworkObject>();
        
        if (networkObject != null)
        {
            networkObject.SpawnWithOwnership(rpcParams.Receive.SenderClientId);
            Debug.Log($"Peashooter spawned and assigned to client {rpcParams.Receive.SenderClientId}");
        }
        else
        {
            Debug.LogError("Peashooter prefab missing NetworkObject component!");
            Destroy(plant);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnZombieServerRpc(string playerId, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"Server: Spawning Zombie for player {playerId}");
        
        Vector3 spawnPosition = zombieSpawnPoint != null 
            ? zombieSpawnPoint.position 
            : new Vector3(5f, 0f, 0f);

        GameObject zombie = Instantiate(zombiePrefab, spawnPosition, Quaternion.identity);
        NetworkObject networkObject = zombie.GetComponent<NetworkObject>();
        
        if (networkObject != null)
        {
            networkObject.SpawnWithOwnership(rpcParams.Receive.SenderClientId);
            Debug.Log($"Zombie spawned and assigned to client {rpcParams.Receive.SenderClientId}");
        }
        else
        {
            Debug.LogError("Zombie prefab missing NetworkObject component!");
            Destroy(zombie);
        }
    }

    // Hàm để spawn ở vị trí cụ thể (cho gameplay sau này)
    public void SpawnUnitAtPosition(Vector3 position)
    {
        if (localPlayerRole == PlayerRole.Plant)
        {
            RequestSpawnPlantAtPositionServerRpc(position, AuthenticationService.Instance.PlayerId);
        }
        else if (localPlayerRole == PlayerRole.Zombie)
        {
            RequestSpawnZombieAtPositionServerRpc(position, AuthenticationService.Instance.PlayerId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnPlantAtPositionServerRpc(Vector3 position, string playerId, ServerRpcParams rpcParams = default)
    {
        GameObject plant = Instantiate(peashooterPrefab, position, Quaternion.identity);
        NetworkObject networkObject = plant.GetComponent<NetworkObject>();
        
        if (networkObject != null)
        {
            networkObject.SpawnWithOwnership(rpcParams.Receive.SenderClientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnZombieAtPositionServerRpc(Vector3 position, string playerId, ServerRpcParams rpcParams = default)
    {
        GameObject zombie = Instantiate(zombiePrefab, position, Quaternion.identity);
        NetworkObject networkObject = zombie.GetComponent<NetworkObject>();
        
        if (networkObject != null)
        {
            networkObject.SpawnWithOwnership(rpcParams.Receive.SenderClientId);
        }
    }
}