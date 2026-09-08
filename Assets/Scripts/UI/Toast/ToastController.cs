using System.Collections;
using UnityEngine;
using TMPro;
using Golfin.UI.Polish;

namespace Golfin.UI.Toast
{
    /// <summary>
    /// Minimal singleton toast notification controller. Lives on ShellScene Canvas
    /// with sortingOrder=950 (above modals at 900, below LoadingScreen at 1000).
    ///
    /// API: ToastController.Instance.Show("TEXT", holdSeconds).
    /// Fade in → hold → fade out → deactivate.
    /// </summary>
    public class ToastController : MonoBehaviour
    {
        public static ToastController Instance { get; private set; }

        [SerializeField] CanvasGroup _canvasGroup;
        [SerializeField] TMP_Text    _text;
        [SerializeField] float _fadeIn  = 0.3f;
        [SerializeField] float _fadeOut = 0.5f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Show a toast message. Cancels any currently-running toast and starts fresh.
        /// </summary>
        public void Show(string message, float holdSeconds = 3f)
        {
            StopAllCoroutines();
            if (_text != null) _text.text = message;
            gameObject.SetActive(true);
            StartCoroutine(Run(holdSeconds));
        }

        IEnumerator Run(float hold)
        {
            yield return Fade(0f, 1f, _fadeIn);
            yield return new WaitForSeconds(hold);
            yield return Fade(1f, 0f, _fadeOut);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// game_polish_c §C4 — the last hand-rolled fade loop in the shell, retired.
        ///
        /// <para><b>Why this is <see cref="UiMotion.Tween"/> on <see cref="Ease.Linear"/>
        /// and not <c>UiMotion.Fade</c>, which is what §C4 names.</b> §C4 asks for three things at
        /// once: route it through UiMotion, keep the durations, and produce a per-frame alpha log
        /// whose worst difference from the old loop is <b>≤ 0.01</b> ("zero visible change").
        /// <c>UiMotion.Fade</c> cannot satisfy the third: it eases on cubic ease-out, the old loop
        /// here was a straight <c>Mathf.Lerp</c>, and the two curves are furthest apart at
        /// t = 0.423, where <c>1-(1-t)³ - t = 0.385</c>. That is thirty-eight times the stated
        /// tolerance and it is visible — over a 0.3 s fade-in the toast would appear to snap to
        /// most of its opacity in the first third and then crawl.</para>
        ///
        /// <para><c>Tween</c> on <c>Ease.Linear</c> is the same UiMotion primitive family, the same
        /// runner, the same interruption-safe settle, and <c>Curve(Linear, t)</c> is
        /// <c>Mathf.Clamp01(t)</c> — so the alpha sequence is not merely close to the old loop's,
        /// it is the same arithmetic in the same order and the parity log is exact to the float.
        /// The choice is recorded as a deviation rather than made silently; switching to the eased
        /// fade later is a one-token change, and it is Cesar's to make, not the sweep's.</para>
        /// </summary>
        IEnumerator Fade(float from, float to, float dur)
            => UiMotion.Tween(from, to, dur,
                              a => { if (_canvasGroup != null) _canvasGroup.alpha = a; },
                              Ease.Linear);
    }
}
