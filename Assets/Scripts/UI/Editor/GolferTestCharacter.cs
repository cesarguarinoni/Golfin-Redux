// golfer_club_grip — which test character the hinge tools, the verification recorder and the tests
// operate on. One EditorPrefs switch; every path derives from it, so a second character
// (Olivia, 2026-09-15) runs the identical stage-0/1/2 pipeline with nothing hand-authored.
//
// Not gated: GolferTestVerificationRecorder (ungated) reads the resource path from here.

using UnityEditor;
using UnityEngine;

namespace Golfin.EditorTools.Golfer
{
    public static class GolferTestCharacter
    {
        const string Key = "Golfin.GolferTest.Character";
        /// <summary>Olivia since 2026-09-15 (Cesar: "retire Remy"), on the v2 mesh since 2026-09-16 (OLIVIA_RIG_HANDOFF.md §4);
        /// "Olivia" (the v1 mesh) and "MixamoNative" (Remy) stay selectable for comparison.</summary>
        public const string Default = "Olivia_v2";
        public const string Folder = "Assets/Art/3D/Characters/_Test/Resources/GolferTest";

        /// <summary>"MixamoNative" (Remy, the stand-in), "Olivia" (the first roster-likeness model, v1 mesh) or "Olivia_v2" (same rig, welded mesh + re-baked atlas).</summary>
        public static string Name
        {
            get => EditorPrefs.GetString(Key, Default);
            set => EditorPrefs.SetString(Key, value);
        }

        public static string PrefabPath   => Folder + "/PfGolfer_" + Name + ".prefab";
        public static string AssetPath    => Folder + "/HandHinge_" + Name + ".asset";
        public static string ResourcePath => "GolferTest/PfGolfer_" + Name;
        /// <summary>Remy keeps the historical evidence/stageN folders; every other character gets evidence/&lt;name&gt;/stageN.</summary>
        public static string EvidenceRoot => "Docs/Specs/Active/golfer_club_grip/evidence" + (Name == "MixamoNative" ? "" : "/" + Name.ToLowerInvariant());

        [MenuItem("GOLFIN/Golfer Test/Character/Use MixamoNative (Remy)")]
        static void UseRemy() { Name = "MixamoNative"; Debug.Log("[GolferTest] character = " + Name + " → " + PrefabPath); }

        [MenuItem("GOLFIN/Golfer Test/Character/Use Olivia")]
        static void UseOlivia() { Name = "Olivia"; Debug.Log("[GolferTest] character = " + Name + " → " + PrefabPath); }

        /// <summary>Olivia on the v2 mesh (OLIVIA_RIG_HANDOFF.md §4: same rig, welded mesh, re-baked atlas) — its own
        /// prefab / hinge asset / evidence/olivia_v2 so the v1 prefab stays untouched for the A/B.</summary>
        [MenuItem("GOLFIN/Golfer Test/Character/Use Olivia_v2")]
        static void UseOliviaV2() { Name = "Olivia_v2"; Debug.Log("[GolferTest] character = " + Name + " → " + PrefabPath); }

        [MenuItem("GOLFIN/Golfer Test/Character/Which character?")]
        static void Which() { Debug.Log("[GolferTest] character = " + Name + " → " + PrefabPath + " | " + AssetPath + " | " + EvidenceRoot); }
    }
}
