// golfer_club_grip SPEC §3.12.6 stage 0 — the fist test, in the editor, with no club and no rig.
//
// Two menu items and the static entry points behind them (an MCP script-execute calls the statics):
//   • CaptureAsset  — capture both hands from PfGolfer_MixamoNative's rest pose into
//                     Assets/Art/3D/Characters/_Test/Resources/GolferTest/HandHinge_MixamoNative.asset
//   • RunFist       — instantiate the prefab into a TEMPORARY additive scene (the open scene is never
//                     dirtied), pose both hands from the asset, measure, render four full-res frames
//                     (each hand, palm side and back side) with a throw-away camera into a
//                     RenderTexture, write the numbers as JSON, close the temp scene.
// The render is the edit-mode Camera.Render → RenderTexture path SPEC §6 A4 sanctions for a second
// camera; nothing here touches CaptureCore or the Game View.

#if GOLFIN_GOLFER_TEST
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
    public static class HandHingeStage0Tool
    {
        public const string PrefabPath = "Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_MixamoNative.prefab";
        public const string AssetPath  = "Assets/Art/3D/Characters/_Test/Resources/GolferTest/HandHinge_MixamoNative.asset";
        const string DefaultOutDir = "Docs/Specs/Active/golfer_club_grip/evidence/stage0";

        [MenuItem("GOLFIN/Golfer Test/Hinge/Capture HandHinge_MixamoNative.asset")]
        public static void CaptureAssetMenu() => Debug.Log(CaptureAsset());

        [MenuItem("GOLFIN/Golfer Test/Hinge/Stage 0 fist test (65-85-40, thumb 30-20)")]
        public static void RunFistMenu() => Debug.Log(RunFist(65f, 85f, 40f, 30f, 20f, DefaultOutDir, "fist_65_85_40"));

        /// <summary>Capture from the rest pose (prefab contents, edit mode) and (re)write the asset.</summary>
        public static string CaptureAsset()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var anim = root.GetComponentInChildren<Animator>(true);
                var left = HandHingeModel.Capture(anim, false);
                var right = HandHingeModel.Capture(anim, true);

                var data = AssetDatabase.LoadAssetAtPath<HandHingeData>(AssetPath);
                bool created = data == null;
                if (created) data = ScriptableObject.CreateInstance<HandHingeData>();
                data.sourcePrefab = PrefabPath;
                data.left = left;
                data.right = right;
                if (created) AssetDatabase.CreateAsset(data, AssetPath);
                else EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                return (created ? "created " : "updated ") + AssetPath + "\n" + Describe(left) + Describe(right);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static string Describe(in HandHingeHand h)
        {
            var sb = new StringBuilder();
            sb.Append(h.right ? "RIGHT" : "LEFT").Append(" palmNormalHandLocal=").Append(h.palmNormalHandLocal.ToString("F4"))
              .Append(" lengthAxisHandLocal=").Append(h.lengthAxisHandLocal.ToString("F4"))
              .Append(" acrossAxisHandLocal=").Append(h.acrossAxisHandLocal.ToString("F4"))
              .Append(" thumbRestDirHandLocal=").Append(h.thumbRestDirHandLocal.ToString("F4")).Append('\n');
            foreach (var j in h.joints)
                sb.Append("  ").Append(j.boneName).Append(" rest=").Append(j.restLocalRotation.ToString("F4"))
                  .Append(" hinge=").Append(j.hingeAxisLocal.ToString("F4")).Append(" abduct=").Append(j.abductAxisLocal.ToString("F4"))
                  .Append(" len=").Append(j.segmentLength.ToString("F4")).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// Pose both hands (same triple on all four fingers, thumbs at rest aim with the two fixed
        /// flexes), measure, and — when <paramref name="frames"/> — render four frames into
        /// <paramref name="outDir"/> named <paramref name="label"/>_{left,right}_{palm,back}.png.
        /// Returns the report text; the same numbers land in <c>&lt;label&gt;_numbers.json</c>.
        /// </summary>
        public static string RunFist(float mcp, float pip, float dip, float thumbInter, float thumbDistal,
                                     string outDir, string label, bool frames = true, int resolution = 1600)
        {
            var data = AssetDatabase.LoadAssetAtPath<HandHingeData>(AssetPath);
            if (data == null) throw new System.InvalidOperationException("Capture the asset first: " + AssetPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Scene prevActive = SceneManager.GetActiveScene();
            Scene temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject inst = null, camGo = null;
            var sb = new StringBuilder();
            try
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, temp);
                inst.transform.position = new Vector3(0f, 500f, 0f);      // nowhere near the open scene
                foreach (var b in inst.GetComponentsInChildren<Behaviour>(true))
                    if (b.GetType().Name == "RigBuilder") b.enabled = false;   // stage 0: no rig
                foreach (var s in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true)) s.updateWhenOffscreen = true;

                var anim = inst.GetComponentInChildren<Animator>(true);
                var pose = HandPose.Uniform(mcp, pip, dip, thumbInter, thumbDistal);
                HandHingeModel.Apply(anim, data.left, pose);
                HandHingeModel.Apply(anim, data.right, pose);

                var mL = HandHingeModel.Measure(anim, data.left);
                var mR = HandHingeModel.Measure(anim, data.right);

                Directory.CreateDirectory(outDir);
                sb.Append("stage0 ").Append(label).Append(": mcp/pip/dip=").Append(mcp).Append('/').Append(pip).Append('/').Append(dip)
                  .Append(" thumb=").Append(thumbInter).Append('/').Append(thumbDistal).Append(" spread=0\n");
                sb.Append(Report(mL)).Append(Report(mR));

                if (frames)
                {
                    camGo = new GameObject("[HingeStage0Cam]");
                    SceneManager.MoveGameObjectToScene(camGo, temp);
                    var cam = camGo.AddComponent<Camera>();
                    var lightGo = new GameObject("[HingeStage0Key]"); lightGo.transform.SetParent(camGo.transform, false);
                    var key = lightGo.AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.3f; key.color = Color.white;
                    lightGo.transform.localRotation = Quaternion.Euler(25f, -20f, 0f);
                    var fillGo = new GameObject("[HingeStage0Fill]"); fillGo.transform.SetParent(camGo.transform, false);
                    var fill = fillGo.AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = 0.5f; fill.color = new Color(0.85f, 0.9f, 1f);
                    fillGo.transform.localRotation = Quaternion.Euler(-30f, 35f, 0f);

                    Shoot(cam, anim, data.left,  mL, true,  resolution, Path.Combine(outDir, label + "_left_palm.png"), sb);
                    Shoot(cam, anim, data.left,  mL, false, resolution, Path.Combine(outDir, label + "_left_back.png"), sb);
                    Shoot(cam, anim, data.right, mR, true,  resolution, Path.Combine(outDir, label + "_right_palm.png"), sb);
                    Shoot(cam, anim, data.right, mR, false, resolution, Path.Combine(outDir, label + "_right_back.png"), sb);
                }

                File.WriteAllText(Path.Combine(outDir, label + "_numbers.json"), Json(label, mcp, pip, dip, thumbInter, thumbDistal, mL, mR));
                sb.Append("json: ").Append(Path.Combine(outDir, label + "_numbers.json")).Append('\n');
            }
            finally
            {
                if (camGo != null) Object.DestroyImmediate(camGo);
                if (inst != null) Object.DestroyImmediate(inst);
                if (prevActive.IsValid()) SceneManager.SetActiveScene(prevActive);
                EditorSceneManager.CloseScene(temp, true);
            }
            return sb.ToString();
        }

        static void Shoot(Camera cam, Animator anim, in HandHingeHand hand, in HandHingeModel.HandMetrics m, bool palmSide,
                          int res, string path, StringBuilder log)
        {
            Transform h = anim.GetBoneTransform(HandHingeModel.HandBone(hand.right));
            // frame centre: wrist, MCPs and tips
            Vector3 c = h.position + m.mcpCentroidWorld;
            foreach (var f in m.fingers) c += f.tipWorld;
            c /= 2f + m.fingers.Length;
            Vector3 n = m.palmNormalWorld, up = m.lengthAxisWorld;
            Vector3 from = palmSide ? n : -n;
            const float dist = 0.30f;
            cam.transform.position = c + from * dist;
            cam.transform.rotation = Quaternion.LookRotation(-from, up);
            cam.fieldOfView = 34f; cam.nearClipPlane = 0.02f; cam.farClipPlane = 1.5f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.17f, 0.20f);
            cam.allowMSAA = true;

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
                if (rt != null) rt.Release();
                if (rt != null) Object.DestroyImmediate(rt);
                if (tex != null) Object.DestroyImmediate(tex);
            }
        }

        static string Report(in HandHingeModel.HandMetrics m)
        {
            var sb = new StringBuilder();
            sb.Append(m.right ? "RIGHT" : "LEFT").Append(" palmNormalWorld=").Append(m.palmNormalWorld.ToString("F4")).Append('\n');
            foreach (var f in m.fingers)
                sb.Append("  ").Append(f.name.PadRight(6)).Append(" tip->palmPlane ").Append(Mm(f.tipToPalmPlaneM)).Append(" mm")
                  .Append("  (pip ").Append(Mm(f.pipToPalmPlaneM)).Append(", dip ").Append(Mm(f.dipToPalmPlaneM)).Append(")\n");
            sb.Append("  thumb  tip->palmPlane ").Append(Mm(m.thumbTipToPalmPlaneM)).Append(" mm\n");
            string[] pairs = { "index-middle", "middle-ring", "ring-little" };
            for (int p = 0; p < 3; p++)
                sb.Append("  ").Append(pairs[p].PadRight(13)).Append(" tipSpacing ").Append(Mm(m.adjacentTipSpacingM[p])).Append(" mm")
                  .Append("  minSegment ").Append(Mm(m.adjacentMinSegmentM[p])).Append(" mm")
                  .Append("  crossing=").Append(m.adjacentCrossing[p]).Append('\n');
            return sb.ToString();
        }

        static string Mm(float m) => (m * 1000f).ToString("F2", CultureInfo.InvariantCulture);
        static string F(float v) => v.ToString("F5", CultureInfo.InvariantCulture);

        static string Json(string label, float mcp, float pip, float dip, float ti, float td,
                           in HandHingeModel.HandMetrics l, in HandHingeModel.HandMetrics r)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"label\": \"").Append(label).Append("\",\n  \"flexDeg\": {\"mcp\": ").Append(F(mcp)).Append(", \"pip\": ").Append(F(pip))
              .Append(", \"dip\": ").Append(F(dip)).Append(", \"spread\": 0, \"thumbIntermediate\": ").Append(F(ti)).Append(", \"thumbDistal\": ").Append(F(td)).Append("},\n");
            sb.Append("  \"gate\": {\"tipToPalmPlaneMm\": [8, 20], \"adjacentTipSpacingMmMin\": 8, \"crossing\": false},\n");
            sb.Append("  \"hands\": [").Append(HandJson(l)).Append(",\n").Append(HandJson(r)).Append("]\n}\n");
            return sb.ToString();
        }

        static string HandJson(in HandHingeModel.HandMetrics m)
        {
            var sb = new StringBuilder();
            sb.Append("  {\"hand\": \"").Append(m.right ? "right" : "left").Append("\", \"palmNormalWorld\": [")
              .Append(F(m.palmNormalWorld.x)).Append(", ").Append(F(m.palmNormalWorld.y)).Append(", ").Append(F(m.palmNormalWorld.z)).Append("],\n   \"fingers\": [");
            for (int i = 0; i < m.fingers.Length; i++)
            {
                var f = m.fingers[i];
                float mm = f.tipToPalmPlaneM * 1000f;
                sb.Append(i > 0 ? ", " : "").Append("{\"name\": \"").Append(f.name).Append("\", \"tipToPalmPlaneMm\": ").Append(Mm(f.tipToPalmPlaneM))
                  .Append(", \"pipToPalmPlaneMm\": ").Append(Mm(f.pipToPalmPlaneM)).Append(", \"dipToPalmPlaneMm\": ").Append(Mm(f.dipToPalmPlaneM))
                  .Append(", \"tipInBand\": ").Append(mm >= 8f && mm <= 20f ? "true" : "false").Append('}');
            }
            sb.Append("],\n   \"thumbTipToPalmPlaneMm\": ").Append(Mm(m.thumbTipToPalmPlaneM)).Append(",\n   \"adjacent\": [");
            string[] pairs = { "index-middle", "middle-ring", "ring-little" };
            for (int p = 0; p < 3; p++)
                sb.Append(p > 0 ? ", " : "").Append("{\"pair\": \"").Append(pairs[p]).Append("\", \"tipSpacingMm\": ").Append(Mm(m.adjacentTipSpacingM[p]))
                  .Append(", \"tipSpacingPass\": ").Append(m.adjacentTipSpacingM[p] >= 0.008f ? "true" : "false")
                  .Append(", \"minSegmentMm\": ").Append(Mm(m.adjacentMinSegmentM[p])).Append(", \"crossing\": ").Append(m.adjacentCrossing[p] ? "true" : "false").Append('}');
            sb.Append("],\n   \"anyCrossing\": ").Append(m.anyCrossing ? "true" : "false").Append("}");
            return sb.ToString();
        }
    }
}
#endif
