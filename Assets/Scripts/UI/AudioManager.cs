using UnityEngine;

namespace GolfinRedux.UI
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _sfxSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void PlayButtonClick()
        {
            // TODO: assign a click SFX to _sfxSource.clip in the editor.
            if (_sfxSource != null)
            {
                _sfxSource.Play();
            }
        }

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (_musicSource == null || clip == null) return;
            _musicSource.clip = clip;
            _musicSource.loop = loop;
            _musicSource.Play();
        }

        public void SetMusicVolume(float value)
        {
            if (_musicSource != null)
                _musicSource.volume = value;
        }

        public void SetSfxVolume(float value)
        {
            if (_sfxSource != null)
                _sfxSource.volume = value;
        }
    }
}
