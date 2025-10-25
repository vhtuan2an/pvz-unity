using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Authentication;

public class LobbyUI : MonoBehaviour
{
    [Header("Player Info")]
    [SerializeField] private TMP_Text playerIdText;
    [SerializeField] private TMP_Text usernameText;

    [Header("Role Selection")]
    [SerializeField] private Button plantButton;
    [SerializeField] private Button zombieButton;
    [SerializeField] private TMP_Text selectedRoleText;

    [Header("Matchmaking")]
    [SerializeField] private Button startMatchmakingButton;
    [SerializeField] private Button cancelMatchmakingButton;
    [SerializeField] private GameObject searchingPanel;
    [SerializeField] private TMP_Text searchingText;

    [Header("Feedback")]
    [SerializeField] private TMP_Text feedbackText;

    private void Start()
    {
        // Subscribe to events
        LobbyManager.Instance.OnRoleSelected += OnRoleSelected;
        LobbyManager.Instance.OnMatchmakingStarted += OnMatchmakingStarted;
        LobbyManager.Instance.OnMatchFound += OnMatchFound;
        LobbyManager.Instance.OnMatchmakingFailed += OnMatchmakingFailed;
        LobbyManager.Instance.OnMatchmakingCancelled += OnMatchmakingCancelled;

        // Setup buttons
        plantButton.onClick.AddListener(() => LobbyManager.Instance.SelectRole(PlayerRole.Plant));
        zombieButton.onClick.AddListener(() => LobbyManager.Instance.SelectRole(PlayerRole.Zombie));
        startMatchmakingButton.onClick.AddListener(() => _ = LobbyManager.Instance.StartMatchmaking());
        cancelMatchmakingButton.onClick.AddListener(() => _ = LobbyManager.Instance.CancelMatchmaking());

        // Display player info
        DisplayPlayerInfo();

        // Initial UI state
        UpdateUI();
        searchingPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnRoleSelected -= OnRoleSelected;
            LobbyManager.Instance.OnMatchmakingStarted -= OnMatchmakingStarted;
            LobbyManager.Instance.OnMatchFound -= OnMatchFound;
            LobbyManager.Instance.OnMatchmakingFailed -= OnMatchmakingFailed;
            LobbyManager.Instance.OnMatchmakingCancelled -= OnMatchmakingCancelled;
        }
    }

    private void DisplayPlayerInfo()
    {
        if (AuthenticationService.Instance.IsSignedIn)
        {
            string playerId = UnityAuthManager.Instance.GetPlayerId();
            playerIdText.text = $"Player ID: {playerId.Substring(0, 8)}...";
            
            // You can store username during login and retrieve it here
            usernameText.text = $"Welcome, Player!";
        }
    }

    private void OnRoleButtonClicked(PlayerRole role)
    {
        LobbyManager.Instance.SelectRole(role);
    }

    private void OnRoleSelected(PlayerRole role)
    {
        selectedRoleText.text = $"Selected: {LobbyManager.Instance.GetRoleDisplayName(role)}";
        selectedRoleText.color = role == PlayerRole.Plant ? Color.green : Color.red;
        UpdateUI();
    }

    private async void OnStartMatchmakingClicked()
    {
        ClearFeedback();
        await LobbyManager.Instance.StartMatchmaking();
    }

    private async void OnCancelMatchmakingClicked()
    {
        await LobbyManager.Instance.CancelMatchmaking();
    }

    private void OnMatchmakingStarted()
    {
        searchingPanel.SetActive(true);
        searchingText.text = $"Searching for opponent as {LobbyManager.Instance.GetRoleDisplayName(LobbyManager.Instance.SelectedRole)}...";
        UpdateUI();
    }

    private void OnMatchFound(string matchId)
    {
        searchingPanel.SetActive(false);
        ShowFeedback($"Match found! Match ID: {matchId}", false);
        
        // Load game scene
        // Debug.Log("Loading game scene...");
        // UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
    }

    private void OnMatchmakingFailed(string error)
    {
        searchingPanel.SetActive(false);
        ShowFeedback($"Matchmaking failed: {error}", true);
        UpdateUI();
    }

    private void OnMatchmakingCancelled()
    {
        searchingPanel.SetActive(false);
        ShowFeedback("Matchmaking cancelled", false);
        UpdateUI();
    }

    private void UpdateUI()
    {
        bool hasRole = LobbyManager.Instance.SelectedRole != PlayerRole.None;
        bool isSearching = LobbyManager.Instance.IsSearching;

        startMatchmakingButton.interactable = hasRole && !isSearching;
        cancelMatchmakingButton.interactable = isSearching;
        
        plantButton.interactable = !isSearching;
        zombieButton.interactable = !isSearching;

        // Highlight selected button
        ColorBlock plantColors = plantButton.colors;
        ColorBlock zombieColors = zombieButton.colors;

        if (LobbyManager.Instance.SelectedRole == PlayerRole.Plant)
        {
            plantColors.normalColor = Color.green;
            zombieColors.normalColor = Color.white;
        }
        else if (LobbyManager.Instance.SelectedRole == PlayerRole.Zombie)
        {
            plantColors.normalColor = Color.white;
            zombieColors.normalColor = Color.red;
        }
        else
        {
            plantColors.normalColor = Color.white;
            zombieColors.normalColor = Color.white;
        }

        plantButton.colors = plantColors;
        zombieButton.colors = zombieColors;
    }

    private void ShowFeedback(string message, bool isError)
    {
        feedbackText.text = message;
        feedbackText.color = isError ? Color.red : Color.green;
        feedbackText.gameObject.SetActive(true);
    }

    private void ClearFeedback()
    {
        feedbackText.gameObject.SetActive(false);
    }
}