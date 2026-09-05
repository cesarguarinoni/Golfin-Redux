#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.UI.Controls;
using Golfin.UI;
using Golfin.UI.Modals;

namespace Golfin.EditorTools.ShotUI
{
    /// <summary>
    /// <c>scheme_confirm_popup</c> acceptance, driven through THE PLAYER'S OWN ENTRY POINTS
    /// (PIPELINE_HARDENING § 2): boot → PLAY → hole card → the in-game gear's real scheme segment
    /// → the real CANCEL / CONFIRM buttons; then Home → Settings → CONTROLS → the real row button.
    /// No synthetic buttons, no <c>ControlSchemeService.Set</c> shortcut.
    ///
    /// <para>The gate is the JSON it writes (<c>scheme_confirm_invariants.json</c>), not the
    /// pictures (PIPELINE_HARDENING § 3). Every assertion is re-derived from LIVE state — the
    /// pref the service actually holds, world corners off the built RectTransforms, the sprite on
    /// the live Image, the tint read off the live gold button — never from what this bot asked
    /// for.</para>
    ///
    /// <para>Menu: <c>GOLFIN ▸ ShotUI ▸ Verify Scheme Confirm Pop-up</c>.</para>
    /// </summary>
    public static class SchemeConfirmVerify
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "SchemeConfirmVerify.Armed";

        public const string TaskDir  = "Docs/Specs/Active/scheme_confirm_popup";
        public static string ShotsDir => TaskDir + "/screenshots";

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/ShotUI/Verify Scheme Confirm Pop-up")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[SchemeConfirmE2E] already playing — stop first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(ShotsDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[SchemeConfirmE2E] armed — entering play mode.");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            Application.runInBackground = true;
            PendulumSchemeVerify.ForceCaptureResolution();
            var host = new GameObject("[SchemeConfirmVerifyBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<SchemeConfirmVerifyRunner>();
        }
    }

    public class SchemeConfirmVerifyRunner : MonoBehaviour
    {
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        readonly List<(string name, bool pass, string expected, string actual)> _inv =
            new List<(string, bool, string, string)>();
        readonly List<string> _log = new List<string>();

        void Note(string k, object v) { _log.Add(k + ": " + v); Debug.Log("[SchemeConfirmE2E] " + k + ": " + v); }

        void Assert(string name, bool pass, object expected, object actual)
        {
            _inv.Add((name, pass, Convert.ToString(expected, CultureInfo.InvariantCulture),
                                  Convert.ToString(actual,   CultureInfo.InvariantCulture)));
            Debug.Log($"[SchemeConfirmE2E] {(pass ? "PASS" : "FAIL")} {name}  expected={expected} actual={actual}");
        }

        void Near(string name, float expected, float actual, float tol)
            => Assert(name, Mathf.Abs(expected - actual) <= tol, expected.ToString("F2"), actual.ToString("F2"));

        void Fail(string why) { Assert("fatal", false, "run completes", why); }

        void Start() => StartCoroutine(Sequence());

        // ── boot helpers ─────────────────────────────────────────────────────
        static Button FindButton(string n) => UnityEngine.Object
            .FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(b => b.gameObject.name == n);

        static void ClickReal(Button b)
        {
            var ped = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerUpHandler);
            b.onClick.Invoke();
        }

        IEnumerator ClickWhenPresent(string n, float timeout = 90f)
        {
            for (float t = 0f; t < timeout; t += 0.25f)
            {
                var b = FindButton(n);
                if (b != null) { ClickReal(b); yield break; }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Note("TIMEOUT", "waiting for button " + n);
        }

        static IEnumerable<MonoBehaviour> HoleCards() => UnityEngine.Object
            .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(m => m.GetType().Name == "HoleCardController");

        IEnumerator ClickHoleCard(int hole, float timeout = 30f)
        {
            for (float t = 0f; t < timeout; t += 0.25f)
            {
                foreach (var c in HoleCards())
                {
                    var p = c.GetType().GetProperty("HoleNumber");
                    if (p == null || (int)p.GetValue(c) != hole) continue;
                    if (c.GetType().GetField("actionButton", NP)?.GetValue(c) is Button btn)
                    { ClickReal(btn); yield break; }
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Note("TIMEOUT", "hole card " + hole);
        }

        static GameObject FindAny(string name)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
            return null;
        }

        // ── capture ──────────────────────────────────────────────────────────
        readonly Dictionary<string, string> _md5 = new Dictionary<string, string>();

        string Snap(string label)
        {
            string p = CaptureCore.SnapPlayModeSafe(label);
            if (string.IsNullOrEmpty(p) || !File.Exists(p))
            { Note("CAPTURE_MISSING", label + " -> " + p); return null; }

            string h = Md5(p);
            foreach (var kv in _md5)
                if (kv.Value == h) Note("CAPTURE_STALE", $"{label} is byte-identical to {kv.Key}");
            _md5[label] = h;

            Directory.CreateDirectory(SchemeConfirmVerify.ShotsDir);
            string dst = Path.Combine(SchemeConfirmVerify.ShotsDir, label + ".png");
            File.Copy(p, dst, true);
            Note("capture", $"{label} -> {dst} ({new FileInfo(dst).Length} bytes)");
            return dst;
        }

        IEnumerator SnapAtEndOfFrame(string label)
        {
            yield return new WaitForEndOfFrame();
            Snap(label);
        }

        static string Md5(string path)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(md5.ComputeHash(fs));
        }

        // ── geometry, in the pop-up panel's own space (node space) ───────────
        RectTransform _panel;

        /// <summary>x/y/w/h of a descendant in PANEL-LOCAL, TOP-DOWN coordinates — the same frame
        /// the Figma node is measured in, so a built number and a node number are directly
        /// comparable with no mental arithmetic.</summary>
        Rect NodeRect(string path)
        {
            var tr = _panel.Find(path) as RectTransform;
            if (tr == null) return new Rect(float.NaN, float.NaN, 0f, 0f);
            var c = new Vector3[4]; tr.GetWorldCorners(c);
            Vector2 lo = _panel.InverseTransformPoint(c[0]);
            Vector2 hi = _panel.InverseTransformPoint(c[2]);
            return new Rect(lo.x + _panel.rect.width * 0.5f,
                            _panel.rect.height * 0.5f - hi.y,
                            hi.x - lo.x, hi.y - lo.y);
        }

        // ─────────────────────────────────────────────────────────────────────

        IEnumerator Sequence()
        {
            // Start from a known scheme so "tapping the current one is a no-op" is testable.
            ControlSchemeService.Set(ControlScheme.Flick, "verify_setup");

            yield return new WaitForSecondsRealtime(5f);
            yield return ClickWhenPresent("StartButton", 25f);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return ClickWhenPresent("PlayButton");
            yield return new WaitForSecondsRealtime(2.5f);

            int hole = 0;
            foreach (int h in new[] { 2, 1, 10, 4 })
            {
                if (!HoleCards().Any(c => (int)(c.GetType().GetProperty("HoleNumber")?.GetValue(c) ?? -1) == h)) continue;
                hole = h; yield return ClickHoleCard(h); break;
            }
            if (hole == 0) { Fail("no hole card available"); yield return Finish(); yield break; }

            for (float t = 0f; FindButton("HoleMap") == null && t < 120f; t += 0.5f)
                yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(4f);

            PendulumSchemeVerify.ForceCaptureResolution();
            yield return null; yield return null;
            Assert("capture.resolution_is_1170x2532", Screen.width == 1170 && Screen.height == 2532,
                   "1170x2532", $"{Screen.width}x{Screen.height}");

            yield return InGameSurface();
            yield return SettingsSurface();
            yield return Finish();
        }

        // ── In-game gear modal ───────────────────────────────────────────────

        IEnumerator InGameSurface()
        {
            var popup = SchemeConfirmModalController.Instance;
            Assert("popup.instance_exists_in_gameplay_scene", popup != null,
                   "a SchemeConfirmModal in LabScaffold", popup != null ? popup.gameObject.scene.name : "none");
            if (popup == null) yield break;

            var canvas = popup.GetComponent<Canvas>();
            Assert("popup.sorting_above_ingame_settings",
                   canvas != null && canvas.overrideSorting && canvas.sortingOrder > ModalScrim.SortingOrder,
                   "> " + ModalScrim.SortingOrder + " with overrideSorting",
                   canvas != null ? $"{canvas.sortingOrder} override={canvas.overrideSorting}" : "no Canvas");

            yield return ClickWhenPresent("SettingsButton", 15f);
            yield return new WaitForSecondsRealtime(1.5f);

            var gear = UnityEngine.Object.FindObjectsByType<InGameSettingsModalController>(
                           FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            // ── 1. tapping a DIFFERENT scheme opens the pop-up and writes nothing ──
            ControlScheme before = ControlSchemeService.Current;
            var seg = Segment(ControlScheme.Pendulum);
            Assert("ingame.real_segment_found", seg != null, "the gear modal's PENDULUM segment",
                   seg != null ? seg.name : "none");
            if (seg == null) yield break;

            ClickReal(seg);
            yield return new WaitForSecondsRealtime(0.8f);

            Assert("ingame.tap_opens_the_popup", popup.IsVisible(), true, popup.IsVisible());
            Assert("ingame.tap_does_not_change_the_scheme", ControlSchemeService.Current == before,
                   before, ControlSchemeService.Current);
            Assert("ingame.gear_modal_stays_open_underneath", gear != null && gear.IsVisible(),
                   true, gear != null && gear.IsVisible());
            Assert("ingame.pending_is_the_tapped_scheme", popup.PendingScheme == ControlScheme.Pendulum,
                   ControlScheme.Pendulum, popup.PendingScheme);
            Assert("ingame.telemetry_where_is_ingame_popup", popup.PendingSource == "ingame_popup",
                   "ingame_popup", popup.PendingSource);

            // Highlight must still be on the CURRENT scheme, not the tapped one.
            AssertHighlightStaysOnCurrent("ingame", before);

            _panel = popup.transform.Find("Panel") as RectTransform;
            yield return new WaitForSecondsRealtime(0.4f);
            MeasureFidelity("pendulum");
            yield return SnapAtEndOfFrame("ingame_popup_pendulum");

            // ── 2. CANCEL changes nothing ─────────────────────────────────────
            var cancel = _panel.Find("ButtonsRow/CancelButton").GetComponent<Button>();
            ClickReal(cancel);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert("ingame.cancel_closes_the_popup", !popup.IsVisible(), false, popup.IsVisible());
            Assert("ingame.cancel_leaves_the_scheme_alone", ControlSchemeService.Current == before,
                   before, ControlSchemeService.Current);

            // ── 3. CONFIRM commits ────────────────────────────────────────────
            ClickReal(Segment(ControlScheme.Pendulum));
            yield return new WaitForSecondsRealtime(0.8f);
            var confirm = _panel.Find("ButtonsRow/ConfirmButton").GetComponent<Button>();

            // Read the gold tint off the LIVE Image rather than asserting it from the report.
            var confirmImg = confirm.GetComponent<Image>();
            Assert("buttons.confirm_is_the_gold_main_button",
                   confirmImg.sprite != null && confirmImg.sprite.name == "Button - Retry",
                   "Button - Retry (gold)", confirmImg.sprite != null ? confirmImg.sprite.name : "<NONE>");
            Assert("buttons.confirm_tint_is_untinted_white",
                   ((Color32)confirmImg.color).r == 255 && ((Color32)confirmImg.color).g == 255 &&
                   ((Color32)confirmImg.color).b == 255 && ((Color32)confirmImg.color).a == 255,
                   "RGBA(255,255,255,255)", "#" + ColorUtility.ToHtmlStringRGBA(confirmImg.color));
            var cancelImg = _panel.Find("ButtonsRow/CancelButton").GetComponent<Image>();
            Assert("buttons.cancel_is_the_silver_main_button",
                   cancelImg.sprite != null && cancelImg.sprite.name == "ButtonCancel",
                   "ButtonCancel (silver)", cancelImg.sprite != null ? cancelImg.sprite.name : "<NONE>");

            ClickReal(confirm);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert("ingame.confirm_commits_the_scheme", ControlSchemeService.Current == ControlScheme.Pendulum,
                   ControlScheme.Pendulum, ControlSchemeService.Current);
            Assert("ingame.confirm_closes_the_popup", !popup.IsVisible(), false, popup.IsVisible());

            var shotHost = UnityEngine.Object.FindObjectsByType<ShotSchemeHost>(
                               FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            yield return new WaitForSecondsRealtime(1.5f);
            Assert("ingame.host_swapped_after_confirm",
                   shotHost != null && shotHost.ActiveScheme == ControlScheme.Pendulum,
                   ControlScheme.Pendulum, shotHost != null ? shotHost.ActiveScheme.ToString() : "no host");

            // ── 4. tapping the CURRENT scheme is a no-op ──────────────────────
            ClickReal(Segment(ControlScheme.Pendulum));
            yield return new WaitForSecondsRealtime(0.8f);
            Assert("ingame.tapping_the_current_scheme_opens_nothing", !popup.IsVisible(),
                   false, popup.IsVisible());

            // ── 5. the longest copy (Free Swing) still fits ───────────────────
            ClickReal(Segment(ControlScheme.FreeSwing));
            yield return new WaitForSecondsRealtime(1.0f);
            MeasureFidelity("freeswing");
            AssertFits("freeswing");
            yield return SnapAtEndOfFrame("ingame_popup_freeswing");

            ClickReal(_panel.Find("ButtonsRow/CancelButton").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(0.6f);
        }

        // ── Settings › Controls ──────────────────────────────────────────────
        //
        // Driven the way a player reaches it: the persistent gear -> the CONTROLS accordion row ->
        // the row's own Button. Reached AFTER the in-game pass because the Settings screen is a
        // ShellScene overlay that is available with gameplay loaded, so no scene teardown is
        // needed to test both surfaces in one run.
        IEnumerator SettingsSurface()
        {
            var settings = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(m => m != null && m.GetType().Name == "SettingsController");
            if (settings == null) { Assert("settings.controller_found", false, "a SettingsController", "none"); yield break; }

            settings.GetType().GetMethod("OpenSettings")?.Invoke(settings, null);
            yield return new WaitForSecondsRealtime(1.2f);

            // Expand the CONTROLS accordion row through its own toggle.
            var row = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(m => m != null && m.GetType().Name == "SettingsMenuItem"
                                  && m.gameObject.name.IndexOf("controls", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert("settings.controls_row_found", row != null, "the CONTROLS accordion row",
                   row != null ? row.gameObject.name : "none");
            if (row == null) yield break;

            bool expanded = (bool)row.GetType().GetProperty("IsExpanded").GetValue(row);
            if (!expanded) row.GetType().GetMethod("Expand").Invoke(row, null);
            yield return new WaitForSecondsRealtime(1.2f);

            var submenu = UnityEngine.Object.FindObjectsByType<ControlsSubmenu>(
                              FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            Assert("settings.submenu_live", submenu != null && submenu.isActiveAndEnabled,
                   true, submenu != null && submenu.isActiveAndEnabled);
            if (submenu == null) yield break;

            // The SETTINGS pop-up instance is the ShellScene one; Instance prefers the gameplay
            // scene's, so resolve the ShellScene one explicitly for the assertions below.
            var shellPopup = UnityEngine.Object
                .FindObjectsByType<SchemeConfirmModalController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(p => p.gameObject.scene.name == "ShellScene");
            Assert("settings.popup_instance_exists_in_shellscene", shellPopup != null,
                   "a SchemeConfirmModal under SettingsScreen", shellPopup != null ? "yes" : "none");

            var live = SchemeConfirmModalController.Instance;
            ControlScheme before = ControlSchemeService.Current;
            var target = before == ControlScheme.Needle ? ControlScheme.FreeSwing : ControlScheme.Needle;

            var rowBtn = RowButton(submenu, target);
            Assert("settings.real_row_button_found", rowBtn != null,
                   "the CONTROLS row Button for " + target, rowBtn != null ? rowBtn.name : "none");
            if (rowBtn == null) yield break;

            ClickReal(rowBtn);
            yield return new WaitForSecondsRealtime(0.9f);

            Assert("settings.tap_opens_the_popup", live != null && live.IsVisible(),
                   true, live != null && live.IsVisible());
            Assert("settings.tap_does_not_change_the_scheme", ControlSchemeService.Current == before,
                   before, ControlSchemeService.Current);
            Assert("settings.telemetry_where_is_settings_popup",
                   live != null && live.PendingSource == "settings_popup",
                   "settings_popup", live != null ? live.PendingSource : "no popup");

            if (live != null)
            {
                _panel = live.transform.Find("Panel") as RectTransform;
                MeasureFidelity("settings_" + target.ToString().ToLowerInvariant());
                AssertFits("settings_" + target.ToString().ToLowerInvariant());
            }
            yield return SnapAtEndOfFrame("settings_popup_" + target.ToString().ToLowerInvariant());

            // CANCEL leaves everything alone, including the row highlight.
            ClickReal(_panel.Find("ButtonsRow/CancelButton").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(0.7f);
            Assert("settings.cancel_leaves_the_scheme_alone", ControlSchemeService.Current == before,
                   before, ControlSchemeService.Current);

            // CONFIRM moves it.
            ClickReal(RowButton(submenu, target));
            yield return new WaitForSecondsRealtime(0.9f);
            ClickReal(_panel.Find("ButtonsRow/ConfirmButton").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(1.0f);
            Assert("settings.confirm_commits_the_scheme", ControlSchemeService.Current == target,
                   target, ControlSchemeService.Current);

            // And the current one is a no-op.
            ClickReal(RowButton(submenu, target));
            yield return new WaitForSecondsRealtime(0.8f);
            Assert("settings.tapping_the_current_scheme_opens_nothing",
                   live != null && !live.IsVisible(), false, live != null && live.IsVisible());
        }

        /// <summary>The real Settings row Button for a scheme, read off ControlsSubmenu's own
        /// serialized fields — the widget the player taps, not a lookalike found by name.</summary>
        static Button RowButton(ControlsSubmenu submenu, ControlScheme scheme)
        {
            string field = scheme switch
            {
                ControlScheme.Pendulum  => "pendulumButton",
                ControlScheme.Needle    => "tapTimingButton",
                ControlScheme.FreeSwing => "freeSwingButton",
                _                       => "flickButton",
            };
            var f = typeof(ControlsSubmenu).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            return f?.GetValue(submenu) as Button;
        }

        static Button Segment(ControlScheme scheme)
        {
            string want = scheme switch
            {
                ControlScheme.Pendulum  => "pendulum",
                ControlScheme.Needle    => "tap",
                ControlScheme.FreeSwing => "free",
                _                       => "flick",
            };
            return UnityEngine.Object
                .FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0
                                  && b.GetComponentInParent<InGameSettingsModalController>() != null);
        }

        /// <summary>The row/segment highlight must still be on the CURRENT scheme while the pop-up
        /// is open — a cancelled selection has to leave no trace (§ 1.1).</summary>
        void AssertHighlightStaysOnCurrent(string surface, ControlScheme current)
        {
            var cur = Segment(current);
            var tapped = Segment(ControlScheme.Pendulum);
            if (cur == null || tapped == null || cur == tapped) return;

            var curLabel    = cur.GetComponentInChildren<TextMeshProUGUI>(true);
            var tappedLabel = tapped.GetComponentInChildren<TextMeshProUGUI>(true);
            if (curLabel == null || tappedLabel == null) return;

            // The selected segment paints its label in the dark navy ink; the unselected ones white.
            bool curSelected = curLabel.color.grayscale < tappedLabel.color.grayscale;
            Assert(surface + ".highlight_stays_on_the_current_scheme", curSelected,
                   current + " still reads as selected",
                   $"{current} ink {ColorUtility.ToHtmlStringRGB(curLabel.color)} vs tapped ink {ColorUtility.ToHtmlStringRGB(tappedLabel.color)}");
        }

        // ── Figma fidelity, measured off live RectTransforms ─────────────────

        void MeasureFidelity(string tag)
        {
            if (_panel == null) { Fail("no panel to measure"); return; }

            Near($"fidelity.{tag}.panel_width_1086", 1086f, _panel.rect.width, 1f);

            Rect steps  = NodeRect("StepsRow");
            Rect tile1  = NodeRect("StepsRow/Step1/Tile1");
            Rect tile2  = NodeRect("StepsRow/Step2/Tile2");
            Rect tile3  = NodeRect("StepsRow/Step3/Tile3");
            Rect sep    = NodeRect("SeparatorRow/ModalSeparator");
            Rect cancel = NodeRect("ButtonsRow/CancelButton");
            Rect conf   = NodeRect("ButtonsRow/ConfirmButton");
            Rect title  = NodeRect("TitleRow/TitleText");

            Near($"fidelity.{tag}.tile_row_left_margin_48",  48f, tile1.xMin, 0.5f);
            Near($"fidelity.{tag}.tile_row_right_margin_48", 48f, _panel.rect.width - tile3.xMax, 0.5f);
            Near($"fidelity.{tag}.tile_gap_24_a", 24f, tile2.xMin - tile1.xMax, 0.5f);
            Near($"fidelity.{tag}.tile_gap_24_b", 24f, tile3.xMin - tile2.xMax, 0.5f);
            Near($"fidelity.{tag}.tile_size_314x340_w", 314f, tile1.width, 0.5f);
            Near($"fidelity.{tag}.tile_size_314x340_h", 340f, tile1.height, 0.5f);
            Near($"fidelity.{tag}.gap_36_under_the_title_separator", 36f, tile1.yMin - sep.yMax, 1.0f);
            Near($"fidelity.{tag}.title_top_24", 24f, title.yMin, 0.5f);
            Near($"fidelity.{tag}.cancel_width_450", 450f, cancel.width, 0.5f);
            Near($"fidelity.{tag}.confirm_width_391", 391f, conf.width, 0.5f);
            Near($"fidelity.{tag}.button_gap_48", 48f, conf.xMin - cancel.xMax, 0.5f);
            Near($"fidelity.{tag}.buttons_centred",
                 cancel.xMin, _panel.rect.width - conf.xMax, 0.5f);

            // Tiles: a real sprite, not a flat fill (Rule 21's fabrication check).
            for (int i = 1; i <= 3; i++)
            {
                var img = _panel.Find($"StepsRow/Step{i}/Tile{i}").GetComponent<Image>();
                Assert($"tiles.{tag}.tile{i}_has_a_captured_sprite",
                       img.enabled && img.sprite != null,
                       "an in-game capture sprite",
                       img.sprite != null ? $"{img.sprite.name} ({img.sprite.rect.width}x{img.sprite.rect.height})" : "<NONE>");
            }

            // CONTAINMENT. Measured on the GLYPHS, not on the RectTransform: a TMP whose text
            // overflows its rect still reports the rect as 990 wide, which is exactly how three
            // clipped HOW IT WORKS lines passed a "990 == 990" width check. textBounds is the
            // drawn extent, and it is compared against the panel's own drawn width.
            foreach (var tmp in _panel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (!tmp.gameObject.activeInHierarchy) continue;
                tmp.ForceMeshUpdate();
                var b = tmp.textBounds;
                var rt = (RectTransform)tmp.transform;

                // Glyph extents -> panel-local, via the label's own rect.
                Vector3 lo = _panel.InverseTransformPoint(rt.TransformPoint(new Vector3(b.min.x, b.min.y)));
                Vector3 hi = _panel.InverseTransformPoint(rt.TransformPoint(new Vector3(b.max.x, b.max.y)));
                float half = _panel.rect.width * 0.5f;

                Assert($"contain.{tag}.{tmp.name}_glyphs_inside_the_panel",
                       lo.x >= -half - 1f && hi.x <= half + 1f,
                       $"x within +/-{half:F0}", $"[{lo.x:F1}, {hi.x:F1}]  text='{Trim(tmp.text)}'");
            }

            // Zero hardcoded text: every label must have resolved its key to something else.
            foreach (var tmp in _panel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                var loc = tmp.GetComponent<LocalizedText>();
                bool numeral = tmp.text.Length <= 2 && tmp.text.All(char.IsDigit);
                if (numeral) continue;   // the 1/2/3 indices are typography, by design

                Assert($"text.{tag}.{tmp.name}_is_localised",
                       loc != null && !tmp.text.StartsWith("(") && !LooksLikeAKey(tmp.text),
                       "a resolved localised value",
                       $"'{tmp.text}' loc={(loc != null)}");
            }
        }

        static string Trim(string s) => s.Length <= 42 ? s : s.Substring(0, 42) + "...";

        /// <summary>A raw key leaks through as SCREAMING_SNAKE — the failure mode when a label was
        /// given a literal or the table never resolved.</summary>
        static bool LooksLikeAKey(string s)
            => !string.IsNullOrEmpty(s) && s.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '_') && s.Contains("_");

        /// <summary>The pop-up must fit on screen with both buttons reachable — checked with the
        /// LONGEST copy (Free Swing), and at 16:9 as well as the 19.5:9 reference.</summary>
        void AssertFits(string tag)
        {
            var canvasRt = _panel.GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
            var c = new Vector3[4]; _panel.GetWorldCorners(c);
            Vector2 lo = canvasRt.InverseTransformPoint(c[0]);
            Vector2 hi = canvasRt.InverseTransformPoint(c[2]);

            Assert($"fits.{tag}.panel_inside_the_canvas",
                   lo.y >= canvasRt.rect.yMin - 0.5f && hi.y <= canvasRt.rect.yMax + 0.5f &&
                   lo.x >= canvasRt.rect.xMin - 0.5f && hi.x <= canvasRt.rect.xMax + 0.5f,
                   $"inside [{canvasRt.rect.xMin:F0},{canvasRt.rect.yMin:F0}]..[{canvasRt.rect.xMax:F0},{canvasRt.rect.yMax:F0}]",
                   $"[{lo.x:F0},{lo.y:F0}]..[{hi.x:F0},{hi.y:F0}]  panel {_panel.rect.width:F0}x{_panel.rect.height:F0}");

            foreach (var n in new[] { "ButtonsRow/CancelButton", "ButtonsRow/ConfirmButton" })
            {
                var b = _panel.Find(n) as RectTransform;
                var bc = new Vector3[4]; b.GetWorldCorners(bc);
                Vector2 blo = canvasRt.InverseTransformPoint(bc[0]);
                Vector2 bhi = canvasRt.InverseTransformPoint(bc[2]);
                Assert($"fits.{tag}.{b.name}_reachable",
                       blo.y >= canvasRt.rect.yMin && bhi.y <= canvasRt.rect.yMax,
                       "on screen", $"y [{blo.y:F0},{bhi.y:F0}]");
            }
        }

        // ── Output ───────────────────────────────────────────────────────────

        IEnumerator Finish()
        {
            var sb = new StringBuilder();
            int pass = _inv.Count(i => i.pass);
            sb.AppendLine("{");
            sb.AppendLine("  \"generated_utc\": \"" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\",");
            sb.AppendLine("  \"total\": " + _inv.Count + ", \"pass\": " + pass + ", \"fail\": " + (_inv.Count - pass) + ",");
            sb.AppendLine("  \"assertions\": [");
            for (int i = 0; i < _inv.Count; i++)
            {
                var a = _inv[i];
                sb.AppendLine($"    {{\"name\": \"{Esc(a.name)}\", \"pass\": {(a.pass ? "true" : "false")}, " +
                              $"\"expected\": \"{Esc(a.expected)}\", \"actual\": \"{Esc(a.actual)}\"}}" +
                              (i < _inv.Count - 1 ? "," : ""));
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            Directory.CreateDirectory(SchemeConfirmVerify.TaskDir);
            File.WriteAllText(SchemeConfirmVerify.TaskDir + "/scheme_confirm_invariants.json", sb.ToString());
            File.WriteAllLines(SchemeConfirmVerify.TaskDir + "/HEARTBEAT_verify.log", _log);

            Debug.Log($"[SchemeConfirmE2E] {pass}/{_inv.Count} invariants pass. " +
                      SchemeConfirmVerify.TaskDir + "/scheme_confirm_invariants.json");

            yield return new WaitForSecondsRealtime(0.5f);
            EditorApplication.isPlaying = false;
        }

        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
#endif
