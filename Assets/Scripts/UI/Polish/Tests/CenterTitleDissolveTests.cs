// ─────────────────────────────────────────────────────────────────────────────
// game_polish_a §D3 — the top-bar centre title dissolves instead of snapping.
//
// WHY THESE TESTS EXIST, and they are scar tissue rather than coverage-for-its-
// own-sake. The dissolve shipped BROKEN once: the CanvasGroup it animates was
// resolved with
//
//     usernameText.GetComponent<CanvasGroup>() ?? usernameText.AddComponent<…>()
//
// and `??` does not consult Unity's overloaded `== null`, so on a label with no
// group it handed back a FAKE-NULL component instead of adding a real one.
// Nothing threw. `UiMotion.Fade` treats a null group by returning an EMPTY
// routine, so both halves of the dissolve silently did nothing while the log
// still cheerfully reported the dissolve starting — and the title went on
// hard-cutting one frame after the push, exactly as before the fix.
//
// The bug was invisible to every gate the pipeline had: it produces no error, no
// exception, no failing assertion, and a screenshot of the REST state is
// pixel-identical either way. Only a frame-by-frame read of the transition
// showed it. So the fix is pinned HERE, where it costs nothing to check:
// the group must be a REAL component, by Unity's own null operator.
//
// ASSEMBLY: the reflection arrangement of UiMotionTests — PersistentUIManager
// lives in Assembly-CSharp and a named test assembly cannot reference it.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Golfin.UI.Polish.Tests
{
    [TestFixture]
    public class CenterTitleDissolveTests
    {
        static Type T => Probe.Type("Golfin.UI.PersistentUIManager");

        GameObject? _host;
        GameObject? _label;

        /// <summary>A manager with only the one field the dissolve touches wired.</summary>
        Component NewManager()
        {
            _host  = new GameObject("PersistentUIManager_test");
            _host.SetActive(false);            // never let Awake run the singleton dance
            var mgr = _host.AddComponent(T);

            _label = new GameObject("UsernameText_test");
            var tmp = _label.AddComponent<TextMeshProUGUI>();
            tmp.text = "MODE SELECTION";
            T.GetField("usernameText", BindingFlags.Public | BindingFlags.Instance)!
             .SetValue(mgr, tmp);
            return mgr;
        }

        [TearDown]
        public void TearDown()
        {
            if (_label != null) UnityEngine.Object.DestroyImmediate(_label);
            if (_host  != null) UnityEngine.Object.DestroyImmediate(_host);
            _label = null; _host = null;
        }

        static object Invoke(Component mgr, string name, params object[] args) =>
            T.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(mgr, args);

        [Test]
        public void EnsureCenterTextGroup_AddsARealComponent_NotAFakeNull()
        {
            Component mgr = NewManager();

            var group = (CanvasGroup)Invoke(mgr, "EnsureCenterTextGroup");

            // Unity's operator, NOT `is null` / ReferenceEquals — a fake-null passes those and
            // is exactly what the `??` version returned.
            Assert.IsFalse(group == null, "EnsureCenterTextGroup returned a fake-null CanvasGroup");
            Assert.IsTrue(_label!.GetComponent<CanvasGroup>() == group,
                          "the group is not the one living on the label");
        }

        [Test]
        public void EnsureCenterTextGroup_IsIdempotent_AndNeverStacksGroups()
        {
            Component mgr = NewManager();

            var a = (CanvasGroup)Invoke(mgr, "EnsureCenterTextGroup");
            var b = (CanvasGroup)Invoke(mgr, "EnsureCenterTextGroup");

            Assert.AreSame(a, b, "a second call built a second group");
            Assert.AreEqual(1, _label!.GetComponents<CanvasGroup>().Length,
                            "more than one CanvasGroup on the label");
        }

        [Test]
        public void TheGroupRestsFullyOpaque_SoTheRestPixelsAreUnchanged()
        {
            Component mgr = NewManager();

            var group = (CanvasGroup)Invoke(mgr, "EnsureCenterTextGroup");

            // A2's parity gate measures the shell's REST pixels. A runtime-added group is only
            // invisible to that gate while it rests at 1 — and blocksRaycasts must stay at the
            // default, which is how the label behaved before it had a group at all.
            Assert.AreEqual(1f, group.alpha, 1e-4f, "the group does not rest opaque");
            Assert.IsTrue(group.blocksRaycasts, "blocksRaycasts changed the label's hit behaviour");
        }

        [Test]
        public void CenterTextFor_IsTheOneResolver_SharedWithTheInstantPaint()
        {
            Component mgr = NewManager();
            Type idT = Probe.Type("GolfinRedux.UI.ScreenId");

            // The dissolve and ApplyTopBarCenterText must agree about what a title IS; that is the
            // entire reason CenterTextFor was split out of ApplyTopBarCenterText.
            object modeSelection = Enum.Parse(idT, "ModeSelection");
            var viaResolver = (string)Invoke(mgr, "CenterTextFor", modeSelection);

            Invoke(mgr, "ApplyTopBarCenterText", modeSelection);
            var viaInstantPaint = ((TMP_Text)T.GetField("usernameText",
                BindingFlags.Public | BindingFlags.Instance)!.GetValue(mgr)).text;

            Assert.AreEqual(viaResolver, viaInstantPaint,
                            "the dissolve and the instant paint disagree about the title");
        }

        [Test]
        public void ApplyTopBarCenterText_ForcesTheGroupBackToOpaque()
        {
            Component mgr = NewManager();
            Type idT = Probe.Type("GolfinRedux.UI.ScreenId");

            var group = (CanvasGroup)Invoke(mgr, "EnsureCenterTextGroup");
            group.alpha = 0.37f;                       // a push interrupted mid-dissolve

            Invoke(mgr, "ApplyTopBarCenterText", Enum.Parse(idT, "ModeSelection"));

            Assert.AreEqual(1f, group.alpha, 1e-4f,
                            "an interrupted dissolve left the title stranded translucent");
        }
    }
}
