using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.Controls;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// control_scheme_seam §6.4 / §6.5 — the persisted setting and the root host.
    ///
    /// <para>The setting is a WIRE FORMAT (it is stamped on every <c>shot_taken</c> row), so the
    /// tests that matter are the ones about bad input: an absent pref and a garbage pref must
    /// both read as Flick, because a player whose PlayerPrefs got scrambled must still be able
    /// to take a shot.</para>
    /// </summary>
    [TestFixture]
    public class ControlSchemeServiceTests
    {
        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(ControlSchemeService.PrefKey);
            ControlSchemeService.ResetCacheForTests();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(ControlSchemeService.PrefKey);
            ControlSchemeService.ResetCacheForTests();
        }

        [Test]
        public void NoPref_ReadsAsFlick()
        {
            Assert.AreEqual(ControlScheme.Flick, ControlSchemeService.Current);
        }

        [Test]
        public void GarbagePref_ReadsAsFlick()
        {
            PlayerPrefs.SetInt(ControlSchemeService.PrefKey, 99);
            ControlSchemeService.ResetCacheForTests();
            Assert.AreEqual(ControlScheme.Flick, ControlSchemeService.Current);

            PlayerPrefs.SetInt(ControlSchemeService.PrefKey, -3);
            ControlSchemeService.ResetCacheForTests();
            Assert.AreEqual(ControlScheme.Flick, ControlSchemeService.Current);
        }

        [Test]
        public void Set_PersistsAndRaisesOnce()
        {
            int raised = 0;
            ControlScheme seen = ControlScheme.Flick;
            System.Action<ControlScheme> h = s => { raised++; seen = s; };
            ControlSchemeService.OnSchemeChanged += h;
            try
            {
                ControlSchemeService.Set(ControlScheme.Needle, "settings");

                Assert.AreEqual(1, raised, "exactly one change event");
                Assert.AreEqual(ControlScheme.Needle, seen);
                Assert.AreEqual(ControlScheme.Needle, ControlSchemeService.Current);
                Assert.AreEqual((int)ControlScheme.Needle,
                    PlayerPrefs.GetInt(ControlSchemeService.PrefKey, -1),
                    "the choice must survive a crash, not just a clean quit");
            }
            finally { ControlSchemeService.OnSchemeChanged -= h; }
        }

        [Test]
        public void SettingTheSameValue_RaisesNothing()
        {
            ControlSchemeService.Set(ControlScheme.Pendulum, "settings");

            int raised = 0;
            System.Action<ControlScheme> h = _ => raised++;
            ControlSchemeService.OnSchemeChanged += h;
            try
            {
                ControlSchemeService.Set(ControlScheme.Pendulum, "ingame");
                Assert.AreEqual(0, raised, "re-selecting the live scheme is not a change");
            }
            finally { ControlSchemeService.OnSchemeChanged -= h; }
        }

        [Test]
        public void DetailedEvent_CarriesFromToAndSource()
        {
            ControlScheme from = ControlScheme.FreeSwing, to = ControlScheme.FreeSwing;
            string where = null;
            System.Action<ControlScheme, ControlScheme, string> h = (f, t, w) => { from = f; to = t; where = w; };
            ControlSchemeService.OnSchemeChangedDetailed += h;
            try
            {
                ControlSchemeService.Set(ControlScheme.FreeSwing, "ingame");
                Assert.AreEqual(ControlScheme.Flick, from);
                Assert.AreEqual(ControlScheme.FreeSwing, to);
                Assert.AreEqual("ingame", where, "telemetry must be able to tell the two surfaces apart");
            }
            finally { ControlSchemeService.OnSchemeChangedDetailed -= h; }
        }

        [Test]
        public void LabelKeys_AreKeysNotLiterals_AndNeedleReadsAsTapTiming()
        {
            Assert.AreEqual("SETTINGS_CONTROLS_FLICK",     ControlSchemeService.LabelKey(ControlScheme.Flick));
            Assert.AreEqual("SETTINGS_CONTROLS_PENDULUM",  ControlSchemeService.LabelKey(ControlScheme.Pendulum));
            Assert.AreEqual("SETTINGS_CONTROLS_TAPTIMING", ControlSchemeService.LabelKey(ControlScheme.Needle));
            Assert.AreEqual("SETTINGS_CONTROLS_FREESWING", ControlSchemeService.LabelKey(ControlScheme.FreeSwing));
        }

        [Test]
        public void EnumValues_AreTheWireFormat()
        {
            // These ints are persisted and shipped on every shot_taken row. Renumbering them
            // silently re-labels every historical row in the dashboard.
            Assert.AreEqual(0, (int)ControlScheme.Flick);
            Assert.AreEqual(1, (int)ControlScheme.Pendulum);
            Assert.AreEqual(2, (int)ControlScheme.Needle);
            Assert.AreEqual(3, (int)ControlScheme.FreeSwing);
        }
    }

    /// <summary>control_scheme_seam §6.5 — the host never swaps mid-swing, and an
    /// unimplemented scheme keeps the flick root live.</summary>
    [TestFixture]
    public class ShotSchemeHostTests
    {
        private GameObject     _hostGo;
        private GameObject     _scGo;
        private ShotController _sc;
        private GameObject[]   _roots;
        private ShotSchemeHost _host;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(ControlSchemeService.PrefKey);
            ControlSchemeService.ResetCacheForTests();

            _scGo = new GameObject("SchemeHostTests_SC");
            _sc   = _scGo.AddComponent<ShotController>();

            _roots = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                _roots[i] = new GameObject($"SchemeRoot_{(ControlScheme)i}");
                if (i == 0) _roots[i].AddComponent<FlickSchemeDriver>();
                else        _roots[i].AddComponent<PlaceholderSchemeDriver>();
                _roots[i].SetActive(false);
            }

            // EditMode runs no MonoBehaviour lifecycle callbacks, so ConfigureForTests does the
            // OnEnable work explicitly rather than the test relying on a callback that never fires.
            _hostGo = new GameObject("ShotSchemeHost");
            _host = _hostGo.AddComponent<ShotSchemeHost>();
            _host.ConfigureForTests(_roots, _sc);
        }

        [TearDown]
        public void TearDown()
        {
            _host.ReleaseForTests();   // OnDisable does not fire in EditMode; the event is static
            Object.DestroyImmediate(_hostGo);
            Object.DestroyImmediate(_scGo);
            foreach (var r in _roots) if (r != null) Object.DestroyImmediate(r);
            PlayerPrefs.DeleteKey(ControlSchemeService.PrefKey);
            ControlSchemeService.ResetCacheForTests();
        }

        [Test]
        public void DefaultsToFlickRootActive()
        {
            Assert.IsTrue(_roots[0].activeSelf, "Flick root must be live by default");
            Assert.IsFalse(_roots[1].activeSelf);
            Assert.IsFalse(_roots[2].activeSelf);
            Assert.IsFalse(_roots[3].activeSelf);
            Assert.AreEqual(ControlScheme.Flick, _host.ActiveScheme);
        }

        [Test]
        public void UnimplementedScheme_KeepsFlickRootActive()
        {
            ControlSchemeService.Set(ControlScheme.Pendulum, "settings");

            Assert.AreEqual(ControlScheme.Pendulum, _host.ActiveScheme, "the choice is honoured");
            Assert.IsTrue(_roots[0].activeSelf,
                "an unimplemented scheme must leave the flick input live — a tester who picks it still plays");
            Assert.IsTrue(_roots[1].activeSelf, "its placeholder root is activated so it logs once");
        }

        [Test]
        public void ChangeDuringTiming_IsDeferredUntilIdle()
        {
            _sc.BeginExternalDrag();
            _sc.SetExternalPower(1f, 0f);
            Assert.AreEqual(ShotState.Timing, _sc.State);

            ControlSchemeService.Set(ControlScheme.Needle, "ingame");

            Assert.IsTrue(_host.HasPendingSwap, "a mid-swing change must be latched, not applied");
            Assert.AreEqual(ControlScheme.Flick, _host.ActiveScheme,
                "the shot in progress keeps the scheme it started on");

            // Completing the shot returns the controller to Idle, which is when the swap lands.
            _sc.CompleteShot();
            _sc.Tick(0.016f);   // PublishState -> OnStateChanged(Idle)

            Assert.IsFalse(_host.HasPendingSwap);
            Assert.AreEqual(ControlScheme.Needle, _host.ActiveScheme, "swap fires on the next Idle");
        }
    }

    /// <summary>control_scheme_seam §6.6 — every <c>shot_taken</c> row is stamped with the
    /// scheme it was played on, and a caller that predates schemes still produces a valid
    /// Flick row rather than a missing key.</summary>
    [TestFixture]
    public class ShotSchemeTelemetryKeyTests
    {
        private static ShotRecord AnyShot() => new ShotRecord(
            1, "Driver", Vector3.zero, new Vector3(200f, 0f, 0f),
            200f, "AtRest", null, "Fairway", 0, 0.9f, 1f);

        [Test]
        public void AppendShotTimingKeys_WritesTheScheme()
        {
            var payload = new Dictionary<string, object>();
            GameSession.AppendShotTimingKeys(payload, AnyShot(), (int)ControlScheme.Needle);

            Assert.IsTrue(payload.ContainsKey("scheme"), "shot_taken must carry the scheme");
            Assert.AreEqual((int)ControlScheme.Needle, payload["scheme"]);
        }

        [Test]
        public void AppendShotTimingKeys_DefaultsToFlick()
        {
            // The dashboard reads a row with no scheme as Flick, because that IS what those
            // testers played. The default here is the same statement, not a placeholder.
            var payload = new Dictionary<string, object>();
            GameSession.AppendShotTimingKeys(payload, AnyShot());

            Assert.AreEqual((int)ControlScheme.Flick, payload["scheme"]);
        }
    }
}
