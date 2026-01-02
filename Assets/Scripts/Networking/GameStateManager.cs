using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStateManager : NetworkBehaviour
{
    public static GameStateManager Instance { get; private set; }

    public enum GameState
    {
        Waiting,    // Waiting for players
        Selection,  // Players selecting plants/zombies
        Intro,      // Camera panning to lawn
        Countdown,  // Ready... Set... Plant!
        Playing,    // Active gameplay
        GameOver    // Game ended
    }

    [Header("Game Settings")]
    [SerializeField] private float gameTimeLimit = 300f; // 5 minutes
    
    // State Sync
    public NetworkVariable<GameState> CurrentState = new NetworkVariable<GameState>(GameState.Waiting);
    
    // Legacy support while transitioning
    private NetworkVariable<bool> gameEnded = new NetworkVariable<bool>(false);
    private NetworkVariable<float> gameTimeRemaining = new NetworkVariable<float>(300f);
    private NetworkVariable<PlayerRole> winner = new NetworkVariable<PlayerRole>(PlayerRole.None);

    // Readiness Sync
    public NetworkVariable<bool> IsPlantReady = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> IsZombieReady = new NetworkVariable<bool>(false);
    
    // Events
    public System.Action<GameState> OnStateChanged;
    public System.Action<PlayerRole> OnGameEnded;
    public System.Action<float> OnTimeUpdated;

    // Ready State Tracking
    private Dictionary<ulong, bool> playerReadyStatus = new Dictionary<ulong, bool>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsServer)
        {
            gameTimeRemaining.Value = gameTimeLimit;
            CurrentState.Value = GameState.Waiting;
            IsPlantReady.Value = false;
            IsZombieReady.Value = false;
        }
        
        // Subscribe to events
        CurrentState.OnValueChanged += OnGameStateChangedCallback;
        gameEnded.OnValueChanged += OnGameEndedChanged;
        gameTimeRemaining.OnValueChanged += OnTimeRemainingChanged;

        // Initial check
        if (IsClient)
        {
            OnStateChanged?.Invoke(CurrentState.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        CurrentState.OnValueChanged -= OnGameStateChangedCallback;
        gameEnded.OnValueChanged -= OnGameEndedChanged;
        gameTimeRemaining.OnValueChanged -= OnTimeRemainingChanged;
        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (!IsServer) return;

        // State Machine Logic
        switch (CurrentState.Value)
        {
            case GameState.Waiting:
                CheckPlayersConnected();
                break;
            case GameState.Selection:
                // Logic handled by SetPlayerReady
                break;
            case GameState.Intro:
                // Logic handled by CameraIntroController reporting back
                break;
            case GameState.Countdown:
                // Logic handled by StartCountdownUI reporting back
                break;
            case GameState.Playing:
                UpdateGameTimer();
                break;
        }
    }

    private void CheckPlayersConnected()
    {
        // Simple check: if we have 2 players (Host + Client), start selection
        // In a real lobby, you might check specific roles
        if (NetworkManager.Singleton.ConnectedClientsIds.Count >= 1) // Allow 1 for testing, 2 for real
        {
             // If we want to enforce 2 players:
             // if (NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
            
            // Move to Selection
            SetState(GameState.Selection);
        }
    }

    private void UpdateGameTimer()
    {
        if (gameEnded.Value) return;

        gameTimeRemaining.Value -= Time.deltaTime;
        
        if (gameTimeRemaining.Value <= 0)
        {
            EndGame(PlayerRole.None); // Time's up - draw
        }
    }

    public void SetState(GameState newState)
    {
        if (!IsServer) return;
        Debug.Log($"[GameStateManager] Changing State: {CurrentState.Value} -> {newState}");
        CurrentState.Value = newState;
    }

    private void OnGameStateChangedCallback(GameState previous, GameState current)
    {
        Debug.Log($"[GameState] Changed to {current}");
        OnStateChanged?.Invoke(current);
    }

    // ===================== READY SYSTEM =====================
    
    [ServerRpc(RequireOwnership = false)]
    public void SetPlayerReadyServerRpc(ulong clientId, bool isReady, PlayerRole role)
    {
        if (playerReadyStatus.ContainsKey(clientId))
            playerReadyStatus[clientId] = isReady;
        else
            playerReadyStatus.Add(clientId, isReady);

        // Update NetworkVariables for UI sync
        if (role == PlayerRole.Plant) IsPlantReady.Value = isReady;
        else if (role == PlayerRole.Zombie) IsZombieReady.Value = isReady;

        Debug.Log($"Player {clientId} ({role}) Ready: {isReady}");

        CheckAllPlayersReady();
    }

    private void CheckAllPlayersReady()
    {
        if (CurrentState.Value != GameState.Selection) return;

        // Rule: All connected players must be ready
        foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (!playerReadyStatus.ContainsKey(id) || !playerReadyStatus[id])
            {
                return; // Someone is not ready
            }
        }

        // All ready! Move to Intro
        Debug.Log("All players ready! Moving to Intro.");
        SetState(GameState.Intro);
    }

    // ===================== FLOW TRIGGERS =====================

    [ServerRpc(RequireOwnership = false)]
    public void ReportIntroFinishedServerRpc()
    {
         // This might be called by multiple clients, so ensure we only trigger once
         if (CurrentState.Value == GameState.Intro)
         {
             SetState(GameState.Countdown);
         }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ReportCountdownFinishedServerRpc()
    {
        if (CurrentState.Value == GameState.Countdown)
        {
            SetState(GameState.Playing);
        }
    }

    // ===================== END GAME (Legacy/Existing) =====================

    [ServerRpc(RequireOwnership = false)]
    public void EndGameServerRpc(PlayerRole winningRole)
    {
        if (gameEnded.Value) return;
        EndGame(winningRole);
    }

    private void EndGame(PlayerRole winningRole)
    {
        if (!IsServer) return;
        
        gameEnded.Value = true;
        winner.Value = winningRole;
        SetState(GameState.GameOver);
        
        string result = winningRole switch
        {
            PlayerRole.Plant => "Plants Win!",
            PlayerRole.Zombie => "Zombies Win!",
            _ => "Draw - Time's Up!"
        };
        
        Debug.Log($"Game ended: {result}");
        
        // Notify all clients
        EndGameClientRpc(winningRole);
    }

    [ClientRpc]
    private void EndGameClientRpc(PlayerRole winningRole)
    {
        OnGameEnded?.Invoke(winningRole);
        
        // Show end game UI
        ShowEndGameUI(winningRole);
    }

    private void ShowEndGameUI(PlayerRole winningRole)
    {
        string message = winningRole switch
        {
            PlayerRole.Plant => "Plants Win!",
            PlayerRole.Zombie => "Zombies Win!",
            _ => "Draw - Time's Up!"
        };
        
        Debug.Log($"Game Result: {message}");
        
        if (ZombieWinUI.Instance != null && winningRole == PlayerRole.Zombie)
        {
             ZombieWinUI.Instance.ShowZombieWin();
        }
    }

    private void OnGameEndedChanged(bool previousValue, bool newValue)
    {
        if (newValue) Debug.Log("Game has ended!");
    }

    private void OnTimeRemainingChanged(float previousValue, float newValue)
    {
        OnTimeUpdated?.Invoke(newValue);
    }

    // Public getters
    public bool IsGameStarted => CurrentState.Value == GameState.Playing;
    public bool IsGameEnded => gameEnded.Value;
    public float TimeRemaining => gameTimeRemaining.Value;
    public PlayerRole Winner => winner.Value;
}
