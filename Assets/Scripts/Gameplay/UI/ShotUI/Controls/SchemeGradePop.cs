using System.Collections;
using UnityEngine;
using TMPro;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Gameplay.UI.Controls.FreeSwing;
using Golfin.Gameplay.UI.Controls.Needle;
using Golfin.Gameplay.UI.Controls.Pendulum;

namespace Golfin.Gameplay.UI.Controls
{
    /// <summary>
    /// The grade pop above the ball (Figma 14091:33996 / <c>ResultChip</c> 14091:102737) — one
    /// vocabulary in all four schemes since miss_grade_duff §3.6: PURE / GOOD / HOOK / SLICE /
    /// THIN / DUFF, and one three-step colour ladder taken from <see cref="ConeBandPalette"/>.
    ///
    /// <para>SHARED, WHICH IS WHY IT IS NO LONGER CALLED <c>PendulumGradePop</c>. It was renamed
    /// (file moved with its .meta, so every scene reference is untouched) when the second scheme
    /// needed the identical component: a word, a colour, a spring, a hold, a fade. Two copies
    /// would have been two places to fix the language-switch bug below. Every scheme's entry
    /// point still exists and still takes that scheme's own grade enum; what miss_grade_duff
    /// changed underneath them is the KEYS they resolve and the colours they resolve to.</para>
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

        // miss_grade_duff §3.6 (D8) — ONE three-step ladder, seeded from ConeBandPalette, which
        // is the same palette the Flick cone's bands and the Pendulum bar's bands are drawn in.
        // There used to be two groups here (JUST/GOOD/MISS and PERFECT/HOOK/SLICE/SHANK) whose
        // greens and reds were already identical literals and whose PERFECT was a blue that
        // agreed with nothing; a scheme cannot pop a colour the cone does not use any more.
        // Still [SerializeField] so a scene can override, but the DEFAULT is now derived.
        [Header("Grade colours (ConeBandPalette — one ladder for all four schemes)")]
        [SerializeField] private Color _pureColor = new Color(0xAD / 255f, 0xEB / 255f, 0xAD / 255f);
        [Tooltip("GOOD, HOOK, SLICE and THIN share the amber: every near-miss reads the same.")]
        [SerializeField] private Color _nearColor = new Color(0xFF / 255f, 0xEB / 255f, 0xA6 / 255f);
        [SerializeField] private Color _duffColor = new Color(0xFF / 255f, 0x5A / 255f, 0x5A / 255f);

        [Header("Timing (seconds — scheme_pendulum §3.3)")]
        [SerializeField] private float _scaleInSeconds = 0.12f;
        [SerializeField] private float _holdSeconds    = 0.60f;
        [SerializeField] private float _fadeSeconds    = 0.25f;

        /// <summary>
        /// How long a pop is on screen, end to end (spring + hold + fade). Named here because
        /// the power gauge's DUFF flash (miss_grade_duff §3.5) has to last exactly as long as the
        /// DUFF word it appears with — one number, not a second constant that drifts. The three
        /// fields above are serialized per-scene; this is the AUTHORED default, which is what a
        /// widget with no pop reference can ask for.
        /// </summary>
        public const float DisplaySeconds = 0.12f + 0.60f + 0.25f;

        /// <summary>This instance's actual on-screen time, in case a scene overrode the timings.</summary>
        public float InstanceDisplaySeconds => _scaleInSeconds + _holdSeconds + _fadeSeconds;
        [Tooltip("Scale the pop starts at before springing to 1.")]
        [SerializeField] private float _startScale     = 0.6f;

        private Coroutine _routine;

        private void Awake()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();

            // Re-seed from the palette before the first pop, the same way ConeMeshGraphic
            // re-syncs its band edges (F15 D3): these three are serialized, so a pop authored
            // before the ladder collapsed would keep the old blue PERFECT while the cone next to
            // it drew green. Only overwrite what has actually drifted, so a deliberate per-scene
            // override of ONE colour is not silently reverted by the other two.
            if (_pureColor != ConeBandPalette.GradePure) _pureColor = ConeBandPalette.GradePure;
            if (_nearColor != ConeBandPalette.GradeNear) _nearColor = ConeBandPalette.GradeNear;
            if (_duffColor != ConeBandPalette.GradeDuff) _duffColor = ConeBandPalette.GradeDuff;

            HideImmediate();
        }

        /// <summary>The Pendulum entry point. Unchanged from <c>PendulumGradePop</c>.</summary>
        public void Show(PendulumGrade grade) => Show(PendulumMath.GradeKey(grade), grade switch
        {
            PendulumGrade.Just => _pureColor,
            PendulumGrade.Good => _nearColor,
            _                  => _duffColor,
        });

        /// <summary>The Needle entry point (scheme_needle §3.3).</summary>
        public void Show(NeedleGrade grade) => Show(NeedleMath.GradeKey(grade), grade switch
        {
            NeedleGrade.Perfect => _pureColor,
            NeedleGrade.Shank   => _duffColor,
            _                   => _nearColor,
        });

        /// <summary>
        /// The Flick entry point (miss_grade_duff §3.6). The shipping scheme had no pop at all
        /// until this task: its grade comes from the band the aim latched in, not from a driver,
        /// so <c>FlickGradePopBinder</c> raises it off <c>ShotController.LastFlickGrade</c>.
        /// </summary>
        public void Show(FlickGrade grade) => Show(FlickMath.GradeKey(grade), grade switch
        {
            FlickGrade.Pure => _pureColor,
            FlickGrade.Duff => _duffColor,
            _               => _nearColor,   // GOOD and THIN: the same near-miss amber
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
                // This scheme's words were already the unified ones; now the colours are too.
                FreeSwingGrade.Pure => _pureColor,
                FreeSwingGrade.Duff => _duffColor,
                _                   => _nearColor,   // HOOK and SLICE: the same near-miss amber
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
