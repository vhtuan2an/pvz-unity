using UnityEngine;
using UnityEngine.UI;

public class PauseButton : MonoBehaviour
{
    private Button btn;

    private void Start()
    {
        btn = GetComponent<Button>();
        if (btn == null)
        {
            btn = GetComponentInChildren<Button>();
        }

        if (btn != null)
        {
            btn.onClick.AddListener(OnPauseClicked);
        }
        else
        {
            Debug.LogWarning($"PauseButton on {gameObject.name} could not find a Button component!");
        }

        // Initial Visibility Check
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
            UpdateButtonVisibility(GameStateManager.Instance.CurrentState.Value);
        }
        else
        {
            gameObject.SetActive(false); // Default invalid
        }
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        UpdateButtonVisibility(newState);
    }

    private void UpdateButtonVisibility(GameStateManager.GameState state)
    {
        // Visible only during Gameplay phases where pausing makes sense
        bool visible = state == GameStateManager.GameState.Playing || 
                       state == GameStateManager.GameState.Paused || 
                       state == GameStateManager.GameState.Unpausing;
        
        gameObject.SetActive(visible);
    }

    private void OnPauseClicked()
    {
        // Safety check: Don't allow pausing if game hasn't started or is in intro
        if (GameStateManager.Instance == null) return;
        
        GameStateManager.Instance.TogglePauseServerRpc();
    }
}
