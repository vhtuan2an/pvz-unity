using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using System.Collections;

public class GameSceneUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject gameUIPanel;
    [SerializeField] private Button spawnZombieButton;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text connectionStatusText;
    [SerializeField] private TMP_Text modeText; // ✅ Hiển thị Test Mode hoặc Production

    private void Start()
    {
        // Hide game UI until connected
        if (gameUIPanel != null)
        {
            gameUIPanel.SetActive(false);
        }
        
        StartCoroutine(WaitForNetworkManagerAndSetupUI());
        
        UpdateConnectionStatus();
        InvokeRepeating(nameof(UpdateConnectionStatus), 0.5f, 0.5f);
        
        // ✅ Hiển thị mode
        UpdateModeDisplay();
    }

    private void UpdateModeDisplay()
    {
        if (modeText != null)
        {
            bool isTestMode = TestModeManager.Instance != null && TestModeManager.Instance.IsTestMode;
            
            if (isTestMode)
            {
                modeText.text = "TEST MODE";
                modeText.color = Color.yellow;
            }
            else
            {
                modeText.text = "PRODUCTION";
                modeText.color = Color.green;
            }
        }
    }

    private IEnumerator WaitForNetworkManagerAndSetupUI()
    {
        // Wait for network connection
        while (NetworkManager.Singleton == null || 
               (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsConnectedClient))
        {
            yield return null;
        }
        
        // Wait for NetworkGameManager
        while (NetworkGameManager.Instance == null)
        {
            yield return null;
        }
        
        // Show game UI
        if (gameUIPanel != null)
        {
            gameUIPanel.SetActive(true);
        }

        // Display role
        if (LobbyManager.Instance != null)
        {
            PlayerRole role = LobbyManager.Instance.SelectedRole;
            roleText.text = $"Role: {role}";
            roleText.color = role == PlayerRole.Plant ? Color.green : Color.red;
            Debug.Log($"GameSceneUI: Displaying role {role}");

            // Show/Hide buttons based on role
            if (role == PlayerRole.Zombie && spawnZombieButton != null)
            {
                spawnZombieButton.gameObject.SetActive(true);
                spawnZombieButton.onClick.AddListener(OnSpawnZombieClicked);
            }
            else if (spawnZombieButton != null)
            {
                spawnZombieButton.gameObject.SetActive(false);
            }
        }
    }

    private void OnSpawnZombieClicked()
    {
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.SpawnZombie();
        }
    }

    private void UpdateConnectionStatus()
    {
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                connectionStatusText.text = "Status: Host";
                connectionStatusText.color = Color.cyan;
            }
            else if (NetworkManager.Singleton.IsClient && NetworkManager.Singleton.IsConnectedClient)
            {
                connectionStatusText.text = "Status: Connected Client";
                connectionStatusText.color = Color.green;
            }
            else
            {
                connectionStatusText.text = "Status: Not Connected";
                connectionStatusText.color = Color.red;
            }
        }
    }

    private void OnDestroy()
    {
        CancelInvoke(nameof(UpdateConnectionStatus));
    }
}