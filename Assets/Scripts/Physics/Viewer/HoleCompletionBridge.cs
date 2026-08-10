using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Production bridge: subscribes to BallStateMachine.OnShotComplete and fires
    /// GameSession.MarkHoleComplete on InCup OR stroke-cap (FAILED) detection.
    ///
    /// Lives on a [Session] GameObject in LabScaffold.unity (the gameplay host scene).
    /// HoleCompleteDriver.HandleShotComplete no longer owns MarkHoleComplete — this
    /// component is the sole authoritative caller in production play.
    ///
    /// Placed in Golfin.Physics.Viewer so it can access PhysicsLabController.BallSM
    /// (which is internal to the Viewer assembly).
    ///
    /// FAILED detection rule — OPT-IN ONLY (Cesar 2026-08-10):
    ///   GameSession.StrokeCapEnabled AND terminal == AtRest
    ///   AND GameSession.TurnCount >= par + CapOverPar
    ///   → fire MarkHoleComplete with terminal=AtRest (FAILED proxy).
    ///
    /// <b>No shipping mode enables it.</b> Practice, Tournament and 1v1 all run to the
    /// cup — a hole only ends on InCup. The cap originally shipped ungated at par + 5
    /// (loop_v2_c1 Q1), which surfaced a FAILED screen on shot 10 of Hole 1 (par 5) in
    /// Practice, where there is no fail condition by design. The machinery is preserved
    /// intact for Missions, which will opt in via GameSession.StrokeCapEnabled and supply
    /// its own limit through GameSession.StrokeCapOverPar.
    ///
    /// OB or AtRest-below-cap are no-ops (play continues; OB adds penalty stroke elsewhere).
    /// </summary>
    public class HoleCompletionBridge : MonoBehaviour
    {
        [SerializeField] PhysicsLabController _controller;

        [Tooltip("Fallback stroke count above par that triggers FAILED, used when " +
                 "GameSession.StrokeCapOverPar is 0. Only consulted when " +
                 "GameSession.StrokeCapEnabled is true (Missions opt-in) — inert otherwise.")]
        [SerializeField] int _strokeCapOverPar = 5;

        /// <summary>
        /// Effective cap magnitude: the mode's runtime override when it supplied one,
        /// otherwise the value serialized on this component.
        /// </summary>
        int CapOverPar => GameSession.StrokeCapOverPar > 0
            ? GameSession.StrokeCapOverPar
            : _strokeCapOverPar;

        BallStateMachine _sm;
        bool _firedThisHole;

        void Awake()
        {
            if (_controller == null)
                _controller = FindObjectOfType<PhysicsLabController>();
        }

        // Start() runs after ALL Awake() calls complete, so PhysicsLabController._ballSM
        // is guaranteed to be constructed before we read BallSM here.
        void Start()
        {
            _sm = _controller != null ? _controller.BallSM : null;
            if (_sm != null)
                _sm.OnShotComplete += HandleShot;
            else
                Debug.LogWarning("[HoleCompletionBridge] BallSM is null in Start — OnShotComplete will not be monitored.");
        }

        void OnDestroy()
        {
            if (_sm != null)
                _sm.OnShotComplete -= HandleShot;
        }

        /// <summary>
        /// Subscribe to GameSession.OnTurnChanged so we can reset _firedThisHole
        /// when ResetForNewHole fires (sets TurnCount=1).
        /// </summary>
        void OnEnable()
        {
            GameSession.OnTurnChanged += OnTurnChanged;
        }

        void OnDisable()
        {
            GameSession.OnTurnChanged -= OnTurnChanged;
        }

        void OnTurnChanged()
        {
            // ResetForNewHole sets TurnCount = 1 — use as hole-start signal.
            if (GameSession.TurnCount == 1)
                _firedThisHole = false;
        }

        /// <summary>Allow tests to reset the fired guard directly.</summary>
        public void ResetFiredFlag() => _firedThisHole = false;

        void HandleShot(ShotResult result)
        {
            // Phase 2a: versus hole-outs are owned by VersusMatchController.
            // Suppress the solo result modal on the versus path entirely.
            if (GameSession.IsVersus) return;

            // T6: tournament hole-outs routed via OnTournamentHoleComplete.
            // TournamentRoundHandler (Assembly-CSharp, ShellScene) handles RP debit + submit.
            if (GameSession.IsTournament)
            {
                // Guard: only fire once per hole.
                if (_firedThisHole) return;

                int tStrokes = GameSession.TurnCount;
                int tPar     = HoleContext.Par;
                int tCap     = tPar + CapOverPar;

                bool isHoleOut = result.TerminalState == BallState.InCup;
                // Opt-in only: tournament rounds ship with no stroke cap.
                bool isCapped  = GameSession.StrokeCapEnabled
                              && result.TerminalState == BallState.AtRest
                              && tStrokes >= tCap;

                if (!isHoleOut && !isCapped) return;

                _firedThisHole = true;

                int tPenalties = 0;
                foreach (var rec in GameSession.ShotHistory)
                    tPenalties += rec.PenaltyStrokes;

                int tHoleNumber = GameSession.CurrentHoleNumber > 0
                    ? GameSession.CurrentHoleNumber
                    : HoleContext.HoleNumber;

                // Use the static fire method — events cannot be invoked with ?. from outside the declaring class.
                GameSession.FireTournamentHoleComplete(tHoleNumber, tStrokes + tPenalties);
                return;
            }

            // Guard: only fire once per hole to prevent double-fire.
            if (_firedThisHole) return;

            int strokes = GameSession.TurnCount;
            int par     = HoleContext.Par;
            int cap     = par + CapOverPar;

            if (result.TerminalState == BallState.InCup)
            {
                _firedThisHole = true;
                Fire(BallState.InCup, strokes);
            }
            else if (GameSession.StrokeCapEnabled
                  && result.TerminalState == BallState.AtRest
                  && strokes >= cap)
            {
                // Opt-in only (Missions). Practice and every other shipping mode fall
                // through here forever — InCup is their sole end-condition.
                _firedThisHole = true;
                Fire(BallState.AtRest, strokes);
            }
            // OB, AtRest-below-cap, or cap disabled: no-op.
        }

        void Fire(BallState terminal, int strokes)
        {
            int penalties = 0;
            foreach (var rec in GameSession.ShotHistory)
                penalties += rec.PenaltyStrokes;

            int holeNumber = GameSession.CurrentHoleNumber > 0
                ? GameSession.CurrentHoleNumber
                : HoleContext.HoleNumber;

            GameSession.MarkHoleComplete(new HoleCompletionData(terminal, strokes, penalties, holeNumber));
        }
    }
}
