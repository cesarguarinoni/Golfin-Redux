// golfer_club_grip §3.4 measurement tool.
// Run: GOLFIN > Golfer Grip > Measure Grip Pose on Hole 06
// Boots Hole 06 with PfGolfer_MixamoNative (weight Rig_Hands=0),
// reads GripTarget world pos/rot at address, computes ClubEnd.localPos
// and ClubSlot.localRotation, writes to
//   Docs/Specs/Active/golfer_club_grip/grip_measure_result.json
// then exits play mode.
//
// NOTE: This is an Editor-only script (Assets/Editor/) — no #if guard needed.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;

namespace Golfin.EditorTools
{
    public static class GolferGripMeasureRecorder
    {
        const string ArmedKey    = "GolferGripMeasure.Armed";
        const string HoleKey     = "GolferGripMeasure.Hole";
        const string ShellPath   = "Assets/Scenes/ShellScene.unity";

        [InitializeOnLoadMethod]
        static void Register() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/Golfer Grip/Measure Grip Pose on Hole 06")]
        public static void MeasureHole06()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[GripMeasure] Stop play mode first.");
                return;
            }
            PlayerSettings.runInBackground = true;
            // Disable Console Error Pause so the coroutine is never stranded.
            try
            {
                var le = Type.GetType("UnityEditor.LogEntries,UnityEditor");
                le?.GetMethod("SetConsoleFlag", BindingFlags.Public | BindingFlags.Static)
                  ?.Invoke(null, new object[] { 0x20, false });
            }
            catch { /* non-critical */ }

            EditorSceneManager.OpenScene(ShellPath, OpenSceneMode.Single);
            SessionState.SetBool(ArmedKey, true);
            SessionState.SetInt(HoleKey, 6);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            int hole = SessionState.GetInt(HoleKey, 6);

            var host = new GameObject("[GolferGripMeasureBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<GolferGripMeasureRunner>().Begin(hole);
        }
    }

    public class GolferGripMeasureRunner : MonoBehaviour
    {
        int _hole;
        public void Begin(int hole) { _hole = hole; StartCoroutine(Sequence()); }

        // ── helpers ──────────────────────────────────────────────────────────
        static Type FindType(string name)
        {
            Type found = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { found = asm.GetType(name); } catch { continue; }
                if (found != null) break;
            }
            return found;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        static bool SeedAndLoad(int hole)
        {
            try
            {
                var gsType = FindType("Golfin.Gameplay.Session.GameSession");
                if (gsType == null) { Debug.LogWarning("[GripMeasure] GameSession not found"); return false; }
                gsType.GetProperty("IsVersus", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, false);

                string charId = "";
                var cmType = FindType("Golfin.Roster.CharacterManager") ?? FindType("CharacterManager");
                if (cmType != null)
                {
                    var inst = cmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (inst != null)
                    {
                        var m = cmType.GetMethod("GetSelectedCharacterId");
                        if (m != null) charId = (string)(m.Invoke(inst, null) ?? "");
                    }
                }

                gsType.GetMethod("SeedSession", new[] { typeof(int), typeof(string), typeof(int) })
                      ?.Invoke(null, new object[] { hole, charId, 0 });

                var loaderType = FindType("Golfin.UI.GameplayTransition.GameplaySceneLoader");
                var loaderInst = loaderType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (loaderInst == null) { Debug.LogWarning("[GripMeasure] GameplaySceneLoader.Instance not found"); return false; }

                MethodInfo begin = null;
                foreach (var m in loaderType.GetMethods()) { if (m.Name == "BeginGameplayLoad") { begin = m; break; } }
                if (begin == null) return false;

                var pars = begin.GetParameters();
                begin.Invoke(loaderInst, pars.Length == 1 ? new object[] { hole } : new object[] { hole, null });
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[GripMeasure] SeedAndLoad failed: " + e.Message); return false; }
        }

        static IEnumerator WaitForScene(string name, float timeout)
        {
            float t = 0f;
            while (t < timeout)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sc = SceneManager.GetSceneAt(i);
                    if (sc.name == name && sc.isLoaded) yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
                t += 0.5f;
            }
            Debug.LogWarning("[GripMeasure] timeout waiting for " + name);
        }

        // Try to click a start-gate button (StartButton / PlayButton)
        IEnumerator PassGate()
        {
            yield return new WaitForSecondsRealtime(6f);
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var buttons = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var b in buttons)
                {
                    if (b == null || !b.gameObject.activeInHierarchy) continue;
                    if (b.name != "StartButton" && b.name != "PlayButton") continue;
                    Debug.Log("[GripMeasure] tapping " + b.name);
                    b.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(2f);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Debug.Log("[GripMeasure] no StartButton found (already past gate?)");
        }

        // ── main sequence ─────────────────────────────────────────────────────
        IEnumerator Sequence()
        {
            yield return new WaitForSecondsRealtime(2f);
            yield return PassGate();

            // Force GolferTestBootstrap to spawn MixamoNative (not the default PfGolfer_Test).
            // Without this the runtime loads PfGolfer_Test which has no GripTarget hierarchy.
            var bootstrapType = FindType("Golfin.Gameplay.Golfer.GolferTestBootstrap");
            if (bootstrapType != null)
            {
                var overrideField = bootstrapType.GetField("ResourcePathOverride",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (overrideField != null)
                {
                    overrideField.SetValue(null, "GolferTest/PfGolfer_MixamoNative");
                    Debug.Log("[GripMeasure] ResourcePathOverride -> PfGolfer_MixamoNative");
                }
                else Debug.LogWarning("[GripMeasure] ResourcePathOverride field not found on GolferTestBootstrap — spawning default prefab");
            }
            else Debug.LogWarning("[GripMeasure] GolferTestBootstrap type not found — spawning default prefab");

            Debug.Log("[GripMeasure] SeedAndLoad hole " + _hole);
            if (!SeedAndLoad(_hole))
            {
                Debug.LogError("[GripMeasure] SeedAndLoad failed — aborting");
                EditorApplication.isPlaying = false;
                yield break;
            }

            string geoScene = "Hole_0" + _hole + "_Geo";
            yield return WaitForScene("LabScaffold", 60f);
            yield return WaitForScene(geoScene, 60f);
            Debug.Log("[GripMeasure] scenes loaded — waiting 10s for address settle");
            yield return new WaitForSecondsRealtime(10f);

            // ── Locate golfer ─────────────────────────────────────────────────
            Animator anim = null;
            var allAnims = UnityEngine.Object.FindObjectsByType<Animator>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var a in allAnims)
            {
                if (!a.isHuman) continue;
                if (FindDeep(a.transform, "ClubRoot") != null) { anim = a; break; }
                if (FindDeep(a.transform, "GripTarget") != null) { anim = a; break; }
            }

            if (anim == null)
            {
                // Fallback: any human animator
                foreach (var a in allAnims) { if (a.isHuman) { anim = a; break; } }
            }

            if (anim == null)
            {
                Debug.LogError("[GripMeasure] No human Animator found — aborting");
                EditorApplication.isPlaying = false;
                yield break;
            }
            Debug.Log("[GripMeasure] Golfer: " + anim.gameObject.name);

            var golferRoot = anim.gameObject;
            Transform gripTarget      = FindDeep(golferRoot.transform, "GripTarget");
            Transform gripAnchorLead  = FindDeep(golferRoot.transform, "GripAnchor_Lead");
            Transform gripAnchorTrail = FindDeep(golferRoot.transform, "GripAnchor_Trail");
            Transform clubSlot        = FindDeep(golferRoot.transform, "ClubSlot");
            Transform clubStart       = FindDeep(golferRoot.transform, "ClubStart");
            Transform clubEnd         = FindDeep(golferRoot.transform, "ClubEnd");
            Transform rigHandsGO      = FindDeep(golferRoot.transform, "Rig_Hands");

            Transform handL = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform handR = anim.GetBoneTransform(HumanBodyBones.RightHand);

            if (gripTarget == null)
            {
                Debug.LogError("[GripMeasure] GripTarget not found. Hierarchy may not have been built. Run Build Grip Rig first.");
                EditorApplication.isPlaying = false;
                yield break;
            }
            if (handL == null || handR == null)
            {
                Debug.LogError("[GripMeasure] Hand bones not found on Animator.");
                EditorApplication.isPlaying = false;
                yield break;
            }

            // ── Capture with IK ON ────────────────────────────────────────────
            yield return null; // ensure one frame rendered
            Vector3 handL_ik  = handL.position;
            Vector3 handR_ik  = handR.position;
            Vector3 gt_ik_pos = gripTarget.position;
            Quaternion gt_ik_rot = gripTarget.rotation;
            Vector3 ga_lead_ik  = gripAnchorLead  != null ? gripAnchorLead.position  : Vector3.zero;
            Vector3 ga_trail_ik = gripAnchorTrail != null ? gripAnchorTrail.position : Vector3.zero;

            // ── Set Rig_Hands weight = 0 ──────────────────────────────────────
            Rig rigHandsComp = null;
            if (rigHandsGO != null) rigHandsComp = rigHandsGO.GetComponent<Rig>();
            float origWeight = 1f;
            if (rigHandsComp != null) { origWeight = rigHandsComp.weight; rigHandsComp.weight = 0f; }
            yield return null; // one frame for constraint to update

            // ── Capture with IK OFF ───────────────────────────────────────────
            Vector3 handL_noIK = handL.position;
            Vector3 handR_noIK = handR.position;
            Vector3 gt_pos     = gripTarget.position;
            Quaternion gt_rot  = gripTarget.rotation;
            Vector3 ga_lead    = gripAnchorLead  != null ? gripAnchorLead.position  : Vector3.zero;
            Vector3 ga_trail   = gripAnchorTrail != null ? gripAnchorTrail.position : Vector3.zero;
            Vector3 cs_pos     = clubStart != null ? clubStart.position : Vector3.zero;
            Vector3 ce_pos     = clubEnd   != null ? clubEnd.position   : Vector3.zero;

            // ── Ball position ─────────────────────────────────────────────────
            Vector3 ballPos = new Vector3(35.1874f, 9.1761f, -21.2336f); // last harness run value
            bool ballFoundViaReflection = false;

            var presType = FindType("Golfin.Gameplay.Golfer.GolferPresenter");
            if (presType != null)
            {
                var pres = golferRoot.GetComponent(presType) as Component;
                if (pres == null) pres = golferRoot.GetComponentInChildren(presType) as Component;
                if (pres != null)
                {
                    // Try property first
                    foreach (var pname in new[] { "BallWorldPos", "AddressClubHeadWorld", "ClubHeadWorld" })
                    {
                        var prop = presType.GetProperty(pname, BindingFlags.Public | BindingFlags.Instance);
                        if (prop != null && prop.PropertyType == typeof(Vector3))
                        {
                            ballPos = (Vector3)prop.GetValue(pres);
                            ballFoundViaReflection = true;
                            Debug.Log("[GripMeasure] ball from property " + pname + ": " + ballPos);
                            break;
                        }
                    }
                    // Try field
                    if (!ballFoundViaReflection)
                    {
                        var f = presType.GetField("addressHeadLocal", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f != null)
                        {
                            Vector3 local = (Vector3)f.GetValue(pres);
                            ballPos = golferRoot.transform.TransformPoint(local);
                            ballFoundViaReflection = true;
                            Debug.Log("[GripMeasure] ball from addressHeadLocal field (local=" + local + "): " + ballPos);
                        }
                    }
                }
            }
            if (!ballFoundViaReflection)
                Debug.LogWarning("[GripMeasure] Using fallback ball pos from prior harness run: " + ballPos);

            // ── Compute target poses ──────────────────────────────────────────
            // ClubEnd.localPos: GripTarget-local position of the ball
            Vector3 clubEndLocalPos = Quaternion.Inverse(gt_rot) * (ballPos - gt_pos);

            // ClubSlot.localRotation: shaft axis = from ball toward GripAnchor_Lead
            // (+Y = shaft direction from head to butt cap)
            Vector3 shaftWorldDir = (ga_lead_ik - ballPos).normalized;
            if (shaftWorldDir.sqrMagnitude < 0.01f)
            {
                // Fallback: use IK grip target up direction
                shaftWorldDir = gt_rot * Vector3.up;
                Debug.LogWarning("[GripMeasure] GripAnchor_Lead and ball too close — using GripTarget.up as shaft dir");
            }
            // In GripTarget local space, +Y should point along shaftWorldDir
            // ClubSlot.localRotation = rotation that takes Vector3.up to shaftWorldDir, in GripTarget space
            Quaternion targetWorldRot = Quaternion.FromToRotation(Vector3.up, shaftWorldDir);
            Quaternion clubSlotLocalRot = Quaternion.Inverse(gt_rot) * targetWorldRot;

            // ── §6 tolerance checks ───────────────────────────────────────────
            // After authoring ClubEnd and ClubSlot, GripAnchor_Lead will be at gt_pos + gt_rot*(0.03*up)
            // where up = clubSlot local +Y in world space = shaftWorldDir * 0.03
            // But right now with the authored rot, GripAnchor_Lead should sit 0.03m up the shaft = 0.03 * shaftWorldDir from gt_pos
            Vector3 predictedLeadWorld  = gt_pos + shaftWorldDir * 0.03f;
            Vector3 predictedTrailWorld = gt_pos + shaftWorldDir * 0.11f;

            float distLeadToHandL  = Vector3.Distance(predictedLeadWorld, handL_noIK);
            float distTrailToHandR = Vector3.Distance(predictedTrailWorld, handR_noIK);
            // Current distances (before authored):
            float curDistLead  = Vector3.Distance(ga_lead,  handL_noIK);
            float curDistTrail = Vector3.Distance(ga_trail, handR_noIK);

            // Tolerance from §6: 0.035m (no explicit value; harness uses ≤ 0.035 heuristically)
            const float kTol = 0.035f;
            bool condA = distLeadToHandL  <= kTol;
            bool condB = distTrailToHandR <= kTol;

            // ── Restore Rig_Hands weight ──────────────────────────────────────
            if (rigHandsComp != null) rigHandsComp.weight = origWeight;

            // ── golfer-local ball pos ─────────────────────────────────────────
            Vector3 ballLocalToGolfer = golferRoot.transform.InverseTransformPoint(ballPos);

            // ── ClubEnd world with IK ON (needed for addressHeadLocal authoring) ──
            // ClubEnd local in GripTarget = (0, -0.80, 0) — fixed by GolferClubGripBuilder
            const float ClubEndLocalY = -0.80f;
            Vector3 clubEnd_IK_world = gt_ik_pos + gt_ik_rot * new Vector3(0f, ClubEndLocalY, 0f);
            Vector3 computed_addressHeadLocal = golferRoot.transform.InverseTransformPoint(clubEnd_IK_world);
            Vector3 golferRootRot_euler = golferRoot.transform.eulerAngles;

            // ── JSON result ───────────────────────────────────────────────────
            Func<Vector3, string> V3 = v =>
                "[" + v.x.ToString("F6") + "," + v.y.ToString("F6") + "," + v.z.ToString("F6") + "]";
            Func<Quaternion, string> Q4 = q =>
                "[" + q.x.ToString("F6") + "," + q.y.ToString("F6") + "," +
                      q.z.ToString("F6") + "," + q.w.ToString("F6") + "]";
            Func<Quaternion, string> Euler = quat =>
            {
                var e = quat.eulerAngles;
                return "[" + e.x.ToString("F2") + "," + e.y.ToString("F2") + "," + e.z.ToString("F2") + "]";
            };

            string json =
                "{\n" +
                "  \"ballWorldPos\": "              + V3(ballPos)                + ",\n" +
                "  \"ballLocalToGolfer\": "         + V3(ballLocalToGolfer)      + ",\n" +
                "  \"golferRootPos\": "             + V3(golferRoot.transform.position) + ",\n" +
                "  \"gripTarget_IK_pos\": "         + V3(gt_ik_pos)             + ",\n" +
                "  \"gripTarget_IK_rot_q\": "       + Q4(gt_ik_rot)             + ",\n" +
                "  \"gripTarget_IK_rot_euler\": "   + Euler(gt_ik_rot)          + ",\n" +
                "  \"handL_IK\": "                  + V3(handL_ik)              + ",\n" +
                "  \"handR_IK\": "                  + V3(handR_ik)              + ",\n" +
                "  \"gripAnchor_Lead_IK\": "        + V3(ga_lead_ik)            + ",\n" +
                "  \"gripAnchor_Trail_IK\": "       + V3(ga_trail_ik)           + ",\n" +
                "  \"gripTarget_noIK_pos\": "       + V3(gt_pos)                + ",\n" +
                "  \"gripTarget_noIK_rot_q\": "     + Q4(gt_rot)                + ",\n" +
                "  \"gripTarget_noIK_rot_euler\": " + Euler(gt_rot)             + ",\n" +
                "  \"handL_noIK\": "                + V3(handL_noIK)            + ",\n" +
                "  \"handR_noIK\": "                + V3(handR_noIK)            + ",\n" +
                "  \"gripAnchor_Lead_noIK\": "      + V3(ga_lead)               + ",\n" +
                "  \"gripAnchor_Trail_noIK\": "     + V3(ga_trail)              + ",\n" +
                "  \"clubStart_current\": "         + V3(cs_pos)                + ",\n" +
                "  \"clubEnd_current\": "           + V3(ce_pos)                + ",\n" +
                "  \"computed_clubEndLocalPos\": "  + V3(clubEndLocalPos)       + ",\n" +
                "  \"computed_clubSlotLocalRot_q\": " + Q4(clubSlotLocalRot)    + ",\n" +
                "  \"computed_clubSlotLocalRot_euler\": " + Euler(clubSlotLocalRot) + ",\n" +
                "  \"shaftWorldDir\": "             + V3(shaftWorldDir)          + ",\n" +
                "  \"predicted_Lead_world\": "      + V3(predictedLeadWorld)     + ",\n" +
                "  \"predicted_Trail_world\": "     + V3(predictedTrailWorld)    + ",\n" +
                "  \"dist_predictedLead_to_handL_noIK\": "  + distLeadToHandL.ToString("F4")  + ",\n" +
                "  \"dist_predictedTrail_to_handR_noIK\": " + distTrailToHandR.ToString("F4") + ",\n" +
                "  \"dist_currentLead_to_handL_noIK\": "    + curDistLead.ToString("F4")      + ",\n" +
                "  \"dist_currentTrail_to_handR_noIK\": "   + curDistTrail.ToString("F4")     + ",\n" +
                "  \"condA_predictedLead_within_35mm\": "   + condA.ToString().ToLower()       + ",\n" +
                "  \"condB_predictedTrail_within_35mm\": "  + condB.ToString().ToLower()       + ",\n" +
                "  \"golferRootRot_euler\": "               + V3(golferRootRot_euler)           + ",\n" +
                "  \"clubEnd_IK_world\": "                  + V3(clubEnd_IK_world)              + ",\n" +
                "  \"computed_addressHeadLocal\": "         + V3(computed_addressHeadLocal)     + "\n" +
                "}";

            string outPath = Path.Combine("Docs", "Specs", "Active", "golfer_club_grip", "grip_measure_result.json");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllText(outPath, json);
                Debug.Log("[GripMeasure] wrote " + outPath);
            }
            catch (Exception e)
            {
                Debug.LogError("[GripMeasure] failed to write JSON: " + e.Message);
            }

            Debug.Log("[GripMeasure] ===== RESULTS =====");
            Debug.Log("[GripMeasure] Ball world: " + ballPos);
            Debug.Log("[GripMeasure] Ball golfer-local: " + ballLocalToGolfer);
            Debug.Log("[GripMeasure] GripTarget pos (IK ON): " + gt_ik_pos);
            Debug.Log("[GripMeasure] GripTarget pos (IK OFF): " + gt_pos);
            Debug.Log("[GripMeasure] ClubEnd localPos (computed): " + clubEndLocalPos);
            Debug.Log("[GripMeasure] ClubSlot localRot euler (computed): " + clubSlotLocalRot.eulerAngles);
            Debug.Log("[GripMeasure] shaftWorldDir: " + shaftWorldDir);
            Debug.Log("[GripMeasure] condA (predicted Lead within 35mm of LeftHand): " + condA + " (" + (distLeadToHandL * 1000f).ToString("F1") + "mm)");
            Debug.Log("[GripMeasure] condB (predicted Trail within 35mm of RightHand): " + condB + " (" + (distTrailToHandR * 1000f).ToString("F1") + "mm)");
            Debug.Log("[GripMeasure] ==================");

            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log("[GripMeasure] exiting play mode");
            EditorApplication.isPlaying = false;
        }
    }
}
