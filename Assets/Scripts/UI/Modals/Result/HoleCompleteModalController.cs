using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Golfin.UI.Modals;
using Golfin.UI.Toast;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.UI.GameplayTransition;
using Golfin.Roster;
using GolfinRedux.UI;

namespace Golfin.UI.Modals.Result
{
    /// <summary>
    /// Stage C1 — ShellScene-resident end-of-hole result modal controller.
    ///
    /// Iteration 6: Reuses the full two-card lab HoleCompleteWidget as the VIEW.
    /// This controller is the PRODUCTION BEHAVIOR LAYER only:
    ///   - Subscribes to GameSession.OnHoleComplete (OnEnable / OnDisable).
    ///   - Assembles a HoleCompleteData from HoleCompletionData + HoleDatabaseLoader.
    ///   - Calls _widget.Show(data, ...) and then hooks the specific card buttons for
    ///     production routing (REPLAY → reload same hole; RETRY → reload same hole;
    ///     PLAY → next hole load + progression write; Hole 18 → COURSE CLEARED toast).
    ///   - Grants rewards on SUCCESS.
    ///   - Writes hole progression on PLAY NEXT or REPLAY (SUCCESS only). RETRY (FAILED)
    ///     reloads the same hole without any progression or reward writes.
    ///
    /// The VIEW (HoleCompleteWidget) is the unmodified lab widget with Card 1 (current
    /// hole) + Card 2 (next hole). Card 2 is LOCKED when FAILED and next hole was never
    /// unlocked. Card 2 is HIDDEN (SetActive false) when hole == 18.
    ///
    /// Canvas z-order: child Canvas with overrideSorting=true, sortingOrder=900.
    /// </summary>
    public class HoleCompleteModalController : ModalController
    {
        // ── Full two-card lab widget (the VIEW) ───────────────────────────────

        [Header("Two-card lab widget (VIEW — set to HoleCompleteWidget GO in ShellScene)")]
        [SerializeField] HoleCompleteWidget _widget;

        // ── Locked defaults (SPEC §4) ─────────────────────────────────────────

        const string REPAIR_KIT_DEFAULT_ID = "repairkit_common";
        const string BALL_DEFAULT_ID       = "ball_golfin";

        // ── Internal state ────────────────────────────────────────────────────

        IHoleProgressionStore _progression;
        HoleCompletionData    _lastSessionData;   // lightweight session payload
        bool                  _lastSuccess;
        bool                  _wasReplay;
        bool                  _rewardsGranted;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();

            // Ensure this GO's Canvas sorts above gameplay (900) and below LoadingScreen (1000).
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder    = 900;
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            _progression = HoleProgressionStoreAdapter.Default;
        }

        /// <summary>
        /// Override Show(): delegate to the widget which manages its own root visibility.
        /// Base class Show() is skipped to avoid the _isVisible guard and backdrop logic
        /// (the widget manages its own dim background via _dimBackground).
        /// </summary>
        public override void Show()
        {
            // Widget manages visibility — no base.Show() call.
            // (We still call Show() from HandleHoleComplete after widget.Show() above;
            // override makes ModalController.Show() a no-op here.)
        }

        /// <summary>
        /// Override Hide(): delegate to the widget. Called by GameplaySceneLoader
        /// at fade midpoint when modalToHideOnMidpoint=this is passed.
        /// </summary>
        public override void Hide()
        {
            if (_widget != null) _widget.Hide();
        }

        void OnEnable()  => GameSession.OnHoleComplete += HandleHoleComplete;
        void OnDisable() => GameSession.OnHoleComplete -= HandleHoleComplete;

        // ── Event handler ─────────────────────────────────────────────────────

        void HandleHoleComplete(HoleCompletionData sessionData)
        {
            _lastSessionData = sessionData;
            _lastSuccess     = sessionData.TerminalState == BallState.InCup;
            _wasReplay       = _progression.HasPlayed(sessionData.HoleNumber);
            _rewardsGranted  = false;

            if (_widget == null)
            {
                Debug.LogWarning("[HoleCompleteModalController] _widget is null — wire HoleCompleteWidget in Inspector.");
                return;
            }

            // Build the UI payload (HoleCompleteData) for the lab widget.
            HoleCompleteData uiData = BuildUIData(sessionData);

            // Determine Card 2 locked state.
            bool isHole18 = sessionData.HoleNumber == 18;
            int  nextHole = sessionData.HoleNumber + 1;
            bool card2Locked = !_lastSuccess && !_progression.IsUnlocked(nextHole);

            // Show the full two-card widget.
            _widget.Show(uiData, () => { /* close handled per-button below */ });

            // Hole 18: hide Card 2 entirely — there is no Hole 19.
            if (isHole18 && _widget.Card2 != null)
                _widget.Card2.gameObject.SetActive(false);

            // Hook production-specific button actions ON TOP of the widget's internal hooks.
            // BindCurrentHole/BindNextHole already wired a close-callback via HookButton
            // which calls RemoveAllListeners + adds _onButtonTap. We AddListener AFTER
            // so our actions fire in addition to (not instead of) the widget close callback.
            if (_widget.Card1 != null)
            {
                _widget.Card1.AddReplayListener(OnReplay);
                _widget.Card1.AddRetryListener(OnRetry);
            }
            if (_widget.Card2 != null && !isHole18)
                _widget.Card2.AddPlayListener(() => OnPlayNext(nextHole));

            // Hole 18 SUCCESS: fire COURSE CLEARED toast.
            if (isHole18 && _lastSuccess)
            {
                if (ToastController.Instance != null)
                    ToastController.Instance.Show("COURSE CLEARED!", 3f);
            }
        }

        // ── Assemble HoleCompleteData from session payload + HoleDatabaseLoader ──

        HoleCompleteData BuildUIData(HoleCompletionData sessionData)
        {
            int holeNumber = sessionData.HoleNumber;
            int nextHole   = holeNumber + 1;

            HoleData currentHoleData = TryGetHoleData(holeNumber);
            HoleData nextHoleData    = TryGetHoleData(nextHole);

            int par = currentHoleData != null ? currentHoleData.par : HoleContext.Par;
            if (par <= 0) par = 4; // fallback

            int    strokes    = sessionData.Strokes;
            int    score      = strokes - par;
            bool   isFailed   = sessionData.TerminalState != BallState.InCup;
            string scoreLabel = ScoreLabelFor(score);

            // HoleCompleteCardWidget.BindCurrentHole formats the subhead as:
            //   "{ToTitleCase(CourseName)} Country Club  - Hole {N} - Par {P}"
            // So CourseName must be the RAW course name (e.g. "LOMOND"), NOT the
            // full localization string ("Lomond Country Club - Hole 1").
            // Derive it from the courseNameKey (e.g. "HOLE_LOMOND_1" → "LOMOND")
            // or fall back to HoleContext.CourseName.
            string courseName = currentHoleData != null
                ? ExtractCourseNameFromKey(currentHoleData.courseNameKey)
                : (HoleContext.CourseName ?? "LOMOND");

            string teeName = HoleContext.TeeName ?? "REGULAR";

            // Hole map sprites — load from Resources/HoleImages/ at runtime.
            Sprite holeMap     = LoadHoleMapSprite(currentHoleData);
            Sprite nextHoleMap = LoadHoleMapSprite(nextHoleData);

            // Next hole info.
            int    nextHolePar  = nextHoleData != null ? nextHoleData.par : 0;
            string nextHoleDesc = nextHoleData != null
                ? LocalizationManager.Get(nextHoleData.descriptionKey)
                : "—";

            // Rewards display: use first-clear or replay pool based on _wasReplay.
            List<HoleReward> displayPool = null;
            if (!isFailed && currentHoleData != null)
                displayPool = _wasReplay ? currentHoleData.replayRewards : currentHoleData.rewards;

            int coinX   = 0;
            int repairX = 0;
            int ballX   = 0;
            if (displayPool != null)
            {
                foreach (var r in displayPool)
                {
                    switch (r.type)
                    {
                        case RewardType.Points:    coinX   += r.amount; break;
                        case RewardType.RepairKit: repairX += r.amount; break;
                        case RewardType.Ball:      ballX   += r.amount; break;
                    }
                }
            }
            // Fallback display values when no rewards (FAILED) or no data.
            if (coinX == 0 && repairX == 0 && ballX == 0)
            { coinX = 0; repairX = 0; ballX = 0; }

            return new HoleCompleteData(
                strokes:          strokes,
                par:              par,
                score:            score,
                scoreLabel:       scoreLabel,
                isFailed:         isFailed,
                hasPersonalBest:  false,          // Q8 lock — no PB tracking yet
                courseName:       courseName,
                holeNumber:       holeNumber,
                teeName:          teeName,
                bestStrokes:      "—",
                bestStrokesLabel: "",
                timeStr:          "00:00:00",
                bestTimeStr:      "—",
                rewardCoinX:      coinX,
                rewardRepairX:    repairX,
                rewardBallX:      ballX,
                nextHoleNumber:   nextHole,
                nextHolePar:      nextHolePar,
                nextHoleTipText:  nextHoleDesc,
                holeMap:          holeMap,
                nextHoleMap:      nextHoleMap
            );
        }

        // ── Reward grant (executed on action button press) ────────────────────

        void GrantRewards()
        {
            if (!_lastSuccess || _rewardsGranted) return;
            _rewardsGranted = true;

            HoleData hole = TryGetHoleData(_lastSessionData.HoleNumber);
            if (hole == null) return;

            var pool = _wasReplay ? hole.replayRewards : hole.rewards;
            if (pool == null) return;

            // Delegate to shared RewardGranter (Stage 2 DRY extraction).
            // No behavior change — the grant switch now lives once in RewardGranter.Grant().
            GolfinRedux.UI.RewardGranter.Grant(pool);
        }

        // ── Progression write ─────────────────────────────────────────────────

        void WriteProgressionIfSuccess()
        {
            if (!_lastSuccess) return;
            int current = _lastSessionData.HoleNumber;
            _progression.MarkHolePlayed(current);
            if (current < 18)
                _progression.UnlockHole(current + 1);
        }

        // ── Action handlers ───────────────────────────────────────────────────

        // REPLAY: SUCCESS state — replay the same hole. MUST write progression +
        // grant rewards on SUCCESS (same as PLAY NEXT) so that tapping REPLAY instead
        // of PLAY NEXT still unlocks the next hole and pays out first-clear rewards.
        // Without these two calls a player who picks REPLAY silently loses both —
        // they'd come back to Hole Selection with Hole 2 still locked and no points.
        // (Stage E fix, 2026-05-22.)
        void OnReplay()
        {
            int current = _lastSessionData.HoleNumber;
            WriteProgressionIfSuccess();
            GrantRewards();
            GameSession.ResetForNewHole();
            Hide();  // delegates to _widget.Hide()

            var loadingScreen = FindObjectOfType<LoadingScreenController>(includeInactive: true);
            if (loadingScreen != null) loadingScreen.PrepareForHoleLoad(current);

            var loader = GameplaySceneLoader.Instance;
            if (loader != null)
            {
                loader.BeginGameplayLoad(current);
            }
            else
            {
                Debug.LogError("[HoleCompleteModalController] GameplaySceneLoader not found.");
            }
        }

        // RETRY: FAILED state — reload same hole without writing progression.
        void OnRetry()
        {
            int current = _lastSessionData.HoleNumber;
            GameSession.ResetForNewHole();
            Hide();  // delegates to _widget.Hide()

            var loadingScreen = FindObjectOfType<LoadingScreenController>(includeInactive: true);
            if (loadingScreen != null) loadingScreen.PrepareForHoleLoad(current);

            var loader = GameplaySceneLoader.Instance;
            if (loader != null)
            {
                loader.BeginGameplayLoad(current);
            }
            else
            {
                Debug.LogError("[HoleCompleteModalController] GameplaySceneLoader not found.");
            }
        }

        // PLAY: Card 2 PLAY button — load next hole + write progression + grant rewards.
        void OnPlayNext(int nextHoleNumber)
        {
            WriteProgressionIfSuccess();
            GrantRewards();
            GameSession.SetCurrentHole(nextHoleNumber);

            var loadingScreen = FindObjectOfType<LoadingScreenController>(includeInactive: true);
            if (loadingScreen != null) loadingScreen.PrepareForHoleLoad(nextHoleNumber);

            var loader = GameplaySceneLoader.Instance;
            if (loader != null)
            {
                // Pass modalToHideOnMidpoint: this so GameplaySceneLoader calls Hide()
                // (which delegates to _widget.Hide()) at the fade midpoint.
                loader.BeginGameplayLoad(nextHoleNumber, modalToHideOnMidpoint: this);
            }
            else
            {
                Debug.LogError("[HoleCompleteModalController] GameplaySceneLoader not found.");
                Hide();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        static HoleData TryGetHoleData(int holeNumber)
        {
            // HoleDatabaseLoader.GetHole uses 0-based index; holeNumber is 1-based.
            return HoleDatabaseLoader.GetHole(holeNumber - 1);
        }

        /// <summary>
        /// Extracts the raw course name from a localization key like "HOLE_LOMOND_1".
        /// Returns "LOMOND" so that HoleCompleteCardWidget.BindCurrentHole can format
        /// the subhead as "Lomond Country Club  - Hole 1 - Par 4".
        /// Falls back to "LOMOND" if the key format is unexpected.
        /// </summary>
        static string ExtractCourseNameFromKey(string courseNameKey)
        {
            if (string.IsNullOrEmpty(courseNameKey)) return "LOMOND";
            // "HOLE_LOMOND_1" → split by '_', take parts between index 1 and last number
            var parts = courseNameKey.Split('_');
            // Expected: ["HOLE", "LOMOND", "1"] or ["HOLE", "COURSE", "NAME", "1"]
            // Collect all parts after "HOLE" and before the trailing number.
            if (parts.Length < 3) return courseNameKey;
            int lastIdx = parts.Length - 1;
            // If last part is a number, exclude it; otherwise use all after "HOLE".
            bool lastIsNum = int.TryParse(parts[lastIdx], out _);
            int endIdx = lastIsNum ? lastIdx : parts.Length;
            // Rebuild course name from parts[1..endIdx-1]
            var nameParts = new System.Collections.Generic.List<string>();
            for (int i = 1; i < endIdx; i++) nameParts.Add(parts[i]);
            return string.Join("_", nameParts);
        }

        static Sprite LoadHoleMapSprite(HoleData hole)
        {
            if (hole == null || string.IsNullOrEmpty(hole.holeImageName)) return null;
            Sprite img = Resources.Load<Sprite>($"HoleImages/{hole.holeImageName}");
            // Fallback to neutral "Missing" sprite — mirrors HoleCardController lines 159-162.
            if (img == null)
                img = Resources.Load<Sprite>("HoleImages/Missing");
            return img;
        }

        /// <summary>
        /// Score-vs-par label. Copied from HoleCompleteDriver.ScoreLabelFor (pure utility).
        /// </summary>
        public static string ScoreLabelFor(int score)
        {
            switch (score)
            {
                case -3: return "Albatross";
                case -2: return "Eagle";
                case -1: return "Birdie";
                case  0: return "Par";
                case  1: return "Bogey";
                case  2: return "Double Bogey";
                case  3: return "Triple Bogey";
                default: return score < 0 ? $"{score}" : $"+{score}";
            }
        }

        // ── Test seams ────────────────────────────────────────────────────────

        internal void InjectProgressionStore(IHoleProgressionStore store)
            => _progression = store;

        internal void InjectWidget(HoleCompleteWidget widget)
            => _widget = widget;

        internal HoleCompletionData LastData    => _lastSessionData;
        internal bool               WasReplay   => _wasReplay;
        internal bool               LastSuccess  => _lastSuccess;

        // ── Expose HandleHoleComplete for tests (via reflection or internal access) ──
        void HandleHoleCompleteInternal(HoleCompletionData data) => HandleHoleComplete(data);
    }
}
