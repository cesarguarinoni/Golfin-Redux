// TEMP: Matchmaking modal capture runner for iter2 screenshot.
// Deleted after screenshot is captured.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Golfin.Roster;
using Golfin.UI.Matchmaking;
using GolfinRedux.UI;

namespace Golfin.EditorTools
{
    [InitializeOnLoad]
    public static class MatchmakingCaptureRunner
    {
        const string PREF_KEY  = "MatchmakingCaptureRunner_Active";
        const string PREF_STEP = "MatchmakingCaptureRunner_Step";

        enum Step { WaitForInit, SelectChar, Navigate, OpenModal, WaitForFound, Capture, Done }

        static double _stepStartTime;
        static Step _step = Step.Done;

        static MatchmakingCaptureRunner()
        {
            if (EditorPrefs.GetBool(PREF_KEY, false))
            {
                _step = (Step)EditorPrefs.GetInt(PREF_STEP, 0);
                _stepStartTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += OnUpdate;
                Debug.Log("[MMCapture] Resumed from editor restart at step " + _step);
            }
        }

        [MenuItem("GOLFIN/Capture/Run Matchmaking Modal Capture")]
        public static void StartCapture()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.Log("[MMCapture] Stopping existing play mode first...");
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => DoStart();
            }
            else
            {
                DoStart();
            }
        }

        static void DoStart()
        {
            EditorPrefs.SetBool(PREF_KEY, true);
            EditorPrefs.SetInt(PREF_STEP, (int)Step.WaitForInit);
            _step = Step.WaitForInit;
            _stepStartTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            Debug.Log("[MMCapture] Starting — entering play mode");
            EditorApplication.isPlaying = true;
        }

        static void OnUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                if (_step != Step.Done && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    // Play mode exited unexpectedly
                    if (_step != Step.Done)
                        Debug.LogWarning("[MMCapture] Play mode exited before capture complete at step " + _step);
                    Cleanup();
                }
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            double elapsed = now - _stepStartTime;

            switch (_step)
            {
                case Step.WaitForInit:
                    // Wait 4 seconds for game singletons to initialize
                    if (elapsed >= 4.0)
                    {
                        Advance(Step.SelectChar);
                    }
                    break;

                case Step.SelectChar:
                {
                    var cm = CharacterManager.Instance;
                    if (cm == null)
                    {
                        // Try to set Instance via reflection if not set
                        var cmComp = UnityEngine.Object.FindFirstObjectByType<CharacterManager>();
                        if (cmComp != null)
                        {
                            var prop = typeof(CharacterManager).GetProperty("Instance",
                                BindingFlags.Public | BindingFlags.Static);
                            prop?.SetValue(null, cmComp);
                            cm = CharacterManager.Instance;
                        }
                    }

                    if (cm == null)
                    {
                        if (elapsed >= 5.0)
                        {
                            Debug.LogError("[MMCapture] CharacterManager.Instance null after 5s at SelectChar — aborting.");
                            Cleanup();
                        }
                        return;
                    }

                    var db = CharacterDatabaseCSV.Instance;
                    if (db == null)
                    {
                        if (elapsed >= 5.0)
                        {
                            Debug.LogError("[MMCapture] CharacterDatabaseCSV.Instance null after 5s — aborting.");
                            Cleanup();
                        }
                        return;
                    }

                    var chars = db.GetAllCharacters();
                    if (chars == null || chars.Count == 0)
                    {
                        Debug.LogError("[MMCapture] No characters in database — aborting.");
                        Cleanup();
                        return;
                    }

                    cm.SelectCharacter(chars[0].characterId);
                    Debug.Log("[MMCapture] Selected character: " + chars[0].characterId);
                    Advance(Step.Navigate);
                    break;
                }

                case Step.Navigate:
                    if (elapsed >= 0.5)
                    {
                        var sm = UnityEngine.Object.FindFirstObjectByType<ScreenManager>();
                        if (sm != null)
                        {
                            sm.ShowScreen(ScreenId.Home);
                            Debug.Log("[MMCapture] ShowScreen(Home) called");
                        }
                        else
                        {
                            Debug.LogError("[MMCapture] ScreenManager not found — aborting.");
                            Cleanup();
                            return;
                        }
                        Advance(Step.OpenModal);
                    }
                    break;

                case Step.OpenModal:
                    // Wait 2s for screen transition to complete
                    if (elapsed >= 2.0)
                    {
                        var modal = UnityEngine.Object.FindFirstObjectByType<MatchmakingModalController>();
                        if (modal != null)
                        {
                            modal.Open(0);
                            Debug.Log("[MMCapture] Open(0) called");
                        }
                        else
                        {
                            Debug.LogError("[MMCapture] MatchmakingModalController not found — aborting.");
                            Cleanup();
                            return;
                        }
                        Advance(Step.WaitForFound);
                    }
                    break;

                case Step.WaitForFound:
                    // Wait 7s for OPPONENT FOUND state (searchDurationSeconds=5 + 2s buffer)
                    if (elapsed >= 7.0)
                    {
                        Advance(Step.Capture);
                    }
                    break;

                case Step.Capture:
                    // Focus GameView, wait 0.5s for RT to update, then capture
                    var gvType2 = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                    if (gvType2 != null)
                    {
                        var gv2 = EditorWindow.GetWindow(gvType2, false, null, false);
                        if (gv2 != null)
                        {
                            gv2.Focus();
                            gv2.Repaint();
                        }
                    }

                    if (elapsed >= 1.0)
                    {
                        DoCapture();
                        Advance(Step.Done);
                        EditorApplication.isPlaying = false;
                        Debug.Log("[MMCapture] Done — stopping play mode.");
                        Cleanup();
                    }
                    break;
            }
        }

        static void Advance(Step next)
        {
            _step = next;
            _stepStartTime = EditorApplication.timeSinceStartup;
            EditorPrefs.SetInt(PREF_STEP, (int)next);
            Debug.Log($"[MMCapture] -> {next}");
        }

        static void DoCapture()
        {
            Directory.CreateDirectory("Docs/Diagnostics/_capture");
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string path = $"Docs/Diagnostics/_capture/matchmaking-modal-iter2_{timestamp}.png";

            // Primary: GrabGameViewRT via reflection
            Texture2D tex = GrabGameViewRT();
            if (tex != null)
            {
                Debug.Log("[MMCapture] Using RT reflection path");
            }
            else
            {
                Debug.LogWarning("[MMCapture] RT reflection failed, using ScreenCapture fallback");
                tex = ScreenCapture.CaptureScreenshotAsTexture();
            }

            if (tex == null)
            {
                Debug.LogError("[MMCapture] All capture methods failed.");
                return;
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("[MMCapture] Saved to: " + Path.GetFullPath(path));
        }

        static Texture2D GrabGameViewRT()
        {
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return null;

            var gv = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (gv == null) return null;

            gv.Focus();
            gv.Repaint();

            string[] candidates = { "m_RenderTexture", "m_TargetTexture", "m_RenderTarget" };
            RenderTexture rt = null;
            foreach (var name in candidates)
            {
                var f = gameViewType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                rt = f?.GetValue(gv) as RenderTexture;
                if (rt != null && rt.IsCreated()) break;
            }

            if (rt == null) return null;

            int w = rt.width, h = rt.height;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            // Flip Y (RT is bottom-left origin)
            var pixels = tex.GetPixels();
            var flipped = new UnityEngine.Color[pixels.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    flipped[y * w + x] = pixels[(h - 1 - y) * w + x];
            tex.SetPixels(flipped);
            tex.Apply();

            return tex;
        }

        static void Cleanup()
        {
            EditorApplication.update -= OnUpdate;
            EditorPrefs.DeleteKey(PREF_KEY);
            EditorPrefs.DeleteKey(PREF_STEP);
            _step = Step.Done;
        }
    }
}
