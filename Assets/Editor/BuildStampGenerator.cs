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

            // Guard: never regress below what App Store Connect already has.
            int lastUploaded = ReadLastUploaded();
            if (buildNumber <= lastUploaded)
            {
                throw new BuildFailedException(
                    $"{Tag} REFUSING TO BUILD: computed build number {buildNumber} <= last-uploaded {lastUploaded}. " +
                    $"Build numbers must strictly increase or App Store Connect rejects the upload. " +
                    $"Commit at least one change (git rev-list --count HEAD must advance) before building again. " +
                    $"(last-uploaded is recorded by GOLFIN/Build/Mark Current Commit As Uploaded in {GuardFileRel}.)");
            }

            // Write the build number into BOTH platforms. Snapshot first so postprocess
            // restores the exact pre-build values (no ProjectSettings.asset churn).
            _prevIosBuildNumber = PlayerSettings.iOS.buildNumber;
            _prevAndroidBundleVersionCode = PlayerSettings.Android.bundleVersionCode;
            _fieldsModified = true;

            PlayerSettings.iOS.buildNumber = buildNumber.ToString(CultureInfo.InvariantCulture);
            PlayerSettings.Android.bundleVersionCode = buildNumber;

            // Bake the display string. bundleVersion is read AFTER the assignments so it
            // reflects the manual marketing version already in PlayerSettings.
            string stamp = ComputeStampString(buildNumber);
            WriteStampAsset(stamp);

            Debug.Log($"{Tag} preprocess: build #{buildNumber} baked. stamp = \"{stamp}\"");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!_fieldsModified) return;

            // Restore ONLY the two fields we changed, back to their exact pre-build
            // values, then persist — so ProjectSettings.asset generates no merge noise.
            // The built player already has the number baked in; this only affects the
            // working copy on disk.
            PlayerSettings.iOS.buildNumber = _prevIosBuildNumber;
            PlayerSettings.Android.bundleVersionCode = _prevAndroidBundleVersionCode;
            AssetDatabase.SaveAssets();

            _fieldsModified = false;
            Debug.Log($"{Tag} postprocess: restored ProjectSettings buildNumber fields (no churn).");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Editor play-mode: keep a live stamp so play-mode verification is real.
        //  Gated on GOLFIN_TESTBUILD so it only runs when a stamp-bearing profile
        //  is active — matching the runtime BuildStamp component's own gate.
        // ─────────────────────────────────────────────────────────────────────
        [InitializeOnLoadMethod]
        static void InstallEditorHooks()
        {
#if GOLFIN_TESTBUILD
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            // Ensure the asset exists (and is imported) before the first play session.
            if (!File.Exists(Path.Combine(ProjectRoot, StampAssetPath)))
                RegenerateEditorStamp();
#endif
        }

#if GOLFIN_TESTBUILD
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
#endif

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

            string diff = GitOutput("diff HEAD") ?? string.Empty;
            bool dirty = diff.Length > 0;
            string diffHash = dirty ? Hash4(diff) : null;

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

        static int ReadLastUploaded()
        {
            var full = Path.Combine(ProjectRoot, GuardFileRel);
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

        static int GitRevCount()
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
