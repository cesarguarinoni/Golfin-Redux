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
    /// FAILED detection rule (Q1 lock: par + 5):
    ///   terminal == AtRest AND GameSession.TurnCount >= par + _strokeCapOverPar
    ///   → fire MarkHoleComplete with terminal=AtRest (FAILED proxy).
    ///
    /// OB or AtRest-below-cap are no-ops (play continues; OB adds penalty stroke elsewhere).
    /// </summary>
    public class HoleCompletionBridge : MonoBehaviour
    {
        [SerializeField] PhysicsLabController _controller;

        [Tooltip("Stroke count above par that triggers FAILED. Default: par + 5 (Q1 lock).")]
        [SerializeField] int _strokeCapOverPar = 5;

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
                int tCap     = tPar + _strokeCapOverPar;

                bool isHoleOut = result.TerminalState == BallState.InCup;
                bool isCapped  = result.TerminalState == BallState.AtRest && tStrokes >= tCap;

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
            int cap     = par + _strokeCapOverPar;

            if (result.TerminalState == BallState.InCup)
            {
                _firedThisHole = true;
                Fire(BallState.InCup, strokes);
            }
            else if (result.TerminalState == BallState.AtRest && strokes >= cap)
            {
                _firedThisHole = true;
                Fire(BallState.AtRest, strokes);
            }
            // OB or AtRest-below-cap: no-op.
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
