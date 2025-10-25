using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class GameSceneUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button spawnButton;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text connectionStatusText;

    private void Start()
    {
        spawnButton.onClick.AddListener(OnSpawnButtonClicked);
        
        // Hiển thị role
        if (LobbyManager.Instance != null)
        {
            PlayerRole role = LobbyManager.Instance.SelectedRole;
            roleText.text = $"Role: {role}";
            roleText.color = role == PlayerRole.Plant ? Color.green : Color.red;
        }

        UpdateConnectionStatus();
        InvokeRepeating(nameof(UpdateConnectionStatus), 0.5f, 0.5f);
    }

    private void OnSpawnButtonClicked()
    {
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.SpawnUnit();
            statusText.text = "Spawning unit...";
        }
        else
        {
            statusText.text = "Error: NetworkGameManager not found!";
            statusText.color = Color.red;
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