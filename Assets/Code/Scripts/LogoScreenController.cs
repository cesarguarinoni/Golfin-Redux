using System.Collections;
using UnityEngine;

namespace GolfinRedux.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class LogoScreenController : MonoBehaviour
    {
        [SerializeField] private float _fadeDuration = 1f;
        [SerializeField] private float _holdDuration = 1.5f;

        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            // Start fully visible so we never show an “empty” frame
            _canvasGroup.alpha = 1f;
        }

        private void OnEnable()
        {
            // Just run hold + fade-out; no fade-in from 0
            StartCoroutine(RunSequence());
        }

        private IEnumerator RunSequence()
        {
            // Hold fully visible
            yield return new WaitForSeconds(_holdDuration);

            // Fade out to 0
            float t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(1f - t / _fadeDuration);
                yield return null;
            }

            // Move to Splash
            var manager = FindObjectOfType<ScreenManager>();
            if (manager != null)
            {
                manager.ShowScreen(ScreenId.Splash, instant: true);
            }
        }
    }
}
