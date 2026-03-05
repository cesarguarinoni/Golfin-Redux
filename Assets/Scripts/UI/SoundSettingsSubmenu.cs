using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Audio;

namespace Golfin.UI
{
    /// <summary>
    /// Sound Settings submenu with Music and SFX volume controls.
    /// Integrates with AudioManager to apply volume changes in real-time.
    /// </summary>
    public class SoundSettingsSubmenu : MonoBehaviour
    {
        [Header("Music Volume")]
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private TextMeshProUGUI musicVolumeText;
        
        [Header("SFX Volume")]
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TextMeshProUGUI sfxVolumeText;

        private void Awake()
        {
            // Wire up slider events
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            }
            
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            }
        }

        private void Start()
        {
            LoadSettings();
        }

        /// <summary>
        /// Load volume settings from AudioManager.
        /// </summary>
        private void LoadSettings()
        {
            if (AudioManager.Instance != null)
            {
                // Get values from AudioManager (0-100 scale)
                float musicVolume = AudioManager.Instance.GetMusicVolume();
                float sfxVolume = AudioManager.Instance.GetSFXVolume();
                
                // Convert to slider range (0-1) and update sliders
                if (musicVolumeSlider != null)
                {
                    musicVolumeSlider.value = musicVolume / 100f;
                }
                
                if (sfxVolumeSlider != null)
                {
                    sfxVolumeSlider.value = sfxVolume / 100f;
                }
                
                // Update text displays
                if (musicVolumeText != null)
                {
                    musicVolumeText.text = Mathf.RoundToInt(musicVolume).ToString();
                }
                
                if (sfxVolumeText != null)
                {
                    sfxVolumeText.text = Mathf.RoundToInt(sfxVolume).ToString();
                }
                
                Debug.Log($"[SoundSettings] Loaded from AudioManager: Music={musicVolume}%, SFX={sfxVolume}%");
            }
            else
            {
                // Fallback if AudioManager not ready yet
                Debug.LogWarning("[SoundSettings] AudioManager not found! Using default values.");
                if (musicVolumeSlider != null) musicVolumeSlider.value = 0.7f;
                if (sfxVolumeSlider != null) sfxVolumeSlider.value = 0.7f;
            }
        }

        /// <summary>
        /// Called when music volume slider changes.
        /// </summary>
        private void OnMusicVolumeChanged(float value)
        {
            // Convert slider value (0-1) to percentage (0-100)
            float volumePercent = value * 100f;
            
            // Update text display
            if (musicVolumeText != null)
            {
                musicVolumeText.text = Mathf.RoundToInt(volumePercent).ToString();
            }
            
            // Apply to AudioManager (this also saves to PlayerPrefs)
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMusicVolume(volumePercent);
            }
            else
            {
                Debug.LogWarning("[SoundSettings] AudioManager not available!");
            }
        }

        /// <summary>
        /// Called when SFX volume slider changes.
        /// </summary>
        private void OnSFXVolumeChanged(float value)
        {
            // Convert slider value (0-1) to percentage (0-100)
            float volumePercent = value * 100f;
            
            // Update text display
            if (sfxVolumeText != null)
            {
                sfxVolumeText.text = Mathf.RoundToInt(volumePercent).ToString();
            }
            
            // Apply to AudioManager (this also saves to PlayerPrefs)
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetSFXVolume(volumePercent);
            }
            else
            {
                Debug.LogWarning("[SoundSettings] AudioManager not available!");
            }
        }

        /// <summary>
        /// Reset both volumes to default (70%).
        /// </summary>
        public void ResetToDefaults()
        {
            // Set sliders to default (0.7 = 70%)
            if (musicVolumeSlider != null) musicVolumeSlider.value = 0.7f;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = 0.7f;
            
            // Slider onValueChanged will trigger AudioManager updates
            
            Debug.Log("[SoundSettings] Reset to defaults (70%)");
        }

        /// <summary>
        /// Get current music volume (0-100).
        /// </summary>
        public float GetMusicVolume()
        {
            return AudioManager.Instance != null ? AudioManager.Instance.GetMusicVolume() : 70f;
        }

        /// <summary>
        /// Get current SFX volume (0-100).
        /// </summary>
        public float GetSFXVolume()
        {
            return AudioManager.Instance != null ? AudioManager.Instance.GetSFXVolume() : 70f;
        }
    }
}
