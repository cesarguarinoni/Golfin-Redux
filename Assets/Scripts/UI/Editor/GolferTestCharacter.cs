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
        public const string Default = "MixamoNative";
        public const string Folder = "Assets/Art/3D/Characters/_Test/Resources/GolferTest";

        /// <summary>"MixamoNative" (Remy, the stand-in) or "Olivia" (the first roster-likeness model).</summary>
        public static string Name
        {
            get => EditorPrefs.GetString(Key, Default);
            set => EditorPrefs.SetString(Key, value);
        }

        public static string PrefabPath   => Folder + "/PfGolfer_" + Name + ".prefab";
        public static string AssetPath    => Folder + "/HandHinge_" + Name + ".asset";
        public static string ResourcePath => "GolferTest/PfGolfer_" + Name;
        /// <summary>Remy keeps the historical evidence/stageN folders; every other character gets evidence/&lt;name&gt;/stageN.</summary>
        public static string EvidenceRoot => "Docs/Specs/Active/golfer_club_grip/evidence" + (Name == Default ? "" : "/" + Name.ToLowerInvariant());

        [MenuItem("GOLFIN/Golfer Test/Character/Use MixamoNative (Remy)")]
        static void UseRemy() { Name = Default; Debug.Log("[GolferTest] character = " + Name + " → " + PrefabPath); }

        [MenuItem("GOLFIN/Golfer Test/Character/Use Olivia")]
        static void UseOlivia() { Name = "Olivia"; Debug.Log("[GolferTest] character = " + Name + " → " + PrefabPath); }

        [MenuItem("GOLFIN/Golfer Test/Character/Which character?")]
        static void Which() { Debug.Log("[GolferTest] character = " + Name + " → " + PrefabPath + " | " + AssetPath + " | " + EvidenceRoot); }
    }
}
