using System.Collections;
using UnityEngine;

namespace GolfinRedux.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class LogoScreenController : MonoBehaviour
    {
        [SerializeField] private float _fadeInDuration = 0.8f;
        [SerializeField] private float _holdDuration = 1.5f;
        [SerializeField] private float _fadeOutDuration = 0.8f;

        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f; // start fully transparent
        }

        private void OnEnable()
        {
            StartCoroutine(RunSequence());
        }

        private IEnumerator RunSequence()
        {
            // Fade in
            float t = 0f;
            while (t < _fadeInDuration)
            {
                t += Time.deltaTime;
                float lerp = Mathf.Clamp01(t / _fadeInDuration);
                _canvasGroup.alpha = lerp;
                yield return null;
            }

            // Hold fully visible
            _canvasGroup.alpha = 1f;
            yield return new WaitForSeconds(_holdDuration);

            // Fade out
            t = 0f;
            while (t < _fadeOutDuration)
            {
                t += Time.deltaTime;
                float lerp = Mathf.Clamp01(t / _fadeOutDuration);
                _canvasGroup.alpha = 1f - lerp;
                yield return null;
            }

            _canvasGroup.alpha = 0f;

            // Switch to Splash
            var manager = FindObjectOfType<ScreenManager>();
            if (manager != null)
                manager.ShowScreen(ScreenId.Splash, true);
            else
                Debug.LogError("ScreenManager not found");
        }
    }
}
