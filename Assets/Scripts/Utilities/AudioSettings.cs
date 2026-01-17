using UnityEngine;
using System;

public class AudioSettings : MonoBehaviour
{
    public static AudioSettings Instance { get; private set; }

    // Volume range: 0.0 to 1.0
    private static float musicVolume = 0.7f;
    private static float sfxVolume = 0.7f;

    // PlayerPrefs keys
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";

    // Events for updating UI
    public event Action<float> OnMusicVolumeChanged;
    public event Action<float> OnSFXVolumeChanged;

    public static float MusicVolume
    {
        get => musicVolume;
        set
        {
            musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, musicVolume);
            PlayerPrefs.Save();
            Instance?.OnMusicVolumeChanged?.Invoke(musicVolume);
            Debug.Log($"[AudioSettings] Music volume set to {musicVolume:F2}");
        }
    }

    public static float SFXVolume
    {
        get => sfxVolume;
        set
        {
            sfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SFX_VOLUME_KEY, sfxVolume);
            PlayerPrefs.Save();
            Instance?.OnSFXVolumeChanged?.Invoke(sfxVolume);
            Debug.Log($"[AudioSettings] SFX volume set to {sfxVolume:F2}");
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
    }

    private void LoadSettings()
    {
        // Load saved volumes or use defaults
        musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 0.7f);
        sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 0.7f);

        Debug.Log($"[AudioSettings] Loaded volumes - Music: {musicVolume:F2}, SFX: {sfxVolume:F2}");
    }

    /// <summary>
    /// Ensures AudioSettings instance exists. Creates one if it doesn't.
    /// </summary>
    public static void EnsureInstance()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("AudioSettings");
            Instance = go.AddComponent<AudioSettings>();
            Debug.Log("[AudioSettings] Auto-created AudioSettings instance.");
        }
    }
}
