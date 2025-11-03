using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;

public class LobbyUI : MonoBehaviour
{
    [Header("Player Info")]
    [SerializeField] private TMP_Text playerIdText;
    [SerializeField] private TMP_Text usernameText;

    // [Header("Role Selection")]
    // [SerializeField] private Button plantButton; // Không còn cần thiết
    // [SerializeField] private Button zombieButton; // Không còn cần thiết
    [SerializeField] private TMP_Text selectedRoleText; // Giữ lại để hiển thị role được gán

    [Header("Matchmaking")]
    [SerializeField] private Button refreshLobbyListButton; // Đổi tên từ startMatchmakingButton
    [SerializeField] private Button cancelMatchmakingButton;
    [SerializeField] private GameObject searchingPanel; // Dùng làm panel "Đang đợi"
    [SerializeField] private TMP_Text searchingText;

    [Header("Feedback")]
    [SerializeField] private TMP_Text feedbackText;

    [Header("List UI")]
    [SerializeField] private RectTransform lobbyListContent;
    [SerializeField] private GameObject lobbyListItemPrefab;
    [SerializeField] private TMP_Text statusText;

    [Header("Create")]
    [SerializeField] private Button createPlantBtn;
    [SerializeField] private Button createZombieBtn;

    private List<GameObject> spawnedItems = new List<GameObject>();

    private void Start()
    {
        // Subscribe to events
        LobbyManager.Instance.OnRoleSelected += OnRoleSelected;
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
        UpdateButtons(isSearching: false); // Cập nhật trạng thái nút ban đầu
        ClearFeedback();
        selectedRoleText.text = "Select a lobby to join or create one";

        _ = RefreshLobbyList();

        // InvokeRepeating(nameof(PeriodicRefresh), 15f, 15f); // Bỏ comment nếu muốn tự động refresh
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

        CancelInvoke(nameof(PeriodicRefresh));
    }

    private void DisplayPlayerInfo()
    {
        if (AuthenticationService.Instance.IsSignedIn)
        {
            string playerId = UnityAuthManager.Instance.GetPlayerId();
            playerIdText.text = $"Player ID: {playerId.Substring(0, 8)}...";

            // Bạn có thể lấy tên người dùng từ Auth service nếu đã đăng ký
            usernameText.text = $"Welcome, Player!";
        }
    }

    // private void OnRoleButtonClicked(PlayerRole role) // Không còn cần thiết
    // {
    //     LobbyManager.Instance.SelectRole(role);
    // }

    private void OnRoleSelected(PlayerRole role)
    {
        // Hàm này giờ được gọi khi ta Join một lobby và được gán Role
        selectedRoleText.text = $"Your Role: {LobbyManager.Instance.GetRoleDisplayName(role)}";
        selectedRoleText.color = role == PlayerRole.Plant ? Color.green : Color.red;
        // UpdateUI(); // Đã xóa
    }

    private async void OnCancelMatchmakingClicked()
    {
        ClearFeedback();
        await LobbyManager.Instance.CancelMatchmaking();
    }

    private void OnMatchmakingStarted()
    {
        // Được gọi khi ta tạo lobby (đang đợi) hoặc join lobby (đang đợi)
        searchingPanel.SetActive(true);
        searchingText.text = $"Waiting for opponent... Role: {LobbyManager.Instance.GetRoleDisplayName(LobbyManager.Instance.SelectedRole)}";
        UpdateButtons(isSearching: true);
        ClearFeedback();
    }

    private void OnMatchFound(string matchId)
    {
        // Game tự động bắt đầu, không cần làm gì ở UI này
        searchingPanel.SetActive(false);
        ShowFeedback($"Match found! Starting game...", false);
        UpdateButtons(isSearching: false); // Trạng thái này sẽ không ở lại lâu
    }

    private void OnMatchmakingFailed(string error)
    {
        searchingPanel.SetActive(false);
        ShowFeedback($"Error: {error}", true);
        UpdateButtons(isSearching: false);
    }

    private void OnMatchmakingCancelled()
    {
        searchingPanel.SetActive(false);
        ShowFeedback("Left lobby", false);
        UpdateButtons(isSearching: false);
        selectedRoleText.text = "Select a lobby to join or create one";
        selectedRoleText.color = Color.white;
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

    // private void UpdateUI() // Hàm cũ, không còn cần thiết
    // {
    //     // ...
    // }

    private void ShowFeedback(string message, bool isError)
    {
        feedbackText.text = message;
        feedbackText.color = isError ? Color.red : Color.green;
        feedbackText.gameObject.SetActive(true);
    }

    private void ClearFeedback()
    {
        feedbackText.text = "";
        feedbackText.gameObject.SetActive(false);
    }

    private async void PeriodicRefresh() => await RefreshLobbyList();

    public async Task RefreshLobbyList()
    {
        if (LobbyManager.Instance == null) return;

        // Đảm bảo không refresh khi đang trong lobby
        if (LobbyManager.Instance.IsSearching) return;

        statusText.text = "Loading lobbies...";
        ClearFeedback();
        var lobbies = await LobbyManager.Instance.GetAvailableLobbiesAsync(30);

        // Clear existing UI
        foreach (var go in spawnedItems) Destroy(go);
        spawnedItems.Clear();

        foreach (var lobby in lobbies)
        {
            var go = Instantiate(lobbyListItemPrefab, lobbyListContent);
            var item = go.GetComponent<LobbyListItem>();
            string name = lobby.Name;
            string owner = LobbyManager.Instance.GetLobbyOwnerName(lobby);
            string ownerRole = LobbyManager.Instance.GetLobbyOwnerRole(lobby).ToString();
            item.Setup(lobby.Id, name, owner, ownerRole, lobby.Players.Count, lobby.MaxPlayers, OnJoinLobbyClicked);
            spawnedItems.Add(go);
        }

        statusText.text = $"Found {lobbies.Count} lobbies";
    }

    private async void OnJoinLobbyClicked(string lobbyId)
    {
        ClearFeedback();
        statusText.text = "Joining lobby...";
        bool ok = await LobbyManager.Instance.JoinLobbyByIdAsyncPublic(lobbyId);

        if (ok)
        {
            // Event OnMatchmakingStarted sẽ được gọi từ LobbyManager,
            // không cần cập nhật UI ở đây
            statusText.text = $"Joined lobby. Waiting for host...";
        }
        else
        {
            statusText.text = "Failed to join lobby.";
            ShowFeedback("Failed to join lobby. It might be full or closed.", true);
            await RefreshLobbyList(); // Tải lại danh sách
        }
    }

    private async Task CreateLobby(PlayerRole role)
    {
        ClearFeedback();
        statusText.text = "Creating lobby...";
        bool ok = await LobbyManager.Instance.CreateLobbyWithRoleAsync(role);

        if (ok)
        {
            // Event OnMatchmakingStarted sẽ được gọi từ LobbyManager
            statusText.text = $"Lobby created. Waiting for opponent...";
        }
        else
        {
            statusText.text = "Failed to create lobby.";
            ShowFeedback("Failed to create lobby.", true);
        }
    }
}