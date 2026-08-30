// daily_mission_home_pill §2 — the Home-screen "NEW DAILY MISSION!" pill.
using System;
using System.Collections;
using Golfin.Gameplay.Missions;
using Golfin.UI.Common;
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
        [SerializeField] private Button tapButton;

        [Header("Notice panel it follows")]
        [Tooltip("HomeScreenController's newsPanelRoot. The pill sits at the notice's top when " +
                 "the notice is hidden, and 24px under its bottom when it is shown.")]
        [SerializeField] private GameObject noticePanelRoot;

        [Tooltip("Used only when noticePanelRoot is unassigned — the Figma y for the no-notice frame.")]
        [SerializeField] private float fallbackTopY = -361f;

        // ── Motion (SPEC §2; Cesar signs off on device) ─────────────────────────
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
        private float _glowPhase;

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

            // Park off-screen and hidden until a fetch says otherwise. Home can be re-entered
            // many times a session; each entry re-plays the arrival, which is the intent.
            SetHiddenInstant();

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
        /// </summary>
        public float ComputeTargetY()
        {
            var noticeRect = noticePanelRoot != null ? noticePanelRoot.transform as RectTransform : null;
            if (noticeRect == null) return fallbackTopY;

            float top = noticeRect.anchoredPosition.y;
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

            if (withEnterDelay && DailyMissionState.ShouldShowPill && enterDelay > 0f)
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
                if (_state == PillState.Shown || _state == PillState.Entering) { RefreshPlacement(); return; }
                Enter(animate);
            }
            else
            {
                if (_state == PillState.Hidden || _state == PillState.Leaving) return;
                Leave(animate);
            }
        }

        // ── Motion ──────────────────────────────────────────────────────────────

        private float OffscreenX => -(pillRect != null ? pillRect.rect.width : 549f) - restX;

        private void SetHiddenInstant()
        {
            StopMotion();
            _state = PillState.Hidden;
            if (pillRect != null) pillRect.anchoredPosition = new Vector2(OffscreenX, ComputeTargetY());
            SetGlowAlpha(0f);
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
                _glowPhase = 0f;
                return;
            }
            _motion = StartCoroutine(SlideRoutine(OffscreenX, restX, enterDuration, easeOut: true, PillState.Shown));
        }

        private void Leave(bool animate)
        {
            StopMotion();
            _state = PillState.Leaving;
            SetGlowAlpha(0f);   // the glow stops the moment the pill starts leaving
            if (!animate || !isActiveAndEnabled)
            {
                SetHiddenInstant();
                return;
            }
            float from = pillRect != null ? pillRect.anchoredPosition.x : restX;
            _motion = StartCoroutine(SlideRoutine(from, OffscreenX, leaveDuration, easeOut: false, PillState.Hidden));
        }

        /// <summary>
        /// The eased slide, in the shape <c>ModeCarouselController.LerpToTargetLayout</c> uses —
        /// unscaled time, cubic ease, settle exactly on target. No tween library in this project,
        /// and no per-frame allocation: <c>yield return null</c> and a struct assignment.
        /// </summary>
        private IEnumerator SlideRoutine(float fromX, float toX, float duration, bool easeOut, PillState settleAs)
        {
            if (pillRect == null) yield break;

            _glowPhase = 0f;
            pillRect.anchoredPosition = new Vector2(fromX, ComputeTargetY());

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(duration <= 0f ? 1f : elapsed / duration);
                float e = easeOut
                    ? 1f - (1f - t) * (1f - t) * (1f - t)   // cubic ease-out
                    : t * t * t;                            // cubic ease-in
                pillRect.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, e), ComputeTargetY());
                yield return null;
            }

            pillRect.anchoredPosition = new Vector2(toX, ComputeTargetY());
            _state = settleAs;
            _motion = null;
            if (settleAs != PillState.Shown) SetGlowAlpha(0f);
        }

        private void StopMotion()
        {
            if (_motion != null) { StopCoroutine(_motion); _motion = null; }
        }

        // ── Per-frame: the glow, and the midnight check ─────────────────────────

        private void Update()
        {
            // Glow — only while settled, so the slide is never fighting a pulse. Sin + a struct
            // assignment: nothing here allocates, which is what FramePacingBootstrap needs.
            if (_state == PillState.Shown && glowPeriod > 0f)
            {
                _glowPhase += Time.unscaledDeltaTime;
                float s = 0.5f + 0.5f * Mathf.Sin(_glowPhase * (2f * Mathf.PI / glowPeriod) - Mathf.PI * 0.5f);
                SetGlowAlpha(Mathf.Lerp(glowMin, glowMax, s));
            }

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

            // The same route the Missions mode card takes — one mode, one destination
            // (missions_v1 §C1). The daily card there is expanded by RefreshDaily.
            if (ScreenManager.Instance != null)
                ScreenManager.Instance.ShowScreen(ScreenId.MissionSelection);
            else
                Debug.LogWarning("[DailyPill] tapped but ScreenManager is not available.");
        }
    }
}
