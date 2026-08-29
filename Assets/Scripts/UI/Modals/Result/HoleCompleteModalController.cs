using System.Linq;
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

            // missions_v1 §B4 — FIRST, before anything reads the reward pool. A mission does
            // not pay the hole's rewards at all: it pays what `golfin_mission_rewards` says,
            // through its own claim. Settling that here keeps the two economies from both
            // firing on one hole-out.
            SettleMissionIfActive(sessionData);

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

            // A mission is presented on the SAME cards the player chose it from, not on a result
            // card wearing a SUCCESS title. When that takes, the hole-complete cards are hidden and
            // the rest of this wiring has nothing to hook.
            if (TryShowMissionCards()) return;

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
                    ToastController.Instance.Show(LocalizationManager.Get("TOAST_COURSE_CLEARED"), 3f);
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

            // On a MISSION, holing out is not success — clearing the goals is. SettleMissionIfActive
            // has already run (line ~112), so the verdict is in hand here. Without this the modal
            // read "✓ SUCCESS" in green directly above "✗ Hole out in 3 strokes or fewer" in red,
            // which is not a near-miss in wording: it tells the player they passed and failed the
            // same round. A mission that is not cleared is FAILED, whatever the ball did.
            if (LastMissionResult != null && !LastMissionResult.Cleared) isFailed = true;

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

        // ── Missions (missions_v1 §B4) ────────────────────────────────────────

        /// <summary>The verdict for the hole just finished, or null when it was not a mission.
        /// Read by the modal's goal strip and by tests.</summary>
        internal Golfin.Gameplay.Missions.MissionResult LastMissionResult { get; private set; }

        /// <summary>
        /// Evaluate the active mission's goals and claim it. No-op in every other mode.
        ///
        /// THE ORDER MATTERS AND IT IS NOT THE OBVIOUS ONE. The goals are evaluated and the
        /// mission session is ENDED before the claim's response comes back, because ending the
        /// session is what pops the supplied bag and clears the stroke cap — and those must not
        /// depend on a network round trip. If the claim fails the player keeps their own clubs
        /// and simply has not been paid yet; if ending waited on the response, a dropped
        /// connection would strand them in a mission's bag.
        ///
        /// THE CLIENT NEVER CREDITS ANYTHING HERE. It sends what it did (`goals_met`, strokes)
        /// and writes back only what the server SAYS — see the mirror below. That is the same
        /// discipline `/shop/purchase` and `/progress/level-up` were built on, and the reason
        /// Missions never needed a `LEGACY_*` constant of its own.
        /// </summary>
        void SettleMissionIfActive(HoleCompletionData sessionData)
        {
            LastMissionResult = null;

            var session = Golfin.Gameplay.Missions.MissionSession.Active;
            var evaluator = Golfin.Gameplay.Missions.MissionSession.Evaluator;
            if (session == null || evaluator == null) return;

            var result = evaluator.EvaluateFinal(sessionData, System.Guid.NewGuid().ToString());
            LastMissionResult = result;
            Debug.Log($"[HoleCompleteModal] MISSION {result.MissionId}: cleared={result.Cleared} " +
                      $"strokes={result.Strokes} putts={result.Putts} " +
                      $"goals=[{string.Join(", ", result.Goals.ConvertAll(g => $"{g.Type}:{g.Met}"))}]");

            // The daily has its own endpoint and its own hash guard; it is claimed by the
            // Mission Selection screen, which is the only place that holds the recipe hash.
            bool isDaily = session.IsDaily;

            // The daily is claimed by the Mission Selection screen, which is the only place
            // holding the recipe hash the server checks. Park the round for it; End() is next
            // and takes the session with it.
            if (isDaily)
                Golfin.Gameplay.Missions.MissionSession.PendingDaily =
                    new Golfin.Gameplay.Missions.MissionSession.FinishedDaily
                    {
                        MissionId = result.MissionId,
                        Strokes   = result.Strokes,
                        Cleared   = result.Cleared,
                    };

            Golfin.Gameplay.Missions.MissionSession.End();

            if (isDaily) return;
            StartCoroutine(ClaimMissionRoutine(result));
        }

        readonly System.Collections.Generic.List<GameObject> _missionCards = new System.Collections.Generic.List<GameObject>();

        /// <summary>
        /// Show the round on Mission Selection's own card: the mission just played, with its pill
        /// reading SUCCESS or FAILED and a tick or cross against each rule, and beneath it the NEXT
        /// mission on an identical card.
        ///
        /// WHY NOT THE HOLE-COMPLETE CARD. It answered the wrong questions. Its second card offered
        /// the next HOLE, which is not what a mission player is doing next, and the first stacked a
        /// SUCCESS banner and a goal strip on top of a layout already sized for neither: ContentRoot
        /// carries a ContentSizeFitter and grew to 802pt inside a Card1 fixed at 855 whose VLG has
        /// childControlHeight off, so the title bled over the top edge and REPLAY over the bottom.
        /// Reusing the mission card removes the mismatch rather than tuning it.
        ///
        /// The prefab is borrowed from the live MissionSelectionScreenController rather than wired
        /// as a second serialized reference, so there is exactly one answer to "which card is the
        /// mission card" and no way for the two to drift apart.
        ///
        /// Returns false for every non-mission hole, which keeps the ordinary path untouched.
        /// </summary>
        bool TryShowMissionCards()
        {
            var result = LastMissionResult;
            if (result == null) return false;

            var selection = FindObjectOfType<GolfinRedux.UI.MissionSelection.MissionSelectionScreenController>(true);
            var prefab = selection != null ? selection.CardPrefab : null;
            if (prefab == null) return false;

            Golfin.Gameplay.Missions.MissionDefinition played = null;
            foreach (var m in Golfin.Gameplay.Missions.MissionCatalog.All)
                if (m.Id == result.MissionId) { played = m; break; }
            if (played == null) return false;

            var root = transform.root.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(r => r.name == "Root" && r.GetComponentInParent<Golfin.Gameplay.UI.ShotUI.HoleCompleteWidget>() != null);
            if (root == null) return false;

            foreach (var old in _missionCards) if (old != null) Destroy(old);
            _missionCards.Clear();
            if (_widget.Card1 != null) _widget.Card1.gameObject.SetActive(false);
            if (_widget.Card2 != null) _widget.Card2.gameObject.SetActive(false);

            // The verdict, on the card that set the terms.
            var verdict = Spawn(prefab, root, played,
                GolfinRedux.UI.MissionSelection.MissionCardMode.Replay);
            if (verdict != null) verdict.ShowResult(result);

            // ...and what comes next, which for a mission player is the next MISSION.
            var next = NextMissionAfter(played);
            if (next != null)
                Spawn(prefab, root, next, GolfinRedux.UI.MissionSelection.MissionCardMode.Play);

            return true;
        }

        GolfinRedux.UI.MissionSelection.MissionCardController Spawn(
            GolfinRedux.UI.MissionSelection.MissionCardController prefab,
            RectTransform parent,
            Golfin.Gameplay.Missions.MissionDefinition m,
            GolfinRedux.UI.MissionSelection.MissionCardMode mode)
        {
            var card = Instantiate(prefab, parent);
            Golfin.Gameplay.Missions.MissionCatalog.Warnings.TryGetValue(m.Id, out string warning);
            card.Bind(m, mode, GolfinRedux.UI.MissionSelection.MissionCardState.Expanded, warning ?? "");
            card.OnActionButtonClicked += c =>
            {
                _widget.Hide();
                GolfinRedux.UI.MissionSelection.MissionLauncher.TryStart(c.Mission, c.IsPlayable);
            };
            _missionCards.Add(card.gameObject);
            return card;
        }

        /// <summary>
        /// The first mission that is unlocked and not yet cleared — the same rule the selection
        /// screen uses to decide which card reads NEXT, so the two never disagree about what is
        /// next. Null once the campaign is finished, and then the modal simply shows the verdict.
        /// </summary>
        static Golfin.Gameplay.Missions.MissionDefinition NextMissionAfter(
            Golfin.Gameplay.Missions.MissionDefinition played)
        {
            var p = Golfin.Gameplay.Missions.MissionProgressionService.Instance;
            foreach (var m in Golfin.Gameplay.Missions.MissionCatalog.All)
                if (!p.HasCleared(m.Id) && p.IsUnlocked(m)) return m;
            return null;
        }

        System.Collections.IEnumerator ClaimMissionRoutine(Golfin.Gameplay.Missions.MissionResult result)
        {
            yield return Golfin.Economy.MissionsClient.Instance.ClaimRoutine(
                result.MissionId, result.Strokes, result.Cleared, result.IdempotencyKey,
                r =>
                {
                    if (!r.Success || r.Data == null)
                    {
                        // Online-only by design (see MissionsClient). A failed claim is a clear
                        // the player has not been PAID for yet, not one they did not achieve —
                        // the same key succeeds on a retry.
                        Debug.LogWarning($"[HoleCompleteModal] mission claim failed for {result.MissionId}: " +
                                         $"{r.ErrorMessage}");
                        return;
                    }

                    var payload = r.Data.Effective;
                    Debug.Log($"[HoleCompleteModal] mission claim {payload.Status}: awarded={payload.Awarded} " +
                              $"(mission {payload.MissionRp} + tier {payload.TierBonus}) " +
                              $"firstClear={payload.FirstClear} clears={payload.Clears}");

                    MirrorMissionProgress(payload);

                    // The balance moved server-side; pull it rather than adding locally, so the
                    // number on screen is the server's and not this client's arithmetic.
                    if (payload.Paid) Golfin.Economy.PointsService.Instance?.RefreshBalanceAsync();
                });
        }

        /// <summary>
        /// Write the server's answer into the local mirror (`SaveData.missionProgress`).
        ///
        /// ⚠️ ONLY FROM A RESPONSE. Nothing here increments anything: `clears`, `attempts` and
        /// `bestStrokes` are copied from what the server reported. A client that counted its
        /// own clears would show a first-clear reward the server was never going to pay again.
        /// </summary>
        static void MirrorMissionProgress(Golfin.Economy.MissionClaimResult payload)
        {
            var save = Golfin.Save.SaveDataHost.Instance?.Data;
            if (save == null || string.IsNullOrEmpty(payload.MissionId)) return;

            save.missionProgress ??= new System.Collections.Generic.List<Golfin.Save.PersistedMissionProgress>();
            var row = save.missionProgress.Find(m => m.missionId == payload.MissionId);
            if (row == null)
            {
                row = new Golfin.Save.PersistedMissionProgress { missionId = payload.MissionId };
                save.missionProgress.Add(row);
            }
            row.clears = payload.Clears;
            row.attempts = payload.Attempts;
            if (payload.BestStrokes.HasValue) row.bestStrokes = payload.BestStrokes.Value;

            Golfin.Save.SaveDataHost.Instance?.MarkDirty();
        }

        // ── Reward grant (executed on action button press) ────────────────────

        void GrantRewards()
        {
            // A MISSION pays through its own claim, not the hole's reward pool. Without this
            // guard a cleared mission would pay twice — once server-priced, once from the
            // hole's own table — and the second one is exactly the client-decided credit this
            // whole feature was built to avoid.
            if (LastMissionResult != null) return;
            if (!_lastSuccess || _rewardsGranted) return;
            _rewardsGranted = true;

            HoleData hole = TryGetHoleData(_lastSessionData.HoleNumber);
            if (hole == null) return;

            var pool = _wasReplay ? hole.replayRewards : hole.rewards;
            if (pool == null) return;

            // Delegate to shared RewardGranter (Stage 2 DRY extraction).
            // No behavior change — the grant switch now lives once in RewardGranter.Grant().
            // Slice 2: the same _wasReplay that chose the pool also chooses the server action, so the
            // ledger books a replay against hole_replay's cap and not hole_complete's.
            GolfinRedux.UI.RewardGranter.Grant(
                pool,
                _wasReplay ? Golfin.Economy.PointsActions.HoleReplay
                           : Golfin.Economy.PointsActions.HoleComplete);
        }

        // ── Progression write ─────────────────────────────────────────────────

        void WriteProgressionIfSuccess()
        {
            if (!_lastSuccess) return;
            int current = _lastSessionData.HoleNumber;
            _progression.MarkHolePlayed(current);
            // demo_build_slice §3.4: finishing a hole in the demo never unlocks another.
            if (current < 18 && !GolfinRedux.Demo.DemoGate.IsDemo)
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
