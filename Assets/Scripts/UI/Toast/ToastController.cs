using System.Collections;
using UnityEngine;
using TMPro;

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

        IEnumerator Fade(float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                if (_canvasGroup != null)
                    _canvasGroup.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            if (_canvasGroup != null) _canvasGroup.alpha = to;
        }
    }
}
