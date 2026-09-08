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
        /// <para>The toast now fades on the shared primitive, so it eases the way every other fade
        /// in the app eases (cubic ease-out) instead of running at a constant rate. Cesar's call,
        /// 2026-09-08, taken with the number in front of him.</para>
        ///
        /// <para><b>This is a deliberate, visible change, and the number is 0.385.</b> §C4 asked
        /// for both <c>UiMotion.Fade</c> and a per-frame alpha log within 0.01 of the old loop;
        /// those are not compatible. The old loop was a straight <c>Mathf.Lerp</c>, <c>Fade</c>
        /// eases, and the two curves are furthest apart at t = 0.423 where
        /// <c>1-(1-t)³ - t = 0.385</c> — thirty-eight times that tolerance. Over the 0.3 s
        /// fade-in the toast now reaches most of its opacity in the first third and settles
        /// gently, rather than ramping evenly. That IS the intent; §A6's tolerance was written
        /// against a "zero visible change" reading that the decision supersedes.</para>
        ///
        /// <para>What did NOT change: both durations (<c>_fadeIn</c> 0.3, <c>_fadeOut</c> 0.5),
        /// the frame count, and the endpoints. <c>ToastFadeParityTests</c> pins all three, pins
        /// the curve to <c>UiMotion.EaseOut</c> value-for-value, and pins the 0.385 itself — so
        /// the divergence from the old loop stays a recorded decision rather than becoming an
        /// unexplained difference somebody re-derives in a year.</para>
        ///
        /// <para><c>UiMotion.Fade</c> also settles the alpha to <paramref name="from"/> on its
        /// first frame, which the old loop never did — it began at one step in. A toast that is
        /// re-<c>Show()</c>n mid-fade therefore starts from a defined alpha instead of wherever
        /// the interrupted fade had reached.</para>
        /// </summary>
        IEnumerator Fade(float from, float to, float dur)
            => UiMotion.Fade(_canvasGroup, from, to, dur);
    }
}
