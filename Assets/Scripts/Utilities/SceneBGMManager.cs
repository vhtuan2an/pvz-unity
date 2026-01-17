using UnityEngine;

/// <summary>
/// Manages background music for a scene.
/// Attach this to a GameObject in Login/Lobby scenes.
/// BGM will start on scene load and stop when scene is destroyed.
/// </summary>
public class SceneBGMManager : MonoBehaviour
{
    [Header("BGM Settings")]
    [Tooltip("Drag and drop the audio clip for background music")]
    [SerializeField] private AudioClip bgmClip;
    
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.5f;
    
    private const string BGM_KEY = "SceneBGM";
    
    private void Awake()
    {
        // Ensure SoundManager exists before we try to use it
        SoundManager.EnsureInstance();
    }
    
    private void Start()
    {
        PlayBGM();
    }
    
    private void OnDestroy()
    {
        StopBGM();
    }
    
    /// <summary>
    /// Starts playing the background music in a loop.
    /// </summary>
    public void PlayBGM()
    {
        if (SoundManager.Instance != null && bgmClip != null)
        {
            SoundManager.Instance.PlayLoopClip(BGM_KEY, bgmClip, volume);
            Debug.Log($"[SceneBGMManager] Playing BGM: {bgmClip.name}");
        }
        else
        {
            if (bgmClip == null)
                Debug.LogWarning("[SceneBGMManager] BGM clip is not assigned!");
            if (SoundManager.Instance == null)
                Debug.LogWarning("[SceneBGMManager] SoundManager not found!");
        }
    }
    
    /// <summary>
    /// Stops the background music.
    /// </summary>
    public void StopBGM()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Stop(BGM_KEY);
            Debug.Log("[SceneBGMManager] Stopped BGM.");
        }
    }
    
    /// <summary>
    /// Changes the BGM to a new clip.
    /// </summary>
    public void ChangeBGM(AudioClip newClip, float newVolume = -1f)
    {
        StopBGM();
        bgmClip = newClip;
        if (newVolume >= 0f) volume = newVolume;
        PlayBGM();
    }
}
