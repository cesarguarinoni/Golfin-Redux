using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// putter_cone_per_shot_lifecycle — Piece 1 EditMode tests (Approach C).
    ///
    /// Approach C: PutterTrack is the actual putter aim viz and follows per-shot lifecycle.
    /// Iron cone (_coneGraphic) stays permanently disabled in putter mode.
    ///
    /// G1: PutterTrack hidden when shot fires (Resolving).
    /// G2: PutterTrack visible again at next Aiming.
    /// G3: Iron cone stays disabled across all states in putter mode (never re-enables).
    /// G4: Non-putter mode does not toggle PutterTrack (stays in whatever state it was set to).
    /// </summary>
    [TestFixture]
    public class PutterConeLifecycleTests
    {
        private GameObject         _rootGO;
        private ShotController     _sc;
        private SyntheticInputSource _input;
        private ShotConeView       _coneView;
        private ConeMeshGraphic    _coneMeshGraphic;
        private GameObject         _putterTrackGO;
        private ControlsConfig     _cfg;

        [SetUp]
        public void SetUp()
        {
            _rootGO = new GameObject("PutterConeTest_Root");

            // ShotController
            var scGO = new GameObject("ShotController");
            scGO.transform.SetParent(_rootGO.transform);
            _sc    = scGO.AddComponent<ShotController>();
            _input = new SyntheticInputSource();
            _cfg   = ControlsConfig.Default;
            _sc.InjectInputSource(_input);
            _sc.InjectConfig(_cfg);

            // ConeMeshGraphic (the iron cone ShotConeView permanently hides in putter mode)
            var coneGO = new GameObject("ConeGraphic");
            coneGO.transform.SetParent(_rootGO.transform);
            _coneMeshGraphic = coneGO.AddComponent<ConeMeshGraphic>();
            _coneMeshGraphic.enabled = true;  // start enabled (as it would be before putter mode)

            // PutterTrack GO (the actual putter aim viz, toggled per-shot)
            _putterTrackGO = new GameObject("PutterTrack");
            _putterTrackGO.transform.SetParent(_rootGO.transform);
            _putterTrackGO.SetActive(false);  // starts inactive (PhysicsLabController sets it active on EnterPutterMode)

            // ShotConeView — wire ShotController, ConeMeshGraphic, and PutterTrack via InjectForTests
            var coneViewGO = new GameObject("ShotConeView");
            coneViewGO.transform.SetParent(_rootGO.transform);
            _coneView = coneViewGO.AddComponent<ShotConeView>();
            _coneView.InjectForTests(_sc, _coneMeshGraphic, _putterTrackGO);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_rootGO);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        void SetPull(float pullPx, float lateralPx = 0f)
        {
            _input.TouchOriginPx   = new Vector2(540f, 1000f);
            _input.TouchPositionPx = new Vector2(540f + lateralPx, 1000f - pullPx);
            _input.IsTouching      = true;
        }

        void DriveToTiming(float pullPx = 170f)
        {
            SetPull(0f);
            _sc.Tick(0.016f);   // Idle → Aiming
            SetPull(35f);
            _sc.Tick(0.016f);   // Aiming → Pulling
            SetPull(pullPx);
            _sc.Tick(0.016f);   // Pulling → Timing
        }

        void FireShot()
        {
            _input.IsTouching            = false;
            _input.TouchVelocityPxPerSec = new Vector2(0f, 2000f);
            _sc.Tick(0.016f);   // Timing/Flicking → Resolving
        }

        void ResetToAiming()
        {
            // CompleteShot resets ShotController back to Idle; next touch → Aiming
            _sc.CompleteShot();
            SetPull(0f);
            _sc.Tick(0.016f);   // Idle → Aiming
        }

        // ── G1: PutterTrack hidden when shot fires (Resolving) ────────────────

        /// <summary>
        /// G1: In putter mode, PutterTrack must be hidden the moment the shot
        /// transitions to Resolving (ball is in flight).
        ///
        /// Lifecycle: Aiming (PutterTrack visible) → fire → Resolving (PutterTrack hidden).
        /// </summary>
        [Test]
        public void G1_PutterMode_PutterTrackHiddenOnResolving()
        {
            // Arrange: simulate EnterPutterMode — ShotConeView.SetPuttMode(true) +
            // PhysicsLabController sets _putterTrack active.
            _sc.IsPutt = true;
            _coneView.SetPuttMode(true);
            _putterTrackGO.SetActive(true);  // mirrors PhysicsLabController.EnterPutterMode

            // Act: drive to Timing (PutterTrack should be visible during aiming states)
            DriveToTiming(170f);

            Assert.IsTrue(_putterTrackGO.activeSelf,
                "G1: PutterTrack must be visible during Timing (putter aiming state)");

            // Act: fire the shot
            FireShot();

            // After fire, ShotController transitions to Resolving.
            // ShotConeView.HandleStateChanged → UpdatePutterTrackVisibility → SetActive(false).
            Assert.IsFalse(_putterTrackGO.activeSelf,
                "G1: PutterTrack must be hidden once shot fires (Resolving state) in putter mode");
        }

        // ── G2: PutterTrack visible again at next Aiming ──────────────────────

        /// <summary>
        /// G2: In putter mode, after a shot resolves and the player starts the next
        /// shot, PutterTrack must become visible again at Aiming.
        ///
        /// Lifecycle: Resolving (hidden) → CompleteShot → Aiming (visible again).
        /// Proves the lifecycle repeats across putts.
        /// </summary>
        [Test]
        public void G2_PutterMode_PutterTrackVisibleAgainAtNextAiming()
        {
            // Arrange: enter putter mode
            _sc.IsPutt = true;
            _coneView.SetPuttMode(true);
            _putterTrackGO.SetActive(true);

            // Act: fire a shot → PutterTrack hidden
            DriveToTiming(170f);
            FireShot();

            Assert.IsFalse(_putterTrackGO.activeSelf,
                "G2 precondition: PutterTrack must be hidden after shot fires");

            // Act: reset to next aiming
            ResetToAiming();

            // Assert: PutterTrack visible again for the next putt
            Assert.IsTrue(_putterTrackGO.activeSelf,
                "G2: PutterTrack must be visible again at Aiming state for the next putt");
        }

        // ── G3: Iron cone stays permanently disabled in putter mode ───────────

        /// <summary>
        /// G3: In putter mode, _coneGraphic.enabled must stay false across all
        /// shot states (Aiming, Timing, Resolving, and back to Aiming).
        /// The iron cone must never re-enable while in putter mode.
        /// </summary>
        [Test]
        public void G3_PutterMode_ConeGraphicStaysDisabledAcrossAllStates()
        {
            // Arrange: enter putter mode — SetPuttMode(true) disables _coneGraphic permanently
            _sc.IsPutt = true;
            _coneView.SetPuttMode(true);
            _putterTrackGO.SetActive(true);

            Assert.IsFalse(_coneMeshGraphic.enabled,
                "G3 precondition: cone must be disabled immediately after SetPuttMode(true)");

            // Drive through Aiming
            SetPull(0f);
            _sc.Tick(0.016f);   // Idle → Aiming
            Assert.IsFalse(_coneMeshGraphic.enabled,
                "G3: cone must stay disabled at Aiming in putter mode");

            // Drive through Timing
            SetPull(35f);
            _sc.Tick(0.016f);   // Aiming → Pulling
            SetPull(170f);
            _sc.Tick(0.016f);   // Pulling → Timing
            Assert.IsFalse(_coneMeshGraphic.enabled,
                "G3: cone must stay disabled at Timing in putter mode");

            // Fire → Resolving
            FireShot();
            Assert.IsFalse(_coneMeshGraphic.enabled,
                "G3: cone must stay disabled at Resolving in putter mode");

            // Reset → next Aiming
            ResetToAiming();
            Assert.IsFalse(_coneMeshGraphic.enabled,
                "G3: cone must stay disabled at next Aiming in putter mode");
        }

        // ── G4: Non-putter mode does not touch PutterTrack ───────────────────

        /// <summary>
        /// G4: In non-putter mode, state changes must NOT toggle PutterTrack.
        /// PutterTrack stays in whatever state PhysicsLabController set it to
        /// (inactive in non-putter mode).
        /// </summary>
        [Test]
        public void G4_NonPutterMode_PutterTrackUntouched()
        {
            // Arrange: non-putter mode (default). PutterTrack starts inactive.
            // _sc.IsPutt = false (default)
            // _putterTrackGO starts inactive (set in SetUp)
            Assert.IsFalse(_putterTrackGO.activeSelf,
                "G4 precondition: PutterTrack inactive in non-putter mode");

            // Act: drive through full shot lifecycle in non-putter mode
            DriveToTiming(170f);
            Assert.IsFalse(_putterTrackGO.activeSelf,
                "G4: PutterTrack must NOT be activated by non-putter Aiming states");

            FireShot();
            Assert.IsFalse(_putterTrackGO.activeSelf,
                "G4: PutterTrack must NOT be toggled by non-putter Resolving state");

            ResetToAiming();
            Assert.IsFalse(_putterTrackGO.activeSelf,
                "G4: PutterTrack must stay inactive across the full non-putter lifecycle");
        }
    }
}
