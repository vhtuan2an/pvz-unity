using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using System.Collections;

public class GameSceneUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject gameUIPanel;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text connectionStatusText;
    [SerializeField] private TMP_Text modeText; // ✅ Hiển thị Test Mode hoặc Production

    [Header("Volume Sliders")]
    [SerializeField] private GameObject volumeSliderPanel; // Parent panel containing both sliders
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

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

        // Initialize volume sliders
        InitializeVolumeSliders();
    }

    private void InitializeVolumeSliders()
    {
        // Ensure AudioSettings exists
        AudioSettings.EnsureInstance();

        // Hide volume sliders initially (show only during Playing)
        if (volumeSliderPanel != null)
        {
            volumeSliderPanel.SetActive(false);
        }

        // Setup Music Volume Slider
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = AudioSettings.MusicVolume;
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        // Setup SFX Volume Slider
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = AudioSettings.SFXVolume;
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        // Subscribe to game state changes
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
        }
    }

    private void OnMusicVolumeChanged(float value)
    {
        AudioSettings.MusicVolume = value;
        
        // Update currently playing music immediately
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.UpdateLoopingVolumes();
        }
    }

    private void OnSFXVolumeChanged(float value)
    {
        AudioSettings.SFXVolume = value;
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        // Show volume sliders during Playing and Paused states
        if (volumeSliderPanel != null)
        {
            bool shouldShow = newState == GameStateManager.GameState.Playing || 
                              newState == GameStateManager.GameState.Paused;
            volumeSliderPanel.SetActive(shouldShow);
        }
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

        // Clean up volume slider listeners
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
        }

        // Unsubscribe from game state changes
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }
    }
}
