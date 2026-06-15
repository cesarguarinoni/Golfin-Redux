using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

namespace Golfin.Audio
{
    /// <summary>
    /// Global audio manager - handles Music and SFX volume control.
    /// Singleton pattern, persists across scenes.
    ///
    /// Order 350: AudioMixer routing added. Volume applied via mixer dB parameters
    /// (MusicVol / SFXVol). IMPORTANT: SetFloat must NOT be called in Awake/OnEnable —
    /// AudioMixer is not ready then. Volume is loaded + applied in Start().
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [Tooltip("Dedicated AudioSource for background music")]
        public AudioSource musicSource;

        [Tooltip("Pool of AudioSources for SFX playback")]
        public List<AudioSource> sfxSources = new List<AudioSource>();

        [Header("AudioMixer (Order 350)")]
        [Tooltip("GolfinAudio mixer asset. Groups: Master → { Music, SFX }.")]
        [SerializeField] private AudioMixer _mixer;
        [SerializeField] private AudioMixerGroup _musicGroup;
        [SerializeField] private AudioMixerGroup _sfxGroup;

        // Exposed mixer parameter names (must match those exposed in GolfinAudio.mixer)
        private const string MixerMusicVol = "MusicVol";
        private const string MixerSfxVol   = "SFXVol";

        [Header("Volume Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float musicVolume = 0.7f;

        [Range(0f, 1f)]
        [SerializeField] private float sfxVolume = 0.7f;

        [Header("SFX Pool Size")]
        [SerializeField] private int sfxPoolSize = 5;

        private const string MUSIC_VOLUME_KEY = "Settings_MusicVolume";
        private const string SFX_VOLUME_KEY = "Settings_SFXVolume";

        // dB floor when volume = 0 (mute)
        private const float DB_FLOOR = -80f;

        /// <summary>Convert a linear 0–1 volume to decibels. Returns DB_FLOOR for 0 input.</summary>
        public static float LinearToDb(float linear01)
        {
            float clamped = Mathf.Clamp(linear01, 0.0001f, 1f);
            return Mathf.Log10(clamped) * 20f;
        }

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

            // Initialize audio sources (and wire mixer groups) — does NOT call SetFloat.
            InitializeAudioSources();

            // NOTE: volume preferences are loaded in Start() (not Awake) because
            // AudioMixer.SetFloat is not reliable in Awake/OnEnable.
            Debug.Log("[AudioManager] Awake complete. Volume load deferred to Start().");
        }

        private void Start()
        {
            // Load saved volume preferences and apply via mixer (safe to call here).
            LoadVolumePreferences();
            Debug.Log($"[AudioManager] Start: Music={musicVolume * 100:F0}%, SFX={sfxVolume * 100:F0}%");
        }

        /// <summary>
        /// Initialize audio sources if they don't exist and wire mixer groups.
        /// Does NOT call AudioMixer.SetFloat — that happens in Start().
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

            // Route music source → Music mixer group
            if (_musicGroup != null)
                musicSource.outputAudioMixerGroup = _musicGroup;

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

            // Route all SFX sources → SFX mixer group
            if (_sfxGroup != null)
            {
                foreach (var src in sfxSources)
                {
                    if (src != null)
                        src.outputAudioMixerGroup = _sfxGroup;
                }
            }
        }

        /// <summary>
        /// Load volume preferences from PlayerPrefs and apply via mixer.
        /// Migrate legacy 0–100 PlayerPrefs values → dB. Safe to call in Start() and later.
        /// </summary>
        private void LoadVolumePreferences()
        {
            // Load volumes (stored as 0–100, convert to 0–1).
            // Legacy keys preserved: "Settings_MusicVolume" / "Settings_SFXVolume".
            musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 70f) / 100f;
            sfxVolume   = PlayerPrefs.GetFloat(SFX_VOLUME_KEY,   70f) / 100f;

            ApplyVolumes();
        }

        /// <summary>
        /// Set music volume (0–100 scale). Internally drives AudioMixer dB parameter.
        /// </summary>
        public void SetMusicVolume(float volume)
        {
            volume = Mathf.Clamp(volume, 0f, 100f);
            musicVolume = volume / 100f;

            ApplyMusicVolume();

            PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, volume);
            PlayerPrefs.Save();

            Debug.Log($"[AudioManager] Music volume set to {volume}% ({musicVolume:F2})");
        }

        /// <summary>
        /// Set SFX volume (0–100 scale). Internally drives AudioMixer dB parameter.
        /// </summary>
        public void SetSFXVolume(float volume)
        {
            volume = Mathf.Clamp(volume, 0f, 100f);
            sfxVolume = volume / 100f;

            ApplySfxVolume();

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
        /// Apply current volume settings. Uses mixer dB if mixer is available;
        /// falls back to per-source volume if mixer is not wired (graceful degradation).
        /// </summary>
        private void ApplyVolumes()
        {
            ApplyMusicVolume();
            ApplySfxVolume();
        }

        private void ApplyMusicVolume()
        {
            if (_mixer != null)
            {
                float db = musicVolume <= 0f ? DB_FLOOR : LinearToDb(musicVolume);
                _mixer.SetFloat(MixerMusicVol, db);
            }
            else
            {
                // Fallback: direct source volume (no mixer)
                if (musicSource != null)
                    musicSource.volume = musicVolume;
            }
        }

        private void ApplySfxVolume()
        {
            if (_mixer != null)
            {
                float db = sfxVolume <= 0f ? DB_FLOOR : LinearToDb(sfxVolume);
                _mixer.SetFloat(MixerSfxVol, db);
            }
            else
            {
                // Fallback: direct source volume (no mixer)
                foreach (var src in sfxSources)
                {
                    if (src != null)
                        src.volume = sfxVolume;
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
