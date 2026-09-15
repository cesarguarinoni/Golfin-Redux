// golfer_club_grip — build a second test character from a Mixamo-native rig, the way
// PfGolfer_MixamoNative was built, with nothing hand-authored (OLIVIA_RIG_HANDOFF.md §3):
//   1. import the T-pose FBX as Humanoid (Create From This Model, useFileScale, no compensating
//      scale) and the clips as Humanoid with Avatar → Copy From Other Avatar = the character's,
//      the golfer_3d_test §5.1 clip settings (root orientation / height / XZ kept, no loop);
//   2. one URP Lit material from the Meshy base + normal textures;
//   3. the golfer controller copied with every state's motion swapped for the character's own
//      clip of the same name (clip avatar = character avatar — Unity retargets nothing, §9.8);
//   4. the prefab: root {Animator, GolferPresenter (Remy's values), RigBuilder} at scale 1, the
//      FBX as a nested instance, Remy's ClubRoot subtree copied and unscaled (root scale 1 →
//      clubs at native scale), GolferRig/Rig_Grip/GripTarget_Constraint on the character's hands.
//   HandHingeModel + HandHinge_<name>.asset + anchors + Rig_Hands/Rig_Stance come from the
//   stage-0 capture and HandHingeStage2.AuthorPrefabStructure, exactly as for Remy.

#if GOLFIN_GOLFER_TEST
using System.IO;
using System.Linq;
using System.Text;
using Golfin.Gameplay.Golfer;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Golfin.EditorTools.Golfer
{
    public static class GolferTestCharacterBuilder
    {
        const string OliviaSrc  = "Assets/Art/3D/Characters/_Test/Olivia/MixamoNative/";
        const string OliviaTex  = "Assets/Art/3D/Characters/_Test/Olivia/meshy_run2_60k/Meshy_AI_Green_Fairway_Mascot_0915034355_texture";
        const string OliviaMat  = "Assets/Art/3D/Characters/_Test/Olivia/Materials/M_Olivia.mat";
        const string CtrlRemy   = "Assets/Animations/Golfer/AnimatorController_Golfer_MixamoNative.controller";
        const string RemyPrefab = GolferTestCharacter.Folder + "/PfGolfer_MixamoNative.prefab";

        [MenuItem("GOLFIN/Golfer Test/Character/Build PfGolfer_Olivia (imports, material, controller, prefab)")]
        public static void BuildOliviaMenu() => Debug.Log(BuildOlivia());

        public static string BuildOlivia()
        {
            if (!EditorUserBuildSettings.activeScriptCompilationDefines.Contains("GOLFIN_GOLFER_TEST"))
                throw new System.InvalidOperationException("define OFF — refusing to build a gated prefab");
            var sb = new StringBuilder();
            string tpose = OliviaSrc + "Olivia_TPose.fbx";
            string[] clips = { "ANIM_Golf_Drive", "ANIM_Golf_DriveSetup", "ANIM_Golf_Putt", "ANIM_Idle" };

            // 1. imports
            ConfigureModel(tpose, true, null, sb);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(tpose).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman) throw new System.InvalidOperationException("Olivia_TPose.fbx did not produce a humanoid avatar");
            sb.AppendLine("avatar " + avatar.name + " isHuman=" + avatar.isHuman + " isValid=" + avatar.isValid);
            foreach (var c in clips) ConfigureModel(OliviaSrc + c + ".fbx", false, avatar, sb);

            // 2. textures + material
            var tiN = (TextureImporter)AssetImporter.GetAtPath(OliviaTex + "_normal.png");
            if (tiN.textureType != TextureImporterType.NormalMap) { tiN.textureType = TextureImporterType.NormalMap; tiN.SaveAndReimport(); }
            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(OliviaTex + ".png");
            var normTex = AssetDatabase.LoadAssetAtPath<Texture2D>(OliviaTex + "_normal.png");
            Directory.CreateDirectory(Path.GetDirectoryName(OliviaMat));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(OliviaMat);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, OliviaMat); }
            mat.SetTexture("_BaseMap", baseTex); mat.SetTexture("_BumpMap", normTex); mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(mat);
            sb.AppendLine("material " + OliviaMat + ": base " + (baseTex ? baseTex.name : "null") + ", normal " + (normTex ? normTex.name : "null") + " — Meshy's _metallic_roughness.png is glTF-packed (G roughness / B metallic), URP Lit wants metallic in R / smoothness in A: left out, metallic 0, smoothness 0.35 (texture cleanup is out of scope)");

            // 3. controller
            string ctrlDst = CtrlRemy.Replace("_MixamoNative", "_Olivia");
            if (AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlDst) == null) AssetDatabase.CopyAsset(CtrlRemy, ctrlDst);
            var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlDst);
            foreach (var layer in ctrl.layers)
                foreach (var st in layer.stateMachine.states)
                {
                    if (st.state.motion == null) continue;
                    string clipName = st.state.motion.name;
                    var clip = AssetDatabase.LoadAllAssetsAtPath(OliviaSrc + clipName + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
                    if (clip == null) throw new System.InvalidOperationException("no clip " + clipName + " in Olivia's " + clipName + ".fbx");
                    st.state.motion = clip;
                    sb.AppendLine("controller state " + st.state.name + " → " + AssetDatabase.GetAssetPath(clip) + " (" + clip.length.ToString("F2") + " s, humanMotion=" + clip.humanMotion + ")");
                }
            EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets();

            // 4. prefab
            string dst = GolferTestCharacter.Folder + "/PfGolfer_Olivia.prefab";
            var remy = PrefabUtility.LoadPrefabContents(RemyPrefab);
            var root = new GameObject("PfGolfer_Olivia");
            try
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(tpose);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.transform.SetParent(root.transform, false);
                inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity; inst.transform.localScale = Vector3.one;

                var remyAnim = remy.GetComponent<Animator>();
                var an = root.AddComponent<Animator>();
                an.avatar = avatar; an.runtimeAnimatorController = ctrl;
                an.applyRootMotion = remyAnim.applyRootMotion; an.updateMode = remyAnim.updateMode; an.cullingMode = remyAnim.cullingMode;

                var pres = root.AddComponent<GolferPresenter>();
                ComponentUtility.CopyComponent(remy.GetComponent<GolferPresenter>());
                ComponentUtility.PasteComponentValues(pres);
                root.AddComponent<RigBuilder>();

                // ClubRoot from Remy, unscaled: Remy's root is 1.05156 and its clubs 0.95097 so the club is native;
                // Olivia's root is 1, so every node between ClubRoot and the clubs gets ×k and the clubs go to 1.
                float k = remy.transform.localScale.x;
                var remyClubRoot = remy.GetComponentsInChildren<Transform>(true).First(t => t.name == "ClubRoot");
                var cr = Object.Instantiate(remyClubRoot.gameObject); cr.name = "ClubRoot";
                cr.transform.SetParent(inst.transform, false);
                cr.transform.localPosition = remyClubRoot.localPosition * k; cr.transform.localRotation = remyClubRoot.localRotation; cr.transform.localScale = Vector3.one;
                foreach (var t in cr.GetComponentsInChildren<Transform>(true))
                {
                    if (t == cr.transform) continue;
                    bool insideClub = false; for (var p = t.parent; p != null && p != cr.transform; p = p.parent) if (p.name.StartsWith("GOLFIN_")) { insideClub = true; break; }
                    if (insideClub) continue;
                    if (t.name.StartsWith("GOLFIN_")) { t.localScale = Vector3.one; t.localPosition *= k; continue; }
                    t.localPosition *= k;
                }
                sb.AppendLine("ClubRoot copied from Remy, ×" + k.ToString("F5") + " on the slot chain, clubs at scale 1");

                var golferRig = new GameObject("GolferRig"); golferRig.transform.SetParent(inst.transform, false);
                var rigGripGo = new GameObject("Rig_Grip"); rigGripGo.transform.SetParent(golferRig.transform, false);
                var rigGrip = rigGripGo.AddComponent<Rig>(); rigGrip.weight = 1f;
                var gtcGo = new GameObject("GripTarget_Constraint"); gtcGo.transform.SetParent(rigGripGo.transform, false);
                var gtc = gtcGo.AddComponent<MultiParentConstraint>(); gtc.weight = 1f;
                var remyGtc = remy.GetComponentsInChildren<MultiParentConstraint>(true).First(c => c.name == "GripTarget_Constraint");
                var d = remyGtc.data;
                d.constrainedObject = cr.GetComponentsInChildren<Transform>(true).First(t => t.name == "GripTarget");
                var arr = new WeightedTransformArray();
                arr.Add(new WeightedTransform(an.GetBoneTransform(HumanBodyBones.LeftHand), 0.5f));
                arr.Add(new WeightedTransform(an.GetBoneTransform(HumanBodyBones.RightHand), 0.5f));
                d.sourceObjects = arr; d.maintainPositionOffset = false; d.maintainRotationOffset = false;
                gtc.data = d;
                sb.AppendLine("GripTarget_Constraint: " + d.constrainedObject.name + " ← " + arr[0].transform.name + " / " + arr[1].transform.name);

                foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mats = smr.sharedMaterials; for (int i = 0; i < mats.Length; i++) mats[i] = mat; smr.sharedMaterials = mats;
                    sb.AppendLine("renderer " + smr.name + ": " + mats.Length + " slot(s) → M_Olivia, bones " + smr.bones.Length + ", verts " + (smr.sharedMesh ? smr.sharedMesh.vertexCount : 0));
                }

                PrefabUtility.SaveAsPrefabAsset(root, dst);
                sb.AppendLine("saved " + dst + " (root scale " + root.transform.localScale.ToString("F3") + ", body bones " + inst.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("mixamorig:")) + ")");
            }
            finally { Object.DestroyImmediate(root); PrefabUtility.UnloadPrefabContents(remy); }
            return sb.ToString();
        }

        static void ConfigureModel(string path, bool isTPose, Avatar avatar, StringBuilder sb)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) throw new System.InvalidOperationException("not a model: " + path);
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = isTPose ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.CopyFromOther;
            if (!isTPose) mi.sourceAvatar = avatar;
            mi.useFileScale = true; mi.globalScale = 1f; mi.bakeAxisConversion = true;
            mi.importAnimation = !isTPose;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importBlendShapes = false;
            mi.resampleCurves = true;
            mi.animationCompression = isTPose ? ModelImporterAnimationCompression.KeyframeReduction : ModelImporterAnimationCompression.Optimal;
            if (!isTPose)
            {
                var clipsIn = mi.defaultClipAnimations;
                string name = Path.GetFileNameWithoutExtension(path);
                for (int i = 0; i < clipsIn.Length; i++)
                {
                    clipsIn[i].name = clipsIn.Length == 1 ? name : name + "_" + i;
                    clipsIn[i].loopTime = false; clipsIn[i].loop = false;
                    clipsIn[i].lockRootRotation = true; clipsIn[i].lockRootHeightY = true; clipsIn[i].lockRootPositionXZ = true;
                    clipsIn[i].keepOriginalOrientation = true; clipsIn[i].keepOriginalPositionY = true; clipsIn[i].keepOriginalPositionXZ = true;
                    clipsIn[i].heightFromFeet = false;
                }
                mi.clipAnimations = clipsIn;
            }
            mi.SaveAndReimport();
            var clipsOut = isTPose ? new ModelImporterClipAnimation[0] : mi.clipAnimations;
            sb.AppendLine("import " + Path.GetFileName(path) + ": Human, " + mi.avatarSetup + ", useFileScale " + mi.useFileScale + " × global " + mi.globalScale + ", anim " + mi.importAnimation + (clipsOut.Length > 0 ? ", clip " + clipsOut[0].name + " frames " + clipsOut[0].firstFrame + "–" + clipsOut[0].lastFrame : ""));
        }
    }
}
#endif
