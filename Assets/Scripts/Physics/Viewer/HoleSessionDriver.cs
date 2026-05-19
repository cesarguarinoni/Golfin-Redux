using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Session;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2c: subscribes to BallStateMachine.OnShotComplete to record the shot,
    /// then advances GameSession.TurnCount the moment the SM re-enters Aiming
    /// (i.e. the same beat the camera returns to aiming framing).
    ///
    /// Mirrors LoopCameraDirector pattern — thin orchestration, no game logic.
    /// </summary>
    public class HoleSessionDriver : MonoBehaviour
    {
        [SerializeField] PhysicsLabController controller;

        BallStateMachine _sm;

        void Start()
        {
            // Use Start (not Awake) so that PhysicsLabController.Awake() has already
            // initialized _ballSM before we read BallSM here. Awake order is non-deterministic
            // across GameObjects; Start is guaranteed to run after all Awake calls complete.
            if (controller == null) controller = GetComponentInParent<PhysicsLabController>();
            _sm = controller != null ? controller.BallSM : null;
            if (_sm != null)
            {
                _sm.OnShotComplete += HandleShotComplete;
                _sm.OnStateChanged += HandleStateChanged;
            }
            else
            {
                Debug.LogError("[HoleSessionDriver] BallSM is null in Start — " +
                               $"controller={controller != null}, BallSM={controller?.BallSM}. " +
                               "Shot history will not be recorded.");
            }
        }

        void OnDestroy()
        {
            if (_sm != null)
            {
                _sm.OnShotComplete -= HandleShotComplete;
                _sm.OnStateChanged -= HandleStateChanged;
            }
        }

        void HandleShotComplete(ShotResult result)
        {
            // Record the shot using ShotNumber=current TurnCount BEFORE the increment.
            // Turn advance happens on the SM's transition back to Aiming (see HandleStateChanged).
            var record = BuildShotRecord(result);
            GameSession.RecordShot(record);
        }

        void HandleStateChanged(BallStateChange change)
        {
            // Advance TURN the moment the SM re-enters Aiming from a terminal state.
            // ReArm() is what fires this — same beat the camera returns to aiming framing,
            // so the UI tick is visually synchronized with the camera transition.
            if (change.Next != BallState.Aiming) return;
            if (change.Previous != BallState.AtRest
             && change.Previous != BallState.InCup
             && change.Previous != BallState.OB) return;

            // §2e: OB transitions add a penalty stroke. Order-independent — reads from
            // BallStateChange.Previous, not from ShotHistory (which may not be populated
            // yet due to subscription order).
            int penalty = (change.Previous == BallState.OB) ? 1 : 0;
            GameSession.SetTurn(ComputeNextTurn(GameSession.TurnCount, penalty));
        }

        /// <summary>
        /// §2e: pure helper for unit tests. Caller passes current turn + penalty stroke
        /// count; returns the new turn value. Negative penalty clamped to 0.
        /// </summary>
        public static int ComputeNextTurn(int currentTurn, int penaltyStrokes)
        {
            if (penaltyStrokes < 0) penaltyStrokes = 0;
            return currentTurn + 1 + penaltyStrokes;
        }

        ShotRecord BuildShotRecord(ShotResult result)
        {
            string clubLabel = "Unknown";
            if (controller != null
                && controller.CurrentClubIndex >= 0
                && controller.CurrentClubIndex < PhysicsLabController.LabClubLabels.Length)
            {
                clubLabel = PhysicsLabController.LabClubLabels[controller.CurrentClubIndex];
            }

            Vector3 origin = controller != null ? controller.LastShotOrigin : Vector3.zero;
            Vector3 finalPos = new Vector3(
                result.EndPosition.x.ToFloat(),
                result.EndPosition.y.ToFloat(),
                result.EndPosition.z.ToFloat());

            float dx = finalPos.x - origin.x;
            float dz = finalPos.z - origin.z;
            float distXZ = Mathf.Sqrt(dx * dx + dz * dz);

            // FinalSurface — best-effort. ShotResult doesn't carry it directly today;
            // attempt to derive from controller.LastTrajectory's last terrain hit.
            string finalSurface = "Unknown";
            var traj = controller != null ? controller.LastTrajectory : null;
            if (traj != null && traj.terrainHits != null && traj.terrainHits.Count > 0)
                finalSurface = traj.terrainHits[traj.terrainHits.Count - 1].Surface.ToString();

            string obReason = result.OBReason.HasValue ? result.OBReason.Value.ToString() : null;

            // §2e: OB shots get +1 penalty stroke.
            int penaltyStrokes = (result.TerminalState == BallState.OB) ? 1 : 0;

            return new ShotRecord(
                shotNumber: GameSession.TurnCount,
                clubLabel: clubLabel,
                originPosition: origin,
                finalPosition: finalPos,
                distanceXZMeters: distXZ,
                terminalState: result.TerminalState.ToString(),
                obReason: obReason,
                finalSurface: finalSurface,
                penaltyStrokes: penaltyStrokes);
        }

        /// <summary>
        /// Test seam: builds a ShotRecord from plain primitives without controller dependencies.
        /// Exercises the math (XZ distance computation) and field round-trip in EditMode tests.
        /// </summary>
        public static ShotRecord BuildShotRecordStatic(
            int shotNumber, string clubLabel,
            Vector3 origin, Vector3 finalPos,
            string terminalState, string obReason, string finalSurface)
        {
            float dx = finalPos.x - origin.x;
            float dz = finalPos.z - origin.z;
            float distXZ = Mathf.Sqrt(dx * dx + dz * dz);
            return new ShotRecord(shotNumber, clubLabel, origin, finalPos, distXZ,
                                  terminalState, obReason, finalSurface);
        }

        /// <summary>
        /// §2e: test seam overload with penaltyStrokes parameter.
        /// </summary>
        public static ShotRecord BuildShotRecordStatic(
            int shotNumber, string clubLabel,
            Vector3 origin, Vector3 finalPos,
            string terminalState, string obReason, string finalSurface,
            int penaltyStrokes)
        {
            float dx = finalPos.x - origin.x;
            float dz = finalPos.z - origin.z;
            float distXZ = Mathf.Sqrt(dx * dx + dz * dz);
            return new ShotRecord(shotNumber, clubLabel, origin, finalPos, distXZ,
                                  terminalState, obReason, finalSurface, penaltyStrokes);
        }
    }
}
