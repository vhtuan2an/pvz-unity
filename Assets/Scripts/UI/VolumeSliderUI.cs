using UnityEngine;
using UnityEngine.UI;

public class VolumeSliderUI : MonoBehaviour
{
    [Header("Slider References")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    private void Start()
    {
        // Ensure AudioSettings exists
        AudioSettings.EnsureInstance();

        // Initialize sliders with saved values
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = AudioSettings.MusicVolume;
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = AudioSettings.SFXVolume;
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        // Subscribe to volume change events to sync UI if changed elsewhere
        if (AudioSettings.Instance != null)
        {
            AudioSettings.Instance.OnMusicVolumeChanged += UpdateMusicSlider;
            AudioSettings.Instance.OnSFXVolumeChanged += UpdateSFXSlider;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
        }

        if (AudioSettings.Instance != null)
        {
            AudioSettings.Instance.OnMusicVolumeChanged -= UpdateMusicSlider;
            AudioSettings.Instance.OnSFXVolumeChanged -= UpdateSFXSlider;
        }
    }

    private void OnMusicVolumeChanged(float value)
    {
        AudioSettings.MusicVolume = value;
        
        // Update currently playing music immediately
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.UpdateLoopingVolumes();
        }
    }

    private void OnSFXVolumeChanged(float value)
    {
        AudioSettings.SFXVolume = value;
    }

    private void UpdateMusicSlider(float value)
    {
        if (musicVolumeSlider != null && !Mathf.Approximately(musicVolumeSlider.value, value))
        {
            musicVolumeSlider.value = value;
        }
    }

    private void UpdateSFXSlider(float value)
    {
        if (sfxVolumeSlider != null && !Mathf.Approximately(sfxVolumeSlider.value, value))
        {
            sfxVolumeSlider.value = value;
        }
    }
}
