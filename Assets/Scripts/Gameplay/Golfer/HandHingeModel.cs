// golfer_club_grip SPEC §3.12.2 — the hinge-model hand.
//
// EXPERIMENT, OPT-IN ONLY. Everything below the #if is compiled out unless the scripting define
// GOLFIN_GOLFER_TEST is present (same gate and same reason as GolferPresenter). Without it the
// component is an inert shell so a prefab that references it still deserializes. The serialized
// capture is HandHingeData (its OWN file — Unity binds a ScriptableObject to its MonoScript by
// file name; declared here it saved with m_Script: {fileID: 0} and lost its type on the next
// domain reload). Sits in Golfin.Physics.Viewer via the sibling .asmref.
//
// WHAT IS DIFFERENT FROM EVERY WRAP BEFORE IT (§3.12.1). Each finger joint bends about an axis that
// is FIXED IN ITS OWN LOCAL SPACE, captured once from the prefab's rest pose, and every pose is
// rest * spread * flex — never a rotation composed onto whatever the clip left. A joint therefore
// cannot leave its own plane, and the next joint's axis cannot drift because the previous one
// moved. The clip's finger animation is discarded; the wrist is never touched here.
//
// Sign conventions, stated once:
//   • palm normal   = OUT OF THE PALM (palmar side). From the triangle (Hand, IndexProximal,
//                     LittleProximal), sign fixed by the thumb: ThumbProximal − IndexProximal has a
//                     positive component along it (the thumb base is palmar).
//   • hinge axis    = cross(childDir, palmNormal), joint-local. +flex carries the child TOWARD
//                     the palm (rotating d about d×n by +θ moves d onto n).
//   • abduct axis   = the palm normal, joint-local, applied at the MCP only. Literal §3.12.2; the
//                     sign is therefore MIRROR-ANTISYMMETRIC: on the RIGHT hand +spread moves the
//                     index toward the thumb, on the LEFT hand away from it. Stage 0 uses 0.
//   • thumb         = ThumbProximal is a 2-DOF aim (FromToRotation of the rest thumb direction
//                     onto a direction given in Hand-local space; Vector3.zero = rest), the other
//                     two thumb joints are plain fixed hinges. No bisection on the thumb.

using UnityEngine;

namespace Golfin.Gameplay.Golfer
{
#if GOLFIN_GOLFER_TEST
    /// <summary>One finger joint's rest state and its two fixed local axes.</summary>
    [System.Serializable]
    public struct HingeJointData
    {
        public HumanBodyBones bone;
        public string boneName;
        public Quaternion restLocalRotation;
        /// <summary>Joint-local. +flex rotates the child toward the palm.</summary>
        public Vector3 hingeAxisLocal;
        /// <summary>Joint-local. The palm normal.</summary>
        public Vector3 abductAxisLocal;
        /// <summary>Joint → first child, metres, in the rest pose.</summary>
        public float segmentLength;
    }

    /// <summary>One hand: palm frame in Hand-local space plus its 15 hinge joints.</summary>
    [System.Serializable]
    public struct HandHingeHand
    {
        public bool right;
        public string handBoneName;
        /// <summary>Out of the palm (palmar side), Hand-local.</summary>
        public Vector3 palmNormalHandLocal;
        /// <summary>Wrist → middle MCP, projected into the palm plane, Hand-local.</summary>
        public Vector3 lengthAxisHandLocal;
        /// <summary>Index MCP → little MCP, projected into the palm plane, Hand-local.</summary>
        public Vector3 acrossAxisHandLocal;
        /// <summary>ThumbProximal → ThumbIntermediate at rest, in ThumbProximal's local frame.</summary>
        public Vector3 thumbRestDirJointLocal;
        /// <summary>The same direction, Hand-local (so an aim can be written relative to it).</summary>
        public Vector3 thumbRestDirHandLocal;
        /// <summary>Thumb, Index, Middle, Ring, Little × Proximal, Intermediate, Distal = 15.</summary>
        public HingeJointData[] joints;
    }

    /// <summary>Rest-relative degrees for one finger. Spread acts at the MCP only.</summary>
    [System.Serializable]
    public struct FingerFlex
    {
        public float mcp, pip, dip, spread;
        public FingerFlex(float mcp, float pip, float dip, float spread = 0f)
        { this.mcp = mcp; this.pip = pip; this.dip = dip; this.spread = spread; }
    }

    /// <summary>A whole hand, rest-relative.</summary>
    [System.Serializable]
    public struct HandPose
    {
        public FingerFlex index, middle, ring, little;
        public float thumbFlexIntermediate, thumbFlexDistal;
        /// <summary>Hand-local target direction for ThumbProximal; zero = leave it at rest.</summary>
        public Vector3 thumbAimHandLocal;

        /// <summary>The same triple on all four fingers, spread 0 — the stage-0 fist.</summary>
        public static HandPose Uniform(float mcp, float pip, float dip, float thumbInter, float thumbDistal)
        {
            var f = new FingerFlex(mcp, pip, dip);
            return new HandPose { index = f, middle = f, ring = f, little = f,
                                  thumbFlexIntermediate = thumbInter, thumbFlexDistal = thumbDistal };
        }
    }

    /// <summary>
    /// The mechanism. Static capture / apply so an EditMode test or an editor tool can drive a
    /// prefab out of play mode; the component itself only re-applies a serialized pose every
    /// LateUpdate once a later stage puts it on the prefab (stage 0 does not).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandHingeModel : MonoBehaviour
    {
        /// <summary>Bumped by hand on every edit; read back by reflection to prove the build loaded.</summary>
        public const string Version = "stage0-b";

        [SerializeField] Animator anim;
        [SerializeField] HandHingeData data;
        [Tooltip("Off until a later stage: stage 0 is edit mode only.")]
        [SerializeField] bool applyEveryFrame = false;
        [SerializeField] HandPose lead;
        [SerializeField] HandPose trail;

        void LateUpdate()
        {
            if (!applyEveryFrame || data == null || anim == null) return;
            Apply(anim, data.left, lead);
            Apply(anim, data.right, trail);
        }

        // ── joint tables ───────────────────────────────────────────────────────────────
        public const int Thumb = 0, Index = 1, Middle = 2, Ring = 3, Little = 4;

        public static readonly HumanBodyBones[] LeftJoints =
        {
            HumanBodyBones.LeftThumbProximal,  HumanBodyBones.LeftThumbIntermediate,  HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal,  HumanBodyBones.LeftIndexIntermediate,  HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal,   HumanBodyBones.LeftRingIntermediate,   HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
        };
        public static readonly HumanBodyBones[] RightJoints =
        {
            HumanBodyBones.RightThumbProximal,  HumanBodyBones.RightThumbIntermediate,  HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexProximal,  HumanBodyBones.RightIndexIntermediate,  HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal,   HumanBodyBones.RightRingIntermediate,   HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
        };

        public static HumanBodyBones[] Joints(bool right) => right ? RightJoints : LeftJoints;
        public static HumanBodyBones HandBone(bool right) => right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;
        /// <summary>Index into <see cref="HandHingeHand.joints"/>: finger 0..4 × joint 0..2.</summary>
        public static int JointIndex(int finger, int k) => finger * 3 + k;

        // ── capture ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Capture one hand from the CURRENT pose of <paramref name="anim"/>'s hierarchy, which
        /// must be the rest pose (edit mode, no animator running). Throws on a missing bone
        /// rather than returning a half-hand.
        /// </summary>
        public static HandHingeHand Capture(Animator anim, bool right)
        {
            Transform hand = Bone(anim, HandBone(right));
            Transform idx  = Bone(anim, right ? HumanBodyBones.RightIndexProximal  : HumanBodyBones.LeftIndexProximal);
            Transform mid  = Bone(anim, right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            Transform lit  = Bone(anim, right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
            Transform th1  = Bone(anim, right ? HumanBodyBones.RightThumbProximal  : HumanBodyBones.LeftThumbProximal);
            Transform th2  = Bone(anim, right ? HumanBodyBones.RightThumbIntermediate : HumanBodyBones.LeftThumbIntermediate);

            // Palm normal: triangle (Hand, IndexProximal, LittleProximal), sign fixed by the thumb.
            Vector3 across = lit.position - idx.position;
            Vector3 along  = (idx.position + lit.position) * 0.5f - hand.position;
            Vector3 n = Vector3.Cross(along, across);
            if (n.sqrMagnitude < 1e-12f) throw new System.InvalidOperationException("HandHingeModel.Capture: degenerate palm triangle");
            n.Normalize();
            if (Vector3.Dot(th1.position - idx.position, n) < 0f) n = -n;

            Vector3 u = Vector3.ProjectOnPlane(mid.position - hand.position, n).normalized;
            Vector3 a = Vector3.ProjectOnPlane(across, n).normalized;
            Vector3 thumbDir = (th2.position - th1.position).normalized;

            var h = new HandHingeHand
            {
                right = right,
                handBoneName = hand.name,
                palmNormalHandLocal    = hand.InverseTransformDirection(n),
                lengthAxisHandLocal    = hand.InverseTransformDirection(u),
                acrossAxisHandLocal    = hand.InverseTransformDirection(a),
                thumbRestDirJointLocal = th1.InverseTransformDirection(thumbDir),
                thumbRestDirHandLocal  = hand.InverseTransformDirection(thumbDir),
                joints = new HingeJointData[15],
            };

            var table = Joints(right);
            for (int i = 0; i < table.Length; i++)
            {
                Transform j = Bone(anim, table[i]);
                if (j.childCount == 0) throw new System.InvalidOperationException("HandHingeModel.Capture: " + j.name + " has no child to define its direction");
                Vector3 d = j.GetChild(0).position - j.position;
                float len = d.magnitude;
                if (len < 1e-6f) throw new System.InvalidOperationException("HandHingeModel.Capture: zero-length segment at " + j.name);
                d /= len;
                Vector3 hinge = Vector3.Cross(d, n);
                if (hinge.sqrMagnitude < 1e-12f) throw new System.InvalidOperationException("HandHingeModel.Capture: " + j.name + " lies along the palm normal");
                h.joints[i] = new HingeJointData
                {
                    bone = table[i],
                    boneName = j.name,
                    restLocalRotation = j.localRotation,
                    hingeAxisLocal  = j.InverseTransformDirection(hinge.normalized),
                    abductAxisLocal = j.InverseTransformDirection(n),
                    segmentLength = len,
                };
            }
            return h;
        }

        /// <summary>Component-wise equality within <paramref name="tol"/> (idempotence test).</summary>
        public static bool Equal(in HandHingeHand x, in HandHingeHand y, float tol, out string firstDifference)
        {
            firstDifference = null;
            if (x.right != y.right) { firstDifference = "right"; return false; }
            if (!V(x.palmNormalHandLocal, y.palmNormalHandLocal, tol)) { firstDifference = "palmNormalHandLocal"; return false; }
            if (!V(x.lengthAxisHandLocal, y.lengthAxisHandLocal, tol)) { firstDifference = "lengthAxisHandLocal"; return false; }
            if (!V(x.acrossAxisHandLocal, y.acrossAxisHandLocal, tol)) { firstDifference = "acrossAxisHandLocal"; return false; }
            if (!V(x.thumbRestDirJointLocal, y.thumbRestDirJointLocal, tol)) { firstDifference = "thumbRestDirJointLocal"; return false; }
            if (!V(x.thumbRestDirHandLocal, y.thumbRestDirHandLocal, tol)) { firstDifference = "thumbRestDirHandLocal"; return false; }
            if (x.joints == null || y.joints == null || x.joints.Length != y.joints.Length) { firstDifference = "joints.Length"; return false; }
            for (int i = 0; i < x.joints.Length; i++)
            {
                var p = x.joints[i]; var q = y.joints[i];
                if (p.bone != q.bone) { firstDifference = p.boneName + ".bone"; return false; }
                if (!Q(p.restLocalRotation, q.restLocalRotation, tol)) { firstDifference = p.boneName + ".restLocalRotation"; return false; }
                if (!V(p.hingeAxisLocal, q.hingeAxisLocal, tol)) { firstDifference = p.boneName + ".hingeAxisLocal"; return false; }
                if (!V(p.abductAxisLocal, q.abductAxisLocal, tol)) { firstDifference = p.boneName + ".abductAxisLocal"; return false; }
                if (Mathf.Abs(p.segmentLength - q.segmentLength) > tol) { firstDifference = p.boneName + ".segmentLength"; return false; }
            }
            return true;
        }

        // ── apply ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// <c>localRotation = rest * AngleAxis(spread, abduct) * AngleAxis(flex, hinge)</c> on
        /// every finger joint; the thumb aim + two fixed hinges. Reads nothing from the current
        /// pose, so the result is the same whatever the clip left in the fingers.
        /// </summary>
        public static void Apply(Animator anim, in HandHingeHand hand, in HandPose pose)
        {
            Finger(anim, hand, Index,  pose.index);
            Finger(anim, hand, Middle, pose.middle);
            Finger(anim, hand, Ring,   pose.ring);
            Finger(anim, hand, Little, pose.little);
            ThumbAim(anim, hand, pose.thumbAimHandLocal);
            Hinge(anim, hand.joints[JointIndex(Thumb, 1)], pose.thumbFlexIntermediate, 0f);
            Hinge(anim, hand.joints[JointIndex(Thumb, 2)], pose.thumbFlexDistal, 0f);
        }

        /// <summary>Every joint back to its captured rest rotation.</summary>
        public static void ApplyRest(Animator anim, in HandHingeHand hand)
        {
            foreach (var j in hand.joints) Bone(anim, j.bone).localRotation = j.restLocalRotation;
        }

        public static void Finger(Animator anim, in HandHingeHand hand, int finger, in FingerFlex f)
        {
            Hinge(anim, hand.joints[JointIndex(finger, 0)], f.mcp, f.spread);
            Hinge(anim, hand.joints[JointIndex(finger, 1)], f.pip, 0f);
            Hinge(anim, hand.joints[JointIndex(finger, 2)], f.dip, 0f);
        }

        public static void Hinge(Animator anim, in HingeJointData j, float flexDeg, float spreadDeg)
        {
            Bone(anim, j.bone).localRotation = HingeRotation(j, flexDeg, spreadDeg);
        }

        /// <summary>The pure formula, for tests that want the number without a Transform.</summary>
        public static Quaternion HingeRotation(in HingeJointData j, float flexDeg, float spreadDeg) =>
            j.restLocalRotation
            * Quaternion.AngleAxis(spreadDeg, j.abductAxisLocal)
            * Quaternion.AngleAxis(flexDeg, j.hingeAxisLocal);

        /// <summary>
        /// ThumbProximal: <c>rest * FromToRotation(restDir, aim)</c>, both in the joint's rest-local
        /// frame. The aim is given Hand-local and carried through the joint's PARENT so it works
        /// whether or not the thumb hangs directly off the Hand bone.
        /// </summary>
        public static void ThumbAim(Animator anim, in HandHingeHand hand, Vector3 aimHandLocal)
        {
            var j = hand.joints[JointIndex(Thumb, 0)];
            Transform t = Bone(anim, j.bone);
            if (aimHandLocal.sqrMagnitude < 1e-12f) { t.localRotation = j.restLocalRotation; return; }
            Transform h = Bone(anim, HandBone(hand.right));
            Vector3 aimWorld  = h.TransformDirection(aimHandLocal.normalized);
            Vector3 aimParent = t.parent != null ? t.parent.InverseTransformDirection(aimWorld) : aimWorld;
            Vector3 aimRestLocal = Quaternion.Inverse(j.restLocalRotation) * aimParent;
            t.localRotation = j.restLocalRotation * Quaternion.FromToRotation(hand.thumbRestDirJointLocal, aimRestLocal);
        }

        // ── measurement (what the stage gates read) ────────────────────────────────────

        public struct FingerMetrics
        {
            public string name;
            public float tipToPalmPlaneM;       // signed along the palm normal; + = palm side
            public float pipToPalmPlaneM, dipToPalmPlaneM;
            public Vector3 tipWorld;
        }

        public struct HandMetrics
        {
            public bool right;
            public Vector3 palmNormalWorld, lengthAxisWorld, acrossAxisWorld, mcpCentroidWorld;
            public FingerMetrics[] fingers;      // Index, Middle, Ring, Little
            public float thumbTipToPalmPlaneM;
            public float[] adjacentTipSpacingM;  // I-M, M-R, R-L
            public bool[] adjacentCrossing;      // order along the across axis flips at PIP/DIP/tip
            public float[] adjacentMinSegmentM;  // closest approach of any two phalanges, per pair
            public float minAdjacentTipSpacingM;
            public bool anyCrossing;
        }

        /// <summary>
        /// Palm plane = through the four finger MCPs, normal = the captured palm normal carried
        /// by the Hand bone. Distances signed along that normal, positive on the palm side.
        /// </summary>
        public static HandMetrics Measure(Animator anim, in HandHingeHand hand)
        {
            Transform h = Bone(anim, HandBone(hand.right));
            var m = new HandMetrics
            {
                right = hand.right,
                palmNormalWorld = h.TransformDirection(hand.palmNormalHandLocal).normalized,
                lengthAxisWorld = h.TransformDirection(hand.lengthAxisHandLocal).normalized,
                acrossAxisWorld = h.TransformDirection(hand.acrossAxisHandLocal).normalized,
                fingers = new FingerMetrics[4],
                adjacentTipSpacingM = new float[3],
                adjacentCrossing = new bool[3],
                adjacentMinSegmentM = new float[3],
            };
            string[] names = { "index", "middle", "ring", "little" };
            var mcp = new Vector3[4]; var pip = new Vector3[4]; var dip = new Vector3[4]; var tip = new Vector3[4];
            for (int f = 0; f < 4; f++)
            {
                mcp[f] = Bone(anim, hand.joints[JointIndex(f + 1, 0)].bone).position;
                pip[f] = Bone(anim, hand.joints[JointIndex(f + 1, 1)].bone).position;
                Transform d = Bone(anim, hand.joints[JointIndex(f + 1, 2)].bone);
                dip[f] = d.position;
                tip[f] = d.GetChild(0).position;
            }
            m.mcpCentroidWorld = (mcp[0] + mcp[1] + mcp[2] + mcp[3]) * 0.25f;
            Vector3 n = m.palmNormalWorld;
            for (int f = 0; f < 4; f++)
                m.fingers[f] = new FingerMetrics
                {
                    name = names[f],
                    tipToPalmPlaneM = Vector3.Dot(tip[f] - m.mcpCentroidWorld, n),
                    pipToPalmPlaneM = Vector3.Dot(pip[f] - m.mcpCentroidWorld, n),
                    dipToPalmPlaneM = Vector3.Dot(dip[f] - m.mcpCentroidWorld, n),
                    tipWorld = tip[f],
                };
            Transform thumbDistal = Bone(anim, hand.joints[JointIndex(Thumb, 2)].bone);
            m.thumbTipToPalmPlaneM = Vector3.Dot(thumbDistal.GetChild(0).position - m.mcpCentroidWorld, n);

            m.minAdjacentTipSpacingM = float.MaxValue;
            Vector3 a = m.acrossAxisWorld;
            for (int p = 0; p < 3; p++)
            {
                int f0 = p, f1 = p + 1;
                m.adjacentTipSpacingM[p] = (tip[f0] - tip[f1]).magnitude;
                m.minAdjacentTipSpacingM = Mathf.Min(m.minAdjacentTipSpacingM, m.adjacentTipSpacingM[p]);
                // MCP order along the across axis is the reference; a flip anywhere down the
                // finger means one finger has crossed over the other.
                float sign = Mathf.Sign(Vector3.Dot(mcp[f1] - mcp[f0], a));
                bool cross = Vector3.Dot(pip[f1] - pip[f0], a) * sign < 0f
                          || Vector3.Dot(dip[f1] - dip[f0], a) * sign < 0f
                          || Vector3.Dot(tip[f1] - tip[f0], a) * sign < 0f;
                m.adjacentCrossing[p] = cross;
                m.anyCrossing |= cross;
                float best = float.MaxValue;
                Vector3[] p0 = { mcp[f0], pip[f0], dip[f0], tip[f0] }, p1 = { mcp[f1], pip[f1], dip[f1], tip[f1] };
                for (int i = 0; i < 3; i++) for (int k = 0; k < 3; k++)
                    best = Mathf.Min(best, SegmentDistance(p0[i], p0[i + 1], p1[k], p1[k + 1]));
                m.adjacentMinSegmentM[p] = best;
            }
            return m;
        }

        /// <summary>Closest approach of two segments (Ericson, Real-Time Collision Detection 5.1.9).</summary>
        public static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            const float eps = 1e-12f;
            if (a <= eps && e <= eps) return r.magnitude;
            if (a <= eps) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= eps) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                    s = denom != 0f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            return ((p1 + d1 * s) - (p2 + d2 * t)).magnitude;
        }

        // ── helpers ────────────────────────────────────────────────────────────────────
        static Transform Bone(Animator anim, HumanBodyBones b)
        {
            Transform t = anim != null ? anim.GetBoneTransform(b) : null;
            if (t == null) throw new System.InvalidOperationException("HandHingeModel: humanoid bone " + b + " is not mapped on " + (anim != null ? anim.name : "<null>"));
            return t;
        }
        static bool V(Vector3 a, Vector3 b, float tol) => (a - b).sqrMagnitude <= tol * tol;
        static bool Q(Quaternion a, Quaternion b, float tol) =>
            Mathf.Abs(a.x - b.x) <= tol && Mathf.Abs(a.y - b.y) <= tol && Mathf.Abs(a.z - b.z) <= tol && Mathf.Abs(a.w - b.w) <= tol;
    }
#else
    /// <summary>GOLFIN_GOLFER_TEST is absent: an inert shell so a prefab referencing it still deserializes.</summary>
    [DisallowMultipleComponent]
    public sealed class HandHingeModel : MonoBehaviour { }
#endif
}
