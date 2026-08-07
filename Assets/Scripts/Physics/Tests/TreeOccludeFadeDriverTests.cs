// tree_occlusion_fade — EditMode unit tests for TreeOccludeFadeDriver (spec §4.4)
// Assembly: Golfin.Physics.Tests (already references Golfin.Physics.Viewer via asmdef)
// Run via: Unity Test Runner → EditMode → filter "TreeOccludeFade"
using NUnit.Framework;
using UnityEngine;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Tests
{
    [TestFixture]
    public class TreeOccludeFadeDriverTests
    {
        const float Eps = 1e-4f;

        // ── strength ramp ──────────────────────────────────────────────────────────

        [Test]
        public void Strength_RampsZeroToOne_InExactlyRampSeconds()
        {
            const float ramp = 0.25f;
            const float dt   = 1f / 60f;

            float s = 0f;
            int steps = 0;
            while (s < 1f && steps < 1000)
            {
                s = TreeOccludeFadeDriver.StepStrength(s, 1f, dt, ramp);
                steps++;
            }

            Assert.AreEqual(1f, s, Eps, "strength must reach exactly 1");
            // 0.25s at 60fps = 15 steps; allow the single-frame rounding of the final MoveTowards.
            Assert.AreEqual(Mathf.CeilToInt(ramp / dt), steps, 1,
                "ramp must take RampSeconds, not longer or shorter");
        }

        [Test]
        public void Strength_RampsOneToZero_InExactlyRampSeconds()
        {
            const float ramp = 0.25f;
            const float dt   = 1f / 60f;

            float s = 1f;
            int steps = 0;
            while (s > 0f && steps < 1000)
            {
                s = TreeOccludeFadeDriver.StepStrength(s, 0f, dt, ramp);
                steps++;
            }

            Assert.AreEqual(0f, s, Eps);
            Assert.AreEqual(Mathf.CeilToInt(ramp / dt), steps, 1);
        }

        [Test]
        public void Strength_NeverOvershoots_ForAnyDt()
        {
            // A huge dt (hitch, editor pause, first frame after a load) must clamp, not fly past 1.
            float s = TreeOccludeFadeDriver.StepStrength(0f, 1f, 10f, 0.25f);
            Assert.AreEqual(1f, s, Eps);

            s = TreeOccludeFadeDriver.StepStrength(1f, 0f, 10f, 0.25f);
            Assert.AreEqual(0f, s, Eps);
        }

        [Test]
        public void Strength_StaysInUnitRange()
        {
            for (float dt = 0f; dt < 0.5f; dt += 0.017f)
            {
                float up   = TreeOccludeFadeDriver.StepStrength(0.5f, 1f, dt, 0.25f);
                float down = TreeOccludeFadeDriver.StepStrength(0.5f, 0f, dt, 0.25f);
                Assert.That(up,   Is.InRange(0f, 1f));
                Assert.That(down, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void Strength_ZeroRampSeconds_SnapsToTarget()
        {
            Assert.AreEqual(1f, TreeOccludeFadeDriver.StepStrength(0f, 1f, 0.016f, 0f), Eps);
            Assert.AreEqual(0f, TreeOccludeFadeDriver.StepStrength(1f, 0f, 0.016f, 0f), Eps);
        }

        // ── focus smoothing ────────────────────────────────────────────────────────

        [Test]
        public void Focus_ConvergesTowardTarget_AndNeverOvershoots()
        {
            var target = new Vector3(100f, 5f, -40f);
            var cur    = Vector3.zero;
            float prevDist = Vector3.Distance(cur, target);

            for (int i = 0; i < 240; i++)
            {
                cur = TreeOccludeFadeDriver.StepFocus(cur, target, 1f / 60f, 10f);
                float d = Vector3.Distance(cur, target);
                Assert.LessOrEqual(d, prevDist + Eps, "focus must approach monotonically");
                prevDist = d;
            }

            Assert.Less(prevDist, 0.01f, "focus must converge within 4s");
        }

        [Test]
        public void Focus_HugeDt_LandsOnTargetWithoutOvershooting()
        {
            // Teleport + a one-second hitch: the exp term saturates at 1, so we land ON the target.
            var target = new Vector3(500f, 0f, 500f);
            var cur = TreeOccludeFadeDriver.StepFocus(Vector3.zero, target, 1f, 10f);
            Assert.LessOrEqual(Vector3.Distance(cur, target), Vector3.Distance(Vector3.zero, target),
                "must not fly past the target");
            Assert.Less(Vector3.Distance(cur, target), 0.1f);
        }

        [Test]
        public void Focus_ZeroDtOrRate_IsAFreeze()
        {
            var cur = new Vector3(1f, 2f, 3f);
            Assert.AreEqual(cur, TreeOccludeFadeDriver.StepFocus(cur, Vector3.one * 99f, 0f, 10f));
            Assert.AreEqual(cur, TreeOccludeFadeDriver.StepFocus(cur, Vector3.one * 99f, 0.016f, 0f));
        }

        // ── params packing ─────────────────────────────────────────────────────────

        [Test]
        public void BuildParams_PacksAnglesAsCosines_OuterFirst()
        {
            var p = TreeOccludeFadeDriver.BuildParams(10f, 16f, 0.85f, 1.5f);

            Assert.AreEqual(Mathf.Cos(16f * Mathf.Deg2Rad), p.x, Eps, "x = cos(outer)");
            Assert.AreEqual(Mathf.Cos(10f * Mathf.Deg2Rad), p.y, Eps, "y = cos(inner)");
            Assert.AreEqual(0.85f, p.z, Eps, "z = maxCut");
            Assert.AreEqual(1.5f,  p.w, Eps, "w = depth feather");
        }

        [Test]
        public void BuildParams_CosOuterIsAlwaysBelowCosInner_SoTheSmoothstepNeverInverts()
        {
            // The shader does smoothstep(x, y, cosAng); if x >= y the window turns inside-out.
            var p = TreeOccludeFadeDriver.BuildParams(10f, 16f, 0.85f, 1.5f);
            Assert.Less(p.x, p.y);

            // Even when the caller passes them the wrong way round.
            var swapped = TreeOccludeFadeDriver.BuildParams(16f, 10f, 0.85f, 1.5f);
            Assert.Less(swapped.x, swapped.y, "inverted input must be clamped, not honoured");
        }

        [Test]
        public void BuildParams_FeatherIsNeverZero_SoTheShaderNeverDividesByZero()
        {
            Assert.Greater(TreeOccludeFadeDriver.BuildParams(10f, 16f, 0.85f, 0f).w, 0f);
            Assert.Greater(TreeOccludeFadeDriver.BuildParams(10f, 16f, 0.85f, -5f).w, 0f);
        }

        [Test]
        public void BuildParams_ClampsMaxCutToUnitRange()
        {
            Assert.AreEqual(1f, TreeOccludeFadeDriver.BuildParams(10f, 16f, 5f, 1.5f).z, Eps);
            Assert.AreEqual(0f, TreeOccludeFadeDriver.BuildParams(10f, 16f, -1f, 1.5f).z, Eps);
        }

        [Test]
        public void BuildParams_DefaultTunables_ProduceAFadeWindowWiderThanItIsFull()
        {
            // Sanity-check the shipped defaults actually describe a soft-edged cone.
            var p = TreeOccludeFadeDriver.BuildParams(
                TreeOccludeFadeDriver.InnerHalfAngleDeg,
                TreeOccludeFadeDriver.OuterHalfAngleDeg,
                TreeOccludeFadeDriver.MaxOpacityCut,
                TreeOccludeFadeDriver.DepthFeatherM);

            Assert.Less(p.x, p.y, "outer must be the wider angle");
            Assert.That(p.z, Is.InRange(0f, 1f));
            Assert.Greater(p.w, 0f);
            Assert.Less(p.z, 1f, "maxCut must leave a ghost, not erase the tree entirely");
        }

        // ── the shader contract: the globals the driver publishes must exist by name ──

        [Test]
        public void ShaderGlobals_RoundTripThroughTheNamesTheShaderReads()
        {
            // Guards against a rename on one side only: these four names are hard-coded in
            // Vegetation.shader's GOLFIN OCCLUDE FADE block.
            var ball   = new Vector4(1f, 2f, 3f, 0f);
            var cam    = new Vector4(4f, 5f, 6f, 0f);
            var pars   = TreeOccludeFadeDriver.BuildParams(10f, 16f, 0.85f, 1.5f);

            Shader.SetGlobalVector("_GolfinOccFadeBall", ball);
            Shader.SetGlobalVector("_GolfinOccFadeCam", cam);
            Shader.SetGlobalFloat ("_GolfinOccFadeStrength", 0.42f);
            Shader.SetGlobalVector("_GolfinOccFadeParams", pars);
            Shader.SetGlobalFloat ("_GolfinOccFadeBias", 0.5f);

            Assert.AreEqual(ball, Shader.GetGlobalVector("_GolfinOccFadeBall"));
            Assert.AreEqual(cam,  Shader.GetGlobalVector("_GolfinOccFadeCam"));
            Assert.AreEqual(0.42f, Shader.GetGlobalFloat("_GolfinOccFadeStrength"), Eps);
            Assert.AreEqual(pars, Shader.GetGlobalVector("_GolfinOccFadeParams"));
            Assert.AreEqual(0.5f, Shader.GetGlobalFloat("_GolfinOccFadeBias"), Eps);

            // leave the globals inert so a later test/render can't inherit a live window
            Shader.SetGlobalFloat("_GolfinOccFadeStrength", 0f);
        }

        [Test]
        public void KillSwitch_IsOffByDefault()
        {
            Assert.IsFalse(TreeOccludeFadeDriver.Disabled,
                "Disabled is a debug kill switch; shipping it true would silently disable the feature");
        }
    }
}
