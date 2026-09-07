// golfer_3d_test §5.6 / §5.7 — the opt-in gate for the stand-in golfer.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Keeps <c>Assets/Art/3D/Characters/_Test/</c> out of every build that did not explicitly
    /// ask for it, and puts it back afterwards.
    ///
    /// <para>WHY A MOVE AND NOT A REFERENCE CHECK. The golfer prefab lives under a
    /// <c>Resources/</c> subfolder so <c>GolferTestBootstrap</c> can load it by name — and
    /// anything under a Resources folder ships in EVERY build whether or not a scene references
    /// it. Compiling <c>GolferPresenter</c> out is therefore necessary but not sufficient: the
    /// prefab, the FBX, the 11 clips, the controller and the textures would all still be in the
    /// player. They have to leave the tree, exactly as
    /// <see cref="StandaloneBuildPreprocessor.MoveGolfResourcesOut"/> moves the golf Resources
    /// out of the PLAYLIFE shell — the same AssetDatabase.MoveAsset rename (GUID-preserving, so
    /// it is a rename and not a re-import), the same on-disk sentinel, the same
    /// <c>[InitializeOnLoadMethod]</c> repair, the same idempotent RestoreNow.</para>
    ///
    /// <para>WHY <see cref="BuildPlayerProcessor"/> AND NOT <c>OnPreprocessBuild</c>. The
    /// preprocess hook runs INSIDE the build, by which point the set of included assets is
    /// already decided and an AssetDatabase.Refresh there would be reentrant —
    /// StandaloneBuildPreprocessor's own comment says so, which is why it moves from CIBuild
    /// instead. <see cref="PrepareForBuild"/> runs BEFORE the pipeline starts collecting, and
    /// unlike the CIBuild call site it also covers the menu-bar Build / Build&amp;Run that no
    /// lane goes through. Restore is <see cref="IPostprocessBuildWithReport"/> plus
    /// <see cref="RestoreNow"/> for the batchmode exits that never get another frame.</para>
    ///
    /// <para>HOW IT KNOWS. NOT <c>#if GOLFIN_GOLFER_TEST</c>: a build profile's scripting
    /// defines reach the PLAYER assemblies only, never the editor's own compilation — the exact
    /// reason <c>StandaloneBuildPreprocessor.ForceStandaloneIdentity</c> exists. It reads
    /// <see cref="IncludeTestAssets"/>, which <see cref="CIBuild.BuildIOSGolferTest"/> sets in a
    /// try/finally, and falls back to the ACTIVE profile's <c>m_ScriptingDefines</c> so that
    /// working in the Editor with iOS-Full-Golfer active behaves the same way.</para>
    /// </summary>
    public sealed class GolferTestBuildGate : BuildPlayerProcessor, IPostprocessBuildWithReport
    {
        const string Tag       = "[GolferGate]";
        public const string Define = "GOLFIN_GOLFER_TEST";

        // WHAT MOVES IS THE Resources SUBFOLDER, NOT ALL OF _Test.
        //   Unity's inclusion rule is literally "the path contains a /Resources/ segment", so
        //   moving Assets/Art/3D/Characters/_Test to Assets/<anything>/_Test would carry
        //   .../_Test/Resources/GolferTest/ along with it and change nothing — the stash would
        //   still be a Resources folder and the whole experiment would still ship. Verified: after
        //   such a move Resources.Load("GolferTest/PfGolfer_Test") still resolved.
        //   Moving the Resources folder itself to a destination with no Resources segment is what
        //   actually removes it from the build, and it is enough: outside a Resources folder an
        //   asset ships only if something included references it, and the ONLY reference chain
        //   into _Test starts at this prefab. GameplayScene holds none (SPEC §5.5).
        //   The SPEC §5.6 alternative — renaming _Test to _Test~ — also works, but a ~ folder is
        //   outside the AssetDatabase, so it needs Directory.Move plus a full re-import of the
        //   FBX set on every restore instead of a GUID-preserving rename.
        const string TestFolder   = "Assets/Art/3D/Characters/_Test";
        const string ResFolder    = "Assets/Art/3D/Characters/_Test/Resources";
        const string StashFolder  = "Assets/_GolferTestStash/GolferTestRes";
        const string StashRoot    = "Assets/_GolferTestStash";
        /// <summary>On-disk record that the folder is stashed, for the same reason
        /// StandaloneBuildPreprocessor keeps one: the mess is on disk, so the note belongs on
        /// disk, readable after a deleted Library folder.</summary>
        const string SentinelPath = "Assets/_GolferTestStash/.golfer_test_moved";

        /// <summary>Batchmode override set by <see cref="CIBuild.BuildIOSGolferTest"/>.</summary>
        internal static bool IncludeTestAssets;

        static bool _moved;

        // After StandaloneBuildPreprocessor (10), which owns identity and the golf Resources.
        public override int callbackOrder => 20;
        int IOrderedCallback.callbackOrder => 20;

        // ── The question ─────────────────────────────────────────────────────────────

        /// <summary>True when THIS build is meant to carry the golfer.</summary>
        public static bool IsGolferBuild()
        {
            if (IncludeTestAssets) return true;
            var profile = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
            return StandaloneBuildPreprocessor.ProfileDefines(profile)
                   .Any(d => string.Equals(d, Define, StringComparison.Ordinal));
        }

        // ── Pre-build ────────────────────────────────────────────────────────────────

        public override void PrepareForBuild(BuildPlayerContext context)
        {
            // Repair first: a build that died between the move and its finally left the folder
            // stashed, and a golfer build would then silently ship WITHOUT the golfer.
            RestoreNow();

            if (IsGolferBuild())
            {
                Debug.Log($"{Tag} INCLUDED — this build carries {TestFolder} (define {Define}).");
                return;
            }
            MoveTestAssetsOut();
        }

        internal static void MoveTestAssetsOut()
        {
            if (_moved) return;
            if (!AssetDatabase.IsValidFolder(ResFolder))
            {
                Debug.Log($"{Tag} {ResFolder} not present — nothing to exclude.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(StashRoot))
                AssetDatabase.CreateFolder("Assets", StashRoot.Substring("Assets/".Length));

            string error = AssetDatabase.MoveAsset(ResFolder, StashFolder);
            if (!string.IsNullOrEmpty(error))
            {
                // A folder that will not move must abort the build loudly: continuing would ship
                // the whole experiment inside an ordinary release while the log said it did not.
                throw new BuildFailedException(
                    $"{Tag} could not move {ResFolder} → {StashFolder}: {error}. Build aborted rather " +
                    $"than ship the golfer test assets in a build that did not ask for them.");
            }

            _moved = true;
            WriteSentinel();
            AssetDatabase.Refresh();
            Debug.Log($"{Tag} EXCLUDED — {ResFolder} stashed at {StashFolder} for this build " +
                      $"(no {Define}). It is restored when the build ends.");
        }

        // ── Post-build ───────────────────────────────────────────────────────────────

        public void OnPostprocessBuild(BuildReport report)
        {
            WriteGateReport(report);
            RestoreNow();
        }

        /// <summary>
        /// Idempotent restore. Safe to call when nothing moved, which is why every build entry
        /// point can call it unconditionally — including <see cref="CIBuild"/> immediately before
        /// it exits the process, since a delayCall never gets a frame in batchmode.
        /// </summary>
        public static void RestoreNow()
        {
            bool sentinel = File.Exists(Path.GetFullPath(SentinelPath));
            if (!_moved && !sentinel && !AssetDatabase.IsValidFolder(StashFolder)) return;

            if (AssetDatabase.IsValidFolder(StashFolder))
            {
                if (!AssetDatabase.IsValidFolder(TestFolder))
                {
                    Debug.LogError($"{Tag} {TestFolder} is gone — cannot restore {StashFolder} into it. " +
                                   $"The stash still holds the golfer prefab; move it back by hand.");
                    return;
                }
                string error = AssetDatabase.MoveAsset(StashFolder, ResFolder);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"{Tag} COULD NOT RESTORE {StashFolder} → {ResFolder}: {error}. " +
                                   $"Move it back by hand before the next golfer build.");
                    return;
                }
                Debug.Log($"{Tag} restored {ResFolder}.");
            }

            DeleteSentinel();
            if (AssetDatabase.IsValidFolder(StashRoot) &&
                Directory.GetFileSystemEntries(Path.GetFullPath(StashRoot))
                         .All(p => p.EndsWith(".meta", StringComparison.Ordinal)))
                AssetDatabase.DeleteAsset(StashRoot);

            _moved = false;
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Repair on editor load — an aborted batchmode build (a crash, a kill, an Exit before
        /// the finally) leaves the folder stashed, and the editor coming up is the last chance
        /// to notice before someone builds the golfer lane and gets no golfer.
        /// </summary>
        [InitializeOnLoadMethod]
        static void RepairStashOnLoad()
        {
            if (!File.Exists(Path.GetFullPath(SentinelPath)) && !AssetDatabase.IsValidFolder(StashRoot)) return;
            Debug.LogWarning($"{Tag} a previous build left {ResFolder} stashed — restoring it now.");
            RestoreNow();
        }

        // ── The §6 gate evidence ─────────────────────────────────────────────────────

        /// <summary>
        /// Writes <c>Builds/golfer-gate-report.txt</c>: whether the folder was stashed, and how
        /// many <c>_Test/</c> paths the BUILD REPORT itself lists. SPEC §6 asks for exactly this
        /// pair of numbers from a build with and without the define, so the build produces the
        /// proof rather than a human re-deriving it. Never fails the build.
        /// </summary>
        static void WriteGateReport(BuildReport report)
        {
            try
            {
                var lines = new List<string>
                {
                    "golfer_3d_test gate report",
                    "when      : " + DateTime.Now.ToString("u"),
                    "profile   : " + (UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile()?.name ?? "<none>"),
                    "define    : " + Define + (IsGolferBuild() ? " PRESENT" : " ABSENT"),
                    "decision  : " + (IsGolferBuild() ? "INCLUDE _Test" : "EXCLUDE _Test (folder stashed for the build)"),
                    "result    : " + report.summary.result,
                };

                var hits = new List<string>();
                foreach (var pa in report.packedAssets)
                    foreach (var c in pa.contents)
                        if (c.sourceAssetPath != null &&
                            c.sourceAssetPath.IndexOf("Characters/_Test", StringComparison.OrdinalIgnoreCase) >= 0)
                            hits.Add(c.sourceAssetPath);

                lines.Add("_Test paths in the build report: " + hits.Count);
                lines.AddRange(hits.Distinct().OrderBy(x => x).Take(50).Select(h => "    " + h));

                Directory.CreateDirectory("Builds");
                File.WriteAllLines("Builds/golfer-gate-report.txt", lines);
                Debug.Log($"{Tag} wrote Builds/golfer-gate-report.txt — _Test paths in build: {hits.Count}.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} could not write the gate report ({e.Message}) — it is evidence, not a gate.");
            }
        }

        static void WriteSentinel()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(SentinelPath)) ?? ".");
                File.WriteAllText(Path.GetFullPath(SentinelPath), ResFolder + Environment.NewLine);
            }
            catch (Exception e) { Debug.LogError($"{Tag} could not write the stash sentinel: {e.Message}"); }
        }

        static void DeleteSentinel()
        {
            try
            {
                string full = Path.GetFullPath(SentinelPath);
                if (File.Exists(full)) File.Delete(full);
            }
            catch (Exception e) { Debug.LogWarning($"{Tag} could not delete the sentinel: {e.Message}"); }
        }
    }
}
