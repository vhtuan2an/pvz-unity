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
using System.Threading;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("Lobby Settings")]
    [SerializeField] private int maxPlayers = 2;
    [SerializeField] private float lobbyHeartbeatInterval = 15f;
    
    // Giảm polling interval để match nhanh hơn
    private float minPollInterval = 2f; // Giảm từ 10s xuống 2s
    private float maxPollInterval = 30f;
    private int consecutiveErrors = 0;

    // Cancellation token để dừng polling an toàn
    private CancellationTokenSource pollCancellationTokenSource;

    public PlayerRole SelectedRole { get; private set; } = PlayerRole.None;
    public bool IsSearching { get; private set; }
    public Lobby CurrentLobby { get; private set; }
    
    // Flag to indicate lobby was cancelled by host (used to show dialog after scene transition)
    public bool WasCancelledByHost { get; private set; } = false;

    // Events
    public event Action<PlayerRole> OnRoleSelected;
    public event Action OnMatchmakingStarted;
    public event Action<string> OnMatchFound;
    public event Action<string> OnMatchmakingFailed;
    public event Action OnMatchmakingCancelled;
    public event Action OnLobbyCancelledByHost;

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
        // Cancel any existing polling
        StopPolling();
        
        pollCancellationTokenSource = new CancellationTokenSource();
        nextHeartbeat = Time.time + lobbyHeartbeatInterval;
        PollLobby(pollCancellationTokenSource.Token);
    }

    private void StopPolling()
    {
        isPolling = false;
        pollCancellationTokenSource?.Cancel();
        pollCancellationTokenSource?.Dispose();
        pollCancellationTokenSource = null;
    }

    private async void PollLobby(CancellationToken cancellationToken)
    {
        if (isPolling) return;
        isPolling = true;

        float baseInterval = minPollInterval; // Giảm xuống 2s
        float currentInterval = baseInterval;
        consecutiveErrors = 0;

        try
        {
            while (isPolling && IsSearching && !cancellationToken.IsCancellationRequested)
            {
                // Kiểm tra CurrentLobby trước khi tiếp tục
                if (CurrentLobby == null)
                {
                    Debug.LogWarning("CurrentLobby is null, stopping polling.");
                    break;
                }
                
                // Lưu lobby ID để tránh null reference
                string lobbyId = CurrentLobby?.Id;
                if (string.IsNullOrEmpty(lobbyId))
                {
                    Debug.LogWarning("Lobby ID is null, stopping polling.");
                    break;
                }

                // Add small random jitter (0-1s) - giảm jitter
                float jitter = UnityEngine.Random.Range(0f, 1f);
                
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(currentInterval + jitter), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    Debug.Log("Polling cancelled during delay.");
                    break;
                }

                // Kiểm tra lại sau delay
                if (cancellationToken.IsCancellationRequested || CurrentLobby == null)
                {
                    break;
                }

                try
                {
                    // Only host should send heartbeat
                    if (IsLobbyHost() && Time.time >= nextHeartbeat)
                    {
                        try
                        {
                            await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
                            nextHeartbeat = Time.time + lobbyHeartbeatInterval;
                            Debug.Log("Lobby heartbeat sent");
                        }
                        catch (LobbyServiceException hex)
                        {
                            // Nếu lobby không tồn tại, thoát
                            if (hex.Reason == LobbyExceptionReason.LobbyNotFound)
                            {
                                Debug.LogWarning("Lobby not found during heartbeat.");
                                HandleLobbyNotFound();
                                break;
                            }
                        }
                    }

                    // Kiểm tra cancellation trước khi poll
                    if (cancellationToken.IsCancellationRequested) break;

                    // Poll lobby state
                    var updatedLobby = await LobbyService.Instance.GetLobbyAsync(lobbyId);
                    
                    // Kiểm tra cancellation sau khi poll
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    CurrentLobby = updatedLobby;
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
                        StopPolling();
                        OnMatchFound?.Invoke(CurrentLobby.Id);
                        StartNetworkGame();
                        return; // Exit method completely
                    }
                }
                catch (LobbyServiceException lex)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    consecutiveErrors++;
                    string msg = lex.Message ?? lex.Reason.ToString();

                    if (lex.Reason == LobbyExceptionReason.LobbyNotFound)
                    {
                        // This is expected behavior when host cancels the lobby, use Warning instead of Error
                        Debug.LogWarning($"Polling lobby failed: {msg}");
                        HandleLobbyNotFound();
                        break;
                    }
                    
                    Debug.LogError($"Error polling lobby (attempt {consecutiveErrors}): {msg}");

                    // Detect rate limit
                    bool isRateLimit = lex.Message?.Contains("Too Many Requests") == true
                                       || lex.Message?.Contains("Rate Limited") == true;

                    if (isRateLimit)
                    {
                        currentInterval = Mathf.Min(currentInterval * 2f, 60f);
                        currentInterval += UnityEngine.Random.Range(0f, 5f);
                        Debug.LogWarning($"Rate limited -> backing off. New interval: {currentInterval}s");
                    }
                    else
                    {
                        currentInterval = Mathf.Min(currentInterval * 1.5f, maxPollInterval);
                    }

                    if (consecutiveErrors >= 6)
                    {
                        Debug.LogError("Too many consecutive errors polling lobby, cancelling matchmaking.");
                        await SafeCancelMatchmaking();
                        OnMatchmakingFailed?.Invoke("Connection issues - polling failed repeatedly");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    consecutiveErrors++;
                    Debug.LogError($"Unexpected error polling lobby: {ex.Message}");
                    currentInterval = Mathf.Min(currentInterval * 1.5f, maxPollInterval);

                    if (consecutiveErrors >= 6)
                    {
                        Debug.LogError("Too many consecutive errors polling lobby, cancelling matchmaking.");
                        await SafeCancelMatchmaking();
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

    private void HandleLobbyNotFound()
    {
        Debug.LogWarning("Lobby was closed or deleted by host.");
        IsSearching = false;
        CurrentLobby = null;
        WasCancelledByHost = true;
        StopPolling();
        OnLobbyCancelledByHost?.Invoke();
    }

    // Safe cancel without throwing
    private async Task SafeCancelMatchmaking()
    {
        try
        {
            await CancelMatchmaking();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error during safe cancel: {ex.Message}");
            // Reset state anyway
            IsSearching = false;
            CurrentLobby = null;
        }
    }

    public async Task CancelMatchmaking()
    {
        // Dừng polling trước
        StopPolling();
        
        if (!IsSearching && CurrentLobby == null)
            return;

        string lobbyId = CurrentLobby?.Id;
        bool wasHost = CurrentLobby != null && IsLobbyHost();

        // Reset state trước khi gọi API
        IsSearching = false;
        var tempLobby = CurrentLobby;
        CurrentLobby = null;

        if (string.IsNullOrEmpty(lobbyId))
        {
            OnMatchmakingCancelled?.Invoke();
            return;
        }

        try
        {
            if (wasHost)
            {
                await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                Debug.Log("Lobby deleted (host left)");
            }
            else
            {
                await LobbyService.Instance.RemovePlayerAsync(lobbyId, AuthenticationService.Instance.PlayerId);
                Debug.Log("Left lobby");
            }
        }
        catch (LobbyServiceException lex)
        {
            // Lobby có thể đã bị xóa, không cần báo lỗi nghiêm trọng
            if (lex.Reason == LobbyExceptionReason.LobbyNotFound)
            {
                Debug.LogWarning("Lobby already deleted.");
            }
            else
            {
                Debug.LogError($"Failed to cancel matchmaking: {lex.Message}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to cancel matchmaking: {ex.Message}");
        }
        finally
        {
            OnMatchmakingCancelled?.Invoke();
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
        
        // FIX: Increase Packet Queue Sizes to prevent 4294967296 error
        utpTransport.MaxSendQueueSize = 1024 * 1024; // 1MB
        utpTransport.MaxReceiveQueueSize = 1024 * 1024; // 1MB

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

        // Check if lobby was cancelled by host before trying to get join code
        if (WasCancelledByHost || CurrentLobby == null)
        {
            Debug.LogWarning("Lobby was cancelled by host, aborting client connection.");
            throw new LobbyCancelledException("Lobby was cancelled by host");
        }

        string joinCode = await GetJoinCodeWithRetry(maxRetries: 10, initialDelay: 2f);
        
        // Check again if lobby was cancelled during retry
        if (WasCancelledByHost || string.IsNullOrEmpty(joinCode))
        {
            Debug.LogWarning("No join code found - lobby may have been cancelled.");
            throw new LobbyCancelledException("Lobby was cancelled by host");
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

        // FIX: Increase Packet Queue Sizes to prevent 4294967296 error
        utpTransport.MaxSendQueueSize = 1024 * 1024; // 1MB
        utpTransport.MaxReceiveQueueSize = 1024 * 1024; // 1MB

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

    private async Task<string> GetJoinCodeWithRetry(int maxRetries = 15, float initialDelay = 1f)
    {
        float currentDelay = initialDelay;
        const float maxDelay = 5f; // Giảm từ 10s xuống 5s
        const float backoffMultiplier = 1.3f; // Giảm từ 1.5 xuống 1.3

        for (int i = 0; i < maxRetries; i++)
        {
            if (CurrentLobby == null)
            {
                Debug.LogWarning("CurrentLobby is null while waiting for join code");
                return null;
            }

            try
            {
                // Refresh lobby để lấy data mới nhất
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
                if (lex.Reason == LobbyExceptionReason.LobbyNotFound)
                {
                    Debug.LogError("Lobby was deleted while waiting for join code");
                    return null;
                }

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

            Debug.Log($"Join code not ready, waiting {currentDelay:F1}s... ({i + 1}/{maxRetries})");
            await Task.Delay(TimeSpan.FromSeconds(currentDelay));
            
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

    // Clear the cancelled by host flag (call after showing the dialog)
    public void ClearCancelledByHostFlag()
    {
        WasCancelledByHost = false;
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

    public void ClearSelectedRole()
    {
        SelectedRole = PlayerRole.None;
        Debug.Log("SelectedRole cleared to None.");
    }

    private void OnDestroy()
    {
        StopPolling();
        if (IsSearching)
        {
            _ = CancelMatchmaking();
        }
    }
}

/// <summary>
/// Custom exception for when lobby is cancelled by host
/// </summary>
public class LobbyCancelledException : System.Exception
{
    public LobbyCancelledException(string message) : base(message) { }
}