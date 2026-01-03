using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using UnityEngine.SceneManagement;

public class UnityAuthManager : MonoBehaviour
{
    public static UnityAuthManager Instance { get; private set; }

    [Header("Scene Settings")]
    [SerializeField] private string lobbySceneName = "LobbyScene";

    private string loggedInUsername;

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

    private async void Start()
    {
        await InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            await UnityServices.InitializeAsync();
            Debug.Log("Unity Services initialized successfully");

            SetupAuthenticationEvents();

            if (AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log($"Already signed in as: {AuthenticationService.Instance.PlayerId}");
                OnSignInSuccess();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
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
        };

        AuthenticationService.Instance.Expired += () =>
        {
            Debug.Log("Session expired. Please sign in again.");
        };
    }

    public async Task SignInAnonymouslyAsync()
    {
        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Anonymous sign in successful!");
            OnSignInSuccess();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogException(ex);
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
        }
    }

    // Sign in with Username and Password
    public async Task SignInWithUsernamePasswordAsync(string username, string password)
    {
        try
        {
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
            loggedInUsername = username; // Store the username used for login
            Debug.Log($"Sign in successful! Player ID: {AuthenticationService.Instance.PlayerId}");
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
    }

    // Sign up with Username and Password
    public async Task SignUpWithUsernamePasswordAsync(string username, string password)
    {
        try
        {
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
            loggedInUsername = username; // Store the username used for signup
            Debug.Log($"Sign up successful! Player ID: {AuthenticationService.Instance.PlayerId}");
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

    private string GetFriendlyErrorMessage(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return "An error occurred. Please try again.";

        string lowerMessage = errorMessage.ToLower();

        // Wrong username or password
        if (lowerMessage.Contains("invalid username or password") ||
            lowerMessage.Contains("wrong_username_password") ||
            lowerMessage.Contains("invalid") && lowerMessage.Contains("password") ||
            lowerMessage.Contains("credentials"))
        {
            return "Wrong username or password.";
        }

        // Account not found
        if (lowerMessage.Contains("not found") || lowerMessage.Contains("does not exist"))
        {
            return "Account not found.";
        }

        // Account already exists
        if (lowerMessage.Contains("already exists") || lowerMessage.Contains("duplicate") ||
            lowerMessage.Contains("entity_exists"))
        {
            return "Username already taken.";
        }

        // Rate limit
        if (lowerMessage.Contains("rate") || lowerMessage.Contains("too many"))
        {
            return "Too many attempts. Please wait a moment.";
        }

        // Network error
        if (lowerMessage.Contains("network") || lowerMessage.Contains("timeout") ||
            lowerMessage.Contains("connection") || lowerMessage.Contains("unable to connect"))
        {
            return "Network error. Please check your connection.";
        }

        // Weak password
        if (lowerMessage.Contains("password") && (lowerMessage.Contains("weak") || lowerMessage.Contains("requirements")))
        {
            return "Password must be at least 8 characters with uppercase, lowercase and numbers.";
        }

        // Default
        return "An error occurred. Please try again.";
    }

    // Add username and password to anonymous account
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

    // Update password
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

    // Sign out
    public void SignOut()
    {
        AuthenticationService.Instance.SignOut();
        Debug.Log("Signed out successfully");
    }

    // Delete account
    public async Task DeleteAccountAsync()
    {
        try
        {
            await AuthenticationService.Instance.DeleteAccountAsync();
            Debug.Log("Account deleted successfully");
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError($"Failed to delete account: {ex.Message}");
            throw;
        }
    }

    // Get player info
    public string GetPlayerId()
    {
        return AuthenticationService.Instance.PlayerId;
    }

    public string GetAccessToken()
    {
        return AuthenticationService.Instance.AccessToken;
    }

    public string GetPlayerName()
    {
        // Return the stored login username, or fallback to PlayerName from Auth service
        if (!string.IsNullOrEmpty(loggedInUsername))
        {
            return loggedInUsername;
        }
        return AuthenticationService.Instance.PlayerName;
    }

    public bool IsSignedIn()
    {
        return AuthenticationService.Instance.IsSignedIn;
    }

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
        
        // Load player data from Cloud Save
        await PlayerDataManager.Instance.LoadPlayerDataAsync();

        LoadLobbyScene();
    }

    private void LoadLobbyScene()
    {
        SceneManager.LoadScene(lobbySceneName);
    }
}