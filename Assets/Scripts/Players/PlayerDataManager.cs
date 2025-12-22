using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.CloudSave;

[Serializable]
public class PlayerStats
{
    public string username = "";
    public string playerId = "";
    public int wins = 0;
    public int losses = 0;
    public int totalGamesPlayed = 0;
    public int plantWins = 0;
    public int zombieWins = 0;
    public string lastPlayedRole = "None";
    public DateTime lastPlayedTime;
    public DateTime createdAt;
}

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance { get; private set; }

    public PlayerStats Stats { get; private set; } = new PlayerStats();
    public bool IsDataLoaded { get; private set; } = false;

    private const string STATS_KEY = "player_stats";

    // Events
    public event Action<PlayerStats> OnStatsUpdated;
    public event Action OnDataLoaded;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public async Task LoadPlayerDataAsync()
    {
        try
        {
            Debug.Log("Loading player data from Cloud Save...");

            var savedData = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { STATS_KEY }
            );

            if (savedData.TryGetValue(STATS_KEY, out var item))
            {
                Stats = item.Value.GetAs<PlayerStats>();
                Debug.Log($"Player data loaded: Username={Stats.username}, Wins={Stats.wins}, Losses={Stats.losses}");

                string currentUsername = GetCurrentUsername();
                if (!string.IsNullOrEmpty(currentUsername) && Stats.username != currentUsername)
                {
                    Stats.username = currentUsername;
                    await SavePlayerDataAsync();
                    Debug.Log($"Username updated to: {currentUsername}");
                }
            }
            else
            {
                Debug.Log("No existing data found, creating new player profile...");
                Stats = new PlayerStats
                {
                    username = GetCurrentUsername(),
                    playerId = GetCurrentPlayerId(),
                    createdAt = DateTime.UtcNow,
                    lastPlayedTime = DateTime.UtcNow
                };
                await SavePlayerDataAsync();
                Debug.Log($"New player profile created for: {Stats.username}");
            }

            IsDataLoaded = true;
            OnDataLoaded?.Invoke();
            OnStatsUpdated?.Invoke(Stats);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load player data: {ex.Message}");
            Stats = new PlayerStats();
            IsDataLoaded = true;
        }
    }
    private string GetCurrentUsername()
    {
        if (UnityAuthManager.Instance != null)
        {
            return UnityAuthManager.Instance.GetPlayerName() ?? "Anonymous";
        }
        return "Anonymous";
    }
    private string GetCurrentPlayerId()
    {
        if (UnityAuthManager.Instance != null)
        {
            return UnityAuthManager.Instance.GetPlayerId() ?? "";
        }
        return "";
    }

    public async Task SavePlayerDataAsync()
    {
        try
        {
            Debug.Log("Saving player data to Cloud Save...");

            var data = new Dictionary<string, object>
            {
                { STATS_KEY, Stats }
            };

            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            Debug.Log("Player data saved successfully");
            OnStatsUpdated?.Invoke(Stats);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save player data: {ex.Message}");
        }
    }


    public async Task UpdateUsernameAsync(string newUsername)
    {
        Stats.username = newUsername;
        await SavePlayerDataAsync();
        Debug.Log($"Username updated to: {newUsername}");
    }

    public async Task RecordWinAsync(PlayerRole role)
    {
        Stats.wins++;
        Stats.totalGamesPlayed++;
        Stats.lastPlayedRole = role.ToString();
        Stats.lastPlayedTime = DateTime.UtcNow;

        if (role == PlayerRole.Plant)
            Stats.plantWins++;
        else if (role == PlayerRole.Zombie)
            Stats.zombieWins++;

        Debug.Log($"Win recorded! Role: {role}, Total Wins: {Stats.wins}");
        await SavePlayerDataAsync();
    }

    public async Task RecordLossAsync(PlayerRole role)
    {
        Stats.losses++;
        Stats.totalGamesPlayed++;
        Stats.lastPlayedRole = role.ToString();
        Stats.lastPlayedTime = DateTime.UtcNow;

        Debug.Log($"Loss recorded! Role: {role}, Total Losses: {Stats.losses}");
        await SavePlayerDataAsync();
    }


    public float GetWinRate()
    {
        if (Stats.totalGamesPlayed == 0) return 0f;
        return (float)Stats.wins / Stats.totalGamesPlayed * 100f;
    }


    public async Task ResetDataAsync()
    {
        try
        {
            await CloudSaveService.Instance.Data.Player.DeleteAsync(STATS_KEY);
            Stats = new PlayerStats();
            Debug.Log("Player data reset successfully");
            OnStatsUpdated?.Invoke(Stats);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to reset player data: {ex.Message}");
        }
    }
}