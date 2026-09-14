// golfer_club_grip SPEC §3.12.6 stage 1 — the grip pose in hand space, with a debug cylinder.
//
// Edit mode, one hand at a time, no club, no rig. For each hand: the §3.12.4 shaft axis in
// Hand-local space, a cylinder of the Grip mesh radius (13.575 mm) drawn on it as a child of the
// Hand bone, the §3.12.3 start pose, the per-finger k bisection onto the surface, the thumb aimed
// along the shaft at "1 o'clock", then measurements + three full-res frames per hand:
// down the shaft from the butt, from the palm side, from the back. Same temp-additive-scene /
// Camera.Render → RenderTexture path as stage 0; the open scene is never dirtied.
//
// RunStage1Batch is the command-line entry (-batchmode -executeMethod ... -quit): it runs, writes
// the console text next to the frames, and exits 0 / 2.

#if GOLFIN_GOLFER_TEST
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Golfin.Gameplay.Golfer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.EditorTools.Golfer
{
    public static class HandHingeStage1Tool
    {
        public const string PrefabPath = HandHingeStage0Tool.PrefabPath;
        public const string AssetPath  = HandHingeStage0Tool.AssetPath;
        const string DefaultOutDir = "Docs/Specs/Active/golfer_club_grip/evidence/stage1";

        /// <summary>Thumb: "1 o'clock viewed from the butt" = 30° from top (−u) toward +n; station down-shaft of the thumb base.</summary>
        public const float ThumbClockDeg = 30f;
        public const float ThumbStationM = 0.060f;
        const float CylinderButtM = 0.06f, CylinderHeadM = 0.24f;   // how far the debug cylinder runs each way from the little MCP

        [MenuItem("GOLFIN/Golfer Test/Hinge/Stage 1 grip pose in hand space")]
        public static void RunStage1Menu() => Debug.Log(RunStage1(DefaultOutDir, true));

        /// <summary>Command-line entry: -batchmode -executeMethod Golfin.EditorTools.Golfer.HandHingeStage1Tool.RunStage1Batch -quit</summary>
        public static void RunStage1Batch()
        {
            try
            {
                string r = RunStage1(DefaultOutDir, true);
                Debug.Log(r);
                File.WriteAllText(Path.Combine(DefaultOutDir, "stage1_console.txt"), r);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[HandHingeStage1] " + e);
                try { Directory.CreateDirectory(DefaultOutDir); File.WriteAllText(Path.Combine(DefaultOutDir, "stage1_console.txt"), "FAILED: " + e); } catch { }
                EditorApplication.Exit(2);
            }
        }

        public static string RunStage1(string outDir, bool frames, int resolution = 1600)
        {
            var data = AssetDatabase.LoadAssetAtPath<HandHingeData>(AssetPath);
            if (data == null) throw new InvalidOperationException("Capture the asset first: " + AssetPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Prefab not found: " + PrefabPath);
            Directory.CreateDirectory(outDir);

            Scene prevActive = SceneManager.GetActiveScene();
            // Batch mode boots into an untitled scene, and Unity refuses an additive NewScene beside
            // one; replace it instead (nothing to preserve). In the Editor a real scene is open and
            // must not be touched, so go additive and close the temp scene afterwards.
            bool untitled = string.IsNullOrEmpty(prevActive.path);
            Scene temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, untitled ? NewSceneMode.Single : NewSceneMode.Additive);
            GameObject inst = null, camGo = null;
            var sb = new StringBuilder();
            var json = new StringBuilder();
            try
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, temp);
                inst.transform.position = new Vector3(0f, 500f, 0f);
                foreach (var b in inst.GetComponentsInChildren<Behaviour>(true))
                    if (b.GetType().Name == "RigBuilder") b.enabled = false;    // stage 1: no rig
                foreach (var s in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    s.updateWhenOffscreen = true;
                    // A SkinnedMeshRenderer is skinned ONCE per editor frame; every Camera.Render in the same
                    // frame reuses it. Without this, frames taken after the pose changed (the second hand,
                    // the supplementary pass) silently show the FIRST render's pose with the new bones.
                    s.forceMatrixRecalculationPerRender = true;
                }
                var anim = inst.GetComponentInChildren<Animator>(true);

                camGo = new GameObject("[HingeStage1Cam]");
                SceneManager.MoveGameObjectToScene(camGo, temp);
                Camera cam = null;
                if (frames)
                {
                    cam = camGo.AddComponent<Camera>();
                    var keyGo = new GameObject("[Key]"); keyGo.transform.SetParent(camGo.transform, false);
                    var key = keyGo.AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.3f;
                    keyGo.transform.localRotation = Quaternion.Euler(25f, -20f, 0f);
                    var fillGo = new GameObject("[Fill]"); fillGo.transform.SetParent(camGo.transform, false);
                    var fill = fillGo.AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = 0.5f; fill.color = new Color(0.85f, 0.9f, 1f);
                    fillGo.transform.localRotation = Quaternion.Euler(-30f, 35f, 0f);
                }

                sb.Append("stage1: contact=").Append(Mm(HandHingeModel.ContactM)).Append(" mm (r ").Append(Mm(HandHingeModel.ShaftRadiusM))
                  .Append(" + finger ").Append(Mm(HandHingeModel.FingerHalfThicknessM)).Append("), tol ±").Append(Mm(HandHingeModel.ContactToleranceM))
                  .Append(" mm; thumb clock ").Append(ThumbClockDeg).Append("° station ").Append(Mm(ThumbStationM)).Append(" mm\n");
                json.Append("{\n  \"contactMm\": ").Append(Mm(HandHingeModel.ContactM)).Append(", \"toleranceMm\": ").Append(Mm(HandHingeModel.ContactToleranceM))
                    .Append(", \"shaftRadiusMm\": ").Append(Mm(HandHingeModel.ShaftRadiusM)).Append(", \"thumbClockDeg\": ").Append(ThumbClockDeg)
                    .Append(", \"thumbStationMm\": ").Append(Mm(ThumbStationM)).Append(",\n  \"hands\": [\n");

                json.Append(OneHand(anim, data.left,  true,  cam, resolution, outDir, "lead_left",   sb)).Append(",\n");
                json.Append(OneHand(anim, data.right, false, cam, resolution, outDir, "trail_right", sb)).Append("\n  ]\n}\n");

                File.WriteAllText(Path.Combine(outDir, "stage1_numbers.json"), json.ToString());

                // supplementary: the inscribed per-joint wrap, same axis, same thumb — data for the §3.12.3 decision
                string supDir = Path.Combine(outDir, "supplementary_inscribed");
                Directory.CreateDirectory(supDir);
                sb.Append("\n===== SUPPLEMENTARY: inscribed per-joint wrap (NOT the §3.12.3 one-k solve) =====\n");
                var jsup = new StringBuilder();
                jsup.Append(OneHand(anim, data.left,  true,  cam, resolution, supDir, "lead_left_inscribed",   sb, true)).Append(",\n");
                jsup.Append(OneHand(anim, data.right, false, cam, resolution, supDir, "trail_right_inscribed", sb, true));
                File.WriteAllText(Path.Combine(supDir, "stage1_inscribed_numbers.json"), "{\n  \"hands\": [\n" + jsup + "\n  ]\n}\n");
                sb.Append("json: ").Append(Path.Combine(outDir, "stage1_numbers.json")).Append('\n');
            }
            finally
            {
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
                if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
                if (!untitled)
                {
                    if (prevActive.IsValid()) SceneManager.SetActiveScene(prevActive);
                    EditorSceneManager.CloseScene(temp, true);
                }
            }
            return sb.ToString();
        }

        static string OneHand(Animator anim, in HandHingeHand hand, bool lead, Camera cam, int res, string outDir, string label, StringBuilder sb, bool inscribed = false)
        {
            Transform h = anim.GetBoneTransform(HandHingeModel.HandBone(hand.right));
            HandHingeModel.GripAxisHandLocal(anim, hand, lead, out Vector3 o, out Vector3 d);
            Vector3 n = hand.palmNormalHandLocal, u = hand.lengthAxisHandLocal;

            // debug cylinder on the axis, child of the Hand bone so it is in hand space by construction
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "[GripCylinder_" + label + "]";
            UnityEngine.Object.DestroyImmediate(cyl.GetComponent<Collider>());
            cyl.transform.SetParent(h, false);
            float len = CylinderButtM + CylinderHeadM;
            cyl.transform.localPosition = o + d * (0.5f * (CylinderHeadM - CylinderButtM));
            cyl.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
            cyl.transform.localScale = new Vector3(2f * HandHingeModel.ShaftRadiusM, 0.5f * len, 2f * HandHingeModel.ShaftRadiusM);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = new Color(0.95f, 0.55f, 0.15f) };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.95f, 0.55f, 0.15f));
            cyl.GetComponent<Renderer>().sharedMaterial = mat;

            // the §3.12.3 start pose, then the per-finger k solve
            var pose = lead ? HandHingeModel.LeadGripStart() : HandHingeModel.TrailGripStart();
            HandHingeModel.Apply(anim, hand, pose);
            var solve = lead
                ? new[] { (HandHingeModel.Index, pose.index), (HandHingeModel.Middle, pose.middle), (HandHingeModel.Ring, pose.ring), (HandHingeModel.Little, pose.little) }
                : new[] { (HandHingeModel.Index, pose.index), (HandHingeModel.Middle, pose.middle), (HandHingeModel.Ring, pose.ring) };
            var results = new HandHingeModel.FingerAxisMetrics[solve.Length];
            for (int i = 0; i < solve.Length; i++)
                results[i] = inscribed
                    ? HandHingeModel.SolveFingerInscribed(anim, hand, solve[i].Item1, solve[i].Item2.spread, o, d).metrics
                    : HandHingeModel.SolveFingerK(anim, hand, solve[i].Item1, solve[i].Item2, o, d);
            HandHingeModel.FingerAxisMetrics? littleFixed = null;
            if (!lead)
            {
                HandHingeModel.Finger(anim, hand, HandHingeModel.Little, pose.little);   // rides on the lead index; not solved
                var lf = HandHingeModel.MeasureFingerAxis(anim, hand, HandHingeModel.Little, o, d);
                lf.k = 1f; lf.note = "fixed 40/60/30 — not solved against the club (§3.12.3)";
                littleFixed = lf;
            }

            // thumb: aim along the shaft at 1 o'clock, then the two fixed hinges
            Vector3 aim = HandHingeModel.ThumbAimAlongShaft(anim, hand, o, d, ThumbClockDeg, ThumbStationM);
            HandHingeModel.ThumbAim(anim, hand, aim);
            HandHingeModel.Hinge(anim, hand.joints[HandHingeModel.JointIndex(HandHingeModel.Thumb, 1)], pose.thumbFlexIntermediate, 0f);
            HandHingeModel.Hinge(anim, hand.joints[HandHingeModel.JointIndex(HandHingeModel.Thumb, 2)], pose.thumbFlexDistal, 0f);
            HandHingeModel.MeasureThumbAxis(anim, hand, o, d, out float th2, out float th3, out float thTip);
            Transform t1 = anim.GetBoneTransform(hand.joints[HandHingeModel.JointIndex(HandHingeModel.Thumb, 0)].bone);
            Transform t2 = anim.GetBoneTransform(hand.joints[HandHingeModel.JointIndex(HandHingeModel.Thumb, 1)].bone);
            float thumbAlongDeg = Vector3.Angle(h.InverseTransformDirection(t2.position - t1.position), d);

            // heel pad: the wrist-side palm — Hand bone origin and a point 30 mm toward the knuckles, against the axis
            Vector3 handO = Vector3.zero, heel = u * 0.03f;
            float heelDist = HandHingeModel.SegmentToLineDistance(handO, heel, o, d, out _);

            // ── report ──
            sb.Append(lead ? "LEAD (left)" : "TRAIL (right)").Append(inscribed ? " [INSCRIBED, supplementary]" : " [one-k, §3.12.3]").Append(" axis o=").Append(o.ToString("F4")).Append(" d=").Append(d.ToString("F4")).Append(" (Hand-local)\n");
            foreach (var m in results) sb.Append(Line(m));
            if (littleFixed.HasValue) sb.Append(Line(littleFixed.Value));
            sb.Append("  thumb  Thumb2 ").Append(Mm(th2)).Append("  Thumb3 ").Append(Mm(th3)).Append("  tip ").Append(Mm(thTip))
              .Append(" mm from the axis; proximal vs shaft ").Append(thumbAlongDeg.ToString("F1")).Append("°\n");
            sb.Append("  wrist  wrist→+30mm segment to axis ").Append(Mm(heelDist)).Append(" mm (heel-pad proxy, informational)\n");

            // ── frames ──
            if (cam != null)
            {
                var pre = HandHingeModel.MeasureFingerAxis(anim, hand, HandHingeModel.Index, o, d);
                sb.Append("  [pre-render] index pip=").Append(Mm(pre.pip)).Append(" tip=").Append(Mm(pre.tip)).Append(" mm; index MCP localRot=")
                  .Append(anim.GetBoneTransform(hand.joints[HandHingeModel.JointIndex(HandHingeModel.Index, 0)].bone).localRotation.ToString("F3")).Append('\n');
                Vector3 centre = h.TransformPoint(o + d * 0.06f);
                Vector3 dW = h.TransformDirection(d), nW = h.TransformDirection(n), uW = h.TransformDirection(u);
                Vector3 topW = Vector3.ProjectOnPlane(-uW, dW).normalized;
                Shoot(cam, h.TransformPoint(o - d * 0.22f), dW, topW, res, Path.Combine(outDir, label + "_downshaft.png"), sb);
                Shoot(cam, centre + nW * 0.30f, -nW, dW, res, Path.Combine(outDir, label + "_palm.png"), sb);
                Shoot(cam, centre - nW * 0.30f, nW, dW, res, Path.Combine(outDir, label + "_back.png"), sb);
                Shoot(cam, centre + (-uW + nW * 0.4f).normalized * 0.30f, (uW - nW * 0.4f).normalized, dW, res, Path.Combine(outDir, label + "_top.png"), sb);
                var post = HandHingeModel.MeasureFingerAxis(anim, hand, HandHingeModel.Index, o, d);
                sb.Append("  [post-render] index pip=").Append(Mm(post.pip)).Append(" tip=").Append(Mm(post.tip)).Append(" mm\n");
            }

            UnityEngine.Object.DestroyImmediate(cyl);
            UnityEngine.Object.DestroyImmediate(mat);

            // ── json ──
            var j = new StringBuilder();
            j.Append("   {\"hand\": \"").Append(label).Append("\", \"axisO\": ").Append(V(o)).Append(", \"axisD\": ").Append(V(d)).Append(",\n    \"fingers\": [");
            bool first = true;
            foreach (var m in results) { j.Append(first ? "" : ", ").Append(J(m)); first = false; }
            if (littleFixed.HasValue) j.Append(", ").Append(J(littleFixed.Value));
            j.Append("],\n    \"thumb\": {\"thumb2Mm\": ").Append(Mm(th2)).Append(", \"thumb3Mm\": ").Append(Mm(th3)).Append(", \"tipMm\": ").Append(Mm(thTip))
             .Append(", \"proximalVsShaftDeg\": ").Append(thumbAlongDeg.ToString("F2", CultureInfo.InvariantCulture)).Append("},\n    \"wristSegmentMm\": ").Append(Mm(heelDist)).Append("}");
            return j.ToString();
        }

        static string Line(in HandHingeModel.FingerAxisMetrics m)
        {
            float delta = (m.closestWrapped - HandHingeModel.ContactM) * 1000f;
            bool inside = m.minAll < HandHingeModel.ContactM - HandHingeModel.ContactToleranceM;
            return "  " + m.name.PadRight(6) + " k=" + m.k.ToString("F3", CultureInfo.InvariantCulture)
                 + " closestWrapped=" + Mm(m.closestWrapped) + " mm (" + (delta >= 0 ? "+" : "") + delta.ToString("F2", CultureInfo.InvariantCulture) + ")"
                 + " seg prox/mid/dist=" + Mm(m.segProx) + "/" + Mm(m.segMid) + "/" + Mm(m.segDist)
                 + " joints mcp/pip/dip/tip=" + Mm(m.mcp) + "/" + Mm(m.pip) + "/" + Mm(m.dip) + "/" + Mm(m.tip)
                 + " minAll=" + Mm(m.minAll) + " boneInsideMesh=" + (m.minAll < HandHingeModel.ShaftRadiusM)
                 + " inside(contact-tol)=" + inside + " " + (m.note ?? "") + "\n";
        }

        static string J(in HandHingeModel.FingerAxisMetrics m)
        {
            float delta = (m.closestWrapped - HandHingeModel.ContactM) * 1000f;
            bool inside = m.minAll < HandHingeModel.ContactM - HandHingeModel.ContactToleranceM;
            bool onSurface = Mathf.Abs(delta) <= HandHingeModel.ContactToleranceM * 1000f;
            return "{\"name\": \"" + m.name + "\", \"k\": " + m.k.ToString("F4", CultureInfo.InvariantCulture)
                 + ", \"closestWrappedMm\": " + Mm(m.closestWrapped) + ", \"deltaFromContactMm\": " + delta.ToString("F2", CultureInfo.InvariantCulture)
                 + ", \"onSurface\": " + (onSurface ? "true" : "false") + ", \"nothingInside\": " + (inside ? "false" : "true")
                 + ", \"segProxMm\": " + Mm(m.segProx) + ", \"segMidMm\": " + Mm(m.segMid) + ", \"segDistMm\": " + Mm(m.segDist)
                 + ", \"mcpMm\": " + Mm(m.mcp) + ", \"pipMm\": " + Mm(m.pip) + ", \"dipMm\": " + Mm(m.dip) + ", \"tipMm\": " + Mm(m.tip)
                 + ", \"minAllMm\": " + Mm(m.minAll) + ", \"boneInsideMesh\": " + (m.minAll < HandHingeModel.ShaftRadiusM ? "true" : "false")
                 + ", \"note\": \"" + (m.note ?? "") + "\"}";
        }

        static void Shoot(Camera cam, Vector3 pos, Vector3 forward, Vector3 up, int res, string path, StringBuilder log)
        {
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(forward, up);
            cam.fieldOfView = 34f; cam.nearClipPlane = 0.02f; cam.farClipPlane = 1.5f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.17f, 0.20f);
            RenderTexture rt = null; Texture2D tex = null; RenderTexture prev = RenderTexture.active;
            try
            {
                rt = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(res, res, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                log.Append("frame: ").Append(path).Append('\n');
            }
            finally
            {
                RenderTexture.active = prev;
                cam.targetTexture = null;
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static string Mm(float m) => (m * 1000f).ToString("F2", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => "[" + v.x.ToString("F5", CultureInfo.InvariantCulture) + ", " + v.y.ToString("F5", CultureInfo.InvariantCulture) + ", " + v.z.ToString("F5", CultureInfo.InvariantCulture) + "]";
    }
}
#endif
