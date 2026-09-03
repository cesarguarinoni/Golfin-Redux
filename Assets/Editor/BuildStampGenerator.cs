using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GolfinRedux.BuildEditor
{
    /// <summary>
    /// Versioning + anti-staleness build stamp (build_version_stamp task).
    ///
    /// PRIMARY GOAL: after a Build &amp; Run, Cesar can confirm AT A GLANCE that the
    /// binary on the device contains the code that was just written. Everything
    /// here optimises for defeating staleness.
    ///
    /// SCHEME
    ///  • Marketing version = SemVer in PlayerSettings.bundleVersion — MANUAL, never
    ///    auto-bumped and never touched by this task.
    ///  • Build number = git rev-list --count HEAD (auto). Written to BOTH
    ///    PlayerSettings.iOS.buildNumber and PlayerSettings.Android.bundleVersionCode.
    ///    Git-derived on purpose: a counter file would merge-conflict across two
    ///    machines and could go backwards, permanently burning an App Store upload slot.
    ///  • Guard: if the computed number is &lt;= the last-uploaded value, FAIL the build
    ///    loudly. Last-uploaded lives in one tracked file, written ONLY at upload time
    ///    (GOLFIN/Build/Mark Current Commit As Uploaded), never on an ordinary build.
    ///
    /// DISPLAY STRING (captured at BUILD time, baked into Resources/Data/build_stamp.txt,
    /// read back at runtime — the device has no git):
    ///   "v{bundleVersion} ({buildNumber}) {shortSha}[+{diffHash}] · {MM-dd HH:mm}"
    ///     clean: v0.1.0 (2014) bb59d32 · 08-03 14:32
    ///     dirty: v0.1.0 (2014) bb59d32+4f9e · 08-03 14:32
    ///   • diffHash = 4-char hash of `git diff HEAD`, present ONLY when the tree is
    ///     dirty. Code iterates WITHOUT committing, so a bare dirty flag would make
    ///     every uncommitted build identical — two different uncommitted builds MUST
    ///     produce different strings, and the diff hash guarantees that.
    ///   • timestamp = BUILD time, local (JST on Cesar's machine). This is THE
    ///     anti-staleness field: it reflects when the binary was produced.
    ///
    /// CHURN: writing PlayerSettings dirties ProjectSettings.asset. OnPostprocessBuild
    /// restores exactly the two fields we changed (via captured pre-build values +
    /// SaveAssets), so an ordinary build leaves no merge noise across machines. The
    /// baked stamp asset is generated + gitignored, so it never enters git either.
    /// </summary>
    public sealed class BuildStampGenerator : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        // ── Paths ─────────────────────────────────────────────────────────────
        const string StampAssetPath = "Assets/Resources/Data/build_stamp.txt"; // project-relative (AssetDatabase)
        const string StampResourceDir = "Assets/Resources/Data";
        // Root-relative, tracked. NOT under any *Build/* dir — .gitignore's "[Bb]uild/"
        // rule ignores every Build/ folder, which would silently untrack this guard.
        const string GuardFileRel = "Docs/Versioning/last_uploaded_build.txt";
        /// <summary>
        /// gps_standalone_shell §D8 — the guard is PER APP RECORD, because App Store Connect's
        /// build-number uniqueness rule is. The game and the PLAYLIFE shell are two records
        /// (com.nextinnovation.golfingame / com.nextinnovation.golfingps), so a standalone upload
        /// at commit N must not refuse the game's upload at commit N: one shared file would make
        /// shipping both variants of one commit impossible instead of merely sequential.
        /// </summary>
        const string StandaloneGuardFileRel = "Docs/Versioning/last_uploaded_build.golfingps.txt";
        const string Tag = "[BuildStamp]";

        public int callbackOrder => 0;

        // Pre-build snapshot restored in OnPostprocessBuild — keeps ProjectSettings.asset clean.
        static bool _fieldsModified;
        static string _prevIosBuildNumber;
        static int _prevAndroidBundleVersionCode;

        // ─────────────────────────────────────────────────────────────────────
        //  Build pipeline
        // ─────────────────────────────────────────────────────────────────────
        public void OnPreprocessBuild(BuildReport report)
        {
            int buildNumber = GitRevCountOrThrow();

            // Guard: never regress below what App Store Connect already has — but ONLY for
            // store-bound builds, since only they can burn an upload slot. Development /
            // iteration builds (Dev-iOS / Dev-Android set BuildOptions.Development) skip the
            // refuse-to-build check, so Cesar can rebuild and test at the same commit after a
            // TestFlight upload without inventing a dummy commit. They STILL bake the build
            // number normally below — only the refusal is skipped.
            var buildOptions = report != null ? report.summary.options : BuildOptions.None;
            if (GuardApplies(buildOptions))
            {
                int lastUploaded = ReadLastUploaded();
                if (buildNumber <= lastUploaded)
                {
                    throw new BuildFailedException(
                        $"{Tag} REFUSING TO BUILD: computed build number {buildNumber} <= last-uploaded {lastUploaded}. " +
                        $"Build numbers must strictly increase or App Store Connect rejects the upload. " +
                        $"Commit at least one change (git rev-list --count HEAD must advance) before building again. " +
                        $"(last-uploaded is recorded by Tools/mark-uploaded.sh in {GuardFileForThisBuild()}.)");
                }
            }

            // Write the build number into BOTH platforms. Snapshot first so postprocess
            // restores the exact pre-build values (no ProjectSettings.asset churn).
            _prevIosBuildNumber = PlayerSettings.iOS.buildNumber;
            _prevAndroidBundleVersionCode = PlayerSettings.Android.bundleVersionCode;
            _fieldsModified = true;

            PlayerSettings.iOS.buildNumber = buildNumber.ToString(CultureInfo.InvariantCulture);
            PlayerSettings.Android.bundleVersionCode = buildNumber;

            // OnPostprocessBuild fires on SUCCESS only, so a FAILED build would otherwise leave
            // the modified fields on disk (the exact churn the restore exists to prevent, in the
            // situation where builds get repeated most). Schedule a safety net: EditorApplication.
            // delayCall fires once the main thread frees, which for a synchronous BuildPlayer is
            // after it returns — success OR failure. RestoreFields is idempotent, so whichever of
            // the two runs first wins and the other no-ops.
            EditorApplication.delayCall += RestoreFieldsSafetyNet;

            // Bake the display string. bundleVersion is read AFTER the assignments so it
            // reflects the manual marketing version already in PlayerSettings.
            string stamp = ComputeStampString(buildNumber);
            WriteStampAsset(stamp);

            Debug.Log($"{Tag} preprocess: build #{buildNumber} baked. stamp = \"{stamp}\"");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            // Success path — fires promptly. Idempotent with the delayCall safety net.
            RestoreFields();
        }

        static void RestoreFieldsSafetyNet()
        {
            EditorApplication.delayCall -= RestoreFieldsSafetyNet;
            RestoreFields();
        }

        // Restore ONLY the two fields we changed, back to their exact captured pre-build
        // values, then persist — so ProjectSettings.asset generates no merge noise. This is a
        // NARROW restore on purpose: a wholesale revert of ProjectSettings.asset would clobber
        // incrementalIl2cppBuild (committed in 35beb2723) and other live settings. The built
        // player already has the number baked in; this only affects the working copy on disk.
        // Guarded by _fieldsModified, so it is idempotent (safe to call from both the
        // postprocess hook and the delayCall net).
        static void RestoreFields()
        {
            if (!_fieldsModified) return;
            PlayerSettings.iOS.buildNumber = _prevIosBuildNumber;
            PlayerSettings.Android.bundleVersionCode = _prevAndroidBundleVersionCode;
            AssetDatabase.SaveAssets();
            _fieldsModified = false;
            Debug.Log($"{Tag} restored ProjectSettings buildNumber fields (no churn).");
        }

        // The upload guard only protects App Store Connect slots, which only store-bound
        // (non-development) builds can burn. Exposed internal for the verification harness.
        internal static bool GuardApplies(BuildOptions options)
            => !options.HasFlag(BuildOptions.Development);

        // ─────────────────────────────────────────────────────────────────────
        //  Editor play-mode: keep a live stamp so play-mode verification is real.
        //  This used to be gated on GOLFIN_TESTBUILD, matching the runtime BuildStamp
        //  overlay's own gate. It is now UNGATED because the stamp asset gained a
        //  second, unconditional consumer: Settings ▸ About reads the build number
        //  back from it (see GolfinRedux.UI.BuildInfo.AppVersion) on every profile.
        //  Without this, About would show a bare marketing version in the editor and
        //  the parenthesised build number could not be verified before a device build.
        // ─────────────────────────────────────────────────────────────────────
        [InitializeOnLoadMethod]
        static void InstallEditorHooks()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            // Ensure the asset exists (and is imported) before the first play session.
            if (!File.Exists(Path.Combine(ProjectRoot, StampAssetPath)))
                RegenerateEditorStamp();
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // Freshen the timestamp + diff hash right before every play session.
            if (state == PlayModeStateChange.ExitingEditMode)
                RegenerateEditorStamp();
        }

        static void RegenerateEditorStamp()
        {
            int buildNumber = GitRevCount(); // -1 if git unavailable
            string stamp = buildNumber >= 0
                ? ComputeStampString(buildNumber)
                : $"v{PlayerSettings.bundleVersion} (editor) · {DateTime.Now.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture)}";
            WriteStampAsset(stamp);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Upload bookkeeping
        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("GOLFIN/Build/Mark Current Commit As Uploaded")]
        public static void MarkUploaded()
        {
            int n = GitRevCount();
            if (n < 0)
            {
                Debug.LogError($"{Tag} cannot mark uploaded — git rev-list failed.");
                return;
            }
            var full = Path.Combine(ProjectRoot, GuardFileRel);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, n.ToString(CultureInfo.InvariantCulture) + "\n");
            AssetDatabase.Refresh();
            Debug.Log($"{Tag} recorded last-uploaded build = {n} → {GuardFileRel}. " +
                      $"Builds at this commit will now FAIL until you commit again.");
        }

        // Batchmode-callable equivalent for CI/upload scripts (-executeMethod).
        public static void MarkUploadedBatch() => MarkUploaded();

        // ─────────────────────────────────────────────────────────────────────
        //  Stamp string
        // ─────────────────────────────────────────────────────────────────────
        static string ComputeStampString(int buildNumber)
        {
            string bundleVersion = PlayerSettings.bundleVersion;      // manual SemVer, e.g. "0.1.0"
            string shortSha = GitOutput("rev-parse --short=7 HEAD")?.Trim() ?? "nogit";

            // `git diff HEAD` reports TRACKED changes only, so a brand-new uncommitted .cs
            // (routine during implementation) would leave the tree looking clean and drop the
            // "+diffHash". Fold untracked files in too. Keep the diff itself in the hash input
            // so tracked-file CONTENT edits still register (porcelain alone would collapse every
            // modification to " M path" and lose that). --exclude-standard respects .gitignore,
            // so the generated (gitignored) build_stamp.txt never counts itself as dirt.
            string diff = GitOutput("diff HEAD") ?? string.Empty;
            string untracked = GitOutput("ls-files --others --exclude-standard") ?? string.Empty;
            string dirtyInput = diff + untracked;
            bool dirty = dirtyInput.Length > 0;
            string diffHash = dirty ? Hash4(dirtyInput) : null;

            // BUILD time, LOCAL (JST on Cesar's machine) — the anti-staleness field.
            string timestamp = DateTime.Now.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);

            var sb = new StringBuilder();
            sb.Append('v').Append(bundleVersion)
              .Append(" (").Append(buildNumber.ToString(CultureInfo.InvariantCulture)).Append(") ")
              .Append(shortSha);
            if (dirty) sb.Append('+').Append(diffHash);
            sb.Append(" · ").Append(timestamp); // " · "
            return sb.ToString();
        }

        static string Hash4(string s)
        {
            using var sha1 = SHA1.Create();
            byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(s));
            // 2 bytes -> 4 hex chars
            return hash[0].ToString("x2") + hash[1].ToString("x2");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Asset IO
        // ─────────────────────────────────────────────────────────────────────
        static void WriteStampAsset(string stamp)
        {
            var full = Path.Combine(ProjectRoot, StampAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, stamp);
            // Import so Resources.Load can find it (fresh file / build packing).
            AssetDatabase.ImportAsset(StampAssetPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// Which guard file this build is judged against — the standalone's own when the build
        /// being produced is the PLAYLIFE shell (see <see cref="StandaloneGuardFileRel"/>).
        /// Detection is delegated to the identity preprocessor, which already has to answer the
        /// same question and answers it from the ACTIVE PROFILE's defines rather than an #if
        /// (profile defines never reach editor assemblies).
        /// </summary>
        static string GuardFileForThisBuild()
            => Golfin.EditorTools.StandaloneBuildPreprocessor.IsStandaloneIdentityBuild()
               ? StandaloneGuardFileRel : GuardFileRel;

        static int ReadLastUploaded()
        {
            var full = Path.Combine(ProjectRoot, GuardFileForThisBuild());
            if (!File.Exists(full)) return 0;
            var txt = File.ReadAllText(full).Trim();
            return int.TryParse(txt, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Git
        // ─────────────────────────────────────────────────────────────────────
        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        static int GitRevCountOrThrow()
        {
            int n = GitRevCount();
            if (n < 0)
                throw new BuildFailedException(
                    $"{Tag} REFUSING TO BUILD: `git rev-list --count HEAD` failed. The build number is " +
                    $"git-derived and cannot be computed. Is this a git working copy with git on PATH?");
            return n;
        }

        /// <summary>`git rev-list --count HEAD`, or -1 when git cannot answer. Public so the
        /// build-time reports (ContentArtValidator) file themselves under the SAME number this
        /// generator will stamp into the binary, rather than deriving a second one.</summary>
        public static int GitRevCount()
        {
            var outp = GitOutput("rev-list --count HEAD");
            if (outp == null) return -1;
            return int.TryParse(outp.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : -1;
        }

        /// <summary>Run `git &lt;args&gt;` in the project root; return stdout, or null on any failure.</summary>
        static string GitOutput(string args)
        {
            foreach (var exe in GitExecutables())
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = args,
                        WorkingDirectory = ProjectRoot,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    using var p = Process.Start(psi);
                    if (p == null) continue;
                    string stdout = p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd(); // drain so the pipe never blocks
                    p.WaitForExit(10000);
                    if (p.ExitCode == 0) return stdout;
                }
                catch
                {
                    // try the next candidate path
                }
            }
            return null;
        }

        // Unity launched from Finder can have a minimal PATH; fall back to common locations.
        static string[] GitExecutables() => new[]
        {
            "git",
            "/usr/bin/git",
            "/usr/local/bin/git",
            "/opt/homebrew/bin/git",
        };
    }
}
