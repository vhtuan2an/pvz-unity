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
using System.Linq;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("Lobby Settings")]
    [SerializeField] private int maxPlayers = 2;
    // [SerializeField] private float lobbyPollInterval = 3f;
    [SerializeField] private float lobbyHeartbeatInterval = 1.5f;
    private float lastPollTime = 0f;
    private float minPollInterval = 10f; // Minimum 10 seconds between polls
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

    // public async Task StartMatchmaking()
    // {
    //     if (IsSearching)
    //     {
    //         Debug.LogWarning("Already searching for a match");
    //         return;
    //     }

    //     if (SelectedRole == PlayerRole.None)
    //     {
    //         OnMatchmakingFailed?.Invoke("Please select a role first");
    //         return;
    //     }

    //     if (!AuthenticationService.Instance.IsSignedIn)
    //     {
    //         OnMatchmakingFailed?.Invoke("Not authenticated");
    //         return;
    //     }

    //     IsSearching = true;
    //     OnMatchmakingStarted?.Invoke();

    //     try
    //     {
    //         Debug.Log("=== Starting Matchmaking ===");
    //         Debug.Log($"Player ID: {AuthenticationService.Instance.PlayerId}");
    //         Debug.Log($"Selected Role: {SelectedRole}");

    //         // Tìm kiếm lobby có role trống
    //         var availableLobbies = await FindAvailableLobbies();

    //         if (availableLobbies != null && availableLobbies.Count > 0)
    //         {
    //             // Join lobby có sẵn
    //             await JoinExistingLobby(availableLobbies[0]);
    //         }
    //         else
    //         {
    //             // Tạo lobby mới
    //             await CreateNewLobby();
    //         }
    //     }
    //     catch (LobbyServiceException ex)
    //     {
    //         IsSearching = false;
    //         Debug.LogError($"Lobby Service Error: {ex.Message} (Reason: {ex.Reason})");
    //         OnMatchmakingFailed?.Invoke($"Failed: {ex.Message}");
    //     }
    //     catch (Exception ex)
    //     {
    //         IsSearching = false;
    //         Debug.LogError($"Failed to start matchmaking: {ex.Message}");
    //         OnMatchmakingFailed?.Invoke(ex.Message);
    //     }
    // }

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

        IsSearching = true;
        StartPolling();
    }

    // private async Task JoinExistingLobby(Lobby lobby)
    // {
    //     var joinOptions = new JoinLobbyByIdOptions
    //     {
    //         Player = CreatePlayerData()
    //     };

    //     CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id, joinOptions);

    //     Debug.Log($"Joined lobby: {CurrentLobby.Name} (ID: {CurrentLobby.Id})");
    //     Debug.Log($"Players in lobby: {CurrentLobby.Players.Count}/{CurrentLobby.MaxPlayers}");

    //     StartPolling();

    //     // Nếu đủ 2 players, bắt đầu game ngay
    //     if (CurrentLobby.Players.Count == maxPlayers)
    //     {
    //         OnMatchFound?.Invoke(CurrentLobby.Id);
    //         StartNetworkGame();
    //         IsSearching = false;
    //     }
    // }

    private Player CreatePlayerData()
    {
        // Get the actual username from UnityAuthManager (which stores the login username)
        string username = UnityAuthManager.Instance?.GetPlayerName() ?? "Player";
        
        return new Player(
            id: AuthenticationService.Instance.PlayerId,
            data: new Dictionary<string, PlayerDataObject>
            {
                { "role", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, SelectedRole.ToString()) },
                { "username", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, username) }
            }
        );
    }

    private void StartPolling()
    {
        nextHeartbeat = Time.time + lobbyHeartbeatInterval;
        PollLobby();
    }

    private async void PollLobby()
    {
        if (isPolling) return; // guard: ensure single poll loop
        isPolling = true;

        float baseInterval = Mathf.Max(10f, minPollInterval); // safer minimum, increased from 3f
        float currentInterval = baseInterval;
        consecutiveErrors = 0;

        try
        {
            while (isPolling && IsSearching && CurrentLobby != null)
            {
                // Add small random jitter to avoid thundering herd (0-2s)
                float jitter = UnityEngine.Random.Range(0f, 2f);
                await Task.Delay(TimeSpan.FromSeconds(currentInterval + jitter));

                try
                {
                    // Only host should send heartbeat frequently
                    if (IsLobbyHost() && Time.time >= nextHeartbeat)
                    {
                        await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
                        nextHeartbeat = Time.time + lobbyHeartbeatInterval;
                        Debug.Log("Lobby heartbeat sent");
                    }

                    // Poll lobby state
                    CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
                    Debug.Log($"Lobby poll - Players: {CurrentLobby.Players.Count}/{CurrentLobby.MaxPlayers}");

                    // Reset backoff on success
                    consecutiveErrors = 0;
                    currentInterval = baseInterval;

                    // Check match condition
                    if (CurrentLobby.Players.Count == maxPlayers && IsSearching)
                    {
                        Debug.Log("=== Match Found! ===");
                        LogLobbyPlayers();

                        IsSearching = false;
                        isPolling = false;
                        OnMatchFound?.Invoke(CurrentLobby.Id);
                        StartNetworkGame();
                        break;
                    }
                }
                catch (LobbyServiceException lex)
                {
                    consecutiveErrors++;
                    string msg = lex.Message ?? lex.Reason.ToString();
                    Debug.LogError($"Error polling lobby (attempt {consecutiveErrors}): {msg}");

                    // Detect rate limit
                    bool isRateLimit = lex.Message?.Contains("Too Many Requests") == true
                                       || lex.Message?.Contains("Rate Limited") == true
                                       || lex.Reason == LobbyExceptionReason.Unknown;

                    if (isRateLimit)
                    {
                        // Exponential backoff with cap + jitter
                        currentInterval = Mathf.Min(currentInterval * 2f, 60f); // cap at 60s
                        currentInterval += UnityEngine.Random.Range(0f, 5f);
                        Debug.LogWarning($"Rate limited -> backing off. New interval: {currentInterval}s");
                    }
                    else if (lex.Reason == LobbyExceptionReason.LobbyNotFound)
                    {
                        await CancelMatchmaking();
                        OnMatchmakingFailed?.Invoke("Lobby was closed");
                        break;
                    }
                    else
                    {
                        // transient error: moderate backoff
                        currentInterval = Mathf.Min(currentInterval * 1.5f, 30f);
                        Debug.LogWarning($"Transient lobby error, increasing interval to {currentInterval}s");
                    }

                    if (consecutiveErrors >= 6)
                    {
                        Debug.LogError("Too many consecutive errors polling lobby, cancelling matchmaking.");
                        await CancelMatchmaking();
                        OnMatchmakingFailed?.Invoke("Connection issues - polling failed repeatedly");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    consecutiveErrors++;
                    Debug.LogError($"Unexpected error polling lobby: {ex.Message}");
                    currentInterval = Mathf.Min(currentInterval * 1.5f, 30f);

                    if (consecutiveErrors >= 6)
                    {
                        Debug.LogError("Too many consecutive errors polling lobby, cancelling matchmaking.");
                        await CancelMatchmaking();
                        OnMatchmakingFailed?.Invoke("Connection issues - polling failed repeatedly");
                        break;
                    }
                }
            }
        }
        finally
        {
            isPolling = false;
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

    private void StartNetworkGame()
    {
        if (networkStarted) return;
        networkStarted = true;

        isPolling = false;
        IsSearching = false;

        Debug.Log("Match found! Loading connection screen...");
        SceneManager.LoadScene("LoadingScene");
    }

    public bool IsLobbyHostPublic()
    {
        return CurrentLobby != null && CurrentLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

    public async Task StartHostConnection()
    {
        if (NetworkManager.Singleton == null)
        {
            throw new Exception("NetworkManager not found");
        }

        Debug.Log("Host creating Relay allocation...");
        hostAllocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);
        Debug.Log($"Relay created with joinCode: {joinCode}");

        await UpdateLobbyWithRelayData(joinCode);

        var utpTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        utpTransport.SetRelayServerData(
            hostAllocation.RelayServer.IpV4,
            (ushort)hostAllocation.RelayServer.Port,
            hostAllocation.AllocationIdBytes,
            hostAllocation.Key,
            hostAllocation.ConnectionData
        );

        if (!NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.StartHost();

        Debug.Log("Host started successfully");
    }

    public async Task StartClientConnection()
    {
        if (NetworkManager.Singleton == null)
        {
            throw new Exception("NetworkManager not found");
        }

        Debug.Log("Client waiting for join code...");
        await Task.Delay(1000);

        string joinCode = await GetJoinCodeWithRetry(maxRetries: 10, initialDelay: 2f);
        if (string.IsNullOrEmpty(joinCode))
        {
            throw new Exception("No join code found");
        }

        Debug.Log($"Client joining with code: {joinCode}");
        clientAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        var utpTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        utpTransport.SetRelayServerData(
            clientAllocation.RelayServer.IpV4,
            (ushort)clientAllocation.RelayServer.Port,
            clientAllocation.AllocationIdBytes,
            clientAllocation.Key,
            clientAllocation.ConnectionData,
            clientAllocation.HostConnectionData
        );

        if (!NetworkManager.Singleton.IsClient)
            NetworkManager.Singleton.StartClient();

        Debug.Log("Client started successfully");
    }

    public void ResetNetworkState()
    {
        networkStarted = false;
        isPolling = false;
        IsSearching = false;
        CurrentLobby = null;
    }





    private async Task<string> GetJoinCodeWithRetry(int maxRetries = 10, float initialDelay = 2f)
    {
        float currentDelay = initialDelay;
        const float maxDelay = 10f;
        const float backoffMultiplier = 1.5f;

        for (int i = 0; i < maxRetries; i++)
        {
            if (CurrentLobby != null)
            {
                try
                {
                    CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
                    
                    string joinCode = GetJoinCodeFromLobby();
                    if (!string.IsNullOrEmpty(joinCode))
                    {
                        Debug.Log($"Found join code after {i + 1} attempts: {joinCode}");
                        return joinCode;
                    }
                }
                catch (LobbyServiceException lex)
                {
                    // Rate limit detected - increase delay significantly
                    if (lex.Message?.Contains("Too Many Requests") == true || 
                        lex.Message?.Contains("Rate Limited") == true)
                    {
                        currentDelay = Mathf.Min(currentDelay * 2f, maxDelay);
                        Debug.LogWarning($"Rate limited while getting join code. Increasing delay to {currentDelay}s");
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to refresh lobby: {lex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to refresh lobby: {ex.Message}");
                }
            }

            Debug.Log($"Join code not ready, waiting {currentDelay:F1}s... ({i + 1}/{maxRetries})");
            await Task.Delay(TimeSpan.FromSeconds(currentDelay));
            
            // Exponential backoff với cap
            currentDelay = Mathf.Min(currentDelay * backoffMultiplier, maxDelay);
        }

        Debug.LogError("Join code not found after all retries");
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

    public async Task<List<Lobby>> GetAvailableLobbiesAsync(int maxResults = 20)
    {
        try
        {
            var queryFilters = new List<QueryFilter>
            {
                // available slots > 0
                new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
            };

            var queryResponse = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Count = maxResults,
                Filters = queryFilters
            });

            return queryResponse.Results;
        }
        catch (Exception ex)
        {
            Debug.LogError($"GetAvailableLobbiesAsync failed: {ex.Message}");
            return new List<Lobby>();
        }
    }

    public async Task<bool> CreateLobbyWithRoleAsync(PlayerRole role)
    {
        if (IsSearching || CurrentLobby != null)
        {
            Debug.LogWarning("Cannot create lobby while already in one.");
            return false;
        }

        SelectedRole = role;
        try
        {
            IsSearching = true;
            OnMatchmakingStarted?.Invoke();

            await CreateNewLobby();

            return CurrentLobby != null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"CreateLobbyWithRoleAsync failed: {ex.Message}");
            IsSearching = false;
            return false;
        }
    }

    public async Task<bool> JoinLobbyByIdAsyncPublic(string lobbyId)
    {
        if (IsSearching || CurrentLobby != null)
        {
            Debug.LogWarning("Cannot join lobby while already in one.");
            return false;
        }

        try
        {
            CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);

            Debug.Log($"Joined lobby (public call): {CurrentLobby.Name} ({CurrentLobby.Id})");

            var otherPlayer = CurrentLobby.Players.FirstOrDefault(p => p.Id != AuthenticationService.Instance.PlayerId);
            if (otherPlayer != null && otherPlayer.Data != null && otherPlayer.Data.ContainsKey("role"))
            {
                if (Enum.TryParse<PlayerRole>(otherPlayer.Data["role"].Value, out var otherRole))
                {
                    SelectedRole = otherRole == PlayerRole.Plant ? PlayerRole.Zombie : PlayerRole.Plant;
                    Debug.Log($"Assigned role after join: {SelectedRole} (other had {otherRole})");

                    await LobbyService.Instance.UpdatePlayerAsync(CurrentLobby.Id, AuthenticationService.Instance.PlayerId, new UpdatePlayerOptions
                    {
                        Data = CreatePlayerData().Data
                    });
                    Debug.Log($"Player role updated to {SelectedRole} on server.");
                    OnRoleSelected?.Invoke(SelectedRole); 
                }
                else
                {
                    SelectedRole = PlayerRole.None;
                }
            }
            else
            {
                SelectedRole = PlayerRole.None;
            }

            IsSearching = true;

            StartPolling();

            // If lobby already full, start match
            if (CurrentLobby.Players.Count == maxPlayers)
            {
                OnMatchFound?.Invoke(CurrentLobby.Id);
                StartNetworkGame();
                IsSearching = false; 
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"JoinLobbyByIdAsyncPublic failed: {ex.Message}");
            return false;
        }
    }

    // Helper: get display name (PlayerName) of host/owner for list UI
    public string GetLobbyOwnerName(Lobby lobby)
    {
        if (lobby == null) return "Unknown";
        // find host player
        var host = lobby.Players.FirstOrDefault(p => p.Id == lobby.HostId);
        if (host != null && host.Data != null && host.Data.ContainsKey("username"))
            return host.Data["username"].Value;
        return host?.Id ?? "Host";
    }

    // Helper: get owner's role
    public PlayerRole GetLobbyOwnerRole(Lobby lobby)
    {
        if (lobby == null) return PlayerRole.None;
        var host = lobby.Players.FirstOrDefault(p => p.Id == lobby.HostId);
        if (host != null && host.Data != null && host.Data.ContainsKey("role"))
        {
            if (Enum.TryParse<PlayerRole>(host.Data["role"].Value, out var r)) return r;
        }
        return PlayerRole.None;
    }
}