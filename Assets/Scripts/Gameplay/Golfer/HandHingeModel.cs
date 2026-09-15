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
        public const string Version = "stage2-c";

        [SerializeField] Animator anim;
        [SerializeField] HandHingeData data;
        [Tooltip("Off until a later stage: stage 0 is edit mode only.")]
        [SerializeField] bool applyEveryFrame = false;
        [SerializeField] HandPose lead;
        [SerializeField] HandPose trail;

        /// <summary>Read-only views for the stage tools (the fields stay serialized data).</summary>
        public HandHingeData Data => data;
        public HandPose Lead => lead;
        public HandPose Trail => trail;
        public bool ApplyEveryFrame => applyEveryFrame;

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

        // ── stage 1 — the shaft in hand space (§3.12.4) and the per-finger k solve (§3.12.3) ──
        //
        // Cross-section conventions at address, lead hand, written down once because every wrap
        // before this one got them from the animated pose and drifted:
        //   axis   = little-finger MCP → index (butt → head), `d`, in Hand-local space;
        //   +n     = the palm normal = the TRAIL side (the shaft sits between the palm and the
        //            trail hand); −n = the target side, where the back of the hand faces;
        //   −u     = the wrist / heel direction = "TOP" of the grip (the heel pad closes over it),
        //            +u = the direction the proximal phalanges run = under the grip.
        // "1 o'clock viewed from the butt" is therefore −u rotated 30° toward +n. The trail hand
        // is the mirror and uses the same formula (its palm faces the target, covering the lead
        // thumb), so one convention serves both.

        public const float ShaftRadiusM          = 0.013575f;                 // Grip mesh radius (iter-9b)
        public const float FingerHalfThicknessM  = 0.00718f;                  // iter-9b 6.83 mm × the 1.0516 real-size rescale (2026-09-15)
        public const float ContactM              = ShaftRadiusM + FingerHalfThicknessM;   // 0.020405
        public const float ContactToleranceM     = 0.0015f;

        /// <summary>§3.12.3 start values, lead (left) hand.</summary>
        public static HandPose LeadGripStart() => new HandPose
        {
            index  = new FingerFlex(55f, 75f, 35f, 0f),
            middle = new FingerFlex(65f, 85f, 40f, 0f),
            ring   = new FingerFlex(70f, 90f, 45f, 0f),
            little = new FingerFlex(75f, 95f, 50f, 0f),
            thumbFlexIntermediate = 15f, thumbFlexDistal = 10f,
        };

        /// <summary>§3.12.3 start values, trail (right) hand. The little finger is fixed — it rides on the lead index.</summary>
        public static HandPose TrailGripStart() => new HandPose
        {
            index  = new FingerFlex(50f, 70f, 30f, 4f),
            middle = new FingerFlex(65f, 85f, 40f, 0f),
            ring   = new FingerFlex(70f, 90f, 45f, 0f),
            little = new FingerFlex(40f, 60f, 30f, 0f),
            thumbFlexIntermediate = 15f, thumbFlexDistal = 10f,
        };

        /// <summary>
        /// §3.12.4: the shaft axis in Hand-local space, from the rest-pose landmarks. Lead: through
        /// <c>LittleProximal + n·c</c> and <c>IndexProximal + u·(0.6·L_prox) + n·c</c>; trail: the
        /// same without the <c>u</c> term. Butt → head = little → index. The MCPs are rigid to the
        /// Hand bone, so this is the same whatever the fingers are doing.
        /// </summary>
        public static void GripAxisHandLocal(Animator anim, in HandHingeHand hand, bool lead, out Vector3 o, out Vector3 d)
            => GripAxisHandLocal(anim, hand, lead ? 0.6f : 0f, out o, out d);

        /// <summary>Same axis with the index-end offset as a fraction of the index proximal length (0.6 = the §3.12.4 lead; 0 = the §3.12.4 trail).</summary>
        public static void GripAxisHandLocal(Animator anim, in HandHingeHand hand, float uOffsetFraction, out Vector3 o, out Vector3 d)
        {
            Transform h = Bone(anim, HandBone(hand.right));
            Vector3 n = hand.palmNormalHandLocal, u = hand.lengthAxisHandLocal;
            Vector3 lit = h.InverseTransformPoint(Bone(anim, hand.joints[JointIndex(Little, 0)].bone).position);
            Vector3 idx = h.InverseTransformPoint(Bone(anim, hand.joints[JointIndex(Index, 0)].bone).position);
            float lProx = hand.joints[JointIndex(Index, 0)].segmentLength;
            Vector3 pLit = lit + n * ContactM;
            Vector3 pIdx = idx + u * (uOffsetFraction * lProx) + n * ContactM;
            o = pLit;
            d = (pIdx - pLit).normalized;
        }

        /// <summary>Closest approach of segment a→b to the infinite line (o, d); exact (convex in t).</summary>
        public static float SegmentToLineDistance(Vector3 a, Vector3 b, Vector3 o, Vector3 d, out float t)
        {
            Vector3 w0 = a - o; w0 -= Vector3.Dot(w0, d) * d;
            Vector3 w1 = b - a; w1 -= Vector3.Dot(w1, d) * d;
            float den = Vector3.Dot(w1, w1);
            t = den < 1e-12f ? 0f : Mathf.Clamp01(-Vector3.Dot(w0, w1) / den);
            return (w0 + t * w1).magnitude;
        }

        public static float PointToLineDistance(Vector3 p, Vector3 o, Vector3 d) =>
            Vector3.Cross(d, p - o).magnitude;

        /// <summary>One finger against the axis, all in Hand-local metres.</summary>
        public struct FingerAxisMetrics
        {
            public string name;
            public float k;
            public bool solved;                // bisection converged inside [0.6, 1.4]
            public string note;
            public float mcp, pip, dip, tip;   // joint distances to the axis
            public float segProx, segMid, segDist;   // segment distances to the axis
            /// <summary>The number the gate reads: min(PIP, mid segment, distal segment) — the proximal
            /// segment is excluded because its MCP end sits at contact by construction (§3.12.4 puts
            /// the axis exactly one contact distance off the knuckle row), so it cannot tell open
            /// from wrapped.</summary>
            public float closestWrapped;
            /// <summary>min over all three segments — "nothing inside" is minAll ≥ contact − tol.</summary>
            public float minAll;
        }

        public static FingerAxisMetrics MeasureFingerAxis(Animator anim, in HandHingeHand hand, int finger, Vector3 o, Vector3 d)
        {
            Transform h = Bone(anim, HandBone(hand.right));
            Transform tMcp = Bone(anim, hand.joints[JointIndex(finger, 0)].bone);
            Transform tPip = Bone(anim, hand.joints[JointIndex(finger, 1)].bone);
            Transform tDip = Bone(anim, hand.joints[JointIndex(finger, 2)].bone);
            Vector3 mcp = h.InverseTransformPoint(tMcp.position), pip = h.InverseTransformPoint(tPip.position);
            Vector3 dip = h.InverseTransformPoint(tDip.position), tip = h.InverseTransformPoint(tDip.GetChild(0).position);
            var m = new FingerAxisMetrics
            {
                name = finger == Thumb ? "thumb" : finger == Index ? "index" : finger == Middle ? "middle" : finger == Ring ? "ring" : "little",
                mcp = PointToLineDistance(mcp, o, d), pip = PointToLineDistance(pip, o, d),
                dip = PointToLineDistance(dip, o, d), tip = PointToLineDistance(tip, o, d),
                segProx = SegmentToLineDistance(mcp, pip, o, d, out _),
                segMid  = SegmentToLineDistance(pip, dip, o, d, out _),
                segDist = SegmentToLineDistance(dip, tip, o, d, out _),
            };
            m.closestWrapped = Mathf.Min(m.pip, Mathf.Min(m.segMid, m.segDist));
            m.minAll = Mathf.Min(m.segProx, Mathf.Min(m.segMid, m.segDist));
            return m;
        }

        /// <summary>
        /// §3.12.3: one scale factor k ∈ [0.6, 1.4] on the finger's (MCP, PIP, DIP) triple, bisected
        /// so the closest wrapped segment sits on the grip surface (distance = ContactM). Spread is
        /// not scaled. Leaves the finger at the solved k. Monotone enough for a bisection: more k =
        /// more curl = closer, until the chain passes through the axis, which the bracket check
        /// reports rather than hides.
        /// </summary>
        public static FingerAxisMetrics SolveFingerK(Animator anim, in HandHingeHand hand, int finger, in FingerFlex f,
                                                     Vector3 o, Vector3 d, float contact = ContactM, int iterations = 24)
        {
            HandHingeHand hh = hand;   // `in` parameters cannot be captured by a local function (CS1628)
            FingerFlex ff = f;
            FingerAxisMetrics At(float k)
            {
                Finger(anim, hh, finger, new FingerFlex(ff.mcp * k, ff.pip * k, ff.dip * k, ff.spread));
                var mm = MeasureFingerAxis(anim, hh, finger, o, d);
                mm.k = k;
                return mm;
            }
            float lo = 0.6f, hi = 1.4f;
            var mLo = At(lo);
            if (mLo.closestWrapped < contact) { mLo.solved = false; mLo.note = "inside contact even at k=0.6 (most open)"; return mLo; }
            var mHi = At(hi);
            if (mHi.closestWrapped > contact) { mHi.solved = false; mHi.note = "cannot reach contact even at k=1.4 (most closed)"; return mHi; }
            for (int i = 0; i < iterations; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (At(mid).closestWrapped > contact) lo = mid; else hi = mid;
            }
            var m = At(0.5f * (lo + hi));
            m.solved = Mathf.Abs(m.closestWrapped - contact) <= ContactToleranceM;
            m.note = m.solved ? "on surface" : "converged but off the ±1.5 mm band (chain crosses the surface between samples)";
            return m;
        }

        /// <summary>
        /// The thumb's target direction, Hand-local: from ThumbProximal to the point on the shaft
        /// SURFACE at <paramref name="clockDeg"/> from "top" (−u) toward +n, <paramref name="stationM"/>
        /// down-shaft of the thumb base's foot on the axis. 30° = the reference's "1 o'clock".
        /// </summary>
        public static Vector3 ThumbAimAlongShaft(Animator anim, in HandHingeHand hand, Vector3 o, Vector3 d, float clockDeg, float stationM)
        {
            Transform h = Bone(anim, HandBone(hand.right));
            Vector3 th1 = h.InverseTransformPoint(Bone(anim, hand.joints[JointIndex(Thumb, 0)].bone).position);
            Vector3 n = hand.palmNormalHandLocal, u = hand.lengthAxisHandLocal;
            Vector3 top = Vector3.ProjectOnPlane(-u, d).normalized;
            Vector3 toward = Vector3.ProjectOnPlane(n, d).normalized;
            Vector3 radial = (Mathf.Cos(clockDeg * Mathf.Deg2Rad) * top + Mathf.Sin(clockDeg * Mathf.Deg2Rad) * toward).normalized;
            Vector3 foot = o + Vector3.Dot(th1 - o, d) * d;
            Vector3 target = foot + d * stationM + radial * ShaftRadiusM;
            return (target - th1).normalized;
        }

        /// <summary>Thumb joints against the axis, Hand-local metres: Thumb2, Thumb3, tip.</summary>
        public static void MeasureThumbAxis(Animator anim, in HandHingeHand hand, Vector3 o, Vector3 d, out float th2, out float th3, out float tip)
        {
            Transform h = Bone(anim, HandBone(hand.right));
            Transform t2 = Bone(anim, hand.joints[JointIndex(Thumb, 1)].bone);
            Transform t3 = Bone(anim, hand.joints[JointIndex(Thumb, 2)].bone);
            th2 = PointToLineDistance(h.InverseTransformPoint(t2.position), o, d);
            th3 = PointToLineDistance(h.InverseTransformPoint(t3.position), o, d);
            tip = PointToLineDistance(h.InverseTransformPoint(t3.GetChild(0).position), o, d);
        }

        // ── stage 1, supplementary — the INSCRIBED wrap (per joint, child lands on the contact circle) ──
        //
        // Measured on this hand: with the shaft one contact distance off the knuckle row, a 30 mm
        // proximal phalanx puts its PIP on the contact circle at ~47° of MCP flexion and INSIDE it
        // beyond that, and a 30 mm phalanx lying as a chord between two joints on a 20.4 mm circle
        // dips ~6.5 mm inside it at mid-length. So "closest segment on the surface" is met by the
        // first graze (PIP touching the side, tip 50 mm away) and "nothing inside" cannot be met by
        // a wrapped finger at all. The alternative measured here: bend each joint, proximal to
        // distal, until its CHILD joint (then the tip) lands on the contact circle — the finger
        // polygon inscribed in the circle, chords allowed inside down to the shaft mesh radius.
        // Axes stay the fixed hinge axes; nothing is composed onto the clip. Reported as data for
        // the §3.12.3 decision, not adopted.

        /// <summary>
        /// Bisect joint <paramref name="k"/> of <paramref name="finger"/> in [lo, hi] so its child
        /// joint (the tip for the distal) lands at <paramref name="contact"/> from the axis; the
        /// smallest such angle (first crossing from outside). reached=false → hi returned.
        /// </summary>
        public static float SolveJointToContact(Animator anim, in HandHingeHand hand, int finger, int k, float spreadDeg,
                                                Vector3 o, Vector3 d, float contact, float lo, float hi, out bool reached, out float dist)
        {
            Transform h = Bone(anim, HandBone(hand.right));
            var j = hand.joints[JointIndex(finger, k)];
            Transform tj = Bone(anim, j.bone);
            Transform child = k < 2 ? Bone(anim, hand.joints[JointIndex(finger, k + 1)].bone) : tj.GetChild(0);
            float spread = k == 0 ? spreadDeg : 0f;
            float D(float a) { Hinge(anim, j, a, spread); return PointToLineDistance(h.InverseTransformPoint(child.position), o, d); }
            float dLo = D(lo);
            if (dLo <= contact) { reached = true; dist = dLo; return lo; }
            float dHi = D(hi);
            if (dHi > contact) { reached = false; dist = dHi; return hi; }
            for (int i = 0; i < 24; i++) { float mid = 0.5f * (lo + hi); if (D(mid) > contact) lo = mid; else hi = mid; }
            float a2 = 0.5f * (lo + hi);
            dist = D(a2); reached = true;
            return a2;
        }

        public struct InscribedSolve
        {
            public float mcp, pip, dip;
            public bool mcpReached, pipReached, dipReached;
            public FingerAxisMetrics metrics;
        }

        public static InscribedSolve SolveFingerInscribed(Animator anim, in HandHingeHand hand, int finger, float spreadDeg,
                                                          Vector3 o, Vector3 d, float contact = ContactM)
        {
            var r = new InscribedSolve();
            Finger(anim, hand, finger, new FingerFlex(0f, 0f, 0f, spreadDeg));        // start from rest, spread kept
            r.mcp = SolveJointToContact(anim, hand, finger, 0, spreadDeg, o, d, contact, 0f, 90f,  out r.mcpReached, out _);
            r.pip = SolveJointToContact(anim, hand, finger, 1, spreadDeg, o, d, contact, 0f, 110f, out r.pipReached, out _);
            r.dip = SolveJointToContact(anim, hand, finger, 2, spreadDeg, o, d, contact, 0f, 80f,  out r.dipReached, out _);
            r.metrics = MeasureFingerAxis(anim, hand, finger, o, d);
            r.metrics.k = float.NaN;
            r.metrics.solved = r.mcpReached && r.pipReached && r.dipReached;
            r.metrics.note = "inscribed: mcp " + r.mcp.ToString("F1") + "° pip " + r.pip.ToString("F1") + "° dip " + r.dip.ToString("F1") + "°"
                           + (r.metrics.solved ? "" : " (a joint could not bring its child to contact)");
            return r;
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
