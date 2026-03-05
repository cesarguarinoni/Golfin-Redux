using UnityEngine;
using System.Collections.Generic;

namespace Golfin.Audio
{
    /// <summary>
    /// Global audio manager - handles Music and SFX volume control.
    /// Singleton pattern, persists across scenes.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [Tooltip("Dedicated AudioSource for background music")]
        public AudioSource musicSource;

        [Tooltip("Pool of AudioSources for SFX playback")]
        public List<AudioSource> sfxSources = new List<AudioSource>();

        [Header("Volume Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float musicVolume = 0.7f;

        [Range(0f, 1f)]
        [SerializeField] private float sfxVolume = 0.7f;

        [Header("SFX Pool Size")]
        [SerializeField] private int sfxPoolSize = 5;

        private const string MUSIC_VOLUME_KEY = "Settings_MusicVolume";
        private const string SFX_VOLUME_KEY = "Settings_SFXVolume";

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize audio sources if not assigned
            InitializeAudioSources();

            // Load saved volume preferences
            LoadVolumePreferences();

            Debug.Log($"[AudioManager] Initialized - Music: {musicVolume * 100}%, SFX: {sfxVolume * 100}%");
        }

        /// <summary>
        /// Initialize audio sources if they don't exist.
        /// </summary>
        private void InitializeAudioSources()
        {
            // Create music source if needed
            if (musicSource == null)
            {
                GameObject musicGO = new GameObject("MusicSource");
                musicGO.transform.SetParent(transform);
                musicSource = musicGO.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
            }

            // Create SFX source pool if needed
            if (sfxSources == null || sfxSources.Count == 0)
            {
                sfxSources = new List<AudioSource>();
                for (int i = 0; i < sfxPoolSize; i++)
                {
                    GameObject sfxGO = new GameObject($"SFXSource_{i}");
                    sfxGO.transform.SetParent(transform);
                    AudioSource sfxSource = sfxGO.AddComponent<AudioSource>();
                    sfxSource.playOnAwake = false;
                    sfxSources.Add(sfxSource);
                }
            }
        }

        /// <summary>
        /// Load volume preferences from PlayerPrefs.
        /// </summary>
        private void LoadVolumePreferences()
        {
            // Load volumes (stored as 0-100, convert to 0-1)
            musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 70f) / 100f;
            sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 70f) / 100f;

            ApplyVolumes();
        }

        /// <summary>
        /// Set music volume (0-100 scale).
        /// </summary>
        /// <param name="volume">Volume from 0 to 100</param>
        public void SetMusicVolume(float volume)
        {
            // Clamp to 0-100 range
            volume = Mathf.Clamp(volume, 0f, 100f);

            // Convert to 0-1 range
            musicVolume = volume / 100f;

            // Apply to music source
            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }

            // Save preference
            PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, volume);
            PlayerPrefs.Save();

            Debug.Log($"[AudioManager] Music volume set to {volume}% ({musicVolume:F2})");
        }

        /// <summary>
        /// Set SFX volume (0-100 scale).
        /// </summary>
        /// <param name="volume">Volume from 0 to 100</param>
        public void SetSFXVolume(float volume)
        {
            // Clamp to 0-100 range
            volume = Mathf.Clamp(volume, 0f, 100f);

            // Convert to 0-1 range
            sfxVolume = volume / 100f;

            // Apply to all SFX sources
            foreach (var sfxSource in sfxSources)
            {
                if (sfxSource != null)
                {
                    sfxSource.volume = sfxVolume;
                }
            }

            // Save preference
            PlayerPrefs.SetFloat(SFX_VOLUME_KEY, volume);
            PlayerPrefs.Save();

            Debug.Log($"[AudioManager] SFX volume set to {volume}% ({sfxVolume:F2})");
        }

        /// <summary>
        /// Get current music volume (0-100 scale).
        /// </summary>
        public float GetMusicVolume()
        {
            return musicVolume * 100f;
        }

        /// <summary>
        /// Get current SFX volume (0-100 scale).
        /// </summary>
        public float GetSFXVolume()
        {
            return sfxVolume * 100f;
        }

        /// <summary>
        /// Apply current volume settings to all sources.
        /// </summary>
        private void ApplyVolumes()
        {
            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }

            foreach (var sfxSource in sfxSources)
            {
                if (sfxSource != null)
                {
                    sfxSource.volume = sfxVolume;
                }
            }
        }

        // ========== MUSIC CONTROL ==========

        /// <summary>
        /// Play background music.
        /// </summary>
        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (musicSource == null || clip == null) return;

            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.Play();

            Debug.Log($"[AudioManager] Playing music: {clip.name}");
        }

        /// <summary>
        /// Stop background music.
        /// </summary>
        public void StopMusic()
        {
            if (musicSource != null)
            {
                musicSource.Stop();
            }
        }

        /// <summary>
        /// Pause background music.
        /// </summary>
        public void PauseMusic()
        {
            if (musicSource != null)
            {
                musicSource.Pause();
            }
        }

        /// <summary>
        /// Resume background music.
        /// </summary>
        public void ResumeMusic()
        {
            if (musicSource != null)
            {
                musicSource.UnPause();
            }
        }

        // ========== SFX CONTROL ==========

        /// <summary>
        /// Play a sound effect.
        /// </summary>
        public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f)
        {
            if (clip == null) return;

            // Find an available AudioSource
            AudioSource availableSource = GetAvailableSFXSource();

            if (availableSource != null)
            {
                availableSource.PlayOneShot(clip, volumeMultiplier);
            }
            else
            {
                Debug.LogWarning($"[AudioManager] No available SFX source for {clip.name}. Consider increasing pool size.");
            }
        }

        /// <summary>
        /// Play a sound effect at a specific position (3D audio).
        /// </summary>
        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeMultiplier = 1f)
        {
            if (clip == null) return;

            AudioSource.PlayClipAtPoint(clip, position, sfxVolume * volumeMultiplier);
        }

        /// <summary>
        /// Get an available SFX AudioSource from the pool.
        /// </summary>
        private AudioSource GetAvailableSFXSource()
        {
            // Find a source that's not playing
            foreach (var source in sfxSources)
            {
                if (source != null && !source.isPlaying)
                {
                    return source;
                }
            }

            // All sources busy, return the first one (will interrupt)
            return sfxSources.Count > 0 ? sfxSources[0] : null;
        }

        // ========== UTILITY ==========

        /// <summary>
        /// Mute all audio.
        /// </summary>
        public void MuteAll(bool mute)
        {
            if (musicSource != null)
            {
                musicSource.mute = mute;
            }

            foreach (var sfxSource in sfxSources)
            {
                if (sfxSource != null)
                {
                    sfxSource.mute = mute;
                }
            }

            Debug.Log($"[AudioManager] Audio {(mute ? "muted" : "unmuted")}");
        }

        /// <summary>
        /// Check if music is currently playing.
        /// </summary>
        public bool IsMusicPlaying()
        {
            return musicSource != null && musicSource.isPlaying;
        }
    }
}
