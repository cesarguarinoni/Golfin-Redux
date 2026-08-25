using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using GolfinRedux.UI;
using Golfin.UI;
using Golfin.UI.Modals;
using Golfin.Gameplay.Session;

namespace Golfin.UI.GameplayTransition
{
    /// <summary>
    /// Stage C0: owns the OPPONENT FOUND → gameplay-scene-loaded transition.
    ///
    /// Flow:
    ///   1. MatchmakingModalController seeds GameSession (Stage B) and calls BeginGameplayLoad.
    ///   2. We show the Loading screen (with tips) via ScreenManager.
    ///   3. We additively load LabScaffold.unity (the gameplay host scene).
    ///   4. We additively load Hole_{NN}_Geo.unity for the seeded hole number.
    ///   5. We set the gameplay scene as active so subsequent Instantiate calls land there.
    ///   6. We finish the loading screen (fade out / hide). PhysicsLabController takes over.
    ///
    /// On hole exit (Stage D's MENU button), UnloadGameplay tears down both scenes.
    /// </summary>
    public class GameplaySceneLoader : MonoBehaviour
    {
        public static GameplaySceneLoader Instance { get; private set; }

        const string GAMEPLAY_SCENE_NAME = "LabScaffold";
        const string HOLE_GEO_SCENE_PREFIX = "Hole_";
        const string HOLE_GEO_SCENE_SUFFIX = "_Geo";

        [Header("References (auto-wired in Awake if left null)")]
        [SerializeField] LoadingScreenController loadingScreen;
        [SerializeField] PersistentUIManager persistentUI;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (loadingScreen == null)
                loadingScreen = FindObjectOfType<LoadingScreenController>(includeInactive: true);
            if (persistentUI == null)
                persistentUI = PersistentUIManager.Instance != null
                    ? PersistentUIManager.Instance
                    : FindObjectOfType<PersistentUIManager>(includeInactive: true);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>
        /// Entry point. MatchmakingModalController calls this after seeding GameSession.
        /// Pass <paramref name="modalToHideOnMidpoint"/> to have the loader hide the
        /// matchmaking modal under the fade-to-black so the modal and the home backdrop
        /// disappear together (Stage C0 visual-gate fix).
        /// </summary>
        public void BeginGameplayLoad(int holeNumber, ModalController modalToHideOnMidpoint = null)
        {
            StartCoroutine(LoadCoroutine(holeNumber, modalToHideOnMidpoint));
        }

        /// <summary>
        /// Synchronous prelude — extracted so EditMode tests can verify the immediate
        /// side effects of BeginGameplayLoad without ticking coroutine frames or running
        /// scene async loads. Used by the FadeController midpoint callback in production
        /// (so the screen swap happens under the full-black overlay).
        /// </summary>
        internal void ApplyPreloadSetup(int holeNumber)
        {
            if (loadingScreen != null) loadingScreen.PrepareForHoleLoad(holeNumber);
            // instant: true — the outer FadeController.FadeOutThenIn already owns the
            // fade-to-black-and-back. Re-triggering ScreenManager's fade here would
            // overlap two fade systems (the bug the visual gate caught).
            if (ScreenManager.Instance != null) ScreenManager.Instance.ShowScreen(ScreenId.Loading, instant: true);

            // Belt-and-suspenders: ScreenManager.ApplyScreen already hides bars on Loading,
            // but call SetBottomNavVisible(false) explicitly so the contract is enforced
            // even if the loading screen transition is bypassed in tests / dev tools.
            if (persistentUI != null) persistentUI.SetBottomNavVisible(false);
        }

        IEnumerator LoadCoroutine(int holeNumber, ModalController modalToHideOnMidpoint)
        {
            // 1. Unified fade: FadeController fades EVERYTHING visible (modal + home) to
            //    black together. At full black, hide modal + swap to LoadingScreen + hide
            //    bottom nav in one frame, all invisible behind the overlay. Then fade back
            //    from black to reveal the LoadingScreen.
            bool midpointFired = false;
            var fadeCtrl = FadeController.Instance;
            if (fadeCtrl != null && ScreenManager.Instance != null)
            {
                fadeCtrl.FadeOutThenIn(() =>
                {
                    if (modalToHideOnMidpoint != null) modalToHideOnMidpoint.Hide();
                    ApplyPreloadSetup(holeNumber);
                    midpointFired = true;
                });
                while (!midpointFired) yield return null;
                // Let the fade-back-from-black complete before kicking off the async scene
                // loads, so the LoadingScreen reveal feels smooth. FadeController's default
                // duration is 0.5s total (0.25s out + 0.25s in) — wait the back half.
                yield return new WaitForSeconds(0.3f);
            }
            else
            {
                // No FadeController available — fall back to an instant cut.
                if (modalToHideOnMidpoint != null) modalToHideOnMidpoint.Hide();
                ApplyPreloadSetup(holeNumber);
            }

            // 2b. Tear down any gameplay scenes still loaded from a previous hole.
            //     Both loads below are ADDITIVE, so without this "Next Hole" stacked a second
            //     LabScaffold and a second Hole_NN_Geo on top of the old ones. PhysicsLab's
            //     ScanForLoadedHoleSceneAtStartup takes the FIRST Hole_*_Geo it finds, so it
            //     kept binding to the PREVIOUS hole and placed the player at that hole's tee
            //     while the new hole's terrain was drawn — 11.3 m underground going from
            //     hole 1 to hole 2. A clean slate also means the fresh LabScaffold's Start()
            //     re-runs the scan, which is the only thing that rebinds it.
            yield return UnloadGameplayScenes();

            // 3. Additively load the gameplay host scene (LabScaffold).
            var hostOp = SceneManager.LoadSceneAsync(GAMEPLAY_SCENE_NAME, LoadSceneMode.Additive);
            hostOp.allowSceneActivation = true;
            while (!hostOp.isDone)
            {
                if (loadingScreen != null) loadingScreen.SetProgress(hostOp.progress * 0.5f);
                yield return null;
            }

            // 4. Set the gameplay scene as active so newly-instantiated GameObjects land there.
            var gameplayScene = SceneManager.GetSceneByName(GAMEPLAY_SCENE_NAME);
            if (gameplayScene.IsValid()) SceneManager.SetActiveScene(gameplayScene);

            // 5. Additively load the hole geo scene (Hole_NN_Geo). PhysicsLabController's
            //    ScanForLoadedHoleSceneAtStartup coroutine will detect this scene and wire
            //    surface providers + tee positioning via OnHoleLoaded.
            string holeSceneName = HOLE_GEO_SCENE_PREFIX + holeNumber.ToString("D2") + HOLE_GEO_SCENE_SUFFIX;
            var holeOp = SceneManager.LoadSceneAsync(holeSceneName, LoadSceneMode.Additive);
            if (holeOp == null)
            {
                Debug.LogError($"[GameplaySceneLoader] LoadSceneAsync returned null for '{holeSceneName}'. " +
                               "Verify the scene is in EditorBuildSettings.");
            }
            else
            {
                holeOp.allowSceneActivation = true;
                while (!holeOp.isDone)
                {
                    if (loadingScreen != null) loadingScreen.SetProgress(0.5f + holeOp.progress * 0.5f);
                    yield return null;
                }

                // 5b. Apply this RUN's sky, overriding the imported skybox + sun.
                //     The sky is rolled once per run and held across Next Hole, so this
                //     is a re-apply on every hole after the first — needed because each
                //     hole scene has its own directional light to point. SkyRandomizer.EndRun
                //     (in UnloadGameplay) is what releases it.
                //
                //     Done HERE, behind the loading screen, so the swap is never visible:
                //     DynamicGI.UpdateEnvironment() re-solves ambient and the default
                //     reflection probe, which would pop if it landed on a visible frame.
                //     No-ops safely when the preset library is missing, in which case the
                //     hole keeps the skybox HoleGeoImporter baked into it.
                var holeScene = SceneManager.GetSceneByName(holeSceneName);
                if (holeScene.IsValid() && holeScene.isLoaded)
                    Golfin.Gameplay.Environment.SkyRandomizer.ApplyRandomTo(holeScene);
            }

            // 6. Loading screen finishes — hands off to gameplay. Does NOT navigate to Home;
            //    the loading screen GameObject is just hidden so the additive gameplay scene
            //    becomes visible.
            if (loadingScreen != null)
                yield return loadingScreen.FinishLoadingCoroutine();

            // 7. Hand-off: the player can now actually SEE the tee. Start the tee-idle
            //    countdown from here.
            //
            //    ShotUI_Canvas is active for the whole of steps 3-6 (it ships active inside
            //    LabScaffold), so TeeIdleGlowController's timer would otherwise run behind
            //    the loading screen and the glow would already be lit the instant the hole
            //    is revealed — which is exactly what it did before this call existed.
            //
            //    Null-safe and no-ops when there is no live controller, so the physics-lab
            //    and bot launchers that never run this loader keep working unchanged.
            Golfin.Gameplay.UI.ShotUI.TeeIdleGlowController.NotifyOtherInteraction();
        }


        /// <summary>
        /// Unloads every gameplay scene: any Hole_NN_Geo, then the LabScaffold host.
        /// Shared by the menu teardown and by LoadCoroutine, because loading a hole is
        /// only safe from a clean slate — see the comment at LoadCoroutine step 2b.
        /// </summary>
        IEnumerator UnloadGameplayScenes()
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                if (s.name != null && s.name.StartsWith(HOLE_GEO_SCENE_PREFIX) && s.name.EndsWith(HOLE_GEO_SCENE_SUFFIX))
                {
                    var op = SceneManager.UnloadSceneAsync(s);
                    while (op != null && !op.isDone) yield return null;
                }
            }

            var hostScene = SceneManager.GetSceneByName(GAMEPLAY_SCENE_NAME);
            if (hostScene.IsValid() && hostScene.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(hostScene);
                while (op != null && !op.isDone) yield return null;
            }
        }

        /// <summary>
        /// Stage D entry point. Tears down gameplay scenes and restores the shell.
        /// </summary>
        public IEnumerator UnloadGameplay()
        {
            // Returning to the menu ends the run, so the next one rolls a fresh sky.
            // Playing Next Hole never comes through here, which is exactly why the sky
            // stays put across a whole round.
            Golfin.Gameplay.Environment.SkyRandomizer.EndRun();

            yield return UnloadGameplayScenes();

            // Restore bottom nav for the shell.
            if (persistentUI != null) persistentUI.SetBottomNavVisible(true);
        }

        /// <summary>
        /// Leave gameplay for a shell screen with nothing ugly on show in between.
        ///
        /// <see cref="UnloadGameplay"/> alone is not enough: unloading Hole_NN_Geo and
        /// LabScaffold takes several frames, and the moment LabScaffold goes there is
        /// nothing left to render — the player watches the bare shell scene (empty camera
        /// clear, no UI) until the target screen finally appears. Worse, ScreenManager's
        /// own fade then starts FROM that empty frame, so it is on screen for the unload
        /// plus another fade-out.
        ///
        /// So: curtain down over the live hole, tear down and swap behind it, curtain up
        /// on the target screen. <paramref name="onTornDown"/> runs while the screen is
        /// still black, for the session/context resets the target screen expects.
        /// </summary>
        public IEnumerator ExitToScreen(ScreenId target, System.Action onTornDown = null)
        {
            var fade = FadeController.Instance;
            if (fade != null) yield return fade.CurtainDown();

            yield return UnloadGameplay();

            onTornDown?.Invoke();

            if (ScreenManager.Instance != null)
            {
                // instant: the curtain already owns the black. A non-instant ShowScreen
                // would run a second fade system on top of it, re-blackening a screen that
                // is about to be revealed.
                ScreenManager.Instance.ShowScreen(target, instant: true);
            }
            else
            {
                Debug.LogWarning($"[GameplaySceneLoader] ScreenManager.Instance is null - cannot route to {target}.");
            }

            if (fade != null) yield return fade.CurtainUp();
        }
    }
}
