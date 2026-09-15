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
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;

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
                // the model instance sits at Remy's instance pose — his is a 180° yaw (Mixamo FBXs face −Z after the
                // axis bake; the presenter aims the ROOT). Identity here turned Olivia around: back to the ball, club
                // between her legs, "hands backwards" (Cesar, 2026-09-15). Copied, never assumed.
                var remyInst = remy.transform.Cast<Transform>().First(t => PrefabUtility.IsPartOfPrefabInstance(t.gameObject));
                inst.transform.localPosition = remyInst.localPosition; inst.transform.localRotation = remyInst.localRotation; inst.transform.localScale = Vector3.one;
                sb.AppendLine("model instance pose copied from Remy's: rot " + remyInst.localRotation.eulerAngles.ToString("F1"));

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

        // ── v2 — OLIVIA_RIG_HANDOFF.md §4 (2026-09-16): the SAME Mixamo rig (65 bones, rest matrices equal to v1
        // within 4e-6), a welded / winding-fixed mesh and a re-baked 2K atlas (269 islands where Meshy's had 11,938).
        // NOT a rebuild. PfGolfer_Olivia carries the approved driver + putter bakes (ClubSlot, anchors, wrist targets,
        // stance rig values, presenter putt/placement fields, face roll), so the v2 prefab is a COPY of it with only
        // the nested FBX instance swapped: the authored children (ClubRoot, GolferRig) move onto the new instance and
        // every object reference into the old instance (IK root/mid/tip, constraint sources, the presenter's skins…)
        // is re-bound to the same-named transform of the new one. The v1 prefab, asset and evidence stay untouched
        // for the A/B; HandHinge_Olivia_v2.asset + the HandHingeModel poses come from the stage-0 capture and
        // HandHingeStage2.AuthorPrefabStructureForAxes afterwards, exactly as for v1.
        const string OliviaV2Src    = OliviaSrc + "v2/";
        const string OliviaV2Mat    = "Assets/Art/3D/Characters/_Test/Olivia/Materials/M_Olivia_v2.mat";
        const string OliviaV1Prefab = GolferTestCharacter.Folder + "/PfGolfer_Olivia.prefab";
        const string OliviaV2Prefab = GolferTestCharacter.Folder + "/PfGolfer_Olivia_v2.prefab";

        [MenuItem("GOLFIN/Golfer Test/Character/Build PfGolfer_Olivia_v2 (v2 mesh swap: imports, material, prefab copy)")]
        public static void BuildOliviaV2Menu() => Debug.Log(BuildOliviaV2());

        public static string BuildOliviaV2()
        {
            if (!EditorUserBuildSettings.activeScriptCompilationDefines.Contains("GOLFIN_GOLFER_TEST"))
                throw new System.InvalidOperationException("define OFF — refusing to build a gated prefab");
            var sb = new StringBuilder();
            string tpose = OliviaV2Src + "Olivia_TPose_v2.fbx";
            string[] clips = { "ANIM_Golf_Drive", "ANIM_Golf_DriveSetup", "ANIM_Golf_Putt", "ANIM_Idle" };

            // 1. imports — the v2 T-pose exactly like v1's. The four clips STAY on v1's avatar: the handoff asks for
            //    Copy-From v2, but the Blender export wraps the skeleton in an extra "Armature" node the Mixamo clip
            //    files do not have, and Unity refuses the copy ("Rig Error: Copied Avatar Rig Configuration mis-match
            //    … 'ANIM_Golf_Drive' was found instead of 'Armature'") — the clips import with ZERO takes and the
            //    controller's states go null. v1's and v2's avatars describe one skeleton (65 bones, rest pose equal
            //    within 0.003 mm / 0°), so the Humanoid retarget v1-avatar-clip → v2 avatar is the identity;
            //    BuildOliviaV2 checks that below by sampling a clip frame on both prefabs.
            ConfigureModel(tpose, true, null, sb);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(tpose).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman) throw new System.InvalidOperationException("Olivia_TPose_v2.fbx did not produce a humanoid avatar");
            sb.AppendLine("avatar " + avatar.name + " isHuman=" + avatar.isHuman + " isValid=" + avatar.isValid);
            var v1Avatar = AssetDatabase.LoadAllAssetsAtPath(OliviaSrc + "Olivia_TPose.fbx").OfType<Avatar>().FirstOrDefault();
            foreach (var c in clips)
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(OliviaSrc + c + ".fbx");
                var clip = AssetDatabase.LoadAllAssetsAtPath(OliviaSrc + c + ".fbx").OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview"));
                if (mi.animationType != ModelImporterAnimationType.Human || mi.sourceAvatar != v1Avatar || clip == null || !clip.humanMotion)
                    throw new System.InvalidOperationException(c + ".fbx must stay Humanoid on v1's avatar with its take imported (type " + mi.animationType + ", avatar " + (mi.sourceAvatar ? mi.sourceAvatar.name : "null") + ", clip " + (clip ? clip.name : "null") + ")");
                sb.AppendLine("clip " + c + ": Humanoid, avatar " + mi.sourceAvatar.name + " (kept — see the Armature note), " + clip.length.ToString("F2") + " s");
            }

            // 2. textures + material. URP Lit reads metallic from R and smoothness from A of ONE texture ("Metallic
            //    Alpha"), so T_Olivia_Metallic (R) and T_Olivia_Roughness are packed into T_Olivia_MetallicSmoothness.png
            //    with A = 1 − roughness; the material's Smoothness stays 1 (a multiplier over the map).
            var tiN = (TextureImporter)AssetImporter.GetAtPath(OliviaV2Src + "T_Olivia_Normal.png");
            if (tiN.textureType != TextureImporterType.NormalMap) { tiN.textureType = TextureImporterType.NormalMap; tiN.SaveAndReimport(); }
            string packed = PackMetallicSmoothness(OliviaV2Src + "T_Olivia_Metallic.png", OliviaV2Src + "T_Olivia_Roughness.png", OliviaV2Src + "T_Olivia_MetallicSmoothness.png", sb);
            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(OliviaV2Src + "T_Olivia_BaseColor.png");
            var normTex = AssetDatabase.LoadAssetAtPath<Texture2D>(OliviaV2Src + "T_Olivia_Normal.png");
            var msTex   = AssetDatabase.LoadAssetAtPath<Texture2D>(packed);
            if (baseTex == null || normTex == null || msTex == null) throw new System.InvalidOperationException("v2 textures missing: base " + (baseTex != null) + " normal " + (normTex != null) + " metallicSmoothness " + (msTex != null));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(OliviaV2Mat);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, OliviaV2Mat); }
            mat.SetTexture("_BaseMap", baseTex);
            mat.SetTexture("_BumpMap", normTex); mat.SetFloat("_BumpScale", 1f); mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", msTex); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_SmoothnessTextureChannel", 0f); mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", 1f);
            EditorUtility.SetDirty(mat);
            sb.AppendLine("material " + OliviaV2Mat + ": base " + baseTex.name + " " + baseTex.width + "², normal " + normTex.name + " " + normTex.width + "² (" + tiN.textureType + "), metallic(R)+smoothness(A=1−roughness) " + msTex.name + " " + msTex.width + "², _Smoothness 1 (map-driven)");

            // 3. prefab — a copy of v1 with the nested FBX instance swapped
            var v1Model = AssetDatabase.LoadAssetAtPath<GameObject>(OliviaSrc + "Olivia_TPose.fbx");
            var v2Model = AssetDatabase.LoadAssetAtPath<GameObject>(tpose);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OliviaV2Prefab) != null) { AssetDatabase.DeleteAsset(OliviaV2Prefab); sb.AppendLine("existing " + OliviaV2Prefab + " deleted (rebuilt from v1)"); }
            if (!AssetDatabase.CopyAsset(OliviaV1Prefab, OliviaV2Prefab)) throw new System.InvalidOperationException("CopyAsset failed: " + OliviaV1Prefab + " → " + OliviaV2Prefab);
            var root = PrefabUtility.LoadPrefabContents(OliviaV2Prefab);
            try
            {
                root.name = "PfGolfer_Olivia_v2";
                var oldInst = root.transform.Cast<Transform>().Select(t => t.gameObject)
                    .FirstOrDefault(g => PrefabUtility.GetCorrespondingObjectFromOriginalSource(g) == v1Model);
                if (oldInst == null) throw new System.InvalidOperationException("no nested instance of Olivia_TPose.fbx under " + OliviaV1Prefab);
                sb.AppendLine(SwapModelInstance(root, oldInst, v2Model, mat, avatar, sb));
                PrefabUtility.SaveAsPrefabAsset(root, OliviaV2Prefab);
                sb.AppendLine("saved " + OliviaV2Prefab + " (root scale " + root.transform.localScale.ToString("F3") + ", body bones " + root.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("mixamorig:")) + ")");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            sb.Append(ClipParityV1V2());
            return sb.ToString();
        }

        /// <summary>
        /// The retarget check behind "clips stay on v1's avatar": every clip sampled at ¼, ½, ¾ of its length through
        /// a PlayableGraph on each prefab's root Animator (v1: clip avatar = character avatar; v2: v1-avatar clip →
        /// v2 avatar), all 65 bones compared root-relative. Throws above 1 mm / 0.1° — that would mean the two
        /// avatars do NOT describe one skeleton and the v2 prefab must not be used.
        /// </summary>
        [MenuItem("GOLFIN/Golfer Test/Character/Check clip retarget parity PfGolfer_Olivia vs _v2")]
        public static void ClipParityMenu() => Debug.Log(ClipParityV1V2());

        public static string ClipParityV1V2()
        {
            var sb = new StringBuilder();
            string[] clips = { "ANIM_Golf_Drive", "ANIM_Golf_DriveSetup", "ANIM_Golf_Putt", "ANIM_Idle" };
            var r1 = PrefabUtility.LoadPrefabContents(OliviaV1Prefab); var r2 = PrefabUtility.LoadPrefabContents(OliviaV2Prefab);
            try
            {
                foreach (var b in r1.GetComponentsInChildren<Behaviour>(true)) if (b is RigBuilder) b.enabled = false;
                foreach (var b in r2.GetComponentsInChildren<Behaviour>(true)) if (b is RigBuilder) b.enabled = false;
                float worstP = 0f, worstA = 0f; string worstAt = "";
                foreach (var c in clips)
                {
                    var clip = AssetDatabase.LoadAllAssetsAtPath(OliviaSrc + c + ".fbx").OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview"));
                    if (clip == null) throw new System.InvalidOperationException("no clip in " + c + ".fbx");
                    foreach (float f in new[] { 0.25f, 0.5f, 0.75f })
                    {
                        float t = clip.length * f;
                        var p1 = SampleBones(r1, clip, t); var p2 = SampleBones(r2, clip, t);
                        float maxP = 0f, maxA = 0f; string at = "";
                        foreach (var kv in p1)
                        {
                            if (!p2.TryGetValue(kv.Key, out var q)) throw new System.InvalidOperationException("bone missing on v2: " + kv.Key);
                            float dp = Vector3.Distance(kv.Value.pos, q.pos), da = Quaternion.Angle(kv.Value.rot, q.rot);
                            if (dp > maxP) { maxP = dp; at = kv.Key; }
                            if (da > maxA) maxA = da;
                        }
                        sb.AppendLine("parity " + c + " @" + t.ToString("F2") + " s: " + p1.Count + " bones, max Δpos " + (maxP * 1000f).ToString("F3") + " mm (" + at + "), max Δrot " + maxA.ToString("F4") + "°");
                        if (maxP > worstP) { worstP = maxP; worstAt = c + " @" + t.ToString("F2"); }
                        if (maxA > worstA) worstA = maxA;
                    }
                }
                sb.AppendLine("clip retarget parity v1 → v2: worst Δpos " + (worstP * 1000f).ToString("F3") + " mm (" + worstAt + "), worst Δrot " + worstA.ToString("F4") + "° — " + (worstP <= 0.001f && worstA <= 0.1f ? "IDENTITY (≤ 1 mm / 0.1°)" : "MISMATCH"));
                if (worstP > 0.001f || worstA > 0.1f) throw new System.InvalidOperationException("v1-avatar clips do not retarget onto the v2 avatar as the identity:\n" + sb);
            }
            finally { PrefabUtility.UnloadPrefabContents(r1); PrefabUtility.UnloadPrefabContents(r2); }
            return sb.ToString();
        }

        /// <summary>Evaluate <paramref name="clip"/> at <paramref name="time"/> on the root Animator (edit mode, PlayableGraph) and read every mixamorig bone root-relative.</summary>
        static System.Collections.Generic.Dictionary<string, (Vector3 pos, Quaternion rot)> SampleBones(GameObject root, AnimationClip clip, float time)
        {
            var an = root.GetComponent<Animator>();
            var graph = PlayableGraph.Create("clipParity");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "out", an);
                var cp = AnimationClipPlayable.Create(graph, clip);
                cp.SetApplyFootIK(false); cp.SetTime(time);
                output.SetSourcePlayable(cp);
                graph.Evaluate(0f);
                var d = new System.Collections.Generic.Dictionary<string, (Vector3, Quaternion)>();
                var inv = Quaternion.Inverse(root.transform.rotation);
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("mixamorig:")) d[t.name] = (root.transform.InverseTransformPoint(t.position), inv * t.rotation);
                return d;
            }
            finally { graph.Destroy(); }
        }

        /// <summary>
        /// Replace the nested model instance <paramref name="oldInst"/> under <paramref name="root"/> with an instance of
        /// <paramref name="newModel"/> at the same local pose and sibling index: the added children (ClubRoot, GolferRig)
        /// move over, every object reference from any component outside the old instance that points INTO it is
        /// re-bound to the same-named transform (or the same-typed component on it) of the new instance, the root
        /// Animator gets <paramref name="avatar"/>, the new skin gets <paramref name="mat"/>. Throws on an unmappable
        /// reference rather than saving a half-bound prefab.
        /// </summary>
        static string SwapModelInstance(GameObject root, GameObject oldInst, GameObject newModel, Material mat, Avatar avatar, StringBuilder sb)
        {
            var newInst = (GameObject)PrefabUtility.InstantiatePrefab(newModel, root.scene);   // into the prefab's own scene — never the open scene
            newInst.transform.SetParent(root.transform, false);
            newInst.transform.localPosition = oldInst.transform.localPosition; newInst.transform.localRotation = oldInst.transform.localRotation; newInst.transform.localScale = oldInst.transform.localScale;
            newInst.transform.SetSiblingIndex(oldInst.transform.GetSiblingIndex());
            sb.AppendLine("model instance " + oldInst.name + " → " + newInst.name + " at rot " + newInst.transform.localRotation.eulerAngles.ToString("F1") + " scale " + newInst.transform.localScale.ToString("F3"));

            // the authored children ride over (added GameObjects on the old instance; nothing of the FBX itself)
            foreach (var c in oldInst.transform.Cast<Transform>().ToArray())
            {
                if (!PrefabUtility.IsAddedGameObjectOverride(c.gameObject)) continue;
                c.SetParent(newInst.transform, false);
                sb.AppendLine("moved " + c.name + " (" + c.GetComponentsInChildren<Transform>(true).Length + " transforms) onto " + newInst.name);
            }

            // name → transform of the new instance (the mixamorig skeleton has unique names; the root and the skin are matched explicitly)
            var byName = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var t in newInst.GetComponentsInChildren<Transform>(true)) { if (t == newInst.transform || t.IsChildOf(oldInst.transform)) continue; if (byName.ContainsKey(t.name)) byName[t.name] = null; else byName[t.name] = t; }
            var oldSmr = oldInst.GetComponentInChildren<SkinnedMeshRenderer>(true); var newSmr = newInst.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (newSmr == null) throw new System.InvalidOperationException("no SkinnedMeshRenderer in " + newModel.name);
            Transform Map(Transform t)
            {
                if (t == oldInst.transform) return newInst.transform;
                if (oldSmr != null && t == oldSmr.transform) return newSmr.transform;
                return byName.TryGetValue(t.name, out var n) ? n : null;
            }

            int rebound = 0; var unmapped = new StringBuilder();
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null || comp.transform.IsChildOf(oldInst.transform)) continue;   // the old instance's own components go with it
                if (comp is Transform) continue;   // the hierarchy (m_Father / m_Children) is SetParent's business, never a serialized write
                var so = new SerializedObject(comp); var it = so.GetIterator(); bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    var o = it.objectReferenceValue;
                    Transform t = o as Transform ?? (o as GameObject)?.transform ?? (o as Component)?.transform;
                    if (t == null || !t.IsChildOf(oldInst.transform)) continue;
                    var nt = Map(t);
                    if (nt == null) { unmapped.AppendLine("  " + comp.name + ":" + comp.GetType().Name + "." + it.propertyPath + " → '" + t.name + "'"); continue; }
                    Object no = o is Transform ? nt : o is GameObject ? (Object)nt.gameObject : nt.GetComponent(o.GetType());
                    if (no == null) { unmapped.AppendLine("  " + comp.name + ":" + comp.GetType().Name + "." + it.propertyPath + " → '" + t.name + "' has no " + o.GetType().Name); continue; }
                    it.objectReferenceValue = no; changed = true; rebound++;
                    sb.AppendLine("rebound " + comp.name + ":" + comp.GetType().Name + "." + it.propertyPath + "  " + t.name + " → " + nt.name);
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (unmapped.Length > 0) throw new System.InvalidOperationException("references into the old model instance with no counterpart in " + newModel.name + ":\n" + unmapped);

            var an = root.GetComponent<Animator>(); an.avatar = avatar;
            var mats = newSmr.sharedMaterials; for (int i = 0; i < mats.Length; i++) mats[i] = mat; newSmr.sharedMaterials = mats;
            sb.AppendLine("renderer " + newSmr.name + ": " + mats.Length + " slot(s) → " + mat.name + ", bones " + newSmr.bones.Length + ", verts " + newSmr.sharedMesh.vertexCount + ", tris " + newSmr.sharedMesh.triangles.Length / 3);
            Object.DestroyImmediate(oldInst);
            return "rebound " + rebound + " reference(s); root Animator avatar → " + avatar.name;
        }

        /// <summary>R = metallic (the metallic map's R), G = B = R, A = 1 − roughness (the roughness map's R) → a linear PNG next to the sources.</summary>
        static string PackMetallicSmoothness(string metallicPath, string roughnessPath, string outPath, StringBuilder sb)
        {
            Texture2D met = new Texture2D(2, 2, TextureFormat.RGBA32, false), rough = new Texture2D(2, 2, TextureFormat.RGBA32, false), outTex = null;
            try
            {
                if (!met.LoadImage(File.ReadAllBytes(metallicPath)) || !rough.LoadImage(File.ReadAllBytes(roughnessPath))) throw new System.InvalidOperationException("could not decode " + metallicPath + " / " + roughnessPath);
                if (met.width != rough.width || met.height != rough.height) throw new System.InvalidOperationException("metallic " + met.width + "×" + met.height + " vs roughness " + rough.width + "×" + rough.height + " — sizes must match");
                var m = met.GetPixels32(); var r = rough.GetPixels32(); var o = new Color32[m.Length];
                long sumM = 0, sumS = 0;
                for (int i = 0; i < m.Length; i++) { byte s = (byte)(255 - r[i].r); o[i] = new Color32(m[i].r, m[i].r, m[i].r, s); sumM += m[i].r; sumS += s; }
                outTex = new Texture2D(met.width, met.height, TextureFormat.RGBA32, false); outTex.SetPixels32(o); outTex.Apply();
                File.WriteAllBytes(outPath, outTex.EncodeToPNG());
                AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);
                var ti = (TextureImporter)AssetImporter.GetAtPath(outPath);
                bool dirty = false;
                if (ti.sRGBTexture) { ti.sRGBTexture = false; dirty = true; }
                if (ti.alphaSource != TextureImporterAlphaSource.FromInput) { ti.alphaSource = TextureImporterAlphaSource.FromInput; dirty = true; }
                if (ti.alphaIsTransparency) { ti.alphaIsTransparency = false; dirty = true; }
                if (dirty) ti.SaveAndReimport();
                sb.AppendLine("packed " + outPath + " " + met.width + "×" + met.height + ": mean metallic " + (sumM / (255f * m.Length)).ToString("F3") + ", mean smoothness " + (sumS / (255f * m.Length)).ToString("F3") + " (linear, alpha from input)");
                return outPath;
            }
            finally { Object.DestroyImmediate(met); Object.DestroyImmediate(rough); if (outTex != null) Object.DestroyImmediate(outTex); }
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
