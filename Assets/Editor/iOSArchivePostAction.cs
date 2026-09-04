// Xcode Archive post-action injection — feeds the App Store upload regression guard.
//
// BuildStampGenerator.OnPreprocessBuild refuses any store-bound build whose computed build
// number (git rev-list --count HEAD) is <= Docs/Versioning/last_uploaded_build.txt. That file
// used to be written only by GOLFIN/Build/Mark Current Commit As Uploaded — a menu item a human
// had to remember. Nobody ran it after the 2026-08-17 upload of 1.5.7 (2192), so the file sat at
// 0 and the guard was inert from the day it was written.
//
// This injects an Archive post-action into the generated .xcscheme that runs
// Tools/mark-uploaded.sh, so the guard advances whether or not anyone remembers. It has to be
// written by Unity — exactly like the Info.plist key in iOSPostProcess.cs — because Unity
// regenerates Unity-iPhone.xcodeproj, schemes included, on every Replace build, destroying
// anything added through Xcode's Edit Scheme UI.
//
// KNOWN TRADE-OFF: the post-action fires on ARCHIVE, not on a successful upload. Archiving then
// discarding the archive still advances the guard. Deliberate — Xcode exposes no upload-success
// hook, and over-strict is the safe direction: the build number is git-derived, so any further
// commit clears the guard again. Do not try to detect real upload success.
//
// NEVER FAILS THE BUILD: a missing scheme or malformed XML logs a warning and returns. A build
// that dies over its own bookkeeping is worse than the bookkeeping being missed.
//
// See Docs/TESTFLIGHT_RUNBOOK.md Phase 3, and Docs/Specs/Active/upload_guard_automation/SPEC.md.
#if UNITY_IOS
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Golfin.EditorTools
{
    public static class iOSArchivePostAction
    {
        // Identifies OUR post-action so a rebuild replaces it instead of stacking a second copy.
        const string ActionTitle = "Mark commit as uploaded";

        // VERIFIED against three real Unity 6000.3.9f1 iOS builds on 2026-08-18
        // (Builds/iOS-Full, Builds/iOS-Dev, Builds/iOS-Demo) — all three emit the scheme here.
        // Unity has moved schemes between xcshareddata and xcuserdata across versions, so if this
        // ever comes up missing the warning below is the signal, not a silent no-op.
        const string SchemeRel = "Unity-iPhone.xcodeproj/xcshareddata/xcschemes/Unity-iPhone.xcscheme";

        const string ScriptRel = "Tools/mark-uploaded.sh";
        const string Tag = "[Build]";

        // 1001: after iOSPostProcess (1000). Independent of it — this file deliberately does not
        // touch Info.plist and that file deliberately does not touch the scheme.
        [PostProcessBuild(1001)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            string schemePath = Path.Combine(pathToBuiltProject, SchemeRel);
            if (!File.Exists(schemePath))
            {
                Debug.LogWarning($"{Tag} Xcode scheme not found at {schemePath} — upload-guard archive " +
                                 $"post-action NOT injected. Run {ScriptRel} by hand after uploading, or use " +
                                 $"GOLFIN/Build/Mark Current Commit As Uploaded.");
                return;
            }

            try
            {
                string repoExpr = RepoRootExpression(pathToBuiltProject);
                InjectArchivePostAction(schemePath, repoExpr);
                Debug.Log($"{Tag} injected \"{ActionTitle}\" archive post-action into {SchemeRel} " +
                          $"(repo root resolves to {repoExpr}). Product → Archive now advances " +
                          $"Docs/Versioning/last_uploaded_build.txt.");
            }
            catch (Exception e)
            {
                // Bookkeeping must never take the build down with it.
                Debug.LogWarning($"{Tag} failed to inject the upload-guard archive post-action into " +
                                 $"{schemePath}: {e.Message}. Build continues; mark uploads manually.");
            }
        }

        static void InjectArchivePostAction(string schemePath, string repoExpr)
        {
            var doc = XDocument.Load(schemePath);
            var root = doc.Root;
            if (root == null) throw new InvalidDataException("scheme has no root element");

            var archive = root.Element("ArchiveAction");
            if (archive == null) throw new InvalidDataException("scheme has no <ArchiveAction>");

            var postActions = archive.Element("PostActions");
            if (postActions == null)
            {
                postActions = new XElement("PostActions");
                archive.AddFirst(postActions);
            }
            else
            {
                // Idempotent: drop any previous copy of OURS, leave anyone else's alone.
                postActions.Elements("ExecutionAction")
                           .Where(ea => (string)ea.Element("ActionContent")?.Attribute("title") == ActionTitle)
                           .ToList()
                           .ForEach(ea => ea.Remove());
            }

            // gps_standalone_shell §D8 — the guard is per App Store record, so the post-action
            // has to name the record it is archiving. Resolved at BUILD time (the variant is
            // known here) rather than left to the script's default, which would advance the
            // GAME's guard on a PLAYLIFE archive and refuse the next game upload for nothing.
            string record = StandaloneBuildPreprocessor.IsStandaloneIdentityBuild() ? "standalone" : "game";
            string scriptText = $"\"{repoExpr}/{ScriptRel}\" \"{repoExpr}\" {record}\n";

            var actionContent = new XElement("ActionContent",
                new XAttribute("title", ActionTitle),
                new XAttribute("scriptText", scriptText));

            // Without an <EnvironmentBuildable> Xcode's "Provide build settings from" is unset,
            // $PROJECT_DIR expands to empty, and the script path collapses to "/Tools/..." — the
            // post-action then silently does nothing. This is the load-bearing part.
            var buildable = FindPrimaryBuildableReference(root);
            if (buildable != null)
                actionContent.Add(new XElement("EnvironmentBuildable", buildable));
            else
                Debug.LogWarning($"{Tag} no primary <BuildableReference> found in the scheme — " +
                                 $"$PROJECT_DIR may be empty and the post-action may not resolve.");

            postActions.Add(new XElement("ExecutionAction",
                new XAttribute("ActionType",
                    "Xcode.IDEStandardExecutionActionsCore.ExecutionActionType.ShellScriptAction"),
                actionContent));

            doc.Save(schemePath); // XDocument.Save preserves the <?xml ...?> declaration.
        }

        /// <summary>
        /// Clone the scheme's own primary BuildableReference (the Unity-iPhone target) rather than
        /// hardcoding its BlueprintIdentifier, which Unity is free to change.
        /// </summary>
        static XElement FindPrimaryBuildableReference(XElement root)
        {
            var refs = root.Descendants("BuildableReference").ToList();
            var match = refs.FirstOrDefault(r => (string)r.Attribute("BlueprintName") == "Unity-iPhone")
                     ?? refs.FirstOrDefault(r => (string)r.Attribute("BuildableIdentifier") == "primary")
                     ?? refs.FirstOrDefault();
            return match == null ? null : new XElement(match);
        }

        /// <summary>
        /// Shell expression for the repo root, as seen from the generated Xcode project.
        /// $PROJECT_DIR is the folder containing Unity-iPhone.xcodeproj, i.e. pathToBuiltProject.
        /// Measured, not assumed: with the standard Builds/iOS-Full output this returns
        /// "$PROJECT_DIR/../.." (verified 2026-08-18). A build written outside the repo falls back
        /// to an absolute path — the generated project is gitignored and per-machine anyway.
        /// </summary>
        static string RepoRootExpression(string pathToBuiltProject)
        {
            string rootFull = Norm(Directory.GetParent(Application.dataPath).FullName);
            var dir = new DirectoryInfo(Path.GetFullPath(pathToBuiltProject));

            for (int up = 0; dir != null; dir = dir.Parent, up++)
            {
                if (!string.Equals(Norm(dir.FullName), rootFull, StringComparison.Ordinal)) continue;
                return up == 0
                    ? "$PROJECT_DIR"
                    : "$PROJECT_DIR/" + string.Join("/", Enumerable.Repeat("..", up));
            }
            return rootFull; // built outside the repo tree
        }

        static string Norm(string p) => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar);
    }
}
#endif
