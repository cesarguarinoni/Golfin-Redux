// golfer_3d_test §5.4 — the stand-in golfer's presenter.
//
// EXPERIMENT, OPT-IN ONLY. Everything below the #if is compiled out unless the scripting
// define GOLFIN_GOLFER_TEST is present (SPEC §5.6). Without it this file contributes an empty
// MonoBehaviour, so a prefab or scene that references the type still deserializes, and no
// gameplay code path changes.
//
// WHY THIS FILE SITS IN Golfin.Physics.Viewer (via the sibling .asmref, NOT a new asmdef).
//   SPEC §4 assumed Assembly-CSharp could see the three event sources. It cannot:
//     • Golfin.Gameplay.Input is autoReferenced:false — Assembly-CSharp cannot name
//       ShotController at all (PuttPathPredictor.cs:3 documents the same wall).
//     • PhysicsLabController.BallSM / .ShotController are `internal` to Golfin.Physics.Viewer,
//       and BallStateMachine is a plain C# object owned by that controller — there is no
//       static accessor to reach OnShotComplete from outside.
//   Golfin.Physics.Viewer already references Input, Loop and UI and owns BallAnimator, so
//   joining it with an assembly-definition REFERENCE costs one 3-line file, adds no assembly,
//   and keeps the source path SPEC §7 asked for. It is also where every sibling presenter
//   driven by these same events already lives (BallTrailController, WaterSplashController,
//   BallAudioEmitter). No file under Assets/Scripts/Physics/ is edited.

using UnityEngine;

namespace Golfin.Gameplay.Golfer
{
#if GOLFIN_GOLFER_TEST
    using System.Collections;
    using System.Collections.Generic;
    using Golfin.Gameplay.Input;
    using Golfin.Gameplay.Loop;
    using Golfin.Gameplay.UI.Quality;
    using Golfin.Gameplay.UI.ShotUI;
    using Golfin.Physics.Viewer;

    /// <summary>
    /// Drives the test golfer: stance beside the ball, swing on commit, idle at rest.
    /// Consumes only events that already exist; the single poll is the aim heading in
    /// LateUpdate while not swinging (SPEC §5.4).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GolferPresenter : MonoBehaviour
    {
        // ── Animator parameter names, hashed once (no per-frame string work) ──────────
        static readonly int PIsPutt  = Animator.StringToHash("IsPutt");
        static readonly int PSwing   = Animator.StringToHash("Swing");
        static readonly int PCancel  = Animator.StringToHash("Cancel");
        static readonly int PReset   = Animator.StringToHash("Reset");
        static readonly int PAddress = Animator.StringToHash("Address");

        [SerializeField] Animator  anim;
        [SerializeField] Transform driverSocketRoot;
        [SerializeField] Transform putterSocketRoot;
        [SerializeField] SkinnedMeshRenderer[] skins;

        [Tooltip("Unused for placement — the address pose fixes the stance (see PlaceAtBall). " +
                 "Kept so the measured value is visible in the Inspector.")]
        [SerializeField] float stanceDistance = 0.735f;
        [Tooltip("Metres along the aim line; + moves the golfer toward the target.")]
        [SerializeField] float stanceForwardOffset = 0f;
        [SerializeField] bool  rightHanded = true;
        [Tooltip("Master off-switch that survives the define being present.")]
        [SerializeField] bool  enabledInBuild = true;

        [Tooltip("Layers the stance raycast may land on. Default = everything but IgnoreRaycast.")]
        [SerializeField] LayerMask groundMask = ~0;

        [Tooltip("Curl the fingers around the club every frame. The Mixamo clips are mocap with " +
                 "no prop in hand, so their finger animation is not a grip.")]
        [SerializeField] bool  forceGripPose = true;


        // ── Bound seams. Resolved once, then never searched for again. ────────────────
        PhysicsLabController _lab;
        ShotController       _shot;
        BallStateMachine     _sm;
        bool                 _bound;
        bool                 _swinging;
        float                _lastHeading = float.NaN;
        Coroutine            _binder;

        // Finger-grip fixup (see ApplyGripPose).

        /// <summary>
        /// Every reference resolves itself here if the Inspector's is missing.
        ///
        /// <para>NOT belt-and-braces — load-bearing. This component's body lives inside
        /// <c>#if GOLFIN_GOLFER_TEST</c>, so a compile WITHOUT the define makes the class
        /// fieldless and Unity drops the serialized data it can no longer map. Re-enabling the
        /// define does not bring it back: the prefab comes back with every reference NULL, and
        /// the only symptom is that the club silently stops swapping. It cost a whole recording
        /// take to notice. Anything that must survive that round trip cannot live only in
        /// serialized data, so the socket names — which SPEC §5.3 already fixes as the contract —
        /// are the fallback. One search, at Awake, never in the hot path.</para>
        /// </summary>
        void Awake()
        {
            if (anim == null) anim = GetComponent<Animator>();
            if (skins == null || skins.Length == 0) skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (driverSocketRoot == null) driverSocketRoot = FindChild("GOLFIN_Driver");
            if (putterSocketRoot == null) putterSocketRoot = FindChild("GOLFIN_Putter");

            if (driverSocketRoot == null || putterSocketRoot == null)
                Debug.LogWarning($"[GolferTest] club roots unresolved (driver={driverSocketRoot != null}, " +
                                 $"putter={putterSocketRoot != null}) — putter mode will not swap the mesh.");

            ResolveArmBones();

        }

        // ── The grip ──────────────────────────────────────────────────────────────────
        //
        // WHAT WENT WRONG BEFORE, so nobody retries it. The first version baked the finger
        // bone rotations off the address frame and replayed them. That reproduces the source
        // clip's hand exactly, and the source clip's hand is not a grip: measured on the
        // rendered mesh, the fingertips sit 0.142 m from the palm where a fist closed on a
        // 24 mm grip puts them near 0.04 m. The fingers curl partway and stop, so the shaft
        // passes THROUGH the gap between fingers and palm without either touching.
        //
        // It is not a sourcing problem. Every Mixamo hand measured the same way — golf 0.142,
        // baseball at-bat 0.136, baseball variation 0.148, greatsword 0.117 — is the same
        // loosely-closed fist by the same animators, and the CMU set has no finger channels at
        // all. A two-hand interlocked golf grip is specific enough that it gets authored per
        // project; it is not lying around in a free motion library.
        //
        // So this solves it as what it is: a contact problem. The grip section of the club is a
        // cylinder of known radius. A finger is wrapped when each of its joints has bent far
        // enough that the far end of that bone lies one finger-radius off the cylinder's
        // surface. Bend each joint about the knuckle line until that holds, capped so a joint
        // can never hyperextend into a claw — which is exactly the failure the old arbitrary
        // 1.15 curl cap was papering over.
        //
        // Solved every frame rather than cached: the club is parented to hand_r so the TRAIL
        // hand's relationship to the shaft is rigid, but the lead hand is placed onto the shaft
        // each frame and keeps the animation's own wrist orientation, so its wrap genuinely
        // varies. Eight fingers x three joints x a short bisection is nothing next to skinning.

        [Tooltip("Radius of the club's grip section, metres. A golf grip is about 24 mm across.")]
        [SerializeField] float shaftRadius = 0.012f;
        [Tooltip("Half-thickness of a finger, metres. Contact distance is shaftRadius + this.")]
        [SerializeField] float fingerRadius = 0.009f;
        [Tooltip("Most one finger joint may be bent by the solve, degrees. The cap is what stops " +
                 "an unreachable target curling the fingers into claws.")]
        [SerializeField] float maxJointBend = 80f;

        // ── iter-9b — the AUTHORED HELD GRIP (Cesar, 2026-09-10) ──────────────────────
        // Separate from forceGripPose on purpose. ApplyGripPose / WrapJoint / JoinLeadHandToShaft
        // are the PfGolfer_Test path and are not touched by any of this; a prefab opts into one
        // or the other, never both.
        [Tooltip("Seat both hands on the club: a capped wrist rotation puts each fist on the " +
                 "shaft axis, then the fingers open or close onto the grip. Use INSTEAD of " +
                 "forceGripPose, not alongside it.")]
        [SerializeField] bool  heldGripPose = false;
        [Tooltip("Most either wrist may be rotated to seat its fist on the shaft, degrees. " +
                 "Cesar: bend the wrists SLIGHTLY.")]
        [SerializeField] float wristSeatMaxDeg = 25f;
        [Tooltip("Most one finger joint may FLEX by, degrees, in the held-grip solve.")]
        [SerializeField] float heldCurlMaxDeg = 55f;
        [Tooltip("Most one finger joint may EXTEND by, degrees. The clip's fists are tighter " +
                 "than the grip, so the solve mostly opens them and this is the cap that matters.")]
        [SerializeField] float heldOpenMaxDeg = 40f;

        static readonly string[] Fingers = { "index", "middle", "ring", "pinky" };

        /// <summary>
        /// Past this distance from a finger, the club is not in this hand at all, in metres.
        /// No hand is 0.15 m across, so it cleanly separates "wrapped around the shaft" from
        /// "arm hanging at idle while the other hand holds the club".
        /// </summary>
        const float ShaftIsInThisHand = 0.15f;

        /// <summary>
        /// Close both hands around the club. Public so an editor harness can pose a character
        /// outside play mode and MEASURE the result instead of taking a screenshot's word for it.
        /// </summary>
        public void ApplyGripPose()
        {
            if (!forceGripPose) return;

            // Resolve on demand: Awake does not run on an edit-mode prefab instance, so an
            // editor harness calling ApplyGripPose would otherwise hit null refs and silently
            // return — which it did, and the measurement read "no change" instead of "not run".
            if (_elbowL == null) ResolveArmBones();

            Transform club = (putterSocketRoot != null && putterSocketRoot.gameObject.activeInHierarchy)
                           ? putterSocketRoot : driverSocketRoot;
            Transform slot = club != null ? club.parent : null;   // the socket carries the shaft axis
            if (slot == null) return;

            if (WrapLogRequested) _wrapLog = new System.Text.StringBuilder(2048);

            JoinLeadHandToShaft(slot);
            WrapHand("r", slot);
            WrapHand("l", slot);
            AimThumbDownShaft("l", slot);
            AimThumbDownShaft("r", slot);

            if (WrapLogRequested && _wrapLog != null)
            { WrapLogResult = _wrapLog.ToString(); WrapLogRequested = false; _wrapLog = null; }
        }

        // ── iter-9b — THE AUTHORED HELD GRIP ──────────────────────────────────────────
        //
        // WHY A SECOND POSE PATH RATHER THAN A FIX TO THE FIRST. The wrap (ApplyGripPose) solves
        // each joint independently, proximal to distal, bisecting it until its OWN far end sits
        // at contact distance. A proximal joint usually cannot get its knuckle there at all, so it
        // takes the cap at 90 deg and every joint below it then starts already inside contact and
        // applies 0 — one joint at the cap with its neighbours straight, which is a claw, not a
        // hand. That path still belongs to PfGolfer_Test and is left exactly as it was.
        //
        // WHAT THIS ONE DOES DIFFERENTLY, and why it is a much smaller correction than it sounds.
        // Measured at address on PfGolfer_MixamoNative (iter-9): the two fists sit 46.4 mm apart
        // PERPENDICULAR to the shaft and only 37.7 mm along it — side by side ACROSS the club
        // rather than threaded on it, which is why no placement of the club alone ever worked
        // (above it, through the fingers, and below it were each rejected on sight). And at the
        // fists' own centre the fingertips are 4-20 mm from the axis while contact is 20.4 mm:
        // the clip's fists are TIGHTER than the grip. So the hands need OPENING onto the club,
        // not closing around it.
        //
        // Two stages per hand, each one parameter:
        //   1. a capped wrist rotation that swings the fist centre onto the shaft axis, which is
        //      the only thing that can fix "side by side" (the fist centre is ~60-80 mm from the
        //      wrist, so 20-25 deg moves it 20-30 mm — enough, and still slight);
        //   2. per finger, ONE curl parameter shared across its three joints in natural
        //      proportions, solved so the FINGERTIP lands on the grip surface. One parameter for
        //      the whole finger is what stops a single joint taking a cap alone.

        static readonly float[] HeldCurlRatio = { 0.40f, 0.35f, 0.25f };   // MCP, PIP, DIP

        /// <summary>
        /// Seat both hands on the club: wrists onto the shaft axis, then fingers onto the grip.
        /// Public for the same reason ApplyGripPose is — an editor harness can pose and MEASURE.
        /// </summary>
        public void ApplyHeldGripPose()
        {
            if (!heldGripPose) return;
            if (!IsHumanoid) return;

            Transform club = (putterSocketRoot != null && putterSocketRoot.gameObject.activeInHierarchy)
                           ? putterSocketRoot : driverSocketRoot;
            Transform slot = club != null ? club.parent : null;   // the socket carries the shaft axis
            if (slot == null) return;

            Vector3 axisO = slot.position, axisD = slot.up;       // slot +Y is butt -> head
            SeatHandOnShaft(false, axisO, axisD);
            SeatHandOnShaft(true,  axisO, axisD);
        }


        /// <summary>
        /// Where the grip should sit in this hand: one contact distance off the KNUCKLE ROW, on
        /// the side the fingers curl toward.
        ///
        /// <para>NOT the middle of the fist, which was the first thing tried and is wrong for a
        /// reason worth writing down: the fist centre is buried in the finger mass, so seating
        /// the shaft there runs it THROUGH the knuckles — the measured result was MCP joints
        /// 9-15 mm from the axis when the grip surface is at 13.6 mm. A curl cannot fix that,
        /// because rotating a joint moves its children and not itself, so an MCP inside the grip
        /// stays there whatever the fingers do. The wrist is the only thing that can place the
        /// knuckle row, so the knuckle row is what it aims.</para>
        /// </summary>
        Vector3 GripSeatInHand(bool right, out int found)
        {
            Vector3 mcp = Vector3.zero; found = 0;
            foreach (var chain in right ? TrailFingers : LeadFingers)
            {
                var t = HumanBone(chain[0]);             // proximal = the knuckle
                if (t != null) { mcp += t.position; found++; }
            }
            if (found == 0) return Vector3.zero;
            mcp /= found;
            Vector3 n = PalmNormal(right ? "r" : "l");   // 3.10.1: points the way the fingers curl
            if (n.sqrMagnitude < 1e-10f) return mcp;
            return mcp + n.normalized * (shaftRadius + fingerRadius);
        }

        void SeatHandOnShaft(bool right, Vector3 axisO, Vector3 axisD)
        {
            Transform wrist = HumanBone(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            if (wrist == null) return;

            // ── 1. the wrist ─────────────────────────────────────────────────────────
            Vector3 fist = GripSeatInHand(right, out int found);
            if (found == 0) return;
            Vector3 arm = fist - wrist.position;
            float reach = arm.magnitude;
            if (reach < 1e-5f) return;

            // Where on the axis could this fist centre reach without moving the wrist? The fist
            // travels on a sphere of radius `reach` about the wrist; intersect that with the
            // shaft line. perp is the wrist's own distance from the axis.
            float alongW  = Vector3.Dot(wrist.position - axisO, axisD);
            Vector3 foot  = axisO + axisD * alongW;
            float perp    = (wrist.position - foot).magnitude;

            Vector3 target;
            if (reach > perp)
            {
                float half = Mathf.Sqrt(reach * reach - perp * perp);
                Vector3 p1 = foot + axisD * half, p2 = foot - axisD * half;
                target = (p1 - fist).sqrMagnitude <= (p2 - fist).sqrMagnitude ? p1 : p2;
            }
            else
            {
                target = foot;      // the axis is out of this hand's reach; aim at the nearest point
            }

            Quaternion want = Quaternion.FromToRotation(arm, target - wrist.position);
            float deg = Quaternion.Angle(Quaternion.identity, want);
            if (deg > wristSeatMaxDeg && deg > 1e-4f)
                want = Quaternion.SlerpUnclamped(Quaternion.identity, want, wristSeatMaxDeg / deg);
            wrist.rotation = want * wrist.rotation;

            // ── the TWIST, which FromToRotation leaves free and the hand badly needs ──────
            // Aiming the grip seat at the shaft fixes WHERE the hand is, not which way it is
            // rolled: FromToRotation returns the minimal rotation between two vectors, so the
            // spin about the arm is whatever the clip happened to have. Measured symptom, trail
            // hand: with the mean knuckle at its contact distance the individual knuckles still
            // read 11.0 and 12.5 mm — a knuckle ROW lying across the shaft instead of along it,
            // so the fingers meet the club side-on and no amount of curl can wrap them.
            //
            // A golf grip runs the shaft along the knuckle row, so that is the target. The twist
            // is about the arm axis THROUGH the seat point, which leaves the seat exactly where
            // the step above put it, and it spends whatever is left of the wrist budget.
            {
                Transform idx = HumanBone(right ? HumanBodyBones.RightIndexProximal  : HumanBodyBones.LeftIndexProximal);
                Transform lit = HumanBone(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
                if (idx != null && lit != null)
                {
                    Vector3 twistAxis = (GripSeatInHand(right, out _) - wrist.position);
                    if (twistAxis.sqrMagnitude > 1e-10f)
                    {
                        twistAxis.Normalize();
                        Vector3 rowNow = Vector3.ProjectOnPlane(lit.position - idx.position, twistAxis);
                        Vector3 rowWant = Vector3.ProjectOnPlane(axisD, twistAxis);
                        if (rowNow.sqrMagnitude > 1e-10f && rowWant.sqrMagnitude > 1e-10f)
                        {
                            // the row is a LINE, not an arrow: take whichever end is nearer, so
                            // the hand is never rolled the long way round to face the same way.
                            if (Vector3.Dot(rowNow, rowWant) < 0f) rowWant = -rowWant;
                            float twist = Vector3.SignedAngle(rowNow.normalized, rowWant.normalized, twistAxis);
                            float budget = Mathf.Max(0f, wristSeatMaxDeg - Mathf.Min(deg, wristSeatMaxDeg));
                            twist = Mathf.Clamp(twist, -budget, budget);
                            if (Mathf.Abs(twist) > 1e-3f)
                                wrist.rotation = Quaternion.AngleAxis(twist, twistAxis) * wrist.rotation;
                        }
                    }
                }
            }

            // ── 2. the fingers ───────────────────────────────────────────────────────
            Vector3 palm = PalmNormal(right ? "r" : "l");
            if (palm.sqrMagnitude < 1e-10f) return;
            foreach (var chain in right ? TrailFingers : LeadFingers)
                CurlFingerOntoGrip(chain, axisO, axisD, palm);
        }

        /// <summary>
        /// One curl parameter for the whole finger, split across its three joints in fixed
        /// proportions and solved so the FINGERTIP sits one finger-radius off the grip surface.
        /// Positive flexes, negative opens. Monotone in t (flexing carries the tip toward the
        /// palm, hence toward a shaft lying in it), so a plain bisection is exact enough.
        /// </summary>
        void CurlFingerOntoGrip(HumanBodyBones[] chain, Vector3 axisO, Vector3 axisD, Vector3 palm)
        {
            var j = new Transform[3];
            for (int k = 0; k < 3; k++) { j[k] = HumanBone(chain[k]); if (j[k] == null) return; }
            Transform tip = j[2].childCount > 0 ? j[2].GetChild(0) : null;
            if (tip == null) return;

            var keep = new Quaternion[3];
            for (int k = 0; k < 3; k++) keep[k] = j[k].localRotation;
            float contact = shaftRadius + fingerRadius;

            // The CLOSEST of the joints the curl can actually move, not just the fingertip.
            // Targeting the tip alone lets a mid-joint dive inside the grip while the tip sits
            // politely on the surface — which is what "the club goes through the finger" looks
            // like. j[0] is excluded because a joint's own rotation does not move itself: the
            // knuckle's place is the wrist's job (see GripSeatInHand).
            float Apply(float t)
            {
                for (int k = 0; k < 3; k++) j[k].localRotation = keep[k];
                for (int k = 0; k < 3; k++)
                {
                    Vector3 bone = (k < 2 ? j[k + 1].position : tip.position) - j[k].position;
                    Vector3 bend = Vector3.Cross(bone, palm);
                    if (bend.sqrMagnitude < 1e-10f) continue;
                    j[k].rotation = Quaternion.AngleAxis(t * HeldCurlRatio[k], bend.normalized) * j[k].rotation;
                }
                return Mathf.Min(AxisDistance(tip.position, axisO, axisD),
                       Mathf.Min(AxisDistance(j[1].position, axisO, axisD),
                                 AxisDistance(j[2].position, axisO, axisD)));
            }

            // The caps are per JOINT, so the parameter's range is set by the biggest ratio.
            float hi = heldCurlMaxDeg / HeldCurlRatio[0];      // most flexed
            float lo = -heldOpenMaxDeg / HeldCurlRatio[0];     // most open

            // SATURATION APPLIES THE CLAMP, it does not bail. Returning here left the finger at
            // the clip's own pose -- which is the pose that was too tight in the first place, so
            // the joints that most needed opening were exactly the ones the solve gave up on.
            float dLo = Apply(lo);
            if (dLo <= contact) return;                        // as open as the cap allows; keep it
            float dHi = Apply(hi);
            if (dHi >= contact) return;                        // as closed as the cap allows; keep it

            for (int k = 0; k < 14; k++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Apply(mid) > contact) lo = mid; else hi = mid;
            }
            Apply((lo + hi) * 0.5f);
        }

        /// <summary>
        /// Swing the lead forearm until the lead fist sits on the shaft one hand-width above
        /// the trail fist.
        ///
        /// <para>A REAL GRIP HAS THE HANDS JOINED, NOT TWO FISTS ON A POLE. Overlap (Vardon) and
        /// interlock both put the trail-hand pinky ON the lead-hand index and the trail palm over
        /// the lead thumb — the hands touch, lead on top, butt just above the LEAD hand. So the
        /// target is not "the nearest point on the shaft", nor a fixed station measured from the
        /// butt: it is one hand width above wherever the TRAIL fist actually is, because the club
        /// is parented to the trail hand and that is the end that cannot move. Clamped so it can
        /// never run off the butt cap, which is what "holding air past the end of the club"
        /// looked like (the lead fist measured 0.0057 m ABOVE the butt).</para>
        ///
        /// <para>Rotating the forearm about the elbow is the cheapest honest way to close the
        /// gap: it moves the hand along an arc, so the elbow stays put and the arm keeps its
        /// length. The angle cap refuses to mangle the arm when the target is unreachable.</para>
        /// </summary>
        void JoinLeadHandToShaft(Transform slot)
        {
            if (_elbowL == null || _fistL == null) return;

            Vector3 axisO = slot.position;      // the butt cap
            Vector3 axisD = slot.up;            // club local +Y runs down the shaft to the head

            // Centre-to-centre spacing, not edge-to-edge: a full hand width apart leaves visible
            // daylight between the fists, and a golf grip has none — the trail pinky rides ON the
            // lead index. Overlapping by a fifth of a hand closes that without driving the fists
            // through each other.
            float handWidth = HandWidth("r") * LeadHandOverlap;
            float alongR    = Vector3.Dot(FistCentre("r") - axisO, axisD);
            float alongL    = Mathf.Max(LeadHandFromButt, alongR - handWidth);
            Vector3 target  = axisO + axisD * alongL;

            Vector3 elbow = _elbowL.position;
            Vector3 a = FistCentre("l") - elbow, b = target - elbow;
            if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f) return;
            float ang = Vector3.Angle(a, b);
            if (ang < 0.05f || ang > 45f) return;     // ignore noise, refuse to mangle the arm
            _elbowL.rotation = Quaternion.AngleAxis(ang, Vector3.Cross(a, b).normalized) * _elbowL.rotation;
        }

        /// <summary>
        /// One finger joint and the bone end the solve drives to the shaft.
        /// Resolved once (see <see cref="WrapChain"/>) so the per-frame solve neither looks bones
        /// up by name nor builds the names to look them up with.
        /// </summary>
        struct FingerJoint { public Transform Joint, End; public float Cap; public string Name; }

        FingerJoint[] _wrapR, _wrapL;

        /// <summary>Bend all four fingers of one hand onto the shaft until they make contact.</summary>
        void WrapHand(string side, Transform slot)
        {
            bool right = side == "r";
            var chain = right ? _wrapR : _wrapL;
            if (chain == null)
            {
                chain = WrapChain(side);
                if (right) _wrapR = chain; else _wrapL = chain;
            }
            Vector3 palm = PalmNormal(side);
            if (palm == Vector3.zero) return;
            Transform wrist = IsHumanoid
                ? HumanBone(side == "r" ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand)
                : FindChild($"hand_{side}");
            Vector3 palmPos = wrist != null ? wrist.position : transform.position;
            // §3.9.4: the cylinder is the REAL shaft, ClubStart -> ClubEnd, not the socket's own
            // origin and up. Those coincide on PfGolfer_Test but not here: the club hangs off
            // GripTarget through an authored ClubSlot pose, so slot.up is the slot's axis and the
            // shaft the fingers must close on is the segment between the two markers.
            ShaftSegment(slot, out var axisO, out var axisD);
            foreach (var fj in chain) WrapJoint(fj, axisO, axisD, palm, palmPos);
        }

        /// <summary>Resolve every drivable finger joint on one hand, proximal to distal.</summary>
        FingerJoint[] WrapChain(string side)
        {
            var list = new List<FingerJoint>(12);

            // SPEC §3.9.4: resolve through HumanBodyBones first, so the wrap works on ANY humanoid
            // rig rather than only on Quaternius' bone names. That was the whole appeal of the
            // muscle-space clip §3.8 tried and this keeps it, without depending on finger muscle
            // axes the Mixamo auto-avatar never had configured.
            //
            // §3.9.4 also excludes the TRAIL LITTLE FINGER: in a real golf grip it does not grip
            // the shaft at all — it rides on the gap between the lead index and middle fingers
            // (reference/GOLF_GRIP_GEOMETRY.html). Wrapping it onto the cylinder is what drove a
            // finger through the other hand. Seven fingers, not eight.
            if (IsHumanoid)
            {
                bool right = side == "r";
                var chains = right
                    ? new[] { TrailIndex, TrailMiddle, TrailRing }                 // little finger EXCLUDED
                    : new[] { LeadIndex, LeadMiddle, LeadRing, LeadLittle };
                foreach (var chain in chains)
                    for (int j = 0; j < 3; j++)
                    {
                        Transform joint = HumanBone(chain[j]);
                        Transform end = j < 2 ? HumanBone(chain[j + 1])
                                              : (joint != null && joint.childCount > 0 ? joint.GetChild(0) : null);
                        // SPEC 3.10.2 caps: 90 proximal / 90 intermediate / 70 distal.
                        if (joint != null && end != null)
                            list.Add(new FingerJoint {
                                Joint = joint, End = end,
                                Cap = j == 2 ? 70f : 90f,
                                Name = (right ? "R." : "L.") + chain[j].ToString()
                                        .Replace("Left", "").Replace("Right", "")
                            });
                    }
                if (list.Count > 0) return list.ToArray();
            }

            foreach (var f in Fingers)
                for (int j = 1; j <= 3; j++)
                {
                    Transform joint = FindChild($"{f}_{j:00}_{side}");
                    Transform end   = FindChild($"{f}_{(j + 1):00}_{side}")
                                   ?? FindChild($"{f}_{(j + 1):00}_leaf_{side}");
                    if (joint != null && end != null)
                        list.Add(new FingerJoint { Joint = joint, End = end, Cap = maxJointBend, Name = $"{f}_{j:00}_{side}" });
                }
            return list.ToArray();
        }

        // ── SPEC §3.9.4 — humanoid bone access for the contact wrap ────────────────────
        bool IsHumanoid => anim != null && anim.avatar != null && anim.avatar.isHuman;
        Transform HumanBone(HumanBodyBones b) => IsHumanoid ? anim.GetBoneTransform(b) : null;

        static readonly HumanBodyBones[] LeadIndex   = { HumanBodyBones.LeftIndexProximal,  HumanBodyBones.LeftIndexIntermediate,  HumanBodyBones.LeftIndexDistal };
        static readonly HumanBodyBones[] LeadMiddle  = { HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal };
        static readonly HumanBodyBones[] LeadRing    = { HumanBodyBones.LeftRingProximal,   HumanBodyBones.LeftRingIntermediate,   HumanBodyBones.LeftRingDistal };
        static readonly HumanBodyBones[] LeadLittle  = { HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal };
        static readonly HumanBodyBones[] TrailIndex  = { HumanBodyBones.RightIndexProximal,  HumanBodyBones.RightIndexIntermediate,  HumanBodyBones.RightIndexDistal };
        static readonly HumanBodyBones[] TrailMiddle = { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal };
        static readonly HumanBodyBones[] TrailRing   = { HumanBodyBones.RightRingProximal,   HumanBodyBones.RightRingIntermediate,   HumanBodyBones.RightRingDistal };
        // The WRAP omits the trail little finger on purpose (a Vardon grip rides it on the lead
        // hand, not on the shaft). The held-grip pose seats both fists on the shaft directly, so
        // it does use this one; the wrap's chain list is untouched.
        static readonly HumanBodyBones[] TrailLittle = { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal };

        // DECLARED AFTER the four-name arrays above, and that is load-bearing: C# runs static
        // field initialisers in declaration order, so listing these first would fill them with
        // nulls and the held grip would silently pose nothing.
        static readonly HumanBodyBones[][] LeadFingers  = { LeadIndex,  LeadMiddle,  LeadRing,  LeadLittle  };
        static readonly HumanBodyBones[][] TrailFingers = { TrailIndex, TrailMiddle, TrailRing, TrailLittle };

        /// <summary>
        /// Bend one finger joint until its bone's far end lies one finger-radius off the
        /// shaft's surface. Called proximal to distal, so a joint is solved against the geometry
        /// its parent has already produced.
        ///
        /// <para>Bisection rather than a closed form because the joint below has already moved
        /// by the time this one is solved, so the geometry is not the rest geometry. Twelve
        /// halvings resolve the angle to well under a degree, finer than the mesh can show.</para>
        /// </summary>
        void WrapJoint(FingerJoint fj, Vector3 axisO, Vector3 axisD, Vector3 palm, Vector3 palmPos)
        {
            float contact = shaftRadius + fingerRadius;
            Transform joint = fj.Joint, end = fj.End;
            float cap = fj.Cap > 0f ? fj.Cap : maxJointBend;

            // EACH JOINT CURLS ABOUT ITS OWN AXIS, not one shared axis for the whole hand.
            // Bending every joint about the knuckle line in world space looks right on paper and
            // splays the hand open in practice: fingers at different points along that line sweep
            // through different planes, so they fan apart as they close. The flexion axis is
            // perpendicular to the bone AND to the palm, which keeps every finger curling in its
            // own parallel plane.
            Vector3 bone = end.position - joint.position;
            if (bone.sqrMagnitude < 1e-10f) return;
            Vector3 bend = Vector3.Cross(bone, palm);
            if (bend.sqrMagnitude < 1e-10f) return;
            bend.Normalize();

            // SPEC 3.10.2 — THE WRAP ONLY FLEXES.
            //
            // This used to pick its direction by "which rotation reduces the distance to the
            // shaft", with a "make a fist" fallback when the club was far away. That heuristic
            // returns EXTENSION whenever the shaft sits in or behind the finger plane, which is
            // exactly what iter-7's frames show: fingers straight, hyperextended and fanned, not
            // "as closed as an 80 degree cap allows". With a handedness-fixed n_out (3.10.1) the
            // flexion direction is known, so there is nothing to infer: +bend is flexion, always.
            // A finger that would have to EXTEND to reach the shaft is a placement error, and
            // grip.fingers.closed_* is the row that says so.
            float d0 = AxisDistance(end.position, axisO, axisD);
            Quaternion keep = joint.localRotation;

            joint.rotation = Quaternion.AngleAxis(cap, bend) * joint.rotation;
            float dMax = AxisDistance(end.position, axisO, axisD);
            joint.localRotation = keep;

            float applied;
            if (d0 <= contact)
            {
                applied = 0f;                                   // already touching
            }
            else if (dMax > contact)
            {
                applied = cap;                                  // cannot reach: as closed as it gets
                joint.rotation = Quaternion.AngleAxis(cap, bend) * joint.rotation;
            }
            else
            {
                float lo = 0f, hi = cap;
                for (int k = 0; k < 12; k++)
                {
                    float mid = (lo + hi) * 0.5f;
                    joint.localRotation = keep;
                    joint.rotation = Quaternion.AngleAxis(mid, bend) * joint.rotation;
                    if (AxisDistance(end.position, axisO, axisD) > contact) lo = mid; else hi = mid;
                }
                applied = hi;
                joint.localRotation = keep;
                joint.rotation = Quaternion.AngleAxis(hi, bend) * joint.rotation;
            }

            // SPEC 3.10.2 per-joint log — the play-mode measurement iter-7 asked for. Built only
            // when the harness asks, so the normal path allocates nothing.
            if (WrapLogRequested && _wrapLog != null)
                _wrapLog.Append(fj.Name).Append(" applied=").Append(applied.ToString("F1"))
                        .Append(" cap=").Append(cap.ToString("F0"))
                        .Append(" d0=").Append(d0.ToString("F4"))
                        .Append(" dMax=").Append(dMax.ToString("F4"))
                        .Append(" after=").Append(AxisDistance(end.position, axisO, axisD).ToString("F4"))
                        .Append('\n');
        }

        /// <summary>
        /// SPEC 3.10.2 — set by the harness for one frame to capture the 21-joint wrap log.
        /// Static so an editor harness can reach it without a reference to the instance.
        /// </summary>
        public static bool WrapLogRequested;
        public static string WrapLogResult = "";
        System.Text.StringBuilder _wrapLog;

        /// <summary>
        /// SPEC 3.10.1 verification: flex the middle MCP by +5 deg about cross(bone, n_out) and
        /// confirm the fingertip moves along +n_out. Returns the dot; the caller reports it.
        /// A negative dot means the sign convention is wrong for this rig — report, never flip
        /// silently, because a silent flip is how the original bug survived three iterations.
        /// </summary>
        public float VerifyPalmNormal(string side)
        {
            bool right = side == "r";
            Vector3 n = PalmNormal(side);
            var mcp = HumanBone(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            var pip = HumanBone(right ? HumanBodyBones.RightMiddleIntermediate : HumanBodyBones.LeftMiddleIntermediate);
            if (n == Vector3.zero || mcp == null || pip == null) return float.NaN;
            Vector3 bone = pip.position - mcp.position;
            Vector3 bend = Vector3.Cross(bone, n);
            if (bend.sqrMagnitude < 1e-10f) return float.NaN;
            Vector3 before = pip.position;
            Quaternion keep = mcp.localRotation;
            mcp.rotation = Quaternion.AngleAxis(5f, bend.normalized) * mcp.rotation;
            Vector3 delta = pip.position - before;
            mcp.localRotation = keep;
            return delta.sqrMagnitude < 1e-12f ? float.NaN : Vector3.Dot(delta.normalized, n);
        }

        /// <summary>
        /// Lay the thumb along the shaft instead of curling it into the fist.
        ///
        /// <para>A golf grip is not two fists on a shaft: the club lies across the FINGERS and
        /// both thumbs run DOWN the shaft — the lead thumb on top of the grip, the trail palm
        /// covering it. Curling the thumbs with the fingers is a baseball-bat hold and reads as
        /// wrong on sight. One-bone aim on the metacarpal carries the whole thumb.</para>
        /// </summary>
        void AimThumbDownShaft(string side, Transform slot)
        {
            bool right = side == "r";
            Transform root = IsHumanoid ? HumanBone(right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal)
                                        : FindChild($"thumb_01_{side}");
            Transform tip = null;
            if (IsHumanoid)
            {
                var distal = HumanBone(right ? HumanBodyBones.RightThumbDistal : HumanBodyBones.LeftThumbDistal);
                tip = distal != null && distal.childCount > 0 ? distal.GetChild(0) : distal;
            }
            tip = tip ?? FindChild($"thumb_04_leaf_{side}") ?? FindChild($"thumb_03_{side}");
            if (root == null || tip == null) return;

            ShaftSegment(slot, out var axisO, out var axisD);
            Vector3 a = tip.position - root.position;
            Vector3 b = axisD;                         // down the shaft, toward the head
            if (a.sqrMagnitude < 1e-8f) return;

            // §3.9.4 / GOLF_GRIP_GEOMETRY: the LEAD thumb does not lie on top of the grip, it sits
            // at "1 o'clock" — 15-30 deg around the shaft toward the trail side, viewed down the
            // shaft from the butt. Aiming it at dead top is the flat-thumb-on-a-bat look. The
            // trail thumb keeps the plain down-shaft aim; it only rests on the shaft's lead side.
            if (!right)
            {
                Vector3 up12 = Vector3.ProjectOnPlane(-PalmNormal("l"), axisD);   // 12 o'clock = away from the palm
                if (up12.sqrMagnitude > 1e-8f)
                {
                    // +22.5 deg = the middle of the 15-30 deg window; sign taken so the roll goes
                    // TOWARD the trail hand rather than away from it.
                    Vector3 toTrail = Vector3.ProjectOnPlane(
                        (HumanBone(HumanBodyBones.RightHand)?.position ?? transform.position) - root.position, axisD);
                    float dir = Vector3.Dot(Vector3.Cross(up12, toTrail), axisD) >= 0f ? 1f : -1f;
                    b = (Quaternion.AngleAxis(dir * ThumbClockDeg, axisD) * up12.normalized * 0.35f + axisD).normalized;
                }
            }

            float ang = Vector3.Angle(a, b);
            if (ang < 0.05f || ang > 90f) return;
            root.rotation = Quaternion.AngleAxis(ang, Vector3.Cross(a, b).normalized) * root.rotation;
        }

        /// <summary>"1 o'clock" for the lead thumb — the middle of the 15-30 deg window (§3.9.4).</summary>
        const float ThumbClockDeg = 22.5f;

        /// <summary>
        /// The real shaft segment the fingers close on: ClubStart (butt) -> ClubEnd (head).
        /// Falls back to the socket's own axis when the markers are absent, which is the
        /// PfGolfer_Test case and byte-identical to the old behaviour there.
        /// </summary>
        void ShaftSegment(Transform slot, out Vector3 axisO, out Vector3 axisD)
        {
            axisO = slot.position; axisD = slot.up;
            var s = FindChild("ClubStart");
            var e = FindChild("ClubEnd");
            if (s == null || e == null) return;
            Vector3 d = e.position - s.position;
            if (d.sqrMagnitude < 1e-10f) return;
            axisO = s.position; axisD = d.normalized;
        }

        /// <summary>Perpendicular distance from a point to the shaft's centre line.</summary>
        static float AxisDistance(Vector3 p, Vector3 axisO, Vector3 axisD) =>
            Vector3.Cross(axisD, p - axisO).magnitude;

        /// <summary>
        /// The normal of the palm — out through the back of the hand. Every finger's flexion
        /// axis is derived from this and the bone itself, so the fingers curl in parallel
        /// planes instead of fanning apart.
        /// </summary>
        Vector3 PalmNormal(string side)
        {
            Transform i, p, w;
            if (IsHumanoid)
            {
                // SPEC §3.10.1 — ONE handedness-fixed normal, OUT OF THE PALM SURFACE.
                //
                // cross(along, across) is mirror-antisymmetric: with across = little - index it
                // points out of the palm on the left hand and out of the BACK on the right. The
                // old comment in WrapJoint already said this normal "flips with handedness" and
                // worked around it with a distance heuristic instead of fixing it. Everything
                // downstream inherited the flip — the trail shaft axis was offset onto the back of
                // the MCP row, where no finger can reach, and the wrap chose extension. Negating
                // for the right hand fixes all of it in one place.
                bool right = side == "r";
                i = HumanBone(right ? HumanBodyBones.RightIndexProximal  : HumanBodyBones.LeftIndexProximal);
                p = HumanBone(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
                w = HumanBone(right ? HumanBodyBones.RightHand           : HumanBodyBones.LeftHand);
                if (i != null && p != null && w != null)
                {
                    Vector3 acrossH = p.position - i.position;                     // index -> little
                    Vector3 alongH  = (i.position + p.position) * 0.5f - w.position; // wrist -> knuckles
                    Vector3 nh = Vector3.Cross(alongH, acrossH);
                    if (nh.sqrMagnitude < 1e-10f) return Vector3.zero;
                    nh.Normalize();
                    return right ? -nh : nh;
                }
            }
            i = FindChild($"index_01_{side}");
            p = FindChild($"pinky_01_{side}");
            w = FindChild($"hand_{side}");
            if (i == null || p == null || w == null) return Vector3.zero;
            Vector3 across = p.position - i.position;                          // index to pinky
            Vector3 along  = (i.position + p.position) * 0.5f - w.position;     // wrist to knuckles
            Vector3 n = Vector3.Cross(along, across);
            return n.sqrMagnitude < 1e-10f ? Vector3.zero : n.normalized;
        }

        /// <summary>Wrist to knuckles — the spacing between the two hands on a joined grip.</summary>
        float HandWidth(string side)
        {
            Transform h = FindChild($"hand_{side}"), k = FindChild($"middle_01_{side}");
            return (h != null && k != null) ? Vector3.Distance(h.position, k.position) : 0.117f;
        }

        /// <summary>Where the lead hand sits below the butt cap, in metres.</summary>
        const float LeadHandFromButt = 0.03f;

        /// <summary>
        /// Fraction of a hand width between the two fist centres.
        ///
        /// <para>Held at 1. Tightening it to 0.82 to close the last of the daylight between the
        /// fists reads well as a number and measures worse where it counts: the lead fist went
        /// from 0.0062 m off the shaft axis to 0.0257 m, and the hand mesh began to shear. The
        /// cause is structural rather than a bad constant — <see cref="JoinLeadHandToShaft"/>
        /// swings one bone, so it can match the DIRECTION of a target but not its distance, and
        /// a station further up the shaft simply is not on that arc. Closing the gap properly
        /// needs two-bone IK on shoulder and elbow, which is more than this experiment is for.
        /// A hand on the club with a seam beats a hand near the club.</para>
        /// </summary>
        const float LeadHandOverlap = 1f;

        Transform _elbowL, _fistL;

        void ResolveArmBones()
        {
            _elbowL = FindChild("lowerarm_l");
            _fistL  = FindChild("hand_l");
        }

        /// <summary>
        /// The middle of a closed fist. Averaging several bones rather than taking the wrist:
        /// the wrist sits behind the hand, so a shaft threaded through the fingers reads as
        /// centimetres off the shaft if measured from there.
        /// </summary>
        Vector3 FistCentre(string side)
        {
            Vector3 acc = Vector3.zero; int n = 0;
            foreach (var nm in new[] { "middle_02_", "index_02_", "ring_02_", "thumb_03_", "middle_01_" })
            {
                var t = FindChild(nm + side);
                if (t != null) { acc += t.position; n++; }
            }
            if (n > 0) return acc / n;
            var h = FindChild($"hand_{side}");
            return h != null ? h.position : Vector3.zero;
        }


        /// <summary>
        /// Bone lookup by name, cached.
        ///
        /// <para>This used to walk <c>GetComponentsInChildren&lt;Transform&gt;</c> on every call,
        /// which was fine when the only callers were Awake resolving two sockets. The grip solve
        /// calls it about sixty times a frame, so that shape would mean sixty full skeleton scans
        /// and sixty array allocations per frame on a character with a hundred-odd bones — real
        /// cost against SPEC §6's frame-time budget, and pure garbage. The hierarchy never
        /// changes, so one dictionary built on first use serves every caller.</para>
        /// </summary>
        Dictionary<string, Transform> _bones;

        Transform FindChild(string n)
        {
            if (_bones == null)
            {
                _bones = new Dictionary<string, Transform>(128);
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    _bones[t.name] = t;          // last wins; bone names are unique on this rig
            }
            return _bones.TryGetValue(n, out var b) ? b : null;
        }

        void OnEnable()
        {
            if (!enabledInBuild) { gameObject.SetActive(false); return; }

            ShotController.ShotCancelled            += HandleShotCancelled;
            ClubSelectionBroadcast.OnPutterModeChanged += HandlePutterMode;
            QualityTierService.OnTierChanged        += ApplyTier;

            ApplyTier(QualityTierService.Current);
            HandlePutterMode(ClubSelectionBroadcast.InPutterMode);

            _binder = StartCoroutine(BindWhenLabReady());
        }

        void OnDisable()
        {
            ShotController.ShotCancelled            -= HandleShotCancelled;
            ClubSelectionBroadcast.OnPutterModeChanged -= HandlePutterMode;
            QualityTierService.OnTierChanged        -= ApplyTier;

            if (_binder != null) { StopCoroutine(_binder); _binder = null; }
            Unbind();
        }

        // ── Binding ───────────────────────────────────────────────────────────────────
        //
        // The golfer is instantiated on GameSession.OnRoundStarted, which can beat the hole
        // scene's PhysicsLabController to Awake. The search is therefore a BOUNDED poll —
        // ~0.25 s apart for at most BindTimeoutSeconds — and stops dead the moment it binds
        // or the budget runs out. SPEC §5.4's "no Find* after Awake" is about the per-shot
        // hot path; a one-shot bounded bind is the cheapest correct alternative to a
        // scene-serialized reference, which §5.5 forbids on purpose.
        const float BindTimeoutSeconds = 20f;

        IEnumerator BindWhenLabReady()
        {
            var wait = new WaitForSeconds(0.25f);
            float deadline = Time.realtimeSinceStartup + BindTimeoutSeconds;
            while (!_bound && Time.realtimeSinceStartup < deadline)
            {
                TryBind();
                if (_bound) break;
                yield return wait;
            }
            _binder = null;
            if (!_bound)
                Debug.LogWarning("[GolferTest] no PhysicsLabController with a ShotController within " +
                                 $"{BindTimeoutSeconds:F0}s — the golfer will stand but not swing.");
        }

        void TryBind()
        {
            if (_bound) return;
#if UNITY_2023_1_OR_NEWER
            _lab = Object.FindFirstObjectByType<PhysicsLabController>();
#else
            _lab = Object.FindObjectOfType<PhysicsLabController>();
#endif
            if (_lab == null) return;

            _shot = _lab.ShotController;
            _sm   = _lab.BallSM;
            if (_shot == null || _sm == null) return;

            // §9.2: the IMMEDIATE event, not OnShotResolved. The swing must start on the commit
            // frame; OnShotResolved is now what carries the ball, and it is held back to the
            // swing's impact frame. Subscribing here is also what ARMS the deferral — the
            // controller defers only while something is listening on the immediate event.
            _shot.OnShotResolvedImmediate += HandleShotResolved;
            _shot.OnStateChanged += HandleShotState;
            _sm.OnShotComplete   += HandleShotComplete;
            _sm.OnStateChanged   += HandleBallState;
            _bound = true;

            PlaceAtBall();
            // BallStateMachine starts in Aiming and fires no event for its initial state, so the
            // first stance has to be derived here or the golfer stands idle until the first shot.
            RefreshStance();
            Debug.Log("[GolferTest] bound to ShotController + BallStateMachine.");
        }

        void Unbind()
        {
            if (!_bound) return;
            if (_shot != null) { _shot.OnShotResolvedImmediate -= HandleShotResolved; _shot.OnStateChanged -= HandleShotState; }
            if (_sm   != null) { _sm.OnShotComplete -= HandleShotComplete; _sm.OnStateChanged -= HandleBallState; }
            _shot = null; _sm = null; _lab = null; _bound = false;
            _stanceKnown = false;
        }

        // ── Event handlers ────────────────────────────────────────────────────────────

        void HandleShotResolved(Golfin.Physics.ShotInput _, Golfin.Physics.BallPhysicsModifiers __)
        {
            if (anim == null) return;
            _swinging = true;
            _stanceKnown = false;                 // the swing owns the body; re-derive after it
            ApplyCulling();                       // never cull a swing — the camera cuts away mid-shot
            anim.SetBool(PIsPutt, _shot != null && _shot.IsPutt);
            anim.SetTrigger(PSwing);
        }

        /// <summary>
        /// ADDRESS IS A RESTING STATE, NOT AN INPUT STATE.
        ///
        /// <para>This used to be edge-triggered off <see cref="ShotInputState.State"/>: anything
        /// other than <see cref="ShotState.Idle"/> meant address. That is wrong at the root, and
        /// it is why in a real round the golfer stood bolt upright, back to camera, arms at his
        /// sides with the club dangling and the head nowhere near the ball.
        /// <c>ShotController.State</c> is Idle whenever the player is not physically touching the
        /// screen — <c>Aiming</c> does not begin until <c>justTouched</c> (ShotController.Tick,
        /// <c>case ShotState.Idle</c>) — so the ENTIRE window in which a golfer is at address,
        /// standing over the ball lining the shot up, is a window in which ShotState is Idle and
        /// this presenter was firing <c>Cancel</c>. The only moments it addressed were the
        /// fraction of a second the finger was down, on the way into the swing. That is also why
        /// the <c>shot.addressBeforeSwing</c> invariant PASSed on a render that plainly showed
        /// Idle: the harness samples it while it is driving a synthetic drag, which is the one
        /// time the old rule happened to be true.</para>
        ///
        /// <para>The signal that actually means "the player may hit" is
        /// <see cref="BallState.Aiming"/>, whose own definition is "no shot in flight; player can
        /// input". So: address whenever the ball is armed and we are not mid-swing, idle while it
        /// is Flying or Rolling.</para>
        ///
        /// <para>DERIVED, NOT EDGE-TRIGGERED. Every handler funnels into this one idempotent
        /// apply, which fires a trigger only when the wanted stance actually changes. The old
        /// comment's failure mode — a trigger left permanently pending because Cancel was set on
        /// every idle frame — cannot come back, because nothing here fires on a frame where the
        /// answer did not change. <c>_stanceKnown</c> is false until the first apply and is
        /// cleared whenever the swing takes the body, so re-arming after a shot always re-fires
        /// Address rather than assuming it is still held.</para>
        /// </summary>
        bool _addressed;
        bool _stanceKnown;

        void RefreshStance()
        {
            if (anim == null) return;

            // _sm == null only before the bind completes; standing at address is the right
            // default there — the golfer is beside a ball nobody has hit yet.
            bool wantAddress = !_swinging && (_sm == null || _sm.State == BallState.Aiming);

            if (_stanceKnown && wantAddress == _addressed) return;
            _stanceKnown = true;
            _addressed   = wantAddress;

            // CLEAR EVERY COMPETING TRIGGER, NOT JUST THE OPPOSITE ONE. `Reset` is an ANY-STATE
            // transition to Idle with canTransitionToSelf = 0, which makes it a delayed-action
            // trap: HandleShotComplete sets it while the animator is ALREADY in Idle, so the
            // any-state transition is not taken (it would be Idle→Idle) and the trigger is not
            // consumed. It sits armed. Address then moves him to Address_Drive — and on the very
            // next frame the still-pending Reset is finally valid and drags him straight back to
            // Idle. That is a second, independent cause of the standing-upright bug, and it hit
            // on every shot after the first: the animator trace read `-> Address_Drive` followed
            // immediately by `-> Idle` with nothing in this presenter having asked for it.
            // Whichever stance wins, it must disarm the others.
            if (wantAddress)
            {
                anim.ResetTrigger(PCancel);
                anim.ResetTrigger(PReset);
                anim.SetTrigger(PAddress);
            }
            else
            {
                anim.ResetTrigger(PAddress);
                anim.SetTrigger(PCancel);
            }
        }

        void HandleBallState(BallStateChange _) => RefreshStance();

        /// <summary>
        /// Putter-vs-driver only. The stance itself is <see cref="RefreshStance"/>'s business —
        /// see its remarks for why shot state is the wrong thing to key address on.
        /// </summary>
        void HandleShotState(ShotInputState s)
        {
            if (anim == null || _swinging) return;
            anim.SetBool(PIsPutt, s.IsPutt);
        }

        void HandleShotCancelled()
        {
            if (anim == null) return;
            _swinging = false;
            ApplyCulling();
            anim.ResetTrigger(PSwing);
            _stanceKnown = false;   // the swing may have left him mid-clip; re-derive from scratch
            RefreshStance();        // a cancelled shot returns him to address, not to idle
        }

        void HandleShotComplete(ShotResult _)
        {
            if (anim != null) { anim.ResetTrigger(PSwing); anim.SetTrigger(PReset); }
            _swinging = false;
            _stanceKnown = false;
            ApplyCulling();
            PlaceAtBall();          // same frame as OnShotComplete, per SPEC §6
            // Ball is AtRest/InCup/OB here, so this settles him into Idle. The owner's ReArm()
            // follows on the same beat the camera returns to aiming framing, and its
            // OnStateChanged(→Aiming) is what puts him back over the ball.
            RefreshStance();
        }

        void HandlePutterMode(bool putt)
        {
            if (driverSocketRoot != null) driverSocketRoot.gameObject.SetActive(!putt);
            if (putterSocketRoot != null) putterSocketRoot.gameObject.SetActive(putt);
            if (anim != null) anim.SetBool(PIsPutt, putt);
        }

        QualityTier _tier = QualityTier.Mid;

        void ApplyTier(QualityTier tier)
        {
            _tier = tier;
            bool low = tier == QualityTier.Low;
            if (skins != null)
                foreach (var s in skins)
                {
                    if (s == null) continue;
                    s.quality = low ? SkinQuality.Bone2 : SkinQuality.Bone4;
                    s.shadowCastingMode = low
                        ? UnityEngine.Rendering.ShadowCastingMode.Off
                        : UnityEngine.Rendering.ShadowCastingMode.On;
                }
            ApplyCulling();
        }

        /// <summary>
        /// Culling is suspended for the duration of a swing.
        ///
        /// <para>THE CAMERA LEAVES HIM MID-SWING, EVERY TIME. The moment the shot commits the game
        /// cuts to its flight framing and the golfer is out of the frustum — and under
        /// <see cref="AnimatorCullingMode.CullUpdateTransforms"/> that stops transform writes, so
        /// the swing freezes at whatever frame the cut happened on and the club never reaches the
        /// ball. Under Low's <see cref="AnimatorCullingMode.CullCompletely"/> it is worse: the
        /// state machine itself stops, so <c>Swing_Drive</c> never reaches its exit time and he is
        /// still mid-swing when the camera comes back.</para>
        ///
        /// <para>A swing is at most a couple of seconds on ONE skinned mesh, so animating it
        /// off-screen costs nothing worth measuring; being idle off-screen is the case worth
        /// culling, and that is still culled at the tier's setting.</para>
        /// </summary>
        void ApplyCulling()
        {
            if (anim == null) return;
            anim.cullingMode = _swinging
                ? AnimatorCullingMode.AlwaysAnimate
                : (_tier == QualityTier.Low ? AnimatorCullingMode.CullCompletely
                                            : AnimatorCullingMode.CullUpdateTransforms);
        }

        // ── Placement ─────────────────────────────────────────────────────────────────

        /// <summary>Current ball transform, or null between shots.</summary>
        static Transform Ball => BallAnimator.Instance != null ? BallAnimator.Instance.CurrentBall : null;

        void PlaceAtBall()
        {
            var b = Ball;
            if (b == null) return;
            PlaceAtBall(b.position, _shot != null ? _shot.CameraHeadingRadians : _lastHeading);
        }

        /// <summary>
        /// THE POSE DECIDES WHERE HE STANDS, NOT A GUESS ABOUT WHICH WAY A GOLFER FACES.
        ///
        /// <para>Measured on the address frame with the root at the origin facing +Z, the club
        /// head sits at local <c>(0.735, 0, -0.069)</c> — 0.74 m out to his SIDE — and travels
        /// toward local -Z through impact. So in this animation the ball belongs on the golfer's
        /// local +X and the target line runs along his local -Z.</para>
        ///
        /// <para>The first version rotated him with <c>LookRotation(f)</c> so the ball sat on his
        /// local +Z. Every stance angle that produced was defensible and the golfer was still
        /// addressing empty grass a metre from the ball, because +Z is not where this pose puts
        /// the club. Placing from <see cref="AddressHeadLocal"/> makes the club head land ON the
        /// ball by construction — there is no angle left to get wrong.</para>
        ///
        /// <para>golfer_club_grip §3.5: promoted to [SerializeField] so each prefab variant can
        /// carry its own value. After §3.4 the Mixamo-native prefab measures ClubEnd at address
        /// and sets this field to the measured golfer-local position — then club.headAtBall
        /// (ClubEnd world) and stance.address.clubReachesBall (AddressClubHeadWorld) agree
        /// instead of the latter passing by construction. PfGolfer_Test keeps the default
        /// (byte-identical behaviour).</para>
        /// </summary>
        [SerializeField] Vector3 addressHeadLocal = new Vector3(0.735f, 0f, -0.069f);

        public void PlaceAtBall(Vector3 ball, float headingRad)
        {
            if (float.IsNaN(headingRad)) headingRad = 0f;
            _lastHeading = headingRad;

            // Aim basis: (cos, 0, sin) — ShotInputState's convention, stated outright at
            // PutterAimLine.cs:317-319.
            Vector3 d = new Vector3(Mathf.Cos(headingRad), 0f, Mathf.Sin(headingRad));

            // Local +Z IS the ball-flight direction. Measured as the club-head velocity AT impact
            // — 26.75 m/s along local (0.05, 0, 0.999) — not as the address-to-impact difference,
            // which is a near-zero noise-dominated vector that points the other way and cost two
            // rounds of wrong stances. Left hand on top of the grip confirms a right-handed swing,
            // and the impact velocity runs to his LEFT, which agrees.
            Quaternion rot = Quaternion.LookRotation(rightHanded ? d : -d, Vector3.up);

            Vector3 head = addressHeadLocal;
            if (!rightHanded) head.x = -head.x;

            Vector3 p = ball - rot * head + d * stanceForwardOffset;
            p.y = GroundY(p, ball.y);

            transform.SetPositionAndRotation(p, rot);
        }

        /// <summary>Where the club head lands at address, for the harness to assert against.</summary>
        public Vector3 AddressClubHeadWorld => transform.TransformPoint(
            rightHanded ? addressHeadLocal : new Vector3(-addressHeadLocal.x, addressHeadLocal.y, addressHeadLocal.z));

        /// <summary>Ground height under <paramref name="p"/>, or <paramref name="fallback"/>.</summary>
        float GroundY(Vector3 p, float fallback)
        {
            var origin = new Vector3(p.x, p.y + 2f, p.z);
            return UnityEngine.Physics.Raycast(origin, Vector3.down, out var hit, 4f,
                                               groundMask, QueryTriggerInteraction.Ignore)
                 ? hit.point.y
                 : fallback;
        }

        /// <summary>
        /// The one poll SPEC §5.4 allows: while the golfer is NOT mid-swing, follow the aim
        /// heading so he turns with the camera. Early-outs on an unchanged heading, so a still
        /// aim costs one float compare per frame and zero transform writes.
        /// </summary>
        void LateUpdate()
        {
            // AFTER the Animator has evaluated: LateUpdate runs past the animation phase, so this
            // overwrites the clip's fingers rather than being overwritten by them.
            //
            // Both are no-ops unless their own flag is set, and a prefab sets one or the other:
            // forceGripPose is PfGolfer_Test's wrap, heldGripPose is the authored grip (iter-9b).
            // Neither reads the club's transform back, so posing the hands here cannot move the
            // club — Animation Rigging has already evaluated GripTarget inside the animator graph.
            ApplyGripPose();
            ApplyHeldGripPose();

            if (_swinging || _shot == null) return;
            float h = _shot.CameraHeadingRadians;
            if (!float.IsNaN(_lastHeading) && Mathf.Abs(Mathf.DeltaAngle(h * Mathf.Rad2Deg, _lastHeading * Mathf.Rad2Deg)) < 0.05f)
                return;
            var b = Ball;
            if (b != null) PlaceAtBall(b.position, h);
        }
    }
#else
    /// <summary>
    /// GOLFIN_GOLFER_TEST is absent: the presenter is an inert shell so a prefab that
    /// references the type still deserializes, and nothing subscribes to anything.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GolferPresenter : MonoBehaviour { }
#endif
}
