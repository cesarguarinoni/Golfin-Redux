using System.Collections;
using UnityEngine;
using TMPro;

namespace GolfinRedux.UI
{
    public class SplashScreenController : MonoBehaviour
    {
        public void OnStartClicked()
        {
            Debug.Log("START clicked - showing Home");

            // K13 (boot_loading_screen_removal): boot goes straight to Home.
            // The Loading screen used to sit here, but on this path it was pure theater —
            // LoadingScreenController's LegacyBootHome mode fakes its bar off a 2s timer and
            // nothing ever calls SetRealProgress/SetProgress for boot (the only real-progress
            // feeder in the repo is GameplaySceneLoader's HoleLoad path). All heavy boot init
            // (CSV singletons, CharacterManager, save load) runs in Awake/RuntimeInitializeOnLoad
            // and is long finished before this button is even tappable. Measured 2026-08-05:
            // Splash interactive at t=9.0s, boot init done at t=3.9s; the real work behind this
            // transition is ~0.23s (Main Theme decode inside ApplyScreen), fully covered by the
            // 0.25s fade. Per Cesar's <2s rule the fake wait is removed.
            //
            // The Loading screen GameObject and ScreenId are deliberately KEPT: they are the
            // hole-load surface (real progress, driven by GameplaySceneLoader), and if boot ever
            // gains a real dependency (backend login is on the roadmap) it can re-enter here via
            // the existing SetRealProgress plumbing.
            var manager = FindFirstObjectByType<ScreenManager>();
            if (manager != null)
                manager.ShowScreen(ScreenId.Home);
            else
                Debug.LogError("ScreenManager not found");
        }

        // demo_build_slice §3.4: the demo boots to this Splash gate. Label the guest button "Play"
        // and remove Create Account (offline demo — guests tap Play to go straight into the game;
        // Login is kept). No-op in the full game.
        private void OnEnable()
        {
            if (GolfinRedux.Demo.DemoGate.IsDemo)
                StartCoroutine(ApplyDemoGate());
        }

        private IEnumerator ApplyDemoGate()
        {
            // Wait one frame so the LocalizedText on the Start label applies first — then override it.
            yield return null;

            var start = transform.Find("StartButton");
            if (start != null)
            {
                var label = start.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    // Disable the auto-localizer so it can't re-set "PLAY" over our "Play".
                    var loc = label.GetComponent("LocalizedText") as Behaviour;
                    if (loc != null) loc.enabled = false;
                    label.text = "Play";
                }
            }

            var create = transform.Find("CreateAccountButton");
            if (create != null) create.gameObject.SetActive(false);
        }

        // You can add public void OnCreateAccountClicked() / OnLoginClicked() later
    }
}
