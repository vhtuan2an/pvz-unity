using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class PauseMenuUI : MonoBehaviour
{
    [System.Serializable]
    public class PauseLayout
    {
        public GameObject panelRoot;
        public Button resumeButton;
        public Button quitButton;
        public Image backgroundImage;
    }

    [Header("Layouts")]
    [SerializeField] private PauseLayout plantLayout;
    [SerializeField] private PauseLayout zombieLayout;

    private void Start()
    {
        HideAll();

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
        }

        SetupLayout(plantLayout);
        SetupLayout(zombieLayout);
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }
    }

    private void SetupLayout(PauseLayout layout)
    {
        if (layout.resumeButton != null)
        {
            layout.resumeButton.onClick.RemoveAllListeners();
            layout.resumeButton.onClick.AddListener(OnResumeClicked);
        }

        if (layout.quitButton != null)
        {
            layout.quitButton.onClick.RemoveAllListeners();
            layout.quitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        if (newState == GameStateManager.GameState.Paused)
        {
            Show();
        }
        else
        {
            HideAll();
        }
    }

    private void Show()
    {
        if (LobbyManager.Instance == null) return;
        
        PlayerRole role = LobbyManager.Instance.SelectedRole;

        if (role == PlayerRole.Plant)
        {
            if (plantLayout.panelRoot != null) plantLayout.panelRoot.SetActive(true);
            if (zombieLayout.panelRoot != null) zombieLayout.panelRoot.SetActive(false);
        }
        else
        {
            if (plantLayout.panelRoot != null) plantLayout.panelRoot.SetActive(false);
            if (zombieLayout.panelRoot != null) zombieLayout.panelRoot.SetActive(true);
        }
    }

    private void HideAll()
    {
        if (plantLayout.panelRoot != null) plantLayout.panelRoot.SetActive(false);
        if (zombieLayout.panelRoot != null) zombieLayout.panelRoot.SetActive(false);
    }

    private void OnResumeClicked()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.TogglePauseServerRpc();
        }
    }

    private void OnQuitClicked()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.QuitGameServerRpc();
        }
        else
        {
            // Fallback if GameStateManager missing
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene("LobbyScene"); 
        }
    }
}
