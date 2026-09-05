using System.Collections;
using System.Globalization;
using UnityEngine;
using TMPro;

namespace Golfin.Gameplay.UI.Controls.FreeSwing
{
    /// <summary>
    /// The analyzer chip above the ball (Figma <c>AnalyzerChip</c> 14091:103270): four columns —
    /// POWER, IMPACT, PATH, TEMPO — reading what the swing that just fired actually committed.
    ///
    /// <para>IT READS THE VERDICT, NOT THE FINGER. Every number and every word comes out of one
    /// <c>FreeSwingMath.Verdict</c>, which is the same struct the <c>ShotIntent</c> was built
    /// from, so the chip cannot describe a shot other than the one that was taken. That is the
    /// whole reason the verdict carries its measurement fields at all.</para>
    ///
    /// <para>NEVER HIDDEN BY <c>Resolving</c> — carry-over 7, and the Needle report §10 scar.
    /// <c>CommitExternal</c> reaches <c>Resolving</c> synchronously, so a shared fading view
    /// would drop this chip about two frames after the shot, which is before a human has read a
    /// word of it. It comes up at the commit, holds for <c>FreeSwingAnalyzerSeconds</c>, fades on
    /// its own, and is only ever hidden early by <c>Idle</c>.</para>
    ///
    /// <para>ZERO HARDCODED TEXT. The four labels and the PATH/TEMPO words are localisation KEYS,
    /// resolved at SHOW time rather than cached at Awake (the language can change under a live
    /// screen). The two NUMBERS are formatted, not translated — and the <c>px</c> unit and the
    /// <c>◀ ▶</c> arrowheads live in format constants on <c>FreeSwingMath</c> rather than in a
    /// <c>.text</c> literal here, which is what the fidelity linter's unlocalized-text check
    /// looks for.</para>
    ///
    /// <para>A coroutine and not a tween, for the reason <c>SchemeGradePop</c> is one: a second
    /// swing can start before the hold is over, and restarting the routine is what makes the chip
    /// belong to the shot the player is looking at.</para>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class FreeSwingAnalyzerChip : MonoBehaviour
    {
        [Header("Figma: AnalyzerChip (14091:103270)")]
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TextMeshProUGUI _labelPower;
        [SerializeField] private TextMeshProUGUI _labelImpact;
        [SerializeField] private TextMeshProUGUI _labelPath;
        [SerializeField] private TextMeshProUGUI _labelTempo;
        [SerializeField] private TextMeshProUGUI _valuePower;
        [SerializeField] private TextMeshProUGUI _valueImpact;
        [SerializeField] private TextMeshProUGUI _valuePath;
        [SerializeField] private TextMeshProUGUI _valueTempo;

        [Header("Timing (seconds)")]
        [Tooltip("Hold before the fade. Overwritten from ControlsConfig.FreeSwingAnalyzerSeconds " +
                 "by the driver, so the CSV is the tuning surface and this is only the fallback.")]
        [SerializeField] private float _holdSeconds = 1.5f;
        [Tooltip("Fade OUT only — the chip comes up on the frame it is shown. See Show().")]
        [SerializeField] private float _fadeSeconds  = 0.35f;

        private Coroutine _routine;

        private CanvasGroup Group
        {
            get
            {
                if (_group == null) _group = GetComponent<CanvasGroup>();
                return _group;
            }
        }

        private void Awake() => HideImmediate();

        /// <summary>How long the chip holds at full opacity. Set from the config by the driver.</summary>
        public void SetHoldSeconds(float seconds) => _holdSeconds = Mathf.Max(seconds, 0f);

        /// <summary>
        /// Fill the four columns from the verdict and bring the chip up.
        /// </summary>
        public void Show(in FreeSwingMath.Verdict v)
        {
            SetText(_labelPower,  LocalizationManager.Get(FreeSwingMath.KeyPower));
            SetText(_labelImpact, LocalizationManager.Get(FreeSwingMath.KeyImpact));
            SetText(_labelPath,   LocalizationManager.Get(FreeSwingMath.KeyPath));
            SetText(_labelTempo,  LocalizationManager.Get(FreeSwingMath.KeyTempo));

            // POWER is the number that FIRED, i.e. the peak pull AFTER the tempo multiplier —
            // showing the raw peak would promise a distance the ball is not going to travel on a
            // mistimed swing, which is the one thing a readout labelled POWER must not do.
            float firedPower = v.PowerNormalized * v.TimingMul;
            SetText(_valuePower, string.Format(CultureInfo.InvariantCulture,
                                               FreeSwingMath.PowerFormat, firedPower * 100f));
            _valuePower.color = FreeSwingColors.ValueWhite;

            SetText(_valueImpact, FormatImpact(v.ImpactPx));
            _valueImpact.color = ImpactColor(v);

            SetText(_valuePath, LocalizationManager.Get(FreeSwingMath.PathKey(v.Path)));
            _valuePath.color = v.Path == FreeSwingPath.Straight
                ? FreeSwingColors.ValueGreen : FreeSwingColors.ValueAmber;

            SetText(_valueTempo, LocalizationManager.Get(FreeSwingMath.TempoKey(v.Tempo)));
            // GOOD is AMBER, which is the node's own token on this column (ValTEMPO #FFEBA6 on a
            // frame whose tempo reads GOOD) and not an oversight: amber is this game's "fine"
            // step — the Pendulum's GOOD band and SchemeGradePop's GOOD word are both #FFEBA6 —
            // and green is reserved for JUST/PURE-grade outcomes, which a tempo column has no
            // word for. Off-tempo drops to red, so worse still reads worse.
            _valueTempo.color = v.Tempo == FreeSwingTempo.Good
                ? FreeSwingColors.ValueAmber : FreeSwingColors.ValueRed;

            LastPowerText  = _valuePower  != null ? _valuePower.text  : null;
            LastImpactText = _valueImpact != null ? _valueImpact.text : null;
            LastPathKey    = FreeSwingMath.PathKey(v.Path);
            LastTempoKey   = FreeSwingMath.TempoKey(v.Tempo);

            if (_routine != null) StopCoroutine(_routine);

            // UP SYNCHRONOUSLY, then the routine only ever fades it OUT. Two reasons, and both
            // are about the chip being readable at all. A result readout that arrives after a
            // fast gesture should be legible on the frame it arrives, not a tenth of a second
            // later — and a disabled root (or any context without a running coroutine scheduler,
            // which includes every EditMode test) cannot run PlayRoutine, so an alpha that only
            // rose inside the routine would leave the chip invisible while every read-back
            // property happily reported the right words.
            Group.alpha = 1f;
            if (!isActiveAndEnabled) return;
            _routine = StartCoroutine(PlayRoutine());
        }

        /// <summary>
        /// The IMPACT column: an arrowhead pointing the way the club head crossed, and the miss
        /// in whole pixels. Rounded to the pixel because a reading of "2.7 px" invites a
        /// precision the gesture does not have.
        /// </summary>
        private static string FormatImpact(float impactPx)
        {
            float a = Mathf.Abs(impactPx);
            if (Mathf.RoundToInt(a) == 0)
                return string.Format(CultureInfo.InvariantCulture, FreeSwingMath.ImpactZeroFormat, 0f);
            string arrow = impactPx < 0f ? FreeSwingMath.ArrowLeft : FreeSwingMath.ArrowRight;
            return string.Format(CultureInfo.InvariantCulture, FreeSwingMath.ImpactFormat, arrow, a);
        }

        /// <summary>Green inside the drawn window, amber for a small miss, red once the miss is
        /// big enough to have fired a HOOK/SLICE pop — the same three-step ladder the pop uses,
        /// so the chip and the word over the ball never disagree.</summary>
        private static Color ImpactColor(in FreeSwingMath.Verdict v)
        {
            if (v.ImpactClean) return FreeSwingColors.ValueGreen;
            if (v.Grade == FreeSwingGrade.Hook || v.Grade == FreeSwingGrade.Slice)
                return FreeSwingColors.ValueRed;
            return FreeSwingColors.ValueAmber;
        }

        private static void SetText(TextMeshProUGUI t, string s)
        {
            if (t != null) t.text = s;
        }

        public void HideImmediate()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
        }

        private IEnumerator PlayRoutine()
        {
            Group.alpha = 1f;
            yield return new WaitForSeconds(_holdSeconds);

            for (float t = 0f; t < _fadeSeconds; t += Time.deltaTime)
            {
                Group.alpha = 1f - (t / Mathf.Max(_fadeSeconds, 1e-4f));
                yield return null;
            }

            _routine = null;
            HideImmediate();
        }

        // ── Read-back seams ─────────────────────────────────────────────────────
        // What the chip SAYS, so a test and the acceptance run can check the readout against the
        // committed intent without reading pixels — and so "zero hardcoded text" is checkable:
        // PATH and TEMPO are asserted as KEYS, never as words.

        public string LastPowerText  { get; private set; }
        public string LastImpactText { get; private set; }
        public string LastPathKey    { get; private set; }
        public string LastTempoKey   { get; private set; }
        public float  Alpha => Group.alpha;

        /// <summary>EditMode wiring seam — a plain MonoBehaviour gets no Awake in EditMode, and a
        /// chip with null texts answers null to every read-back, which reads as a passing
        /// assertion instead of a missing one.</summary>
        public void ConfigureForTests(CanvasGroup group,
                                      TextMeshProUGUI lPower, TextMeshProUGUI lImpact,
                                      TextMeshProUGUI lPath,  TextMeshProUGUI lTempo,
                                      TextMeshProUGUI vPower, TextMeshProUGUI vImpact,
                                      TextMeshProUGUI vPath,  TextMeshProUGUI vTempo)
        {
            _group = group;
            _labelPower = lPower; _labelImpact = lImpact; _labelPath = lPath; _labelTempo = lTempo;
            _valuePower = vPower; _valueImpact = vImpact; _valuePath = vPath; _valueTempo = vTempo;
        }
    }
}
