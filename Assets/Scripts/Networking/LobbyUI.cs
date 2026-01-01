using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;

public class LobbyUI : MonoBehaviour
{
    [Header("Player Info")]
    [SerializeField] private TMP_Text usernameText;

    [Header("Matchmaking")]
    [SerializeField] private Button refreshLobbyListButton; 
    [SerializeField] private Button cancelMatchmakingButton;
    // [SerializeField] private GameObject searchingPanel; 
    [SerializeField] private TMP_Text searchingText;

    [Header("List UI")]
    [SerializeField] private RectTransform lobbyListContent;
    [SerializeField] private GameObject plantLobbyItemPrefab;
    [SerializeField] private GameObject zombieLobbyItemPrefab;

    [Header("Create")]
    [SerializeField] private Button createPlantBtn;
    [SerializeField] private Button createZombieBtn;

    private List<GameObject> spawnedItems = new List<GameObject>();

    private void Start()
    {
        // Subscribe to events
        LobbyManager.Instance.OnMatchmakingStarted += OnMatchmakingStarted;
        LobbyManager.Instance.OnMatchFound += OnMatchFound;
        LobbyManager.Instance.OnMatchmakingFailed += OnMatchmakingFailed;
        LobbyManager.Instance.OnMatchmakingCancelled += OnMatchmakingCancelled;

        // Setup buttons
        refreshLobbyListButton.onClick.AddListener(() => { _ = RefreshLobbyList(); });
        cancelMatchmakingButton.onClick.AddListener(() => { _ = LobbyManager.Instance.CancelMatchmaking(); });

        createPlantBtn.onClick.AddListener(() => { _ = CreateLobby(PlayerRole.Plant); });

        createZombieBtn.onClick.AddListener(() => { _ = CreateLobby(PlayerRole.Zombie); });

        DisplayPlayerInfo();

        // searchingPanel.SetActive(false);
        UpdateButtons(isSearching: false);

        _ = RefreshLobbyList();
        ForceContentLayout(); // Fix layout issues
    }

    private void ForceContentLayout()
    {
        if (lobbyListContent == null) return;

        // 1. Pivot & Anchors: Top-Center
        lobbyListContent.pivot = new Vector2(0.5f, 1f);
        lobbyListContent.anchorMin = new Vector2(0f, 1f);
        lobbyListContent.anchorMax = new Vector2(1f, 1f);
        lobbyListContent.anchoredPosition = Vector2.zero;

        // 2. Vertical Layout Group
        // var vlg = lobbyListContent.GetComponent<VerticalLayoutGroup>();
        // if (vlg == null) vlg = lobbyListContent.gameObject.AddComponent<VerticalLayoutGroup>();
        
        // vlg.childAlignment = TextAnchor.UpperCenter; // Or UpperLeft depending on design
        // vlg.childControlWidth = true; // Cho phép LayoutGroup điều khiển chiều rộng
        // vlg.childControlHeight = true; // Cho phép LayoutGroup điều khiển chiều cao (cần LayoutElement)
        // vlg.childForceExpandWidth = true; // Item giãn ra full width
        // vlg.childForceExpandHeight = false;
        // vlg.spacing = 10f; // Gap between items

        // 3. Content Size Fitter (Crucial for scrolling)
        var csf = lobbyListContent.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = lobbyListContent.gameObject.AddComponent<ContentSizeFitter>();
        
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnMatchmakingStarted -= OnMatchmakingStarted;
            LobbyManager.Instance.OnMatchFound -= OnMatchFound;
            LobbyManager.Instance.OnMatchmakingFailed -= OnMatchmakingFailed;
            LobbyManager.Instance.OnMatchmakingCancelled -= OnMatchmakingCancelled;
        }

        CancelInvoke(nameof(PeriodicRefresh));
    }

    private void DisplayPlayerInfo()
    {
        if (AuthenticationService.Instance.IsSignedIn)
        {
            string playerName = UnityAuthManager.Instance.GetPlayerName();
            if (string.IsNullOrEmpty(playerName))
            {
                string playerId = UnityAuthManager.Instance.GetPlayerId();
                playerName = $"Player #{playerId.Substring(0, 4)}";
            }
            usernameText.text = $"Welcome, {playerName}!";
        }
    }



    private async void OnCancelMatchmakingClicked()
    {
        await LobbyManager.Instance.CancelMatchmaking();
    }

    private void OnMatchmakingStarted()
    {
        // searchingPanel.SetActive(true);
        searchingText.text = $"Finding Match...";
        UpdateButtons(isSearching: true);
    }

    private void OnMatchFound(string matchId)
    {
        searchingText.text = "Match found! Connecting...";
    }

    private void OnMatchmakingFailed(string error)
    {
        // searchingPanel.SetActive(false);
        Debug.LogError($"Matchmaking failed: {error}");
        UpdateButtons(isSearching: false);
    }

    private void OnMatchmakingCancelled()
    {
        // searchingPanel.SetActive(false);
        UpdateButtons(isSearching: false);
    }

    private void UpdateButtons(bool isSearching)
    {
        refreshLobbyListButton.interactable = !isSearching;
        createPlantBtn.interactable = !isSearching;
        createZombieBtn.interactable = !isSearching;

        cancelMatchmakingButton.interactable = isSearching;

        foreach (var itemGO in spawnedItems)
        {
            itemGO.GetComponentInChildren<Button>().interactable = !isSearching;
        }
    }



    private async void PeriodicRefresh() => await RefreshLobbyList();

    public async Task RefreshLobbyList()
    {
        if (LobbyManager.Instance == null) return;

        if (LobbyManager.Instance.IsSearching) return;

        var lobbies = await LobbyManager.Instance.GetAvailableLobbiesAsync(30);

        // Clear existing UI
        foreach (var go in spawnedItems) Destroy(go);
        spawnedItems.Clear();

        foreach (var lobby in lobbies)
        {
            PlayerRole ownerRole = LobbyManager.Instance.GetLobbyOwnerRole(lobby);
            GameObject prefab = ownerRole == PlayerRole.Plant ? plantLobbyItemPrefab : zombieLobbyItemPrefab;
            
            var go = Instantiate(prefab, lobbyListContent);
            var item = go.GetComponent<LobbyListItem>();
            string name = lobby.Name;
            string owner = LobbyManager.Instance.GetLobbyOwnerName(lobby);
            item.Setup(lobby.Id, name, owner, ownerRole.ToString(), OnJoinLobbyClicked);
            
            var layoutElement = go.GetComponent<LayoutElement>();
            if (layoutElement == null) layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 30f; 
            layoutElement.preferredWidth = -1; 
            layoutElement.flexibleWidth = 1; 

            go.transform.SetAsFirstSibling();
            spawnedItems.Add(go);
        }
    }

    private async void OnJoinLobbyClicked(string lobbyId)
    {
        bool ok = await LobbyManager.Instance.JoinLobbyByIdAsyncPublic(lobbyId);

        if (!ok)
        {
            Debug.LogWarning("Failed to join lobby. It might be full or closed.");
            await RefreshLobbyList();
        }
    }

    private async Task CreateLobby(PlayerRole role)
    {
        bool ok = await LobbyManager.Instance.CreateLobbyWithRoleAsync(role);

        if (!ok)
        {
            Debug.LogWarning("Failed to create lobby.");
        }
    }
}