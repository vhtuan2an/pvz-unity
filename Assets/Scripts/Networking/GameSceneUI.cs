using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using System.Collections;

public class GameSceneUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button spawnZombieButton; // Thêm button này
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text connectionStatusText;

    private void Start()
    {
        // Đợi NetworkGameManager ready
        StartCoroutine(WaitForNetworkManagerAndSetupUI());

        UpdateConnectionStatus();
        InvokeRepeating(nameof(UpdateConnectionStatus), 0.5f, 0.5f);
    }

    private IEnumerator WaitForNetworkManagerAndSetupUI()
    {
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

            // Show/Hide buttons dựa trên role
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