using UnityEngine;

public class GameplayMusicManager : MonoBehaviour
{
    public static GameplayMusicManager Instance { get; private set; }

    private enum MusicStage
    {
        None,
        ChooseYourSeeds,
        FirstWave,
        MidWaveA,
        MidWaveB,
        FinalWave
    }

    private MusicStage currentStage = MusicStage.None;
    private string selectedTheme;
    private readonly string[] themes = { "PvZ1", "Modern Day", "Holiday Mashup" };
    
    // Health thresholds for music transitions
    private const float FIRST_WAVE_THRESHOLD = 0.75f;  // 75%
    private const float MID_WAVE_A_THRESHOLD = 0.45f;   // 45%
    private const float FINAL_WAVE_THRESHOLD = 0.15f;   // 15%

    private bool hasTransitionedToFirstWave = false;
    private bool hasTransitionedToMidWaveA = false;
    private bool hasTransitionedToMidWaveB = false;
    private bool hasTransitionedToFinalWave = false;

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

    private void Start()
    {
        // Randomly select a theme at the start
        selectedTheme = themes[Random.Range(0, themes.Length)];
        Debug.Log($"[GameplayMusicManager] Selected theme: {selectedTheme}");

        // Subscribe to game state changes
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
            GameStateManager.Instance.OnGameEnded += OnGameEnded;
        }
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
            GameStateManager.Instance.OnGameEnded -= OnGameEnded;
        }
    }

    private void Update()
    {
        // Monitor boss health during gameplay
        if (currentStage != MusicStage.None && currentStage != MusicStage.ChooseYourSeeds)
        {
            UpdateMusicBasedOnBossHealth();
        }
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        switch (newState)
        {
            case GameStateManager.GameState.Selection:
            case GameStateManager.GameState.Intro:
            case GameStateManager.GameState.Countdown:
                // Play "Choose Your Seeds" during selection, intro, and countdown
                if (currentStage != MusicStage.ChooseYourSeeds)
                {
                    PlayChooseYourSeeds();
                }
                break;

            case GameStateManager.GameState.Playing:
                // Transition to First Wave when gameplay starts
                if (currentStage == MusicStage.ChooseYourSeeds)
                {
                    PlayFirstWave();
                }
                break;

            case GameStateManager.GameState.GameOver:
                // Stop all background music when game ends
                StopCurrentMusic();
                break;
        }
    }

    private void PlayChooseYourSeeds()
    {
        currentStage = MusicStage.ChooseYourSeeds;
        string musicPath = $"background music/Choose Your Seeds - {selectedTheme}";
        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Stop("GameplayBGM");
            SoundManager.Instance.PlayLoop("GameplayBGM", musicPath);
            Debug.Log($"[GameplayMusicManager] Playing: {musicPath}");
        }
    }

    private void PlayFirstWave()
    {
        currentStage = MusicStage.FirstWave;
        hasTransitionedToFirstWave = true;
        string musicPath = $"background music/First Wave - {selectedTheme}";
        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Stop("GameplayBGM");
            SoundManager.Instance.PlayLoop("GameplayBGM", musicPath);
            Debug.Log($"[GameplayMusicManager] Playing: {musicPath}");
        }
    }

    private void PlayMidWaveA()
    {
        currentStage = MusicStage.MidWaveA;
        hasTransitionedToMidWaveA = true;
        string musicPath = $"background music/Mid Wave A - {selectedTheme}";
        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Stop("GameplayBGM");
            SoundManager.Instance.PlayLoop("GameplayBGM", musicPath);
            Debug.Log($"[GameplayMusicManager] Playing: {musicPath}");
        }
    }

    private void PlayMidWaveB()
    {
        currentStage = MusicStage.MidWaveB;
        hasTransitionedToMidWaveB = true;
        string musicPath = $"background music/Mid Wave B - {selectedTheme}";
        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Stop("GameplayBGM");
            SoundManager.Instance.PlayLoop("GameplayBGM", musicPath);
            Debug.Log($"[GameplayMusicManager] Playing: {musicPath}");
        }
    }

    private void PlayFinalWave()
    {
        currentStage = MusicStage.FinalWave;
        hasTransitionedToFinalWave = true;
        string musicPath = $"background music/Final Wave - {selectedTheme}";
        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Stop("GameplayBGM");
            SoundManager.Instance.PlayLoop("GameplayBGM", musicPath);
            Debug.Log($"[GameplayMusicManager] Playing: {musicPath}");
        }
    }

    private void UpdateMusicBasedOnBossHealth()
    {
        if (YourMomZombie.Instance == null) return;

        float healthPercentage = YourMomZombie.Instance.GetHealthPercentage();

        // Transition to Mid Wave A at 75% health
        if (!hasTransitionedToMidWaveA && healthPercentage <= FIRST_WAVE_THRESHOLD)
        {
            PlayMidWaveA();
        }
        // Transition to Mid Wave B at 45% health
        else if (!hasTransitionedToMidWaveB && healthPercentage <= MID_WAVE_A_THRESHOLD)
        {
            PlayMidWaveB();
        }
        // Transition to Final Wave at 15% health
        else if (!hasTransitionedToFinalWave && healthPercentage <= FINAL_WAVE_THRESHOLD)
        {
            PlayFinalWave();
        }
    }

    private void OnGameEnded(PlayerRole winner)
    {
        // Stop background music when game ends (victory sounds will play)
        StopCurrentMusic();
        Debug.Log($"[GameplayMusicManager] Game ended, stopping music. Winner: {winner}");
    }

    private void StopCurrentMusic()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Stop("GameplayBGM");
        }
        currentStage = MusicStage.None;
    }
}
