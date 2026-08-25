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

        // ── Gameplay curtain ──────────────────────────────────────────────────
        // FadeOverlay is child #1 of ShellScene's root Canvas, which runs at
        // sortingOrder -1. Everything that gives itself its own canvas draws straight
        // through a normal fade: the additively-loaded gameplay UI (ShotUI_Canvas 0,
        // LabCanvas 10 — the same trap LoadingScreenController.Awake documents), and
        // the shell's own later siblings (the modals at child index 2+).
        //
        // Screen-to-screen fades deliberately keep that behaviour — the persistent top
        // bar and bottom nav (PersistentUI, sortingOrder 0) are meant to stay lit while
        // the screen underneath them swaps. The curtain is therefore opt-in, and raises
        // the overlay only for as long as it is actually covering the screen: use it to
        // hide a teardown (scene unloads) that would otherwise be watched happening.

        // Max signed-16-bit — Unity serializes Canvas.sortingOrder as a short and 33000
        // reads back as -32536 (see HoleCompleteWidgetBuilder's note). 32767 clears every
        // canvas in the project: CameraModeDebugHUD 32760, TapFeedback 5000,
        // LoadingScreen 1000, Toast 950, modals 900/901, gameplay HUD 0-10.
        private const int CurtainSortingOrder = 32767;

        private Canvas _overlayCanvas;

        /// <summary>
        /// Fade to black ABOVE every other canvas, gameplay included, and leave it there.
        /// Always pair with <see cref="CurtainUp"/> — the screen stays black until you do.
        /// Yield on it from the caller's coroutine (the caller must be a MonoBehaviour that
        /// outlives whatever is being torn down).
        /// </summary>
        public IEnumerator CurtainDown(float? duration = null)
        {
            SetCurtainRaised(true);
            _gen++;
            yield return FadeRoutine(0f, 1f, duration ?? _defaultDuration * 0.5f, _gen);
        }

        /// <summary>Reveal what is now behind the curtain and hand sorting back.</summary>
        public IEnumerator CurtainUp(float? duration = null)
        {
            _gen++;
            int myGen = _gen;
            yield return FadeRoutine(1f, 0f, duration ?? _defaultDuration * 0.5f, myGen);

            // A superseded routine must not un-raise an overlay the fade that superseded
            // it is still relying on.
            if (myGen == _gen) SetCurtainRaised(false);
        }

        private void SetCurtainRaised(bool raised)
        {
            if (_overlayCanvas == null)
            {
                _overlayCanvas = GetComponent<Canvas>();
                if (_overlayCanvas == null) _overlayCanvas = gameObject.AddComponent<Canvas>();
            }

            // overrideSorting off = the nested canvas renders exactly where the overlay sat
            // before, so authored screen-transition behaviour is untouched between curtains.
            _overlayCanvas.overrideSorting = raised;
            if (raised) _overlayCanvas.sortingOrder = CurtainSortingOrder;
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
