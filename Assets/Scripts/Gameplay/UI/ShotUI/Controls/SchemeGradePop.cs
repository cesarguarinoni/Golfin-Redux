using System.Collections;
using UnityEngine;
using TMPro;
using Golfin.Gameplay.UI.Controls.FreeSwing;
using Golfin.Gameplay.UI.Controls.Needle;
using Golfin.Gameplay.UI.Controls.Pendulum;

namespace Golfin.Gameplay.UI.Controls
{
    /// <summary>
    /// The grade pop above the ball — JUST! / GOOD / MISS under Pendulum (Figma 14091:33996),
    /// PERFECT / HOOK / SLICE / SHANK under Needle (Figma <c>ResultChip</c> 14091:102737).
    ///
    /// <para>SHARED, WHICH IS WHY IT IS NO LONGER CALLED <c>PendulumGradePop</c>. It was renamed
    /// (file moved with its .meta, so every scene reference is untouched) when the second scheme
    /// needed the identical component: a word, a colour, a spring, a hold, a fade. Two copies
    /// would have been two places to fix the language-switch bug below. Pendulum's behaviour is
    /// unchanged — <c>Show(PendulumGrade)</c> still exists and still resolves the same three keys
    /// and the same three serialized colours.</para>
    ///
    /// <para>ZERO HARDCODED TEXT. Every word comes from <see cref="LocalizationManager"/> through a
    /// key, read at SHOW time rather than cached at Awake: the language can change under a live
    /// screen, and a cached string would leave the previous language on the first pop after the
    /// switch.</para>
    ///
    /// <para>It is a coroutine and not a tween because it has to be interruptible: a second swing
    /// can start before the 0.97 s of animation is done, and restarting the routine is what makes
    /// the pop belong to the shot the player is looking at.</para>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SchemeGradePop : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private CanvasGroup     _group;

        [Header("Figma colours (Scheme — Pendulum / GradePop)")]
        [SerializeField] private Color _justColor = new Color(0xAD / 255f, 0xEB / 255f, 0xAD / 255f);
        [SerializeField] private Color _goodColor = new Color(0xFF / 255f, 0xEB / 255f, 0xA6 / 255f);
        [SerializeField] private Color _missColor = new Color(0xFF / 255f, 0x5A / 255f, 0x5A / 255f);

        [Header("Figma colours (Scheme — Needle / ResultChip 14091:102737)")]
        [SerializeField] private Color _perfectColor = new Color(0x4D / 255f, 0xA3 / 255f, 0xFF / 255f);
        [Tooltip("HOOK and SLICE share the amber; they are the same near-miss on opposite sides.")]
        [SerializeField] private Color _nearMissColor = new Color(0xFF / 255f, 0xEB / 255f, 0xA6 / 255f);
        [SerializeField] private Color _shankColor    = new Color(0xFF / 255f, 0x5A / 255f, 0x5A / 255f);

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

        /// <summary>The Pendulum entry point. Unchanged from <c>PendulumGradePop</c>.</summary>
        public void Show(PendulumGrade grade) => Show(PendulumMath.GradeKey(grade), grade switch
        {
            PendulumGrade.Just => _justColor,
            PendulumGrade.Good => _goodColor,
            _                  => _missColor,
        });

        /// <summary>The Needle entry point (scheme_needle §3.3).</summary>
        public void Show(NeedleGrade grade) => Show(NeedleMath.GradeKey(grade), grade switch
        {
            NeedleGrade.Perfect => _perfectColor,
            NeedleGrade.Shank   => _shankColor,
            _                   => _nearMissColor,
        });

        /// <summary>
        /// The Free Swing entry point (scheme_freeswing §3.3).
        ///
        /// <para>Returns without showing anything for <see cref="FreeSwingGrade.None"/>, which is
        /// the COMMON case in that scheme and not an error: an ordinary swing gets the analyzer
        /// chip and no banner, and the pop is reserved for PURE / DUFF / HOOK / SLICE. Guarded
        /// here as well as at the call site so a future caller cannot resolve a null key into a
        /// blank word hanging over the ball.</para>
        /// </summary>
        public void Show(FreeSwingGrade grade)
        {
            if (grade == FreeSwingGrade.None) return;
            Show(FreeSwingMath.GradeKey(grade), grade switch
            {
                // PURE reuses the JUST green and DUFF the MISS red — the same three-step ladder
                // the other two schemes pop, so a player who has learned one has learned this one.
                FreeSwingGrade.Pure => _justColor,
                FreeSwingGrade.Duff => _missColor,
                _                   => _nearMissColor,   // HOOK and SLICE: the same near-miss amber
            });
        }

        /// <summary>The one that does the work: a localisation KEY and a colour. Public so a
        /// future scheme adds a grade enum and a mapping, not another copy of this animation.</summary>
        public void Show(string key, Color color)
        {
            if (_label == null || _group == null) return;

            _label.text  = LocalizationManager.Get(key);
            _label.color = color;
            LastKeyShown = key;

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

        /// <summary>The key of the last word shown. Read back by the tests and the acceptance run,
        /// so "the pop said SHANK" is checkable without reading pixels — and so the zero-hardcoded-
        /// text rule is checkable too: what is asserted is a KEY, never a word.</summary>
        public string LastKeyShown { get; private set; }

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

        /// <summary>EditMode wiring seam — a plain MonoBehaviour gets no Awake in EditMode.</summary>
        public void ConfigureForTests(TextMeshProUGUI label, CanvasGroup group)
        {
            _label = label; _group = group;
        }
    }
}
