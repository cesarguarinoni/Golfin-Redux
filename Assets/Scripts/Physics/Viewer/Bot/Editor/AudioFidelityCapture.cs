#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Audio.Events;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// Records a captioned "audio fidelity tour" MP4 — WITH an audio track — that fires every
    /// <see cref="SfxId"/> through the real <see cref="SfxBus"/> in sequence over a menu-music bed,
    /// labelling each sound on-screen. This is the Tier-3 fidelity-gate deliverable for the
    /// sound_effects task (Order 350): a clip Cesar can play and *hear* to judge clip choice + that
    /// every event actually produces audio through the bus → SfxPlayer → AudioManager → mixer chain.
    ///
    /// Why a captioned sound-board (not a raw bot playthrough): audio cannot be heard in a still, and
    /// the EditMode bus-wiring tests already prove the emitter fires at the right trajectory events.
    /// What a human still must judge is "does the right clip play, and does it sound right" — best
    /// served by isolating + labelling every sound. Menu music plays underneath the whole tour.
    ///
    /// Mirrors <see cref="BotVideoRecorder"/>'s GPU-safety posture: full iPhone-14 1170×2532 via
    /// <see cref="GameViewSizeUtil.EnsureIPhone14Selected"/>, 30 fps real-time, render-loop capped
    /// during record, hard duration backstop. The ONE difference from BotVideoRecorder is
    /// <c>PreserveAudio = true</c> — this clip's whole point is the audio track.
    ///
    /// Output (project-root relative): Docs/Specs/Active/sound_effects/videos/audio_tour_raw.mp4
    /// Run: GOLFIN ▸ Capture ▸ Record Audio Fidelity Tour.
    /// </summary>
    public static class AudioFidelityCapture
    {
        const string ArmKey = "AudioFidelityCapture.Arm";
        const int    Fps     = 30;
        const string OutDir  = "Docs/Specs/Active/sound_effects/videos";
        const string OutBase = "audio_tour_raw";
        const double HardCapSeconds = 55.0; // safety backstop; the tour is ~36s

        static RecorderController _controller;
        static Text     _caption;
        static double   _seqStart;        // realtime tour start
        static double   _stepStart;       // realtime current-step start
        static int      _step = -1;
        static int      _savedFps;
        static int      _savedVSync;
        static bool     _loadOverridden;

        // Reflected AudioManager music hooks (AudioManager lives in Assembly-CSharp; this editor
        // assembly cannot reference it directly, so reflection keeps the music bed alive).
        static object     _am;
        static MethodInfo _playMusic;
        static MethodInfo _isMusicPlaying;
        static AudioClip  _mainTheme;

        struct Step { public string Label; public SfxId Id; public bool HasSfx; public float Dur; }
        static List<Step> _seq;

        [MenuItem("GOLFIN/Capture/Record Audio Fidelity Tour")]
        public static void Launch()
        {
            SessionState.SetBool(ArmKey, true);
            if (!EditorApplication.isPlaying)
                EditorApplication.isPlaying = true;
            else
                Debug.LogWarning("[AudioFidelityCapture] Already in play mode — exit and re-run the menu item to record from a clean boot.");
        }

        [InitializeOnLoadMethod]
        static void Init() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        static void OnPlayModeChanged(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ArmKey, false))
            {
                SessionState.SetBool(ArmKey, false);
                try { Begin(); }
                catch (Exception e) { Debug.LogError($"[AudioFidelityCapture] Begin failed: {e}"); Cleanup(); }
            }
            else if (s == PlayModeStateChange.ExitingPlayMode)
            {
                Cleanup();
            }
        }

        static void Begin()
        {
            BuildSequence();
            BuildOverlay();
            ResolveMusic();

            Directory.CreateDirectory(OutDir);
            bool selected = GameViewSizeUtil.EnsureIPhone14Selected();
            int w = GameViewSizeUtil.IPhone14Width, h = GameViewSizeUtil.IPhone14Height;
            if (!selected)
                Debug.LogWarning("[AudioFidelityCapture] iPhone-14 size not confirmed; recording at constants anyway.");

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "AudioTour";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = true;   // <-- the whole point of this recorder
            movie.OutputFile = $"{OutDir}/{OutBase}";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = Fps;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _controller = new RecorderController(settings);
            _controller.PrepareRecording();
            _controller.StartRecording();

            _savedFps   = Application.targetFrameRate;
            _savedVSync = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount  = 0;
            Application.targetFrameRate = Fps;
            _loadOverridden = true;

            _seqStart = _stepStart = EditorApplication.timeSinceStartup;
            _step = 0;
            ApplyStep();
            EditorApplication.update += Tick;
            Debug.Log($"[AudioFidelityCapture] Recording → {OutDir}/{OutBase}.mp4 ({w}x{h}@{Fps}, audio ON). {_seq.Count} steps.");
        }

        static void Tick()
        {
            if (_controller == null) { EditorApplication.update -= Tick; return; }
            double now = EditorApplication.timeSinceStartup;

            // Keep the menu-music bed alive (ScreenManager may stop it on non-menu boot screens).
            if (_isMusicPlaying != null && _playMusic != null && _am != null && _mainTheme != null)
            {
                try
                {
                    bool playing = (bool)_isMusicPlaying.Invoke(_am, null);
                    if (!playing) _playMusic.Invoke(_am, new object[] { _mainTheme, true });
                }
                catch { /* one-off reflection hiccup — non-fatal */ }
            }

            if (now - _seqStart > HardCapSeconds)
            {
                Debug.LogWarning("[AudioFidelityCapture] Hard cap reached — stopping.");
                Finish();
                return;
            }

            if (_step < _seq.Count && now - _stepStart >= _seq[_step].Dur)
            {
                _step++;
                _stepStart = now;
                if (_step >= _seq.Count) { Finish(); return; }
                ApplyStep();
            }
        }

        static void ApplyStep()
        {
            var st = _seq[_step];
            if (_caption != null) _caption.text = st.Label;
            if (st.HasSfx) SfxBus.Play(st.Id);
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            StopRecorder();
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        static void Cleanup()
        {
            EditorApplication.update -= Tick;
            StopRecorder();
            GameViewSizeUtil.PurgeFabricatedEntries();
        }

        static void StopRecorder()
        {
            if (_controller != null)
            {
                try { if (_controller.IsRecording()) _controller.StopRecording(); }
                catch (Exception e) { Debug.LogWarning($"[AudioFidelityCapture] stop: {e.Message}"); }
                _controller = null;
                Debug.Log("[AudioFidelityCapture] Recording stopped.");
            }
            if (_loadOverridden)
            {
                Application.targetFrameRate = _savedFps;
                QualitySettings.vSyncCount  = _savedVSync;
                _loadOverridden = false;
            }
        }

        static void ResolveMusic()
        {
            try
            {
                Type amType = Type.GetType("Golfin.Audio.AudioManager, Assembly-CSharp");
                if (amType == null)
                    foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("Golfin.Audio.AudioManager"); if (t != null) { amType = t; break; } }
                if (amType == null) { Debug.LogWarning("[AudioFidelityCapture] AudioManager type not found; music bed skipped."); return; }

                var inst = amType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                _am = inst?.GetValue(null);
                _playMusic      = amType.GetMethod("PlayMusic", new[] { typeof(AudioClip), typeof(bool) });
                _isMusicPlaying = amType.GetMethod("IsMusicPlaying", Type.EmptyTypes);

                _mainTheme = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/Main Theme.mp3")
                          ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/Main Theme.wav");
                if (_mainTheme == null) Debug.LogWarning("[AudioFidelityCapture] Main Theme clip not found at Assets/Music/.");
            }
            catch (Exception e) { Debug.LogWarning($"[AudioFidelityCapture] ResolveMusic: {e.Message}"); }
        }

        static void BuildOverlay()
        {
            var go = new GameObject("__AudioTourOverlay");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1170, 2532);
            go.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Title (top)
            var title = MakeText(go.transform, "Title", font, 46, TextAnchor.UpperCenter,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -70), new Vector2(-40, -180));
            title.text = "ORDER 350 — AUDIO FIDELITY TOUR";
            title.color = new Color(1f, 1f, 1f, 0.85f);

            // Bottom caption bar background
            var bar = new GameObject("CaptionBar", typeof(Image));
            bar.transform.SetParent(go.transform, false);
            var barImg = bar.GetComponent<Image>();
            barImg.color = new Color(0f, 0f, 0f, 0.62f);
            var brt = bar.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0);
            brt.offsetMin = new Vector2(0, 220); brt.offsetMax = new Vector2(0, 470);

            _caption = MakeText(go.transform, "Caption", font, 64, TextAnchor.MiddleCenter,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(50, 230), new Vector2(-50, 460));
            _caption.fontStyle = FontStyle.Bold;
            _caption.color = Color.white;
            _caption.text = "";
        }

        static Text MakeText(Transform parent, string name, Font font, int size, TextAnchor anchor,
                             Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            // outline for legibility over any backdrop
            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0, 0, 0, 0.9f);
            ol.effectDistance = new Vector2(2, -2);
            return t;
        }

        static void BuildSequence()
        {
            _seq = new List<Step>();
            void S(string label, SfxId id, float dur) => _seq.Add(new Step { Label = label, Id = id, HasSfx = true, Dur = dur });
            void Hdr(string label, float dur) => _seq.Add(new Step { Label = label, HasSfx = false, Dur = dur });

            Hdr("♪  MENU MUSIC — Main Theme (looping bed)", 3.0f);

            Hdr("— SWING (per club) —", 1.0f);
            S("Swing · Driver",  SfxId.SwingDriver, 1.1f);
            S("Swing · Wood",    SfxId.SwingWood,   1.1f);
            S("Swing · Iron",    SfxId.SwingIron,   1.1f);
            S("Swing · Wedge",   SfxId.SwingWedge,  1.1f);
            S("Swing · Putter",  SfxId.SwingPutt,   1.1f);

            Hdr("— HIT (by power band) —", 1.0f);
            S("Hit · Strong",    SfxId.HitStrong,   1.1f);
            S("Hit · Default",   SfxId.HitDefault,  1.1f);
            S("Hit · Weak",      SfxId.HitWeak,     1.1f);
            S("Hit · Putt",      SfxId.HitPutt,     1.1f);
            S("Hit · Bunker",    SfxId.HitBunker,   1.1f);

            Hdr("— LANDING (per surface) —", 1.0f);
            S("Land · Fairway",  SfxId.LandFairway, 1.2f);
            S("Land · Green",    SfxId.LandGreen,   1.2f);
            S("Land · Rough",    SfxId.LandRough,   1.2f);
            S("Land · Sand (bunker)", SfxId.LandSand, 1.2f);
            S("Land · Road",     SfxId.LandRoad,    1.2f);
            S("Land · Bushes",   SfxId.LandBushes,  1.2f);
            S("WATER SPLASH",    SfxId.LandWater,   1.5f);
            S("CUP DROP-IN",     SfxId.HitBallIn,   1.6f);

            Hdr("— UI —", 1.0f);
            S("UI · Tap",        SfxId.UiTap,       1.0f);
            S("UI · Confirm",    SfxId.UiConfirm,   1.0f);
            S("UI · Cancel",     SfxId.UiCancel,    1.0f);
            S("UI · Back",       SfxId.UiBack,      1.0f);

            Hdr("— STINGERS —", 1.0f);
            S("RP Earned  (placeholder clip)", SfxId.RpEarn,  1.4f);
            S("Level Up   (placeholder clip)", SfxId.LevelUp, 1.4f);
            S("Match · WIN",     SfxId.MatchWin,    1.8f);
            S("Match · LOSE",    SfxId.MatchLose,   1.8f);
            S("Match · DRAW",    SfxId.MatchDraw,   1.8f);

            Hdr("♪  end of tour", 1.5f);
        }
    }
}
#endif
