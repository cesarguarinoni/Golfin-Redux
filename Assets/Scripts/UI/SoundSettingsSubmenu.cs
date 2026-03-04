using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.UI
{
    /// <summary>
    /// Sound Settings submenu with Music and SFX volume controls.
    /// </summary>
    public class SoundSettingsSubmenu : MonoBehaviour
    {
        [Header("Music Volume")]
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private TextMeshProUGUI musicVolumeText;
        
        [Header("SFX Volume")]
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TextMeshProUGUI sfxVolumeText;
        
        [Header("Settings")]
        [SerializeField] private bool saveToPlayerPrefs = true;
        
        private const string MUSIC_VOLUME_KEY = "Settings_MusicVolume";
        private const string SFX_VOLUME_KEY = "Settings_SFXVolume";

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
        /// Load volume settings from PlayerPrefs.
        /// </summary>
        private void LoadSettings()
        {
            if (saveToPlayerPrefs)
            {
                float musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 0.8f);
                float sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 0.8f);
                
                if (musicVolumeSlider != null)
                {
                    musicVolumeSlider.value = musicVolume;
                }
                
                if (sfxVolumeSlider != null)
                {
                    sfxVolumeSlider.value = sfxVolume;
                }
                
                Debug.Log($"[SoundSettings] Loaded: Music={musicVolume:F2}, SFX={sfxVolume:F2}");
            }
            else
            {
                // Default values
                if (musicVolumeSlider != null) musicVolumeSlider.value = 0.8f;
                if (sfxVolumeSlider != null) sfxVolumeSlider.value = 0.8f;
            }
        }

        /// <summary>
        /// Called when music volume slider changes.
        /// </summary>
        private void OnMusicVolumeChanged(float value)
        {
            // Update text display
            if (musicVolumeText != null)
            {
                musicVolumeText.text = Mathf.RoundToInt(value * 100f).ToString();
            }
            
            // Save to PlayerPrefs
            if (saveToPlayerPrefs)
            {
                PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, value);
                PlayerPrefs.Save();
            }
            
            // TODO Phase 3: Apply to AudioManager
            // AudioManager.Instance?.SetMusicVolume(value);
            
            Debug.Log($"[SoundSettings] Music volume changed: {value:F2} ({Mathf.RoundToInt(value * 100)}%)");
        }

        /// <summary>
        /// Called when SFX volume slider changes.
        /// </summary>
        private void OnSFXVolumeChanged(float value)
        {
            // Update text display
            if (sfxVolumeText != null)
            {
                sfxVolumeText.text = Mathf.RoundToInt(value * 100f).ToString();
            }
            
            // Save to PlayerPrefs
            if (saveToPlayerPrefs)
            {
                PlayerPrefs.SetFloat(SFX_VOLUME_KEY, value);
                PlayerPrefs.Save();
            }
            
            // TODO Phase 3: Apply to AudioManager
            // AudioManager.Instance?.SetSFXVolume(value);
            
            Debug.Log($"[SoundSettings] SFX volume changed: {value:F2} ({Mathf.RoundToInt(value * 100)}%)");
        }

        /// <summary>
        /// Reset both volumes to default (80%).
        /// </summary>
        public void ResetToDefaults()
        {
            if (musicVolumeSlider != null) musicVolumeSlider.value = 0.8f;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = 0.8f;
            
            Debug.Log("[SoundSettings] Reset to defaults");
        }

        /// <summary>
        /// Get current music volume (0-1).
        /// </summary>
        public float GetMusicVolume()
        {
            return musicVolumeSlider != null ? musicVolumeSlider.value : 0.8f;
        }

        /// <summary>
        /// Get current SFX volume (0-1).
        /// </summary>
        public float GetSFXVolume()
        {
            return sfxVolumeSlider != null ? sfxVolumeSlider.value : 0.8f;
        }
    }
}
