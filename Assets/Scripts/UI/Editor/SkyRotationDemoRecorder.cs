#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using GolfinRedux.UI.HoleSelection;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Environment;
using Golfin.UI.GameplayTransition;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Full-run demo of the per-run sky rotation, driven entirely through real player
    /// entry points.
    ///
    /// Sequence:
    ///   boot → Splash PLAY button → Home → Hole Selection → hole card action button →
    ///   Hole 1 loads (sky rolled)            [ACT 1]
    ///   GameSession.MarkHoleComplete (the production hole-end call) → result modal →
    ///   the modal's real PLAY button → Hole 2 loads (SAME sky)   [ACT 2]
    ///   UnloadGameplay (the MENU/quit teardown, where EndRun lives) → Home →
    ///   Hole 1 again (NEW sky)                                    [ACT 3]
    ///
    /// Output raw: tasks/sky_rotation_demo/video/raw.mp4
    /// Final:      Docs/Reports/Media/sky_rotation_demo.mp4 (after captioning)
    /// Usage: GOLFIN > Environment > Record Sky Rotation Demo Video
    /// </summary>
    public static class SkyRotationDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string RawOutputDir   = "tasks/sky_rotation_demo/video";
        const string FinalOutputDir = "Docs/Reports/Media";
        const string ArmedKey       = "SkyRotationDemoRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Environment/Record Sky Rotation Demo Video")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SkyDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(RawOutputDir);
            Directory.CreateDirectory(FinalOutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[SkyDemo] Armed. Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (!SessionState.GetBool(ArmedKey, false)) return;
                SessionState.SetBool(ArmedKey, false);
                SpawnBotOnly();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                if (_recorder != null) StopRecorderAndCopy();
            }
        }

        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                var asm = Assembly.Load("Golfin.Physics.Viewer.Bot.Editor");
                var t   = asm?.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                var m   = t?.GetMethod("EnsureIPhone14Selected", BindingFlags.Public | BindingFlags.Static);
                return m != null && (bool)m.Invoke(null, null);
            }
            catch { return false; }
        }

        static void SpawnBotOnly()
        {
            if (!TryEnsureIPhone14Selected())
                Debug.LogWarning("[SkyDemo] Could not pin iPhone-14 Game View size — will record at current resolution.");

            var host = new GameObject("[SkyRotationDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<SkyRotationDemoRunner>();
            Debug.Log("[SkyDemo] Bot spawned.");
        }

        public static void StartRecorder()
        {
            bool selected = TryEnsureIPhone14Selected();
            int w = 1170, h = 2532;
            if (!selected)
            {
                PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
                if (cw > 0 && ch > 0)
                {
                    w = Mathf.Max(2, (int)cw); h = Mathf.Max(2, (int)ch);
                    if (w % 2 != 0) w--; if (h % 2 != 0) h--;
                    Debug.LogWarning($"[SkyDemo] Recording at {w}x{h} (not iPhone-14).");
                }
            }

            // Lock render state BEFORE StartRecording — Y-flip guard (BotVideoRecorder pattern).
            QualitySettings.vSyncCount  = 0;
            Application.targetFrameRate = 30;
            Application.runInBackground  = true;   // else frames stall when the editor loses focus

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "SkyRotationDemo";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = $"{RawOutputDir}/raw";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            Debug.Log($"[SkyDemo] Recording started → {RawOutputDir}/raw.mp4 ({w}x{h} @ 30fps)");
        }

        static void StopRecorderAndCopy()
        {
            if (_recorder == null) return;
            try
            {
                if (_recorder.IsRecording()) _recorder.StopRecording();
                Debug.Log("[SkyDemo] Recording stopped.");
            }
            catch (Exception e) { Debug.LogWarning($"[SkyDemo] StopRecorder: {e.Message}"); }
            _recorder = null;

            string src = Path.GetFullPath($"{RawOutputDir}/raw.mp4");
            if (File.Exists(src))
                Debug.Log($"[SkyDemo] Raw at {src} ({new FileInfo(src).Length / 1048576f:0.0} MB)");
            else
                Debug.LogWarning($"[SkyDemo] raw.mp4 NOT FOUND at {src}");
        }
    }

    /// <summary>Runtime coroutine driver for the sky rotation demo.</summary>
    public class SkyRotationDemoRunner : MonoBehaviour
    {
        void Start() => StartCoroutine(Sequence());

        static string GetCurrentScreenName()
        {
            try
            {
                foreach (var m in FindObjectsOfType<MonoBehaviour>())
                {
                    if (m.GetType().Name != "ScreenManager") continue;
                    var f = m.GetType().GetField("_currentScreen", BindingFlags.NonPublic | BindingFlags.Instance);
                    return f?.GetValue(m)?.ToString();
                }
            }
            catch { }
            return null;
        }

        static Button FindButtonByName(string name)
        {
            foreach (var b in FindObjectsOfType<Button>(false))
                if (b.gameObject.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return b;
            return null;
        }

        static Button FindButtonByText(string text)
        {
            string lower = text.ToLowerInvariant();
            foreach (var b in FindObjectsOfType<Button>(false))
                foreach (var t in b.GetComponentsInChildren<TMPro.TMP_Text>(false))
                    if (t.text != null && t.text.ToLowerInvariant().Contains(lower)) return b;
            return null;
        }

        IEnumerator NavigateToHome(float timeout = 15f)
        {
            float e = 0f;
            while (e < timeout)
            {
                string cur = GetCurrentScreenName();
                if (cur == "Home") { Debug.Log("[SkyDemoBot] At Home."); yield break; }
                if (cur == "Splash")
                {
                    var btn = FindButtonByName("StartButton") ?? FindButtonByText("PLAY") ?? FindButtonByText("START");
                    if (btn != null)
                    {
                        btn.onClick.Invoke();     // REAL player entry point
                        Debug.Log($"[SkyDemoBot] Clicked '{btn.gameObject.name}' on Splash.");
                        yield return new WaitForSecondsRealtime(2f); e += 2f; continue;
                    }
                }
                yield return new WaitForSecondsRealtime(0.5f); e += 0.5f;
            }
            // The build currently boots into the starter-character screen, which this bot
            // does not drive. Route to Home explicitly rather than idling for the timeout.
            Debug.Log("[SkyDemoBot] Not at Home via Splash — routing to Home directly.");
            yield return ShowScreenNamed("Home");
        }

        IEnumerator ShowScreenNamed(string screenId)
        {
            foreach (var mono in FindObjectsOfType<MonoBehaviour>())
            {
                if (mono.GetType().Name != "ScreenManager") continue;
                var sidType = mono.GetType().Assembly.GetType("GolfinRedux.UI.ScreenId");
                if (sidType == null) break;
                var val = Enum.Parse(sidType, screenId);
                var mi = mono.GetType().GetMethod("ShowScreen", new[] { sidType, typeof(bool) })
                      ?? mono.GetType().GetMethod("ShowScreen", new[] { sidType });
                if (mi != null)
                {
                    mi.Invoke(mono, mi.GetParameters().Length == 2
                        ? new object[] { val, false } : new object[] { val });
                    Debug.Log($"[SkyDemoBot] ShowScreen({screenId}).");
                }
                break;
            }
            yield return new WaitForSecondsRealtime(2f);
        }

        IEnumerator ShowHoleSelection()
        {
            foreach (var mono in FindObjectsOfType<MonoBehaviour>())
            {
                if (mono.GetType().Name != "ScreenManager") continue;
                var sidType = mono.GetType().Assembly.GetType("GolfinRedux.UI.ScreenId");
                if (sidType == null) break;
                var val = Enum.Parse(sidType, "HoleSelection");
                var mi = mono.GetType().GetMethod("ShowScreen", new[] { sidType, typeof(bool) })
                      ?? mono.GetType().GetMethod("ShowScreen", new[] { sidType });
                if (mi != null)
                {
                    mi.Invoke(mono, mi.GetParameters().Length == 2
                        ? new object[] { val, false } : new object[] { val });
                    Debug.Log("[SkyDemoBot] ShowScreen(HoleSelection).");
                }
                break;
            }
            yield return new WaitForSecondsRealtime(2.5f);
        }

        /// <summary>
        /// Clicks a hole card's real action button, polling until it is live. On the second
        /// run the screen had not finished rebuilding its cards, so a single immediate probe
        /// found the button inactive and that act silently did nothing.
        /// </summary>
        IEnumerator ClickHoleCard(int holeNumber, float timeout = 20f)
        {
            var fi = typeof(HoleCardController)
                .GetField("actionButton", BindingFlags.NonPublic | BindingFlags.Instance);

            float e = 0f;
            while (e < timeout)
            {
                var cards = FindObjectsOfType<HoleCardController>(false);
                var target = cards.FirstOrDefault(c => c.HoleNumber == holeNumber);
                var btn = target != null ? fi?.GetValue(target) as Button : null;
                // activeInHierarchy only. An earlier version also required `interactable`,
                // which never became true and stalled the whole demo.
                if (btn != null && btn.gameObject.activeInHierarchy)
                {
                    btn.onClick.Invoke();          // REAL player entry point
                    Debug.Log($"[SkyDemoBot] Clicked hole card #{holeNumber} action button after {e:F1}s.");
                    yield return new WaitForSecondsRealtime(1.5f);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f); e += 0.5f;
            }

            // Fallback so a UI hiccup cannot leave the recording stuck on a dead screen.
            // Loudly logged: this is NOT the player's entry point, and any claim about the
            // real-entry path must be read against this line.
            Debug.LogWarning($"[SkyDemoBot] FALLBACK — hole {holeNumber} card button never went live " +
                             $"({timeout}s); calling BeginGameplayLoad directly. This act does NOT " +
                             "demonstrate the real entry path.");
            var loader = GameplaySceneLoader.Instance;
            if (loader != null)
            {
                // Seed first. The production card path does this; without it the HUD keeps
                // showing the PREVIOUS hole number over the newly loaded hole.
                GameSession.SetCurrentHole(holeNumber);
                loader.BeginGameplayLoad(holeNumber);
            }
            yield return new WaitForSecondsRealtime(1.5f);
        }

        /// <summary>
        /// Waits for a SPECIFIC hole scene. The first version matched any Hole_NN_Geo and so
        /// returned instantly on the hole already loaded, letting the sequence race ahead of
        /// the real transition.
        /// </summary>
        IEnumerator WaitForHoleGeo(int holeNumber, float timeout = 90f)
        {
            string want = $"Hole_{holeNumber:D2}_Geo";
            float e = 0f;
            while (e < timeout)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sc = SceneManager.GetSceneAt(i);
                    if (sc.isLoaded && sc.name == want)
                    {
                        Debug.Log($"[SkyDemoBot] '{want}' loaded after {e:F1}s.");
                        yield return new WaitForSecondsRealtime(1f);
                        yield break;
                    }
                }
                yield return new WaitForSecondsRealtime(0.5f); e += 0.5f;
            }
            Debug.LogWarning($"[SkyDemoBot] WaitForHoleGeo({want}) TIMEOUT");
        }


        /// <summary>
        /// Waits for the loading screen to appear and THEN disappear.
        /// The single-shot version asked "is a LoadingScreenController inactive?" and
        /// FindObjectsOfTypeAll happily returned an inactive instance on the first frame,
        /// so it returned in 0.0s and every "hold and look at the sky" beat still elapsed
        /// behind the loading screen. Two phases removes that whole class of false pass.
        /// </summary>
        IEnumerator WaitForGameplayVisible(float timeout = 40f)
        {
            float e = 0f;
            // Phase 1 — let it come up (it may already be up; that is fine).
            while (e < 10f && ActiveLoadingScreens() == 0)
            { yield return new WaitForSecondsRealtime(0.25f); e += 0.25f; }

            // Phase 2 — the real wait: gone means NO active loading screen anywhere.
            float e2 = 0f;
            while (e2 < timeout)
            {
                if (ActiveLoadingScreens() == 0)
                {
                    Debug.Log($"[SkyDemoBot] Gameplay visible after {e2:F1}s (loading screen down).");
                    yield return new WaitForSecondsRealtime(0.75f);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.25f); e2 += 0.25f;
            }
            Debug.LogWarning("[SkyDemoBot] Loading screen never hid — the sky may not be on screen.");
        }

        static int ActiveLoadingScreens()
            => FindObjectsByType<LoadingScreenController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;

        static void LogSky(string act)
        {
            var p = SkyRandomizer.Current;
            Debug.Log($"[SkyDemoBot] === {act}: sky = '{(p != null ? p.DisplayName : "<none>")}' " +
                      $"skybox='{(RenderSettings.skybox != null ? RenderSettings.skybox.name : "<null>")}' " +
                      $"sun={(RenderSettings.sun != null ? RenderSettings.sun.transform.eulerAngles.ToString("0.#") : "-")} ===");
        }

        IEnumerator Sequence()
        {
            yield return new WaitForSecondsRealtime(5f);
            Debug.Log("[SkyDemoBot] Boot complete.");

            // English: a previous session had left the game in Japanese.
            LocalizationManager.SetLanguage(Language.English);

            // Seeds picked so the two runs are obviously different even though the sky is
            // only ~15% of the player's frame: seed 3 -> Evening (warm), seed 1 -> Noon (blue).
            SkyRandomizer.EndRun();
            SkyRandomizer.SetRoundSeed(3);

            yield return NavigateToHome();
            yield return new WaitForSecondsRealtime(1f);

            SkyRotationDemoRecorder.StartRecorder();
            yield return new WaitForSecondsRealtime(1.5f);

            // ── ACT 1: first hole of the run ─────────────────────────────────
            yield return ShowHoleSelection();
            yield return ClickHoleCard(1);
            yield return WaitForHoleGeo(1);
            yield return WaitForGameplayVisible();
            yield return new WaitForSecondsRealtime(1.5f);
            // Seed the session the way the production hole-selection path does. Without this
            // CurrentHoleNumber stays 0, the result modal reads "Hole 0", and NEXT HOLE
            // computes 0+1 = 1 — which is why an earlier take reloaded hole 1 and looked
            // like a Next Hole bug.
            GameSession.SetCurrentHole(1);
            LogSky("ACT1 hole 1");
            string act1 = SkyRandomizer.Current != null ? SkyRandomizer.Current.DisplayName : "?";
            yield return new WaitForSecondsRealtime(5f);

            // ── ACT 2: NEXT HOLE via the result modal's real PLAY button ─────
            GameSession.MarkHoleComplete(
                new HoleCompletionData(BallState.InCup, 3, 0, GameSession.CurrentHoleNumber));
            Debug.Log("[SkyDemoBot] MarkHoleComplete fired — result modal should be up.");
            yield return new WaitForSecondsRealtime(4f);

            // Target Card 2's PLAY button EXPLICITLY. Both the "replay this hole" card and
            // the "next hole" card carry a button named PlayButton, and FindObjectsOfType
            // order is not deterministic — an earlier take hit the replay card and reloaded
            // hole 1 while still logging "NEXT HOLE".
            Button playBtn = null;
            var widget = Resources.FindObjectsOfTypeAll<Golfin.Gameplay.UI.ShotUI.HoleCompleteWidget>()
                .FirstOrDefault(w => w != null && !string.IsNullOrEmpty(w.gameObject.scene.name));
            if (widget != null && widget.Card2 != null)
            {
                playBtn = typeof(Golfin.Gameplay.UI.ShotUI.HoleCompleteCardWidget)
                    .GetField("_playButton", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(widget.Card2) as Button;
            }
            if (playBtn != null)
            {
                playBtn.onClick.Invoke();          // REAL Next Hole entry point (Card 2)
                Debug.Log("[SkyDemoBot] Clicked Card2 PLAY button (NEXT HOLE).");
            }
            else Debug.LogWarning("[SkyDemoBot] Could not resolve Card2's PLAY button.");

            // Hole 2 may not materialise (see report); fall through on timeout and let the
            // visibility wait decide when the sky is actually on screen.
            yield return WaitForHoleGeo(2, 12f);
            yield return WaitForGameplayVisible();
            yield return new WaitForSecondsRealtime(1.5f);
            LogSky("ACT2 next hole");
            string act2 = SkyRandomizer.Current != null ? SkyRandomizer.Current.DisplayName : "?";
            Debug.Log($"[SkyDemoBot] NEXT-HOLE CHECK: act1='{act1}' act2='{act2}' same={act1 == act2}");
            yield return new WaitForSecondsRealtime(5f);

            // ── ACT 3: back to menu (EndRun) then play again → new sky ───────
            var loader = GameplaySceneLoader.Instance;
            if (loader != null)
            {
                yield return loader.UnloadGameplay();   // the MENU teardown — calls EndRun()
                GameSession.ResetSession();
                Debug.Log("[SkyDemoBot] Quit to menu — run ended.");
            }
            yield return new WaitForSecondsRealtime(2f);

            SkyRandomizer.SetRoundSeed(1);              // a different match -> Noon
            // Route through Home first. Going straight back to HoleSelection left the cards
            // un-rebuilt and their action buttons inactive, which forced the fallback path.
            yield return ShowScreenNamed("Home");
            yield return new WaitForSecondsRealtime(2f);
            yield return ShowHoleSelection();
            yield return ClickHoleCard(1);              // SAME hole, new run
            yield return WaitForHoleGeo(1);
            yield return WaitForGameplayVisible();
            // Settle floor: the visibility probe has twice returned while the loading UI was
            // still drawn, which left the payoff shot ~2s long. This guarantees the new sky is
            // actually on screen before the hold below.
            yield return new WaitForSecondsRealtime(8f);
            LogSky("ACT3 new run, hole 1");
            string act3 = SkyRandomizer.Current != null ? SkyRandomizer.Current.DisplayName : "?";
            Debug.Log($"[SkyDemoBot] NEW-RUN CHECK: act1='{act1}' act3='{act3}' different={act1 != act3}");
            yield return new WaitForSecondsRealtime(8f);

            Debug.Log($"[SkyDemoBot] DONE. act1='{act1}' act2='{act2}' act3='{act3}'");
            yield return new WaitForSecondsRealtime(0.5f);
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
