using Unity.Netcode;
using System.Collections;
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
        Paused,     // Game paused
        Unpausing,  // Countdown to resume
        GameOver    // Game ended
    }

    private GameState previousStateBeforePause; // To return to correct state

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
        // Handle Input for everyone (Client + Server)
        if (CurrentState.Value == GameState.Playing || CurrentState.Value == GameState.Paused)
        {
            HandleInput();
        }

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
                UpdateGameTimer();
                break;
            case GameState.Paused:
                // Paused logic
                break;
            case GameState.Unpausing:
                // Waiting for routine to finish
                break;
        }
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            TogglePauseServerRpc();
        }
    }

    private void CheckPlayersConnected()
    {
        if (NetworkManager.Singleton.ConnectedClientsIds.Count >= 1)
        {
            SetState(GameState.Selection);
        }
    }

    private void UpdateGameTimer()
    {
        if (gameEnded.Value) return;

        gameTimeRemaining.Value -= Time.deltaTime;
        
        if (gameTimeRemaining.Value <= 0)
        {
            EndGame(PlayerRole.None, 0);
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
        
        if (current == GameState.Paused)
        {
             Time.timeScale = 0f;
        }
        else if (current == GameState.Playing)
        {
             Time.timeScale = 1f;
        }
    }
    
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

        // All connected players must be ready
        foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (!playerReadyStatus.ContainsKey(id) || !playerReadyStatus[id])
            {
                return;
            }
        }
        SetState(GameState.Intro);
    }
    
    [Header("Boss Spawn")]
    [SerializeField] private GameObject bossZombiePrefab;

    [ServerRpc(RequireOwnership = false)]
    public void ReportIntroFinishedServerRpc()
    {
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
            // Spawn Boss
            SpawnBoss();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TogglePauseServerRpc()
    {
        if (CurrentState.Value == GameState.Playing)
        {
            previousStateBeforePause = GameState.Playing;
            SetState(GameState.Paused);
        }
        else if (CurrentState.Value == GameState.Paused)
        {
            // Start Resume Sequence
            StartCoroutine(ResumeGameRoutine());
        }
    }

    private IEnumerator ResumeGameRoutine()
    {
        SetState(GameState.Unpausing);
        
        ShowResumeCountdownClientRpc();
        
        yield return new WaitForSecondsRealtime(3f);
        
        SetState(GameState.Playing);
    }

    [ClientRpc]
    private void ShowResumeCountdownClientRpc()
    {
        if (StartCountdownUI.Instance != null)
        {
            StartCountdownUI.Instance.StartResumeCountdown();
        }
    }

    private void SpawnBoss()
    {
        if (bossZombiePrefab != null)
        {
            GameObject boss = Instantiate(bossZombiePrefab, Vector3.zero, Quaternion.identity); // Position handled by Boss script intro
            boss.GetComponent<NetworkObject>().Spawn();
            Debug.Log("BOSS SPAWNED!");
        }
        else
        {
             Debug.LogWarning("Boss Prefab not assigned in GameStateManager!");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void EndGameServerRpc(PlayerRole winningRole, ulong focusNetworkId)
    {
        if (gameEnded.Value) return;
        EndGame(winningRole, focusNetworkId);
    }

    private void EndGame(PlayerRole winningRole, ulong focusNetworkId)
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
        EndGameClientRpc(winningRole, focusNetworkId);
    }

    [ClientRpc]
    private void EndGameClientRpc(PlayerRole winningRole, ulong focusNetworkId)
    {
        OnGameEnded?.Invoke(winningRole);
        
        // Show end game UI with Iris focus
        ShowEndGameUI(winningRole, focusNetworkId);
    }

    private void ShowEndGameUI(PlayerRole winningRole, ulong focusNetworkId)
    {
        // Try to find the focus target
        Vector3 targetPos = Vector3.zero;
        bool hasTarget = false;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(focusNetworkId, out NetworkObject netObj))
        {
            if (netObj != null) 
            {
                targetPos = netObj.transform.position;
                hasTarget = true;

                if (winningRole == PlayerRole.Zombie)
                {
                    if (netObj.TryGetComponent(out Animator anim)) anim.speed = 0f;
                    if (netObj.TryGetComponent(out ZombieBase zb)) zb.enabled = false;
                }
            }
        }

        // Trigger Iris Transition
        if (IrisTransitionUI.Instance != null)
        {
            if (hasTarget)
                IrisTransitionUI.Instance.PlayTransition(targetPos, winningRole);
            else
                IrisTransitionUI.Instance.PlayTransitionCentered(winningRole);
        }
        else
        {
            Debug.LogWarning("IrisTransitionUI not found! Falling back to legacy UI.");
            if (ZombieWinUI.Instance != null && winningRole == PlayerRole.Zombie)
                ZombieWinUI.Instance.ShowZombieWin();
            else if (PlantWinUI.Instance != null && winningRole == PlayerRole.Plant)
                PlantWinUI.Instance.ShowPlantWin();
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

    public bool IsGameStarted => CurrentState.Value == GameState.Playing;
    public bool IsGameEnded => gameEnded.Value;
    public float TimeRemaining => gameTimeRemaining.Value;
    public PlayerRole Winner => winner.Value;

    // ===================== GLOBAL QUIT =====================

    [ServerRpc(RequireOwnership = false)]
    public void QuitGameServerRpc()
    {
        Debug.Log("QuitGameServerRpc received. Ending game for all.");
        
        // Notify all clients to leave
        ReturnToLobbyClientRpc();
    }

    [ClientRpc]
    private void ReturnToLobbyClientRpc()
    {
        Debug.Log("Returning to Lobby...");
        
        // 1. Shutdown Network
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // 2. Load Lobby Scene
        SceneManager.LoadScene("LobbyScene");
    }
}
