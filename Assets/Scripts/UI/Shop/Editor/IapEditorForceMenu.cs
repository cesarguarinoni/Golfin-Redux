// Assets/Scripts/UI/Shop/Editor/IapEditorForceMenu.cs
// iap_plumbing — the Editor-only toggle behind IapService.EditorForceFakeStorePref (see that field).
// OFF by default; leave it off after a capture session (feedback_restore_playable_state).
using UnityEditor;
using UnityEngine;

namespace GolfinRedux.UI.Shop.EditorTools
{
    public static class IapEditorForceMenu
    {
        private const string MenuPath = "GOLFIN/Store/IAP: force FakeStore (Editor)";

        [MenuItem(MenuPath, priority = 265)]
        private static void Toggle()
        {
            bool now = !EditorPrefs.GetBool(Golfin.Economy.IapService.EditorForceFakeStorePref, false);
            EditorPrefs.SetBool(Golfin.Economy.IapService.EditorForceFakeStorePref, now);
            Debug.Log($"[IapService] Editor FakeStore force is now {(now ? "ON" : "OFF")} (takes effect on the next play-mode entry).");
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool Validate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(Golfin.Economy.IapService.EditorForceFakeStorePref, false));
            return true;
        }
    }
}
