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
    [SerializeField] private Button refreshLobbyListButton; // Đổi tên từ startMatchmakingButton
    [SerializeField] private Button cancelMatchmakingButton;
    [SerializeField] private GameObject searchingPanel; // Dùng làm panel "Đang đợi"
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
        // plantButton.onClick.AddListener(...); // Đã xóa
        // zombieButton.onClick.AddListener(...); // Đã xóa

        // Sửa dòng 53
        refreshLobbyListButton.onClick.AddListener(() => { _ = RefreshLobbyList(); });
        // Sửa dòng 55
        cancelMatchmakingButton.onClick.AddListener(() => { _ = LobbyManager.Instance.CancelMatchmaking(); });

        // Sửa dòng 56
        createPlantBtn.onClick.AddListener(() => { _ = CreateLobby(PlayerRole.Plant); });

        // Sửa dòng 57
        createZombieBtn.onClick.AddListener(() => { _ = CreateLobby(PlayerRole.Zombie); });

        // Display player info
        DisplayPlayerInfo();

        // Initial UI state
        searchingPanel.SetActive(false);
        UpdateButtons(isSearching: false);

        _ = RefreshLobbyList();

        // InvokeRepeating(nameof(PeriodicRefresh), 15f, 15f); // Bỏ comment nếu muốn tự động refresh
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
        // Được gọi khi ta tạo lobby (đang đợi) hoặc join lobby (đang đợi)
        searchingPanel.SetActive(true);
        searchingText.text = $"Waiting for opponent... Role: {LobbyManager.Instance.GetRoleDisplayName(LobbyManager.Instance.SelectedRole)}";
        UpdateButtons(isSearching: true);
    }

    private void OnMatchFound(string matchId)
    {
        searchingText.text = "Match found! Connecting...";
    }

    private void OnMatchmakingFailed(string error)
    {
        searchingPanel.SetActive(false);
        Debug.LogError($"Matchmaking failed: {error}");
        UpdateButtons(isSearching: false);
    }

    private void OnMatchmakingCancelled()
    {
        searchingPanel.SetActive(false);
        UpdateButtons(isSearching: false);
    }

    /// <summary>
    /// Hàm mới thay thế UpdateUI, dùng để bật/tắt các nút dựa trên việc có đang ở trong lobby hay không.
    /// </summary>
    private void UpdateButtons(bool isSearching)
    {
        // Khi đang tìm/đợi, không cho refresh hoặc tạo/join lobby
        refreshLobbyListButton.interactable = !isSearching;
        createPlantBtn.interactable = !isSearching;
        createZombieBtn.interactable = !isSearching;

        // Chỉ cho phép hủy khi đang tìm/đợi
        cancelMatchmakingButton.interactable = isSearching;

        // Vô hiệu hóa các item trong danh sách lobby
        foreach (var itemGO in spawnedItems)
        {
            itemGO.GetComponentInChildren<Button>().interactable = !isSearching;
        }
    }



    private async void PeriodicRefresh() => await RefreshLobbyList();

    public async Task RefreshLobbyList()
    {
        if (LobbyManager.Instance == null) return;

        // Đảm bảo không refresh khi đang trong lobby
        if (LobbyManager.Instance.IsSearching) return;

        var lobbies = await LobbyManager.Instance.GetAvailableLobbiesAsync(30);

        // Clear existing UI
        foreach (var go in spawnedItems) Destroy(go);
        spawnedItems.Clear();

        foreach (var lobby in lobbies)
        {
            // Chọn prefab dựa trên role của host
            PlayerRole ownerRole = LobbyManager.Instance.GetLobbyOwnerRole(lobby);
            GameObject prefab = ownerRole == PlayerRole.Plant ? plantLobbyItemPrefab : zombieLobbyItemPrefab;
            
            var go = Instantiate(prefab, lobbyListContent);
            var item = go.GetComponent<LobbyListItem>();
            string name = lobby.Name;
            string owner = LobbyManager.Instance.GetLobbyOwnerName(lobby);
            item.Setup(lobby.Id, name, owner, ownerRole.ToString(), OnJoinLobbyClicked);
            
            // Đảm bảo item có kích thước cố định cho VerticalLayoutGroup
            var layoutElement = go.GetComponent<LayoutElement>();
            if (layoutElement == null) layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 30f; // Chiều cao mỗi item (điều chỉnh theo design)
            layoutElement.preferredWidth = -1; // Tự động theo parent
            layoutElement.flexibleWidth = 1; // Cho phép co giãn theo chiều ngang

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