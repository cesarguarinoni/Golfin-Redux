// BUILD-FIX 2026-07-27: whole-file editor-only guard. This capture bot is dev/diagnostics
// tooling injected at play-mode by Bot/Editor/VersusHudCaptureMenu (an Editor-only asmdef)
// and calls #if UNITY_EDITOR debug seams on VersusHudController. It must never compile into
// a player build. Not serialized in any scene/prefab, so excluding it from the player is safe.
#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Diagnostics.Runtime;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Play-mode coroutine bot for the "nav_menu_to_opponent_turn" video deliverable.
    ///
    /// Records the FULL production navigation flow (Defect-6, iter-4):
    ///   Home → Mode Select → 1v1 PLAY → Matchmaking (OPPONENT FOUND) →
    ///   in-game HUD with both cards full (P1 active) → opponent's turn trigger.
    ///
    /// Injected at EnteredPlayMode by VersusHudCaptureMenu when scenario="nav_menu_to_opponent_turn".
    /// Uses reflection to drive GolfinRedux.UI.ScreenManager and
    /// Golfin.UI.Matchmaking.MatchmakingModalController without requiring a hard asmdef reference
    /// (mirrors TapFeedbackDemoRecorder pattern).
    ///
    /// Timeline (approximate):
    ///   0.0s  Play mode starts — ShellScene loaded (Logo → Splash → Loading → Home).
    ///   3.0s  Navigate to ModeSelection screen.
    ///   5.0s  Trigger 1v1 PLAY → matchmaking modal opens.
    ///         ModeSelectionScreen is immediately hidden so the modal is the only
    ///         visible UI (MatchMakingModal is at sibling index 8, Mode Select at 9 —
    ///         without hiding Mode Select, its opaque background covers the modal).
    ///   8.0s  Matchmaking completes ("OPPONENT FOUND") → 2s dwell → gameplay loads.
    ///  13.0s  LabScaffold + Hole scene loaded — both cards populated, banner shows.
    ///  17.0s  Trigger opponent's turn (debug swap) → "OPPONENT'S TURN" banner.
    ///  21.0s  Exit play mode.
    /// </summary>
    public class VersusHudNavCaptureBot : MonoBehaviour
    {
        public static bool Armed { get; set; }

        void Start()
        {
            if (!Armed) return;
            Armed = false;
            StartCoroutine(RunNav());
        }

        IEnumerator RunNav()
        {
            Debug.Log("[VersusHudNavBot] Starting nav sequence from ShellScene.");

            // ── Phase 1: Let ShellScene initialise (Logo → Splash → Loading → Home) ──
            // ShellScene runs through Logo/Splash/Loading automatically.
            yield return new WaitForSecondsRealtime(3.0f);

            // ── Phase 2: Navigate to ModeSelection ───────────────────────────────────
            Debug.Log("[VersusHudNavBot] Phase 2 — navigating to ModeSelection.");
            bool navigated = TryShowScreen("ModeSelection");
            if (!navigated)
            {
                Debug.LogWarning("[VersusHudNavBot] ShowScreen(ModeSelection) failed — trying Home first.");
                TryShowScreen("Home");
                yield return new WaitForSecondsRealtime(1.0f);
                TryShowScreen("ModeSelection");
            }

            // Hold on Mode Select screen briefly so the viewer can see it.
            yield return new WaitForSecondsRealtime(2.0f);

            // ── Phase 3: Trigger 1v1 PLAY (matchmaking modal) ────────────────────────
            Debug.Log("[VersusHudNavBot] Phase 3 — triggering 1v1 matchmaking.");
            // Set IsVersus before opening modal (mirrors ModeCarouselController path).
            GameSession.IsVersus = true;

            bool opened = TryOpenMatchmakingModal(0); // hole index 0 (Hole 1)
            if (!opened)
            {
                Debug.LogWarning("[VersusHudNavBot] MatchmakingModalController.Open() call failed.");
            }
            // R2-5 fix (iter-7): Do NOT deactivate ModeSelectionScreen.
            // ModalController.Show() calls transform.SetAsLastSibling() which moves the
            // modal's root to the last sibling position, making it render ABOVE Mode Select
            // in the same-sortOrder ScreenSpaceOverlay canvas. Mode Select stays ACTIVE
            // and renders (dimmed by the modal's semi-transparent backdrop) behind the modal —
            // which is the spec requirement. Previously HideModeSelectionScreen() was called
            // as a workaround but it made the background empty (SELF_REVIEW_FAIL iter-6 R2-5).
            else
            {
                Debug.Log("[VersusHudNavBot] Modal opened — Mode Select stays visible behind backdrop (R2-5 fix).");
            }

            // Hold on matchmaking "SEARCHING" state for 1.5s so the viewer sees it.
            yield return new WaitForSecondsRealtime(1.5f);
            CaptureCore.SnapPlayModeSafe("nav_matchmaking_searching");
            Debug.Log("[VersusHudNavBot] Captured: matchmaking SEARCHING state.");

            // Wait for the matchmaking modal to reach "OPPONENT FOUND" phase.
            // searchDurationSeconds defaults to 5f, so we poll for the phase change.
            // (DEFECT-6 FIX iter-5: dwell 2s on OPPONENT FOUND so the modal is
            //  clearly visible in the recorded video — self-reviewer flagged no
            //  sampled frame showed OPPONENT FOUND in the iter-4 delivery.)
            float opponentFoundWait = 0f;
            const float opponentFoundTimeout = 15f;
            bool opponentFoundVisible = false;
            while (opponentFoundWait < opponentFoundTimeout)
            {
                if (IsMatchmakingOpponentFound())
                {
                    opponentFoundVisible = true;
                    Debug.Log("[VersusHudNavBot] OPPONENT FOUND phase reached.");
                    break;
                }
                yield return new WaitForSecondsRealtime(0.25f);
                opponentFoundWait += 0.25f;
            }

            if (opponentFoundVisible)
            {
                // Dwell 2 full seconds on OPPONENT FOUND so the modal is plainly
                // visible in the recorded video (Defect-6 fix, iter-5).
                yield return new WaitForSecondsRealtime(2.0f);
                CaptureCore.SnapPlayModeSafe("nav_opponent_found");
                Debug.Log("[VersusHudNavBot] Captured: OPPONENT FOUND modal (2s dwell).");
            }
            else
            {
                Debug.LogWarning("[VersusHudNavBot] OPPONENT FOUND phase not reached within timeout — proceeding.");
                CaptureCore.SnapPlayModeSafe("nav_opponent_found_timeout");
            }

            // ── Phase 4: Wait for gameplay scene to load ────────────────────────────
            // The matchmaking controller automatically completes after searchDurationSeconds
            // (typically ~3s) and calls GameplaySceneLoader to load the gameplay scene.
            Debug.Log("[VersusHudNavBot] Phase 4 — waiting for gameplay scene load.");

            // Wait for LabScaffold to appear in the loaded scenes list.
            float waitStart = Time.realtimeSinceStartup;
            const float maxWait = 20f;
            bool gameplayLoaded = false;
            while (Time.realtimeSinceStartup - waitStart < maxWait)
            {
                if (IsSceneLoaded("LabScaffold"))
                {
                    gameplayLoaded = true;
                    Debug.Log("[VersusHudNavBot] LabScaffold loaded.");
                    break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }

            if (!gameplayLoaded)
            {
                Debug.LogWarning("[VersusHudNavBot] LabScaffold did not load within timeout. Proceeding anyway.");
            }

            // Wait an additional 3s for all HUD elements to initialise and both cards to populate.
            yield return new WaitForSecondsRealtime(3.0f);
            CaptureCore.SnapPlayModeSafe("nav_both_cards_full");
            Debug.Log("[VersusHudNavBot] Captured: both cards full at game entry.");

            // ── Phase 5: Trigger opponent's turn ─────────────────────────────────────
            Debug.Log("[VersusHudNavBot] Phase 5 — triggering opponent's turn.");
            var versusCtrl = FindFirstObjectByType<VersusHudController>();
            if (versusCtrl != null)
            {
                versusCtrl.DebugSwapTurn();
            }
            else
            {
                // Fallback: directly set active index via MatchContext.
                MatchContext.SetActive(1);
                Debug.LogWarning("[VersusHudNavBot] VersusHudController not found — used MatchContext.SetActive(1) directly.");
            }

            // Wait for "OPPONENT'S TURN" banner to play through (slide-in 0.25s + hold 1.2s + fade 0.3s = 1.75s).
            yield return new WaitForSecondsRealtime(2.5f);
            CaptureCore.SnapPlayModeSafe("nav_opponents_turn");
            Debug.Log("[VersusHudNavBot] Captured: opponent's turn banner + opacity swap.");

            // Brief hold at end so the viewer can see the final state.
            yield return new WaitForSecondsRealtime(1.0f);

            Debug.Log("[VersusHudNavBot] Nav sequence complete — exiting play mode.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#endif
        }

        // ── Reflection helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Calls ScreenManager.ShowScreen(ScreenId.{screenName}) via reflection.
        /// Returns true if the call succeeded.
        /// </summary>
        static bool TryShowScreen(string screenIdName)
        {
            try
            {
                var smType = FindType("GolfinRedux.UI", "ScreenManager");
                if (smType == null) { Debug.LogWarning("[VersusHudNavBot] ScreenManager type not found."); return false; }

                var instanceProp = smType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProp == null) { Debug.LogWarning("[VersusHudNavBot] ScreenManager.Instance not found."); return false; }

                var sm = instanceProp.GetValue(null);
                if (sm == null) { Debug.LogWarning("[VersusHudNavBot] ScreenManager.Instance is null."); return false; }

                var screenIdType = FindType("GolfinRedux.UI", "ScreenId");
                if (screenIdType == null) { Debug.LogWarning("[VersusHudNavBot] ScreenId enum not found."); return false; }

                object screenIdValue = System.Enum.Parse(screenIdType, screenIdName);

                // Try the (ScreenId, bool) overload first, then (ScreenId) only.
                var showMethod = smType.GetMethod("ShowScreen",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                    null,
                    new System.Type[] { screenIdType, typeof(bool) },
                    null);
                if (showMethod != null)
                {
                    showMethod.Invoke(sm, new object[] { screenIdValue, false });
                }
                else
                {
                    showMethod = smType.GetMethod("ShowScreen",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (showMethod == null) { Debug.LogWarning("[VersusHudNavBot] ShowScreen method not found."); return false; }
                    showMethod.Invoke(sm, new object[] { screenIdValue });
                }

                Debug.Log($"[VersusHudNavBot] ShowScreen({screenIdName}) called.");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[VersusHudNavBot] TryShowScreen({screenIdName}) failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Finds MatchmakingModalController in the scene and calls Open(holeIndex) on it.
        /// Returns true if the call succeeded.
        /// </summary>
        static bool TryOpenMatchmakingModal(int holeIndex)
        {
            try
            {
                var modalType = FindType("Golfin.UI.Matchmaking", "MatchmakingModalController");
                if (modalType == null) { Debug.LogWarning("[VersusHudNavBot] MatchmakingModalController type not found."); return false; }

                var objs = FindObjectsByType(modalType, FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (objs.Length == 0) { Debug.LogWarning("[VersusHudNavBot] MatchmakingModalController not found in scene."); return false; }

                var modal = objs[0];

                var openMethod = modalType.GetMethod("Open",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                    null,
                    new System.Type[] { typeof(int) },
                    null);
                if (openMethod == null) { Debug.LogWarning("[VersusHudNavBot] MatchmakingModalController.Open(int) not found."); return false; }

                openMethod.Invoke(modal, new object[] { holeIndex });
                Debug.Log($"[VersusHudNavBot] MatchmakingModalController.Open({holeIndex}) called.");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[VersusHudNavBot] TryOpenMatchmakingModal failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deactivates the ModeSelectionScreen GameObject so the matchmaking modal
        /// is not covered by its opaque background.
        ///
        /// Root cause (iter-5): MatchMakingModal is at ScreensRoot sibling index 8,
        /// ModeSelectionScreen at index 9. Because Mode Select renders AFTER the modal
        /// in transform order (higher sibling index = renders on top in same-sortOrder
        /// ScreenSpaceOverlay canvas), its opaque background covers the modal even when
        /// the modal is SetActive(true). Directly deactivating Mode Select exposes the
        /// modal to the camera for the video capture.
        ///
        /// This is safe for the capture-bot use-case because the bot exits play mode
        /// after the scenario — the scene state is not persisted.
        /// </summary>
        static void HideModeSelectionScreen()
        {
            try
            {
                // Find ScreensRoot/ModeSelectionScreen by searching all root GOs in the scene.
                var screensRootGo = GameObject.Find("ScreensRoot");
                if (screensRootGo == null)
                {
                    // Try Canvas/ScreensRoot.
                    var canvas = GameObject.Find("Canvas");
                    if (canvas != null)
                    {
                        var sr = canvas.transform.Find("ScreensRoot");
                        if (sr != null) screensRootGo = sr.gameObject;
                    }
                }

                if (screensRootGo == null)
                {
                    Debug.LogWarning("[VersusHudNavBot] ScreensRoot not found — cannot hide ModeSelectionScreen.");
                    return;
                }

                var modeSelTransform = screensRootGo.transform.Find("ModeSelectionScreen");
                if (modeSelTransform == null)
                {
                    // Fallback: search all children for a GO with "ModeSelection" in the name.
                    for (int i = 0; i < screensRootGo.transform.childCount; i++)
                    {
                        var child = screensRootGo.transform.GetChild(i);
                        if (child.name.Contains("ModeSelection"))
                        {
                            modeSelTransform = child;
                            break;
                        }
                    }
                }

                if (modeSelTransform == null)
                {
                    Debug.LogWarning("[VersusHudNavBot] ModeSelectionScreen child not found under ScreensRoot.");
                    return;
                }

                modeSelTransform.gameObject.SetActive(false);
                Debug.Log("[VersusHudNavBot] ModeSelectionScreen deactivated — matchmaking modal now visible.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[VersusHudNavBot] HideModeSelectionScreen() failed: {ex.Message}");
            }
        }

        /// <summary>Searches all loaded assemblies for a type by namespace + name.</summary>
        static System.Type FindType(string ns, string typeName)
        {
            string fullName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        static bool IsSceneLoaded(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && s.name == name) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true when MatchmakingModalController.Phase == OpponentFound.
        /// Uses reflection because MatchmakingModalController lives in Assembly-CSharp
        /// which is not directly referenced by Golfin.Physics.Viewer.asmdef.
        /// DEFECT-6 FIX (iter-5): used to poll until OPPONENT FOUND is displayed
        /// so the nav video clearly shows the modal at that state.
        /// </summary>
        static bool IsMatchmakingOpponentFound()
        {
            try
            {
                var modalType = FindType("Golfin.UI.Matchmaking", "MatchmakingModalController");
                if (modalType == null) return false;

                // Find the phase property.
                var phaseProp = modalType.GetProperty("Phase",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (phaseProp == null) return false;

                // Get the MatchmakingPhase enum type and the "OpponentFound" value.
                var phaseEnumType = phaseProp.PropertyType;
                object opponentFoundValue;
                try
                {
                    opponentFoundValue = System.Enum.Parse(phaseEnumType, "OpponentFound");
                }
                catch
                {
                    return false;
                }

                var objs = FindObjectsByType(modalType, FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (objs.Length == 0) return false;

                object currentPhase = phaseProp.GetValue(objs[0]);
                return currentPhase != null && currentPhase.Equals(opponentFoundValue);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[VersusHudNavBot] IsMatchmakingOpponentFound() failed: {ex.Message}");
                return false;
            }
        }
    }
}
#endif // UNITY_EDITOR
