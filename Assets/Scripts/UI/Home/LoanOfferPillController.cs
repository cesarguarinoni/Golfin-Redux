// asset_loans_offers §3.3 — the Home-screen "LOAN OFFER FROM {0}" pill (Figma 14261:32997,
// pill 14261:33108).
//
// A SIBLING OF DailyMissionPillController, and deliberately a separate type rather than a second
// instance of it: the two share their MOTION (the same slide, the same glow, the same computed-Y
// placement, all of which live in UiMotion and are reused verbatim below) but nothing else. The
// daily pill's state machine is about a DATE — announce once per day, re-announce at UTC rollover,
// hide when claimed — and this one's is about a LIST that can be empty, hold one offer, or hold
// three. Making one class serve both would be two state machines behind a flag.
#nullable enable
using System.Collections;
using System.Collections.Generic;
using Golfin.Social;
using Golfin.UI.Loans;
using Golfin.UI.Polish;
using GolfinRedux.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Home
{
    /// <summary>
    /// The pill that tells the player somebody wants to lend them something, and opens the offer.
    ///
    /// ⚠️ ITS Y IS COMPUTED, NOT LAID OUT — the same constraint as the daily pill, and it stacks
    /// UNDER it: Home has no vertical layout group, so nothing reflows when either pill appears or
    /// disappears. This one seats itself 40px below the daily pill when the daily pill is showing
    /// and takes the daily pill's own slot when it is not, so the column reads as one stack in
    /// both of the Figma frames.
    ///
    /// ⚠️ IT NEVER SHOWS A STALE OFFER. Everything it draws comes from
    /// <see cref="LoanService.OffersIn"/>, which only a successful refresh writes. Offline, signed
    /// out, or with the points backend off: the list is empty and the pill stays hidden. Showing
    /// nothing is always recoverable; showing an offer that was rescinded an hour ago sends the
    /// player into a modal that can only fail.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoanOfferPillController : MonoBehaviour
    {
        private enum PillState { Hidden, Entering, Shown, Leaving }

        // ── Wiring ──────────────────────────────────────────────────────────────
        [Header("Parts")]
        [SerializeField] private RectTransform? pillRect;
        [SerializeField] private Image? glowImage;
        [SerializeField] private Image? iconImage;
        [SerializeField] private RectTransform? labelRect;
        [SerializeField] private TextMeshProUGUI? labelText;
        [SerializeField] private Button? tapButton;

        [Header("The pill it stacks under")]
        [Tooltip("The daily-mission pill. This pill sits 40px below it when it is showing, and " +
                 "takes its slot when it is not.")]
        [SerializeField] private DailyMissionPillController? dailyPill;

        [Tooltip("Used only when dailyPill is unassigned — the Figma y for the no-daily frame.")]
        [SerializeField] private float fallbackTopY = -361f;

        [Header("The modal it opens")]
        [SerializeField] private LoanOfferModalController? offerModal;

        // ── Node auto-layout (Figma `14261:33108`: px-24 py-16 gap-10, icon 56) ──
        // The pill HUGS its content exactly as the daily one does, so the width follows the
        // label. Reproducing the auto layout rather than pinning a width is what keeps a short
        // name ("LOAN OFFER FROM KO") from ending in 200px of empty navy.
        [Header("Layout (Figma auto-layout)")]
        [SerializeField] private float padX    = 24f;
        [SerializeField] private float iconW   = 56f;
        [SerializeField] private float iconGap = 10f;
        [SerializeField] private float maxLabelW = 433f;

        // ── Motion — the daily pill's feel values, verbatim (SPEC § Polish atoms) ──
        [Header("Motion")]
        [SerializeField] private float restX         = 36f;
        [SerializeField] private float enterDuration = 0.45f;
        [SerializeField] private float leaveDuration = 0.30f;
        [SerializeField] private float enterDelay    = 0.25f;

        /// <summary>Gap between the daily pill's bottom and this one's top (Figma: 40).</summary>
        [SerializeField] private float pillGap = 40f;

        /// <summary>The pill's own height (Figma: 122), used to place this one under the daily.</summary>
        [SerializeField] private float pillHeight = 122f;

        [Header("Glow")]
        [SerializeField] private float glowPeriod = 1.6f;
        [SerializeField] private float glowMin    = 0.25f;
        [SerializeField] private float glowMax    = 0.65f;

        // ── Runtime ─────────────────────────────────────────────────────────────
        private PillState _state = PillState.Hidden;
        private Coroutine? _motion;
        private Coroutine? _enter;
        private CanvasGroup? _glowGroup;
        private Coroutine? _glowMotion;

        /// <summary>
        /// The offer ids this pill has already slid in for, this session.
        ///
        /// ⚠️ THE SLIDE IS AN ANNOUNCEMENT, NOT A TRANSITION — the daily pill's rule, and it
        /// applies here for the same reason. It says "something new arrived", so it plays the
        /// first time a given offer appears and again when a DIFFERENT offer does. Coming back
        /// from the Roster is neither: the offer was already announced and must simply BE there,
        /// at rest, rather than re-entering on every Home entry.
        ///
        /// <para>Keyed on the SET of pending ids rather than a bool, because that is exactly what
        /// "a new offer" means: a second offer arriving while the first is still pending is worth
        /// announcing, and answering one of two is not.</para>
        /// </summary>
        private static readonly HashSet<string> AnnouncedOfferIds =
            new HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>Test seam — a fresh session has announced nothing.</summary>
        public static void ResetAnnouncedForTest() => AnnouncedOfferIds.Clear();

        /// <summary>Exposed for the placement test — no reflection needed to assert the Y rule.</summary>
        public float CurrentY => pillRect != null ? pillRect.anchoredPosition.y : 0f;

        /// <summary>Exposed for tests / review: is the pill on screen right now?</summary>
        public bool IsShowing => _state == PillState.Shown || _state == PillState.Entering;

        private void Reset()
        {
            pillRect = GetComponent<RectTransform>();
            tapButton = GetComponent<Button>();
        }

        private void Awake()
        {
            if (pillRect == null) pillRect = GetComponent<RectTransform>();
            if (tapButton == null) tapButton = GetComponent<Button>();
            if (tapButton != null) tapButton.onClick.AddListener(OnPillTapped);
        }

        private void OnEnable()
        {
            LoanService.Instance.OnLoansChanged += OnLoansChanged;
            RefreshPlacement();

            // The refresh itself is LoanSyncBehaviour's job (Home is one of its screen-entry
            // triggers as of this task) — this pill only paints what the last one produced. Two
            // fetchers for one list would race and double the request on every Home entry.
            if (AlreadyAnnounced) ApplyState(animate: false);
            else                  SetHiddenInstant();

            _enter = StartCoroutine(PresentAfterDelay());
        }

        private void OnDisable()
        {
            LoanService.Instance.OnLoansChanged -= OnLoansChanged;
            StopMotion();
            StopGlow();
            if (_enter != null) { StopCoroutine(_enter); _enter = null; }
        }

        private void OnDestroy()
        {
            if (tapButton != null) tapButton.onClick.RemoveListener(OnPillTapped);
        }

        // ── Placement ───────────────────────────────────────────────────────────

        /// <summary>
        /// Re-seat the pill under the daily one. Called by <c>HomeScreenController</c> whenever it
        /// shows or hides the notice (which moves the daily pill, which moves this one), and on
        /// every state change of our own.
        /// </summary>
        public void RefreshPlacement()
        {
            if (pillRect == null) return;
            pillRect.anchoredPosition = new Vector2(pillRect.anchoredPosition.x, ComputeTargetY());
        }

        /// <summary>
        /// Daily pill showing ⇒ 40px below its bottom edge (its y − 122 − 40). Daily pill hidden
        /// ⇒ this pill takes the daily's own slot, so a player with an offer and no daily sees
        /// one pill in the place a pill belongs rather than a gap and then a pill.
        ///
        /// <para>Computed from the daily pill's LIVE y rather than from a constant, so the
        /// maintenance notice moving the daily pill moves this one by the same amount without
        /// either of them knowing the notice exists.</para>
        /// </summary>
        public float ComputeTargetY()
        {
            if (dailyPill == null) return fallbackTopY;

            float dailyY = dailyPill.ComputeTargetY();
            if (!dailyPill.IsShowing) return dailyY;
            return dailyY - pillHeight - pillGap;
        }

        // ── Offer state ─────────────────────────────────────────────────────────

        private void OnLoansChanged() => ApplyState(animate: true);

        /// <summary>One frame of delay so the arrival lands AFTER the screen fade, not under it —
        /// the daily pill's `enterDelay`, reused verbatim.</summary>
        private IEnumerator PresentAfterDelay()
        {
            if (enterDelay > 0f && !AlreadyAnnounced)
            {
                float wait = 0f;
                while (wait < enterDelay) { wait += Time.unscaledDeltaTime; yield return null; }
            }
            _enter = null;
            ApplyState(animate: true);
        }

        /// <summary>Drive the state machine from whatever <see cref="LoanService.OffersIn"/> now says.</summary>
        private void ApplyState(bool animate)
        {
            IReadOnlyList<LoanDto> offers = LoanService.Instance.OffersIn;
            bool want = offers.Count > 0;

            if (want)
            {
                ApplyLabel(offers);
                if (_state == PillState.Shown || _state == PillState.Entering)
                {
                    RefreshPlacement();
                    NoteAnnounced(offers);
                    return;
                }
                // The slide is owed once per NEW offer. Re-entering Home gets the pill at rest.
                Enter(animate && !AlreadyAnnounced);
                NoteAnnounced(offers);
            }
            else
            {
                if (_state == PillState.Hidden || _state == PillState.Leaving) return;
                Leave(animate);
            }
        }

        /// <summary>
        /// "LOAN OFFER FROM KENJI" for one, "2 LOAN OFFERS" for more.
        ///
        /// <para>The many-form drops the name deliberately: with two lenders there is no single
        /// name to show, and picking one would tell the player who the pill is about when tapping
        /// it opens the OTHER one's offer half the time.</para>
        /// </summary>
        private void ApplyLabel(IReadOnlyList<LoanDto> offers)
        {
            if (labelText == null) return;

            if (offers.Count > 1)
            {
                labelText.text = string.Format(
                    LocalizationManager.Get("LOAN_PILL_MANY_FMT"), offers.Count);
            }
            else
            {
                LoanDto? newest = LoanService.Instance.NewestOfferIn();
                string lender = newest?.Lender != null ? newest.Lender.Name : LoanPartyDto.Fallback;
                labelText.text = string.Format(
                    LocalizationManager.Get("LOAN_PILL_FMT"), lender.ToUpperInvariant());
            }

            ApplyWidth();
        }

        /// <summary>
        /// Resize the pill to hug its label, capped at the node's own 433.
        ///
        /// <para>The panel and glow are 9-SLICED for exactly this reason — a baked sprite
        /// stretched between two widths would squash its corners (PIPELINE_HARDENING C3 / the
        /// linter's corner-distortion check).</para>
        /// </summary>
        private void ApplyWidth()
        {
            if (pillRect == null || labelText == null) return;

            labelText.ForceMeshUpdate();
            float wanted = Mathf.Min(maxLabelW, Mathf.Ceil(labelText.preferredWidth));
            if (wanted <= 0f) wanted = maxLabelW;

            if (labelRect != null)
            {
                var ls = labelRect.sizeDelta;
                if (!Mathf.Approximately(ls.x, wanted))
                    labelRect.sizeDelta = new Vector2(wanted, ls.y);
                var lp = labelRect.anchoredPosition;
                float lx = padX + iconW + iconGap;
                if (!Mathf.Approximately(lp.x, lx))
                    labelRect.anchoredPosition = new Vector2(lx, lp.y);
            }

            float width = padX + iconW + iconGap + wanted + padX;
            var sd = pillRect.sizeDelta;
            if (!Mathf.Approximately(sd.x, width)) pillRect.sizeDelta = new Vector2(width, sd.y);
        }

        /// <summary>True when every pending offer has already had its entrance — so this
        /// appearance is a return to Home, not an announcement.</summary>
        private bool AlreadyAnnounced
        {
            get
            {
                IReadOnlyList<LoanDto> offers = LoanService.Instance.OffersIn;
                if (offers.Count == 0) return false;
                foreach (LoanDto l in offers)
                    if (l != null && !string.IsNullOrEmpty(l.Id)
                        && !AnnouncedOfferIds.Contains(l.Id)) return false;
                return true;
            }
        }

        private static void NoteAnnounced(IReadOnlyList<LoanDto> offers)
        {
            foreach (LoanDto l in offers)
                if (l != null && !string.IsNullOrEmpty(l.Id)) AnnouncedOfferIds.Add(l.Id);
        }

        // ── Motion — the daily pill's, verbatim ─────────────────────────────────

        private float OffscreenX => -(pillRect != null ? pillRect.rect.width : 549f) - restX;

        private void SetHiddenInstant()
        {
            StopMotion();
            _state = PillState.Hidden;
            if (pillRect != null)
                pillRect.anchoredPosition = new Vector2(OffscreenX, ComputeTargetY());
            StopGlow();
        }

        private void Enter(bool animate)
        {
            StopMotion();
            _state = PillState.Entering;
            RefreshPlacement();
            if (!animate || !isActiveAndEnabled)
            {
                if (pillRect != null)
                    pillRect.anchoredPosition = new Vector2(restX, ComputeTargetY());
                _state = PillState.Shown;
                StartGlow();
                return;
            }
            Slide(OffscreenX, restX, enterDuration, easeOut: true, PillState.Shown);
        }

        private void Leave(bool animate)
        {
            StopMotion();
            _state = PillState.Leaving;
            StopGlow();
            if (!animate || !isActiveAndEnabled) { SetHiddenInstant(); return; }
            float from = pillRect != null ? pillRect.anchoredPosition.x : restX;
            Slide(from, OffscreenX, leaveDuration, easeOut: false, PillState.Hidden);
        }

        private void Slide(float fromX, float toX, float duration, bool easeOut, PillState settleAs)
        {
            if (pillRect == null) return;

            pillRect.anchoredPosition = new Vector2(fromX, ComputeTargetY());

            UiMotion.Run(this, ref _motion, UiMotion.Then(
                UiMotion.Slide(pillRect, fromX, toX, duration, easeOut),
                () =>
                {
                    _state  = settleAs;
                    _motion = null;
                    if (settleAs == PillState.Shown) StartGlow();
                    else                             StopGlow();
                }));
        }

        private void StopMotion() => UiMotion.Stop(this, ref _motion);

        // ── Glow ────────────────────────────────────────────────────────────────
        //
        // ⚠️ A LOOPING ROUTINE, NOT A RE-ARM FROM Pulse'S TAIL. `UiMotion.Then(inner, after)` runs
        // `after` twice — at the tail AND in the finalizer an interrupted sequence still owes — so
        // re-arming from `after` is unbounded recursion and takes the Editor down with
        // RaiseStackOverflowException. The daily pill learned this the hard way; the shape is
        // copied along with the feel values. (DailyMissionPillController.StartGlow has the full
        // post-mortem.)

        private void StartGlow()
        {
            if (glowImage == null || glowPeriod <= 0f) return;
            EnsureGlowGroup();
            if (_glowGroup == null) return;
            UiMotion.Run(this, ref _glowMotion, GlowLoop());
        }

        private IEnumerator GlowLoop()
        {
            while (true)
            {
                IEnumerator sweep = UiMotion.Pulse(_glowGroup, glowMin, glowMax, 1, glowPeriod);
                while (sweep.MoveNext()) yield return sweep.Current;
            }
        }

        private void StopGlow()
        {
            UiMotion.Stop(this, ref _glowMotion);
            if (_glowGroup != null) _glowGroup.alpha = 0f;
            else                    SetGlowAlpha(0f);
        }

        /// <summary>The glow's CanvasGroup, created on first use. Creating it PINS the Image's own
        /// alpha to 1 and hands the sweep to the group, so the product the player sees is the same
        /// [glowMin, glowMax] it always was and the prefab is untouched.</summary>
        private void EnsureGlowGroup()
        {
            if (_glowGroup != null || glowImage == null) return;
            _glowGroup = glowImage.GetComponent<CanvasGroup>();
            if (_glowGroup == null) _glowGroup = glowImage.gameObject.AddComponent<CanvasGroup>();
            _glowGroup.alpha = 0f;
            SetGlowAlpha(1f);
        }

        private void SetGlowAlpha(float a)
        {
            if (glowImage == null) return;
            var c = glowImage.color;
            if (Mathf.Approximately(c.a, a)) return;
            c.a = a;
            glowImage.color = c;
        }

        // ── Tap ─────────────────────────────────────────────────────────────────

        private void OnPillTapped()
        {
            LoanDto? offer = LoanService.Instance.NewestOfferIn();
            if (offer == null)
            {
                // The list emptied between the paint and the tap — a rescind, or an expiry. Say
                // nothing and let the pill leave on the next state change; a toast about an offer
                // the player never opened would be noise about a thing they never saw.
                ApplyState(animate: true);
                return;
            }

            Golfin.Telemetry.TelemetryService.Instance?.RecordSafe("loan_pill_open",
                () => new Dictionary<string, object>
                {
                    { "pending", LoanService.Instance.OffersIn.Count },
                    { "kind", offer.Kind },
                });

            if (offerModal != null) offerModal.Open(offer);
            else Debug.LogWarning("[LoanOfferPill] tapped but no offer modal is wired.");
        }
    }
}
