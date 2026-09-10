#if UNITY_EDITOR
// Assets/Scripts/UI/Hints/Editor/ScreenHintVerifyBot.cs
// screen_hints — the acceptance-list driver.
//
// Modelled on LoadingTipsDemoRecorder: boot ShellScene, get past the Splash gate with the
// REAL StartButton, then walk the screens exactly as a player does — nav-bar buttons,
// the Home mode card's PlayButton, the hole card's ActionButton, the Settings gear and
// the Controls accordion row — and press the hint modal's OWN BackButton / NextButton
// through their onClick. The only navigation that is not a nav-bar tap is
// ScreenManager.ShowScreen for the screens the nav bar does not reach (GeneralShop,
// ModeSelection, MissionSelection, Leaderboard); the presenter listens to ScreenChanged,
// which fires the same way for both.
//
// Every observation is written to screenshots/verify_<lang>.log as it happens, and every
// frame cited in the report is a CaptureHelper.SnapPlayModeSafe capture (no
// AssetDatabase.Refresh, so this coroutine survives it) copied into the task folder with
// its provenance sidecar. Existence and an md5 are logged per snap because SnapPlayModeSafe
// has returned phantom paths and byte-identical stale frames before.
//
// Usage: GOLFIN > Hints > Run verify bot (EN) / (JA). Clears the seen-state first.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Golfin.Gameplay.UI.Controls;
using Golfin.UI;
using Golfin.UI.Modals;
using GolfinRedux.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Golfin.EditorTools.Hints
{
    public static class ScreenHintVerifyBot
    {
        const string ArmedKey   = "ScreenHintVerifyBot.Armed";
        const string LangKey    = "ScreenHintVerifyBot.Lang";
        const string ShellScene = "Assets/Scenes/ShellScene.unity";

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Hints/Run verify bot (EN)", priority = 300)]
        public static void LaunchEn() => Launch("en");

        [MenuItem("GOLFIN/Hints/Run verify bot (JA)", priority = 301)]
        public static void LaunchJa() => Launch("ja");

        static void Launch(string lang)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[HintVerify] Already playing — stop first."); return; }
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ShellScene)
                EditorSceneManager.OpenScene(ShellScene, OpenSceneMode.Single);

            // A FRESH INSTALL: every screen hints again.
            ScreenHintStore.Clear();

            Application.runInBackground = true;
            SessionState.SetBool(ArmedKey, true);
            SessionState.SetString(LangKey, lang);
            EditorApplication.EnterPlaymode();
            Debug.Log("[HintVerify] Armed (" + lang + "), seen-state cleared. Entering play mode…");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            SessionState.SetBool(ArmedKey, false);

            TryEnsureIPhone14Selected();
            var host = new GameObject("[ScreenHintVerifyBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ScreenHintVerifyRunner>().Begin(SessionState.GetString(LangKey, "en"));
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
    }

    public class ScreenHintVerifyRunner : MonoBehaviour
    {
        const string OutDir = "Docs/Specs/Active/screen_hints/screenshots";

        readonly StringBuilder _log = new StringBuilder();
        string _lang = "en";
        string _logPath;
        string _lastMd5;
        float _t0;

        public void Begin(string lang)
        {
            _lang = lang;
            _logPath = Path.Combine(OutDir, "verify_" + lang + ".log");
            Directory.CreateDirectory(OutDir);
            _t0 = Time.realtimeSinceStartup;
            StartCoroutine(Sequence());
        }

        // ── logging / capture ─────────────────────────────────────────────────

        void Log(string s)
        {
            string line = (Time.realtimeSinceStartup - _t0).ToString("F2").PadLeft(7) + "s  " + s;
            _log.AppendLine(line);
            Debug.Log("[HintVerify] " + s);
            try { File.WriteAllText(_logPath, _log.ToString()); } catch { }
        }

        static string Md5(string path)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var f = File.OpenRead(path))
                return BitConverter.ToString(md5.ComputeHash(f)).Replace("-", "").Substring(0, 12).ToLowerInvariant();
        }

        /// <summary>One frame into the task folder, with its provenance sidecar.
        /// Yields WaitForEndOfFrame FIRST: SnapPlayModeSafe's grab is synchronous and returns a
        /// phantom path when the caller is not at end of frame (reference_snapplaymodesafe_phantom_path).</summary>
        IEnumerator Snap(string label)
        {
            yield return new WaitForEndOfFrame();
            string src = CaptureHelper.SnapPlayModeSafe("screen_hints_" + label);
            if (string.IsNullOrEmpty(src) || !File.Exists(src)) { Log("SNAP " + label + ": NO FILE (" + src + ")"); yield break; }
            string dst = Path.Combine(OutDir, label + ".png");
            File.Copy(src, dst, true);
            if (File.Exists(src + ".json")) File.Copy(src + ".json", dst + ".json", true);
            string md5 = Md5(dst);
            long bytes = new FileInfo(dst).Length;
            Log("SNAP " + label + " -> " + dst + " (" + PngSize(dst) + ", " + bytes + " B, md5 " + md5 + (md5 == _lastMd5 ? ", IDENTICAL TO PREVIOUS SNAP" : "") + ")");
            _lastMd5 = md5;
        }

        static string PngSize(string path)
        {
            try
            {
                using (var f = File.OpenRead(path))
                {
                    var b = new byte[24]; f.Read(b, 0, 24);
                    int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                    int h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                    return w + "x" + h;
                }
            }
            catch { return "?x?"; }
        }

        // ── the modal ─────────────────────────────────────────────────────────

        static ScreenHintModalController Modal => ScreenHintModalController.Instance;

        static Button NextButton => Modal.transform.Find("Panel/ButtonsRow/NextButton").GetComponent<Button>();
        static Button BackButton => Modal.transform.Find("Panel/ButtonsRow/BackButton").GetComponent<Button>();
        static TextMeshProUGUI GoldLabel => Modal.transform.Find("Panel/ButtonsRow/NextButton/Text").GetComponent<TextMeshProUGUI>();
        static TextMeshProUGUI Counter => Modal.transform.Find("Panel/Counter").GetComponent<TextMeshProUGUI>();
        static TextMeshProUGUI TipText => Modal.transform.Find("Panel/TipContent/TipText").GetComponent<TextMeshProUGUI>();
        static CanvasGroup TipGroup => Modal.transform.Find("Panel/TipContent").GetComponent<CanvasGroup>();
        static RectTransform Panel => (RectTransform)Modal.transform.Find("Panel");

        static string TipKey()
        {
            var loc = TipText.GetComponent<LocalizedText>();
            var f = typeof(LocalizedText).GetField("key", BindingFlags.Instance | BindingFlags.NonPublic);
            return loc != null && f != null ? (string)f.GetValue(loc) : "?";
        }

        static string State() => PlayerPrefs.GetString(ScreenHintStore.PrefsKey, "(none)");

        void Describe(string tag)
        {
            var m = Modal;
            if (m == null) { Log(tag + ": NO MODAL INSTANCE"); return; }
            Log(tag + ": visible=" + m.IsVisible() + " n=" + (m.Index + 1) + "/" + m.Count
                + " key=" + TipKey()
                + " counter=" + (Counter.gameObject.activeSelf ? "'" + Counter.text + "'" : "HIDDEN")
                + " back=" + (BackButton.gameObject.activeSelf ? "PRESENT" : "ABSENT")
                + " gold='" + GoldLabel.text + "'"
                + " plateH=" + Panel.rect.height.ToString("F1")
                + " alpha=" + TipGroup.alpha.ToString("F2")
                + " openModals=" + ModalController.OpenModalCount
                + " scene=" + m.gameObject.scene.name
                + " | state=" + State());
        }

        static void Press(Button b, string what)
        {
            var ped = new PointerEventData(EventSystem.current);
            (b.GetComponent("ButtonPressFeedback") as IPointerDownHandler)?.OnPointerDown(ped);
            b.onClick.Invoke();
            (b.GetComponent("ButtonPressFeedback") as IPointerUpHandler)?.OnPointerUp(ped);
        }

        IEnumerator TapNext(string why)
        {
            int before = Modal.Index;
            Press(NextButton, "next");
            Log("TAP NextButton (" + why + "): index " + before + " -> " + Modal.Index + " (swap running=" + Modal.Swapping + ")");
            yield return null;
        }

        IEnumerator TapBack(string why)
        {
            int before = Modal.Index;
            Press(BackButton, "back");
            Log("TAP BackButton (" + why + "): index " + before + " -> " + Modal.Index + " (swap running=" + Modal.Swapping + ")");
            yield return null;
        }

        IEnumerator WaitVisible(float timeout, string what)
        {
            float t = 0f;
            while (t < timeout)
            {
                var m = Modal;
                if (m != null && m.IsVisible()) { Log("modal visible for " + what + " after " + t.ToString("F2") + "s"); yield break; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Log("TIMEOUT waiting for the modal (" + what + ", " + timeout + "s)");
        }

        IEnumerator WaitHidden(float timeout)
        {
            float t = 0f;
            while (t < timeout && Modal != null && Modal.IsVisible()) { t += Time.unscaledDeltaTime; yield return null; }
            Log("modal hidden=" + (Modal == null || !Modal.IsVisible()) + " openModals=" + ModalController.OpenModalCount);
        }

        /// <summary>Per-frame trace of a swap; a snap every <paramref name="snapEvery"/> frames.</summary>
        IEnumerator FrameStrip(string label, int frames, int snapEvery)
        {
            for (int i = 0; i < frames; i++)
            {
                Log("  " + label + " f" + i.ToString("00") + ": plateH=" + Panel.rect.height.ToString("F1")
                    + " alpha=" + TipGroup.alpha.ToString("F2")
                    + " counter='" + Counter.text + "' counterScale=" + ((RectTransform)Counter.transform).localScale.x.ToString("F3")
                    + " gold='" + GoldLabel.text + "' back=" + (BackButton.gameObject.activeSelf ? "on" : "off")
                    + " key=" + TipKey());
                if (snapEvery > 0 && i % snapEvery == 0) yield return Snap(label + "_f" + i.ToString("00"));
                yield return null;
            }
        }

        static IEnumerator Settle(float s) { yield return new WaitForSecondsRealtime(s); }

        // ── real widgets ──────────────────────────────────────────────────────

        static T FindLive<T>() where T : Component
            => Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(c => c != null && !string.IsNullOrEmpty(c.gameObject.scene.name) && c.gameObject.activeInHierarchy);

        static Button Live(string name)
            => Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                   b.gameObject.name == name && !string.IsNullOrEmpty(b.gameObject.scene.name)
                   && b.gameObject.activeInHierarchy && b.interactable);

        IEnumerator TapWhenLive(string name, float timeout = 20f)
        {
            float t = 0f;
            while (t < timeout)
            {
                Button b = Live(name);
                if (b != null) { b.onClick.Invoke(); Log("tapped real " + name); yield break; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Log("never found an interactable " + name);
        }

        IEnumerator NavBar(Func<PersistentUIManager, Button> pick, string what)
        {
            var pui = FindLive<PersistentUIManager>();
            var b = pui != null ? pick(pui) : null;
            if (b == null) { Log("nav-bar button missing for " + what); yield break; }
            b.onClick.Invoke();
            Log("tapped nav-bar " + what + " -> CurrentScreen=" + ScreenManager.Instance.CurrentScreen);
            yield return null;
        }

        IEnumerator Show(ScreenId id)
        {
            ScreenManager.Instance.ShowScreen(id);
            Log("ShowScreen(" + id + ") -> CurrentScreen=" + ScreenManager.Instance.CurrentScreen);
            yield return null;
        }

        /// <summary>Walk a group to its end with CONTINUE, then CLOSE it.</summary>
        IEnumerator CloseGroup(string tag)
        {
            int guard = 0;
            while (Modal != null && Modal.IsVisible() && guard++ < 12)
            {
                bool last = Modal.Index == Modal.Count - 1;
                yield return TapNext(last ? tag + " CLOSE" : tag + " continue");
                yield return Settle(last ? 0.6f : 0.45f);
            }
            yield return WaitHidden(2f);
        }

        // ── the run ───────────────────────────────────────────────────────────

        IEnumerator Sequence()
        {
            Application.runInBackground = true;
            Log("run lang=" + _lang + " state at boot=" + State());

            // Splash gate — the real StartButton (DevAutoSignIn usually taps it first).
            float t = 0f;
            while (t < 25f)
            {
                var splash = FindFirstObjectByType<SplashScreenController>();
                Transform btn = splash == null ? null : splash.transform.Find("StartButton");
                if (btn != null && btn.gameObject.activeInHierarchy) { btn.GetComponent<Button>()?.onClick.Invoke(); Log("tapped StartButton"); break; }
                if (ScreenManager.Instance != null && ScreenManager.Instance.CurrentScreen == ScreenId.Home) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_lang == "ja") { LocalizationManager.SetLanguage(Language.Japanese); Log("language -> Japanese"); }

            // ── Home 1/2 ──────────────────────────────────────────────────────
            yield return WaitVisible(20f, "Home");
            yield return Settle(5f);
            Describe("HOME hint 1");
            yield return Snap(_lang + "_home_hint_1of2");

            if (_lang == "ja")
            {
                yield return TapNext("ja home"); yield return Settle(1f);
                Describe("HOME hint 2 (JA)"); yield return Snap("ja_home_hint_2of2");
                yield return CloseGroup("home");
                yield return NavBar(p => p.charactersButton, "Roster");
                yield return WaitVisible(10f, "Roster"); yield return Settle(4f);
                Describe("ROSTER hint 1 (JA)"); yield return Snap("ja_roster_hint_1of4");
                yield return TapNext("ja roster"); yield return Settle(1f);
                Describe("ROSTER hint 2 (JA)"); yield return Snap("ja_roster_hint_2of4");
                yield return CloseGroup("roster");
                LocalizationManager.SetLanguage(Language.English);
                Log("language -> English; DONE");
                yield return Settle(0.5f);
                EditorApplication.ExitPlaymode();
                yield break;
            }

            yield return TapNext("home 1->2");
            yield return FrameStrip("home_swap_1to2", 22, 0);
            yield return Settle(0.4f);
            Describe("HOME hint 2");
            yield return Snap("home_hint_2of2");

            yield return TapBack("home 2->1");
            yield return FrameStrip("home_swap_2to1", 22, 0);
            yield return Settle(0.4f);
            Describe("HOME hint 1 after BACK");
            yield return Snap("home_hint_1of2_after_back");

            yield return TapNext("home 1->2 again"); yield return Settle(0.7f);
            Describe("HOME hint 2 again");
            yield return TapNext("home CLOSE");
            yield return WaitHidden(2f);
            yield return Settle(0.8f);
            Log("after CLOSE: state=" + State());
            yield return Snap("home_after_close");

            // Second visit to Home shows nothing.
            yield return NavBar(p => p.charactersButton, "Roster");
            yield return Settle(0.3f);

            // ── Roster 1/4 … 4/4 ──────────────────────────────────────────────
            yield return WaitVisible(10f, "Roster");
            yield return Settle(5f);
            Describe("ROSTER hint 1");
            yield return Snap("roster_hint_1of4");

            // RARITIES (628 px diagram) -> STATS: the plate height eases. Strip every 2 frames.
            yield return TapNext("roster 1->2");
            yield return FrameStrip("roster_swap_1to2", 22, 2);
            yield return Settle(0.4f);
            Describe("ROSTER hint 2");
            yield return Snap("roster_hint_2of4");

            // One tap == one step: two taps in one frame, the second must be ignored.
            int before = Modal.Index;
            Press(NextButton, "double-tap 1"); Press(NextButton, "double-tap 2");
            Log("DOUBLE TAP in one frame: index " + before + " -> " + Modal.Index + " (expect +1)");
            yield return Settle(0.7f);
            Describe("ROSTER hint 3 after double tap");
            yield return Snap("roster_hint_3of4");

            // 3/4 -> 4/4: the gold label must only change inside the fade seam.
            yield return TapNext("roster 3->4");
            yield return FrameStrip("roster_swap_3to4", 22, 2);
            yield return Settle(0.4f);
            Describe("ROSTER hint 4 (last)");
            yield return Snap("roster_hint_4of4");

            // BACK from the last hint: counter bumps the other way, CLOSE reverts to CONTINUE.
            yield return TapBack("roster 4->3");
            yield return FrameStrip("roster_swap_4to3", 14, 0);
            yield return Settle(0.4f);
            Describe("ROSTER hint 3 after BACK");
            yield return Snap("roster_hint_3of4_after_back");
            yield return CloseGroup("roster");

            // ── Inventory 1/2, 2/2 ────────────────────────────────────────────
            yield return NavBar(p => p.inventoryButton, "Inventory");
            yield return WaitVisible(10f, "Inventory");
            yield return Settle(4f);
            Describe("INVENTORY hint 1");
            yield return Snap("inventory_hint_1of2");
            yield return TapNext("inventory 1->2"); yield return Settle(0.8f);
            Describe("INVENTORY hint 2");
            yield return Snap("inventory_hint_2of2");
            yield return CloseGroup("inventory");

            // ── GeneralShop: CLOSE alone, no counter, no BACK ─────────────────
            yield return Show(ScreenId.GeneralShop);
            yield return WaitVisible(10f, "GeneralShop");
            yield return Settle(4f);
            Describe("GENERALSHOP single hint");
            yield return Snap("generalshop_hint_single");
            yield return CloseGroup("generalshop");

            // ── ModeSelection then MissionSelection (default b) ───────────────
            yield return Show(ScreenId.ModeSelection);
            yield return WaitVisible(10f, "ModeSelection");
            yield return Settle(3f);
            Describe("MODESELECTION hint 1");
            yield return Snap("modeselection_hint_1of3");
            yield return CloseGroup("modeselection");
            yield return Show(ScreenId.MissionSelection);
            yield return Settle(2f);
            Log("MISSIONSELECTION: modal visible=" + (Modal != null && Modal.IsVisible()) + " (Home showed TIP_DAILY, ModeSelection showed TIP_MISSIONS -> nothing left) state=" + State());
            yield return Snap("missionselection_no_hint");

            // ── Stacking: another modal already up when the screen changes ────
            var scheme = SchemeConfirmModalController.Instance;
            if (scheme != null)
            {
                var other = ControlSchemeService.Current == ControlScheme.Pendulum ? ControlScheme.Flick : ControlScheme.Pendulum;
                scheme.Show(other, "hint_verify");
                Log("STACK: opened SchemeConfirmModal first; openModals=" + ModalController.OpenModalCount);
                yield return Show(ScreenId.Leaderboard);
                yield return Settle(1.5f);
                Log("STACK: 1.5s after Leaderboard: hint visible=" + (Modal != null && Modal.IsVisible()) + " (expect false) openModals=" + ModalController.OpenModalCount);
                yield return Snap("leaderboard_hint_waiting_behind_modal");
                scheme.Hide();
                Log("STACK: SchemeConfirmModal hidden");
                yield return WaitVisible(5f, "Leaderboard after stack");
                yield return Settle(3f);
                Describe("LEADERBOARD hint after the stacked modal closed");
                yield return Snap("leaderboard_hint_after_stack");
                yield return CloseGroup("leaderboard");
            }
            else Log("STACK: no SchemeConfirmModal instance — skipped");

            // ── Settings › Controls ───────────────────────────────────────────
            yield return NavBar(p => p.homeButton, "Home");
            yield return Settle(0.8f);
            var pui = FindLive<PersistentUIManager>();
            if (pui != null && pui.settingsButton != null) { pui.settingsButton.onClick.Invoke(); Log("tapped settings gear"); }
            yield return Settle(1.2f);
            Log("SETTINGS opened: hint visible=" + (Modal != null && Modal.IsVisible()) + " (expect false — Controls not expanded yet)");
            var settings = FindLive<SettingsController>();
            var controls = settings != null ? settings.controlsSubmenu : null;
            var row = controls != null ? controls.GetComponentInParent<SettingsMenuItem>(true) : null;
            if (row != null)
            {
                var rowButton = row.GetComponent<Button>();
                if (rowButton != null) { rowButton.onClick.Invoke(); Log("tapped Controls row button"); }
                else { row.Expand(); Log("Controls row Expand()"); }
            }
            else Log("Controls row not found");
            yield return WaitVisible(10f, "SettingsControls");
            yield return Settle(4f);
            Describe("SETTINGS CONTROLS hint");
            var settingsCanvas = settings != null ? settings.GetComponent<Canvas>() : null;
            Log("sorting: hint canvas=" + Modal.GetComponent<Canvas>().sortingOrder + " settings canvas=" + (settingsCanvas != null ? settingsCanvas.sortingOrder.ToString() : "?"));
            yield return Snap("settings_controls_hint");
            yield return CloseGroup("settings_controls");
            if (settings != null) { settings.CloseSettings(); Log("settings closed"); }
            yield return Settle(0.8f);
            if (pui != null && pui.settingsButton != null) { pui.settingsButton.onClick.Invoke(); }
            yield return Settle(0.6f);
            if (row != null) { var rb = row.GetComponent<Button>(); if (rb != null) rb.onClick.Invoke(); else row.Expand(); }
            yield return Settle(1.5f);
            Log("SETTINGS CONTROLS second open: hint visible=" + (Modal != null && Modal.IsVisible()) + " (expect false)");
            if (settings != null) settings.CloseSettings();
            yield return Settle(0.8f);

            // ── Hole load: loading screen shows the rewritten TIP_RP, then the shot view hints ──
            var lsc = Resources.FindObjectsOfTypeAll<LoadingScreenController>()
                .FirstOrDefault(c => c != null && !string.IsNullOrEmpty(c.gameObject.scene.name));
            var minTime = typeof(LoadingScreenController).GetField("minLoadingTime", BindingFlags.Instance | BindingFlags.NonPublic);
            if (lsc != null && minTime != null) { minTime.SetValue(lsc, 30f); Log("loading screen held 30s for the TIP_RP capture"); }

            yield return TapWhenLive("PlayButton");
            // HoleSelection has its own hints (AUTOCLUB, SURFACES). A player must CLOSE them
            // before the hole card is tappable — the scrim covers it — so the bot does too.
            yield return WaitVisible(10f, "HoleSelection");
            yield return Settle(4f);
            Describe("HOLESELECTION hint 1");
            yield return Snap("holeselection_hint_1of2");
            yield return CloseGroup("holeselection");
            yield return Settle(0.5f);
            yield return TapWhenLive("ActionButton");

            ProTipCard card = null; t = 0f;
            while (t < 20f) { card = FindLive<ProTipCard>(); if (card != null) break; t += Time.unscaledDeltaTime; yield return null; }
            if (card != null)
            {
                var cycle = typeof(ProTipCard).GetField("autoCycleInterval", BindingFlags.Instance | BindingFlags.NonPublic);
                if (cycle != null) cycle.SetValue(card, 600f);
                var cardText = card.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(x => x.name == "TipText");
                var keyF = typeof(LocalizedText).GetField("key", BindingFlags.Instance | BindingFlags.NonPublic);
                string k = "";
                for (int i = 0; i < 70; i++)
                {
                    k = cardText != null ? (string)keyF.GetValue(cardText.GetComponent<LocalizedText>()) : "";
                    if (k == "TIP_RP") break;
                    var ped = new PointerEventData(EventSystem.current);
                    card.OnPointerClick(ped);
                    yield return new WaitForSecondsRealtime(0.4f);
                }
                Log("LOADING SCREEN: card key=" + k + " text='" + (cardText != null ? cardText.text.Replace("\n", " ") : "?") + "'");
                if (k == "TIP_RP") { yield return Settle(0.5f); yield return Snap("loading_screen_tip_rp_new_copy"); }
            }
            else Log("no ProTipCard appeared on the loading screen");

            // Wait for the hint over the tee.
            t = 0f;
            while (t < 90f)
            {
                var m = Modal;
                if (m != null && m.IsVisible()) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            bool loadingActive = lsc != null && lsc.gameObject.activeInHierarchy;
            Log("GAMEPLAY: hint visible after " + t.ToString("F1") + "s; loading screen active=" + loadingActive + " (expect false) scenes=" +
                string.Join(",", Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Select(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name)));
            yield return Settle(5f);
            Describe("GAMEPLAY hint 1");
            yield return Snap("gameplay_hint_1of6");

            // Input under the scrim: what does a raycast at the cone hit?
            var es = EventSystem.current;
            if (es != null)
            {
                var ped = new PointerEventData(es) { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.30f) };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                es.RaycastAll(ped, hits);
                Log("RAYCAST at cone (" + ped.position + "): top=" + (hits.Count > 0 ? hits[0].gameObject.name + " under " + Root(hits[0].gameObject.transform) : "nothing")
                    + " sortingOrder=" + (hits.Count > 0 ? hits[0].sortingOrder.ToString() : "-") + " openModals=" + ModalController.OpenModalCount);
            }

            for (int i = 2; i <= 6; i++)
            {
                yield return TapNext("gameplay " + (i - 1) + "->" + i);
                yield return Settle(0.7f);
                Describe("GAMEPLAY hint " + i);
                if (i == 6) yield return Snap("gameplay_hint_6of6");
            }
            yield return TapNext("gameplay CLOSE");
            Log("tee-idle timer right after CLOSE: " + TeeIdleTimer());
            yield return WaitHidden(2f);
            yield return Settle(1f);
            Log("tee-idle timer 1s later: " + TeeIdleTimer() + " state=" + State());
            yield return Snap("gameplay_after_close");

            // Second entry into the shot view: nothing (the same static hook the loader calls).
            ScreenHintPresenter.NotifyScreenEntered(ScreenHintCatalog.GameplayScreen);
            yield return Settle(1.5f);
            Log("GAMEPLAY re-entry via NotifyScreenEntered: hint visible=" + (Modal != null && Modal.IsVisible()) + " (expect false)");

            Log("DONE");
            yield return Settle(0.5f);
            EditorApplication.ExitPlaymode();
        }

        static string Root(Transform t) { while (t.parent != null) t = t.parent; return t.name; }

        static string TeeIdleTimer()
        {
            var type = typeof(Golfin.Gameplay.UI.ShotUI.TeeIdleGlowController);
            var inst = type.GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            if (inst == null) return "(no TeeIdleGlowController instance)";
            var f = type.GetField("_idleTimer", BindingFlags.Instance | BindingFlags.NonPublic);
            return f != null ? ((float)f.GetValue(inst)).ToString("F2") + "s" : "(no _idleTimer)";
        }
    }
}
#endif
