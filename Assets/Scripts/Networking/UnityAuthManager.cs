using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class UnityAuthManager : MonoBehaviour
{
    public static UnityAuthManager Instance { get; private set; }

    [Header("Scene Settings")]
    [SerializeField] private string lobbySceneName = "LobbyScene";
    [SerializeField] private string loginSceneName = "LoginScene";

    [Header("Session Settings")]
    [SerializeField] private bool kickPreviousSession = false;
    [SerializeField] private float sessionCheckInterval = 30f;

    private string loggedInUsername;
    private string currentSessionId;
    private const string SESSION_KEY = "active_session";
    private bool isCheckingSession = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Cleanup ngay trong Awake - trước khi bất kỳ network activity nào xảy ra
            CleanupPreviousNetworkSession();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void Start()
    {
        await InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            // Không cần gọi CleanupPreviousNetworkSession() ở đây nữa vì đã gọi trong Awake
            
            await UnityServices.InitializeAsync();
            Debug.Log("Unity Services initialized successfully");

            SetupAuthenticationEvents();

            if (AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log($"Already signed in as: {AuthenticationService.Instance.PlayerId}");
                
                // Verify session is still valid
                bool sessionValid = await VerifyCurrentSession();
                if (sessionValid)
                {
                    OnSignInSuccess();
                }
                else
                {
                    AuthenticationService.Instance.SignOut();
                    Debug.Log("Previous session invalid, signed out");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
        }
    }

    /// <summary>
    /// Dọn dẹp network session từ phiên trước để tránh lỗi "allocation ID not found"
    /// </summary>
    private void CleanupPreviousNetworkSession()
    {
        try
        {
            // Reset LobbyManager state nếu có
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.ResetNetworkState();
                Debug.Log("LobbyManager state reset");
            }

            // Shutdown và reset NetworkManager nếu đang active
            if (Unity.Netcode.NetworkManager.Singleton != null)
            {
                if (Unity.Netcode.NetworkManager.Singleton.IsListening)
                {
                    Unity.Netcode.NetworkManager.Singleton.Shutdown();
                    Debug.Log("NetworkManager shutdown completed");
                }

                // Reset UnityTransport để xóa Relay data cũ
                var transport = Unity.Netcode.NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
                if (transport != null)
                {
                    // Reset về default local connection (không dùng Relay)
                    transport.SetConnectionData("127.0.0.1", 7777);
                    Debug.Log("UnityTransport reset to default");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Error during network cleanup: {ex.Message}");
        }
    }

    private void SetupAuthenticationEvents()
    {
        AuthenticationService.Instance.SignedIn += () =>
        {
            Debug.Log($"Signed in successfully! Player ID: {AuthenticationService.Instance.PlayerId}");
        };

        AuthenticationService.Instance.SignInFailed += (err) =>
        {
            Debug.LogWarning($"Sign in failed: {err.Message}");
        };

        AuthenticationService.Instance.SignedOut += () =>
        {
            Debug.Log("Player signed out");
            StopSessionCheck();
            currentSessionId = null;
        };

        AuthenticationService.Instance.Expired += () =>
        {
            Debug.Log("Session expired. Please sign in again.");
            HandleSessionExpired();
        };
    }

    public async Task SignInAnonymouslyAsync()
    {
        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Anonymous sign in successful!");
            await CreateNewSession();
            OnSignInSuccess();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogException(ex);
            throw;
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
            throw;
        }
    }

    public async Task SignInWithUsernamePasswordAsync(string username, string password)
    {
        try
        {
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
            loggedInUsername = username;
            Debug.Log($"Sign in successful! Player ID: {AuthenticationService.Instance.PlayerId}");

            // Check for existing session BEFORE proceeding
            bool canProceed = await CheckAndHandleExistingSession();
            
            if (!canProceed)
            {
                // Sign out immediately since we can't proceed
                AuthenticationService.Instance.SignOut();
                throw new Exception("This account is already logged in on another device.");
            }

            OnSignInSuccess();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogWarning($"Sign in failed: {ex.Message}");
            string friendlyMessage = GetFriendlyErrorMessage(ex.Message);
            throw new Exception(friendlyMessage);
        }
        catch (RequestFailedException ex)
        {
            Debug.LogWarning($"Sign in request failed: {ex.Message}");
            string friendlyMessage = GetFriendlyErrorMessage(ex.Message);
            throw new Exception(friendlyMessage);
        }
        catch (Exception ex) when (ex.Message == "This account is already logged in on another device.")
        {
            // Re-throw this specific exception as-is
            throw;
        }
    }

    public async Task SignUpWithUsernamePasswordAsync(string username, string password)
    {
        try
        {
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
            loggedInUsername = username;
            Debug.Log($"Sign up successful! Player ID: {AuthenticationService.Instance.PlayerId}");
            
            await CreateNewSession();
            OnSignInSuccess();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogWarning($"Sign up failed: {ex.Message}");
            string friendlyMessage = GetFriendlyErrorMessage(ex.Message);
            throw new Exception(friendlyMessage);
        }
        catch (RequestFailedException ex)
        {
            Debug.LogWarning($"Sign up request failed: {ex.Message}");
            string friendlyMessage = GetFriendlyErrorMessage(ex.Message);
            throw new Exception(friendlyMessage);
        }
    }

    #region Session Management

    private async Task<bool> CheckAndHandleExistingSession()
    {
        try
        {
            Debug.Log("Checking for existing session...");
            
            var savedData = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { SESSION_KEY }
            );

            if (savedData.TryGetValue(SESSION_KEY, out var item))
            {
                string existingSession = item.Value.GetAs<string>();
                Debug.Log($"Found existing session: '{existingSession}'");
                
                if (!string.IsNullOrEmpty(existingSession))
                {
                    if (kickPreviousSession)
                    {
                        Debug.Log("kickPreviousSession=true, creating new session and kicking old one...");
                        await CreateNewSession();
                        return true;
                    }
                    else
                    {
                        Debug.Log("kickPreviousSession=false, rejecting new login attempt");
                        return false; // Reject login
                    }
                }
            }

            // No existing session, create new one
            Debug.Log("No existing session found, creating new session...");
            await CreateNewSession();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Session check failed: {ex.Message}");
            // On error, allow login but create new session
            await CreateNewSession();
            return true;
        }
    }

    private async Task<bool> VerifyCurrentSession()
    {
        if (string.IsNullOrEmpty(currentSessionId))
            return false;

        try
        {
            var savedData = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { SESSION_KEY }
            );

            if (savedData.TryGetValue(SESSION_KEY, out var item))
            {
                string serverSession = item.Value.GetAs<string>();
                return serverSession == currentSessionId;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task CreateNewSession()
    {
        currentSessionId = Guid.NewGuid().ToString();
        
        var data = new Dictionary<string, object>
        {
            { SESSION_KEY, currentSessionId }
        };

        await CloudSaveService.Instance.Data.Player.SaveAsync(data);
        Debug.Log($"New session created: {currentSessionId}");
        
        StartSessionCheck();
    }

    private async Task ClearSession()
    {
        try
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("Not signed in, skipping session clear");
                return;
            }

            var data = new Dictionary<string, object>
            {
                { SESSION_KEY, "" }
            };
            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            currentSessionId = null;
            Debug.Log("Session cleared");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to clear session: {ex.Message}");
        }
    }

    private void StartSessionCheck()
    {
        if (!isCheckingSession)
        {
            isCheckingSession = true;
            InvokeRepeating(nameof(CheckSessionValidity), sessionCheckInterval, sessionCheckInterval);
            Debug.Log($"Session check started (interval: {sessionCheckInterval}s)");
        }
    }

    private void StopSessionCheck()
    {
        if (isCheckingSession)
        {
            isCheckingSession = false;
            CancelInvoke(nameof(CheckSessionValidity));
            Debug.Log("Session check stopped");
        }
    }

    private async void CheckSessionValidity()
    {
        if (!AuthenticationService.Instance.IsSignedIn || string.IsNullOrEmpty(currentSessionId))
            return;

        try
        {
            var savedData = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { SESSION_KEY }
            );

            if (savedData.TryGetValue(SESSION_KEY, out var item))
            {
                string serverSession = item.Value.GetAs<string>();
                
                if (serverSession != currentSessionId)
                {
                    Debug.Log($"Session mismatch! Local: {currentSessionId}, Server: {serverSession}");
                    HandleKickedOut();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Session check error: {ex.Message}");
        }
    }

    private void HandleKickedOut()
    {
        StopSessionCheck();
        currentSessionId = null;
        AuthenticationService.Instance.SignOut();
        
        if (DialogManager.Instance != null)
        {
            DialogManager.Instance.ShowDialog(
                "You have been logged out because your account was accessed from another device.",
                "OK",
                () => SceneManager.LoadScene(loginSceneName)
            );
        }
        else
        {
            SceneManager.LoadScene(loginSceneName);
        }
    }

    private void HandleSessionExpired()
    {
        StopSessionCheck();
        currentSessionId = null;
        
        if (DialogManager.Instance != null)
        {
            DialogManager.Instance.ShowDialog(
                "Your session has expired. Please log in again.",
                "OK",
                () => SceneManager.LoadScene(loginSceneName)
            );
        }
        else
        {
            SceneManager.LoadScene(loginSceneName);
        }
    }

    #endregion

    private string GetFriendlyErrorMessage(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return "An error occurred. Please try again.";

        string lowerMessage = errorMessage.ToLower();

        if (lowerMessage.Contains("invalid username or password") ||
            lowerMessage.Contains("wrong_username_password") ||
            lowerMessage.Contains("invalid") && lowerMessage.Contains("password") ||
            lowerMessage.Contains("credentials"))
        {
            return "Wrong username or password.";
        }

        if (lowerMessage.Contains("not found") || lowerMessage.Contains("does not exist"))
        {
            return "Account not found.";
        }

        if (lowerMessage.Contains("already exists") || lowerMessage.Contains("duplicate") ||
            lowerMessage.Contains("entity_exists"))
        {
            return "Username already taken.";
        }

        if (lowerMessage.Contains("rate") || lowerMessage.Contains("too many"))
        {
            return "Too many attempts. Please wait a moment.";
        }

        if (lowerMessage.Contains("network") || lowerMessage.Contains("timeout") ||
            lowerMessage.Contains("connection") || lowerMessage.Contains("unable to connect"))
        {
            return "Network error. Please check your connection.";
        }

        if (lowerMessage.Contains("password") && (lowerMessage.Contains("weak") || lowerMessage.Contains("requirements")))
        {
            return "Password must be at least 8 characters with uppercase, lowercase and numbers.";
        }

        return "An error occurred. Please try again.";
    }

    public async Task AddUsernamePasswordAsync(string username, string password)
    {
        try
        {
            await AuthenticationService.Instance.AddUsernamePasswordAsync(username, password);
            Debug.Log("Username and password added successfully!");
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError($"Failed to add username/password: {ex.Message}");
            throw;
        }
    }

    public async Task UpdatePasswordAsync(string currentPassword, string newPassword)
    {
        try
        {
            await AuthenticationService.Instance.UpdatePasswordAsync(currentPassword, newPassword);
            Debug.Log("Password updated successfully!");
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError($"Failed to update password: {ex.Message}");
            throw;
        }
    }

    public async void SignOut()
    {
        await ClearSession();
        StopSessionCheck();
        AuthenticationService.Instance.SignOut();
        Debug.Log("Signed out successfully");
    }

    public async Task DeleteAccountAsync()
    {
        try
        {
            await ClearSession();
            await AuthenticationService.Instance.DeleteAccountAsync();
            Debug.Log("Account deleted successfully");
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError($"Failed to delete account: {ex.Message}");
            throw;
        }
    }

    public string GetPlayerId() => AuthenticationService.Instance.PlayerId;
    public string GetAccessToken() => AuthenticationService.Instance.AccessToken;
    
    public string GetPlayerName()
    {
        if (!string.IsNullOrEmpty(loggedInUsername))
            return loggedInUsername;
        return AuthenticationService.Instance.PlayerName;
    }

    public bool IsSignedIn() => AuthenticationService.Instance.IsSignedIn;

    private async void OnSignInSuccess()
    {
        if (LobbyManager.Instance == null)
        {
            GameObject lobbyObj = new GameObject("LobbyManager");
            lobbyObj.AddComponent<LobbyManager>();
        }

        if (PlayerDataManager.Instance == null)
        {
            GameObject dataObj = new GameObject("PlayerDataManager");
            dataObj.AddComponent<PlayerDataManager>();
        }
        
        await PlayerDataManager.Instance.LoadPlayerDataAsync();
        LoadLobbyScene();
    }

    private void LoadLobbyScene()
    {
        SceneManager.LoadScene(lobbySceneName);
    }

    private async void OnApplicationQuit()
    {
        if (AuthenticationService.Instance.IsSignedIn && !string.IsNullOrEmpty(currentSessionId))
        {
            await ClearSession();
        }
    }

    private void OnDestroy()
    {
        StopSessionCheck();
    }
}