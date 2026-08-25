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

        // Generation guard: each new fade request increments _gen.
        // Superseded routines check their captured myGen against _gen and yield break immediately,
        // so only the most-recently-started routine ever writes _canvasGroup.alpha.
        // StopCoroutine is NOT used — supersession is handled entirely by the generation counter.
        // This eliminates the stuck-opaque race where StopCoroutine killed a routine after its
        // fade-to-black but before its fade-back-in, leaving the overlay permanently black.
        private int _gen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _canvasGroup = GetComponent<CanvasGroup>();
            // DontDestroyOnLoad intentionally omitted: FadeController lives inside Canvas,
            // which is not a root GameObject. ShellScene persists anyway.
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
            _gen++;
            StartCoroutine(FadeOutThenInRoutine(onMidpoint, duration ?? _defaultDuration, _gen));
        }

        private void StartFade(float from, float to, float duration)
        {
            _gen++;
            StartCoroutine(FadeRoutine(from, to, duration, _gen));
        }

        private IEnumerator FadeRoutine(float from, float to, float duration, int myGen)
        {
            // Superseded before we even started: exit without touching alpha.
            if (myGen != _gen) yield break;

            float t = 0f;
            _canvasGroup.alpha = from;

            while (t < duration)
            {
                t += Time.deltaTime;
                // Check for supersession on every frame before writing alpha.
                if (myGen != _gen) yield break;
                float lerp = Mathf.Clamp01(t / duration);
                _canvasGroup.alpha = Mathf.Lerp(from, to, lerp);
                yield return null;
            }

            // Final check before committing the terminal value.
            if (myGen != _gen) yield break;
            _canvasGroup.alpha = to;
        }

        private IEnumerator FadeOutThenInRoutine(System.Action onMidpoint, float duration, int myGen)
        {
            // Fade to black (0 → 1 alpha on the overlay = black)
            yield return FadeRoutine(0f, 1f, duration * 0.5f, myGen);

            // If superseded at the midpoint, do not invoke the callback or fade back in.
            if (myGen != _gen) yield break;

            onMidpoint?.Invoke();

            // Fade back to transparent (1 → 0 alpha)
            yield return FadeRoutine(1f, 0f, duration * 0.5f, myGen);
        }
    }
}
