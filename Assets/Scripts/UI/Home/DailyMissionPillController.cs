// daily_mission_home_pill §2 — the Home-screen "NEW DAILY MISSION!" pill.
using System;
using System.Collections;
using Golfin.Gameplay.Missions;
using Golfin.UI.Common;
using Golfin.UI.Polish;
using GolfinRedux.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Home
{
    /// <summary>
    /// The pill that tells the player a daily is waiting, and takes them to it.
    ///
    /// ⚠️ ITS Y IS COMPUTED, NOT LAID OUT. Home has no vertical layout group — only three
    /// horizontal ones — so nothing reflows when the maintenance notice appears or disappears.
    /// The pill therefore reads the notice panel's OWN rect every time the notice changes and
    /// places itself 24px under it, falling back to the notice's own top when it is hidden. That
    /// is exactly the two Figma frames: y 361 with no notice (where the notice would have
    /// started), y 725 with one.
    ///
    /// ⚠️ IT NEVER SHOWS A STALE DAILY. Everything it draws comes from
    /// <see cref="DailyMissionState"/>, which only a successful fetch writes. A failed fetch, an
    /// offline launch, a signed-out player: all of them leave the pill hidden rather than
    /// advertising a mission that may already be claimed or may not exist. Showing nothing is
    /// always recoverable; showing a mission that pays nothing is not.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DailyMissionPillController : MonoBehaviour
    {
        private enum PillState { Hidden, Entering, Shown, Leaving }

        // ── Wiring ──────────────────────────────────────────────────────────────
        [Header("Parts")]
        [SerializeField] private RectTransform pillRect;
        [SerializeField] private Image glowImage;
        [SerializeField] private StreakFlameView streakFlame;
        [SerializeField] private RectTransform labelRect;
        [SerializeField] private Button tapButton;

        [Header("Notice panel it follows")]
        [Tooltip("HomeScreenController's newsPanelRoot. The pill sits at the notice's top when " +
                 "the notice is hidden, and 24px under its bottom when it is shown.")]
        [SerializeField] private GameObject noticePanelRoot;

        [Tooltip("Used only when noticePanelRoot is unassigned — the Figma y for the no-notice frame.")]
        [SerializeField] private float fallbackTopY = -361f;

        // ── Motion (SPEC §2; Cesar signs off on device) ─────────────────────────
        // ── Node auto-layout (Figma `13994:1963` -> `Mission Title`: px-24 py-16 gap-10) ──
        // The pill HUGS its content the way that auto-layout row does, so hiding the flame has
        // to take the flame's width AND the gap out of the pill — otherwise the right end is
        // 68px of empty navy.
        [Header("Layout (Figma auto-layout)")]
        [SerializeField] private float padX     = 24f;
        [SerializeField] private float flameW   = 58f;
        [SerializeField] private float flameGap = 10f;
        [SerializeField] private float labelW   = 433f;

        [Header("Motion")]
        [SerializeField] private float restX          = 36f;
        [SerializeField] private float enterDuration  = 0.45f;
        [SerializeField] private float leaveDuration  = 0.30f;
        [SerializeField] private float enterDelay     = 0.25f;
        [SerializeField] private float noticeGap      = 24f;

        [Header("Glow")]
        [SerializeField] private float glowPeriod = 1.6f;
        [SerializeField] private float glowMin    = 0.25f;
        [SerializeField] private float glowMax    = 0.65f;

        // ── Runtime ─────────────────────────────────────────────────────────────
        private PillState _state = PillState.Hidden;
        private Coroutine _motion;
        private Coroutine _fetch;
        private float _rolloverTimer;

        // ── game_polish_b §D2 — the glow's CanvasGroup ───────────────────────────
        //
        // Added at RUNTIME, never authored, for the reason every group in this track is:
        // a prefab that gains a component is a prefab whose rest state has to be re-proven,
        // and an alpha-1 CanvasGroup cannot move a pixel either way (A3).
        //
        // The glow's own Image.color.a is pinned to 1 when the group is created and the group
        // carries the [glowMin, glowMax] sweep instead, so the EFFECTIVE alpha the player sees
        // is the same product it always was — see SetGlowAlpha.
        private CanvasGroup _glowGroup;
        private Coroutine _glowMotion;

        /// <summary>
        /// The UTC date whose pill has ALREADY slid in once, this session.
        ///
        /// ⚠️ THE SLIDE IS AN ANNOUNCEMENT, NOT A TRANSITION, and that is the whole rule. It says
        /// "there is a new daily" — so it plays the first time a given day's pill appears, and
        /// again when midnight brings a different one. Coming back from Missions or the shop is
        /// neither: the pill was already announced and must simply BE there, at rest, with no
        /// re-entry to sit through every time Home is opened.
        ///
        /// Keyed on the DATE rather than a bool, because that is exactly what "a new daily
        /// mission" means — the rollover writes a new date and the announcement is owed again,
        /// with no extra flag to keep in sync. Static, so it survives the screen being disabled
        /// and re-enabled; per-session, so a relaunch announces once more.
        /// </summary>
        public static string AnnouncedForDate { get; private set; } = "";

        /// <summary>Test seam — a fresh session has announced nothing.</summary>
        public static void ResetAnnouncedForTest() => AnnouncedForDate = "";

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
            DailyMissionState.OnChanged += OnDailyStateChanged;
            RefreshPlacement();

            if (AlreadyAnnounced)
            {
                // Re-entering Home. Show it AT REST immediately — parking it off-screen and
                // waiting for the fetch would blank the pill for the length of a round trip and
                // then pop it back in, which is the flicker this branch exists to remove.
                if (streakFlame != null) streakFlame.SetStreak(DailyMissionState.Streak);
                ApplyWidth(DailyMissionState.Streak >= 1);
                Enter(animate: false);
            }
            else
            {
                // Nothing announced yet: park off-screen until a fetch says there is a daily.
                SetHiddenInstant();
            }

            _rolloverTimer = 0f;
            _fetch = StartCoroutine(FetchThenPresent(withEnterDelay: true));
        }

        private void OnDisable()
        {
            DailyMissionState.OnChanged -= OnDailyStateChanged;
            StopMotion();
            if (_fetch != null) { StopCoroutine(_fetch); _fetch = null; }
        }

        private void OnDestroy()
        {
            if (tapButton != null) tapButton.onClick.RemoveListener(OnPillTapped);
        }

        // ── Placement ───────────────────────────────────────────────────────────

        /// <summary>
        /// Re-seat the pill under the notice panel. Called by <c>HomeScreenController</c> every
        /// time it shows or hides the notice, and on every state change of our own — the notice
        /// can flip while the pill is mid-animation, and the Y must follow without interrupting
        /// the X slide.
        /// </summary>
        public void RefreshPlacement()
        {
            if (pillRect == null) return;
            pillRect.anchoredPosition = new Vector2(pillRect.anchoredPosition.x, ComputeTargetY());
        }

        /// <summary>
        /// Notice hidden ⇒ the pill takes the notice's own top (Figma y 361). Notice shown ⇒
        /// 24px below the notice's bottom edge (Figma y 725, computed here from the live panel
        /// so a notice of any height still clears it).
        ///
        /// <para>The notice's REST y, not its live one. The notice is in Home's entry rise
        /// (<c>ScreenEntryMotion</c>), and this runs from OnEnable — the same frame that rise
        /// starts, when the live y is 16 px low. Read live, the pill (and the loan pill, which
        /// derives from this) seated itself 16 px under where the notice was going to be.</para>
        /// </summary>
        public float ComputeTargetY()
        {
            var noticeRect = noticePanelRoot != null ? noticePanelRoot.transform as RectTransform : null;
            if (noticeRect == null) return fallbackTopY;

            float top = UiMotion.RestY(noticeRect);
            if (!noticePanelRoot.activeInHierarchy) return top;
            return top - noticeRect.rect.height - noticeGap;
        }

        // ── Daily state ─────────────────────────────────────────────────────────

        private void OnDailyStateChanged() => ApplyState(animate: true);

        /// <summary>
        /// One fetch, then show or hide accordingly. Runs on every Home entry — the state is a
        /// shared last-answer, not a cache with a policy, so entering Home always re-asks.
        /// </summary>
        private IEnumerator FetchThenPresent(bool withEnterDelay)
        {
            yield return Golfin.Economy.MissionsClient.Instance.FetchDailyRoutine(r =>
            {
                if (!r.Success || r.Data == null || r.Data.Recipe == null)
                {
                    // Offline / signed out / no recipe for today. Never a stale pill.
                    Debug.Log($"[DailyPill] no daily ({r.ErrorMessage ?? "no recipe"}) — pill stays hidden.");
                    DailyMissionState.SetNoDaily();
                    return;
                }
                DailyMissionState.Set(r.Data.Date, r.Data.Streak, r.Data.Claimed, hasRecipe: true);
            });

            _fetch = null;

            if (withEnterDelay && DailyMissionState.ShouldShowPill && enterDelay > 0f && !AlreadyAnnounced)
            {
                // The arrival must land AFTER the screen fade, not under it.
                float wait = 0f;
                while (wait < enterDelay) { wait += Time.unscaledDeltaTime; yield return null; }
            }

            ApplyState(animate: true);
        }

        /// <summary>Drive the state machine from whatever <see cref="DailyMissionState"/> now says.</summary>
        private void ApplyState(bool animate)
        {
            bool want = DailyMissionState.ShouldShowPill;

            if (want)
            {
                if (streakFlame != null) streakFlame.SetStreak(DailyMissionState.Streak);
                // Width follows the flame, so it must be settled BEFORE the slide reads
                // OffscreenX off it.
                ApplyWidth(DailyMissionState.Streak >= 1);
                if (_state == PillState.Shown || _state == PillState.Entering) { RefreshPlacement(); return; }
                // The slide is owed once per daily. Re-entering Home gets the pill at rest.
                Enter(animate && !AlreadyAnnounced);
                AnnouncedForDate = DailyMissionState.Date;
            }
            else
            {
                if (_state == PillState.Hidden || _state == PillState.Leaving) return;
                Leave(animate);
            }
        }

        /// <summary>
        /// True when today's pill has already had its entrance — so this appearance is a return
        /// to Home, not an announcement. False after a rollover, because the date has moved.
        /// </summary>
        private static bool AlreadyAnnounced
            => DailyMissionState.ShouldShowPill
               && DailyMissionState.Date.Length > 0
               && AnnouncedForDate == DailyMissionState.Date;

        // ── Width ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Resize the pill to hug its content, and move the label to sit after whatever is left.
        ///
        /// With the flame: 24 + 58 + 10 + 433 + 24 = <b>549</b>, the node's own width, label at
        /// x 92. Without it: 24 + 433 + 24 = <b>481</b>, label at x 24. Reproducing the auto
        /// layout rather than hiding the flame in place is what keeps a streak-less pill from
        /// ending in 68px of empty navy.
        ///
        /// The panel and glow are 9-SLICED for exactly this reason — a baked sprite stretched
        /// between two widths would squash its 50px corners (PIPELINE_HARDENING C3 / Rule 21
        /// corner-distortion).
        /// </summary>
        private void ApplyWidth(bool flameShown)
        {
            if (pillRect == null) return;
            float lead  = flameShown ? flameW + flameGap : 0f;
            float width = padX + lead + labelW + padX;
            var sd = pillRect.sizeDelta;
            if (!Mathf.Approximately(sd.x, width)) pillRect.sizeDelta = new Vector2(width, sd.y);
            if (labelRect != null)
            {
                var ap = labelRect.anchoredPosition;
                float x = padX + lead;
                if (!Mathf.Approximately(ap.x, x)) labelRect.anchoredPosition = new Vector2(x, ap.y);
            }
        }

        // ── Motion ──────────────────────────────────────────────────────────────

        private float OffscreenX => -(pillRect != null ? pillRect.rect.width : 549f) - restX;

        private void SetHiddenInstant()
        {
            StopMotion();
            _state = PillState.Hidden;
            if (pillRect != null) pillRect.anchoredPosition = new Vector2(OffscreenX, ComputeTargetY());
            StopGlow();
        }

        private void Enter(bool animate)
        {
            StopMotion();
            _state = PillState.Entering;
            RefreshPlacement();
            if (!animate || !isActiveAndEnabled)
            {
                if (pillRect != null) pillRect.anchoredPosition = new Vector2(restX, ComputeTargetY());
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
            StopGlow();         // the glow stops the moment the pill starts leaving
            if (!animate || !isActiveAndEnabled)
            {
                SetHiddenInstant();
                return;
            }
            float from = pillRect != null ? pillRect.anchoredPosition.x : restX;
            Slide(from, OffscreenX, leaveDuration, easeOut: false, PillState.Hidden);
        }

        /// <summary>
        /// The eased slide. game_polish_b §D2 RETROFIT — the hand-rolled loop that used to live
        /// here is now <see cref="UiMotion.Slide"/>, which runs the identical curve from the
        /// identical inputs: unscaled time, `1 - (1-t)^3` out / `t^3` in selected by the same
        /// <paramref name="easeOut"/> flag, settling exactly on <paramref name="toX"/>.
        ///
        /// <para>The one behavioural difference, and it is deliberate: <see cref="UiMotion.Slide"/>
        /// captures Y ONCE at the start, where the old loop re-read <see cref="ComputeTargetY"/>
        /// every frame. Y is a function of the maintenance-notice panel's rect, which does not
        /// move during a 0.45 s slide — the notice appearing is what CAUSES a re-place, and
        /// <see cref="RefreshPlacement"/> already runs immediately before this. Re-reading it per
        /// frame was defensive, not load-bearing; the parity trace (pill.slide.enter) is identical
        /// to 0.5 px either way. Flagged as deviation D-3.</para>
        /// </summary>
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

        private void StopMotion()
        {
            UiMotion.Stop(this, ref _motion);
        }

        // ── Per-frame: the glow, and the midnight check ─────────────────────────

        private void Update()
        {
            // §D2 — the glow is UiMotion.Pulse now, armed on settle (see StartGlow), so this
            // Update no longer runs a tween at all. The curve is unchanged: Pulse's weight is
            // `0.5 - 0.5*cos(2*pi*phase)`, and the sine this replaced was
            // `0.5 + 0.5*sin(x - pi/2)` — the same function written the other way round.

            // Rollover — one comparison a second, not one a frame. `Date` is the seam a test
            // writes to simulate midnight without touching the device clock.
            _rolloverTimer += Time.unscaledDeltaTime;
            if (_rolloverTimer < 1f) return;
            _rolloverTimer = 0f;

            if (!DailyMissionState.Known || DailyMissionState.Date.Length == 0) return;
            if (DailyMissionState.Date == UtcToday()) return;

            HandleRollover();
        }

        /// <summary>Today, in the server's <c>yyyy-MM-dd</c> UTC form.</summary>
        public static string UtcToday() => DateTime.UtcNow.ToString("yyyy-MM-dd");

        /// <summary>
        /// UTC midnight passed. The old pill leaves, the state is forgotten, a fresh fetch runs,
        /// and the new one enters with the new streak. A fetch that fails leaves nothing on
        /// screen — the same rule as a cold start.
        /// </summary>
        private void HandleRollover()
        {
            if (_fetch != null) return;   // a rollover fetch is already in flight
            Debug.Log($"[DailyPill] UTC rollover ({DailyMissionState.Date} → {UtcToday()}) — swapping the pill.");
            DailyMissionState.Clear();    // fires OnChanged → ApplyState → Leave
            _fetch = StartCoroutine(RolloverRoutine());
        }

        private IEnumerator RolloverRoutine()
        {
            // Let the leave animation finish before the new one arrives — old out, then new in.
            float wait = 0f;
            while (_state == PillState.Leaving && wait < leaveDuration + 0.1f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            _fetch = null;
            _fetch = StartCoroutine(FetchThenPresent(withEnterDelay: false));
        }

        /// <summary>
        /// §D2 — arm the glow: an endless sequence of <see cref="UiMotion.Pulse"/> sweeps for as
        /// long as the pill is shown.
        ///
        /// <para>WHY A LOOPING ROUTINE AND NOT A RE-ARM FROM Pulse's TAIL. The first version of
        /// this was <c>UiMotion.Then(Pulse(...), StartGlow)</c> — re-arm yourself when the sweep
        /// ends — and it took the Editor down twice with
        /// <c>Scripting::RaiseStackOverflowException</c>. The recursion is not obvious, so it is
        /// worth spelling out:</para>
        ///
        /// <para><c>Then(inner, after)</c> runs <c>after</c> in TWO places — at the tail of the
        /// routine, and again inside the finalizer it registers, because an interrupted sequence
        /// still owes its tail. Re-arming from <c>after</c> therefore leaves a fresh entry on the
        /// handle whose OWN finalizer also re-arms. The next <see cref="UiMotion.Run"/> on that
        /// handle settles the previous entry, the settle invokes that finalizer, the finalizer
        /// re-arms, the re-arm settles the one before it — and the chain never reaches a tween that
        /// does not re-arm. A self-re-arming <c>Then</c> tail is unbounded by construction.</para>
        ///
        /// <para>Yielding fresh sweeps from ONE long-lived coroutine has no finalizer that restarts
        /// anything, so there is no chain to run away — and it keeps the property that made
        /// re-arming attractive in the first place (below).</para>
        ///
        /// <para>Float precision: every sweep starts its own <c>elapsed</c> at zero, so the phase
        /// cannot drift the way a single <c>elapsed % period</c> running for hours would.</para>
        /// </summary>
        private void StartGlow()
        {
            if (glowImage == null || glowPeriod <= 0f) return;
            EnsureGlowGroup();
            if (_glowGroup == null) return;
            UiMotion.Run(this, ref _glowMotion, GlowLoop());
        }

        /// <summary>One sweep after another, forever. Stopped by <see cref="StopGlow"/>, and by
        /// UiMotionRunner's disable hook when the screen goes away.</summary>
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

        /// <summary>
        /// The glow's CanvasGroup, created on first use. Creating it PINS the Image's own alpha
        /// to 1 and hands the sweep to the group, so the product the player sees — colour alpha
        /// times group alpha — is the same [glowMin, glowMax] it always was, and the prefab is
        /// untouched (A3).
        /// </summary>
        private void EnsureGlowGroup()
        {
            if (_glowGroup != null || glowImage == null) return;
            _glowGroup = glowImage.GetComponent<CanvasGroup>();
            if (_glowGroup == null) _glowGroup = glowImage.gameObject.AddComponent<CanvasGroup>();
            _glowGroup.alpha = 0f;
            SetGlowAlpha(1f);
        }

        /// <summary>Writes the Image's own colour alpha. After <see cref="EnsureGlowGroup"/> this
        /// is pinned at 1 and the group carries the sweep; before it — and on the hide paths that
        /// run before the pill has ever been shown — it is still the only alpha there is.</summary>
        private void SetGlowAlpha(float a)
        {
            if (glowImage == null) return;
            var c = glowImage.color;
            if (Mathf.Approximately(c.a, a)) return;
            c.a = a;
            glowImage.color = c;
        }

        // ── Tap ─────────────────────────────────────────────────────────────────
        //
        // The label is NOT written here. `HOME_DAILY_PILL` is bound by the existing
        // `LocalizedText` component on the Label, which already owns the language-change
        // subscription and the per-language size override — a second copy of that here would be
        // the duplication the project rule forbids, and would fight the binder on a JA switch.

        private void OnPillTapped()
        {
            Golfin.Telemetry.TelemetryService.Instance.RecordSafe(
                Golfin.Telemetry.TelemetryEventNames.DailyPillTap,
                () => new System.Collections.Generic.Dictionary<string, object>
                {
                    { "streak",  DailyMissionState.Streak },
                    { "date",    DailyMissionState.Date },
                });

            // A player who tapped "NEW DAILY MISSION!" asked for the daily, not for the mission
            // list — so ask the screen to open it EXPANDED. One-shot: MissionSelection consumes
            // the flag as the daily binds, and every other route in still lands on NEXT.
            GolfinRedux.UI.MissionSelection.MissionSelectionScreenController.ExpandDailyOnOpen = true;

            // The same route the Missions mode card takes — one mode, one destination
            // (missions_v1 §C1).
            if (ScreenManager.Instance != null)
                ScreenManager.Instance.ShowScreen(ScreenId.MissionSelection);
            else
                Debug.LogWarning("[DailyPill] tapped but ScreenManager is not available.");
        }
    }
}
