using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// §2d — HoleCompleteDriver integration tests (4 required).
    ///
    /// ISOLATION NOTE: GameSession and HoleContext are static classes with global state.
    /// Every test MUST call ResetForNewHole() and HoleContext.Reset() in [SetUp].
    ///
    /// Test seam: HoleCompleteDriver.InjectForTests(BallStateMachine, HoleCompleteWidget)
    /// bypasses GetComponentInParent lookup. HoleCompleteWidget is built from real
    /// GameObjects with all child references wired programmatically.
    /// </summary>
    public class HoleCompleteDriverTests
    {
        // GameObjects created in SetUp, destroyed in TearDown.
        GameObject _driverGO;
        GameObject _widgetGO;
        GameObject _rootGO;
        GameObject _card1GO;
        GameObject _card2GO;
        List<GameObject> _allGOs = new List<GameObject>();

        HoleCompleteDriver    _driver;
        HoleCompleteWidget    _widget;
        HoleCompleteCardWidget _card1;
        HoleCompleteCardWidget _card2;
        BallStateMachine      _sm;

        [SetUp]
        public void SetUp()
        {
            // Reset static bus state.
            GameSession.ResetForNewHole();
            HoleContext.Reset();

            // Suppress Unity's 'ShouldRunBehaviour()' assert that fires on
            // AddComponent<MonoBehaviour> in EditMode. Each AddComponent call
            // generates one assert log; expect them to avoid test failures.
            // We create: HoleCompleteCardWidget x2, HoleCompleteWidget x1,
            //            HoleCompleteDriver x1, Button x3+3 = potentially many asserts.
            // ignoreFailingMessages covers all unhandled log messages in this scope.
            LogAssert.ignoreFailingMessages = true;

            // Build minimal widget hierarchy.
            _widgetGO = new GameObject("HoleCompleteWidget");
            _rootGO   = new GameObject("Root");
            _rootGO.transform.SetParent(_widgetGO.transform);

            _card1GO = new GameObject("Card1");
            _card1GO.transform.SetParent(_rootGO.transform);

            _card2GO = new GameObject("Card2");
            _card2GO.transform.SetParent(_rootGO.transform);

            // Add canvas components so Button works (not strictly needed for unit tests
            // that don't test button clicks, but prevents null refs in HookButton).
            _card1 = _card1GO.AddComponent<HoleCompleteCardWidget>();
            _card2 = _card2GO.AddComponent<HoleCompleteCardWidget>();

            // Build header sub-GOs for card1 so IsSuccessHeaderActive / IsFailedHeaderActive etc. work.
            WireCardHeaders(_card1, _card1GO);
            WireCardHeaders(_card2, _card2GO);

            _widget = _widgetGO.AddComponent<HoleCompleteWidget>();
            // Manually inject references using Unity-style reflection-free approach:
            // We use the internal properties via script-level access (same assembly boundary).
            SetWidgetRefs(_widget, _rootGO, _card1, _card2);

            // Call Awake manually (Unity doesn't auto-call in EditMode).
            _widget.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);

            // Build driver.
            _driverGO = new GameObject("HoleCompleteDriver");
            _driver = _driverGO.AddComponent<HoleCompleteDriver>();

            // Build a minimal BallStateMachine (uses NullCupDetector + ConstantSurfaceProvider).
            var surfaceProvider = new ConstantSurfaceProvider(SurfaceType.Green);
            _sm = new BallStateMachine(surfaceProvider, new NullCupDetector());
            _sm.Headless = true;

            // Inject without going through GetComponentInParent.
            _driver.InjectForTests(_sm, _widget);

            _allGOs.Add(_driverGO);
            _allGOs.Add(_widgetGO);
            // Note: ignoreFailingMessages stays true through the test body
            // to suppress any deferred 'ShouldRunBehaviour' asserts from UI components.
            // TearDown resets it.
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;

            foreach (var go in _allGOs)
                if (go != null) GameObject.DestroyImmediate(go);
            _allGOs.Clear();

            GameSession.ResetForNewHole();
            HoleContext.Reset();
        }

        // ── Test 6: InCup + at-par → Success header + REPLAY ──────────────────────

        [Test]
        public void HoleCompleteDriver_OnInCupTerminal_AtPar_ShowsSuccessReplay()
        {
            // Arrange: TurnCount=4, Par=4 → score=0 → SUCCESS.
            GameSession.SetTurn(4);
            HoleContext.Par = 4;

            // Act: fire OnShotComplete with InCup terminal.
            FireShotComplete(BallState.InCup);

            // Assert
            Assert.IsTrue(_widget.IsShowing,
                "Widget should be visible after InCup terminal.");
            Assert.IsTrue(_card1.IsSuccessHeaderActive,
                "Card1 SUCCESS header should be active when score=0.");
            Assert.IsFalse(_card1.IsFailedHeaderActive,
                "Card1 FAILED header should be inactive when score=0.");
            Assert.IsTrue(_card1.IsReplayButtonActive,
                "Card1 REPLAY button should be visible for success state.");
            Assert.IsFalse(_card1.IsRetryButtonActive,
                "Card1 RETRY button should be hidden for success state.");

            // Card 2: success → not locked → NEXT header visible, PLAY visible.
            Assert.IsTrue(_card2.IsNextHeaderActive,
                "Card2 NEXT header should be active when success (not locked).");
            Assert.IsFalse(_card2.IsLockedHeaderActive,
                "Card2 LOCKED header should be inactive for success.");
        }

        // ── Test 7: InCup + over-par → Failed header + RETRY + Card 2 LOCKED ──────

        [Test]
        public void HoleCompleteDriver_OnInCupTerminal_OverPar_ShowsFailedRetryAndLockedNext()
        {
            // Arrange: TurnCount=5, Par=4 → score=+1 → FAILED, no PB.
            GameSession.SetTurn(5);
            HoleContext.Par = 4;

            // Act
            FireShotComplete(BallState.InCup);

            // Assert
            Assert.IsTrue(_widget.IsShowing,
                "Widget should be visible after InCup with over-par score.");
            Assert.IsFalse(_card1.IsSuccessHeaderActive,
                "Card1 SUCCESS header should be inactive when score>0.");
            Assert.IsTrue(_card1.IsFailedHeaderActive,
                "Card1 FAILED header should be active when score>0.");
            Assert.IsTrue(_card1.IsRetryButtonActive,
                "Card1 RETRY button should be visible for Failed+no-PB state.");
            Assert.IsFalse(_card1.IsReplayButtonActive,
                "Card1 REPLAY button should be hidden for Failed+no-PB state.");

            // Card 2: failed + no PB → LOCKED.
            Assert.IsFalse(_card2.IsNextHeaderActive,
                "Card2 NEXT header should be inactive when locked.");
            Assert.IsTrue(_card2.IsLockedHeaderActive,
                "Card2 LOCKED header should be active when Failed+no-PB.");
            Assert.IsTrue(_card2.IsDarkenOverlayActive,
                "Card2 darken overlay should be active when locked.");
        }

        // ── Test 8: AtRest terminal → modal NOT shown ─────────────────────────────

        [Test]
        public void HoleCompleteDriver_OnAtRestTerminal_DoesNotShowModal()
        {
            // Arrange
            GameSession.SetTurn(3);
            HoleContext.Par = 4;

            // Act: fire OnShotComplete with AtRest terminal.
            FireShotComplete(BallState.AtRest);

            // Assert: widget should remain hidden.
            Assert.IsFalse(_widget.IsShowing,
                "Widget must NOT show when terminal state is AtRest.");
        }

        // ── Test 9: OB terminal → modal NOT shown ──────────────────────────────────

        [Test]
        public void HoleCompleteDriver_OnOBTerminal_DoesNotShowModal()
        {
            // Arrange
            GameSession.SetTurn(2);
            HoleContext.Par = 4;

            // Act: fire OB terminal.
            FireShotComplete(BallState.OB);

            // Assert: widget should remain hidden.
            Assert.IsFalse(_widget.IsShowing,
                "Widget must NOT show when terminal state is OB.");
        }

        // ── Stage B Test 6: InCup → GameSession.OnHoleComplete fires with correct payload ──

        [Test]
        public void HoleCompleteDriver_OnInCupTerminal_FiresMarkHoleComplete()
        {
            // Arrange
            GameSession.SetTurn(3);
            HoleContext.Par         = 4;
            HoleContext.HoleNumber  = 11;
            // CurrentHoleNumber stays 0 (set in SeedSession only) — fallback to HoleContext.

            HoleCompletionData? received = null;
            int fireCount = 0;
            System.Action<HoleCompletionData> listener = d =>
            {
                received = d;
                fireCount++;
            };
            GameSession.OnHoleComplete += listener;

            try
            {
                // Act
                FireShotComplete(BallState.InCup);

                // Assert
                Assert.AreEqual(1, fireCount,
                    "MarkHoleComplete must fire exactly once on InCup terminal.");
                Assert.IsTrue(received.HasValue, "Listener must have received a payload.");
                Assert.AreEqual(BallState.InCup, received.Value.TerminalState,
                    "Payload TerminalState must match ShotResult terminal.");
                Assert.AreEqual(3, received.Value.Strokes,
                    "Payload Strokes must equal GameSession.TurnCount at fire time.");
                Assert.AreEqual(0, received.Value.PenaltyStrokes,
                    "Payload PenaltyStrokes must equal sum of ShotHistory penalties (none here).");
                Assert.AreEqual(11, received.Value.HoleNumber,
                    "Payload HoleNumber must fall back to HoleContext.HoleNumber when CurrentHoleNumber is 0.");
            }
            finally
            {
                GameSession.OnHoleComplete -= listener;
            }
        }

        // ── ScoreLabelFor table-driven bonus test ─────────────────────────────────

        [Test]
        public void ScoreLabelFor_KnownValues_ReturnsExpectedLabel()
        {
            Assert.AreEqual("Albatross",    HoleCompleteDriver.ScoreLabelFor(-3));
            Assert.AreEqual("Eagle",        HoleCompleteDriver.ScoreLabelFor(-2));
            Assert.AreEqual("Birdie",       HoleCompleteDriver.ScoreLabelFor(-1));
            Assert.AreEqual("Par",          HoleCompleteDriver.ScoreLabelFor(0));
            Assert.AreEqual("Bogey",        HoleCompleteDriver.ScoreLabelFor(1));
            Assert.AreEqual("Double Bogey", HoleCompleteDriver.ScoreLabelFor(2));
            Assert.AreEqual("Triple Bogey", HoleCompleteDriver.ScoreLabelFor(3));
            Assert.AreEqual("+4",           HoleCompleteDriver.ScoreLabelFor(4));
            Assert.AreEqual("-4",           HoleCompleteDriver.ScoreLabelFor(-4));
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// Fire the driver's HandleShotComplete logic directly, bypassing the full BallSimulation.
        void FireShotComplete(BallState terminalState)
        {
            var result = new ShotResult(
                terminalState: terminalState,
                obReason:      terminalState == BallState.OB
                                   ? (OBReason?)OBReason.Water
                                   : null,
                startPosition: fp3.Zero,
                endPosition:   fp3.Zero,
                startSurface:  SurfaceType.Green,
                endSurface:    SurfaceType.Green,
                simDuration:   fp.Zero,
                bounceCount:   0
            );

            // Use the internal test seam to bypass SM event dispatch.
            _driver.HandleShotCompleteForTest(result);
        }

        /// Wire minimal header + button GameObjects onto a card widget.
        static void WireCardHeaders(HoleCompleteCardWidget card, GameObject cardGO)
        {
            var successHeader = new GameObject("SuccessHeader");
            successHeader.transform.SetParent(cardGO.transform);

            var failedHeader = new GameObject("FailedHeader");
            failedHeader.transform.SetParent(cardGO.transform);

            var nextHeader = new GameObject("NextHeader");
            nextHeader.transform.SetParent(cardGO.transform);

            var lockedHeader = new GameObject("LockedHeader");
            lockedHeader.transform.SetParent(cardGO.transform);

            var darkenOverlay = new GameObject("DarkenOverlay");
            darkenOverlay.transform.SetParent(cardGO.transform);

            // Buttons — add Image component so Button.targetGraphic doesn't crash.
            var replayBtnGO = new GameObject("ReplayButton");
            replayBtnGO.transform.SetParent(cardGO.transform);
            replayBtnGO.AddComponent<UnityEngine.RectTransform>();
            var replayImg = replayBtnGO.AddComponent<UnityEngine.UI.Image>();
            var replayBtn = replayBtnGO.AddComponent<UnityEngine.UI.Button>();
            replayBtn.targetGraphic = replayImg;

            var retryBtnGO = new GameObject("RetryButton");
            retryBtnGO.transform.SetParent(cardGO.transform);
            retryBtnGO.AddComponent<UnityEngine.RectTransform>();
            var retryImg = retryBtnGO.AddComponent<UnityEngine.UI.Image>();
            var retryBtn = retryBtnGO.AddComponent<UnityEngine.UI.Button>();
            retryBtn.targetGraphic = retryImg;

            var playBtnGO = new GameObject("PlayButton");
            playBtnGO.transform.SetParent(cardGO.transform);
            playBtnGO.AddComponent<UnityEngine.RectTransform>();
            var playImg = playBtnGO.AddComponent<UnityEngine.UI.Image>();
            var playBtn = playBtnGO.AddComponent<UnityEngine.UI.Button>();
            playBtn.targetGraphic = playImg;

            SetSerializedField(card, "_successHeaderRoot", successHeader);
            SetSerializedField(card, "_failedHeaderRoot",  failedHeader);
            SetSerializedField(card, "_nextHeaderRoot",    nextHeader);
            SetSerializedField(card, "_lockedHeaderRoot",  lockedHeader);
            SetSerializedField(card, "_darkenOverlay",     darkenOverlay);
            SetSerializedField(card, "_replayButton",      replayBtn);
            SetSerializedField(card, "_retryButton",       retryBtn);
            SetSerializedField(card, "_playButton",        playBtn);
        }

        static void SetWidgetRefs(HoleCompleteWidget widget, GameObject root,
            HoleCompleteCardWidget card1, HoleCompleteCardWidget card2)
        {
            SetSerializedField(widget, "_root",  root);
            SetSerializedField(widget, "_card1", card1);
            SetSerializedField(widget, "_card2", card2);
        }

        static void SetSerializedField(UnityEngine.Object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }
            throw new InvalidOperationException($"Field '{fieldName}' not found on {target.GetType().Name}");
        }
    }
}
