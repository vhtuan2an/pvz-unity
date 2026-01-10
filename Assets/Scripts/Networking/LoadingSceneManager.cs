using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

public class LoadingSceneManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private Image loadingSpinner;

    private bool isConnecting = false;
    private float spinSpeed = 200f;
    private float connectionTimeout = 30f;
    private float timeoutTimer = 0f;

    private void Start()
    {
        if (LobbyManager.Instance == null)
        {
            Debug.LogError("LobbyManager not found!");
            SceneManager.LoadScene("LobbyScene");
            return;
        }

        UpdateRoleText();
        StartConnection();
    }

    private void Update()
    {
        if (loadingSpinner != null)
        {
            loadingSpinner.transform.Rotate(0, 0, spinSpeed * Time.deltaTime);
        }

        if (isConnecting)
        {
            timeoutTimer += Time.deltaTime;
            if (timeoutTimer >= connectionTimeout)
            {
                HandleConnectionTimeout();
            }
        }
    }

    private void UpdateRoleText()
    {
        if (roleText != null && LobbyManager.Instance != null)
        {
            string role = LobbyManager.Instance.SelectedRole.ToString();
            roleText.text = $"Playing as: {role}";
        }
    }

    private async void StartConnection()
    {
        isConnecting = true;
        UpdateStatus("Preparing connection...");

        await Task.Delay(500);

        try
        {
            bool isHost = LobbyManager.Instance.IsLobbyHostPublic();

            if (isHost)
            {
                UpdateStatus("Creating game server...");
                await LobbyManager.Instance.StartHostConnection();
            }
            else
            {
                UpdateStatus("Connecting to game server...");
                await LobbyManager.Instance.StartClientConnection();
            }

            UpdateStatus("Waiting for all players...");
            WaitForAllPlayersConnected();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Connection failed: {ex.Message}");
            UpdateStatus($"Connection failed: {ex.Message}");
            await Task.Delay(2000);
            ReturnToLobby();
        }
    }

    private void WaitForAllPlayersConnected()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager not found!");
            ReturnToLobby();
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        
        if (NetworkManager.Singleton.IsHost)
        {
            CheckAllPlayersReady();
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
        CheckAllPlayersReady();
    }

    private void CheckAllPlayersReady()
    {
        if (NetworkManager.Singleton == null) return;

        int connectedClients = NetworkManager.Singleton.ConnectedClientsIds.Count;
        UpdateStatus($"Players connected: {connectedClients}/2");

        if (connectedClients >= 2)
        {
            isConnecting = false;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            
            UpdateStatus("All players ready! Starting game...");
            
            if (NetworkManager.Singleton.IsHost)
            {
                Invoke(nameof(LoadGameScene), 1f);
            }
        }
    }

    private void LoadGameScene()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        }
    }

    private void HandleConnectionTimeout()
    {
        isConnecting = false;
        Debug.LogError("Connection timeout!");
        UpdateStatus("Connection timeout. Returning to lobby...");
        
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        Invoke(nameof(ReturnToLobby), 2f);
    }

    private void CleanupConnection()
    {
        isConnecting = false;
        
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            
            if (NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
    }

    private void ReturnToLobby()
    {
        CleanupConnection();
        
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.ResetNetworkState();
            _ = LobbyManager.Instance.CancelMatchmaking();
        }
        
        // Reset transport để xóa Relay data
        ResetTransportData();
        
        SceneManager.LoadScene("LobbyScene");
    }

    private void ResetTransportData()
    {
        try
        {
            if (NetworkManager.Singleton != null)
            {
                var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
                if (transport != null)
                {
                    transport.SetConnectionData("127.0.0.1", 7777);
                    Debug.Log("Transport data reset");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Error resetting transport: {ex.Message}");
        }
    }

    private void UpdateStatus(string message)
    {
        Debug.Log($"[Loading] {message}");
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
}
