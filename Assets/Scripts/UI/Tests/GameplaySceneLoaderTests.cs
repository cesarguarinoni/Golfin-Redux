using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.UI.Tests
{
    /// <summary>
    /// Stage C0 — GameplaySceneLoader + LoadingScreenController unit tests.
    ///
    /// LoadingScreenController, PersistentUIManager, and GameplaySceneLoader live in
    /// the predefined Assembly-CSharp assembly (no asmdef), which Unity disallows as a
    /// reference from another asmdef. Rather than refactoring half the UI layer into
    /// new asmdefs, these tests use reflection to access the types. The actual
    /// invariants under test (Target enum value, hole number, bottom-nav active state)
    /// are still exercised end-to-end via the real production code paths.
    ///
    /// Five EditMode tests verify:
    ///  1. BeginGameplayLoad prelude hides the bottom nav.
    ///  2. BeginGameplayLoad prelude prepares the loading screen with the seeded hole number.
    ///  3. UnloadGameplay restores the bottom nav (drained in test, no real scenes loaded).
    ///  4. LoadingScreenController.PrepareForHoleLoad sets Target=HoleLoad + TargetHoleNumber.
    ///  5. LoadingScreenController.ClearTarget resets back to LegacyBootHome.
    ///  6. ExitToScreen is self-hosting (returns a Coroutine started on the loader).
    ///  7. No production caller unloads gameplay outside ExitToScreen — the source lint
    ///     that keeps the empty-scene gap from coming back in a new exit path.
    /// </summary>
    public class GameplaySceneLoaderTests
    {
        // Type handles resolved once per test session.
        Type _loadingScreenType;
        Type _loadTargetType;
        Type _persistentUIType;
        Type _gameplayLoaderType;

        GameObject _loaderGO;
        GameObject _loadingScreenGO;
        GameObject _persistentUIGO;
        GameObject _bottomNavPanel;

        MonoBehaviour _loader;
        MonoBehaviour _loadingScreen;
        MonoBehaviour _persistentUI;

        [SetUp]
        public void SetUp()
        {
            _loadingScreenType  = Type.GetType("LoadingScreenController, Assembly-CSharp");
            _loadTargetType     = Type.GetType("LoadingScreenController+LoadTarget, Assembly-CSharp");
            _persistentUIType   = Type.GetType("Golfin.UI.PersistentUIManager, Assembly-CSharp");
            _gameplayLoaderType = Type.GetType("Golfin.UI.GameplayTransition.GameplaySceneLoader, Assembly-CSharp");

            Assert.IsNotNull(_loadingScreenType,  "LoadingScreenController must resolve from Assembly-CSharp.");
            Assert.IsNotNull(_loadTargetType,     "LoadingScreenController+LoadTarget enum must resolve.");
            Assert.IsNotNull(_persistentUIType,   "Golfin.UI.PersistentUIManager must resolve from Assembly-CSharp.");
            Assert.IsNotNull(_gameplayLoaderType, "Golfin.UI.GameplayTransition.GameplaySceneLoader must resolve.");

            _bottomNavPanel = new GameObject("BottomNavPanel_Test");
            _bottomNavPanel.SetActive(true);

            _persistentUIGO = new GameObject("PersistentUI_Test");
            _persistentUI = (MonoBehaviour)_persistentUIGO.AddComponent(_persistentUIType);
            // Assign bottomNavPanel field (public on PersistentUIManager).
            var navField = _persistentUIType.GetField("bottomNavPanel",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(navField, "PersistentUIManager.bottomNavPanel field must exist.");
            navField.SetValue(_persistentUI, _bottomNavPanel);

            _loadingScreenGO = new GameObject("LoadingScreen_Test");
            _loadingScreenGO.SetActive(false);  // keep inactive so OnEnable doesn't fire BeginLoading
            _loadingScreen = (MonoBehaviour)_loadingScreenGO.AddComponent(_loadingScreenType);

            _loaderGO = new GameObject("GameplaySceneLoader_Test");
            _loader = (MonoBehaviour)_loaderGO.AddComponent(_gameplayLoaderType);

            // Wire SerializeFields on the loader (Awake auto-wire may have grabbed nothing
            // useful; ensure deterministic test wiring).
            var loadingField = _gameplayLoaderType.GetField("loadingScreen",
                BindingFlags.Instance | BindingFlags.NonPublic);
            loadingField.SetValue(_loader, _loadingScreen);
            var persistField = _gameplayLoaderType.GetField("persistentUI",
                BindingFlags.Instance | BindingFlags.NonPublic);
            persistField.SetValue(_loader, _persistentUI);
        }

        [TearDown]
        public void TearDown()
        {
            if (_loaderGO != null)        UnityEngine.Object.DestroyImmediate(_loaderGO);
            if (_loadingScreenGO != null) UnityEngine.Object.DestroyImmediate(_loadingScreenGO);
            if (_persistentUIGO != null)  UnityEngine.Object.DestroyImmediate(_persistentUIGO);
            if (_bottomNavPanel != null)  UnityEngine.Object.DestroyImmediate(_bottomNavPanel);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        void InvokeApplyPreloadSetup(int holeNumber)
        {
            var mi = _gameplayLoaderType.GetMethod("ApplyPreloadSetup",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(mi, "ApplyPreloadSetup must exist on GameplaySceneLoader.");
            mi.Invoke(_loader, new object[] { holeNumber });
        }

        System.Collections.IEnumerator InvokeUnloadGameplay()
        {
            // NonPublic: UnloadGameplay is deliberately private so every exit goes through
            // ExitToScreen (see NoProductionCallerUnloadsGameplayDirectly).
            var mi = _gameplayLoaderType.GetMethod("UnloadGameplay",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(mi, "UnloadGameplay must exist on GameplaySceneLoader.");
            return (System.Collections.IEnumerator)mi.Invoke(_loader, null);
        }

        object GetTarget(MonoBehaviour lsc)
        {
            var prop = _loadingScreenType.GetProperty("Target",
                BindingFlags.Instance | BindingFlags.Public);
            return prop.GetValue(lsc);
        }

        int GetTargetHoleNumber(MonoBehaviour lsc)
        {
            var prop = _loadingScreenType.GetProperty("TargetHoleNumber",
                BindingFlags.Instance | BindingFlags.Public);
            return (int)prop.GetValue(lsc);
        }

        void InvokePrepareForHoleLoad(MonoBehaviour lsc, int holeNumber)
        {
            var mi = _loadingScreenType.GetMethod("PrepareForHoleLoad",
                BindingFlags.Instance | BindingFlags.Public);
            mi.Invoke(lsc, new object[] { holeNumber });
        }

        void InvokeClearTarget(MonoBehaviour lsc)
        {
            var mi = _loadingScreenType.GetMethod("ClearTarget",
                BindingFlags.Instance | BindingFlags.Public);
            mi.Invoke(lsc, null);
        }

        // ── Test 1: BeginGameplayLoad hides the bottom nav (via ApplyPreloadSetup) ──

        [Test]
        public void BeginGameplayLoad_HidesBottomNav()
        {
            Assert.IsTrue(_bottomNavPanel.activeSelf,
                "Sanity: bottom nav should start active before BeginGameplayLoad.");

            InvokeApplyPreloadSetup(1);

            Assert.IsFalse(_bottomNavPanel.activeSelf,
                "BeginGameplayLoad prelude must hide the bottom nav so it isn't visible in gameplay.");
        }

        // ── Test 2: BeginGameplayLoad prepares the loading screen with hole number ──

        [Test]
        public void BeginGameplayLoad_PreparesLoadingScreenWithHoleNumber()
        {
            InvokeApplyPreloadSetup(7);

            object target = GetTarget(_loadingScreen);
            int targetHole = GetTargetHoleNumber(_loadingScreen);

            // HoleLoad is the second enum value (0 = LegacyBootHome, 1 = HoleLoad).
            Assert.AreEqual(Enum.GetName(_loadTargetType, 1), target.ToString(),
                "After BeginGameplayLoad prelude, the loading screen must be in HoleLoad mode.");
            Assert.AreEqual(7, targetHole,
                "After BeginGameplayLoad prelude, the loading screen must know the seeded hole number.");
        }

        // ── Test 3: UnloadGameplay restores the bottom nav ──

        [Test]
        public void UnloadGameplay_RestoresBottomNav()
        {
            _bottomNavPanel.SetActive(false);
            Assert.IsFalse(_bottomNavPanel.activeSelf, "Sanity: bottom nav should be hidden pre-unload.");

            // With no Hole_NN_Geo or LabScaffold loaded in the test env, both unload
            // branches are no-ops; the final SetBottomNavVisible(true) at the end of the
            // method body fires before any yield is hit.
            var enumerator = InvokeUnloadGameplay();
            int safetyTicks = 8;
            while (enumerator.MoveNext() && safetyTicks-- > 0) { }

            Assert.IsTrue(_bottomNavPanel.activeSelf,
                "UnloadGameplay must restore the bottom nav after gameplay teardown.");
        }

        // ── Test 4: LoadingScreenController.PrepareForHoleLoad sets target + hole number ──

        [Test]
        public void LoadingScreenController_PrepareForHoleLoad_SetsTarget()
        {
            var go = new GameObject("LSC_Solo_Test");
            go.SetActive(false);  // avoid OnEnable
            var lsc = (MonoBehaviour)go.AddComponent(_loadingScreenType);

            InvokePrepareForHoleLoad(lsc, 5);

            object target = GetTarget(lsc);
            Assert.AreEqual(Enum.GetName(_loadTargetType, 1), target.ToString(),
                "PrepareForHoleLoad must set Target = HoleLoad.");
            Assert.AreEqual(5, GetTargetHoleNumber(lsc),
                "PrepareForHoleLoad must record the hole number.");

            UnityEngine.Object.DestroyImmediate(go);
        }

        // ── Test 5: LoadingScreenController.ClearTarget resets to legacy ──

        [Test]
        public void LoadingScreenController_ClearTarget_ResetsToLegacy()
        {
            var go = new GameObject("LSC_Solo_Test");
            go.SetActive(false);
            var lsc = (MonoBehaviour)go.AddComponent(_loadingScreenType);

            InvokePrepareForHoleLoad(lsc, 5);
            object midTarget = GetTarget(lsc);
            Assert.AreEqual(Enum.GetName(_loadTargetType, 1), midTarget.ToString(),
                "Sanity: target should be HoleLoad after PrepareForHoleLoad.");

            InvokeClearTarget(lsc);

            object endTarget = GetTarget(lsc);
            Assert.AreEqual(Enum.GetName(_loadTargetType, 0), endTarget.ToString(),
                "ClearTarget must reset Target back to LegacyBootHome.");
            Assert.AreEqual(0, GetTargetHoleNumber(lsc),
                "ClearTarget must zero out TargetHoleNumber.");

            UnityEngine.Object.DestroyImmediate(go);
        }

        // ── Test 6: ExitToScreen hosts its own coroutine ──

        [Test]
        public void ExitToScreen_IsSelfHosting()
        {
            var mi = _gameplayLoaderType.GetMethod("ExitToScreen",
                BindingFlags.Public | BindingFlags.Instance);

            Assert.IsNotNull(mi, "GameplaySceneLoader.ExitToScreen must exist — it is the only " +
                                 "sanctioned way to leave gameplay for a shell screen.");
            // Returning Coroutine (not IEnumerator) is what makes it self-hosting: the loader
            // lives in ShellScene and survives the unload, while several callers live in
            // LabScaffold and are destroyed halfway through. A caller-hosted teardown would
            // die with the curtain down and leave the screen black forever.
            Assert.AreEqual(typeof(Coroutine), mi.ReturnType,
                "ExitToScreen must return a Coroutine started on the loader, not an IEnumerator " +
                "the caller hosts — callers in LabScaffold are destroyed by the unload.");
        }

        // ── Tests 8-10: result_screen_nav_bars ──
        // The nav bars are live on the hole-complete result screen, so leaving gameplay is no
        // longer only the in-game QUIT's business: ScreenManager's gate keys off
        // IsGameplayLoaded, hands the navigation to ExitToScreen with ClearRunState, and the
        // result screen closes on GameplayExiting. These pin the seams the gate and the modal
        // depend on; the play-mode driver (ResultScreenNavVerifyBot) proves the whole path.

        [Test]
        public void IsGameplayLoaded_IsFalseWithoutLabScaffold()
        {
            var prop = _gameplayLoaderType.GetProperty("IsGameplayLoaded",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(prop, "GameplaySceneLoader.IsGameplayLoaded must exist — ScreenManager's " +
                                   "gameplay-exit gate keys off it.");
            Assert.IsFalse((bool)prop.GetValue(null),
                "No LabScaffold is loaded in an EditMode test, so a shell navigation must be a plain swap.");
        }

        [Test]
        public void ClearRunState_IsTheMenuContract()
        {
            var mi = _gameplayLoaderType.GetMethod("ClearRunState",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(mi, "GameplaySceneLoader.ClearRunState must exist — the one home for the " +
                                 "Stage D MENU reset, shared by the in-game QUIT and the nav-bar exit.");

            Golfin.Gameplay.Session.GameSession.SeedSession(7, "char_a", 0);
            Golfin.Gameplay.Session.GameSession.IsVersus = true;
            mi.Invoke(null, null);

            Assert.AreEqual(0, Golfin.Gameplay.Session.GameSession.CurrentHoleNumber,
                "ClearRunState must clear the hole pointer (GameSession.ResetSession).");
            Assert.IsFalse(Golfin.Gameplay.Session.GameSession.IsVersus,
                "ClearRunState must clear IsVersus.");
            Assert.AreEqual(1, Golfin.Gameplay.Session.GameSession.TurnCount,
                "ClearRunState must reset the per-hole state.");
        }

        [Test]
        public void GameplayExiting_IsAStaticEventRaisedByTheExit()
        {
            var ev = _gameplayLoaderType.GetEvent("GameplayExiting",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(ev, "GameplaySceneLoader.GameplayExiting must exist — the result screen " +
                                 "closes and settles on it.");
            // Raised from ExitRoutine, under the curtain and before the unload: the source is the
            // contract here, because the routine itself needs a FadeController and real scenes.
            string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "Scripts/UI/GameplayTransition/GameplaySceneLoader.cs"));
            int raise  = src.IndexOf("GameplayExiting?.Invoke()", StringComparison.Ordinal);
            int unload = src.IndexOf("yield return UnloadGameplay();", StringComparison.Ordinal);
            Assert.Greater(raise, 0, "ExitRoutine must raise GameplayExiting.");
            Assert.Less(raise, unload,
                "GameplayExiting must be raised BEFORE the unload — the result screen settles the " +
                "round while the hole is still there to settle.");
        }

        [Test]
        public void ShowBars_WithTitleKey_OwnsTheCentreTitleUntilTheNextHighlight()
        {
            // The top bar's centre label is a TextMeshProUGUI; the assembly is not referenced
            // here, so it is created and read by reflection like everything else in this file.
            var tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Assert.IsNotNull(tmpType, "TMPro.TextMeshProUGUI must resolve.");
            var labelGO = new GameObject("UsernameText_Test");
            labelGO.AddComponent<Canvas>();
            var label = labelGO.AddComponent(tmpType);
            var textProp = tmpType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            _persistentUIType.GetField("usernameText", BindingFlags.Public | BindingFlags.Instance)
                .SetValue(_persistentUI, label);
            var topBar = new GameObject("TopBar_Test");
            _persistentUIType.GetField("topBarPanel", BindingFlags.Public | BindingFlags.Instance)
                .SetValue(_persistentUI, topBar);

            try
            {
                var showBars = _persistentUIType.GetMethod("ShowBars", new[] { typeof(string) });
                Assert.IsNotNull(showBars, "PersistentUIManager.ShowBars(string titleKey) must exist — " +
                                           "the result screen's chrome call.");
                showBars.Invoke(_persistentUI, new object[] { "RESULT_RESULTS" });

                var loc = Type.GetType("LocalizationManager, Golfin.Localization");
                string expected = (string)loc.GetMethod("Get", new[] { typeof(string) }).Invoke(null, new object[] { "RESULT_RESULTS" });
                Assert.AreEqual(expected, (string)textProp.GetValue(label),
                    "ShowBars(titleKey) must paint the localized key as the centre title.");
                Assert.IsTrue(topBar.activeSelf, "ShowBars(titleKey) must show the top bar.");
                Assert.IsTrue(_bottomNavPanel.activeSelf, "ShowBars(titleKey) must show the bottom nav.");

                // A language change re-resolves the override, not the last highlighted screen.
                var refresh = _persistentUIType.GetMethod("RefreshTopBarCenterText",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                textProp.SetValue(label, "stale");
                refresh.Invoke(_persistentUI, null);
                Assert.AreEqual(expected, (string)textProp.GetValue(label),
                    "RefreshTopBarCenterText must re-apply the title-key override.");

                // Any real navigation forgets it: HighlightScreen(Home) paints the username.
                var screenIdType = Type.GetType("GolfinRedux.UI.ScreenId, Assembly-CSharp");
                object home = Enum.Parse(screenIdType, "Home");
                _persistentUIType.GetMethod("HighlightScreen", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(_persistentUI, new[] { home });
                var overrideField = _persistentUIType.GetField("_centerTitleKeyOverride",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNull(overrideField.GetValue(_persistentUI),
                    "HighlightScreen must clear the title-key override — the result's title must not " +
                    "outlive the result.");
                Assert.AreNotEqual(expected, (string)textProp.GetValue(label),
                    "After HighlightScreen(Home) the centre title is the username, not RESULTS.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(labelGO);
                UnityEngine.Object.DestroyImmediate(topBar);
            }
        }

        // ── Test 7: nobody tears gameplay down outside ExitToScreen ──

        [Test]
        public void NoProductionCallerUnloadsGameplayDirectly()
        {
            string scripts = System.IO.Path.Combine(Application.dataPath, "Scripts");
            var offenders = new System.Collections.Generic.List<string>();

            foreach (string file in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
            {
                string name = System.IO.Path.GetFileName(file);
                // The loader declares it; this test file names it in prose and reflection.
                if (name == "GameplaySceneLoader.cs" || name == "GameplaySceneLoaderTests.cs") continue;

                string[] lines = System.IO.File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (!line.Contains("UnloadGameplay(")) continue;
                    // Prose in a comment is fine — only real calls matter.
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("///") || trimmed.StartsWith("*")) continue;

                    offenders.Add($"{name}:{i + 1}: {trimmed}");
                }
            }

            Assert.IsEmpty(offenders,
                "Gameplay teardown must go through GameplaySceneLoader.ExitToScreen, which runs the " +
                "unload behind the black curtain and reveals the target screen. Calling UnloadGameplay " +
                "directly puts the empty shell scene on screen for the length of the unload. Offenders:\n" +
                string.Join("\n", offenders));
        }
    }
}
