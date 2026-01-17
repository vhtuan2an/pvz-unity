using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Automatically adds click sound to all buttons in the scene, including inactive ones.
/// Attach this to a manager GameObject in Login/Lobby scenes.
/// This is an alternative to adding ButtonSFX to each button individually.
/// </summary>
public class GlobalButtonSFX : MonoBehaviour
{
    [Header("Sound Settings")]
    [Tooltip("Drag and drop the audio clip for button click sound")]
    [SerializeField] private AudioClip clickSound;
    
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;
    
    [Range(0.5f, 2f)]
    [SerializeField] private float pitch = 1f;
    
    [Header("Options")]
    [Tooltip("If true, will find and register all buttons on Start.")]
    [SerializeField] private bool autoRegisterOnStart = true;
    
    [Tooltip("If true, includes inactive (hidden) buttons as well.")]
    [SerializeField] private bool includeInactiveButtons = true;
    
    private HashSet<Button> registeredButtons = new HashSet<Button>();
    
    private void Awake()
    {
        // Ensure SoundManager exists before we try to use it
        SoundManager.EnsureInstance();
    }
    
    private void Start()
    {
        if (autoRegisterOnStart)
        {
            RegisterAllButtons();
        }
    }
    
    /// <summary>
    /// Finds all buttons in the scene (including inactive ones) and adds click sound listeners to them.
    /// </summary>
    public void RegisterAllButtons()
    {
        Button[] allButtons;
        
        if (includeInactiveButtons)
        {
            // Find ALL buttons including inactive ones
            allButtons = Resources.FindObjectsOfTypeAll<Button>();
        }
        else
        {
            allButtons = FindObjectsByType<Button>(FindObjectsSortMode.None);
        }
        
        int registeredCount = 0;
        foreach (Button button in allButtons)
        {
            if (RegisterButton(button))
            {
                registeredCount++;
            }
        }
        
        Debug.Log($"[GlobalButtonSFX] Registered click sound for {registeredCount} buttons (Total found: {allButtons.Length}).");
    }
    
    /// <summary>
    /// Registers a single button to play click sound.
    /// Call this for dynamically created buttons.
    /// </summary>
    /// <param name="button">The button to register</param>
    /// <returns>True if registered, false if already registered or has ButtonSFX</returns>
    public bool RegisterButton(Button button)
    {
        if (button == null) return false;
        
        // Skip if button is from a prefab asset (not in scene)
        if (!button.gameObject.scene.IsValid()) return false;
        
        // Skip if already registered
        if (registeredButtons.Contains(button)) return false;
        
        // Skip if button already has ButtonSFX component
        if (button.GetComponent<ButtonSFX>() != null) return false;
        
        // Add listener for click sound
        button.onClick.AddListener(PlayClickSound);
        registeredButtons.Add(button);
        
        return true;
    }
    
    /// <summary>
    /// Plays the button click sound effect.
    /// </summary>
    public void PlayClickSound()
    {
        if (SoundManager.Instance != null && clickSound != null)
        {
            SoundManager.Instance.PlayClip(clickSound, volume, pitch);
        }
    }
    
    private void OnDestroy()
    {
        // Clean up listeners
        foreach (Button button in registeredButtons)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(PlayClickSound);
            }
        }
        registeredButtons.Clear();
    }
}
