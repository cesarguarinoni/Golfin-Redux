#if UNITY_EDITOR
// Assets/Scripts/UI/Editor/GachaTaglineDemoRecorder.cs
// gacha_banner_tagline — the sign-off clip: the weekly gacha card selling through its two
// localized copy lines (ribbon + hook band) drawn over the textless artwork.
//
// Drives the REAL flow through the real widgets, exactly as the ResultScreenNavVerifyBot /
// GachaDemoRecorder family does: boot → DevAutoSignIn → Home → PersistentUIManager.gachaButton
// → the first-visit hint's own NextButton (if it shows) → the live wk_2026_38 card → the language
// toggle through the real Settings overlay (gear ▸ Language ▸ 日本語 ▸ close) and back → the
// carousel moved to STANDARD CLUB 1 (a row with a blank pair: no plates) and back.
//
// Recording: BotVideoRecorder (Unity Recorder, 1170×2532 @ 30 fps capped, vSync 0), armed
// DEFERRED and started once Home is on screen so the boot transient is not in the clip. Caption
// times are stamped relative to BeginDeferred() into videos/captions.json and burned in by
// Docs/Scripts/build_bot_video.py --mode captionsjson (the textfile drawtext idiom). Captions are
// pre-wrapped ≤ 40 chars/line and carry no `%` (feedback_portrait_video_captions_prewrap).
//
// Output: Docs/Specs/Active/gacha_banner_tagline/videos/gacha_tagline_raw.mp4 + captions.json
// Usage:  GOLFIN > Gacha > Record tagline demo clip

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Golfin.Physics.Viewer.Editor;
using Golfin.UI;
using GolfinRedux.UI.Gacha;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Golfin.EditorTools
{
    public static class GachaTaglineDemoRecorder
    {
        const string ArmedKey   = "GachaTaglineDemo.Armed";
        const string ShellScene = "Assets/Scenes/ShellScene.unity";
        public const string OutDir = "Docs/Specs/Active/gacha_banner_tagline/videos";

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Gacha/Record tagline demo clip", priority = 410)]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[GachaTaglineDemo] Already playing — stop first."); return; }
            if (SceneManager.GetActiveScene().path != ShellScene)
                EditorSceneManager.OpenScene(ShellScene, OpenSceneMode.Single);

            Directory.CreateDirectory(OutDir);
            // Deferred start: BeginDeferred() fires once Home is live. Begin() applies the 30 fps
            // cap + vSync 0 itself (feedback_cap_play_mode_frame_rate_never_record_long_sweeps).
            BotVideoRecorder.CustomOutputPath = OutDir + "/gacha_tagline_raw";
            BotVideoRecorder.MaxRecordSecondsSessionOverride = 75;
            BotVideoRecorder.ArmDeferred();

            Application.runInBackground = true;
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[GachaTaglineDemo] Armed. Entering play mode…");
        }

        // GameViewSizeUtil is internal to the Golfin.Physics.Viewer.BotEditor asmdef — the same
        // reflection hop every demo recorder in this folder makes.
        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                var asm = Assembly.Load("Golfin.Physics.Viewer.BotEditor");
                var t   = asm?.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                var m   = t?.GetMethod("EnsureIPhone14Selected", BindingFlags.Public | BindingFlags.Static);
                return m != null && (bool)m.Invoke(null, null);
            }
            catch { return false; }
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            SessionState.SetBool(ArmedKey, false);

            TryEnsureIPhone14Selected();
            var host = new GameObject("[GachaTaglineDemoRecorder]");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<GachaTaglineDemoRunner>();
        }
    }

    public class GachaTaglineDemoRunner : MonoBehaviour
    {
        const string Out = GachaTaglineDemoRecorder.OutDir;

        readonly List<(float start, float end, string text)> _captions = new List<(float, float, string)>();
        readonly StringBuilder _log = new StringBuilder();
        float _rec0 = -1f;
        (float start, string text)? _open;

        void Start() => StartCoroutine(Run());

        // ── helpers ────────────────────────────────────────────────────────────

        float Now => _rec0 < 0 ? 0f : Time.realtimeSinceStartup - _rec0;

        void Log(string s)
        {
            _log.AppendLine(Now.ToString("F2").PadLeft(6) + "s  " + s);
            Debug.Log("[GachaTaglineDemo] " + s);
        }

        /// <summary>Open a caption now; the previous one closes at this instant.</summary>
        void Say(string text)
        {
            CloseCaption();
            _open = (Now, text);
            Log("CAPTION: " + text.Replace("\n", " / "));
        }

        void CloseCaption()
        {
            if (_open == null) return;
            var (start, text) = _open.Value;
            _captions.Add((start, Now, text));
            _open = null;
        }

        static IEnumerator Settle(float s) { yield return new WaitForSecondsRealtime(s); }

        static string Screen()
        {
            var sm = GolfinRedux.UI.ScreenManager.Instance;
            return sm != null ? sm.CurrentScreen.ToString() : "null";
        }

        static bool Click(string path)
        {
            var go = GameObject.Find(path);
            var b = go != null ? go.GetComponent<Button>() : null;
            if (b == null) { Debug.LogWarning("[GachaTaglineDemo] no Button at " + path); return false; }
            b.onClick.Invoke();
            return true;
        }

        IEnumerator CloseHints()
        {
            for (int i = 0; i < 10; i++)
            {
                var modal = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.scene.IsValid() && g.activeInHierarchy && g.name == "ScreenHintModal");
                var next = modal == null ? null : modal.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.gameObject.name == "NextButton" && b.gameObject.activeInHierarchy);
                if (next == null) yield break;
                Log("first-visit hint open — pressing its real NextButton");
                next.onClick.Invoke();
                yield return Settle(0.7f);
            }
        }

        /// <summary>Language through the REAL Settings overlay: gear ▸ Language ▸ button ▸ close.</summary>
        IEnumerator SwitchLanguage(string button)
        {
            Click("PersistentUI/TopBar/SettingsButton");
            yield return Settle(1.1f);
            Click("SettingsScreen/SettingsPanel/SettingsList/LanguageRow");
            yield return Settle(1.1f);
            Click("SettingsScreen/SettingsPanel/SettingsList/LanguageRow/LanguageSubmenu/" + button);
            yield return Settle(1.2f);
            Click("SettingsScreen/SettingsPanel/CloseButton");
            yield return Settle(0.8f);
        }

        /// <summary>Move the carousel to a banner id. Offsets are ABSOLUTE scroll positions
        /// (Reload sets _currentOffset = _currentIndex * _cardSpacing), so the target is
        /// index × spacing and the controller's own Update lerps there — the same fields the
        /// GachaDemoRecorder drives.</summary>
        static GachaBannerCard MoveTo(string bannerId)
        {
            var ctrl = Object.FindFirstObjectByType<GachaCarouselController>();
            if (ctrl == null) return null;
            var t = typeof(GachaCarouselController);
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
            var list = t.GetField("_cards", F)?.GetValue(ctrl) as List<GachaBannerCard>;
            if (list == null) return null;
            int idx = list.FindIndex(c => c.Entry != null && c.Entry.BannerId == bannerId);
            if (idx < 0) return null;
            float spacing = (float)t.GetField("_cardSpacing", F).GetValue(ctrl);
            t.GetField("_currentIndex", F).SetValue(ctrl, idx);
            t.GetField("_targetOffset", F).SetValue(ctrl, idx * spacing);
            t.GetMethod("UpdateDots", F)?.Invoke(ctrl, null);
            return list[idx];
        }

        static string Plates(GachaBannerCard card)
        {
            if (card == null) return "no card";
            var rib = card.transform.Find("ArtImage/TaglineRibbon");
            var hook = card.transform.Find("ArtImage/TaglineHook");
            string rt = rib != null ? rib.Find("Label").GetComponent<TextMeshProUGUI>().text : "";
            string ht = hook != null ? hook.Find("Label").GetComponent<TextMeshProUGUI>().text.Replace("\n", "⏎") : "";
            return card.Entry.BannerId + " ribbonActive=" + (rib != null && rib.gameObject.activeSelf) + " hookActive=" + (hook != null && hook.gameObject.activeSelf) + " ribbon='" + rt + "' hook='" + ht + "'";
        }

        // ── the take ───────────────────────────────────────────────────────────

        IEnumerator Run()
        {
            Application.runInBackground = true;

            // Boot → Home (DevAutoSignIn signs in and taps the Splash StartButton for us).
            float t0 = Time.realtimeSinceStartup;
            PersistentUIManager pm = null;
            while (Time.realtimeSinceStartup - t0 < 60f)
            {
                pm = Object.FindFirstObjectByType<PersistentUIManager>();
                if (Screen() == "Home" && pm != null && pm.gachaButton != null && pm.gachaButton.gameObject.activeInHierarchy) break;
                yield return Settle(0.5f);
            }
            if (Screen() != "Home") { Log("ABORT: never reached Home (" + Screen() + ")"); Finish(); yield break; }
            LocalizationManager.SetLanguage(Language.English);
            yield return Settle(1.0f);

            // Recording starts on Home so the real navigation is in the clip.
            BotVideoRecorder.BeginDeferred();
            _rec0 = Time.realtimeSinceStartup;
            Log("recording started (t=0)");
            yield return Settle(1.2f);

            Say("The weekly gacha card now sells:\nribbon + hook band over textless art");
            yield return Settle(1.6f);
            Log("tapping the real bottom-nav gacha slot");
            pm.gachaButton.onClick.Invoke();
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 12f && !(Screen() == "GeneralShop" && Object.FindObjectsByType<GachaBannerCard>(FindObjectsSortMode.None).Length > 0)) yield return Settle(0.25f);
            yield return Settle(1.6f);
            yield return CloseHints();

            var card = Object.FindObjectsByType<GachaBannerCard>(FindObjectsSortMode.None).FirstOrDefault(c => c.Entry != null && c.Entry.BannerId == "banner_wk_2026_38");
            Log("EN: " + Plates(card));
            Say("DRIVER WEEK · BOGEYB - the copy is\nlocalized UI, not baked into the art");
            yield return Settle(3.4f);
            Say("Pink ribbon: taglineEn, straight\nfrom the published catalog row");
            yield return Settle(3.2f);
            Say("Navy hook band: hookEn - the\n*marked* word renders in pink");
            yield return Settle(3.2f);

            // JA through the REAL Settings overlay.
            Say("Settings > Language > 日本語 -\nsame check the title uses");
            yield return SwitchLanguage("JapaneseButton");
            Log("JA: " + Plates(card));
            Say("Title, ribbon and hook flip together\nto the JA pair - no screen re-entry");
            yield return Settle(3.4f);
            Say("Back to English the same way");
            yield return SwitchLanguage("EnglishButton");
            Log("EN again: " + Plates(card));
            yield return Settle(1.0f);

            // A row with a blank pair: no plates.
            Say("Next banner: a row with a blank pair\nshows NO plate at all");
            var std = MoveTo("banner_standard_club1");
            yield return Settle(2.4f);
            Log("STANDARD CLUB 1: " + Plates(std));
            Say("STANDARD CLUB 1 keeps its baked art -\nribbon and hook stay hidden");
            yield return Settle(3.2f);
            Say("Back to the weekly card");
            MoveTo("banner_wk_2026_38");
            yield return Settle(2.4f);
            Say("Copy lives in the rotation plan -\n52 weeks, changed by a publish, no build");
            yield return Settle(3.4f);
            CloseCaption();

            Finish();
        }

        void Finish()
        {
            CloseCaption();
            BotVideoRecorder.End();
            Log("recording stopped");
            try
            {
                Directory.CreateDirectory(Out);
                var sb = new StringBuilder();
                sb.Append("{\n  \"_note\": \"times are video-relative + 0.40 (build_bot_video.py subtracts RECORDER_LEAD); stamped from BeginDeferred() by GachaTaglineDemoRecorder\",\n  \"captions\": [\n");
                for (int i = 0; i < _captions.Count; i++)
                {
                    var (s, e, text) = _captions[i];
                    string esc = text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
                    sb.Append("    { \"start\": " + (s + 0.40f).ToString("F2") + ", \"end\": " + (e + 0.40f).ToString("F2") + ", \"text\": \"" + esc + "\" }" + (i < _captions.Count - 1 ? "," : "") + "\n");
                }
                sb.Append("  ]\n}\n");
                File.WriteAllText(Path.Combine(Out, "captions.json"), sb.ToString());
                File.WriteAllText(Path.Combine(Out, "gacha_tagline_take.log"), _log.ToString());
            }
            catch (System.Exception e) { Debug.LogWarning("[GachaTaglineDemo] sidecar write failed: " + e.Message); }
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
