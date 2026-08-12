// account_flow_wiring Phase 2b — iOS deep-link workaround.
// Unity 6000.3.9f1 iOS builds include UIApplicationSceneManifest (UIScene lifecycle) in Info.plist,
// and with it present Application.deepLinkActivated NEVER fires while the app is running
// (Unity bug IN-135632 / issuetracker "deepLinkActivated does not get invoked ... when
// UIApplicationSceneManifest is added"). Confirmed on-device 2026-08-11: golfin:// opened the app
// but no deep-link event reached AuthService, breaking the OAuth return leg.
// Workaround per the Unity thread: remove UIApplicationSceneManifest → classic lifecycle → deep
// links work. Delete this file once we're on a Unity patch where IN-135632 is fixed.
#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Golfin.EditorTools
{
    public static class StripSceneManifestPostprocessor
    {
        [PostProcessBuild(999)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            if (!File.Exists(plistPath))
            {
                UnityEngine.Debug.LogWarning($"[Build] Info.plist not found at {plistPath} — scene-manifest strip skipped.");
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            if (plist.root.values.ContainsKey("UIApplicationSceneManifest"))
            {
                plist.root.values.Remove("UIApplicationSceneManifest");
                plist.WriteToFile(plistPath);
                UnityEngine.Debug.Log("[Build] Stripped UIApplicationSceneManifest from Info.plist (IN-135632 deep-link workaround).");
            }
            else
            {
                UnityEngine.Debug.Log("[Build] No UIApplicationSceneManifest in Info.plist — nothing to strip (Unity fixed IN-135632? consider deleting this postprocessor).");
            }
        }
    }
}
#endif
