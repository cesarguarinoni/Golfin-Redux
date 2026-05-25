using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Golfin.Physics.Viewer;
using Golfin.Gameplay.Input;
using Golfin.Physics.Stats;
using Golfin.Physics.Math;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// F6 gate tests for the lab-vs-prod split introduced in live_stat_provider_wiring Phase 3.
    ///
    /// T1: SetClub MUST NOT inject a stat bundle. After SetClub, _statBundleOverridden must
    ///     remain false (so the StatProviderBus is free to resolve live stats for committed shots).
    ///
    /// T2: InjectLabBundleForCurrentClub MUST set _statBundleOverridden=true with the correct
    ///     lab club bundle (proves lab callers that call InjectLabBundleForCurrentClub explicitly
    ///     after SetClub continue to get the per-club neutral bundle behavior).
    /// </summary>
    public class PhysicsLabControllerLabVsProdTests
    {
        // ── Reflection helpers ───────────────────────────────────────────────────

        /// <summary>Reads ShotController._statBundleOverridden via reflection.</summary>
        static bool GetStatBundleOverridden(ShotController sc)
        {
            var f = typeof(ShotController).GetField(
                "_statBundleOverridden",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, "_statBundleOverridden field not found — was it renamed?");
            return (bool)f.GetValue(sc);
        }

        /// <summary>Reads ShotController._statBundle via reflection.</summary>
        static StatBundle GetStatBundle(ShotController sc)
        {
            var f = typeof(ShotController).GetField(
                "_statBundle",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, "_statBundle field not found — was it renamed?");
            return (StatBundle)f.GetValue(sc);
        }

        /// <summary>
        /// Injects _shotController into PhysicsLabController via reflection (the field is
        /// [SerializeField] private; no InjectForTests API exposes it currently).
        /// </summary>
        static void InjectShotController(PhysicsLabController ctrl, ShotController sc)
        {
            var f = typeof(PhysicsLabController).GetField(
                "_shotController",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, "_shotController field not found — was it renamed?");
            f.SetValue(ctrl, sc);
        }

        // ── Teardown ─────────────────────────────────────────────────────────────

        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
            _root = null;
        }

        // ── Test 1: SetClub must NOT set _statBundleOverridden ───────────────────

        [Test]
        public void SetClub_DoesNotInjectStatBundle()
        {
            // Arrange
            _root = new GameObject("LabVsProd_T1");
            var ctrl = _root.AddComponent<PhysicsLabController>();
            var scGO = new GameObject("SC_T1");
            scGO.transform.SetParent(_root.transform);
            var sc = scGO.AddComponent<ShotController>();

            // Inject ShotController into the lab controller.
            InjectShotController(ctrl, sc);

            // Pre-condition: _statBundleOverridden starts false.
            Assert.IsFalse(GetStatBundleOverridden(sc),
                "Pre-condition: _statBundleOverridden must be false before SetClub.");

            // Act: call SetClub with Driver (index 0).
            ctrl.SetClub(0);

            // Assert: SetClub must NOT have flipped _statBundleOverridden to true.
            Assert.IsFalse(GetStatBundleOverridden(sc),
                "SetClub(0) must NOT set _statBundleOverridden — lab-vs-prod split (F1).");

            // Act: also verify with Putter (PutterIndex) — the other branch.
            ctrl.SetClub(PhysicsLabController.PutterIndex);

            Assert.IsFalse(GetStatBundleOverridden(sc),
                "SetClub(PutterIndex) must NOT set _statBundleOverridden — lab-vs-prod split (F1).");
        }

        // ── Test 2: InjectLabBundleForCurrentClub sets the override ──────────────

        [Test]
        public void InjectLabBundleForCurrentClub_SetsOverrideAndBundle()
        {
            // Arrange
            _root = new GameObject("LabVsProd_T2");
            var ctrl = _root.AddComponent<PhysicsLabController>();
            var scGO = new GameObject("SC_T2");
            scGO.transform.SetParent(_root.transform);
            var sc = scGO.AddComponent<ShotController>();

            InjectShotController(ctrl, sc);

            // Act: SetClub then InjectLabBundleForCurrentClub — the explicit lab-caller contract.
            ctrl.SetClub(0); // Driver — _statBundleOverridden stays false
            Assert.IsFalse(GetStatBundleOverridden(sc),
                "After SetClub(0), override must still be false.");

            ctrl.InjectLabBundleForCurrentClub(); // explicitly opt into lab bundle

            // Assert: override is now true (lab bundle is injected).
            Assert.IsTrue(GetStatBundleOverridden(sc),
                "After InjectLabBundleForCurrentClub(), _statBundleOverridden must be true.");

            // Assert: the bundle is a swing bundle (Club set, Putter absent).
            StatBundle bundle = GetStatBundle(sc);
            Assert.IsTrue(bundle.Club.HasValue,
                "Lab swing bundle must have a Club (not null).");
            Assert.IsFalse(bundle.Putter.HasValue,
                "Lab swing bundle must NOT have a Putter.");
            Assert.AreEqual(BallStats.Neutral, bundle.Ball,
                "Lab bundle must use BallStats.Neutral.");

            // Act: switch to Putter and inject lab bundle.
            sc.ClearStatBundleOverride(); // reset
            ctrl.SetClub(PhysicsLabController.PutterIndex);
            Assert.IsFalse(GetStatBundleOverridden(sc),
                "After SetClub(PutterIndex), override must still be false.");

            ctrl.InjectLabBundleForCurrentClub();

            // Assert: override is true; bundle is a putt bundle (Putter set, Club absent).
            Assert.IsTrue(GetStatBundleOverridden(sc),
                "After InjectLabBundleForCurrentClub() for putter, _statBundleOverridden must be true.");
            StatBundle puttBundle = GetStatBundle(sc);
            Assert.IsTrue(puttBundle.Putter.HasValue,
                "Lab putter bundle must have a Putter.");
            Assert.IsFalse(puttBundle.Club.HasValue,
                "Lab putter bundle must NOT have a Club.");
        }
    }
}
