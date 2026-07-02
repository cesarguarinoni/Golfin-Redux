using System.Collections;
using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Turn-flow state machine for 1v1 versus matches (Phase 2a).
    ///
    /// Lives on the [Session] GameObject in LabScaffold.unity alongside HoleCompletionBridge.
    /// Hard no-op when GameSession.IsVersus == false (solo/Practice untouched).
    ///
    /// States: MatchStart → AnnounceTurn → AwaitShot → ResolveShot → Decide → AnnounceTurn | MatchEnd
    ///
    /// Ball model: one shared ball (PhysicsLabController), teleported each turn to the
    /// active player's stored lie. Ball resting position is written back after each shot.
    ///
    /// Win/tie/draw truth table (SPEC §10):
    ///   P1 sinks → P2 gets ONE courtesy shot → DRAW or P1 WIN.
    ///   P2 sinks → P2 WIN immediately.
    ///
    /// Asmdef boundary: this controller fires GameSession.MarkMatchComplete(outcome) and
    /// lets VersusResultHandler (Assembly-CSharp) handle RP grant and navigation.
    /// VersusMatchController MUST NOT call RewardPointsManager directly.
    /// </summary>
    public class VersusMatchController : MonoBehaviour
    {
        [Header("Required references")]
        [SerializeField] ClubHandleDragger _humanInput;
        [SerializeField] TurnBannerWidget  _banner;
        [SerializeField] VersusBot         _bot;
        [SerializeField] PhysicsLabController _controller;

        // ── Deferred-recorder signal (FIX 1 — §15 visual gate) ───────────────
        /// <summary>
        /// Fired just before MatchFlow() begins (hole loaded, BallSM ready, IsVersus confirmed).
        /// VersusHudCaptureMenu subscribes to this to defer BotVideoRecorder.Begin() until
        /// the full 30s watchdog window points at the match itself (not hole load time).
        /// Editor-only listener; runtime game ignores it.
        /// </summary>
        public static event System.Action OnMatchReadyToBegin;

        // ── Debug-only: full-bot mode (NOT serialized) ─────────────────────────
        // Set by VersusBotMatchLauncher editor menu to drive BOTH P1 and P2 as bots
        // for the full-match video deliverable. Never set in the shipped scene.
        // Production play always has _debugBothBots = false.
        [System.NonSerialized] public bool _debugBothBots;

        // ── Debug-only: override start lie (capture tool — NOT serialized) ────
        // When non-zero and _debugBothBots is true, MatchFlow() uses this as the
        // initial lie for both players instead of BallPosition at hole load.
        // Lets the capture scenario start both players near the pin for a fast
        // match resolution inside the 30s BotVideoRecorder window.
        // Production play: always Vector3.zero (ignored).
        [System.NonSerialized] public Vector3 _debugStartLie;

        BallStateMachine _sm;

        // ── Turn-flow state ──────────────────────────────────────────────────
        bool  _matchRunning;
        bool  _awaitingShot;
        bool  _shotResolved;
        ShotResult _lastResult;
        bool  _courtesyShotPending; // P2's single courtesy shot after P1 sinks

        void Awake()
        {
            if (_controller == null) _controller = FindObjectOfType<PhysicsLabController>();
            if (_bot == null)        _bot        = FindObjectOfType<VersusBot>();
            if (_humanInput == null) _humanInput = FindObjectOfType<ClubHandleDragger>();
        }

        IEnumerator Start()
        {
            // Wait up to 5 seconds for GameSession.IsVersus to be set to true.
            // This handles:
            //   - Production path: matchmaking sets IsVersus before LabScaffold loads → IsVersus
            //     is already true on frame 0, loop exits immediately.
            //   - Debug-menu path (VersusHudCaptureMenu): OnPlayModeStateChanged(EnteredPlayMode)
            //     fires AFTER all Awake/Start — IsVersus is false on frame 0 but becomes true
            //     within 1-2 frames once the editor callback runs.
            float waited = 0f;
            while (!GameSession.IsVersus && waited < 5f)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }

            if (!GameSession.IsVersus)
            {
                // Timed out — this is a solo/practice session; hard no-op.
                yield break;
            }

            // Suppress VersusHudController's opening banner (set here after IsVersus confirmed,
            // in case Awake ran before IsVersus was set by the debug-menu path).
            VersusHudController._suppressOpeningBanner = true;

            // Wait until BallSM is available (OnHoleLoaded runs asynchronously and may not
            // have fired yet at this point).
            waited = 0f;
            while ((_controller == null || _controller.BallSM == null) && waited < 10f)
            {
                if (_controller == null) _controller = FindObjectOfType<PhysicsLabController>();
                yield return null;
                waited += Time.unscaledDeltaTime;
            }

            _sm = _controller != null ? _controller.BallSM : null;
            if (_sm == null)
            {
                Debug.LogError("[VersusMatchController] BallSM still null after 10s — cannot run turn-flow.");
                yield break;
            }

            // Subscribe to shot completion to receive turn-end signals.
            _sm.OnShotComplete += OnShotComplete;

            // Wait for PhysicsLabController.OnHoleLoaded to complete (BUG A fix iter-5).
            // ScanForLoadedHoleSceneAtStartup yields 2 frames then fires OnHoleLoaded, which
            // sets _useSceneProviders=true, calls SetupAtTee(), and updates _savedTeeWorldPos to
            // the real hole tee. MatchFlow() must read BallPosition AFTER this completes so
            // ResetMatchState() receives the correct tee, not the serialized Hole_18 fallback.
            // Timeout 5s: flat-ground sessions never set IsHoleReady (no _Geo scene); that's fine
            // for those — MatchFlow just uses whatever BallPosition is set to (likely spawn point).
            waited = 0f;
            while (!_controller.IsHoleReady && waited < 5f)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }
            Debug.Log($"[VersusMatchController] IsHoleReady={_controller.IsHoleReady} after {waited:F2}s — proceeding to MatchFlow.");

            Debug.Log("[VersusMatchController] IsVersus confirmed — starting MatchFlow.");

            // Fire deferred-recorder signal: hole is loaded, BallSM is ready, match about to begin.
            // VersusHudCaptureMenu (editor-only) subscribes to start BotVideoRecorder.Begin() here
            // so the full 30s watchdog window is spent on the match, not on hole-load overhead.
            OnMatchReadyToBegin?.Invoke();

            // Start the match flow.
            StartCoroutine(MatchFlow());
        }

        void OnDestroy()
        {
            if (_sm != null) _sm.OnShotComplete -= OnShotComplete;
        }

        // ── BallStateMachine callback ────────────────────────────────────────

        void OnShotComplete(ShotResult result)
        {
            if (!_awaitingShot) return;
            _lastResult   = result;
            _shotResolved = true;
        }

        // ── Main coroutine ───────────────────────────────────────────────────

        IEnumerator MatchFlow()
        {
            _matchRunning = true;

            // ── MatchStart ──────────────────────────────────────────────────
            // Read tee/start lie from the current ball position at hole load.
            // Debug-capture override: if _debugStartLie is set (non-zero) and _debugBothBots
            // is true, use that position as the starting lie for both players. This lets the
            // capture scenario start near the pin so the match resolves inside 30s.
            // Production play: _debugStartLie is Vector3.zero → always reads BallPosition.
            Vector3 tee = (_debugBothBots && _debugStartLie != Vector3.zero)
                ? _debugStartLie
                : _controller.BallPosition;
            if (_debugBothBots && _debugStartLie != Vector3.zero)
                Debug.Log($"[VersusMatchController] Debug start lie override: {_debugStartLie:F2} (near-pin start for capture scenario).");
            MatchContext.ResetMatchState(tee);

            int active = 0; // P1 shoots first (SPEC §2 decision 3).

            // ── Alternating turns ────────────────────────────────────────────
            while (_matchRunning)
            {
                // ── AnnounceTurn ─────────────────────────────────────────────
                yield return StartCoroutine(AnnounceTurn(active));

                // ── AwaitShot ─────────────────────────────────────────────────
                yield return StartCoroutine(AwaitShot(active));

                // ── ResolveShot ───────────────────────────────────────────────
                ShotResult result = _lastResult;
                bool holedOut = ApplyResolveShotToContext(active, result);

                // ── Decide ─────────────────────────────────────────────────────
                bool decided = TryDecide(active, holedOut, out int nextActive, out GameSession.MatchOutcome outcome, out bool matchEnded);

                if (matchEnded)
                {
                    yield return StartCoroutine(MatchEnd(outcome));
                    yield break;
                }

                active = nextActive;
            }
        }

        // ── State methods ────────────────────────────────────────────────────

        /// <summary>
        /// AnnounceTurn: update active player, teleport ball to their lie, aim camera, show banner.
        /// </summary>
        IEnumerator AnnounceTurn(int active)
        {
            MatchContext.SetActive(active);

            // Teleport ball to active player's lie.
            _controller.PlaceBallAt(MatchContext.Players[active].Lie);

            // Aim camera toward cup.
            Vector3 cup  = HoleContext.PinWorld;
            Vector3 ball = _controller.BallPosition;
            Vector3 flat = new Vector3(cup.x - ball.x, 0f, cup.z - ball.z);
            if (flat.sqrMagnitude > 0.001f)
                _controller.SetCameraYawRadians(Mathf.Atan2(flat.z, flat.x));

            // Show turn banner.
            if (_banner != null)
            {
                bool fromLeft = active == 0;
                string text   = active == 0 ? "YOUR TURN" : "OPPONENT'S TURN";
                _banner.Show(text, fromLeft);
            }

            // Wait for the banner slide-in + hold (slideIn 0.25s + hold 1.2s = ~1.45s).
            // Use a fixed wait rather than banner's full animation so AwaitShot can start
            // while the banner fade-out is running — this matches the Phase-1 behavior.
            // Debug capture mode: halve the wait so the full 4-turn match fits in 30s.
            yield return new WaitForSeconds(_debugBothBots ? 0.75f : 1.5f);
        }

        /// <summary>
        /// AwaitShot: enable/disable human input, trigger bot if P2, wait for OnShotComplete.
        /// </summary>
        IEnumerator AwaitShot(int active)
        {
            _shotResolved = false;
            _lastResult   = default;
            _awaitingShot = true;

            if (active == 0 && !_debugBothBots)
            {
                // Human turn — unlock input.
                if (_humanInput != null) _humanInput.enabled = true;
                Debug.Log("[VersusMatchController] Human turn — input unlocked.");
            }
            else
            {
                // Bot turn (P2 always bot; P1 also bot in _debugBothBots mode).
                if (_humanInput != null) _humanInput.enabled = false;
                string who = active == 0 ? "P1(debug-bot)" : "P2(bot)";
                Debug.Log($"[VersusMatchController] {who} turn — starting VersusBot.TakeShot().");
                StartCoroutine(_bot.TakeShot());
            }

            // Wait for OnShotComplete (with timeout safety = 120s).
            float elapsed = 0f;
            while (!_shotResolved && elapsed < 120f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _awaitingShot = false;

            if (!_shotResolved)
                Debug.LogWarning($"[VersusMatchController] AwaitShot TIMEOUT after {elapsed:F1}s for player {active}.");

            // Lock human input after shot regardless of who just shot.
            if (_humanInput != null) _humanInput.enabled = false;

            // Small pause so settled ball frame is visible.
            // Debug capture mode: shorten pause to save time inside the 30s window.
            yield return new WaitForSeconds(_debugBothBots ? 0.1f : 0.5f);
        }

        /// <summary>
        /// Record shot result into MatchContext. Returns true if this player holed out.
        /// </summary>
        bool ApplyResolveShotToContext(int active, ShotResult result)
        {
            // Increment stroke count (SPEC §7.3: Strokes++ on OnShotComplete).
            MatchContext.Players[active].Strokes++;

            // Mirror into TurnCount so PlayerCardWidget._turnText refreshes live (reads TurnCount, not Strokes).
            // Raise so all card widgets pick up the change immediately (DEFECT 2 fix — iter-7).
            MatchContext.Players[active].TurnCount = MatchContext.Players[active].Strokes;
            MatchContext.Raise();

            // Update lie to the new ball resting position (Vector3 from PhysicsLabController).
            // fp3 → Vector3: use BallPosition (public Vector3 on PhysicsLabController:127)
            // which already does the conversion internally.
            MatchContext.Players[active].Lie = _controller.BallPosition;

            bool holedOut = result.TerminalState == BallState.InCup;
            if (holedOut && !MatchContext.Players[active].HoledOut)
            {
                MatchContext.Players[active].HoledOut      = true;
                MatchContext.Players[active].HoleOutStroke = MatchContext.Players[active].Strokes;
                Debug.Log($"[VersusMatchController] Player {active} holed out at stroke {MatchContext.Players[active].HoleOutStroke}.");
            }

            return holedOut;
        }

        /// <summary>
        /// Win/tie truth table (SPEC §10). Returns the next active player and whether the match ended.
        /// </summary>
        bool TryDecide(int active, bool holedOut, out int nextActive, out GameSession.MatchOutcome outcome, out bool matchEnded)
        {
            nextActive  = MatchContext.Other(active);
            outcome     = GameSession.MatchOutcome.Draw;
            matchEnded  = false;

            int par = HoleContext.Par;
            // Read stroke cap from GameSession; written there by VersusResultHandler (Assembly-CSharp)
            // reading modes.csv "versus_1v1.versusStrokeCapOverPar". Default 5 per SPEC §11.
            int cap = par + GameSession.VersusStrokeCapOverPar;

            // ── Safety cap check ─────────────────────────────────────────────
            bool p0Capped = MatchContext.Players[0].Strokes >= cap;
            bool p1Capped = MatchContext.Players[1].Strokes >= cap;

            if ((p0Capped || p1Capped) && !MatchContext.Players[0].HoledOut && !MatchContext.Players[1].HoledOut)
            {
                // Both capped without holing → DRAW.
                if (p0Capped && p1Capped)
                {
                    outcome    = GameSession.MatchOutcome.Draw;
                    matchEnded = true;
                    nextActive = 0;
                    Debug.Log("[VersusMatchController] Safety cap: both players capped — DRAW.");
                    return true;
                }
                // One player capped → they cannot win; continue for the other (loop continues naturally).
            }

            // ── Both holed (shouldn't happen under alternation, but guard it) ──
            if (MatchContext.Players[0].HoledOut && MatchContext.Players[1].HoledOut)
            {
                int s0 = MatchContext.Players[0].HoleOutStroke;
                int s1 = MatchContext.Players[1].HoleOutStroke;
                outcome    = s0 < s1 ? GameSession.MatchOutcome.P1Win
                           : s1 < s0 ? GameSession.MatchOutcome.P2Win
                           : GameSession.MatchOutcome.Draw;
                matchEnded = true;
                nextActive = 0;
                Debug.Log($"[VersusMatchController] Both holed: P0.stroke={s0} P1.stroke={s1} → {outcome}");
                return true;
            }

            // ── P1 (active==0) sinks ────────────────────────────────────────
            if (active == 0 && holedOut)
            {
                // P2 gets ONE courtesy shot to tie.
                _courtesyShotPending = true;
                nextActive  = 1;
                matchEnded  = false;
                Debug.Log("[VersusMatchController] P1 sank — P2 gets courtesy shot.");
                return false;
            }

            // ── P2 (active==1) sinks ────────────────────────────────────────
            if (active == 1 && holedOut)
            {
                if (_courtesyShotPending)
                {
                    // P2 used the courtesy shot and holed — compare strokes.
                    _courtesyShotPending = false;
                    int s0 = MatchContext.Players[0].HoleOutStroke;
                    int s1 = MatchContext.Players[1].HoleOutStroke;
                    outcome = s0 == s1 ? GameSession.MatchOutcome.Draw
                            : s0 < s1  ? GameSession.MatchOutcome.P1Win
                            : GameSession.MatchOutcome.P2Win;
                    matchEnded = true;
                    nextActive = 0;
                    Debug.Log($"[VersusMatchController] P2 used courtesy shot and holed: P0.stroke={s0} P1.stroke={s1} → {outcome}");
                    return true;
                }
                else
                {
                    // P2 sinks outside courtesy → P2 WIN immediately (SPEC §10).
                    outcome    = GameSession.MatchOutcome.P2Win;
                    matchEnded = true;
                    nextActive = 0;
                    Debug.Log("[VersusMatchController] P2 sank on their own turn → P2 WIN.");
                    return true;
                }
            }

            // ── P2 used courtesy shot but missed ────────────────────────────
            if (_courtesyShotPending && active == 1 && !holedOut)
            {
                _courtesyShotPending = false;
                outcome    = GameSession.MatchOutcome.P1Win;
                matchEnded = true;
                nextActive = 0;
                Debug.Log("[VersusMatchController] P2 courtesy shot missed → P1 WIN.");
                return true;
            }

            // ── No resolution yet — swap turns ──────────────────────────────
            nextActive = MatchContext.Other(active);
            return false;
        }

        /// <summary>
        /// MatchEnd: show persistent WIN/LOSE/DRAW banner, lock input, fire OnMatchComplete event.
        /// </summary>
        IEnumerator MatchEnd(GameSession.MatchOutcome outcome)
        {
            _matchRunning = false;

            // Lock human input permanently.
            if (_humanInput != null) _humanInput.enabled = false;

            // Hide the shot-aiming HUD (aim cone, power gauge, action buttons, putter track)
            // so it does not appear behind/over the WIN/LOSE/DRAW banner. (DEFECT 1 fix — iter-7)
            if (_controller != null) _controller.HideShotUI();

            // Show persistent banner (SPEC §7 step 6).
            if (_banner != null)
            {
                string text = outcome == GameSession.MatchOutcome.P1Win ? "YOU WIN"
                            : outcome == GameSession.MatchOutcome.P2Win ? "YOU LOSE"
                            : "TIE";
                _banner.ShowPersistent(text, fromLeft: true);
            }

            // Brief pause before firing event (let banner animate in).
            // Debug capture mode: reduce pause so the WIN/LOSE/DRAW banner is
            // visible within the 30s BotVideoRecorder watchdog window.
            yield return new WaitForSeconds(_debugBothBots ? 0.5f : 2.0f);

            // Fire event across asmdef boundary → VersusResultHandler handles RP + return.
            int p1Strokes = MatchContext.Players[0].Strokes;
            int p2Strokes = MatchContext.Players[1].Strokes;
            Debug.Log($"[VersusMatchController] MatchEnd: {outcome} P1={p1Strokes} P2={p2Strokes}");
            GameSession.MarkMatchComplete(outcome, p1Strokes, p2Strokes);
        }
    }
}
