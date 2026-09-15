// golfer_club_grip SPEC §3.12.6 stage 2 — two hands on one club, static, at address in play mode.
//
// What runs, in order (Run is a coroutine the verification runner hands control to at address):
//   1. Rig_Hands weight 0 → the hands are the clip's. Capture the clip hand rotations, the club
//      (GripTarget = two-hand average, ClubSlot authored pose), the shaft, Head, aim.
//   2. §3.12.4 anchors as closed-form geometry: per hand the rest-pose axis (o, d) in Hand-local
//      space is mapped onto the world shaft (FromToRotation), the roll about the shaft is fixed by
//      the palm rule (lead: back of hand toward Head; trail: palm toward the lead thumb), the
//      station puts the LeftHand origin 10 mm·s down-shaft of ClubStart and the trail little MCP
//      at the lead Index/Middle MCP gap. WristTarget = −(hand-local foot of the hand origin on the
//      axis) under an anchor that carries the hand frame.
//   3. §3.12.5: the club pivots about the head point (yaw about up, pitch about the horizontal
//      normal) and slides along the aim, on a coarse grid then a halving refine, to minimise the
//      wrist rotation the IK must impose = Σ Angle(clipHandRot, anchorHandRot). Deterministic.
//   4. Anchors written (runtime), Rig_Hands weight 1, three frames evaluated, then the §3.9.6-style
//      rows, wrist residuals, three full-res scene-cam frames (down-shaft, target-side, golfer's
//      eye) plus one gameplay-camera frame, JSON, and a bake file the edit-mode step writes back
//      into the prefab (play-mode edits are lost).
//   Verify mode (SessionState Stage2Verify): skip 1–3, measure the prefab as committed.
//
// Everything is gated by GOLFIN_GOLFER_TEST like the rest of the experiment. The recorder calls
// Run by reflection so it stays define-agnostic.

#if GOLFIN_GOLFER_TEST
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.Golfer;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Golfin.EditorTools.Golfer
{
    public static class HandHingeStage2
    {
        public static string PrefabPath => HandHingeStage0Tool.PrefabPath;
        public static string AssetPath  => HandHingeStage0Tool.AssetPath;
        public static string OutDir     => GolferTestCharacter.EvidenceRoot + "/stage2";
        public static string BakePath   => OutDir + "/stage2_bake.json";
        public const string Stage2Key       = "GolferTestVerification.Stage2";
        public const string Stage2VerifyKey = "GolferTestVerification.Stage2Verify";

        /// <summary>Character scale s (the "mm·s" of the spec) — 1.0 since the 2026-09-15 rescale to real size (prefab root 1.0516, body 1.75 m, club 1.06 m).</summary>
        public const float CharScale = 1.0f;
        const float LeadStationM  = 0.010f * CharScale;    // LeftHand origin down-shaft of ClubStart
        const float ThumbClockDeg = HandHingeStage1Tool.ThumbClockDeg;
        const float ThumbStationM = HandHingeStage1Tool.ThumbStationM;

        // ── menu ───────────────────────────────────────────────────────────────────────
        public const string RollModeKey = "GolferTestVerification.Stage2Roll";   // "palm" (§3.12.4 rules) | "minwrist"

        [MenuItem("GOLFIN/Golfer Test/Hinge/Stage 2 — solve (palm-rule roll, §3.12.4) + measure (Hole 06)")]
        public static void RunSolvePalm() => RunSolve("palm");

        [MenuItem("GOLFIN/Golfer Test/Hinge/Stage 2 — solve (min-wrist roll) + measure (Hole 06)")]
        public static void RunSolveMinWrist() => RunSolve("minwrist");

        [MenuItem("GOLFIN/Golfer Test/Hinge/Stage 2 — solve (anatomical wrist angle) + measure (Hole 06)")]
        public static void RunSolveAnatomy() => RunSolve("anatomy");

        [MenuItem("GOLFIN/Golfer Test/Hinge/Stage 2 — solve (min-wrist roll, wrist-angle cost, free station) + measure (Hole 06)")]
        public static void RunSolveWristAngle() => RunSolve("wristangle");

        [MenuItem("GOLFIN/Golfer Test/Hinge/Stage 2 — pitch scan with the IK in the loop (Hole 06)")]
        public static void RunPitchScan() => RunSolve("pitchscan");

        [MenuItem("GOLFIN/Golfer Test/Hinge/Roll rule — palm side ON")]
        public static void PalmSideOn() { PalmSideRoll = true; Debug.Log("[HingeStage2] roll rule: palm side"); }
        [MenuItem("GOLFIN/Golfer Test/Hinge/Roll rule — closest to the clip (default)")]
        public static void PalmSideOff() { PalmSideRoll = false; Debug.Log("[HingeStage2] roll rule: closest to the clip"); }

        [MenuItem("GOLFIN/Golfer Test/Hinge/Stage 2 — stance sweep: spine bend vs the posture guidelines (Hole 06)")]
        public static void RunStanceScan() => RunSolve("stancescan");

        [MenuItem("GOLFIN/Golfer Test/Hinge/Stage 2 — apply stage2_stance.json to the prefab (define ON)")]
        public static void ApplyStanceMenu() => Debug.Log(ApplyStanceToPrefab());

        public static void RunSolve(string rollMode = "palm")
        {
            SessionState.SetBool(Stage2Key, true);
            SessionState.SetBool(Stage2VerifyKey, false);
            SessionState.SetString(RollModeKey, rollMode);
            GolferTestVerificationRecorder.VerifyMixamoNative();
        }

        [MenuItem("GOLFIN/Golfer Test/Hinge/Stage 2 — verify the prefab as committed (Hole 06)")]
        public static void RunVerify()
        {
            SessionState.SetBool(Stage2Key, true);
            SessionState.SetBool(Stage2VerifyKey, true);
            GolferTestVerificationRecorder.VerifyMixamoNative();
        }

        // ── prefab structure (edit mode, define ON) ────────────────────────────────────

        /// <summary>
        /// Puts the §3.12.4 objects back on the prefab: HandHingeModel (inscribed poses as data,
        /// thumb aims from the rest-pose axis), GripAnchor_Lead/Trail + WristTarget under ClubSlot,
        /// Rig_Hands + IK_Lead/IK_Trail under GolferRig, RigBuilder.layers = [Rig_Grip, Rig_Hands].
        /// Idempotent: existing objects are reused. Anchors start at identity; the play-mode solve
        /// fills them and ApplyBakeToPrefab writes them back.
        /// </summary>
        public static string AuthorPrefabStructure(HandPose lead, HandPose trail) => AuthorPrefabStructure(lead, trail, 0.6f, 0f);

        /// <summary>
        /// Re-solve both finger poses (stage-1 inscribed wrap) against the axes with the given index-end
        /// offsets, then author the prefab with them. Used by the bake when the solve chose non-spec fractions.
        /// </summary>
        public static string AuthorPrefabStructureForAxes(float leadFrac, float trailFrac)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            HandPose lead, trail; string log;
            try
            {
                var anim = root.GetComponentInChildren<Animator>(true);
                var data = AssetDatabase.LoadAssetAtPath<HandHingeData>(AssetPath);
                HandHingeModel.UseData(data);
                lead = InscribedFor(anim, data.left, leadFrac, true, out string l1);
                trail = InscribedFor(anim, data.right, trailFrac, false, out string l2);
                log = l1 + l2;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return log + AuthorPrefabStructure(lead, trail, leadFrac, trailFrac);
        }

        static HandPose InscribedFor(Animator anim, HandHingeHand hand, float frac, bool lead, out string log)
        {
            HandHingeModel.GripAxisHandLocal(anim, hand, frac, out Vector3 o, out Vector3 d);
            var start = lead ? HandHingeModel.LeadGripStart() : HandHingeModel.TrailGripStart();
            var sb = new StringBuilder();
            FingerFlex Solve(int finger, FingerFlex f)
            {
                var r = HandHingeModel.SolveFingerInscribed(anim, hand, finger, f.spread, o, d);
                sb.Append(lead ? "lead " : "trail ").Append(r.metrics.name).Append(" @").Append(frac.ToString("F1")).Append(": ").Append(r.metrics.note).Append(" tip ").Append((r.metrics.tip * 1000f).ToString("F1")).Append(" mm minAll ").Append((r.metrics.minAll * 1000f).ToString("F1")).Append(" mm\n");
                return new FingerFlex(r.mcp, r.pip, r.dip, f.spread);
            }
            var pose = start;
            pose.index = Solve(HandHingeModel.Index, start.index);
            pose.middle = Solve(HandHingeModel.Middle, start.middle);
            pose.ring = Solve(HandHingeModel.Ring, start.ring);
            if (lead) pose.little = Solve(HandHingeModel.Little, start.little);   // trail little stays fixed 40/60/30
            HandHingeModel.ApplyRest(anim, hand);
            log = sb.ToString();
            return pose;
        }

        public static string AuthorPrefabStructure(HandPose lead, HandPose trail, float leadFrac, float trailFrac)
        {
            if (!EditorUserBuildSettings.activeScriptCompilationDefines.Contains("GOLFIN_GOLFER_TEST"))
                throw new InvalidOperationException("define OFF — refusing to save a gated prefab");
            var sb = new StringBuilder();
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var anim = root.GetComponentInChildren<Animator>(true);
                var data = AssetDatabase.LoadAssetAtPath<HandHingeData>(AssetPath);
                HandHingeModel.UseData(data);
                Transform Tf(string n) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
                Transform clubSlot = Tf("ClubSlot"), golferRig = Tf("GolferRig");
                if (clubSlot == null || golferRig == null) throw new InvalidOperationException("ClubSlot / GolferRig not found");

                // thumb aims from the rest-pose axes (the prefab contents ARE the rest pose)
                var hl = data.left; var hr = data.right;
                HandHingeModel.GripAxisHandLocal(anim, hl, leadFrac,  out Vector3 oL, out Vector3 dL);
                HandHingeModel.GripAxisHandLocal(anim, hr, trailFrac, out Vector3 oR, out Vector3 dR);
                lead.thumbAimHandLocal  = HandHingeModel.ThumbAimAlongShaft(anim, hl, oL, dL, ThumbClockDeg, ThumbStationM);
                trail.thumbAimHandLocal = HandHingeModel.ThumbAimAlongShaft(anim, hr, oR, dR, ThumbClockDeg, ThumbStationM);

                // HandHingeModel on the root, as data
                var hm = root.GetComponent<HandHingeModel>() ?? root.AddComponent<HandHingeModel>();
                var so = new SerializedObject(hm);
                so.FindProperty("anim").objectReferenceValue = anim;
                so.FindProperty("data").objectReferenceValue = data;
                so.FindProperty("applyEveryFrame").boolValue = true;
                WritePose(so.FindProperty("lead"), lead);
                WritePose(so.FindProperty("trail"), trail);
                so.ApplyModifiedPropertiesWithoutUndo();
                sb.AppendLine("HandHingeModel: axes lead " + leadFrac.ToString("F1") + " / trail " + trailFrac.ToString("F1") + " ·L_prox; applyEveryFrame=1, lead thumbAim=" + lead.thumbAimHandLocal.ToString("F4") + " trail thumbAim=" + trail.thumbAimHandLocal.ToString("F4"));

                // anchors under ClubSlot
                Transform aL = Child(clubSlot, "GripAnchor_Lead"), aR = Child(clubSlot, "GripAnchor_Trail");
                Transform wL = Child(aL, "WristTarget"), wR = Child(aR, "WristTarget");

                // Rig_Hands + the two IK constraints
                Transform rigHandsT = Child(golferRig, "Rig_Hands");
                var rigHands = rigHandsT.GetComponent<Rig>() ?? rigHandsT.gameObject.AddComponent<Rig>();
                rigHands.weight = 1f;
                Transform ikL = Child(rigHandsT, "IK_Lead"), ikR = Child(rigHandsT, "IK_Trail");
                Wire(ikL, anim, HumanBodyBones.LeftUpperArm,  HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftHand,  wL);
                Wire(ikR, anim, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, wR);

                // Rig_Stance — the stance edit (Cesar, 2026-09-15: "hands clearly touch the knees in that pose"):
                // an additive rotation on the Spine bone in Pivot space (the bone's own frame, so the bend is
                // body-relative through the swing), evaluated FIRST so the clip's hands, GripTarget and the arm IK all
                // see the adjusted torso. Data lives on the constraint (rotation Euler); an existing value is kept.
                Transform rigStanceT = Child(golferRig, "Rig_Stance");
                var rigStance = rigStanceT.GetComponent<Rig>() ?? rigStanceT.gameObject.AddComponent<Rig>();
                rigStance.weight = 1f;
                Transform bendT = Child(rigStanceT, "Stance_SpineBend");
                var ot = bendT.GetComponent<OverrideTransform>() ?? bendT.gameObject.AddComponent<OverrideTransform>();
                ot.weight = 1f;
                var od = ot.data;
                od.constrainedObject = anim.GetBoneTransform(HumanBodyBones.Spine); od.sourceObject = null;
                od.space = OverrideTransformData.Space.Pivot; od.position = Vector3.zero;
                od.positionWeight = 0f; od.rotationWeight = 1f;
                ot.data = od;
                sb.AppendLine("Rig_Stance: OverrideTransform on " + od.constrainedObject.name + ", Pivot space, rotation " + od.rotation.ToString("F3") + " (kept)");

                // Knee-flex guideline (15–25°; the clip has 31°): lift the Hips (Pivot-space position offset, kept as
                // data) while two-bone leg IK pins the feet to where the CLIP puts them each frame — the feet are
                // copied into targets by a layer that runs BEFORE the lift (Rig_StanceFeet), so the swing's own foot
                // motion survives and the knees straighten by exactly the lift.
                Transform rigFeetT = Child(golferRig, "Rig_StanceFeet");
                var rigFeet = rigFeetT.GetComponent<Rig>() ?? rigFeetT.gameObject.AddComponent<Rig>();
                rigFeet.weight = 1f;
                Transform footTL = Child(rigFeetT, "Stance_FootL_Target"), footTR = Child(rigFeetT, "Stance_FootR_Target");
                CopyBone(Child(rigFeetT, "Stance_FootL_Copy"), footTL, anim.GetBoneTransform(HumanBodyBones.LeftFoot));
                CopyBone(Child(rigFeetT, "Stance_FootR_Copy"), footTR, anim.GetBoneTransform(HumanBodyBones.RightFoot));
                Transform hipsT = Child(rigStanceT, "Stance_Hips");
                var oh = hipsT.GetComponent<OverrideTransform>() ?? hipsT.gameObject.AddComponent<OverrideTransform>();
                oh.weight = 1f;
                var hd = oh.data;
                hd.constrainedObject = anim.GetBoneTransform(HumanBodyBones.Hips); hd.sourceObject = null;
                hd.space = OverrideTransformData.Space.Pivot; hd.rotation = Vector3.zero;
                hd.positionWeight = 1f; hd.rotationWeight = 0f;
                oh.data = hd;
                Wire(Child(rigStanceT, "Stance_LegL_IK"), anim, HumanBodyBones.LeftUpperLeg,  HumanBodyBones.LeftLowerLeg,  HumanBodyBones.LeftFoot,  footTL);
                Wire(Child(rigStanceT, "Stance_LegR_IK"), anim, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, footTR);
                sb.AppendLine("Rig_StanceFeet: foot targets copied from the clip's feet; Rig_Stance: Hips lift " + hd.position.ToString("F4") + " (kept, hips-local), leg IK to the foot targets");

                // Rig_HandTwist (2026-09-15, a few hours) was a per-hand rotation invented to explain Olivia's
                // "backwards" hands; the cause was the builder placing her FBX instance at identity where Remy's sits
                // at a 180° yaw. Removed; a prefab that still carries it is cleaned here.
                var stale = golferRig.Find("Rig_HandTwist");
                if (stale != null) { UnityEngine.Object.DestroyImmediate(stale.gameObject); sb.AppendLine("Rig_HandTwist removed (stale)"); }

                var rb = root.GetComponent<RigBuilder>();
                var rigGrip = Tf("Rig_Grip")?.GetComponent<Rig>();
                rb.layers.Clear();
                rb.layers.Add(new RigLayer(rigFeet, true));
                rb.layers.Add(new RigLayer(rigStance, true));
                if (rigGrip != null) rb.layers.Add(new RigLayer(rigGrip, true));
                rb.layers.Add(new RigLayer(rigHands, true));
                sb.AppendLine("RigBuilder.layers = [" + string.Join(", ", rb.layers.Select(l => l.rig.name)) + "]");

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                sb.AppendLine("saved " + PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return sb.ToString();
        }

        static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t == null) { var go = new GameObject(name); t = go.transform; t.SetParent(parent, false); }
            return t;
        }

        /// <summary>A MultiParentConstraint that copies one bone's world pose into a rig target every frame (no offsets).</summary>
        static void CopyBone(Transform go, Transform target, Transform bone)
        {
            var c = go.GetComponent<MultiParentConstraint>() ?? go.gameObject.AddComponent<MultiParentConstraint>();
            c.weight = 1f;
            var d = c.data;
            d.constrainedObject = target;
            var arr = new WeightedTransformArray(); arr.Add(new WeightedTransform(bone, 1f));
            d.sourceObjects = arr;
            d.maintainPositionOffset = false; d.maintainRotationOffset = false;
            c.data = d;
        }

        static void Wire(Transform ikGo, Animator anim, HumanBodyBones root, HumanBodyBones mid, HumanBodyBones tip, Transform target)
        {
            var c = ikGo.GetComponent<TwoBoneIKConstraint>() ?? ikGo.gameObject.AddComponent<TwoBoneIKConstraint>();
            c.weight = 1f;
            var d = c.data;
            d.root = anim.GetBoneTransform(root); d.mid = anim.GetBoneTransform(mid); d.tip = anim.GetBoneTransform(tip);
            d.target = target; d.hint = null;
            d.targetPositionWeight = 1f; d.targetRotationWeight = 1f; d.hintWeight = 0f;
            d.maintainTargetPositionOffset = false; d.maintainTargetRotationOffset = false;
            c.data = d;
        }

        static void WritePose(SerializedProperty p, HandPose pose)
        {
            void F(string n, FingerFlex f)
            {
                var q = p.FindPropertyRelative(n);
                q.FindPropertyRelative("mcp").floatValue = f.mcp; q.FindPropertyRelative("pip").floatValue = f.pip;
                q.FindPropertyRelative("dip").floatValue = f.dip; q.FindPropertyRelative("spread").floatValue = f.spread;
            }
            F("index", pose.index); F("middle", pose.middle); F("ring", pose.ring); F("little", pose.little);
            p.FindPropertyRelative("thumbFlexIntermediate").floatValue = pose.thumbFlexIntermediate;
            p.FindPropertyRelative("thumbFlexDistal").floatValue = pose.thumbFlexDistal;
            p.FindPropertyRelative("thumbAimHandLocal").vector3Value = pose.thumbAimHandLocal;
        }

        /// <summary>The stage-1 inscribed solve, as data (evidence/stage1/supplementary_inscribed).</summary>
        public static HandPose InscribedLead() => new HandPose
        {
            index = new FingerFlex(5.5f, 59.7f, 80.0f, 0f), middle = new FingerFlex(26.2f, 84.8f, 77.0f, 0f),
            ring = new FingerFlex(32.2f, 78.3f, 72.4f, 0f), little = new FingerFlex(30.5f, 69.4f, 58.2f, 0f),
            thumbFlexIntermediate = 15f, thumbFlexDistal = 10f,
        };
        public static HandPose InscribedTrail() => new HandPose
        {
            index = new FingerFlex(42.9f, 98.3f, 73.9f, 4f), middle = new FingerFlex(55.8f, 81.7f, 80.0f, 0f),
            ring = new FingerFlex(56.5f, 84.7f, 76.3f, 0f), little = new FingerFlex(40f, 60f, 30f, 0f),
            thumbFlexIntermediate = 15f, thumbFlexDistal = 10f,
        };

        // ── the play-mode stage ────────────────────────────────────────────────────────

        struct HandGeom
        {
            public bool right;
            public HandHingeHand data;
            public Vector3 o, d;                 // axis, Hand-local
            public Vector3 fHand;                // foot of the hand origin on the axis, Hand-local
            public Vector3 fLittle;              // foot of the little MCP on the axis
            public Vector3 gapLocal;             // lead: midpoint of Index/Middle MCP (Hand-local)
            public Vector3 thumbInterLocal;      // posed ThumbIntermediate, Hand-local
            public Vector3 n, u;                 // palm normal, length axis, Hand-local
            public Vector3 forearmClip;          // elbow → wrist, world, rig off (the clip's forearm)
            public float uFrac;                  // index-end offset of the axis, fraction of L_prox (0.6 lead / 0 trail per §3.12.4)
        }

        /// <summary>Forearm-to-hand angle = the visible wrist bend: Angle(elbow→wrist, wrist→middle MCP).</summary>
        static float WristAngle(Vector3 forearmDir, Quaternion handRot, Vector3 uLocal) => Vector3.Angle(forearmDir, handRot * uLocal);

        /// <summary>Split the wrist bend into flexion (along the palm normal) and deviation (across the hand), degrees.</summary>
        static void WristSplit(Vector3 forearmDir, Transform hand, HandGeom g, out float total, out float flex, out float dev)
        {
            Vector3 u = hand.TransformDirection(g.u), n = hand.TransformDirection(g.n);
            Vector3 across = Vector3.Cross(n, u).normalized;
            total = Vector3.Angle(forearmDir, u);
            flex = Mathf.Asin(Mathf.Clamp(Vector3.Dot(forearmDir, n), -1f, 1f)) * Mathf.Rad2Deg;      // + = palm side (flexion)
            dev  = Mathf.Asin(Mathf.Clamp(Vector3.Dot(forearmDir, across), -1f, 1f)) * Mathf.Rad2Deg;
        }

        struct AnchorSolve
        {
            public Quaternion rotL, rotR;        // hand frames (world)
            public Vector3 posL, posR;           // anchor positions on the shaft (world) = where fHand lands
            public float rollL, rollR, ruleL, ruleR;
            public float stationL, stationR, stationGap;
            public Vector3 leadThumbW;
            public float wristL, wristR;         // predicted forearm-to-hand angle at the anchor (clip forearm)
        }

        static float Heading(Component shot) { var p = shot?.GetType().GetProperty("CameraHeadingRadians"); return p == null ? 0f : (float)p.GetValue(shot); }

        public static IEnumerator Run(MonoBehaviour runner, GameObject golfer, Animator anim, Component shot, Transform ball)
        {
            bool verify = SessionState.GetBool(Stage2VerifyKey, false);
            string rollMode = SessionState.GetString(RollModeKey, "palm");
            SessionState.SetBool(Stage2Key, false); SessionState.SetBool(Stage2VerifyKey, false);
            string tag = verify ? "verify" : "solve_" + rollMode;
            Directory.CreateDirectory(OutDir);
            var log = new StringBuilder();
            void L(string s) { log.AppendLine(s); Debug.Log("[HingeStage2] " + s); }
            L("stage 2 " + (verify ? "VERIFY (prefab as committed)" : "SOLVE, roll mode " + rollMode) + " at address, captureDeltaTime=" + Time.captureDeltaTime.ToString("F4"));

            var all = golfer.GetComponentsInChildren<Transform>(true);
            Transform Tf(string n) => all.FirstOrDefault(t => t.name == n);
            Transform gripTarget = Tf("GripTarget"), clubSlot = Tf("ClubSlot"), clubStart = Tf("ClubStart"), clubEnd = Tf("ClubEnd");
            Transform aL = Tf("GripAnchor_Lead"), aR = Tf("GripAnchor_Trail");
            Transform wL = aL?.Find("WristTarget"), wR = aR?.Find("WristTarget");
            var rigHands = golfer.GetComponentsInChildren<Rig>(true).FirstOrDefault(r => r.gameObject.name == "Rig_Hands");
            var hm = golfer.GetComponent<HandHingeModel>();
            if (gripTarget == null || clubSlot == null || clubStart == null || clubEnd == null || aL == null || aR == null || wL == null || wR == null || rigHands == null || hm == null || hm.Data == null)
            { L("MISSING: gripTarget=" + (gripTarget != null) + " clubSlot=" + (clubSlot != null) + " clubStart=" + (clubStart != null) + " clubEnd=" + (clubEnd != null) + " anchors=" + (aL != null && aR != null) + " wrists=" + (wL != null && wR != null) + " Rig_Hands=" + (rigHands != null) + " HandHingeModel=" + (hm != null) + " — stage 2 cannot run"); File.WriteAllText(Path.Combine(OutDir, "stage2_" + tag + "_console.txt"), log.ToString()); yield break; }

            HandHingeModel.UseData(hm.Data);
            L("character " + GolferTestCharacter.Name + ": contact radius " + Mm(HandHingeModel.ContactM) + " mm (finger half-thickness " + Mm(HandHingeModel.FingerHalfThicknessM) + " mm)");
            Transform handL = anim.GetBoneTransform(HumanBodyBones.LeftHand), handR = anim.GetBoneTransform(HumanBodyBones.RightHand);
            Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
            float hAim = Heading(shot);
            Vector3 aimDir = new Vector3(Mathf.Cos(hAim), 0f, Mathf.Sin(hAim));
            RollAim = aimDir;
            L("roll rule: " + (PalmSideRoll ? "PALM SIDE (lead palm → −aim, trail palm → +aim)" : "closest to the clip") + "; aim " + V(aimDir));

            // 1. rig off → the clip's hands
            float rigRestore = rigHands.weight;
            rigHands.weight = 0f; yield return null; yield return null; yield return new WaitForEndOfFrame();
            Quaternion clipRotL = handL.rotation, clipRotR = handR.rotation;
            Vector3 clipPosL = handL.position, clipPosR = handR.position;
            L("clip hands (rig OFF" + (verify ? ", verify mode — restored to 1 after this capture" : "") + "): L pos=" + V(clipPosL) + " rot=" + Q(clipRotL) + " | R pos=" + V(clipPosR) + " rot=" + Q(clipRotR));

            if (!verify && rollMode == "stancescan")
            {
                // ── the stance edit, measured: sweep the Spine bend and read the posture rows against the
                //    published address guidelines (reference/WRIST_ANGLES_AT_ADDRESS.md § posture) with the
                //    CLIP's hands (rig hands off). The chosen bend is written to stage2_stance.json; the pitch
                //    scan then runs on top of it.
                var ot = golfer.GetComponentsInChildren<OverrideTransform>(true).FirstOrDefault(o => o.gameObject.name == "Stance_SpineBend");
                var oh = golfer.GetComponentsInChildren<OverrideTransform>(true).FirstOrDefault(o => o.gameObject.name == "Stance_Hips");
                if (ot == null || oh == null) { L("no Stance_SpineBend / Stance_Hips on this prefab — author the structure first"); File.WriteAllText(Path.Combine(OutDir, "stage2_" + tag + "_console.txt"), log.ToString()); rigHands.weight = rigRestore; yield break; }
                Transform spine = anim.GetBoneTransform(HumanBodyBones.Spine), hipsB = anim.GetBoneTransform(HumanBodyBones.Hips);
                Vector3 hips0 = oh.data.position;
                Vector3 upHipsLocal = hipsB.InverseTransformDirection(Vector3.up);   // the hips' frame does not change with the lift
                Vector3 fwdS = ball != null ? Vector3.ProjectOnPlane(ball.position - golfer.transform.position, Vector3.up).normalized : Vector3.Cross(aimDir, Vector3.up);
                Vector3 rot0 = ot.data.rotation;
                var tbl = new StringBuilder("lift mm bend° | torso tilt° | knee flex L/R° | arm hang L/R° | hands↔thighs mm (surface) | hands vs chin mm | hands over knee mm | hand height mm | shoulder height mm\n");
                float bestBend = 0f, bestLift = 0f, bestScore = float.MaxValue; Vector3 bestEuler = rot0, bestHips = hips0; string bestWhy = "";
                foreach (float lift in new[] { 0f, 0.02f, 0.04f, 0.06f, 0.08f, 0.10f })
                foreach (float bend in new[] { 0f, 5f, 10f, -5f, 15f })
                {
                    // the bend axis is the target line, expressed in the Spine's own frame AT ADDRESS (Pivot space
                    // post-multiplies, so the stored Euler is body-relative); the lift is world-up in the hips' frame
                    { var h = oh.data; h.position = hips0 + upHipsLocal * lift; oh.data = h; }
                    var d = ot.data; d.rotation = bend == 0f ? rot0 : Quaternion.AngleAxis(bend, spine.InverseTransformDirection(aimDir)).eulerAngles; ot.data = d;
                    yield return null; yield return null; yield return new WaitForEndOfFrame();
                    var ps = Posture(anim, golfer.transform, fwdS, out float tilt, out float kL, out float kR, out float aL0, out float aR0, out float thighMm, out float chinMm);
                    float kc = KneeClear(anim, out _, out float hy, out float ky);
                    float shY = 0.5f * (anim.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.y + anim.GetBoneTransform(HumanBodyBones.RightUpperArm).position.y) - golfer.transform.position.y;
                    float handsY = 0.5f * (handL.position.y + handR.position.y) - golfer.transform.position.y;
                    tbl.Append(Mm(lift).PadLeft(6)).Append(" ").Append(bend.ToString("F0").PadLeft(4)).Append(" | ").Append(F1(tilt)).Append(" | ").Append(F1(kL)).Append("/").Append(F1(kR)).Append(" | ").Append(F1(aL0)).Append("/").Append(F1(aR0))
                       .Append(" | ").Append(Mm(thighMm)).Append(" (").Append(Mm(thighMm - ThighRadiusM - HandHalfM)).Append(") | ").Append(Mm(chinMm)).Append(" | ").Append(Mm(kc)).Append(" | ").Append(Mm(handsY)).Append(" | ").Append(Mm(shY)).Append("\n");
                    // pick: knee flex AND torso tilt inside the guideline bands, the CLIP hands ≥ KneeClearM + 60 mm
                    // over the knee (the pitch scan lowered the hands 62 mm to straighten the wrists last time), then
                    // the smallest edit (|bend| + lift in cm)
                    bool inBand = tilt >= TorsoTiltMinDeg && tilt <= TorsoTiltMaxDeg && kL >= KneeFlexMinDeg && kL <= KneeFlexMaxDeg && kR >= KneeFlexMinDeg && kR <= KneeFlexMaxDeg;
                    bool room = kc >= KneeClearM + 0.060f;
                    float score = (inBand ? 0f : 1000f) + (room ? 0f : 100f + Mathf.Max(0f, KneeClearM + 0.060f - kc) * 1000f) + Mathf.Abs(bend) + lift * 100f;
                    if (score < bestScore) { bestScore = score; bestBend = bend; bestLift = lift; bestEuler = d.rotation; bestHips = oh.data.position; bestWhy = (inBand ? "posture in band" : "posture OUT of band") + (room ? ", room for the scan" : ", short of room"); }
                }
                L("STANCE SWEEP (Hips lift with the feet pinned by leg IK × Spine bend about the target line; clip hands, rig hands off):\n" + tbl);
                L("stance pick: lift " + Mm(bestLift) + " mm, bend " + bestBend.ToString("F0") + "° (" + bestWhy + "), Euler " + bestEuler.ToString("F4") + ", hips offset " + bestHips.ToString("F4") + " — guideline: torso tilt " + F1(TorsoTiltMinDeg) + "–" + F1(TorsoTiltMaxDeg) + "°, knee flex " + F1(KneeFlexMinDeg) + "–" + F1(KneeFlexMaxDeg) + "°, clip hands ≥ " + Mm(KneeClearM + 0.060f) + " mm over the knee");
                File.WriteAllText(Path.Combine(OutDir, "stage2_stance.json"), "{\n  \"stanceBendDeg\": " + F(bestBend) + ",\n  \"hipsLiftM\": " + F(bestLift) + ",\n  \"rotationEuler\": " + J(bestEuler) + ",\n  \"hipsOffsetLocal\": " + J(bestHips) + ",\n  \"why\": \"" + bestWhy + "\"\n}\n");
                { var d = ot.data; d.rotation = rot0; ot.data = d; var h = oh.data; h.position = hips0; oh.data = h; }
                rigHands.weight = rigRestore;
                File.WriteAllText(Path.Combine(OutDir, "stage2_" + tag + "_console.txt"), log.ToString());
                L("wrote stage2_stance.json"); yield break;
            }

            float[] leadFracs = (rollMode == "wristangle" || rollMode == "pitchscan") ? new[] { 0.2f, 0.4f, 0.6f, 0.8f } : new[] { 0.6f };
            var gLs = new HandGeom[leadFracs.Length];
            Transform foreL = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm), foreR = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform shL = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm), shR = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            float reachL = Vector3.Distance(shL.position, foreL.position) + Vector3.Distance(foreL.position, handL.position);
            float reachR = Vector3.Distance(shR.position, foreR.position) + Vector3.Distance(foreR.position, handR.position);
            for (int i = 0; i < leadFracs.Length; i++) { gLs[i] = Geom(anim, hm.Data.left, leadFracs[i]); gLs[i].forearmClip = (handL.position - foreL.position).normalized; }
            var gL = gLs[leadFracs.Length == 1 ? 0 : 2];   // 0.6 = the §3.12.4 lead
            // trail axis offset candidates (§3.12.4 says 0; the clip's trail knuckle row sits ~40° off the shaft, so it is a solve DOF in wristangle mode)
            float[] trailFracs = (rollMode == "wristangle" || rollMode == "pitchscan") ? new[] { 0f, 0.2f, 0.4f, 0.6f } : new[] { 0f };
            var gRs = new HandGeom[trailFracs.Length];
            for (int i = 0; i < trailFracs.Length; i++) { gRs[i] = Geom(anim, hm.Data.right, trailFracs[i]); gRs[i].forearmClip = (handR.position - foreR.position).normalized; }
            var gR = gRs[0];
            // wrappability per axis candidate: the stage-1 inscribed solve must reach the contact circle
            // with every wrapped finger (tip within 5 mm, bones outside the mesh) — a fraction the fingers
            // cannot wrap is not an axis, whatever it does for the wrist. Run on the live hand, rig off;
            // HandHingeModel.LateUpdate restores the data pose next frame.
            bool[] leadOk = new bool[gLs.Length], trailOk = new bool[gRs.Length];
            for (int i = 0; i < gLs.Length; i++) leadOk[i] = Wrappable(anim, hm.Data.left, gLs[i], true, L);
            for (int i = 0; i < gRs.Length; i++) trailOk[i] = Wrappable(anim, hm.Data.right, gRs[i], false, L);
            HandHingeModel.ApplyRest(anim, hm.Data.left); HandHingeModel.ApplyRest(anim, hm.Data.right);
            L("reach: lead arm " + Mm(reachL) + " mm from " + shL.name + ", trail " + Mm(reachR) + " mm");
            WristSplit(gL.forearmClip, handL, gL, out float clipWL, out float clipFL, out float clipDL);
            WristSplit(gR.forearmClip, handR, gR, out float clipWR, out float clipFR, out float clipDR);
            L("CLIP wrist angles (forearm→hand): lead " + F1(clipWL) + "° (flex " + F1(clipFL) + ", dev " + F1(clipDL) + ") | trail " + F1(clipWR) + "° (flex " + F1(clipFR) + ", dev " + F1(clipDR) + ")");
            { float kc0 = KneeClear(anim, out float kd0, out float hy0, out float ky0); L("CLIP hands above knees: lowest hand joint " + Mm(kc0) + " mm above the higher knee (hand " + Mm(hy0) + " / knee " + Mm(ky0) + " mm over the root), nearest hand joint to a knee " + Mm(kd0) + " mm; floor " + Mm(KneeClearM) + " mm");
              float al0 = ArmLegClear(anim, out string alp0); L("CLIP arms vs legs: nearest arm bone to a leg bone " + Mm(al0) + " mm centreline (" + alp0 + "); floor " + Mm(ArmLegClearM) + " mm"); }
            if (verify) { rigHands.weight = rigRestore; yield return null; yield return null; }
            L("hand-local lead: o=" + V(gL.o) + " d=" + V(gL.d) + " fHand=" + V(gL.fHand) + " gap=" + V(gL.gapLocal) + " thumbInter=" + V(gL.thumbInterLocal));
            L("hand-local trail: o=" + V(gR.o) + " d=" + V(gR.d) + " fHand=" + V(gR.fHand) + " fLittle=" + V(gR.fLittle));

            if (!verify && ball != null)
            {
                // ClubSlot's local pose lives under GripTarget = the average of the two hand-bone frames, which are
                // rig-specific: another character's bake hangs the club wherever ITS hand frames point (Olivia with
                // Remy's pose: mirrored, head 1.6 m from the ball). Before solving, aim the shaft from the hands at
                // the ball and put the butt 30 mm behind the hand centre; roll is kept (the face-square fix owns it).
                Vector3 s0c = clubStart.position, ec = clubEnd.position, sc = (ec - s0c).normalized;
                // the head must land ON THE GROUND: azimuth toward the ball, elevation from the hand height and
                // the shaft length (butt 30 mm behind the hand centre) — the ball's plan distance is then whatever the
                // bake's addressHeadLocal makes PlaceAtBall stand the golfer at, so a pass-1 bake + re-scan closes it
                Vector3 planToBall = Vector3.ProjectOnPlane(ball.position - gripTarget.position, Vector3.up).normalized;
                float shaftL = Vector3.Distance(s0c, ec), drop = gripTarget.position.y - ball.position.y;
                float sinE = Mathf.Clamp(drop / (shaftL - 0.03f), 0f, 0.99f);
                Vector3 want = (planToBall * Mathf.Sqrt(1f - sinE * sinE) - Vector3.up * sinE).normalized;
                float headOff = Vector3.Distance(new Vector3(ec.x, 0f, ec.z), new Vector3(ball.position.x, 0f, ball.position.z));
                float headUp = ec.y - ball.position.y;
                if (headOff > 0.30f || Vector3.Dot(sc, want) < 0.8f || Mathf.Abs(headUp) > 0.10f)
                {
                    clubSlot.rotation = Quaternion.FromToRotation(sc, want) * clubSlot.rotation;
                    yield return null;
                    clubSlot.position += (gripTarget.position - want * 0.03f) - clubStart.position;
                    yield return null; yield return new WaitForEndOfFrame();
                    L("RE-AIM: the club head was " + Mm(headOff) + " mm from the ball in plan and " + Mm(headUp) + " mm above it (shaft·want " + F(Vector3.Dot(sc, want)) + ") — ClubSlot re-aimed from the hands toward the ball with the head on the ground (elevation " + F1(Mathf.Asin(sinE) * Mathf.Rad2Deg) + "°), butt 30 mm behind the hand centre; now head " + Mm(Vector3.Distance(new Vector3(clubEnd.position.x, 0f, clubEnd.position.z), new Vector3(ball.position.x, 0f, ball.position.z))) + " mm off, ClubSlot local " + V(clubSlot.localPosition) + " " + Q(clubSlot.localRotation));
                }
            }
            Vector3 E = clubEnd.position, S0 = clubStart.position, sdir = (E - S0).normalized;
            // the CLIP's hands against the §3.12.4 rules, before anything is solved
            foreach (var (whichHand, g, h) in new[] { ("lead", gL, handL), ("trail", gR, handR) })
            {
                Vector3 dW = h.TransformDirection(g.d), nW = h.TransformDirection(g.n), uW = h.TransformDirection(g.u);
                Vector3 tunnel = h.TransformPoint(g.fHand);
                float st = Vector3.Dot(tunnel - S0, sdir), off = AxisDist(tunnel, S0, sdir);
                L("CLIP " + whichHand + ": little→index vs shaft dot=" + F(Vector3.Dot(dW, sdir)) + " | palm n·toHead=" + F(Vector3.Dot(nW, (head.position - h.position).normalized)) + " n·aim=" + F(Vector3.Dot(nW, aimDir)) + " n·up=" + F(nW.y) + " | length u·shaft=" + F(Vector3.Dot(uW, sdir)) + " u·up=" + F(uW.y) + " | tunnel station " + Mm(st) + " mm, " + Mm(off) + " mm off the axis");
            }
            L("club at address: ClubStart=" + V(S0) + " ClubEnd=" + V(E) + " shaft=" + V(sdir) + " aim=" + V(aimDir) + " Head=" + V(head.position) + " GripTarget=" + V(gripTarget.position) + " ClubSlot.local pos=" + V(clubSlot.localPosition) + " rot=" + Q(clubSlot.localRotation));

            AnchorSolve best;
            if (!verify)
            {
                // 2+3. §3.12.5 — coarse grid then halving refine, deterministic
                Vector3 up = Vector3.up;
                Vector3 pitchAxis = Vector3.Cross(up, sdir).normalized;
                float bestCost = float.MaxValue; float bYaw = 0, bPitch = 0, bSlide = 0, bStL = LeadStationM, bGapOff = 0f; best = default;
                bool anatomy = rollMode == "anatomy" || rollMode == "wristangle" || rollMode == "pitchscan";   // wrist-angle cost + free station
                bool wide = rollMode == "wristangle" || rollMode == "pitchscan";      // wider pivot, cheaper displacement
                float dispPrice = wide ? 0.2f : 0.5f;                                // ° per mm
                int bTrail = 0, bLead = leadFracs.Length == 1 ? 0 : 2;
                AnchorSolve Eval(float yaw, float pitch, float slide, out float cost, float stL = LeadStationM, float gapOff = 0f, int trailIdx = 0, int leadIdx = -1)
                {
                    if (leadIdx < 0) leadIdx = bLead;
                    Quaternion Rp = Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(pitch, pitchAxis);
                    Vector3 s0 = E + Rp * (S0 - E) + aimDir * slide;
                    Vector3 dir = (Rp * sdir).normalized;
                    var gRc = gRs[trailIdx]; var gLc = gLs[leadIdx];
                    var a = Anchors(gLc, gRc, s0, dir, head.position, clipRotL, clipRotR, rollMode, stL, gapOff);
                    // wrist rotation the IK must impose, plus the hand displacement it must impose
                    // (0.5° per mm — a pivot of a 0.9 m club moves the butt 16 mm per degree, so a
                    // rotation-only cost runs to the grid corner and out of the arms' reach)
                    Vector3 hpL = a.posL - a.rotL * gLc.fHand, hpR = a.posR - a.rotR * gRc.fHand;
                    float disp = Vector3.Distance(hpL, clipPosL) + Vector3.Distance(hpR, clipPosR);
                    // anatomy: the visible wrist bend itself (forearm→hand angle at the anchor, clip forearm),
                    // not the rotation from the clip — Cesar: "the wrists bend too much compared to real golfers"
                    cost = (anatomy ? a.wristL + a.wristR : Quaternion.Angle(clipRotL, a.rotL) + Quaternion.Angle(clipRotR, a.rotR)) + disp * 1000f * dispPrice;
                    // the two-bone IK cannot reach past the arm: hard penalty outside 97 % of the shoulder-to-hand length
                    if (Vector3.Distance(hpL, shL.position) > reachL * 0.97f || Vector3.Distance(hpR, shR.position) > reachR * 0.97f) cost += 1000f;
                    if (!leadOk[leadIdx] || !trailOk[trailIdx]) cost += 1000f;
                    return a;
                }
                float[] stLs  = anatomy ? new[] { 0.0076f, 0.02f, 0.03f, 0.04f, 0.05f, 0.06f } : new[] { LeadStationM };
                float[] gapOs = wide ? new[] { -0.008f, -0.004f, 0f, 0.004f, 0.008f } : anatomy ? new[] { -0.02f, -0.01f, 0f, 0.01f, 0.02f } : new[] { 0f };
                float range = wide ? 12f : 6f, stepDeg = wide ? 3f : 2f, slideRange = wide ? 0.02f : 0.015f;
                for (float yaw = -range; yaw <= range + 0.01f; yaw += stepDeg)
                    for (float pitch = -range; pitch <= range + 0.01f; pitch += stepDeg)
                        for (float slide = -slideRange; slide <= slideRange + 0.0001f; slide += 0.005f)
                            foreach (float stL in stLs) foreach (float gapOff in gapOs) for (int ti = 0; ti < gRs.Length; ti++) for (int li = 0; li < gLs.Length; li++)
                            { var a = Eval(yaw, pitch, slide, out float c, stL, gapOff, ti, li); if (c < bestCost) { bestCost = c; bYaw = yaw; bPitch = pitch; bSlide = slide; bStL = stL; bGapOff = gapOff; bTrail = ti; bLead = li; best = a; } }
                L("§3.12.5 coarse: yaw=" + bYaw + " pitch=" + bPitch + " slide=" + bSlide.ToString("F3") + " leadStation=" + Mm(bStL) + " gapOff=" + Mm(bGapOff) + " cost=" + bestCost.ToString("F2"));
                float sy = stepDeg * 0.5f, sp = stepDeg * 0.5f, ss = 0.0025f, sst = 0.005f, sg = wide ? 0.002f : 0.005f;
                for (int round = 0; round < 4; round++)
                {
                    float cy = bYaw, cp = bPitch, cs = bSlide, cst = bStL, cg = bGapOff;
                    for (int iy = -1; iy <= 1; iy++) for (int ip = -1; ip <= 1; ip++) for (int isl = -1; isl <= 1; isl++)
                    for (int ist = (anatomy ? -1 : 0); ist <= (anatomy ? 1 : 0); ist++) for (int ig = (anatomy ? -1 : 0); ig <= (anatomy ? 1 : 0); ig++)
                    {
                        float stL = Mathf.Clamp(cst + ist * sst, 0.005f, 0.065f), gapOff = Mathf.Clamp(cg + ig * sg, wide ? -0.008f : -0.025f, wide ? 0.008f : 0.025f);
                        var a = Eval(cy + iy * sy, cp + ip * sp, cs + isl * ss, out float c, stL, gapOff, bTrail, bLead);
                        if (c < bestCost) { bestCost = c; bYaw = cy + iy * sy; bPitch = cp + ip * sp; bSlide = cs + isl * ss; bStL = stL; bGapOff = gapOff; best = a; }
                    }
                    sy *= 0.5f; sp *= 0.5f; ss *= 0.5f; sst *= 0.5f; sg *= 0.5f;
                }
                gR = gRs[bTrail]; gL = gLs[bLead];
                L("§3.12.5 axes: lead index-end offset " + gL.uFrac.ToString("F1") + "·L_prox (spec 0.6)");
                L("§3.12.5 stations: lead " + Mm(bStL) + " mm from ClubStart (spec 10 mm·s = " + Mm(LeadStationM) + "), trail little MCP " + Mm(bGapOff) + " mm from the lead gap, trail axis index-end offset " + gR.uFrac.ToString("F1") + "·L_prox (spec 0); predicted wrist angles L " + F1(best.wristL) + "° R " + F1(best.wristR) + "°" + (bestCost >= 1000f ? " — NO REACHABLE CANDIDATE" : ""));
                var zero = Eval(0, 0, 0, out float cost0, LeadStationM, 0f, 0, leadFracs.Length == 1 ? 0 : 2);
                L("§3.12.5 refined: yaw=" + bYaw.ToString("F2") + "° pitch=" + bPitch.ToString("F2") + "° slide=" + (bSlide * 1000f).ToString("F1") + " mm cost=" + bestCost.ToString("F2") + " (rot L " + Quaternion.Angle(clipRotL, best.rotL).ToString("F1") + "° R " + Quaternion.Angle(clipRotR, best.rotR).ToString("F1") + "°, disp L " + Mm(Vector3.Distance(best.posL - best.rotL * gL.fHand, clipPosL)) + " R " + Mm(Vector3.Distance(best.posR - best.rotR * gR.fHand, clipPosR)) + " mm) | unpivoted cost " + cost0.ToString("F2") + ": rot L " + Quaternion.Angle(clipRotL, zero.rotL).ToString("F1") + "° R " + Quaternion.Angle(clipRotR, zero.rotR).ToString("F1") + "°, disp L " + Mm(Vector3.Distance(zero.posL - zero.rotL * gL.fHand, clipPosL)) + " R " + Mm(Vector3.Distance(zero.posR - zero.rotR * gR.fHand, clipPosR)) + " mm");

                // apply the pivot to the club: ClubSlot local under GripTarget
                {
                    Quaternion Rp = Quaternion.AngleAxis(bYaw, up) * Quaternion.AngleAxis(bPitch, pitchAxis);
                    Vector3 newPos = E + aimDir * bSlide + Rp * (clubSlot.position - E);
                    Quaternion newRot = Rp * clubSlot.rotation;
                    clubSlot.localPosition = gripTarget.InverseTransformPoint(newPos);
                    clubSlot.localRotation = Quaternion.Inverse(gripTarget.rotation) * newRot;
                }
                yield return null; yield return new WaitForEndOfFrame();
                S0 = clubStart.position; E = clubEnd.position; sdir = (E - S0).normalized;
                best = Anchors(gL, gR, S0, sdir, head.position, clipRotL, clipRotR, rollMode, bStL, bGapOff);
                L("club after pivot: ClubStart=" + V(S0) + " ClubEnd=" + V(E) + " shaft=" + V(sdir) + " ClubSlot.local pos=" + V(clubSlot.localPosition) + " rot=" + Q(clubSlot.localRotation));

                // 4. anchors (world → local under ClubSlot), wrist targets
                aL.SetPositionAndRotation(best.posL, best.rotL); wL.localPosition = -gL.fHand; wL.localRotation = Quaternion.identity;
                aR.SetPositionAndRotation(best.posR, best.rotR); wR.localPosition = -gR.fHand; wR.localRotation = Quaternion.identity;
                rigHands.weight = 1f;
                // Animation Rigging binds its target transforms at RigBuilder.Build() — the spawn-time
                // values (identity anchors). Three runs measured identical hands whatever the anchors
                // said. Rebuild so the constraints bind the anchors as they are now.
                var rb = golfer.GetComponentInChildren<RigBuilder>(true);
                if (rb != null) { rb.Build(); L("RigBuilder.Build() after writing the anchors"); }
                L("anchors written: lead local pos=" + V(aL.localPosition) + " rot=" + Q(aL.localRotation) + " wrist=" + V(wL.localPosition) + " | trail local pos=" + V(aR.localPosition) + " rot=" + Q(aR.localRotation) + " wrist=" + V(wR.localPosition));
                L("anchor solve: lead station=" + (best.stationL * 1000f).ToString("F1") + " mm roll=" + best.rollL.ToString("F1") + "° rule=" + best.ruleL.ToString("F3") + " | trail station=" + (best.stationR * 1000f).ToString("F1") + " mm (gap station " + (best.stationGap * 1000f).ToString("F1") + ") roll=" + best.rollR.ToString("F1") + "° rule=" + best.ruleR.ToString("F3"));

                // bake file for the edit-mode write-back
                var bake = new StringBuilder();
                bake.Append("{\n  \"clubSlotLocalPos\": ").Append(J(clubSlot.localPosition)).Append(",\n  \"clubSlotLocalRot\": ").Append(J(clubSlot.localRotation))
                    .Append(",\n  \"leadAnchorLocalPos\": ").Append(J(aL.localPosition)).Append(",\n  \"leadAnchorLocalRot\": ").Append(J(aL.localRotation)).Append(",\n  \"leadWristLocalPos\": ").Append(J(wL.localPosition))
                    .Append(",\n  \"trailAnchorLocalPos\": ").Append(J(aR.localPosition)).Append(",\n  \"trailAnchorLocalRot\": ").Append(J(aR.localRotation)).Append(",\n  \"trailWristLocalPos\": ").Append(J(wR.localPosition))
                    .Append(",\n  \"leadStationM\": ").Append(F(bStL)).Append(", \"trailGapOffsetM\": ").Append(F(bGapOff)).Append(", \"leadAxisUFrac\": ").Append(F(gL.uFrac)).Append(", \"trailAxisUFrac\": ").Append(F(gR.uFrac)).Append(", \"rollMode\": \"").Append(rollMode).Append("\"")
                    .Append(",\n  \"pivotYawDeg\": ").Append(F(bYaw)).Append(", \"pivotPitchDeg\": ").Append(F(bPitch)).Append(", \"slideM\": ").Append(F(bSlide)).Append(", \"costDeg\": ").Append(F(bestCost)).Append("\n}\n");
                File.WriteAllText(BakePath, bake.ToString()); File.WriteAllText(Path.Combine(OutDir, "stage2_bake_" + rollMode + ".json"), bake.ToString());
            }
            else
            {
                best = Anchors(gL, gR, S0, sdir, head.position, clipRotL, clipRotR, rollMode, LeadStationM, 0f);   // what the geometry says the anchors should be, for comparison
                L("prefab anchors: lead local pos=" + V(aL.localPosition) + " rot=" + Q(aL.localRotation) + " | trail local pos=" + V(aR.localPosition) + " rot=" + Q(aR.localRotation) + " | geometry now: lead pos=" + V(best.posL) + " trail pos=" + V(best.posR));
            }

            // let the IK settle, then measure at end of frame (fingers are posed in LateUpdate)
            for (int i = 0; i < 3; i++) yield return null;
            yield return new WaitForEndOfFrame();

            if (!verify && rollMode == "pitchscan")
            {
                // ── the IK in the loop: pitch the club about the head (hands lower / further out with
                //    the arms extending), keep the solved axes, stations and roll mode, rebuild the rig,
                //    and read the POST-IK wrist angles. The predictor cannot see this because it uses
                //    the clip's forearm; only the IK knows where the elbow goes.
                Vector3 up = Vector3.up;
                var rb2 = golfer.GetComponentInChildren<RigBuilder>(true);
                Vector3 slot0Pos = clubSlot.localPosition; Quaternion slot0Rot = clubSlot.localRotation;
                Vector3 S0b = clubStart.position, Eb = clubEnd.position, sdirb = (Eb - S0b).normalized;
                Vector3 pitchAxisB = Vector3.Cross(up, sdirb).normalized;
                var scan = new StringBuilder();
                scan.Append("station yaw pitch gap | wristL (flex,dev) | wristR (flex,dev) | dispL dispR mm | reachL reachR | elbowL elbowR | headAtBall mm | onShaft L/R mm | joint clearance mm\n");
                // station × yaw × pitch: the trail arm is the reach limit (elbow straight at −2° pitch), so the
                // butt-ward station (hands nearer the body) and a yaw toward the trail shoulder buy reach.
                // stance edit (2026-09-15): the shoulders moved up and back, so the club also needs a translation
                // toward the golfer ("stand closer" — PlaceAtBall then puts the golfer that much nearer the ball);
                // rotations about the head cannot express it, and without it every grip-feasible configuration had
                // the lead arm reaching 38° forward.
                float[] pitches = { 2f, 0f, -2f, -4f, -6f };
                float[] yaws = { 0f, -4f, -8f, -12f, -16f };
                float[] stations = { 0.030f, 0.020f, 0.012f };
                float[] stands = { 0f, 0.015f, 0.03f, 0.045f };
                // trail-hand offset down the shaft is a scan dimension too: the trail arm is the reach limit, so
                // a coordinate-descent sweep after the best station/yaw/pitch pushes the trail hand out of reach
                // (12 mm cleared the joints but left the trail hand 5.9 mm off the shaft); the constraint set is
                // both hands on the shaft (≤ 3 mm) AND lead-to-trail joint clearance ≥ 8 mm, objective = wrist sum
                float[] gaps = { 0.008f, 0.012f, 0.016f, 0.020f };   // Olivia's smaller hands (L_prox 53 mm vs Remy's 70) stack tighter: 8/12 left the joints 2.9 mm apart
                var leadJ = HandHingeModel.LeftJoints.Skip(3).Select(b => anim.GetBoneTransform(b)).ToArray();
                var trailJ = HandHingeModel.RightJoints.Skip(3).Take(9).Select(b => anim.GetBoneTransform(b)).ToArray();
                float bestScanCost = float.MaxValue, bestPitch = 0f, bestYaw = 0f, bestSt = best.stationL, bestGap = 0f, bestStand = 0f;
                int nGrip = 0, nKnee = 0, nPosture = 0, nAll = 0;
                Vector3 fwdScan = ball != null ? Vector3.ProjectOnPlane(ball.position - golfer.transform.position, Vector3.up).normalized : Vector3.Cross(aimDir, Vector3.up);
                var configs = new List<(float st, float yw, float pd, float gp, float sd)>();
                foreach (float stS in stations) foreach (float yw in yaws) foreach (float pd in pitches) foreach (float gp in gaps) foreach (float sd in stands) configs.Add((stS, yw, pd, gp, sd));
                for (int pass = 0; pass < 2; pass++)
                {
                if (pass == 1)
                {
                    // local refinement around the coarse best (the coarse best left the trail hand 1.9 mm short of the
                    // shaft and its finger chords 1 mm inside the mesh): ±4 mm station, ±2° yaw, ±1° pitch, ±2 mm gap
                    configs.Clear();
                    foreach (float dSt in new[] { -0.004f, 0f, 0.004f }) foreach (float dYw in new[] { -2f, 0f, 2f }) foreach (float dPd in new[] { -1f, -0.5f, 0f, 0.5f, 1f }) foreach (float dGp in new[] { -0.002f, 0f, 0.002f }) foreach (float dSd in new[] { -0.015f, 0f, 0.015f })
                    { float stR = bestSt + dSt; if (stR < 0.008f) continue; configs.Add((stR, bestYaw + dYw, bestPitch + dPd, Mathf.Max(0f, bestGap + dGp), Mathf.Max(0f, bestStand + dSd))); }
                    scan.Append("--- refinement around the coarse best ---\n");
                }
                foreach (var cfg in configs)
                {
                    float stS = cfg.st, yw = cfg.yw, pd = cfg.pd, gp = cfg.gp, sd = cfg.sd;
                    Quaternion Rp = Quaternion.AngleAxis(yw, up) * Quaternion.AngleAxis(pd, pitchAxisB);
                    clubSlot.localPosition = slot0Pos; clubSlot.localRotation = slot0Rot;
                    yield return null;
                    Vector3 newPos = Eb + Rp * (clubSlot.position - Eb) - fwdScan * sd;
                    Quaternion newRot = Rp * clubSlot.rotation;
                    clubSlot.localPosition = gripTarget.InverseTransformPoint(newPos);
                    clubSlot.localRotation = Quaternion.Inverse(gripTarget.rotation) * newRot;
                    yield return null; yield return new WaitForEndOfFrame();
                    Vector3 s0p = clubStart.position, ep = clubEnd.position, dirp = (ep - s0p).normalized;
                    var ap = Anchors(gL, gR, s0p, dirp, head.position, clipRotL, clipRotR, rollMode, stS, gp, out _);
                    aL.SetPositionAndRotation(ap.posL, ap.rotL); aR.SetPositionAndRotation(ap.posR, ap.rotR);
                    if (rb2 != null) rb2.Build();
                    for (int i = 0; i < 3; i++) yield return null;
                    yield return new WaitForEndOfFrame();
                    float clr = float.MaxValue; foreach (var a in leadJ) foreach (var b in trailJ) clr = Mathf.Min(clr, Vector3.Distance(a.position, b.position));
                    float segL = FingerSegMin(anim, gL, s0p, dirp), segR = FingerSegMin(anim, gR, s0p, dirp);
                    float knee = KneeClear(anim, out float handKnee, out _, out _);
                    float armLeg = ArmLegClear(anim, out _);
                    float buttS = Vector3.Dot(handL.position - s0p, dirp);
                    bool buttOk = buttS >= 0.008f * CharScale && buttS <= 0.020f * CharScale;
                    Posture(anim, golfer.transform, fwdScan, out _, out _, out _, out float ahLs, out float ahRs, out float thighS, out float chinS);
                    float thighSurf = thighS - ThighRadiusM - HandHalfM;
                    bool hangOk = ahLs <= ArmHangMaxDeg && ahRs <= ArmHangMaxDeg, thighOk = thighSurf >= HandsThighMinM && thighSurf <= HandsThighMaxM, chinOk = chinS >= HandsChinMinM && chinS <= HandsChinMaxM;
                    bool postureOk = hangOk && thighOk && chinOk;
                    float ovl = Vector3.Dot(anim.GetBoneTransform(HumanBodyBones.RightLittleProximal).position - s0p, dirp)
                              - Vector3.Dot(0.5f * (anim.GetBoneTransform(HumanBodyBones.LeftIndexProximal).position + anim.GetBoneTransform(HumanBodyBones.LeftMiddleProximal).position) - s0p, dirp);
                    Vector3 fLp = (handL.position - foreL.position).normalized, fRp = (handR.position - foreR.position).normalized;
                    WristSplit(fLp, handL, gL, out float wLp, out float flLp, out float dvLp);
                    WristSplit(fRp, handR, gR, out float wRp, out float flRp, out float dvRp);
                    float dL = Vector3.Distance(handL.position, clipPosL), dR = Vector3.Distance(handR.position, clipPosR);
                    float rL = Vector3.Distance(handL.position, shL.position) / reachL, rR = Vector3.Distance(handR.position, shR.position) / reachR;
                    float elL = Vector3.Angle(shL.position - foreL.position, handL.position - foreL.position), elR = Vector3.Angle(shR.position - foreR.position, handR.position - foreR.position);
                    float hb = ball != null ? Vector3.Distance(new Vector3(ep.x, 0, ep.z), new Vector3(ball.position.x, 0, ball.position.z)) : -1f;
                    float onLp = AxisDist(handL.TransformPoint(-wL.localPosition), s0p, dirp), onRp = AxisDist(handR.TransformPoint(-wR.localPosition), s0p, dirp);
                    scan.Append("st").Append(Mm(stS)).Append(" yaw").Append(yw.ToString("F1")).Append(" pitch").Append(pd.ToString("F1")).Append(" gap").Append(Mm(gp)).Append(" stand").Append(Mm(sd)).Append(" | ").Append(F1(wLp)).Append(" (").Append(F1(flLp)).Append(",").Append(F1(dvLp)).Append(") | ")
                        .Append(F1(wRp)).Append(" (").Append(F1(flRp)).Append(",").Append(F1(dvRp)).Append(") | ").Append(Mm(dL)).Append(" ").Append(Mm(dR))
                        .Append(" | ").Append(rL.ToString("F2")).Append(" ").Append(rR.ToString("F2")).Append(" | ").Append(F1(elL)).Append(" ").Append(F1(elR))
                        .Append(" | ").Append(Mm(hb)).Append(" | ").Append(Mm(onLp)).Append(" ").Append(Mm(onRp)).Append(" | clr ").Append(Mm(clr)).Append(" | seg ").Append(Mm(segL)).Append(" ").Append(Mm(segR)).Append(" | ovl ").Append(Mm(ovl)).Append(" | knee ").Append(Mm(knee)).Append(" hk ").Append(Mm(handKnee)).Append(" | armleg ").Append(Mm(armLeg)).Append(" | hang ").Append(F1(ahLs)).Append("/").Append(F1(ahRs)).Append(" thigh ").Append(Mm(thighSurf)).Append(" chin ").Append(Mm(chinS)).Append(postureOk ? " ok" : " POSTURE").Append(" | butt ").Append(Mm(buttS)).Append(buttOk ? "" : " OUT").Append("\n");
                    // pick: least wrist bend among the configurations where BOTH hands land on the shaft (the post-IK
                    // truth of reach), the lead joints clear the trail index/middle/ring joints by ≥ 8 mm, and every
                    // finger bone segment stays outside the shaft mesh, and the trail little MCP sits within ±8 mm of
                    // the lead index/middle gap, and the hands stay clear above the knees for the swing (Cesar,
                    // 2026-09-15: "as you straighten the grip, move the arms higher so they don't collide with the
                    // knees when swinging") — the same rules the rows below grade
                    float segFloor = HandHingeModel.ShaftRadiusM - 0.0005f;
                    float c = wLp + wRp + (onLp > 0.003f || onRp > 0.003f ? 1000f : 0f) + (clr < 0.008f ? 1000f : 0f) + (segL < segFloor || segR < segFloor ? 1000f : 0f) + (Mathf.Abs(ovl) > 0.008f ? 1000f : 0f) + (knee < KneeClearM || handKnee < HandKneeMinM ? 1000f : 0f) + (hangOk ? 0f : 1000f) + (thighOk ? 0f : 1000f) + (chinOk ? 0f : 1000f) + (buttOk ? 0f : 1000f);
                    if (onLp <= 0.003f && onRp <= 0.003f && clr >= 0.008f && segL >= segFloor && segR >= segFloor && Mathf.Abs(ovl) <= 0.008f) { nGrip++; if (knee >= KneeClearM && handKnee >= HandKneeMinM) nKnee++; if (postureOk) nPosture++; if (knee >= KneeClearM && handKnee >= HandKneeMinM && postureOk) nAll++; }
                    if (c < bestScanCost) { bestScanCost = c; bestPitch = pd; bestYaw = yw; bestSt = stS; bestGap = gp; bestStand = sd; }
                }
                }
                L("PITCH SCAN (about the head, IK in the loop; elbow = interior angle, 180 = straight):\n" + scan);
                L("scan best (min wrist sum with both hands on the shaft, ≥ 8 mm joint clearance, fingers outside the mesh, overlap in band, hands ≥ " + Mm(KneeClearM) + " mm above the knees and ≥ " + Mm(HandKneeMinM) + " mm from them, arm hang / hands-off-thighs / hands-under-chin in band): station " + Mm(bestSt) + " mm, yaw " + bestYaw.ToString("F1") + "°, pitch " + bestPitch.ToString("F1") + "°, trail gap offset " + Mm(bestGap) + " mm, stand closer " + Mm(bestStand) + " mm" + (bestScanCost >= 1000f ? "  — NO configuration met every constraint; this violates " + Mathf.FloorToInt(bestScanCost / 1000f) + " row(s), the fewest" : "") + " | grip-feasible " + nGrip + ", of which knee-clear " + nKnee + ", posture-in-band " + nPosture + ", both " + nAll);
                // leave the best applied for the rows and frames below
                {
                    Quaternion Rp = Quaternion.AngleAxis(bestYaw, up) * Quaternion.AngleAxis(bestPitch, pitchAxisB);
                    clubSlot.localPosition = slot0Pos; clubSlot.localRotation = slot0Rot;
                    yield return null;
                    Vector3 newPos = Eb + Rp * (clubSlot.position - Eb) - fwdScan * bestStand;
                    Quaternion newRot = Rp * clubSlot.rotation;
                    clubSlot.localPosition = gripTarget.InverseTransformPoint(newPos);
                    clubSlot.localRotation = Quaternion.Inverse(gripTarget.rotation) * newRot;
                    yield return null; yield return new WaitForEndOfFrame();
                    Vector3 s0p = clubStart.position, ep = clubEnd.position, dirp = (ep - s0p).normalized;
                    best = Anchors(gL, gR, s0p, dirp, head.position, clipRotL, clipRotR, rollMode, bestSt, bestGap, out _);
                    aL.SetPositionAndRotation(best.posL, best.rotL); aR.SetPositionAndRotation(best.posR, best.rotR);
                    if (rb2 != null) rb2.Build();
                    for (int i = 0; i < 3; i++) yield return null;
                    yield return new WaitForEndOfFrame();
                    var bake2 = new StringBuilder();
                    bake2.Append("{\n  \"clubSlotLocalPos\": ").Append(J(clubSlot.localPosition)).Append(",\n  \"clubSlotLocalRot\": ").Append(J(clubSlot.localRotation))
                        .Append(",\n  \"leadAnchorLocalPos\": ").Append(J(aL.localPosition)).Append(",\n  \"leadAnchorLocalRot\": ").Append(J(aL.localRotation)).Append(",\n  \"leadWristLocalPos\": ").Append(J(wL.localPosition))
                        .Append(",\n  \"trailAnchorLocalPos\": ").Append(J(aR.localPosition)).Append(",\n  \"trailAnchorLocalRot\": ").Append(J(aR.localRotation)).Append(",\n  \"trailWristLocalPos\": ").Append(J(wR.localPosition))
                        .Append(",\n  \"leadStationM\": ").Append(F(best.stationL)).Append(", \"trailGapOffsetM\": ").Append(F(bestGap)).Append(", \"leadAxisUFrac\": ").Append(F(gL.uFrac)).Append(", \"trailAxisUFrac\": ").Append(F(gR.uFrac)).Append(", \"rollMode\": \"pitchscan\"")
                        .Append(",\n  \"scanPitchDeg\": ").Append(F(bestPitch)).Append(", \"scanYawDeg\": ").Append(F(bestYaw)).Append(", \"standCloserM\": ").Append(F(bestStand)).Append("\n}\n");
                    File.WriteAllText(BakePath, bake2.ToString()); File.WriteAllText(Path.Combine(OutDir, "stage2_bake_pitchscan.json"), bake2.ToString());
                }
            }

            // ── measurements ──
            var rows = new List<string>();
            void Row(string id, bool? pass, string detail) { rows.Add("    {\"id\": \"" + id + "\", \"verdict\": \"" + (pass == null ? "INFO" : pass.Value ? "PASS" : "FAIL") + "\", \"detail\": \"" + detail.Replace("\\", "/").Replace("\"", "'") + "\"}"); L((pass == null ? "INFO " : pass.Value ? "PASS " : "FAIL ") + id + " — " + detail); }

            S0 = clubStart.position; E = clubEnd.position; sdir = (E - S0).normalized;
            float s = CharScale;
            // wrist residuals: what the IK imposed on the clip, and how well it reached the anchor
            float resL = Quaternion.Angle(clipRotL, handL.rotation), resR = Quaternion.Angle(clipRotR, handR.rotation);
            float ikErrL = Quaternion.Angle(aL.rotation, handL.rotation), ikErrR = Quaternion.Angle(aR.rotation, handR.rotation);
            float ikPosL = Vector3.Distance(wL.position, handL.position), ikPosR = Vector3.Distance(wR.position, handR.position);
            Row("grip.wrist.residual_l", resL <= 40f, "IK rotated the lead wrist " + F1(resL) + "° from the clip (stop line 40); IK reached the anchor to " + F1(ikErrL) + "° / " + Mm(ikPosL) + " mm");
            Row("grip.wrist.residual_r", resR <= 40f, "IK rotated the trail wrist " + F1(resR) + "° from the clip (stop line 40); IK reached the anchor to " + F1(ikErrR) + "° / " + Mm(ikPosR) + " mm");
            {
                Vector3 fL = (handL.position - foreL.position).normalized, fR = (handR.position - foreR.position).normalized;
                WristSplit(fL, handL, gL, out float angL, out float flL, out float dvL);
                WristSplit(fR, handR, gR, out float angR, out float flR, out float dvR);
                Row("grip.wrist.angle_l", null, "lead forearm→hand " + F1(angL) + "° (flex " + F1(flL) + ", dev " + F1(dvL) + ") vs clip " + F1(clipWL) + "° (flex " + F1(clipFL) + ", dev " + F1(clipDL) + ")");
                Row("grip.wrist.angle_r", null, "trail forearm→hand " + F1(angR) + "° (flex " + F1(flR) + ", dev " + F1(dvR) + ") vs clip " + F1(clipWR) + "° (flex " + F1(clipFR) + ", dev " + F1(clipDR) + ")");
            }
            { float kc = KneeClear(anim, out float kd, out float hy, out float ky); Row("grip.hands.aboveKnees", kc >= KneeClearM && kd >= HandKneeMinM, "lowest hand joint " + Mm(kc) + " mm above the higher knee (≥ " + Mm(KneeClearM) + "; hand " + Mm(hy) + " / knee " + Mm(ky) + " mm over the root), nearest hand joint to a knee joint " + Mm(kd) + " mm (≥ " + Mm(HandKneeMinM) + ")"); }
            {
                float rootY = golfer.transform.position.y, headTopY = anim.GetBoneTransform(HumanBodyBones.Head).position.y + 0.11f * s;   // crown ≈ head joint + 0.11 m·s
                float bodyH = headTopY - rootY, handsY = 0.5f * (handL.position.y + handR.position.y) - rootY;
                Row("stance.handsHeight", null, "hand origins " + Mm(handsY) + " mm over the ground (real golfers ≈ 0.75–0.90 m with a driver, hands hanging under the shoulders)");
                Vector3 fwdP = ball != null ? Vector3.ProjectOnPlane(ball.position - golfer.transform.position, Vector3.up).normalized : Vector3.Cross(aimDir, Vector3.up);
                Posture(anim, golfer.transform, fwdP, out float tilt, out float kL, out float kR, out float ahL, out float ahR, out float thighMm, out float chinMm);
                var otNow = golfer.GetComponentsInChildren<OverrideTransform>(true).FirstOrDefault(o => o.gameObject.name == "Stance_SpineBend");
                var ohNow = golfer.GetComponentsInChildren<OverrideTransform>(true).FirstOrDefault(o => o.gameObject.name == "Stance_Hips");
                Row("stance.hipsLift", null, ohNow == null ? "no Stance_Hips on this prefab" : "Hips OverrideTransform (Pivot) position offset " + ohNow.data.position.ToString("F4") + " = " + Mm(ohNow.data.position.magnitude) + " mm, feet pinned to the clip's by leg IK");
                Row("stance.spineBend", null, otNow == null ? "no Rig_Stance on this prefab" : "Spine OverrideTransform (Pivot) rotation " + otNow.data.rotation.ToString("F3") + " = " + F1(Quaternion.Angle(Quaternion.identity, Quaternion.Euler(otNow.data.rotation))) + "° off the clip");
                Row("stance.torsoTilt", tilt >= TorsoTiltMinDeg && tilt <= TorsoTiltMaxDeg, "hips→neck " + F1(tilt) + "° from vertical (guideline " + F1(TorsoTiltMinDeg) + "–" + F1(TorsoTiltMaxDeg) + ")");
                Row("stance.kneeFlex", kL >= KneeFlexMinDeg && kL <= KneeFlexMaxDeg && kR >= KneeFlexMinDeg && kR <= KneeFlexMaxDeg, "L " + F1(kL) + "° / R " + F1(kR) + "° (guideline " + F1(KneeFlexMinDeg) + "–" + F1(KneeFlexMaxDeg) + "; the clip's, untouched)");
                Row("stance.armHang", ahL <= ArmHangMaxDeg && ahR <= ArmHangMaxDeg, "shoulder→hand L " + F1(ahL) + "° / R " + F1(ahR) + "° from vertical (guideline: hanging, ≤ " + F1(ArmHangMaxDeg) + ")");
                Row("stance.handsFromThighs", thighMm - ThighRadiusM - HandHalfM >= HandsThighMinM && thighMm - ThighRadiusM - HandHalfM <= HandsThighMaxM, "hands↔thigh " + Mm(thighMm) + " mm centreline ≈ " + Mm(thighMm - ThighRadiusM - HandHalfM) + " mm surface (guideline 6–8 in = " + Mm(HandsThighMinM) + "–" + Mm(HandsThighMaxM) + " with a driver)");
                Row("stance.handsUnderChin", chinMm >= HandsChinMinM && chinMm <= HandsChinMaxM, "hands " + Mm(chinMm) + " mm toward the ball from the head (guideline: under to just in front of the chin, " + Mm(HandsChinMinM) + " … " + Mm(HandsChinMaxM) + ")");
                float elev = Mathf.Asin(Mathf.Clamp(Vector3.Dot((S0 - clubEnd.position).normalized, Vector3.up), -1f, 1f)) * Mathf.Rad2Deg;
                Row("club.shaftElevation", null, "shaft " + F1(elev) + "° above horizontal, head→butt (driver lie at address ≈ 55–60°)");
            }
            { float al = ArmLegClear(anim, out string alp); Row("grip.arms.clearLegs", null, "nearest arm bone (shoulder→elbow→hand) to a leg bone (hip→knee→ankle): " + Mm(al) + " mm centreline (" + alp + "; information — Cesar withdrew the elbow concern 2026-09-15)"); }
            Row("grip.wrist.displacement", null, "hand origin moved L " + Mm(Vector3.Distance(clipPosL, handL.position)) + " mm, R " + Mm(Vector3.Distance(clipPosR, handR.position)) + " mm from the clip");

            // hands on the shaft: the hand-local foot must sit on the world axis
            {
                // the physical side of the grip: lead palm away from the target, trail palm toward it (Remy −0.9997 / +0.9993)
                float psL = Vector3.Dot(handL.TransformDirection(gL.n), -aimDir), psR = Vector3.Dot(handR.TransformDirection(gR.n), aimDir);
                Row("grip.palmSide_l", psL > 0.5f, "lead palm · (−aim) = " + F(psL) + " (> 0.5: the palm faces away from the target, the hand sits target-side of the shaft)");
                Row("grip.palmSide_r", psR > 0.5f, "trail palm · (+aim) = " + F(psR) + " (> 0.5: the palm faces the target, over the lead thumb)");
                Vector3 upLocal = clubSlot.InverseTransformDirection(Vector3.up);
                float crown = Vector3.Dot(upLocal, ClubCrownUpSlotLocal);
                Row("club.crownUp", crown > 0.7f, "world up in ClubSlot-local = " + V(upLocal) + " · Remy's accepted (-0.0905, -0.7323, -0.6749) = " + F(crown) + " (> 0.7: the club is soled crown-up, not rolled 180° about the shaft — the face-square row cannot tell the two apart)");
            }
            float onL = AxisDist(handL.TransformPoint(-wL.localPosition), S0, sdir), onR = AxisDist(handR.TransformPoint(-wR.localPosition), S0, sdir);
            Row("grip.hand.onShaft_l", onL < 0.003f, "lead tunnel point " + Mm(onL) + " mm off the shaft axis (< 3)");
            Row("grip.hand.onShaft_r", onR < 0.003f, "trail tunnel point " + Mm(onR) + " mm off the shaft axis (< 3)");

            // overlap: trail little MCP station vs the lead Index/Middle MCP gap
            Transform rLit = anim.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            Transform lIdx = anim.GetBoneTransform(HumanBodyBones.LeftIndexProximal), lMid = anim.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            float stGap = Vector3.Dot(0.5f * (lIdx.position + lMid.position) - S0, sdir), stLit = Vector3.Dot(rLit.position - S0, sdir);
            Row("grip.hands.overlap", Mathf.Abs(stLit - stGap) <= 0.008f, "trail little MCP station " + Mm(stLit) + " mm vs lead index/middle gap " + Mm(stGap) + " mm (Δ " + Mm(stLit - stGap) + ", ±8)");

            // heel pad on top: lead back of hand faces the head
            Vector3 nL = handL.TransformDirection(gL.n), nR = handR.TransformDirection(gR.n);
            float heelDot = Vector3.Dot(-nL, (head.position - aL.position).normalized);
            Row("grip.heelPad.onTop", heelDot > 0.5f, "lead dot(−n, toHead) = " + F(heelDot) + " (> 0.5)");

            // trail palm on the lead thumb — §3.10.6 geometric
            Transform lThumb2 = anim.GetBoneTransform(HumanBodyBones.LeftThumbIntermediate);
            Vector3 trailPalmO = 0.25f * (anim.GetBoneTransform(HumanBodyBones.RightIndexProximal).position + anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal).position + anim.GetBoneTransform(HumanBodyBones.RightRingProximal).position + rLit.position);
            float thumbAlong = Vector3.Dot(lThumb2.position - trailPalmO, nR);
            float axisAlong = Vector3.Dot(ClosestOnAxis(lThumb2.position, S0, sdir) - trailPalmO, nR);
            float palmDot = Vector3.Dot(nR, (lThumb2.position - aR.position).normalized);
            Row("grip.trailPalm.onThumb", thumbAlong > 0f && thumbAlong < 0.025f * s && axisAlong > thumbAlong, "lead thumb " + Mm(thumbAlong) + " mm palm-side of the trail MCP plane (0 … " + Mm(0.025f * s) + "), shaft " + Mm(axisAlong) + " mm (must be further); dot(n, toThumb) = " + F(palmDot));

            // no interpenetration: lead finger joints vs trail finger joints, trail little excluded
            {
                var leadJ = HandHingeModel.LeftJoints.Skip(3).Select(b => anim.GetBoneTransform(b).position).ToArray();
                var trailJ = HandHingeModel.RightJoints.Skip(3).Take(9).Select(b => anim.GetBoneTransform(b).position).ToArray();   // index/middle/ring only
                float min = float.MaxValue; foreach (var a in leadJ) foreach (var b in trailJ) min = Mathf.Min(min, Vector3.Distance(a, b));
                Row("grip.hands.noInterpenetration", min >= 0.008f, "closest lead joint to a trail index/middle/ring joint: " + Mm(min) + " mm (≥ 8)");
                float minAll = float.MaxValue; var lit = HandHingeModel.RightJoints.Skip(12).Select(b => anim.GetBoneTransform(b).position).ToArray();
                foreach (var a in leadJ) foreach (var b in lit) minAll = Mathf.Min(minAll, Vector3.Distance(a, b));
                Row("grip.hands.trailLittleOnLead", null, "closest lead joint to the trail little finger: " + Mm(minAll) + " mm (the intended contact)");
            }

            // butt cap: 8–20 mm·s beyond the heel edge along the shaft
            Transform lLit = anim.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
            float pastLittle = Vector3.Dot(lLit.position - S0, sdir), pastHand = Vector3.Dot(handL.position - S0, sdir);
            Row("grip.buttCap.pastHeel", pastHand >= 0.008f * s && pastHand <= 0.020f * s, "LeftHand origin " + Mm(pastHand) + " mm down-shaft of ClubStart (" + Mm(0.008f * s) + " … " + Mm(0.020f * s) + "); lead little MCP at " + Mm(pastLittle) + " mm");

            // fingers on the shaft (world): joints vs the axis, per finger
            foreach (var (nm, right) in new[] { ("l", false), ("r", true) })
            {
                var g = right ? gR : gL; Transform h = right ? handR : handL;
                var sbF = new StringBuilder(); float worstOut = 0f, worstIn = float.MaxValue;
                for (int f = 1; f <= 4; f++)
                {
                    var t0 = anim.GetBoneTransform(g.data.joints[HandHingeModel.JointIndex(f, 0)].bone); var t1 = anim.GetBoneTransform(g.data.joints[HandHingeModel.JointIndex(f, 1)].bone); var t2 = anim.GetBoneTransform(g.data.joints[HandHingeModel.JointIndex(f, 2)].bone);
                    float dPip = AxisDist(t1.position, S0, sdir), dDip = AxisDist(t2.position, S0, sdir), dTip = AxisDist(t2.GetChild(0).position, S0, sdir);
                    float segMin = Mathf.Min(HandHingeModel.SegmentToLineDistance(t0.position, t1.position, S0, sdir, out _), Mathf.Min(HandHingeModel.SegmentToLineDistance(t1.position, t2.position, S0, sdir, out _), HandHingeModel.SegmentToLineDistance(t2.position, t2.GetChild(0).position, S0, sdir, out _)));
                    sbF.Append(f == 1 ? "index" : f == 2 ? "middle" : f == 3 ? "ring" : "little").Append(" pip/dip/tip ").Append(Mm(dPip)).Append('/').Append(Mm(dDip)).Append('/').Append(Mm(dTip)).Append(" segMin ").Append(Mm(segMin)).Append("; ");
                    if (!(right && f == 4)) { worstOut = Mathf.Max(worstOut, Mathf.Abs(dTip - HandHingeModel.ContactM)); }
                    worstIn = Mathf.Min(worstIn, segMin);
                }
                Row("grip.fingers.onShaft_" + nm, worstIn >= HandHingeModel.ShaftRadiusM - 0.0005f, "bone segments ≥ mesh radius " + Mm(HandHingeModel.ShaftRadiusM) + ": min " + Mm(worstIn) + " mm | " + sbF);
            }

            // thumb along the shaft
            {
                Transform t1 = anim.GetBoneTransform(HumanBodyBones.LeftThumbProximal), t2 = anim.GetBoneTransform(HumanBodyBones.LeftThumbIntermediate), t3 = anim.GetBoneTransform(HumanBodyBones.LeftThumbDistal);
                float ang = Vector3.Angle(t2.position - t1.position, sdir); float d3 = AxisDist(t3.position, S0, sdir), dt = AxisDist(t3.GetChild(0).position, S0, sdir);
                Row("grip.thumb.downShaft_l", null, "lead thumb proximal " + F1(ang) + "° off the shaft, Thumb3 " + Mm(d3) + " mm / tip " + Mm(dt) + " mm from the axis");
            }

            // head at ball, face square (recorder rows, by reflection)
            if (ball != null)
            {
                float gap = Vector3.Distance(new Vector3(E.x, 0, E.z), new Vector3(ball.position.x, 0, ball.position.z));
                Row("club.headAtBall", gap < 0.05f, "ClubEnd " + Mm(gap) + " mm from the ball in plan (< 50)");
            }
            try { runner.GetType().GetMethod("MeasureFaceSquare", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(runner, new object[] { "club.faceSquare", clubSlot, shot, "stage 2 address" }); L("club.faceSquare measured into the recorder JSON"); }
            catch (Exception e) { L("faceSquare reflection failed: " + e.Message); }

            // where the club head actually sits at address, golfer-local (what GolferPresenter.addressHeadLocal
            // should be so PlaceAtBall lands it on the ball with THIS club) — the club follows the clip's hand
            // average (layer 1), so this is stable across anchor changes
            Vector3 headLocalNow = golfer.transform.InverseTransformPoint(clubEnd.position);
            Row("club.addressHeadLocal", null, "ClubEnd in golfer-local = " + V(headLocalNow) + " (presenter field addressHeadLocal; PlaceAtBall scales it by the root scale)");
            if (!verify)
            {
                try
                {
                    string bk = File.ReadAllText(BakePath);
                    bk = bk.TrimEnd().TrimEnd('}').TrimEnd() + ",\n  \"addressHeadLocal\": " + J(headLocalNow) + "\n}\n";
                    File.WriteAllText(BakePath, bk);
                    File.WriteAllText(Path.Combine(OutDir, "stage2_bake_" + rollMode + ".json"), bk);
                }
                catch (Exception e) { L("could not append addressHeadLocal to the bake: " + e.Message); }
            }

            // ── frames ──
            Vector3 handsMid = 0.5f * (handL.position + handR.position);
            Vector3 side = Vector3.Cross(sdir, Vector3.up); if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
            // down the shaft from the butt end, from IN FRONT of the golfer (toward the ball) so the
            // forearms and torso do not fill the frame — the coach's view of the knuckles
            Vector3 fwd = ball != null ? Vector3.ProjectOnPlane(ball.position - golfer.transform.position, Vector3.up).normalized : Vector3.Cross(aimDir, Vector3.up);
            Shoot(handsMid, handsMid - sdir * 0.32f + fwd * 0.45f + Vector3.up * 0.05f, Vector3.up, 1600, Path.Combine(OutDir, tag + "_downshaft.png"), log);
            Shoot(handsMid, handsMid + aimDir * 0.85f, Vector3.up, 1600, Path.Combine(OutDir, tag + "_targetside.png"), log);
            Shoot(handsMid, head.position + (handsMid - head.position).normalized * 0.10f, Vector3.up, 1600, Path.Combine(OutDir, tag + "_golferseye.png"), log);
            Shoot(handsMid, handsMid - aimDir * 0.85f, Vector3.up, 1600, Path.Combine(OutDir, tag + "_awayside.png"), log);
            // full body (30° FOV → 4 m frames ~2.1 m): hands vs knees, arm hang, shaft elevation
            Vector3 mid = golfer.transform.position + Vector3.up * 0.90f * s;
            Shoot(mid, mid + fwd * 4.0f + Vector3.up * 0.2f, Vector3.up, 1600, Path.Combine(OutDir, tag + "_stance_faceon.png"), log);
            Shoot(mid, mid + aimDir * 4.0f + Vector3.up * 0.2f, Vector3.up, 1600, Path.Combine(OutDir, tag + "_stance_targetside.png"), log);
            try { string p = CaptureCore.SnapPlayModeSafe("hinge_stage2_" + tag + "_gameplay"); L("gameplay frame: " + p); } catch (Exception e) { L("gameplay frame failed: " + e.Message); }

            var json = new StringBuilder();
            json.Append("{\n  \"mode\": \"").Append(tag).Append("\",\n  \"charScale\": ").Append(F(s)).Append(",\n  \"wristResidualDeg\": {\"lead\": ").Append(F(resL)).Append(", \"trail\": ").Append(F(resR)).Append("},\n  \"rows\": [\n").Append(string.Join(",\n", rows)).Append("\n  ]\n}\n");
            File.WriteAllText(Path.Combine(OutDir, "stage2_" + tag + "_numbers.json"), json.ToString());
            File.WriteAllText(Path.Combine(OutDir, "stage2_" + tag + "_console.txt"), log.ToString());
            L("wrote " + OutDir);
        }

        static bool Wrappable(Animator anim, HandHingeHand hand, HandGeom g, bool lead, Action<string> log)
        {
            var start = lead ? HandHingeModel.LeadGripStart() : HandHingeModel.TrailGripStart();
            var fingers = lead
                ? new[] { (HandHingeModel.Index, start.index.spread), (HandHingeModel.Middle, start.middle.spread), (HandHingeModel.Ring, start.ring.spread), (HandHingeModel.Little, start.little.spread) }
                : new[] { (HandHingeModel.Index, start.index.spread), (HandHingeModel.Middle, start.middle.spread), (HandHingeModel.Ring, start.ring.spread) };
            bool ok = true; var sb = new StringBuilder();
            foreach (var (finger, spread) in fingers)
            {
                var r = HandHingeModel.SolveFingerInscribed(anim, hand, finger, spread, g.o, g.d);
                bool fOk = Mathf.Abs(r.metrics.tip - HandHingeModel.ContactM) <= 0.005f && r.metrics.minAll >= HandHingeModel.ShaftRadiusM - 0.0005f;
                ok &= fOk;
                sb.Append(r.metrics.name).Append(fOk ? " ok" : " NO").Append("(tip ").Append(Mm(r.metrics.tip)).Append(") ");
            }
            log((lead ? "lead" : "trail") + " axis " + g.uFrac.ToString("F1") + "·L_prox wrappable=" + ok + ": " + sb);
            return ok;
        }

        static HandGeom Geom(Animator anim, HandHingeHand data, float uFrac)
        {
            var g = new HandGeom { right = data.right, data = data, n = data.palmNormalHandLocal, u = data.lengthAxisHandLocal, uFrac = uFrac };
            HandHingeModel.GripAxisHandLocal(anim, data, uFrac, out g.o, out g.d);
            Transform h = anim.GetBoneTransform(HandHingeModel.HandBone(data.right));
            g.fHand = g.o + Vector3.Dot(-g.o, g.d) * g.d;
            Vector3 lit = h.InverseTransformPoint(anim.GetBoneTransform(data.joints[HandHingeModel.JointIndex(HandHingeModel.Little, 0)].bone).position);
            g.fLittle = g.o + Vector3.Dot(lit - g.o, g.d) * g.d;
            Vector3 idx = h.InverseTransformPoint(anim.GetBoneTransform(data.joints[HandHingeModel.JointIndex(HandHingeModel.Index, 0)].bone).position);
            Vector3 mid = h.InverseTransformPoint(anim.GetBoneTransform(data.joints[HandHingeModel.JointIndex(HandHingeModel.Middle, 0)].bone).position);
            g.gapLocal = 0.5f * (idx + mid);
            g.thumbInterLocal = h.InverseTransformPoint(anim.GetBoneTransform(data.joints[HandHingeModel.JointIndex(HandHingeModel.Thumb, 1)].bone).position);
            return g;
        }

        /// <summary>§3.12.4 closed form: both anchors for a given world shaft.</summary>
        static AnchorSolve Anchors(HandGeom gL, HandGeom gR, Vector3 s0, Vector3 dir, Vector3 headW, Quaternion clipRotL, Quaternion clipRotR, string rollMode, float leadStation, float trailGapOffset, out float gapUsed)
        {
            gapUsed = trailGapOffset;
            return Anchors(gL, gR, s0, dir, headW, clipRotL, clipRotR, rollMode, leadStation, trailGapOffset);
        }

        static AnchorSolve Anchors(HandGeom gL, HandGeom gR, Vector3 s0, Vector3 dir, Vector3 headW, Quaternion clipRotL, Quaternion clipRotR, string rollMode, float leadStation, float trailGapOffset)
        {
            bool minWrist = rollMode == "minwrist";
            var a = new AnchorSolve();
            // lead: station, then roll so the back of the hand faces the head
            a.stationL = leadStation;
            a.posL = s0 + dir * a.stationL;
            Quaternion r0 = Quaternion.FromToRotation(gL.d, dir);
            a.rotL = RollFor(r0, dir, gL, a.posL, headW, true, rollMode, clipRotL, out a.rollL, out a.ruleL);
            a.wristL = WristAngle(gL.forearmClip, a.rotL, gL.u);
            Vector3 handPosL = a.posL - a.rotL * gL.fHand;
            a.leadThumbW = handPosL + a.rotL * gL.thumbInterLocal;
            Vector3 gapW = handPosL + a.rotL * gL.gapLocal;
            a.stationGap = Vector3.Dot(gapW - s0, dir);
            // trail: little MCP at the gap station, roll so the palm faces the lead thumb
            a.stationR = a.stationGap + trailGapOffset + Vector3.Dot(gR.fHand - gR.fLittle, gR.d);
            a.posR = s0 + dir * a.stationR;
            Quaternion r1 = Quaternion.FromToRotation(gR.d, dir);
            a.rotR = RollFor(r1, dir, gR, a.posR, a.leadThumbW, false, rollMode, clipRotR, out a.rollR, out a.ruleR);
            a.wristR = WristAngle(gR.forearmClip, a.rotR, gR.u);
            return a;
        }

        /// <summary>
        /// Roll about the shaft. Palm mode (§3.12.4): maximise the rule dot (lead: −n toward the
        /// head; trail: +n toward the lead thumb). Min-wrist mode (§3.12.5's objective on this DOF):
        /// minimise the rotation from the clip's hand; the rule dot is then REPORTED at that roll.
        /// 0.5° scan, deterministic.
        /// </summary>
        /// <summary>
        /// Palm-side roll (2026-09-15, Olivia): "closest to the clip" is only a roll rule when the clip's hands are a
        /// grip. Olivia's auto-rig plays them twisted, and the closest roll swung both hands to the wrong side of the
        /// shaft — Cesar: "her hands are backwards". This rule uses the physical grip instead: roll about the shaft so
        /// the LEAD palm faces away from the target (−aim; Remy's accepted bake reads −0.9997) and the TRAIL palm
        /// faces the target (+aim; Remy +0.9993). Opt-in so Remy's committed bake is untouched.
        /// </summary>
        public static bool PalmSideRoll { get => EditorPrefs.GetBool("Golfin.GolferTest.PalmSideRoll", false); set => EditorPrefs.SetBool("Golfin.GolferTest.PalmSideRoll", value); }
        static Vector3 RollAim = Vector3.right;   // set by Run from the shot heading
        /// <summary>World up expressed in ClubSlot-local on Remy's accepted, face-square, crown-up bake (verify 2026-09-15). A constant of the club model + ClubSlot.</summary>
        static readonly Vector3 ClubCrownUpSlotLocal = new Vector3(-0.0905f, -0.7323f, -0.6749f);

        static Quaternion RollFor(Quaternion r0, Vector3 dir, HandGeom g, Vector3 anchorPos, Vector3 target, bool backOfHand, string rollMode, Quaternion clipRot, out float bestRoll, out float ruleDot)
        {
            Vector3 to = (target - anchorPos).normalized;
            bool palmSide = PalmSideRoll && (rollMode == "minwrist" || rollMode == "wristangle" || rollMode == "pitchscan");
            Vector3 palmWant = backOfHand ? -RollAim : RollAim;   // lead palm away from the target, trail palm toward it
            bestRoll = 0f; ruleDot = float.MinValue; Quaternion best = r0;
            float bestScore = float.MinValue;
            for (int i = 0; i < 360; i++)
            {
                float th = -180f + i * 1f;
                Quaternion r = Quaternion.AngleAxis(th, dir) * r0;
                Vector3 n = r * g.n;
                float dot = Vector3.Dot(backOfHand ? -n : n, to);
                float score = palmSide ? Vector3.Dot(n, palmWant)
                            : (rollMode == "minwrist" || rollMode == "wristangle" || rollMode == "pitchscan") ? -Quaternion.Angle(clipRot, r)
                            : rollMode == "anatomy"  ? -WristAngle(g.forearmClip, r, g.u)
                            : dot;
                if (score > bestScore) { bestScore = score; bestRoll = th; best = r; ruleDot = dot; }
            }
            return best;
        }

        // ── bake write-back (edit mode, define ON) ────────────────────────────────────
        [MenuItem("GOLFIN/Golfer Test/Hinge/Stage 2 — apply bake to prefab")]
        public static void ApplyBakeMenu() => Debug.Log(ApplyBakeToPrefab());

        public static string ApplyBakeToPrefab()
        {
            if (!EditorUserBuildSettings.activeScriptCompilationDefines.Contains("GOLFIN_GOLFER_TEST"))
                throw new InvalidOperationException("define OFF — refusing to save a gated prefab");
            if (!File.Exists(BakePath)) throw new FileNotFoundException(BakePath);
            string bakeText = File.ReadAllText(BakePath);
            var kv = ParseFlat(bakeText);
            string axesLog = "";
            var mL = System.Text.RegularExpressions.Regex.Match(bakeText, "\"leadAxisUFrac\": ([-0-9.]+)");
            var mR = System.Text.RegularExpressions.Regex.Match(bakeText, "\"trailAxisUFrac\": ([-0-9.]+)");
            if (mL.Success && mR.Success)
                axesLog = AuthorPrefabStructureForAxes(float.Parse(mL.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(mR.Groups[1].Value, CultureInfo.InvariantCulture)) + "\n";
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform Tf(string n) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
                var clubSlot = Tf("ClubSlot"); var aL = Tf("GripAnchor_Lead"); var aR = Tf("GripAnchor_Trail");
                clubSlot.localPosition = kv["clubSlotLocalPos"].v; clubSlot.localRotation = kv["clubSlotLocalRot"].q;
                aL.localPosition = kv["leadAnchorLocalPos"].v; aL.localRotation = kv["leadAnchorLocalRot"].q; aL.Find("WristTarget").localPosition = kv["leadWristLocalPos"].v;
                aR.localPosition = kv["trailAnchorLocalPos"].v; aR.localRotation = kv["trailAnchorLocalRot"].q; aR.Find("WristTarget").localPosition = kv["trailWristLocalPos"].v;
                string headNote = "";
                if (kv.ContainsKey("addressHeadLocal"))
                {
                    var pres = root.GetComponent<GolferPresenter>();
                    var so = new SerializedObject(pres); var prop = so.FindProperty("addressHeadLocal");
                    if (prop != null) { prop.vector3Value = kv["addressHeadLocal"].v; so.ApplyModifiedPropertiesWithoutUndo(); headNote = "; addressHeadLocal " + V(kv["addressHeadLocal"].v); }
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return axesLog + "baked into " + PrefabPath + ": ClubSlot " + V(clubSlot.localPosition) + " " + Q(clubSlot.localRotation) + "; lead " + V(aL.localPosition) + " " + Q(aL.localRotation) + "; trail " + V(aR.localPosition) + " " + Q(aR.localRotation) + headNote;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>
        /// Face-square roll (§3.11.3 kept by §3.12.7): roll ClubSlot about its own +Y (the shaft) by
        /// <paramref name="rollFixDeg"/> — the recorder's SOLVED value — and counter-rotate both anchors
        /// about the same axis so the hands' world frames do not move. The anchors sit on the axis
        /// (x = z = 0), so only their rotation changes.
        /// </summary>
        public static string ApplyFaceRollFix(float rollFixDeg)
        {
            if (!EditorUserBuildSettings.activeScriptCompilationDefines.Contains("GOLFIN_GOLFER_TEST"))
                throw new InvalidOperationException("define OFF — refusing to save a gated prefab");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform Tf(string n) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
                var clubSlot = Tf("ClubSlot"); var aL = Tf("GripAnchor_Lead"); var aR = Tf("GripAnchor_Trail");
                Quaternion roll = Quaternion.AngleAxis(rollFixDeg, Vector3.up), counter = Quaternion.AngleAxis(-rollFixDeg, Vector3.up);
                clubSlot.localRotation = clubSlot.localRotation * roll;
                aL.localRotation = counter * aL.localRotation; aL.localPosition = counter * aL.localPosition;
                aR.localRotation = counter * aR.localRotation; aR.localPosition = counter * aR.localPosition;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return "face roll fix " + rollFixDeg.ToString("F3") + "° applied: ClubSlot rot " + Q(clubSlot.localRotation) + "; lead " + Q(aL.localRotation) + " " + V(aL.localPosition) + "; trail " + Q(aR.localRotation) + " " + V(aR.localPosition);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        struct VQ { public Vector3 v; public Quaternion q; }
        static Dictionary<string, VQ> ParseFlat(string json)
        {
            var d = new Dictionary<string, VQ>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(json, "\"(\\w+)\": \\[([^\\]]+)\\]"))
            {
                var parts = m.Groups[2].Value.Split(',').Select(x => float.Parse(x.Trim(), CultureInfo.InvariantCulture)).ToArray();
                d[m.Groups[1].Value] = parts.Length == 4 ? new VQ { q = new Quaternion(parts[0], parts[1], parts[2], parts[3]) } : new VQ { v = new Vector3(parts[0], parts[1], parts[2]) };
            }
            return d;
        }

        // ── helpers ────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Hands-above-knees floor for the swing (Cesar, 2026-09-15). Measured as the lowest hand/finger joint over
        /// the higher knee joint. From the posture guidelines for a 1.75 m golfer: hips ≈ 0.93 m, torso 0.5 m bent
        /// 30–35° → shoulders ≈ 1.34 m; arms 0.6 m hanging 15–20° forward → wrists ≈ 0.77 m; the trail fingertips
        /// ≈ 0.14 m further down the shaft line → ≈ 0.63 m; knee joint ≈ 0.50 m → ≈ 100–130 mm. 100 mm is the floor.
        /// (An earlier 140 mm came from a wrist height taken standing, not at address.) The horizontal separation
        /// the same guidelines give — hands 150–200 mm off the thighs — is graded by stance.handsFromThighs, and
        /// HandKneeMinM is the 3D centreline distance from any hand joint to a knee joint that keeps the fingertips
        /// (≈ 10 mm) off the kneecap (≈ 60 mm) with 90 mm to spare.
        /// </summary>
        const float KneeClearM = 0.10f;
        const float HandKneeMinM = 0.16f;

        /// <summary>
        /// Arm-bone to leg-bone centreline floor for the swing: an upper arm is ≈ 45 mm in radius and a thigh ≈ 80 mm,
        /// so 125 mm is skin contact; 160 mm leaves ≈ 35 mm of surface gap at address for the elbows to pass the thighs.
        /// </summary>
        const float ArmLegClearM = 0.16f;

        /// <summary>Smallest centreline distance between the arm bones (upper arm, forearm, both sides) and the leg bones (thigh, shin, both sides); names the closest pair.</summary>
        static float ArmLegClear(Animator anim, out string pair)
        {
            Vector3 P(HumanBodyBones b) => anim.GetBoneTransform(b).position;
            var arms = new[] {
                ("L upper arm", P(HumanBodyBones.LeftUpperArm), P(HumanBodyBones.LeftLowerArm)), ("L forearm", P(HumanBodyBones.LeftLowerArm), P(HumanBodyBones.LeftHand)),
                ("R upper arm", P(HumanBodyBones.RightUpperArm), P(HumanBodyBones.RightLowerArm)), ("R forearm", P(HumanBodyBones.RightLowerArm), P(HumanBodyBones.RightHand)) };
            var legs = new[] {
                ("L thigh", P(HumanBodyBones.LeftUpperLeg), P(HumanBodyBones.LeftLowerLeg)), ("L shin", P(HumanBodyBones.LeftLowerLeg), P(HumanBodyBones.LeftFoot)),
                ("R thigh", P(HumanBodyBones.RightUpperLeg), P(HumanBodyBones.RightLowerLeg)), ("R shin", P(HumanBodyBones.RightLowerLeg), P(HumanBodyBones.RightFoot)) };
            float best = float.MaxValue; pair = "";
            foreach (var a in arms) foreach (var l in legs)
            {
                float d = HandHingeModel.SegmentDistance(a.Item2, a.Item3, l.Item2, l.Item3);
                if (d < best) { best = d; pair = a.Item1 + " ↔ " + l.Item1; }
            }
            return best;
        }

        // ── address posture guidelines (reference/WRIST_ANGLES_AT_ADDRESS.md § posture, 2026-09-15) ──
        const float TorsoTiltMinDeg = 25f, TorsoTiltMaxDeg = 45f;   // forward bend from vertical: "25°" average … "35–45°"
        const float KneeFlexMinDeg = 15f, KneeFlexMaxDeg = 25f;     // "most golfers 15–25°"
        const float ArmHangMaxDeg = 20f;                            // arms "hang vertically down from the shoulders"
        const float HandsThighMinM = 0.15f, HandsThighMaxM = 0.20f; // "6–8 in from the thighs with a driver" (surface)
        const float HandsChinMinM = -0.05f, HandsChinMaxM = 0.15f;  // "directly under the chin, or just in front"
        const float ThighRadiusM = 0.08f, HandHalfM = 0.02f;        // mesh allowances to turn centreline into surface

        /// <summary>The address posture numbers the guidelines talk about, from the humanoid bones.</summary>
        static string Posture(Animator anim, Transform root, Vector3 fwd, out float torsoTilt, out float kneeL, out float kneeR, out float armHangL, out float armHangR, out float handsThigh, out float handsChin)
        {
            Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips), neck = anim.GetBoneTransform(HumanBodyBones.Neck) ?? anim.GetBoneTransform(HumanBodyBones.Head);
            torsoTilt = Vector3.Angle(neck.position - hips.position, Vector3.up);
            float Knee(HumanBodyBones h, HumanBodyBones k, HumanBodyBones a) { var K = anim.GetBoneTransform(k).position; return 180f - Vector3.Angle(anim.GetBoneTransform(h).position - K, anim.GetBoneTransform(a).position - K); }
            kneeL = Knee(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
            kneeR = Knee(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
            Vector3 hL = anim.GetBoneTransform(HumanBodyBones.LeftHand).position, hR = anim.GetBoneTransform(HumanBodyBones.RightHand).position;
            armHangL = Vector3.Angle(hL - anim.GetBoneTransform(HumanBodyBones.LeftUpperArm).position, Vector3.down);
            armHangR = Vector3.Angle(hR - anim.GetBoneTransform(HumanBodyBones.RightUpperArm).position, Vector3.down);
            handsThigh = Mathf.Min(
                HandHingeModel.SegmentDistance(hL, hR, anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position, anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position),
                HandHingeModel.SegmentDistance(hL, hR, anim.GetBoneTransform(HumanBodyBones.RightUpperLeg).position, anim.GetBoneTransform(HumanBodyBones.RightLowerLeg).position));
            handsChin = Vector3.Dot(0.5f * (hL + hR) - anim.GetBoneTransform(HumanBodyBones.Head).position, fwd);
            return "";
        }

        /// <summary>Writes stage2_stance.json (the stance sweep's pick) into the prefab's Stance_SpineBend override rotation.</summary>
        public static string ApplyStanceToPrefab()
        {
            if (!EditorUserBuildSettings.activeScriptCompilationDefines.Contains("GOLFIN_GOLFER_TEST"))
                throw new InvalidOperationException("define OFF — refusing to save a gated prefab");
            string path = Path.Combine(OutDir, "stage2_stance.json");
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            var kv = ParseFlat(File.ReadAllText(path));
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var ot = root.GetComponentsInChildren<OverrideTransform>(true).FirstOrDefault(o => o.gameObject.name == "Stance_SpineBend");
                if (ot == null) throw new InvalidOperationException("Stance_SpineBend not on the prefab — run AuthorPrefabStructure first");
                var d = ot.data; d.rotation = kv["rotationEuler"].v; ot.data = d;
                var oh = root.GetComponentsInChildren<OverrideTransform>(true).FirstOrDefault(o => o.gameObject.name == "Stance_Hips");
                if (oh == null) throw new InvalidOperationException("Stance_Hips not on the prefab — run AuthorPrefabStructure first");
                var h = oh.data; h.position = kv.ContainsKey("hipsOffsetLocal") ? kv["hipsOffsetLocal"].v : Vector3.zero; oh.data = h;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return "stance written: Spine bend Euler " + d.rotation.ToString("F4") + ", hips offset " + h.position.ToString("F4") + " (" + File.ReadAllText(path).Replace("\n", " ") + ")";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>Vertical clearance of the lowest hand/finger joint over the higher knee joint (world up), plus the nearest hand-joint-to-knee distance.</summary>
        static float KneeClear(Animator anim, out float nearest, out float handYOverRoot, out float kneeYOverRoot)
        {
            Transform kL = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg), kR = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            float kneeY = Mathf.Max(kL.position.y, kR.position.y), handY = float.MaxValue; nearest = float.MaxValue;
            foreach (var bones in new[] { HandHingeModel.LeftJoints, HandHingeModel.RightJoints })
                foreach (var b in bones)
                {
                    var t = anim.GetBoneTransform(b); if (t == null) continue;
                    foreach (var pnt in new[] { t.position, t.childCount > 0 ? t.GetChild(0).position : t.position })
                    { handY = Mathf.Min(handY, pnt.y); nearest = Mathf.Min(nearest, Mathf.Min(Vector3.Distance(pnt, kL.position), Vector3.Distance(pnt, kR.position))); }
                }
            float rootY = anim.transform.position.y;
            handYOverRoot = handY - rootY; kneeYOverRoot = kneeY - rootY;
            return handY - kneeY;
        }

        /// <summary>Smallest distance from any finger bone segment (MCP→PIP→DIP→tip, four fingers) to the shaft axis.</summary>
        static float FingerSegMin(Animator anim, HandGeom g, Vector3 s0, Vector3 dir)
        {
            float worst = float.MaxValue;
            for (int f = 1; f <= 4; f++)
            {
                var t0 = anim.GetBoneTransform(g.data.joints[HandHingeModel.JointIndex(f, 0)].bone); var t1 = anim.GetBoneTransform(g.data.joints[HandHingeModel.JointIndex(f, 1)].bone); var t2 = anim.GetBoneTransform(g.data.joints[HandHingeModel.JointIndex(f, 2)].bone);
                worst = Mathf.Min(worst, Mathf.Min(HandHingeModel.SegmentToLineDistance(t0.position, t1.position, s0, dir, out _), Mathf.Min(HandHingeModel.SegmentToLineDistance(t1.position, t2.position, s0, dir, out _), HandHingeModel.SegmentToLineDistance(t2.position, t2.GetChild(0).position, s0, dir, out _))));
            }
            return worst;
        }

        static float AxisDist(Vector3 p, Vector3 o, Vector3 d) => Vector3.Cross(d, p - o).magnitude;
        static Vector3 ClosestOnAxis(Vector3 p, Vector3 o, Vector3 d) => o + Vector3.Dot(p - o, d) * d;
        static string F(float v) => v.ToString("F5", CultureInfo.InvariantCulture);
        static string F1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
        static string Mm(float m) => (m * 1000f).ToString("F2", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => v.ToString("F4");
        static string Q(Quaternion q) => "(" + F(q.x) + ", " + F(q.y) + ", " + F(q.z) + ", " + F(q.w) + ")";
        static string J(Vector3 v) => "[" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + "]";
        static string J(Quaternion q) => "[" + F(q.x) + ", " + F(q.y) + ", " + F(q.z) + ", " + F(q.w) + "]";

        /// <summary>
        /// Scene-camera frames at the PUTT address (Cesar, 2026-09-15: "putter blade points the wrong way"): the
        /// gameplay camera never shows the putter head at address, and the drive-address frames show the driver.
        /// Called by the verification recorder's putt block (reflection) once the animator is in Address_Putt.
        /// Writes &lt;EvidenceRoot&gt;/putt/putt_{targetside,faceon,downshaft,head}.png.
        /// </summary>
        public static string ShootPuttFrames(GameObject golfer, Component shot)
        {
            var log = new StringBuilder();
            var all = golfer.GetComponentsInChildren<Transform>(true);
            Transform Tf(string n) => all.FirstOrDefault(t => t.name == n);
            var anim = golfer.GetComponentInChildren<Animator>(true);
            Transform putter = Tf("GOLFIN_Putter"), pslot = Tf("PutterSlot");
            // the head transform is "Clubhead" on the putters and "ClubHead" on the drivers (the recorder keys its
            // face conventions on exactly those names); "Shaft_Head" is the shaft+head group at the grip end — not it
            Transform head = putter != null ? putter.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Clubhead" || t.name == "ClubHead") : null;
            if (anim == null || putter == null || pslot == null || head == null) return "putt frames: missing putter/slot/head";
            // the Clubhead TRANSFORM's pivot sits at the club origin (the grip end) on these prefabs; the head MESH is
            // 0.78 m down the shaft — frame the renderer, not the pivot
            var headRend = head.GetComponentInChildren<Renderer>(true);
            Vector3 headPos = headRend != null ? headRend.bounds.center : head.position;
            foreach (var smr in golfer.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.forceMatrixRecalculationPerRender = true;
            Transform handL = anim.GetBoneTransform(HumanBodyBones.LeftHand), handR = anim.GetBoneTransform(HumanBodyBones.RightHand);
            float hAim = Heading(shot);
            Vector3 aimDir = new Vector3(Mathf.Cos(hAim), 0f, Mathf.Sin(hAim));
            Vector3 handsMid = 0.5f * (handL.position + handR.position);
            Vector3 fwd = Vector3.ProjectOnPlane(headPos - golfer.transform.position, Vector3.up).normalized;
            string dir = GolferTestCharacter.EvidenceRoot + "/putt"; Directory.CreateDirectory(dir);
            Vector3 mid = golfer.transform.position + Vector3.up * 0.90f;
            Shoot(mid, mid + aimDir * 4.0f + Vector3.up * 0.2f, Vector3.up, 1600, Path.Combine(dir, "putt_targetside.png"), log);
            Shoot(mid, mid + fwd * 4.0f + Vector3.up * 0.2f, Vector3.up, 1600, Path.Combine(dir, "putt_faceon.png"), log);
            Shoot(handsMid, handsMid - aimDir * 0.85f, Vector3.up, 1600, Path.Combine(dir, "putt_awayside.png"), log);
            // wider than the first cut (0.35/0.45 m framed grass): a metre off, from the target side and above, and
            // a metre straight above — the blade is 12 cm long, FOV 30 at 1 m is a 54 cm field
            Shoot(headPos, headPos + (aimDir * 0.75f + Vector3.up * 0.65f - fwd * 0.25f), Vector3.up, 1600, Path.Combine(dir, "putt_head.png"), log);
            Shoot(headPos, headPos + Vector3.up * 1.0f + aimDir * 0.02f, aimDir, 1600, Path.Combine(dir, "putt_head_top.png"), log);   // straight down, target line = image up
            Shoot(headPos, handsMid + (handsMid - headPos).normalized * 0.4f + Vector3.up * 0.1f, Vector3.up, 1600, Path.Combine(dir, "putt_head_downshaft.png"), log);   // from behind the hands, down the shaft
            // ground truth for the face side, independent of the heading convention: the face must point from the
            // head toward the CUP. Any transform whose name says cup/hole-flag is a candidate; all are logged.
            var ballTf = golfer.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Ball" || t.name.StartsWith("Ball_") || t.name == "GolfBall");
            foreach (var cand in golfer.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t.name.IndexOf("cup", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("flag", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("pin", StringComparison.OrdinalIgnoreCase) >= 0).Take(8))
            {
                Vector3 toCup = Vector3.ProjectOnPlane(cand.position - headPos, Vector3.up).normalized;
                log.AppendLine("cup candidate '" + cand.name + "' at " + V(cand.position) + " dist " + F(Vector3.Distance(cand.position, headPos)) + " m; head.forward(+Z)·toCup " + F(Vector3.Dot(head.forward, toCup)) + "; aimDir·toCup " + F(Vector3.Dot(aimDir, toCup)));
            }
            // a marker at where this code believes the head is, so the close-ups can be read even when they miss
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere); marker.name = "[PuttHeadMarker]"; marker.transform.position = headPos; marker.transform.localScale = Vector3.one * 0.03f;
            var mrend = marker.GetComponent<Renderer>(); mrend.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")); mrend.sharedMaterial.color = Color.red;
            try
            {
                Shoot(headPos, headPos + Vector3.up * 2.0f + aimDir * 0.02f, aimDir, 1600, Path.Combine(dir, "putt_head_top2m.png"), log);
                Shoot(headPos, headPos + (aimDir * 1.4f + Vector3.up * 1.0f), Vector3.up, 1600, Path.Combine(dir, "putt_head_fromtarget.png"), log);
                Shoot(headPos, headPos + (-aimDir * 1.4f + Vector3.up * 1.0f), Vector3.up, 1600, Path.Combine(dir, "putt_head_frombehind.png"), log);
            }
            finally { UnityEngine.Object.DestroyImmediate(marker); }
            log.AppendLine("putt head renderer: " + (headRend == null ? "<none>" : headRend.name + " enabled=" + headRend.enabled + " active=" + headRend.gameObject.activeInHierarchy + " bounds " + V(headRend.bounds.size)) + "; hands mid " + V(handsMid) + "; golfer root " + V(golfer.transform.position));
            log.AppendLine("putt address: PutterSlot local " + V(pslot.localPosition) + " " + Q(pslot.localRotation) + "; head mesh " + V(headPos) + "; slot.up·aim " + F(Vector3.Dot(pslot.up, aimDir)) + "; head.forward·aim " + F(Vector3.Dot(head.forward, aimDir)) + " head.right·aim " + F(Vector3.Dot(head.right, aimDir)));
            return log.ToString();
        }

        static void Shoot(Vector3 aimAt, Vector3 camPos, Vector3 up, int res, string path, StringBuilder log)
        {
            RenderTexture rt = null; GameObject camGo = null; Texture2D tex = null; RenderTexture prev = RenderTexture.active;
            try
            {
                camGo = new GameObject("[HingeStage2Cam]");
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 30f; cam.nearClipPlane = 0.02f; cam.farClipPlane = 200f;
                camGo.transform.position = camPos;
                camGo.transform.rotation = Quaternion.LookRotation(aimAt - camPos, up);
                rt = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(res, res, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, res, res), 0, 0); tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                log.AppendLine("frame: " + path);
            }
            catch (Exception e) { log.AppendLine("frame FAILED " + path + ": " + e.Message); }
            finally
            {
                RenderTexture.active = prev;
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
#endif
