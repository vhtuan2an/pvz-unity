using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds click sound effect to a UI Button.
/// Attach this component to any Button GameObject.
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonSFX : MonoBehaviour
{
    [Header("Sound Settings")]
    [Tooltip("Drag and drop the audio clip for button click sound")]
    [SerializeField] private AudioClip clickSound;
    
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;
    
    [Range(0.5f, 2f)]
    [SerializeField] private float pitch = 1f;
    
    private Button button;
    
    private void Awake()
    {
        // Ensure SoundManager exists before we try to use it
        SoundManager.EnsureInstance();
        
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(PlayClickSound);
        }
    }
    
    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(PlayClickSound);
        }
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
}
