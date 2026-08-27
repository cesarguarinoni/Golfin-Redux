#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.EditorTools
{
    /// <summary>
    /// green_putter_portrait — proves the club selector actually follows the putter auto-equip,
    /// through the REAL chain, in play mode.
    ///
    /// The chain under test: PhysicsLabController.SetClub(PutterIndex) -> OnClubChanged ->
    /// OnClubIndexChanged -> EnterPutterMode -> SyncClubSelectorToPutter ->
    /// ClubContext.RequestSelection -> the live populator -> SelectedPortrait/ClubId/TypeLabel.
    ///
    /// Nothing here reaches into the fix's internals; it drives the same entry point the game does
    /// and reads the bus the club button paints from. A static assertion could confirm the bag scan
    /// but NOT that RequestSelection reaches a live populator, which is the half that can actually
    /// be broken in a real scene.
    /// </summary>
    public static class PutterSelectorVerify
    {
        const string ArmedKey = "PutterSelectorVerify.Armed";

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("GOLFIN/Quality Tiers/Verify putter auto-equip drives the club selector")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[PutterVerify] stop play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/ShellScene.unity");
            PlayerSettings.runInBackground = true;
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            var go = new GameObject("[PutterSelectorVerifyBot]");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<PutterSelectorVerifyRunner>().Begin();
        }
    }

    public class PutterSelectorVerifyRunner : MonoBehaviour
    {
        public void Begin() => StartCoroutine(Seq());

        static string Snap(string tag) =>
            $"{tag,-18} clubId={ClubContext.SelectedClubId,-24} type={ClubContext.SelectedTypeLabel,-10} " +
            $"idx={ClubContext.SelectedIndex} portrait={(ClubContext.SelectedPortrait != null ? ClubContext.SelectedPortrait.name : "<NULL>")}";

        IEnumerator Seq()
        {
            var sb = new StringBuilder();
            yield return new WaitForSecondsRealtime(6f);

            // Past the Title/PLAY gate — ShowScreen swaps screens BEHIND it.
            foreach (var b in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (b != null && (b.name == "StartButton" || b.name == "PlayButton") && b.gameObject.activeInHierarchy)
                { b.onClick.Invoke(); break; }
            yield return new WaitForSecondsRealtime(3f);

            GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.Home, true);
            yield return new WaitForSecondsRealtime(2f);

            if (!SeedAndLoad(6)) { Debug.LogError("[PutterVerify] could not load hole"); EditorApplication.isPlaying = false; yield break; }
            yield return WaitScene("LabScaffold", 60f);
            yield return WaitScene("Hole_06_Geo", 60f);
            yield return new WaitForSecondsRealtime(8f);

            var lab = Object.FindFirstObjectByType<Golfin.Physics.Viewer.PhysicsLabController>(FindObjectsInactive.Include);
            if (lab == null) { Debug.LogError("[PutterVerify] no PhysicsLabController"); EditorApplication.isPlaying = false; yield break; }

            var bag = ClubContext.EquippedBag;
            sb.AppendLine("BAG (" + (bag?.Count ?? 0) + " clubs):");
            if (bag != null) for (int i = 0; i < bag.Count; i++)
                sb.AppendLine($"   [{i}] {bag[i].ClubId,-24} label={bag[i].TypeLabel,-10} labIdx={bag[i].LabClubIndex}");

            var setClub = lab.GetType().GetMethod("SetClub",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int putterIdx = Golfin.Physics.Viewer.PhysicsLabController.PutterIndex;

            // Start on a NON-putter club so a change is observable.
            setClub?.Invoke(lab, new object[] { 0 });
            yield return new WaitForSecondsRealtime(2f);
            sb.AppendLine(Snap("BEFORE (driver)"));

            // THE REAL ENTRY POINT the green uses.
            setClub?.Invoke(lab, new object[] { putterIdx });
            yield return new WaitForSecondsRealtime(2.5f);
            sb.AppendLine(Snap("AFTER  (putter)"));
            bool becamePutter = ClubContext.SelectedTypeLabel != null &&
                                ClubContext.SelectedTypeLabel.ToUpperInvariant().Contains("PUTTER");
            sb.AppendLine("   => selector shows the PUTTER: " + (becamePutter ? "PASS" : "FAIL"));

            // And back off the green.
            setClub?.Invoke(lab, new object[] { 0 });
            yield return new WaitForSecondsRealtime(2.5f);
            sb.AppendLine(Snap("RESTORED"));
            bool restored = ClubContext.SelectedTypeLabel != null &&
                            !ClubContext.SelectedTypeLabel.ToUpperInvariant().Contains("PUTTER");
            sb.AppendLine("   => club restored on leaving: " + (restored ? "PASS" : "FAIL"));

            Debug.Log("[PutterVerify]\n" + sb);
            yield return new WaitForSecondsRealtime(1f);
            EditorApplication.isPlaying = false;
        }

        static System.Type Find(string n)
        {
            foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
            { var t = a.GetType(n, false); if (t != null) return t; }
            return null;
        }

        static bool SeedAndLoad(int hole)
        {
            try
            {
                var gs = Find("Golfin.Gameplay.Session.GameSession");
                gs.GetProperty("IsVersus", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, false);
                gs.GetMethod("SeedSession", new[] { typeof(int), typeof(string), typeof(int) })
                  ?.Invoke(null, new object[] { hole, "", 0 });
                var lt = Find("Golfin.UI.GameplayTransition.GameplaySceneLoader");
                var li = lt?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (li == null) return false;
                foreach (var m in lt.GetMethods())
                    if (m.Name == "BeginGameplayLoad")
                    { m.Invoke(li, m.GetParameters().Length == 1 ? new object[] { hole } : new object[] { hole, null }); return true; }
                return false;
            }
            catch (System.Exception e) { Debug.LogWarning("[PutterVerify] " + e.Message); return false; }
        }

        static IEnumerator WaitScene(string name, float timeout)
        {
            float t = 0;
            while (t < timeout)
            {
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                { var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i); if (s.name == name && s.isLoaded) yield break; }
                yield return new WaitForSecondsRealtime(0.5f); t += 0.5f;
            }
        }
    }
}
#endif
