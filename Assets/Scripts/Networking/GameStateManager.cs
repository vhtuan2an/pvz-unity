using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStateManager : NetworkBehaviour
{
    public static GameStateManager Instance { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private float gameTimeLimit = 300f; // 5 minutes
    
    private NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> gameEnded = new NetworkVariable<bool>(false);
    private NetworkVariable<float> gameTimeRemaining = new NetworkVariable<float>(300f);
    private NetworkVariable<PlayerRole> winner = new NetworkVariable<PlayerRole>(PlayerRole.None);
    
    // Events
    public System.Action<PlayerRole> OnGameEnded;
    public System.Action<float> OnTimeUpdated;

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
            gameStarted.Value = true;
            Debug.Log("Game started!");
        }
        
        // Subscribe to events
        gameEnded.OnValueChanged += OnGameEndedChanged;
        gameTimeRemaining.OnValueChanged += OnTimeRemainingChanged;
    }

    public override void OnNetworkDespawn()
    {
        gameEnded.OnValueChanged -= OnGameEndedChanged;
        gameTimeRemaining.OnValueChanged -= OnTimeRemainingChanged;
        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (IsServer && gameStarted.Value && !gameEnded.Value)
        {
            gameTimeRemaining.Value -= Time.deltaTime;
            
            if (gameTimeRemaining.Value <= 0)
            {
                EndGame(PlayerRole.None); // Time's up - draw
            }
        }
    }

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
        
        // TODO: Show UI popup with result
        // You can integrate this with your UI system
    }

    private void OnGameEndedChanged(bool previousValue, bool newValue)
    {
        if (newValue)
        {
            Debug.Log("Game has ended!");
        }
    }

    private void OnTimeRemainingChanged(float previousValue, float newValue)
    {
        OnTimeUpdated?.Invoke(newValue);
        
        if (newValue <= 0)
        {
            Debug.Log("Time's up!");
        }
    }

    // Public getters
    public bool IsGameStarted => gameStarted.Value;
    public bool IsGameEnded => gameEnded.Value;
    public float TimeRemaining => gameTimeRemaining.Value;
    public PlayerRole Winner => winner.Value;
}
