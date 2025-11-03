using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using System.Collections;

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
        
        // Đợi NetworkGameManager ready
        StartCoroutine(WaitForNetworkManagerAndDisplayRole());

        UpdateConnectionStatus();
        InvokeRepeating(nameof(UpdateConnectionStatus), 0.5f, 0.5f);
    }

    private IEnumerator WaitForNetworkManagerAndDisplayRole()
    {
        // Đợi NetworkGameManager spawn
        while (NetworkGameManager.Instance == null)
        {
            yield return null;
        }

        // Hiển thị role
        if (LobbyManager.Instance != null)
        {
            PlayerRole role = LobbyManager.Instance.SelectedRole;
            roleText.text = $"Role: {role}";
            roleText.color = role == PlayerRole.Plant ? Color.green : Color.red;
            Debug.Log($"GameSceneUI: Displaying role {role}");
        }
        else
        {
            Debug.LogError("GameSceneUI: LobbyManager.Instance is null!");
            roleText.text = "Role: Unknown";
            roleText.color = Color.yellow;
        }
    }

    private void OnSpawnButtonClicked()
    {
        Debug.Log("Spawn button clicked!");

        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.SpawnUnit();
            statusText.text = "Spawning unit...";
        }
        else
        {
            statusText.text = "Error: NetworkGameManager not found!";
            statusText.color = Color.red;
            Debug.LogError("NetworkGameManager.Instance is null when spawn button clicked!");
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