// TestFlight upload prep — export-compliance declaration.
// Apple asks "does your app use encryption?" on every single TestFlight/App Store upload unless
// ITSAppUsesNonExemptEncryption is already declared in Info.plist. GOLFIN only uses standard
// HTTPS/TLS (Supabase REST + auth), which is exempt under the mass-market/standard-encryption
// exemption, so we declare false once here and the "Missing Compliance" step disappears from
// every future upload. Unity regenerates Info.plist on each build, so this has to be a
// PostProcessBuild callback rather than a hand edit in the Xcode project.
// See Docs/TESTFLIGHT_RUNBOOK.md Phase 3.
#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Golfin.EditorTools
{
    public static class iOSPostProcess
    {
        [PostProcessBuild(1000)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            if (!File.Exists(plistPath))
            {
                UnityEngine.Debug.LogWarning($"[Build] Info.plist not found at {plistPath} — export-compliance key not written.");
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);
            UnityEngine.Debug.Log("[Build] Wrote ITSAppUsesNonExemptEncryption=false to Info.plist (skips the export-compliance step on upload).");
        }
    }
}
#endif
