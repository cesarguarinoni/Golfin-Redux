// golfer_club_grip SPEC §3.12.2 — EditMode tests for HandHingeModel, (a)–(d), both hands.
//
// The prefab is opened with PrefabUtility.LoadPrefabContents: an isolated preview instance in its
// FBX bind pose (fingers straight, hand flat), never an open scene, never saved. With the define
// OFF the model is an inert shell and the single test below asserts exactly that, so the sweep is
// green in both configurations (SPEC: "these run with the define off too").

using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Golfin.Gameplay.Golfer.Tests
{
#if GOLFIN_GOLFER_TEST
    [TestFixture]
    public class HandHingeModelTests
    {
        const string PrefabPath = "Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_MixamoNative.prefab";

        GameObject _root;
        Animator _anim;

        [SetUp]
        public void Load()
        {
            _root = PrefabUtility.LoadPrefabContents(PrefabPath);
            Assert.That(_root, Is.Not.Null, PrefabPath);
            _anim = _root.GetComponentInChildren<Animator>(true);
            Assert.That(_anim, Is.Not.Null, "Animator");
            Assert.That(_anim.isHuman, Is.True, "humanoid avatar");
        }

        [TearDown]
        public void Unload()
        {
            if (_root != null) PrefabUtility.UnloadPrefabContents(_root);
        }

        // (a) — 90° at the index MCP carries the PIP toward the palm by ≥ 0.8 × L and ≤ 3 mm sideways.
        [TestCase(false)]
        [TestCase(true)]
        public void A_IndexProximal90_MovesIntermediateTowardPalm_NotSideways(bool right)
        {
            var hand = HandHingeModel.Capture(_anim, right);
            var mcpJ = hand.joints[HandHingeModel.JointIndex(HandHingeModel.Index, 0)];
            Transform mcp = _anim.GetBoneTransform(mcpJ.bone);
            Transform pip = _anim.GetBoneTransform(hand.joints[HandHingeModel.JointIndex(HandHingeModel.Index, 1)].bone);
            Transform h   = _anim.GetBoneTransform(HandHingeModel.HandBone(right));

            Vector3 n = h.TransformDirection(hand.palmNormalHandLocal);
            Vector3 hingeWorld = mcp.TransformDirection(mcpJ.hingeAxisLocal);   // rest — the axis the joint turns about
            Vector3 before = pip.position;
            float L = mcpJ.segmentLength;

            HandHingeModel.Hinge(_anim, mcpJ, 90f, 0f);

            Vector3 disp = pip.position - before;
            float towardPalm = Vector3.Dot(disp, n);
            float sideways   = Mathf.Abs(Vector3.Dot(disp, hingeWorld));
            Assert.That(towardPalm, Is.GreaterThanOrEqualTo(0.8f * L), $"toward palm {towardPalm:F5} m vs 0.8 × L = {0.8f * L:F5}");
            Assert.That(sideways, Is.LessThanOrEqualTo(0.003f), $"sideways {sideways * 1000f:F3} mm");
            // and the segment did not change length
            Assert.That((pip.position - mcp.position).magnitude, Is.EqualTo(L).Within(1e-5f));
        }

        // (b) left / (c) right — 60/80/40 on all four fingers: adjacent tips ≥ 8 mm apart, every tip palm-side of the MCP plane.
        [TestCase(false)]
        [TestCase(true)]
        public void BC_Fist_60_80_40_TipsApart_AndPalmSide(bool right)
        {
            var hand = HandHingeModel.Capture(_anim, right);
            HandHingeModel.Apply(_anim, hand, HandPose.Uniform(60f, 80f, 40f, 0f, 0f));
            var m = HandHingeModel.Measure(_anim, hand);

            for (int p = 0; p < 3; p++)
                Assert.That(m.adjacentTipSpacingM[p], Is.GreaterThanOrEqualTo(0.008f), $"pair {p} spacing {m.adjacentTipSpacingM[p] * 1000f:F2} mm");
            foreach (var f in m.fingers)
                Assert.That(f.tipToPalmPlaneM, Is.GreaterThan(0f), $"{f.name} tip {f.tipToPalmPlaneM * 1000f:F2} mm from the MCP plane (must be palm side)");
            Assert.That(m.anyCrossing, Is.False, "a finger crossed its neighbour");
        }

        // (d) — capture is idempotent: same instance twice, and after apply→rest, to 1e-5.
        [TestCase(false)]
        [TestCase(true)]
        public void D_CaptureIsIdempotent(bool right)
        {
            var first = HandHingeModel.Capture(_anim, right);
            var second = HandHingeModel.Capture(_anim, right);
            Assert.That(HandHingeModel.Equal(first, second, 1e-5f, out string diff), Is.True, "second capture differs at " + diff);

            HandHingeModel.Apply(_anim, first, HandPose.Uniform(65f, 85f, 40f, 30f, 20f));
            HandHingeModel.ApplyRest(_anim, first);
            var third = HandHingeModel.Capture(_anim, right);
            Assert.That(HandHingeModel.Equal(first, third, 1e-5f, out diff), Is.True, "capture after apply→rest differs at " + diff);

            // a fresh instance of the same prefab captures the same data
            var other = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var fourth = HandHingeModel.Capture(other.GetComponentInChildren<Animator>(true), right);
                Assert.That(HandHingeModel.Equal(first, fourth, 1e-5f, out diff), Is.True, "fresh-instance capture differs at " + diff);
            }
            finally { PrefabUtility.UnloadPrefabContents(other); }
        }

        // Sign convention pinned: the palm normal is palmar (the thumb base lies on its positive side).
        [TestCase(false)]
        [TestCase(true)]
        public void PalmNormal_IsPalmar_ByTheThumb(bool right)
        {
            var hand = HandHingeModel.Capture(_anim, right);
            Transform h = _anim.GetBoneTransform(HandHingeModel.HandBone(right));
            Transform idx = _anim.GetBoneTransform(hand.joints[HandHingeModel.JointIndex(HandHingeModel.Index, 0)].bone);
            Transform th  = _anim.GetBoneTransform(hand.joints[HandHingeModel.JointIndex(HandHingeModel.Thumb, 0)].bone);
            Vector3 n = h.TransformDirection(hand.palmNormalHandLocal);
            Assert.That(n.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(Vector3.Dot(th.position - idx.position, n), Is.GreaterThan(0f));
        }
    }
#else
    [TestFixture]
    public class HandHingeModelTests
    {
        [Test]
        public void DefineOff_ModelCompilesAsAnInertShell()
        {
            const System.Reflection.BindingFlags own = System.Reflection.BindingFlags.DeclaredOnly
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
            Assert.That(typeof(HandHingeModel).GetMethods(own).Length, Is.EqualTo(0), "HandHingeModel must be empty without GOLFIN_GOLFER_TEST");
            Assert.That(typeof(HandHingeData).GetFields(own).Length, Is.EqualTo(0), "HandHingeData must be empty without GOLFIN_GOLFER_TEST");
        }
    }
#endif
}
