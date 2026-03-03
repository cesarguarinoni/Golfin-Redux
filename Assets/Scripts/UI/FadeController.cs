using System.Collections;
using UnityEngine;

namespace GolfinRedux.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class FadeController : MonoBehaviour
    {
        public static FadeController Instance { get; private set; }

        [SerializeField] private float _defaultDuration = 0.5f;

        private CanvasGroup _canvasGroup;
        private Coroutine _currentRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _canvasGroup = GetComponent<CanvasGroup>();
            DontDestroyOnLoad(gameObject); // optional if ShellScene persists
        }

        public void FadeIn(float? duration = null)
        {
            StartFade(1f, 0f, duration ?? _defaultDuration);
        }

        public void FadeOut(float? duration = null)
        {
            StartFade(0f, 1f, duration ?? _defaultDuration);
        }

        public void FadeOutThenIn(System.Action onMidpoint, float? duration = null)
        {
            if (_currentRoutine != null)
                StopCoroutine(_currentRoutine);
            _currentRoutine = StartCoroutine(FadeOutThenInRoutine(onMidpoint, duration ?? _defaultDuration));
        }

        private void StartFade(float from, float to, float duration)
        {
            if (_currentRoutine != null)
                StopCoroutine(_currentRoutine);
            _currentRoutine = StartCoroutine(FadeRoutine(from, to, duration));
        }

        private IEnumerator FadeRoutine(float from, float to, float duration)
        {
            float t = 0f;
            _canvasGroup.alpha = from;

            while (t < duration)
            {
                t += Time.deltaTime;
                float lerp = Mathf.Clamp01(t / duration);
                _canvasGroup.alpha = Mathf.Lerp(from, to, lerp);
                yield return null;
            }

            _canvasGroup.alpha = to;
        }

        private IEnumerator FadeOutThenInRoutine(System.Action onMidpoint, float duration)
        {
            // Fade to black
            yield return FadeRoutine(0f, 1f, duration * 0.5f);
            onMidpoint?.Invoke();
            // Fade from black
            yield return FadeRoutine(1f, 0f, duration * 0.5f);
        }
    }
}
