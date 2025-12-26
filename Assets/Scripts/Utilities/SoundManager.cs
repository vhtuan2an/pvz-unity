using UnityEngine;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int initialPoolSize = 10;
    
    private Queue<AudioSource> audioSourcePool;
    private GameObject poolContainer;

    private Dictionary<string, AudioClip[]> clipCache;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializePool();
        clipCache = new Dictionary<string, AudioClip[]>();
        Resources.LoadAll<AudioClip>("Audio");
        Debug.Log("[SoundManager] Preloaded all audio files into memory.");
    }

    private void InitializePool()
    {
        audioSourcePool = new Queue<AudioSource>();
        poolContainer = new GameObject("AudioPool");
        poolContainer.transform.SetParent(transform);

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewAudioSource();
        }
    }

    private AudioSource CreateNewAudioSource()
    {
        GameObject go = new GameObject("PooledAudioSource");
        go.transform.SetParent(poolContainer.transform);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        
        // Add to pool
        audioSourcePool.Enqueue(source);
        return source;
    }

    private AudioSource GetAudioSource()
    {
        if (audioSourcePool.Count == 0)
        {
            return CreateNewAudioSource();
        }

        AudioSource source = audioSourcePool.Dequeue();
        
        // Check if null (in case object was destroyed externally)
        if (source == null)
        {
            return CreateNewAudioSource();
        }

        return source;
    }

    private void ReturnToPool(AudioSource source, float delay)
    {
        StartCoroutine(ReturnToPoolRoutine(source, delay));
    }

    private System.Collections.IEnumerator ReturnToPoolRoutine(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (source != null)
        {
            source.Stop();
            source.clip = null;
            source.gameObject.SetActive(true); // Ensure it's active
            audioSourcePool.Enqueue(source);
        }
    }

    /// <summary>
    /// Plays a sound by name from Resources/Audio folder.
    /// If 'clipName' is a folder, picks a random clip from inside.
    /// </summary>
    /// <param name="clipName">Name of file or folder in Assets/Resources/Audio/</param>
    public void PlaySound(string clipName, float volume = 1f, float pitch = 1f)
    {
        AudioClip[] clips = GetClips(clipName);
        if (clips == null || clips.Length == 0) return;

        // Pick random clip
        AudioClip selectedClip = clips[Random.Range(0, clips.Length)];
        PlayClip(selectedClip, volume, pitch);
    }

    public void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        AudioSource source = GetAudioSource();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.Play();

        // Return to pool after it finishes
        ReturnToPool(source, clip.length / pitch + 0.1f);
    }

    private AudioClip[] GetClips(string name)
    {
        if (clipCache.ContainsKey(name))
        {
            return clipCache[name];
        }

        List<AudioClip> loadedClips = new List<AudioClip>();

        // 1. Try loading as a folder first (for random variations)
        // Check "Audio/{name}"
        AudioClip[] folderClips = Resources.LoadAll<AudioClip>($"Audio/{name}");
        if (folderClips != null && folderClips.Length > 0)
        {
            loadedClips.AddRange(folderClips);
        }
        else
        {
            // 2. Try loading as a single file
            AudioClip singleClip = Resources.Load<AudioClip>($"Audio/{name}");
            if (singleClip != null)
            {
                loadedClips.Add(singleClip);
            }
        }

        // If still nothing, try root Resources (legacy fallback)
        if (loadedClips.Count == 0)
        {
             folderClips = Resources.LoadAll<AudioClip>(name);
             if (folderClips != null && folderClips.Length > 0)
             {
                 loadedClips.AddRange(folderClips);
             }
        }

        if (loadedClips.Count > 0)
        {
            AudioClip[] result = loadedClips.ToArray();
            clipCache[name] = result;
            return result;
        }
        else
        {
            Debug.LogWarning($"[SoundManager] No AudioClips found for '{name}' (checked File and Folder in Resources/Audio)");
            return null;
        }
    }
}
