using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using GolfinRedux.UI;
using Golfin.UI;
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
        /// Entry point. MatchmakingModalController calls this after seeding GameSession
        /// and after its fade-out completes. The hole number is read off GameSession
        /// (passed in for explicitness and testability).
        /// </summary>
        public void BeginGameplayLoad(int holeNumber)
        {
            StartCoroutine(LoadCoroutine(holeNumber));
        }

        /// <summary>
        /// Synchronous prelude — extracted so EditMode tests can verify the immediate
        /// side effects of BeginGameplayLoad without ticking coroutine frames or running
        /// scene async loads. Called as the first action of LoadCoroutine.
        /// </summary>
        internal void ApplyPreloadSetup(int holeNumber)
        {
            if (loadingScreen != null) loadingScreen.PrepareForHoleLoad(holeNumber);
            if (ScreenManager.Instance != null) ScreenManager.Instance.ShowScreen(ScreenId.Loading);

            // Belt-and-suspenders: ScreenManager.ApplyScreen already hides bars on Loading,
            // but call SetBottomNavVisible(false) explicitly so the contract is enforced
            // even if the loading screen transition is bypassed in tests / dev tools.
            if (persistentUI != null) persistentUI.SetBottomNavVisible(false);
        }

        IEnumerator LoadCoroutine(int holeNumber)
        {
            // 1. Synchronous prelude: prepare loading screen, navigate, hide bottom nav.
            ApplyPreloadSetup(holeNumber);

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
            }

            // 6. Loading screen finishes — hands off to gameplay. Does NOT navigate to Home;
            //    the loading screen GameObject is just hidden so the additive gameplay scene
            //    becomes visible.
            if (loadingScreen != null)
                yield return loadingScreen.FinishLoadingCoroutine();
        }

        /// <summary>
        /// Stage D entry point. Tears down gameplay scenes and restores the shell.
        /// </summary>
        public IEnumerator UnloadGameplay()
        {
            // Unload any loaded Hole_NN_Geo scene first.
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

            // Unload the gameplay host scene.
            var hostScene = SceneManager.GetSceneByName(GAMEPLAY_SCENE_NAME);
            if (hostScene.IsValid() && hostScene.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(hostScene);
                while (op != null && !op.isDone) yield return null;
            }

            // Restore bottom nav for the shell.
            if (persistentUI != null) persistentUI.SetBottomNavVisible(true);
        }
    }
}
