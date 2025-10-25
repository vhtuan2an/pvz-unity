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

            // Setup authentication event listeners
            SetupAuthenticationEvents();

            // Check if already signed in
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
            Debug.LogError($"Sign in failed: {err}");
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

    // Anonymous Sign In - Fastest way to authenticate
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
            Debug.Log($"Sign in successful! Player ID: {AuthenticationService.Instance.PlayerId}");
            OnSignInSuccess();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError($"Sign in failed: {ex.Message}");
            throw;
        }
        catch (RequestFailedException ex)
        {
            Debug.LogError($"Sign in request failed: {ex.Message}");
            throw;
        }
    }

    // Sign up with Username and Password
    public async Task SignUpWithUsernamePasswordAsync(string username, string password)
    {
        try
        {
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
            Debug.Log($"Sign up successful! Player ID: {AuthenticationService.Instance.PlayerId}");
            OnSignInSuccess();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError($"Sign up failed: {ex.Message}");
            throw;
        }
        catch (RequestFailedException ex)
        {
            Debug.LogError($"Sign up request failed: {ex.Message}");
            throw;
        }
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

    public bool IsSignedIn()
    {
        return AuthenticationService.Instance.IsSignedIn;
    }

    private void OnSignInSuccess()
    {
        // Ensure LobbyManager exists
        if (LobbyManager.Instance == null)
        {
            GameObject lobbyObj = new GameObject("LobbyManager");
            lobbyObj.AddComponent<LobbyManager>();
        }

        // Navigate to lobby scene after successful authentication
        LoadLobbyScene();
    }

    private void LoadLobbyScene()
    {
        SceneManager.LoadScene(lobbySceneName);
    }
}