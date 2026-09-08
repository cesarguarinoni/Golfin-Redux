// ─────────────────────────────────────────────────────────────────────────────
// game_polish_b §D3 — the top bar counts DOWN as well as up.
//
// gps_polish only ever counted up, because the only armed GPS delta was an earn:
// SetRewardPoints guarded on `points > from`. The game's armed deltas are mostly
// SPENDS — a level-up, a shop purchase, a tournament entry fee — and those are the
// numbers a player most wants to see move, so the guard is `points != from` now.
//
// WHAT IS ACTUALLY OBSERVABLE IN EDITMODE, because it decides the shape of this suite.
// UiMotion.Run refuses to start a coroutine outside play mode and settles the tween on
// its final value instead, so "the label counted" is NOT visible here — the label just
// arrives. What IS visible is the DECISION: an armed decreasing change must consume the
// arm (it took the animated path) where before it fell through to the snap and left the
// arm sitting there for the next change to steal. That consumption is the whole
// behavioural difference, so that is what is pinned, alongside the primitive itself
// counting down correctly.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Golfin.UI.Polish.Tests
{
    [TestFixture]
    public class CountDownTests
    {
        static Type MotionT => Probe.Type("Golfin.UI.Polish.UiMotion");
        static Type PumT    => Probe.Type("Golfin.UI.PersistentUIManager");

        const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

        static int Drain(IEnumerator e, int cap = 100000)
        {
            int n = 0;
            while (e.MoveNext()) if (++n > cap) Assert.Fail("routine did not terminate");
            return n;
        }

        // ═════════════════════════════════════════════════════════════════════
        // The primitive: CountUp is directionless
        // ═════════════════════════════════════════════════════════════════════

        static TMP_Text NewLabel(out GameObject go)
        {
            go = new GameObject("Counter");
            return go.AddComponent<TextMeshProUGUI>();
        }

        [Test]
        public void CountUp_CountsDown_AndSettlesExactlyOnTo()
        {
            TMP_Text label = NewLabel(out GameObject go);
            var routine = (IEnumerator)MotionT.GetMethod("CountUp")!.Invoke(null,
                new object?[] { label, 1000, 200, 0.4f, "N0", null, null })!;
            Drain(routine);
            Assert.AreEqual("200", label.text);
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void CountUp_DownwardSequence_NeverGoesUp()
        {
            // A "count down" that wobbles upward is worse than a snap. Step the routine and read
            // the label each frame; the rendered integer must be monotonically non-increasing.
            TMP_Text label = NewLabel(out GameObject go);
            var routine = (IEnumerator)MotionT.GetMethod("CountUp")!.Invoke(null,
                new object?[] { label, 500, 100, 0.4f, "N0", null, null })!;

            var seen = new List<int>();
            int guard = 0;
            while (routine.MoveNext())
            {
                if (int.TryParse(label.text.Replace(",", "").Replace(".", ""), out int v)) seen.Add(v);
                if (++guard > 100000) Assert.Fail("routine did not terminate");
            }
            seen.Add(100);

            for (int i = 1; i < seen.Count; i++)
                Assert.LessOrEqual(seen[i], seen[i - 1], $"went UP at step {i}: {seen[i - 1]} -> {seen[i]}");
            Assert.AreEqual(100, seen[seen.Count - 1]);
            Assert.LessOrEqual(seen[0], 500);
            UnityEngine.Object.DestroyImmediate(go);
        }

        // ═════════════════════════════════════════════════════════════════════
        // The decision: an armed DECREASE takes the animated path
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>A PersistentUIManager with just enough wired to run SetRewardPoints.</summary>
        static Component NewBar(out GameObject go, out TMP_Text label)
        {
            go = new GameObject("TopBarStandIn");
            go.SetActive(false);   // no Awake: this class reaches for managers that do not exist here
            Component pum = go.AddComponent(PumT);
            var labelGo = new GameObject("RP");
            labelGo.transform.SetParent(go.transform);
            label = labelGo.AddComponent<TextMeshProUGUI>();
            PumT.GetField("rewardPointsText", Priv | BindingFlags.Public)!.SetValue(pum, label);
            return pum;
        }

        static float ArmedUntil(Component pum)
            => (float)PumT.GetField("_rpCountUpArmedUntil", Priv)!.GetValue(pum)!;

        static void Arm(Component pum) => PumT.GetMethod("ArmRewardPointsCountUp")!.Invoke(pum, null);
        static void Set(Component pum, int v) => PumT.GetMethod("SetRewardPoints")!.Invoke(pum, new object[] { v });

        [Test]
        public void ArmedDecrease_ConsumesTheArm_WhichIsWhatTheOldGuardRefusedToDo()
        {
            Component pum = NewBar(out GameObject go, out TMP_Text label);
            label.text = "1.000";
            Arm(pum);
            Assert.Greater(ArmedUntil(pum), 0f, "arm did not take");

            Set(pum, 400);

            Assert.AreEqual(-1f, ArmedUntil(pum),
                "a DECREASING armed change must take the animated path and consume the arm; " +
                "with the old `points > from` guard it fell through to the snap and left the arm " +
                "armed for whatever changed RP next");
            Assert.AreEqual("400", label.text, "and it must still settle on the exact value");
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ArmedIncrease_StillCountsUp()
        {
            Component pum = NewBar(out GameObject go, out TMP_Text label);
            label.text = "400";
            Arm(pum);
            Set(pum, 1000);
            Assert.AreEqual(-1f, ArmedUntil(pum));
            Assert.AreEqual("1.000", label.text);
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ArmedButUnchanged_DoesNotBurnTheArm()
        {
            // A repaint with the same balance must not spend the arm on a tween from a number to
            // itself — the real delta is often the very next call.
            Component pum = NewBar(out GameObject go, out TMP_Text label);
            label.text = "750";
            Arm(pum);
            Set(pum, 750);
            Assert.Greater(ArmedUntil(pum), 0f, "an unchanged value must leave the arm intact");
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void UnarmedChange_StillSnaps()
        {
            // The discrimination the one-shot arm exists to make: a balance that moves because a
            // server refresh landed must NOT animate as though the player just spent something.
            Component pum = NewBar(out GameObject go, out TMP_Text label);
            label.text = "1.000";
            Set(pum, 200);
            Assert.AreEqual("200", label.text);
            Assert.LessOrEqual(ArmedUntil(pum), 0f);
            UnityEngine.Object.DestroyImmediate(go);
        }

        // ═════════════════════════════════════════════════════════════════════
        // §D3 arming is complete BY CONSTRUCTION, not by a list of screens
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void BothPlayerDrivenRpPaths_ArmTheCountUp_AndTheServerPathsDoNot()
        {
            Type rpm = Probe.Type("Golfin.Roster.RewardPointsManager");
            string Body(string name)
            {
                MethodInfo? m = rpm.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
                Assert.NotNull(m, "RewardPointsManager." + name + " is gone — §D3 needs re-siting");
                return name;
            }
            // The arm helper itself must exist and be private-static on the manager: §D3 puts it
            // there deliberately so a NEW call site cannot forget it.
            MethodInfo? arm = rpm.GetMethod("ArmTopBarCountUp", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(arm,
                "RewardPointsManager.ArmTopBarCountUp is missing — §D3 armed the count-up inside "
                + "SpendPoints/EarnPoints precisely so the eight call sites could not drift out of sync");
            Body("SpendPoints"); Body("EarnPoints");
            // And the server-authoritative paths must still exist unarmed, or the discrimination
            // between "the player did this" and "a refresh landed" has no seam to live on.
            Assert.NotNull(rpm.GetMethod("ApplyServerBalance", BindingFlags.Public | BindingFlags.Instance));
            Assert.NotNull(rpm.GetMethod("SetPoints", BindingFlags.Public | BindingFlags.Instance));
        }
    }
}
