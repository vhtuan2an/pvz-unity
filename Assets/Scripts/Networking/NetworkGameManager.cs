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
        
        // QUAN TRỌNG: Set role cho cả Host và Client
        if (LobbyManager.Instance != null)
        {
            localPlayerRole = LobbyManager.Instance.SelectedRole;
            Debug.Log($"NetworkGameManager: Local player role set to {localPlayerRole} (IsServer: {IsServer}, IsClient: {IsClient})");
        }
        else
        {
            Debug.LogError("LobbyManager.Instance is null in OnNetworkSpawn!");
        }
        
        if (IsServer)
        {
            Debug.Log("Server started!");
        }
        
        if (IsClient)
        {
            Debug.Log("Client connected!");
        }

        // Debug prefab references
        Debug.Log($"Peashooter prefab assigned: {peashooterPrefab != null}");
        Debug.Log($"Zombie prefab assigned: {zombiePrefab != null}");
    }

    // Gọi hàm này khi player muốn spawn unit
    public void SpawnUnit()
    {
        Debug.Log($"SpawnUnit called! Local role: {localPlayerRole}");

        if (localPlayerRole == PlayerRole.None)
        {
            Debug.LogError("Player role not set!");
            return;
        }

        if (localPlayerRole == PlayerRole.Plant)
        {
            Debug.Log("Requesting spawn Peashooter...");
            RequestSpawnPlantServerRpc(AuthenticationService.Instance.PlayerId);
        }
        else if (localPlayerRole == PlayerRole.Zombie)
        {
            Debug.Log("Requesting spawn Zombie...");
            RequestSpawnZombieServerRpc(AuthenticationService.Instance.PlayerId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnPlantServerRpc(string playerId, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"Server: Spawning Peashooter for player {playerId}");
        
        if (peashooterPrefab == null)
        {
            Debug.LogError("Peashooter prefab is not assigned in NetworkGameManager!");
            return;
        }

        Vector3 spawnPosition = plantSpawnPoint != null 
            ? plantSpawnPoint.position 
            : new Vector3(-5f, 0f, -1f); // THÊM Z = -1 để ở phía trước camera

        GameObject plant = Instantiate(peashooterPrefab, spawnPosition, Quaternion.identity);
        NetworkObject networkObject = plant.GetComponent<NetworkObject>();
        
        if (networkObject != null)
        {
            networkObject.SpawnWithOwnership(rpcParams.Receive.SenderClientId);
            Debug.Log($"Peashooter spawned at {spawnPosition} and assigned to client {rpcParams.Receive.SenderClientId}");
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
        
        if (zombiePrefab == null)
        {
            Debug.LogError("Zombie prefab is not assigned in NetworkGameManager!");
            return;
        }

        Vector3 spawnPosition = zombieSpawnPoint != null 
            ? zombieSpawnPoint.position 
            : new Vector3(5f, 0f, -1f); // THÊM Z = -1 để ở phía trước camera

        GameObject zombie = Instantiate(zombiePrefab, spawnPosition, Quaternion.identity);
        NetworkObject networkObject = zombie.GetComponent<NetworkObject>();
        
        if (networkObject != null)
        {
            networkObject.SpawnWithOwnership(rpcParams.Receive.SenderClientId);
            Debug.Log($"Zombie spawned at {spawnPosition} and assigned to client {rpcParams.Receive.SenderClientId}");
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
        if (peashooterPrefab == null) return;

        GameObject plant = Instantiate(peashooterPrefab, position, Quaternion.identity);
        NetworkObject networkObject = plant.GetComponent<NetworkObject>();
        
        if (networkObject != null)
        {
            networkObject.SpawnWithOwnership(rpcParams.Receive.SenderClientId);
        }
        else
        {
            Destroy(plant);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnZombieAtPositionServerRpc(Vector3 position, string playerId, ServerRpcParams rpcParams = default)
    {
        if (zombiePrefab == null) return;

        GameObject zombie = Instantiate(zombiePrefab, position, Quaternion.identity);
        NetworkObject networkObject = zombie.GetComponent<NetworkObject>();
        
        if (networkObject != null)
        {
            networkObject.SpawnWithOwnership(rpcParams.Receive.SenderClientId);
        }
        else
        {
            Destroy(zombie);
        }
    }
}