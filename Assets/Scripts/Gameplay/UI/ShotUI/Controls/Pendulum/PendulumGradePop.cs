using System.Collections;
using UnityEngine;
using TMPro;

namespace Golfin.Gameplay.UI.Controls.Pendulum
{
    /// <summary>
    /// The JUST! / GOOD / MISS pop above the ball (Figma <c>GradePop</c>, node 14091:33996).
    ///
    /// <para>ZERO HARDCODED TEXT. The three words come from <c>SHOT_GRADE_JUST/GOOD/MISS</c>
    /// through <see cref="LocalizationManager"/>, read at SHOW time rather than cached at Awake:
    /// the language can change under a live screen, and a cached string would leave the previous
    /// language on the first pop after the switch.</para>
    ///
    /// <para>It is a coroutine and not a tween because it has to be interruptible: a second swing
    /// can start before the 0.97 s of animation is done, and restarting the routine is what makes
    /// the pop belong to the shot the player is looking at.</para>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class PendulumGradePop : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private CanvasGroup     _group;

        [Header("Figma colours (Scheme — Pendulum / GradePop)")]
        [SerializeField] private Color _justColor = new Color(0xAD / 255f, 0xEB / 255f, 0xAD / 255f);
        [SerializeField] private Color _goodColor = new Color(0xFF / 255f, 0xEB / 255f, 0xA6 / 255f);
        [SerializeField] private Color _missColor = new Color(0xFF / 255f, 0x5A / 255f, 0x5A / 255f);

        [Header("Timing (seconds — scheme_pendulum §3.3)")]
        [SerializeField] private float _scaleInSeconds = 0.12f;
        [SerializeField] private float _holdSeconds    = 0.60f;
        [SerializeField] private float _fadeSeconds    = 0.25f;
        [Tooltip("Scale the pop starts at before springing to 1.")]
        [SerializeField] private float _startScale     = 0.6f;

        private Coroutine _routine;

        private void Awake()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            HideImmediate();
        }

        public void Show(PendulumGrade grade)
        {
            if (_label == null || _group == null) return;

            _label.text  = LocalizationManager.Get(PendulumMath.GradeKey(grade));
            _label.color = grade switch
            {
                PendulumGrade.Just => _justColor,
                PendulumGrade.Good => _goodColor,
                _                  => _missColor,
            };

            if (_routine != null) StopCoroutine(_routine);
            // A disabled root cannot run a coroutine; snap to the finished frame instead of
            // silently dropping the pop, so a driver that shows one while faded out is visible.
            if (!isActiveAndEnabled)
            {
                _group.alpha = 1f;
                transform.localScale = Vector3.one;
                return;
            }
            _routine = StartCoroutine(PlayRoutine());
        }

        public void HideImmediate()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (_group != null) _group.alpha = 0f;
            transform.localScale = Vector3.one * _startScale;
        }

        private IEnumerator PlayRoutine()
        {
            _group.alpha = 1f;

            for (float t = 0f; t < _scaleInSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(_startScale, 1f, t / Mathf.Max(_scaleInSeconds, 1e-4f));
                transform.localScale = Vector3.one * k;
                yield return null;
            }
            transform.localScale = Vector3.one;

            yield return new WaitForSeconds(_holdSeconds);

            for (float t = 0f; t < _fadeSeconds; t += Time.deltaTime)
            {
                _group.alpha = 1f - (t / Mathf.Max(_fadeSeconds, 1e-4f));
                yield return null;
            }

            _routine = null;
            HideImmediate();
        }
    }
}
