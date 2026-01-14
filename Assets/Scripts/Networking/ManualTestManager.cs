using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ManualTestManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject testPanel;
    [SerializeField] private Button startAsPlantButton;
    [SerializeField] private Button startAsZombieButton;
    [SerializeField] private TMP_Text statusText;
    
    [Header("Network Settings")]
    [SerializeField] private ushort hostPort = 7779;
    
    private bool hasStarted = false;

    private void Start()
    {
        bool isTestMode = TestModeManager.Instance != null && TestModeManager.Instance.IsTestMode;
        
        // Debug: Log all relevant states
        Debug.Log($"[ManualTestManager] Start() called - isTestMode: {isTestMode}");
        Debug.Log($"[ManualTestManager] NetworkManager.Singleton: {NetworkManager.Singleton != null}");
        if (NetworkManager.Singleton != null)
        {
            Debug.Log($"[ManualTestManager] IsServer: {NetworkManager.Singleton.IsServer}, IsClient: {NetworkManager.Singleton.IsClient}, IsConnectedClient: {NetworkManager.Singleton.IsConnectedClient}");
        }
        Debug.Log($"[ManualTestManager] LobbyManager.Instance: {LobbyManager.Instance != null}");
        if (LobbyManager.Instance != null)
        {
            Debug.Log($"[ManualTestManager] SelectedRole: {LobbyManager.Instance.SelectedRole}, CurrentLobby: {LobbyManager.Instance.CurrentLobby != null}");
        }
        
        // If network is already connected, players joined via lobby (production flow)
        // Do not show test panel in this case, even if TestModeManager says test mode is enabled
        bool isAlreadyConnected = NetworkManager.Singleton != null && 
                                  (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsConnectedClient);
        
        // Also check if player joined via lobby (has selected role or has current lobby)
        bool joinedViaLobby = LobbyManager.Instance != null && 
                              (LobbyManager.Instance.SelectedRole != PlayerRole.None || 
                               LobbyManager.Instance.CurrentLobby != null);
        
        if (isAlreadyConnected || joinedViaLobby)
        {
            Debug.Log($"[ManualTestManager] Hiding test panel - isAlreadyConnected: {isAlreadyConnected}, joinedViaLobby: {joinedViaLobby}");
            Debug.Log($"[ManualTestManager] testPanel reference is: {(testPanel != null ? "assigned" : "NULL")}");
            
            // If testPanel reference is null, try to find it by name as fallback
            if (testPanel == null)
            {
                testPanel = GameObject.Find("TestUI");
                if (testPanel != null)
                {
                    Debug.Log("[ManualTestManager] Found testPanel by name (fallback)");
                }
            }
            
            if (testPanel != null)
            {
                testPanel.SetActive(false);
                Debug.Log("[ManualTestManager] testPanel.SetActive(false) called successfully");
            }
            else
            {
                Debug.LogWarning("[ManualTestManager] testPanel is still null, cannot hide it!");
            }
            
            enabled = false;
            return;
        }
        
        if (testPanel != null)
        {
            testPanel.SetActive(isTestMode);
        }
        
        if (!isTestMode)
        {
            enabled = false;
            return;
        }
        
        // Setup buttons
        if (startAsPlantButton != null)
        {
            startAsPlantButton.onClick.AddListener(OnStartAsPlantClicked);
        }
        
        if (startAsZombieButton != null)
        {
            startAsZombieButton.onClick.AddListener(OnStartAsZombieClicked);
        }
        
        UpdateStatusText("TEST MODE: Choose your role to start testing");
    }

    private void OnStartAsPlantClicked()
    {
        if (hasStarted)
        {
            Debug.LogWarning("Already started!");
            return;
        }
        
        hasStarted = true;
        UpdateStatusText("Starting as Plant (Host)...");
        StartAsHost(PlayerRole.Plant);
    }

    private void OnStartAsZombieClicked()
    {
        if (hasStarted)
        {
            Debug.LogWarning("Already started!");
            return;
        }
        
        hasStarted = true;
        UpdateStatusText("Starting as Zombie (Client)...");
        StartAsClient(PlayerRole.Zombie);
    }

    private void StartAsHost(PlayerRole role)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager not found!");
            UpdateStatusText("ERROR: NetworkManager not found!");
            return;
        }
        
        // Set role in LobbyManager
        if (LobbyManager.Instance == null)
        {
            GameObject lobbyManagerObj = new GameObject("LobbyManager");
            LobbyManager lobbyManager = lobbyManagerObj.AddComponent<LobbyManager>();
            DontDestroyOnLoad(lobbyManagerObj);
        }
        
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.SelectRole(role);
            Debug.Log($"[TEST MODE] Role set to: {role}");
        }
        
        // Setup transport
        var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Port = hostPort;
            Debug.Log($"[TEST MODE] Host port set to: {hostPort}");
        }
        
        // Start as Host
        NetworkManager.Singleton.StartHost();
        Debug.Log("[TEST MODE] Started as Host (Plant Player)");
        UpdateStatusText($"Running as Host - {role}");
        
        // Hide test panel
        if (testPanel != null)
        {
            testPanel.SetActive(false);
        }
    }

    private void StartAsClient(PlayerRole role)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager not found!");
            UpdateStatusText("ERROR: NetworkManager not found!");
            return;
        }
        
        // Set role in LobbyManager
        if (LobbyManager.Instance == null)
        {
            GameObject lobbyManagerObj = new GameObject("LobbyManager");
            LobbyManager lobbyManager = lobbyManagerObj.AddComponent<LobbyManager>();
            DontDestroyOnLoad(lobbyManagerObj);
        }
        
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.SelectRole(role);
            Debug.Log($"[TEST MODE] Role set to: {role}");
        }
        
        // Setup transport
        var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = "127.0.0.1";
            transport.ConnectionData.Port = hostPort;
            Debug.Log($"[TEST MODE] Connecting to 127.0.0.1:{hostPort}");
        }
        
        // Start as Client
        NetworkManager.Singleton.StartClient();
        Debug.Log("[TEST MODE] Started as Client (Zombie Player)");
        UpdateStatusText($"Connecting as Client - {role}");
        
        // Check connection status
        Invoke(nameof(CheckConnectionStatus), 2f);
    }

    private void CheckConnectionStatus()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            UpdateStatusText($"Connected as Client - {LobbyManager.Instance?.SelectedRole}");
            
            // Hide test panel
            if (testPanel != null)
            {
                testPanel.SetActive(false);
            }
        }
        else
        {
            UpdateStatusText("ERROR: Failed to connect to Host!");
            Debug.LogError("[TEST MODE] Failed to connect! Make sure Host is running first.");
        }
    }

    private void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"[TEST MODE] {message}");
    }

    private void OnDestroy()
    {
        if (startAsPlantButton != null)
        {
            startAsPlantButton.onClick.RemoveListener(OnStartAsPlantClicked);
        }
        
        if (startAsZombieButton != null)
        {
            startAsZombieButton.onClick.RemoveListener(OnStartAsZombieClicked);
        }
    }
}