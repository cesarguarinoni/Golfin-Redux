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

            JoinLeadHandToShaft(slot);
            WrapHand("r", slot);
            WrapHand("l", slot);
            AimThumbDownShaft("l", slot);
            AimThumbDownShaft("r", slot);
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
        struct FingerJoint { public Transform Joint, End; }

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
            Transform wrist = FindChild($"hand_{side}");
            Vector3 palmPos = wrist != null ? wrist.position : transform.position;
            foreach (var fj in chain) WrapJoint(fj, slot.position, slot.up, palm, palmPos);
        }

        /// <summary>Resolve every drivable finger joint on one hand, proximal to distal.</summary>
        FingerJoint[] WrapChain(string side)
        {
            var list = new List<FingerJoint>(12);
            foreach (var f in Fingers)
                for (int j = 1; j <= 3; j++)
                {
                    Transform joint = FindChild($"{f}_{j:00}_{side}");
                    Transform end   = FindChild($"{f}_{(j + 1):00}_{side}")
                                   ?? FindChild($"{f}_{(j + 1):00}_leaf_{side}");
                    if (joint != null && end != null) list.Add(new FingerJoint { Joint = joint, End = end });
                }
            return list.ToArray();
        }

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

            // EACH JOINT CURLS ABOUT ITS OWN AXIS, not one shared axis for the whole hand.
            // The first version bent every joint about the knuckle line in world space, which
            // looks right on paper and splays the hand open in practice: fingers sitting at
            // different points along that line sweep through different planes, so they fan
            // apart as they close. The flexion axis is perpendicular to the bone AND to the
            // palm, which keeps every finger curling in its own parallel plane.
            Vector3 bone = end.position - joint.position;
            if (bone.sqrMagnitude < 1e-10f) return;
            Vector3 bend = Vector3.Cross(bone, palm);
            if (bend.sqrMagnitude < 1e-10f) return;
            bend.Normalize();

            float d0 = AxisDistance(end.position, axisO, axisD);
            if (d0 <= contact) return;                // already touching, leave it alone

            // WHICH WAY THIS JOINT CURLS DEPENDS ON WHETHER THE CLUB IS IN THIS HAND AT ALL.
            //
            // "Bend toward the shaft" is the right rule while the shaft is inside the hand, and
            // it is the rule that produced a real grip at address. It is catastrophic when the
            // club is elsewhere: with the lead arm hanging at idle the club is half a metre away
            // and roughly across the hand, so the direction that reduces distance to it is
            // EXTENSION, and the hand solves itself flat open — the live harness on Hole 06
            // measured the right middle finger tip-to-knuckle at 0.0873 m, straight, where a
            // fist is about 0.04.
            //
            // Two other rules were tried and measured worse, so neither is worth revisiting.
            // Taking the sign from the palm normal fails because that normal is built from an
            // index-to-pinky vector and so flips with handedness: it wrecked the working case
            // (worst fingertip 0.0323 -> 0.0843 m at address). Falling back to "make a fist"
            // whenever contact is merely unreachable fails too, because a finger can be a
            // centimetre short of a shaft it is properly wrapped around — that took the right
            // pinky from 0.0323 to 0.1073 m.
            //
            // The regimes are simply distinguished by distance. No hand is 0.15 m across, so a
            // shaft further away than that is not in this hand and the pose owes it nothing.
            Quaternion keep = joint.localRotation;
            float sign;
            if (d0 < ShaftIsInThisHand)
            {
                joint.rotation = Quaternion.AngleAxis(5f, bend) * joint.rotation;
                float dPlus = AxisDistance(end.position, axisO, axisD);
                joint.localRotation = keep;
                sign = dPlus < d0 ? 1f : -1f;                 // curl toward the club
            }
            else
            {
                float toPalm0 = Vector3.Distance(end.position, palmPos);
                joint.rotation = Quaternion.AngleAxis(10f, bend) * joint.rotation;
                float toPalm1 = Vector3.Distance(end.position, palmPos);
                joint.localRotation = keep;
                sign = toPalm1 < toPalm0 ? 1f : -1f;          // no club here: just close the fist
            }

            // Does the cap reach contact? If not, take the cap — a finger that cannot reach
            // ends up as closed as it can be, not left where it started.
            joint.rotation = Quaternion.AngleAxis(sign * maxJointBend, bend) * joint.rotation;
            float dMax = AxisDistance(end.position, axisO, axisD);
            joint.localRotation = keep;
            if (dMax > contact)
            {
                joint.rotation = Quaternion.AngleAxis(sign * maxJointBend, bend) * joint.rotation;
                return;
            }

            float lo = 0f, hi = maxJointBend;
            for (int i = 0; i < 12; i++)
            {
                float mid = (lo + hi) * 0.5f;
                joint.localRotation = keep;
                joint.rotation = Quaternion.AngleAxis(sign * mid, bend) * joint.rotation;
                if (AxisDistance(end.position, axisO, axisD) > contact) lo = mid; else hi = mid;
            }
            joint.localRotation = keep;
            joint.rotation = Quaternion.AngleAxis(sign * hi, bend) * joint.rotation;
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
            Transform root = FindChild($"thumb_01_{side}");
            Transform tip  = FindChild($"thumb_04_leaf_{side}") ?? FindChild($"thumb_03_{side}");
            if (root == null || tip == null) return;

            Vector3 a = tip.position - root.position;
            Vector3 b = slot.up;                       // down the shaft, toward the head
            if (a.sqrMagnitude < 1e-8f) return;
            float ang = Vector3.Angle(a, b);
            if (ang < 0.05f || ang > 90f) return;
            root.rotation = Quaternion.AngleAxis(ang, Vector3.Cross(a, b).normalized) * root.rotation;
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
            Transform i = FindChild($"index_01_{side}"),
                      p = FindChild($"pinky_01_{side}"),
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

            _shot.OnShotResolved += HandleShotResolved;
            _shot.OnStateChanged += HandleShotState;
            _sm.OnShotComplete   += HandleShotComplete;
            _bound = true;

            PlaceAtBall();
            Debug.Log("[GolferTest] bound to ShotController + BallStateMachine.");
        }

        void Unbind()
        {
            if (!_bound) return;
            if (_shot != null) { _shot.OnShotResolved -= HandleShotResolved; _shot.OnStateChanged -= HandleShotState; }
            if (_sm   != null) _sm.OnShotComplete -= HandleShotComplete;
            _shot = null; _sm = null; _lab = null; _bound = false;
        }

        // ── Event handlers ────────────────────────────────────────────────────────────

        void HandleShotResolved(Golfin.Physics.ShotInput _, Golfin.Physics.BallPhysicsModifiers __)
        {
            if (anim == null) return;
            _swinging = true;
            ApplyCulling();                       // never cull a swing — the camera cuts away mid-shot
            anim.SetBool(PIsPutt, _shot != null && _shot.IsPutt);
            anim.SetTrigger(PSwing);
        }

        /// <summary>
        /// Address covers EVERY non-idle shot state, not just <see cref="ShotState.Aiming"/>.
        ///
        /// <para>SPEC §5.2 says to pick "the first state after idle", and Aiming is that state —
        /// but keying on it alone meant the golfer stood in his Idle pose with a club stretched
        /// out to the ball for the whole shot, because a swing can pass through Aiming in a
        /// frame or two on its way to Pulling / Timing / Flicking. Every one of those states is
        /// the player setting up or making a shot, which is exactly when a golfer is over the
        /// ball. Anything other than Idle means address.</para>
        /// </summary>
        ShotState _lastShotState = ShotState.Idle;

        void HandleShotState(ShotInputState s)
        {
            if (anim == null || _swinging) return;
            anim.SetBool(PIsPutt, s.IsPutt);

            // EDGE-TRIGGERED, and that is the whole point. OnStateChanged publishes every frame,
            // so setting a trigger unconditionally leaves one permanently pending: fire Cancel on
            // every idle frame and the golfer reaches Address only to be yanked straight back out
            // by the Cancel queued the frame before. He then renders as Idle — standing upright
            // with a club stretched out to the ball — for the entire shot, which is exactly what
            // it looked like.
            if (s.State == _lastShotState) return;
            _lastShotState = s.State;

            if (s.State != ShotState.Idle) { anim.ResetTrigger(PCancel);  anim.SetTrigger(PAddress); }
            else                           { anim.ResetTrigger(PAddress); anim.SetTrigger(PCancel);  }
        }

        void HandleShotCancelled()
        {
            if (anim == null) return;
            _swinging = false;
            ApplyCulling();
            anim.ResetTrigger(PSwing);
            anim.SetTrigger(PCancel);
        }

        void HandleShotComplete(ShotResult _)
        {
            if (anim != null) { anim.ResetTrigger(PSwing); anim.SetTrigger(PReset); }
            _swinging = false;
            _lastShotState = ShotState.Idle;
            ApplyCulling();
            PlaceAtBall();          // same frame as OnShotComplete, per SPEC §6
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
        /// </summary>
        static readonly Vector3 AddressHeadLocal = new Vector3(0.735f, 0f, -0.069f);

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

            Vector3 head = AddressHeadLocal;
            if (!rightHanded) head.x = -head.x;

            Vector3 p = ball - rot * head + d * stanceForwardOffset;
            p.y = GroundY(p, ball.y);

            transform.SetPositionAndRotation(p, rot);
        }

        /// <summary>Where the club head lands at address, for the harness to assert against.</summary>
        public Vector3 AddressClubHeadWorld => transform.TransformPoint(
            rightHanded ? AddressHeadLocal : new Vector3(-AddressHeadLocal.x, AddressHeadLocal.y, AddressHeadLocal.z));

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
            ApplyGripPose();

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
