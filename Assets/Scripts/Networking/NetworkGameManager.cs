using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode.Transports.UTP;
using System.Collections;

public class NetworkGameManager : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text playerRoleText;
    [SerializeField] private TMP_Text gameStatusText;
    [SerializeField] private TMP_Text resourceText;
    // Remove spawnButton reference
    
    [Header("Network")]
    [SerializeField] private NetworkManager networkManager;
    
    [Header("Spawn Settings")]
    [SerializeField] private NetworkPrefabsList networkPrefabs;
    [SerializeField] private GameObject peashooterPrefab; // Assign in inspector
    [SerializeField] private GameObject basicZombiePrefab; // Assign in inspector
    
    [Header("Camera")]
    [SerializeField] private Camera gameCamera;
    
    private PlayerRole playerRole;
    private bool isHost;
    private string roomCode;
    private bool canSpawn = false;
    
    // Resources - chỉ host quản lý
    private NetworkVariable<int> hostResources = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> clientResources = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Start()
    {
        // Get saved match info
        playerRole = (PlayerRole)System.Enum.Parse(typeof(PlayerRole), PlayerPrefs.GetString("PlayerRole"));
        isHost = PlayerPrefs.GetInt("IsHost") == 1;
        roomCode = PlayerPrefs.GetString("RoomCode");
        
        playerRoleText.text = $"Role: {playerRole}";
        gameStatusText.text = "Connecting...";
        
        // Remove spawn button setup
        
        // Setup camera if not assigned
        if (gameCamera == null)
        {
            gameCamera = Camera.main;
        }
        
        // Ensure NetworkManager is properly configured
        if (networkManager == null)
        {
            networkManager = NetworkManager.Singleton;
        }
        
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager not found!");
            gameStatusText.text = "Network setup error";
            return;
        }
        
        // Setup transport BEFORE setting up callbacks and starting
        if (!SetupTransport())
        {
            return;
        }
        
        // Setup network callbacks
        networkManager.OnServerStarted += OnServerStarted;
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        
        // Start networking
        StartCoroutine(StartNetworking());
    }

    void Update()
    {
        // Handle mouse click for spawning
        if (canSpawn && Input.GetMouseButtonDown(0))
        {
            HandleMouseClick();
        }
    }

    private void HandleMouseClick()
    {
        if (!IsSpawned || gameCamera == null) return;
        
        // Convert mouse position to world position
        Vector3 mousePos = Input.mousePosition;
        Ray ray = gameCamera.ScreenPointToRay(mousePos);
        
        // Raycast to find spawn position
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 spawnPosition = hit.point;
            
            // Validate spawn position based on player role
            if (IsValidSpawnPosition(spawnPosition))
            {
                SpawnUnitAtPositionServerRpc(spawnPosition);
            }
            else
            {
                Debug.Log("Invalid spawn position for your role!");
            }
        }
        else
        {
            // If no hit, spawn on a default plane (y = 0)
            Vector3 worldPos = gameCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, gameCamera.nearClipPlane + 10f));
            worldPos.y = 0f; // Set to ground level
            
            if (IsValidSpawnPosition(worldPos))
            {
                SpawnUnitAtPositionServerRpc(worldPos);
            }
        }
    }

    private bool IsValidSpawnPosition(Vector3 position)
    {
        // Check if player has enough resources
        bool hasResources = false;
        if (IsHost)
        {
            hasResources = hostResources.Value >= 25;
        }
        else if (IsClient)
        {
            hasResources = clientResources.Value >= 25;
        }

        if (!hasResources)
        {
            Debug.Log("Not enough resources to spawn!");
            return false;
        }

        // Validate spawn area based on role
        if (playerRole == PlayerRole.Plant)
        {
            // Plants can only spawn on the left side
            return position.x < 0f;
        }
        else // Zombie
        {
            // Zombies can only spawn on the right side
            return position.x > 0f;
        }
    }

    private bool SetupTransport()
    {
        var transport = networkManager.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("UnityTransport component not found on NetworkManager!");
            gameStatusText.text = "Transport setup error";
            return false;
        }

        // Configure transport for local development
        try 
        {
            if (isHost)
            {
                transport.SetConnectionData("127.0.0.1", 7777, "127.0.0.1");
            }
            else
            {
                transport.SetConnectionData("127.0.0.1", 7777);
            }
            
            Debug.Log($"Transport configured successfully. IsHost: {isHost}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to setup transport: {e.Message}");
            gameStatusText.text = "Transport configuration failed";
            return false;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to resource changes
        hostResources.OnValueChanged += OnResourcesChanged;
        clientResources.OnValueChanged += OnResourcesChanged;
        
        UpdateResourceDisplay();
    }

    private void OnResourcesChanged(int oldValue, int newValue)
    {
        UpdateResourceDisplay();
    }

    private void UpdateResourceDisplay()
    {
        if (IsHost)
        {
            resourceText.text = $"Your Resources: {hostResources.Value}\nOpponent: {clientResources.Value}";
        }
        else
        {
            resourceText.text = $"Your Resources: {clientResources.Value}\nOpponent: {hostResources.Value}";
        }
    }

    private IEnumerator StartNetworking()
    {
        yield return new WaitForSeconds(0.5f); // Wait for setup
        
        if (isHost)
        {
            gameStatusText.text = "Starting as host...";
            
            bool success = networkManager.StartHost();
            if (success)
            {
                gameStatusText.text = $"Hosting room: {roomCode}";
                Debug.Log($"Host started successfully. Room code: {roomCode}");
            }
            else
            {
                gameStatusText.text = "Failed to start host";
                Debug.LogError("Failed to start host");
            }
        }
        else
        {
            gameStatusText.text = "Joining game...";
            
            // Wait a bit for host to start
            yield return new WaitForSeconds(3f);
            
            bool success = networkManager.StartClient();
            if (!success)
            {
                gameStatusText.text = "Failed to connect";
                Debug.LogError("Failed to start client");
            }
        }
    }

    private void OnServerStarted()
    {
        Debug.Log("Server started successfully");
        gameStatusText.text = "Waiting for opponent...";
        
        // Start resource generation on server
        if (IsServer)
        {
            StartCoroutine(GenerateResources());
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
        
        if (networkManager.ConnectedClients.Count >= 2)
        {
            gameStatusText.text = "Game ready! Click to spawn units.";
            canSpawn = true;
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");
        gameStatusText.text = "Opponent disconnected";
        canSpawn = false;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnUnitAtPositionServerRpc(Vector3 spawnPosition, ServerRpcParams rpcParams = default)
    {
        var clientId = rpcParams.Receive.SenderClientId;
        bool isHostClient = clientId == NetworkManager.ServerClientId;
        
        // Check resources
        int currentResources = isHostClient ? hostResources.Value : clientResources.Value;
        
        if (currentResources >= 25)
        {
            // Deduct resources
            if (isHostClient)
            {
                hostResources.Value -= 25;
            }
            else
            {
                clientResources.Value -= 25;
            }
            
            // Determine role and spawn unit
            PlayerRole spawnRole = GetPlayerRoleForClient(clientId);
            GameObject unitPrefab = GetUnitPrefab(spawnRole);
            
            if (unitPrefab != null)
            {
                GameObject unitObject = Instantiate(unitPrefab, spawnPosition, Quaternion.identity);
                var networkObject = unitObject.GetComponent<NetworkObject>();
                
                if (networkObject != null)
                {
                    networkObject.SpawnWithOwnership(clientId);
                    Debug.Log($"Unit spawned for client {clientId} ({spawnRole}) at {spawnPosition}");
                }
                else
                {
                    Debug.LogError("NetworkObject component not found on unit prefab!");
                    Destroy(unitObject);
                }
            }
            else
            {
                Debug.LogError($"Unit prefab not found for role: {spawnRole}");
            }
        }
        else
        {
            Debug.Log($"Not enough resources to spawn unit. Current: {currentResources}, Required: 25");
        }
    }

    private GameObject GetUnitPrefab(PlayerRole role)
    {
        switch (role)
        {
            case PlayerRole.Plant:
                return peashooterPrefab;
            case PlayerRole.Zombie:
                return basicZombiePrefab;
            default:
                return null;
        }
    }

    private PlayerRole GetPlayerRoleForClient(ulong clientId)
    {
        bool isHostClient = clientId == NetworkManager.ServerClientId;
        
        if (isHost)
        {
            return isHostClient ? playerRole : (playerRole == PlayerRole.Plant ? PlayerRole.Zombie : PlayerRole.Plant);
        }
        else
        {
            return isHostClient ? (playerRole == PlayerRole.Plant ? PlayerRole.Zombie : PlayerRole.Plant) : playerRole;
        }
    }

    private IEnumerator GenerateResources()
    {
        while (IsServer && networkManager != null && networkManager.IsListening)
        {
            yield return new WaitForSeconds(1f);
            
            // Generate resources for both players
            hostResources.Value = Mathf.Min(hostResources.Value + 5, 200);
            clientResources.Value = Mathf.Min(clientResources.Value + 5, 200);
        }
    }

    public override void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnServerStarted -= OnServerStarted;
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
}