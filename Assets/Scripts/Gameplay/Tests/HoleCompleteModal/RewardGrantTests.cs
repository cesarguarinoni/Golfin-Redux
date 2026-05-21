using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Session;

namespace Golfin.HoleCompleteModal.Tests
{
    // ── Reward tests ──────────────────────────────────────────────────────────

    /// <summary>
    /// Tests 11-12: Verify HoleCompleteModalController routes reward grants to the
    /// correct pool (first-clear vs replay) based on _wasReplay flag.
    ///
    /// EDITMODE LIFECYCLE NOTE: Unity does NOT call Awake/OnEnable in EditMode tests
    /// unless [ExecuteInEditMode] is present. HandleHoleComplete is called directly
    /// via the ModalTestHelper.InvokeHandleHoleComplete seam instead of via event.
    /// </summary>
    public class RewardGrantTests
    {
        MonoBehaviour        _modal;
        FakeProgressionStore _store;

        [SetUp]
        public void SetUp()
        {
            GameSession.ResetSession();
            var built = ModalTestHelper.Build();
            _modal = built.modal;
            _store = built.store;
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.ResetSession();
            if (_modal != null) UnityEngine.Object.DestroyImmediate(_modal.gameObject);
        }

        // ── Test 11: First clear uses rewards pool ─────────────────────────────

        [Test]
        public void RewardGrant_FirstClearUsesRewardsPool()
        {
            // Arrange: HasPlayed = false (not a replay).
            _store.SetHasPlayed(1, false);

            ModalTestHelper.InvokeHandleHoleComplete(_modal, ModalTestHelper.SuccessData(hole: 1));

            // Assert: _wasReplay must be false (first-clear path).
            Assert.IsFalse(ModalTestHelper.GetWasReplay(_modal),
                "First clear: HasPlayed=false → _wasReplay must be false → uses hole.rewards pool.");
            Assert.IsTrue(ModalTestHelper.GetLastSuccess(_modal),
                "Terminal=InCup → _lastSuccess must be true.");
        }

        // ── Test 12: Replay uses replayRewards pool ────────────────────────────

        [Test]
        public void RewardGrant_ReplayUsesReplayRewardsPool()
        {
            // Arrange: HasPlayed = true (replay).
            _store.SetHasPlayed(1, true);

            ModalTestHelper.InvokeHandleHoleComplete(_modal, ModalTestHelper.SuccessData(hole: 1));

            // Assert: _wasReplay must be true (replay path).
            Assert.IsTrue(ModalTestHelper.GetWasReplay(_modal),
                "Replay: HasPlayed=true → _wasReplay must be true → uses hole.replayRewards pool.");
            Assert.IsTrue(ModalTestHelper.GetLastSuccess(_modal),
                "Terminal=InCup → _lastSuccess must be true.");
        }
    }
}
