using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Authentication;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

[Header("Lobby Settings")]
[SerializeField] private int maxPlayers = 2;
[SerializeField] private float lobbyPollInterval = 5f; // Tăng từ 1.5f lên 3f
[SerializeField] private float lobbyHeartbeatInterval = 15f;
private float lastPollTime = 0f;
private float minPollInterval = 5f; // Minimum 3 seconds between polls
private int consecutiveErrors = 0;
private float maxPollInterval = 30f; // Maximum 30 seconds between polls


    public PlayerRole SelectedRole { get; private set; } = PlayerRole.None;
    public bool IsSearching { get; private set; }
    public Lobby CurrentLobby { get; private set; }

    // Events
    public event Action<PlayerRole> OnRoleSelected;
    public event Action OnMatchmakingStarted;
    public event Action<string> OnMatchFound; // lobbyId
    public event Action<string> OnMatchmakingFailed;
    public event Action OnMatchmakingCancelled;

    private bool isPolling;
    private float nextHeartbeat;
    private bool networkStarted = false;
    private Allocation hostAllocation;
    private JoinAllocation clientAllocation;

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

    public void SelectRole(PlayerRole role)
    {
        if (IsSearching)
        {
            Debug.LogWarning("Cannot change role while searching for match");
            return;
        }

        SelectedRole = role;
        OnRoleSelected?.Invoke(role);
        Debug.Log($"Role selected: {role}");
    }

    public async Task StartMatchmaking()
    {
        if (IsSearching)
        {
            Debug.LogWarning("Already searching for a match");
            return;
        }

        if (SelectedRole == PlayerRole.None)
        {
            OnMatchmakingFailed?.Invoke("Please select a role first");
            return;
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            OnMatchmakingFailed?.Invoke("Not authenticated");
            return;
        }

        IsSearching = true;
        OnMatchmakingStarted?.Invoke();

        try
        {
            Debug.Log("=== Starting Matchmaking ===");
            Debug.Log($"Player ID: {AuthenticationService.Instance.PlayerId}");
            Debug.Log($"Selected Role: {SelectedRole}");

            // Tìm kiếm lobby có role trống
            var availableLobbies = await FindAvailableLobbies();

            if (availableLobbies != null && availableLobbies.Count > 0)
            {
                // Join lobby có sẵn
                await JoinExistingLobby(availableLobbies[0]);
            }
            else
            {
                // Tạo lobby mới
                await CreateNewLobby();
            }
        }
        catch (LobbyServiceException ex)
        {
            IsSearching = false;
            Debug.LogError($"Lobby Service Error: {ex.Message} (Reason: {ex.Reason})");
            OnMatchmakingFailed?.Invoke($"Failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            IsSearching = false;
            Debug.LogError($"Failed to start matchmaking: {ex.Message}");
            OnMatchmakingFailed?.Invoke(ex.Message);
        }
    }

    private async Task<List<Lobby>> FindAvailableLobbies()
    {
        try
        {
            var queryFilters = new List<QueryFilter>
            {
                new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                new QueryFilter(QueryFilter.FieldOptions.MaxPlayers, maxPlayers.ToString(), QueryFilter.OpOptions.EQ)
            };

            var queryResponse = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Count = 10,
                Filters = queryFilters
            });

            Debug.Log($"Found {queryResponse.Results.Count} available lobbies");
            return queryResponse.Results;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error querying lobbies: {ex.Message}");
            return new List<Lobby>();
        }
    }

    private async Task CreateNewLobby()
    {
        var lobbyName = $"PvZ-{Guid.NewGuid().ToString().Substring(0, 8)}";
        
        var createOptions = new CreateLobbyOptions
        {
            IsPrivate = false,
            Player = CreatePlayerData(),
            Data = new Dictionary<string, DataObject>
            {
                { "gameMode", new DataObject(DataObject.VisibilityOptions.Public, "pvz-1v1") }
            }
        };

        CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createOptions);
        
        Debug.Log($"Created lobby: {CurrentLobby.Name} (ID: {CurrentLobby.Id})");
        Debug.Log($"Host role: {SelectedRole}");

        StartPolling();
    }

    private async Task JoinExistingLobby(Lobby lobby)
    {
        var joinOptions = new JoinLobbyByIdOptions
        {
            Player = CreatePlayerData()
        };

        CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id, joinOptions);
        
        Debug.Log($"Joined lobby: {CurrentLobby.Name} (ID: {CurrentLobby.Id})");
        Debug.Log($"Players in lobby: {CurrentLobby.Players.Count}/{CurrentLobby.MaxPlayers}");

        StartPolling();

        // Nếu đủ 2 players, bắt đầu game ngay
        if (CurrentLobby.Players.Count == maxPlayers)
        {
            OnMatchFound?.Invoke(CurrentLobby.Id);
            StartNetworkGame();
            IsSearching = false;
        }
    }

    private Player CreatePlayerData()
    {
        return new Player(
            id: AuthenticationService.Instance.PlayerId,
            data: new Dictionary<string, PlayerDataObject>
            {
                { "role", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, SelectedRole.ToString()) },
                { "username", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, AuthenticationService.Instance.PlayerName ?? "Player") }
            }
        );
    }

    private void StartPolling()
    {
        isPolling = true;
        nextHeartbeat = Time.time + lobbyHeartbeatInterval;
        PollLobby();
    }

    private async void PollLobby()
{
    while (isPolling && IsSearching && CurrentLobby != null)
    {
        // Ensure minimum time between polls
        float timeSinceLastPoll = Time.time - lastPollTime;
        if (timeSinceLastPoll < minPollInterval)
        {
            await Task.Delay(TimeSpan.FromSeconds(minPollInterval - timeSinceLastPoll));
        }
        
        lastPollTime = Time.time;

        try
        {
            // Heartbeat để giữ lobby sống (chỉ host mới cần)
            if (Time.time >= nextHeartbeat && IsLobbyHost())
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
                nextHeartbeat = Time.time + lobbyHeartbeatInterval;
                Debug.Log("Lobby heartbeat sent");
            }

            // Poll để cập nhật thông tin lobby
            CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);

            Debug.Log($"Lobby poll - Players: {CurrentLobby.Players.Count}/{CurrentLobby.MaxPlayers}");

            // Reset error counter on successful poll
            consecutiveErrors = 0;
            minPollInterval = 3f; // Reset to normal interval

            // Kiểm tra nếu đủ 2 players
            if (CurrentLobby.Players.Count == maxPlayers && IsSearching)
            {
                Debug.Log("=== Match Found! ===");
                LogLobbyPlayers();
                
                IsSearching = false;
                isPolling = false;
                OnMatchFound?.Invoke(CurrentLobby.Id);

                // Load scene then start network when scene loaded
                StartNetworkGame();
                return;
            }
        }
        catch (LobbyServiceException ex)
        {
            consecutiveErrors++;
            Debug.LogError($"Error polling lobby (attempt {consecutiveErrors}): {ex.Message}");
            
            // Handle rate limiting with exponential backoff
            if (ex.Message.Contains("Too Many Requests") || ex.Message.Contains("Rate Limited"))
            {
                minPollInterval = Mathf.Min(minPollInterval * 2f, maxPollInterval);
                Debug.LogWarning($"Rate limited! New poll interval: {minPollInterval}s");
                
                // If too many consecutive errors, stop polling
                if (consecutiveErrors >= 5)
                {
                    Debug.LogError("Too many consecutive errors, stopping polling");
                    await CancelMatchmaking();
                    OnMatchmakingFailed?.Invoke("Connection issues - too many errors");
                    return;
                }
            }
            else if (ex.Reason == LobbyExceptionReason.LobbyNotFound)
            {
                await CancelMatchmaking();
                OnMatchmakingFailed?.Invoke("Lobby was closed");
                return;
            }
        }
        catch (Exception ex)
        {
            consecutiveErrors++;
            Debug.LogError($"Unexpected error polling lobby: {ex.Message}");
            
            // If too many consecutive errors, stop polling
            if (consecutiveErrors >= 5)
            {
                Debug.LogError("Too many consecutive errors, stopping polling");
                await CancelMatchmaking();
                OnMatchmakingFailed?.Invoke("Connection issues - too many errors");
                return;
            }
        }
    }
}

    private void LogLobbyPlayers()
    {
        Debug.Log("=== Players in Lobby ===");
        foreach (var player in CurrentLobby.Players)
        {
            var role = player.Data.ContainsKey("role") ? player.Data["role"].Value : "Unknown";
            Debug.Log($"Player {player.Id}: Role = {role}");
        }
        Debug.Log("========================");
    }

    private bool IsLobbyHost()
    {
        return CurrentLobby != null && CurrentLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

    public async Task CancelMatchmaking()
    {
        if (!IsSearching || CurrentLobby == null)
            return;

        try
        {
            isPolling = false;

            if (IsLobbyHost())
            {
                await LobbyService.Instance.DeleteLobbyAsync(CurrentLobby.Id);
                Debug.Log("Lobby deleted (host left)");
            }
            else
            {
                await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, AuthenticationService.Instance.PlayerId);
                Debug.Log("Left lobby");
            }

            IsSearching = false;
            CurrentLobby = null;
            OnMatchmakingCancelled?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to cancel matchmaking: {ex.Message}");
        }
    }

    public string GetRoleDisplayName(PlayerRole role)
    {
        return role switch
        {
            PlayerRole.Plant => "Plants",
            PlayerRole.Zombie => "Zombies",
            _ => "None"
        };
    }

    private void OnDestroy()
    {
        if (IsSearching)
        {
            _ = CancelMatchmaking();
        }
    }

    private async void StartNetworkGame()
    {
        if (networkStarted) return;
        networkStarted = true;

        Debug.Log("Starting network game... (loading GameScene)");
        SceneManager.sceneLoaded += OnGameSceneLoadedStartNetwork;
        SceneManager.LoadScene("GameScene");
    }

    private async void OnGameSceneLoadedStartNetwork(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "GameScene") return;
        SceneManager.sceneLoaded -= OnGameSceneLoadedStartNetwork;

        bool isHost = CurrentLobby != null && CurrentLobby.HostId == AuthenticationService.Instance.PlayerId;

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager not found in GameScene. Make sure a NetworkManager exists in the scene.");
            return;
        }

        try
        {
            if (isHost)
            {
                Debug.Log("Starting as HOST with Relay");
                await StartHostWithRelay();
            }
            else
            {
                Debug.Log("Starting as CLIENT with Relay");
                await StartClientWithRelay();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to start network game: {ex.Message}");
        }
    }

    private async Task StartHostWithRelay()
    {
        try
        {
            // Tạo Relay allocation
            hostAllocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            Debug.Log($"Host allocation created: {hostAllocation.AllocationId}");

            // Lấy join code
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);
            Debug.Log($"Join code: {joinCode}");

            // Lưu join code vào lobby data
            await UpdateLobbyWithRelayData(joinCode);

            // Setup UTP transport với Relay
            var utpTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utpTransport.SetRelayServerData(hostAllocation.RelayServer.IpV4, (ushort)hostAllocation.RelayServer.Port, 
                hostAllocation.AllocationIdBytes, hostAllocation.Key, hostAllocation.ConnectionData);

            // Start host
            if (!NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.StartHost();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to start host with Relay: {ex.Message}");
            throw;
        }
    }

    private async Task StartClientWithRelay()
{
    try
    {
        // Lấy join code từ lobby với retry mechanism
        string joinCode = await GetJoinCodeWithRetry();
        if (string.IsNullOrEmpty(joinCode))
        {
            throw new Exception("No join code found in lobby after retries");
        }

        Debug.Log($"Joining with code: {joinCode}");

        // Join Relay allocation
        clientAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
        Debug.Log($"Client joined allocation: {clientAllocation.AllocationId}");

        // Setup UTP transport với Relay
        var utpTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        utpTransport.SetRelayServerData(clientAllocation.RelayServer.IpV4, (ushort)clientAllocation.RelayServer.Port,
            clientAllocation.AllocationIdBytes, clientAllocation.Key, clientAllocation.ConnectionData, clientAllocation.HostConnectionData);

        // Start client
        if (!NetworkManager.Singleton.IsClient)
            NetworkManager.Singleton.StartClient();
    }
    catch (Exception ex)
    {
        Debug.LogError($"Failed to start client with Relay: {ex.Message}");
        throw;
    }
}

// Thêm method mới để retry lấy join code
private async Task<string> GetJoinCodeWithRetry()
{
    int maxRetries = 10; // 10 lần thử
    float retryDelay = 0.5f; // 0.5 giây giữa các lần thử
    
    for (int i = 0; i < maxRetries; i++)
    {
        // Refresh lobby data
        if (CurrentLobby != null)
        {
            try
            {
                CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to refresh lobby: {ex.Message}");
            }
        }
        
        string joinCode = GetJoinCodeFromLobby();
        if (!string.IsNullOrEmpty(joinCode))
        {
            Debug.Log($"Found join code after {i + 1} attempts: {joinCode}");
            return joinCode;
        }
        
        Debug.Log($"Join code not found, retrying... ({i + 1}/{maxRetries})");
        await Task.Delay(TimeSpan.FromSeconds(retryDelay));
    }
    
    return null;
}

    private async Task UpdateLobbyWithRelayData(string joinCode)
    {
        if (CurrentLobby == null) return;

        try
        {
            var updateOptions = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "relayJoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                }
            };

            CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, updateOptions);
            Debug.Log($"Lobby updated with join code: {joinCode}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to update lobby with Relay data: {ex.Message}");
        }
    }

    private string GetJoinCodeFromLobby()
    {
        if (CurrentLobby?.Data == null) return null;
        
        return CurrentLobby.Data.ContainsKey("relayJoinCode") 
            ? CurrentLobby.Data["relayJoinCode"].Value 
            : null;
    }
}