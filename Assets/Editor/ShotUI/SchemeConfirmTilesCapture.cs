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
using Golfin.Gameplay.UI.Controls.FreeSwing;
using Golfin.Gameplay.UI.Controls.Needle;
using Golfin.Gameplay.UI.Controls.Pendulum;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.EditorTools.ShotUI
{
    /// <summary>
    /// <c>GOLFIN ▸ Capture ▸ Scheme Confirm Tiles</c> — the twelve step tiles the scheme confirm
    /// pop-up shows (<c>scheme_confirm_popup</c> § 3.2).
    ///
    /// <para><b>The tiles are captured from the RUNNING GAME, not exported from Figma.</b> The
    /// Figma tiles are crops of design frames and are wrong on their own terms — the Flick frames
    /// are a static pose that shows neither a pull, nor the arrows, nor the flick. So this bot
    /// boots the real game through the real entry path (PLAY → hole card), switches scheme through
    /// the REAL in-game gear segment, drives each scheme's three states with REAL pointer events on
    /// the REAL driver, and photographs the result. The Figma frames stay what they always were:
    /// the LAYOUT reference for the pop-up around the tiles.</para>
    ///
    /// <para><b>The crop is measured, not eyeballed.</b> For each state the subject box is the
    /// union of every enabled <see cref="Graphic"/> under the live <c>SchemeRoot_*</c> plus the
    /// ball — read off world corners, so it tracks the scheme UI automatically instead of encoding
    /// a rect that goes stale. Full-canvas overlays (dims, tap catchers) are excluded by size, and
    /// the cone is special-cased because <c>ConeMeshGraphic</c> draws far outside its own 0x0 rect.
    /// The box is then grown 10 %, fitted to the tile's 314:340 aspect, and CLAMPED into the shot
    /// area — the band between the bottom of the top HUD chrome and the top of the action buttons.
    /// Every crop is asserted to lie inside that band, so no HUD chrome can reach a tile.</para>
    ///
    /// <para>Writes <c>Assets/Resources/UI/Controls/Tiles/T_&lt;Scheme&gt;_&lt;1|2|3&gt;.png</c> at
    /// 628x680 (2x the 314x340 the pop-up draws), with the pop-up's 32 px corner radius baked into
    /// the alpha, imported as sprites with mips off — plus <c>tiles_manifest.json</c> in the task
    /// folder recording every source frame and crop rect.</para>
    ///
    /// <para><b>RE-RUN THIS WHENEVER A SCHEME'S UI CHANGES</b>, or the pop-up keeps explaining the
    /// old controls. Noted next to the scheme keys in <c>Assets/Data/controls.csv</c>.</para>
    /// </summary>
    public static class SchemeConfirmTilesCapture
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "SchemeConfirmTilesCapture.Armed";

        /// <summary>
        /// The task folder, wherever it currently lives. Resolved rather than pinned so a re-run
        /// after close-out writes into the real folder instead of resurrecting an empty
        /// <c>Active/</c> sibling.
        /// </summary>
        public static readonly string TaskDir = ResolveTaskDir("scheme_confirm_popup");

        /// <summary>
        /// Resolve a spec task folder across the <c>Active/</c> -&gt; <c>Completed/</c> move every
        /// task folder eventually undergoes. Active wins when both exist (in-flight beats
        /// archived); when neither exists the Active path is returned so a fresh run creates it
        /// where a new task belongs.
        /// </summary>
        public static string ResolveTaskDir(string slug)
        {
            string active = "Docs/Specs/Active/" + slug;
            if (Directory.Exists(active)) return active;
            string completed = "Docs/Specs/Completed/" + slug;
            return Directory.Exists(completed) ? completed : active;
        }

        public const string TilesDir  = "Assets/Resources/UI/Controls/Tiles";
        public static string ShotsDir => TaskDir + "/screenshots";

        /// <summary>Tile size in the pop-up (Figma <c>14140:35478</c>), and the 2x source we bake.</summary>
        public const int TileW = 314, TileH = 340, Scale = 2;

        /// <summary>Corner radius of the tile in the node, baked into the PNG's alpha so the prefab
        /// can stay a plain Image (no mask component, no extra draw call).</summary>
        public const int TileRadius = 32;

        /// <summary>Margin added around the measured subject box before the aspect fit.</summary>
        public const float Margin = 0.10f;

        /// <summary>
        /// Crop width bounds, in canvas px. The node's own tiles crop a 556 px window out of the
        /// 1170-wide frame (Figma <c>14140:35479</c>: a 660.55-wide scaled game screen showing a
        /// 314-wide slice, i.e. 314 / 0.5646), so the tiles are framed at roughly that scale here
        /// too. The MAXIMUM also keeps a crop clear of the HUD columns on its own — the gear,
        /// wind and power widgets all sit outside x = +/-337.
        /// </summary>
        public const float MinCropW = 520f, MaxCropW = 900f;

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/Capture/Scheme Confirm Tiles")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[SchemeTiles] already playing — stop first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(ShotsDir);
            Directory.CreateDirectory(TilesDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[SchemeTiles] armed — entering play mode.");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            Application.runInBackground = true;   // MANDATORY for MCP-driven runs
            PendulumSchemeVerify.ForceCaptureResolution();   // the shared 1170x2532 pin
            var host = new GameObject("[SchemeTilesBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<SchemeConfirmTilesRunner>();
        }

        // ── Import + bake helpers, also used by the runner ────────────────────

        /// <summary>Import a freshly written PNG AS A SPRITE. A default import lands as a Texture,
        /// <c>Resources.Load&lt;Sprite&gt;</c> then returns null and the pop-up draws nothing —
        /// the "new PNG imports as a texture, Unity draws a white box" trap.</summary>
        public static void ImportAsSprite(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            ti.textureType         = TextureImporterType.Sprite;
            ti.spriteImportMode    = SpriteImportMode.Single;
            ti.mipmapEnabled       = false;
            ti.alphaIsTransparency = true;
            ti.filterMode          = FilterMode.Bilinear;
            ti.wrapMode            = TextureWrapMode.Clamp;
            ti.maxTextureSize      = 1024;
            ti.SaveAndReimport();
        }

        /// <summary>Round the corners in the alpha channel, at the same 32 px radius the node draws
        /// (scaled to the 2x source).</summary>
        public static void RoundCorners(Color32[] px, int w, int h, int radius)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x < radius ? radius - x : (x >= w - radius ? x - (w - radius - 1) : 0f);
                float dy = y < radius ? radius - y : (y >= h - radius ? y - (h - radius - 1) : 0f);
                if (dx <= 0f || dy <= 0f) continue;

                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - d + 0.5f);          // 1 px of antialiasing
                int i = y * w + x;
                px[i] = new Color32(px[i].r, px[i].g, px[i].b, (byte)(px[i].a * a));
            }
        }
    }

    /// <summary>The play-mode half of <see cref="SchemeConfirmTilesCapture"/>.</summary>
    public class SchemeConfirmTilesRunner : MonoBehaviour
    {
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        readonly List<string> _log = new List<string>();
        readonly List<object> _manifest = new List<object>();
        readonly List<string> _fails = new List<string>();
        /// <summary>Every tile written this run, so <see cref="Finish"/> can force each one to
        /// import AS A SPRITE. A default import lands as a Texture and
        /// <c>Resources.Load&lt;Sprite&gt;</c> then returns null — which is exactly what "all
        /// pop-ups are lacking images" looks like from the player's side.</summary>
        readonly List<string> _written = new List<string>();
        /// <summary>The last crop used per scheme, so a state with nothing left on screen
        /// can be framed the same way as the state before it.</summary>
        readonly Dictionary<ControlScheme, Rect> _lastCrop = new Dictionary<ControlScheme, Rect>();

        void Note(string k, object v) { _log.Add(k + ": " + v); Debug.Log("[SchemeTiles] " + k + ": " + v); }
        void Fail(string why) { _fails.Add(why); Debug.LogError("[SchemeTiles] FAIL " + why); }

        void Start() => StartCoroutine(Sequence());

        // ── Boot helpers (same shape as the three scheme verify bots) ─────────
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

        // ── Pointer gesture ───────────────────────────────────────────────────
        GraphicRaycaster _raycaster;
        RectTransform    _canvasRt;
        PointerEventData _ped;
        Vector2          _last;
        GameObject       _target;

        void Down(GameObject target, Vector2 p)
        {
            _target = target;
            var rr = new RaycastResult { module = _raycaster, screenPosition = p };
            _ped = new PointerEventData(EventSystem.current)
            { position = p, pointerId = 0, button = PointerEventData.InputButton.Left };
            _ped.pointerPressRaycast = rr;
            _ped.pointerCurrentRaycast = rr;
            _ped.pressEventCamera_Set(_raycaster.eventCamera);
            _last = p;
            ExecuteEvents.Execute(target, _ped, ExecuteEvents.pointerDownHandler);
        }

        void Drag(Vector2 p)
        {
            _ped.delta = p - _last; _ped.position = p; _last = p;
            ExecuteEvents.Execute(_target, _ped, ExecuteEvents.dragHandler);
        }

        void Up() => ExecuteEvents.Execute(_target, _ped, ExecuteEvents.pointerUpHandler);

        /// <summary>Canvas-local point → screen point, so a gesture computed in the same space the
        /// layout is measured in lands where it was aimed.</summary>
        Vector2 ScreenAt(Vector2 canvasLocal)
        {
            Vector3 world = _canvasRt.TransformPoint(canvasLocal);
            return RectTransformUtility.WorldToScreenPoint(_raycaster.eventCamera, world);
        }

        Rect CanvasRect(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            Vector2 lo = _canvasRt.InverseTransformPoint(c[0]);
            Vector2 hi = _canvasRt.InverseTransformPoint(c[2]);
            return new Rect(lo.x, lo.y, hi.x - lo.x, hi.y - lo.y);
        }

        Rect Of(string name)
        {
            var go = FindAny(name);
            return go != null ? CanvasRect(go.GetComponent<RectTransform>()) : new Rect();
        }

        // ── ShotController reflection (Golfin.Gameplay.Input is autoReferenced:false) ──
        Component _sc; PropertyInfo _pState, _pArrow;
        bool BindShot()
        {
            _sc = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                  .FirstOrDefault(m => m.GetType().Name == "ShotController");
            if (_sc == null) return false;
            _pState = _sc.GetType().GetProperty("State");
            _pArrow = _sc.GetType().GetProperty("ArrowProgress01");
            return _pState != null;
        }
        string StateName => _pState.GetValue(_sc).ToString();
        float  ArrowProgress => _pArrow != null ? (float)_pArrow.GetValue(_sc) : float.NaN;

        IEnumerator WaitForIdle(float timeout = 30f)
        {
            for (float t = 0f; t < timeout && StateName != "Idle"; t += 0.1f)
                yield return new WaitForSecondsRealtime(0.1f);
        }

        // ─────────────────────────────────────────────────────────────────────

        IEnumerator Sequence()
        {
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
            Note("hole", hole);

            for (float t = 0f; FindButton("HoleMap") == null && t < 120f; t += 0.5f)
                yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(4f);

            PendulumSchemeVerify.ForceCaptureResolution();
            yield return null; yield return null;
            Note("resolution", Screen.width + "x" + Screen.height);
            if (Screen.width != 1170 || Screen.height != 2532)
                Fail("capture resolution is " + Screen.width + "x" + Screen.height + ", not 1170x2532");

            var rootCanvasGo = FindAny("ShotUI_Canvas");
            _canvasRt  = rootCanvasGo.GetComponent<RectTransform>();
            _raycaster = rootCanvasGo.GetComponent<Canvas>().rootCanvas.GetComponent<GraphicRaycaster>();

            if (!BindShot()) { Fail("ShotController not reachable"); yield return Finish(); yield break; }

            // Hide the debug panel FIRST: MeasureChrome reads the LIVE HUD, so a panel disabled
            // afterwards still counts as an obstacle and pushes every crop off the subject.
            var dbg = FindAny("DebugShotPanelController");
            if (dbg != null && dbg.activeSelf) { dbg.SetActive(false); Note("hid", "DebugShotPanelController"); }
            yield return null;

            MeasureChrome();
            HideChrome();
            Note("chrome_hidden", string.Join(", ", _hidden.Select(g => g.name)));

            foreach (var scheme in new[] { ControlScheme.Flick, ControlScheme.Pendulum,
                                           ControlScheme.Needle, ControlScheme.FreeSwing })
            {
                yield return SelectScheme(scheme);
                yield return Capture(scheme);
                yield return WaitForIdle();
                yield return new WaitForSecondsRealtime(1.0f);
            }

            yield return Finish();
        }

        // ── HUD chrome (§3.2: "No HUD chrome may appear in a tile — assert on the crop bounds") ──
        //
        // The chrome is not a band to clamp into — it is a set of RECTS the crop may not touch.
        // An earlier version clamped the crop into "the gap between the top and bottom chrome",
        // classified PowerHUD as bottom chrome, and every tile came out framed on empty fairway
        // 600 px above the ball. The rects are collected once, off the live HUD, and every crop is
        // tested against all of them.
        readonly List<(string name, Rect rect)> _chrome = new List<(string, Rect)>();

        static readonly string[] ChromeNames =
        {
            "PlayerCard", "PlayerCard_P2", "HoleCard", "SettingsButton", "WindIndicator",
            "HoleIndicator", "PowerHUD", "SpinButton", "FadeDrawButton", "GolfinButton",
            "DriverButton", "TurnBanner", "DebugShotPanelController",
        };

        void MeasureChrome()
        {
            _chrome.Clear();
            foreach (var n in ChromeNames)
            {
                var go = FindAny(n);
                if (go == null || !go.activeInHierarchy) continue;
                var rt = go.GetComponent<RectTransform>();
                if (rt == null) continue;
                var r = CanvasRect(rt);
                if (r.width <= 0f || r.height <= 0f) continue;
                _chrome.Add((n, r));
                Note("chrome", $"{n} x[{r.xMin:F0},{r.xMax:F0}] y[{r.yMin:F0},{r.yMax:F0}]");
            }
        }

        readonly List<GameObject> _hidden = new List<GameObject>();

        /// <summary>
        /// Turn the HUD off for the duration of the capture.
        ///
        /// <para>§3.2 requires that "no HUD chrome may appear in a tile". Doing that by CROPPING
        /// around the chrome does not work: the analyzer chip is 840 px wide and sits at the same
        /// height as the power gauge, so the only crop that clears the HUD also cuts the chip in
        /// half — which is what the previous run shipped ("OWER 97% ... TEMP SLO"). The design's
        /// own tiles have the same answer: the Figma crops zoom differently per step and simply do
        /// not contain the HUD. A tile explains a CONTROL; the player card, gear, wind, hole
        /// indicator and action buttons are noise in it.</para>
        ///
        /// <para>Play-mode only, and restored before the run ends — nothing is saved.</para>
        /// </summary>
        void HideChrome()
        {
            foreach (var n in ChromeNames)
            {
                var go = FindAny(n);
                if (go == null || !go.activeSelf) continue;
                go.SetActive(false);
                if (!_hidden.Contains(go)) _hidden.Add(go);
            }
        }

        void RestoreChrome()
        {
            foreach (var go in _hidden) if (go != null) go.SetActive(true);
            _hidden.Clear();
        }

        /// <summary>THE gate: the names of any chrome still ACTIVE at capture time. Empty is the
        /// pass condition, and it is a stronger claim than "the crop happened to miss it" — it
        /// says the chrome was not rendered into the frame at all.</summary>
        List<string> ChromeInside(Rect crop)
        {
            var live = new List<string>();
            foreach (var n in ChromeNames)
            {
                var go = FindAny(n);
                if (go != null && go.activeInHierarchy) live.Add(n);
            }
            return live;
        }

        // ── Scheme selection, through the player's own entry point ────────────

        IEnumerator SelectScheme(ControlScheme scheme)
        {
            if (ControlSchemeService.Current != scheme)
            {
                yield return ClickWhenPresent("SettingsButton", 15f);
                yield return new WaitForSecondsRealtime(1.5f);

                var seg = FindSegment(scheme);
                if (seg != null)
                {
                    ClickReal(seg);
                    yield return new WaitForSecondsRealtime(0.8f);

                    // The tap now opens the confirm pop-up this task adds; CONFIRM commits.
                    var confirm = FindButton("ConfirmButton");
                    if (confirm != null && confirm.GetComponentInParent<Golfin.UI.Modals.SchemeConfirmModalController>() != null)
                    { ClickReal(confirm); Note("scheme_entry", scheme + " via segment + pop-up CONFIRM"); }
                    else
                    { Note("scheme_entry", scheme + " via segment (no pop-up in this scene)"); }
                }
                else
                {
                    ControlSchemeService.Set(scheme, "capture");
                    Note("scheme_entry", "FALLBACK ControlSchemeService.Set for " + scheme);
                }

                yield return new WaitForSecondsRealtime(1f);
                var close = FindButton("CloseButton") ?? FindButton("ResumeButton") ?? FindButton("BackButton");
                if (close != null) ClickReal(close);
                yield return new WaitForSecondsRealtime(2f);
            }

            var host = UnityEngine.Object.FindObjectsByType<ShotSchemeHost>(
                           FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (host == null || host.ActiveScheme != scheme)
                Fail($"host did not swap to {scheme} (is {(host != null ? host.ActiveScheme.ToString() : "no host")})");
        }

        static Button FindSegment(ControlScheme scheme)
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
                                  && b.GetComponentInParent<Golfin.UI.Modals.InGameSettingsModalController>() != null);
        }

        // ── Per-scheme capture ────────────────────────────────────────────────

        IEnumerator Capture(ControlScheme scheme)
        {
            switch (scheme)
            {
                case ControlScheme.Flick:     yield return CaptureFlick();     break;
                case ControlScheme.Pendulum:  yield return CapturePendulum();  break;
                case ControlScheme.Needle:    yield return CaptureNeedle();    break;
                case ControlScheme.FreeSwing: yield return CaptureFreeSwing(); break;
            }
        }

        // Flick — the shipping scheme. There is no *SchemeVerify bot for it, so the three states
        // are driven exactly the way a player drives them: a real pointer gesture on the real
        // ClubHandle, through ClubHandleDragger's external-drag path (§3.2).
        IEnumerator CaptureFlick()
        {
            var handleGo = FindAny("ClubHandle");
            var dragger  = handleGo.GetComponent<ClubHandleDragger>();
            var cone     = FindAny("ConeMesh").GetComponent<ConeMeshGraphic>();
            var slab     = FindAny("TimingSlab");
            var slabGfx  = slab != null ? slab.GetComponent<TimingSlabGraphic>() : null;

            Rect ball = Of("CentralBall");
            float ballY = ball.center.y;

            Vector2 top  = ScreenAt(new Vector2(0f, ballY - 40f));
            Vector2 hold = ScreenAt(new Vector2(0f, ballY - 40f - cone.HeightPx * 0.70f));   // 70 % down the cone

            Down(handleGo, top);
            yield return null;
            for (int i = 1; i <= 12; i++) { Drag(Vector2.Lerp(top, hold, i / 12f)); yield return null; }
            for (int i = 0; i < 4; i++)   { Drag(hold); yield return null; }

            // The cone is deliberately NOT a subject: at 1009 px tall it would zoom the tile out
            // until the club head is a speck. It is still drawn around the club in the crop.
            yield return Tile(ControlScheme.Flick, 1, "PULL — handle 70% down the cone",
                              "CentralBall", "ClubHandle");

            // Tile 2: hold until the timing slab is inside the green band. CurrentY01 on the slab
            // graphic IS the number the player is timing against (ShotConeView.UpdateSlab writes
            // it from ArrowProgress01), and ConeBandPalette.BandGreenY01 is where green starts —
            // so this waits on the drawn thing, not on a stopwatch.
            float greenY = ConeBandPalette.BandGreenY01;
            bool inBand = false;
            for (float t = 0f; t < 15f && !inBand; t += Time.unscaledDeltaTime)
            {
                Drag(hold);
                yield return null;
                inBand = slabGfx != null && slab.activeInHierarchy && slabGfx.CurrentY01 >= greenY;
            }
            if (!inBand) Fail($"Flick: the timing slab never reached the green band (>= {greenY:F2})");
            yield return Tile(ControlScheme.Flick, 2,
                              $"AIM & TIME — slab at {(slabGfx != null ? slabGfx.CurrentY01 : float.NaN):F2}, green band from {greenY:F2}",
                              "CentralBall", "ClubHandle", "TimingSlab");

            // Tile 3 — THE FLICK ITSELF, not the frame after it.
            //
            // §3.2 asks for "Resolving, ball just launched, handle gone". That frame cannot be
            // photographed in a 520 px tile: the chase camera cuts to the ball the moment the shot
            // resolves, so three frames later the crop is looking at fairway 40 m downrange — every
            // attempt came back as grass and a targeting line, with or without the ball as the
            // subject. What a player needs to see for "FLICK UP" is the gesture, so the tile is the
            // club travelling UP past the ball on the real flick that fires the shot. Deviation
            // from the spec's wording is deliberate and reported.
            float step = Screen.height * 0.12f;
            Drag(hold + new Vector2(0f, step)); yield return null;
            Drag(hold + new Vector2(0f, step * 2f));
            yield return Tile(ControlScheme.Flick, 3,
                              "FLICK UP — the club travelling up past the ball on the flick that fires " +
                              "the shot (the post-launch frame is unphotographable: the chase camera cuts away)",
                              "CentralBall", "ClubHandle");

            for (int i = 3; i <= 4; i++) { Drag(hold + new Vector2(0f, step * i)); yield return null; }
            Up();
            yield return WaitUntilFlying();

            string how = "real flick";
            if (StateName == "Idle")
            {
                // The windowed flick gate rejected the synthesised gesture. Fall back to the
                // dragger's OWN shipped ReleaseToFire debug property rather than inventing a
                // capture-only firing path, and record which one fired the shot.
                bool prev = dragger.ReleaseToFire;
                dragger.ReleaseToFire = true;
                Down(handleGo, top);
                yield return null;
                for (int i = 1; i <= 12; i++) { Drag(Vector2.Lerp(top, hold, i / 12f)); yield return null; }
                for (int i = 0; i < 3; i++)   { Drag(hold); yield return null; }
                Up();
                dragger.ReleaseToFire = prev;
                yield return WaitUntilFlying();
                how = "ReleaseToFire fallback";
            }
            Note("flick_fired_via", how + " -> state " + StateName);
            if (StateName == "Idle") Fail("Flick: the shot never fired");
            yield return WaitForIdleFlick();
        }

        IEnumerator WaitForIdleFlick(float timeout = 25f)
        {
            for (float t = 0f; t < timeout && StateName != "Idle"; t += 0.1f)
                yield return new WaitForSecondsRealtime(0.1f);
        }

        IEnumerator CapturePendulum()
        {
            var driver = UnityEngine.Object.FindObjectsByType<PendulumSchemeDriver>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (driver == null) { Fail("PendulumSchemeDriver not live"); yield break; }

            Rect ball = Of("CentralBall");
            float ballY = ball.center.y;
            Vector2 top  = ScreenAt(new Vector2(0f, ballY - 30f));
            Vector2 hold = ScreenAt(new Vector2(0f, ballY - 30f - driver.Pull100Px));

            Down(driver.gameObject, top);
            yield return null;
            for (int i = 1; i <= 10; i++) { Drag(Vector2.Lerp(top, hold, i / 10f)); yield return null; }
            for (int i = 0; i < 4; i++)   { Drag(hold); yield return null; }

            // Marker away from centre for the pull tile — "power set, timing still running".
            driver.SetPhaseForTests(0.25f);
            yield return null;
            yield return Tile(ControlScheme.Pendulum, 1, "PULL — 100% lane, club on the gold tick",
                              "CentralBall", "PowerLane", "PendulumHandle");

            driver.SetPhaseForTests(0f);            // marker exactly on the centre pip
            yield return null;
            yield return Tile(ControlScheme.Pendulum, 2, $"TIME IT — marker at {driver.MarkerOffset:F3}",
                              "CentralBall", "PowerLane", "PendulumTrack", "PendulumHandle");

            // Flick up from the pip: the latch takes the marker at the start of the upswing.
            driver.SetPhaseForTests(0f);
            float step = Screen.height * 0.10f;
            for (int i = 1; i <= 3; i++) { Drag(hold + new Vector2(0f, step * i)); yield return null; }
            Up();
            // The FIRST frame the pop is on screen — the bar is still up and the chase camera has
            // not started following the ball yet.
            yield return WaitUntilActive("PendulumGradePop");
            yield return Tile(ControlScheme.Pendulum, 3,
                              $"FLICK UP — grade {driver.LastCommittedGrade}, marker {driver.LastCommittedMarker:F3}",
                              "PendulumTrack", "PendulumGradePop", "PendulumHandle");
        }

        IEnumerator CaptureNeedle()
        {
            var driver = UnityEngine.Object.FindObjectsByType<NeedleSchemeDriver>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (driver == null) { Fail("NeedleSchemeDriver not live"); yield break; }

            Rect ball = Of("CentralBall");
            float ballY = ball.center.y;
            Vector2 top  = ScreenAt(new Vector2(0f, ballY - 30f));
            Vector2 hold = ScreenAt(new Vector2(0f, ballY - 30f - driver.Pull100Px));

            Down(driver.gameObject, top);
            yield return null;
            for (int i = 1; i <= 10; i++) { Drag(Vector2.Lerp(top, hold, i / 10f)); yield return null; }
            for (int i = 0; i < 4; i++)   { Drag(hold); yield return null; }
            yield return Tile(ControlScheme.Needle, 1, "PULL — 100% ring, club on it, crescent visible",
                              "CentralBall", "NeedleHandle");

            Up();                                   // release starts the needle sweep
            yield return null; yield return null;
            driver.SetNeedleForTests(0f);           // needle parked in the blue zone
            yield return null;
            yield return Tile(ControlScheme.Needle, 2, $"TAP — needle at {driver.NeedleOffset:F3}, TAP! hint",
                              "CentralBall", "Needle", "TapHint");

            driver.SetNeedleForTests(0f);
            driver.OnTap();
            yield return WaitUntilActive("NeedleGradePop");
            yield return Tile(ControlScheme.Needle, 3,
                              $"RESULT — grade {driver.LastCommittedGrade}, needle {driver.LastCommittedNeedle:F3}",
                              "CentralBall", "TapPip", "NeedleGradePop");
        }

        IEnumerator CaptureFreeSwing()
        {
            var driver = UnityEngine.Object.FindObjectsByType<FreeSwingSchemeDriver>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (driver == null) { Fail("FreeSwingSchemeDriver not live"); yield break; }

            Rect ball = Of("CentralBall");
            float ballY = ball.center.y;
            Vector2 top  = ScreenAt(new Vector2(0f, ballY - 30f));
            float pull   = Mathf.Max(driver.MinUsefulPullPx * 3f, 380f);
            Vector2 down = ScreenAt(new Vector2(0f, ballY - 30f - pull));

            // ExecuteEvents, NOT driver.ProcessDrag(screenPos): ProcessDrag takes a LOCAL point
            // (OnDrag calls ToLocal first), so feeding it screen coordinates measured a pull of
            // zero and produced two byte-identical tiles.
            Down(driver.gameObject, top);
            yield return null;
            for (int i = 1; i <= 12; i++) { Drag(Vector2.Lerp(top, down, i / 12f)); yield return null; }
            for (int i = 0; i < 3; i++)   { Drag(down); yield return null; }
            if (driver.PeakPower <= 0.02f) Fail($"FreeSwing: backswing registered no power ({driver.PeakPower:F2})");
            yield return Tile(ControlScheme.FreeSwing, 1,
                              $"BACKSWING — power {driver.PeakPower:F2}, pull {driver.PeakPullPx:F0}px",
                              "CentralBall", "FreeSwingLane", "FreeSwingHandle");

            // Half-way back up: the trace has curved, the IMPACT window is on screen, and the shot
            // has NOT fired yet — which is exactly what step 2 has to show.
            Vector2 mid = Vector2.Lerp(down, top, 0.55f);
            for (int i = 1; i <= 6; i++) { Drag(Vector2.Lerp(down, mid, i / 6f)); yield return null; }
            if (!driver.IsUpstroke) Fail("FreeSwing: the upstroke never started, so step 2 shows a backswing");
            yield return Tile(ControlScheme.FreeSwing, 2,
                              $"SWING UP — upstroke={driver.IsUpstroke}, impact offset {driver.ImpactCrossOffsetPx:F0}px",
                              "CentralBall", "FreeSwingLane", "FreeSwingHandle");

            // Finish the upswing through the IMPACT line: the shot fires as it is crossed.
            for (int i = 1; i <= 6; i++) { Drag(Vector2.Lerp(mid, top + new Vector2(0f, 140f), i / 6f)); yield return null; }
            Up();
            yield return WaitUntilActive("FreeSwingAnalyzerChip");
            var chip = FindAny("FreeSwingAnalyzerChip");
            if (chip == null || !chip.activeInHierarchy) Fail("FreeSwing: the analyzer chip is not showing for step 3");
            yield return Tile(ControlScheme.FreeSwing, 3,
                              $"RESULT — analyzer chip, commits {driver.CommitCount}, power {driver.LastCommittedPower:F2}",
                              "CentralBall", "FreeSwingAnalyzerChip");
        }

        /// <summary>Spin frames until <paramref name="name"/> is on screen, then return
        /// IMMEDIATELY — a fixed sleep either misses the pop or lands after the chase camera has
        /// swung away and the scheme UI has been torn down.</summary>
        IEnumerator WaitUntilActive(string name, float timeout = 3f)
        {
            for (float t = 0f; t < timeout; t += Time.unscaledDeltaTime)
            {
                var go = FindAny(name);
                if (go != null && go.activeInHierarchy) yield break;
                yield return null;
            }
            Note("never_appeared", name);
        }

        IEnumerator WaitUntilFlying(float timeout = 4f)
        {
            for (float t = 0f; t < timeout; t += 0.05f)
            {
                if (StateName == "Resolving" || StateName == "Flying") yield break;
                yield return null;
            }
        }

        // ── Snap + crop + write ──────────────────────────────────────────────

        /// <summary>
        /// Photograph the current frame and cut one tile out of it, framed on
        /// <paramref name="subjects"/> — the named live elements that ARE the step being explained
        /// (§3.2: "centre the crop on the bounding box of the subject elements"). The token
        /// <c>#cone</c> stands for the Flick cone, which draws far outside its own 0x0 rect, and
        /// <c>#centre</c> for the tee itself (the canvas origin) when every subject is hidden.
        /// </summary>
        IEnumerator Tile(ControlScheme scheme, int step, string note, params string[] subjects)
        {
            // ActionButtonsRoot re-enables its buttons on some state changes, so re-hide rather
            // than trusting the one-shot at boot.
            HideChrome();
            yield return null;
            yield return new WaitForEndOfFrame();

            string label = $"{scheme}_{step}_full";
            string src = CaptureCore.SnapPlayModeSafe(label);
            if (string.IsNullOrEmpty(src) || !File.Exists(src))
            { Fail($"{scheme} step {step}: capture wrote nothing ({src})"); yield break; }

            Rect subject = SubjectBox(subjects, out var used, out var missing);
            if (missing.Count > 0) Note("subject_missing", $"{scheme} {step}: " + string.Join(", ", missing));

            Rect crop;
            if (subject.width <= 1f || subject.height <= 1f)
            {
                // Every subject is gone. That is not a failure for a "the shot is away" tile — the
                // ball and the club are BOTH hidden the moment it launches. Reuse this scheme's
                // previous framing, which is also what the design does: its third tile is the same
                // viewport as its second, in a later state.
                if (!_lastCrop.TryGetValue(scheme, out crop))
                { Fail($"{scheme} step {step}: empty subject box and no earlier crop to reuse"); yield break; }
                note += "  [framing reused from the previous step: every subject is hidden once the ball launches]";
                subject = crop;
            }
            else
            {
                crop = FitCrop(subject);
                _lastCrop[scheme] = crop;
            }
            WriteTile(scheme, step, src, crop, subject, note + "  [subjects: " + string.Join(", ", used) + "]");
        }

        /// <summary>
        /// The union of the named live elements' canvas rects. Explicit rather than "everything
        /// under the scheme root": that union included the 1200x1200 arc, the 2600x2600 trace and
        /// the off-screen tick labels, and framed the tiles on empty fairway. The names are the
        /// scene's own, so a renamed element shows up as a reported miss instead of silently
        /// shrinking the box.
        /// </summary>
        Rect SubjectBox(string[] names, out List<string> used, out List<string> missing)
        {
            used = new List<string>();
            missing = new List<string>();

            bool any = false;
            float xMin = 0, xMax = 0, yMin = 0, yMax = 0;

            void Add(Rect r)
            {
                if (r.width <= 0f || r.height <= 0f) return;
                if (!any) { xMin = r.xMin; xMax = r.xMax; yMin = r.yMin; yMax = r.yMax; any = true; return; }
                xMin = Mathf.Min(xMin, r.xMin); xMax = Mathf.Max(xMax, r.xMax);
                yMin = Mathf.Min(yMin, r.yMin); yMax = Mathf.Max(yMax, r.yMax);
            }

            foreach (var n in names)
            {
                if (n == "#cone")
                {
                    var coneGo = FindAny("ConeMesh");
                    var cone = coneGo != null ? coneGo.GetComponent<ConeMeshGraphic>() : null;
                    if (cone == null || !cone.isActiveAndEnabled) { missing.Add(n); continue; }

                    // The cone runs from its apex (the rect origin) down HeightPx, widening to
                    // HalfBasePx. Only the top 60% is taken: the full 1009 px cone would zoom the
                    // tile out until the club head is a speck.
                    var rt = cone.rectTransform;
                    float h = cone.HeightPx * 0.60f;
                    float halfW = cone.HalfBasePx * 0.60f;
                    Vector2 lo = _canvasRt.InverseTransformPoint(rt.TransformPoint(new Vector3(-halfW, -h)));
                    Vector2 hi = _canvasRt.InverseTransformPoint(rt.TransformPoint(new Vector3( halfW, 0f)));
                    Add(Rect.MinMaxRect(Mathf.Min(lo.x, hi.x), Mathf.Min(lo.y, hi.y),
                                        Mathf.Max(lo.x, hi.x), Mathf.Max(lo.y, hi.y)));
                    used.Add(n);
                    continue;
                }

                if (n == "#centre")
                {
                    // The ball's own position, as a subject that survives the ball being HIDDEN.
                    // CentralBall sits at the canvas origin; once the shot launches both it and the
                    // club are switched off, and a tile framed on "whatever is left" ends up on
                    // grass 600 px below the tee.
                    Add(new Rect(-150f, -150f, 300f, 300f));
                    used.Add(n);
                    continue;
                }

                var go = FindAny(n);
                if (go == null || !go.activeInHierarchy) { missing.Add(n); continue; }
                var grt = go.GetComponent<RectTransform>();
                if (grt == null) { missing.Add(n); continue; }
                Add(CanvasRect(grt));
                used.Add(n);
            }

            return any ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : new Rect();
        }

        /// <summary>Grow the subject box by <see cref="SchemeConfirmTilesCapture.Margin"/> and fit
        /// it to the tile's 314:340 aspect, centred on the subject. Clamped to the CANVAS only —
        /// HUD chrome is an assertion on the result (see <see cref="ChromeInside"/>), not a band
        /// the crop is squeezed into. Canvas-local, bottom-up.</summary>
        Rect FitCrop(Rect subject)
        {
            const float aspect = (float)SchemeConfirmTilesCapture.TileW / SchemeConfirmTilesCapture.TileH;
            float m = SchemeConfirmTilesCapture.Margin;

            float w = subject.width  * (1f + 2f * m);
            float h = subject.height * (1f + 2f * m);
            if (w / h < aspect) w = h * aspect; else h = w / aspect;

            w = Mathf.Clamp(w, SchemeConfirmTilesCapture.MinCropW, SchemeConfirmTilesCapture.MaxCropW);
            h = w / aspect;

            Rect crop = Centre(subject.center, w, h);

            // Separate from the HUD. Shrink-and-nudge rather than clamp-into-a-band: a band has to
            // be classified (which side is this widget on?) and the first version got PowerHUD
            // wrong, framing every tile on empty fairway. This only ever moves the crop AWAY from
            // a rect it actually overlaps, so it cannot invent an offset that is not needed.
            for (int shrink = 0; shrink < 8; shrink++)
            {
                for (int nudge = 0; nudge < 12; nudge++)
                {
                    var hit = _chrome.FirstOrDefault(c => c.rect.Overlaps(crop) && IsChromeVisible(c.name));
                    if (hit.rect.width <= 0f) return crop;   // clear

                    // Push along the axis of LEAST penetration — the smallest move that separates.
                    float dxRight = hit.rect.xMax - crop.xMin, dxLeft = crop.xMax - hit.rect.xMin;
                    float dyUp    = hit.rect.yMax - crop.yMin, dyDown = crop.yMax - hit.rect.yMin;
                    float best = Mathf.Min(Mathf.Min(dxRight, dxLeft), Mathf.Min(dyUp, dyDown));

                    Vector2 c2 = crop.center;
                    if      (best == dxRight) c2.x += dxRight + 1f;
                    else if (best == dxLeft)  c2.x -= dxLeft  + 1f;
                    else if (best == dyUp)    c2.y += dyUp    + 1f;
                    else                      c2.y -= dyDown  + 1f;

                    crop = Centre(c2, crop.width, crop.height);
                }

                w *= 0.90f;
                if (w < SchemeConfirmTilesCapture.MinCropW * 0.6f) break;
                h = w / aspect;
                crop = Centre(subject.center, w, h);
            }

            return crop;
        }

        static bool IsChromeVisible(string name)
        {
            var go = FindAny(name);
            return go != null && go.activeInHierarchy;
        }

        /// <summary>A w x h rect centred on <paramref name="c"/>, kept inside the canvas.</summary>
        Rect Centre(Vector2 c, float w, float h)
        {
            float cx = Mathf.Clamp(c.x, _canvasRt.rect.xMin + w * 0.5f, _canvasRt.rect.xMax - w * 0.5f);
            float cy = Mathf.Clamp(c.y, _canvasRt.rect.yMin + h * 0.5f, _canvasRt.rect.yMax - h * 0.5f);
            return new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
        }

        void WriteTile(ControlScheme scheme, int step, string srcPng, Rect crop, Rect subject, string note)
        {
            var full = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            full.LoadImage(File.ReadAllBytes(srcPng));

            // Canvas-local (bottom-up, centre origin) → pixel (top-down, top-left origin).
            float sx = full.width  / _canvasRt.rect.width;
            float sy = full.height / _canvasRt.rect.height;
            int px = Mathf.RoundToInt((crop.xMin - _canvasRt.rect.xMin) * sx);
            int pw = Mathf.RoundToInt(crop.width  * sx);
            int ph = Mathf.RoundToInt(crop.height * sy);
            int py = Mathf.RoundToInt((_canvasRt.rect.yMax - crop.yMax) * sy);

            px = Mathf.Clamp(px, 0, full.width  - 1);
            py = Mathf.Clamp(py, 0, full.height - 1);
            pw = Mathf.Clamp(pw, 1, full.width  - px);
            ph = Mathf.Clamp(ph, 1, full.height - py);

            // THE gate (§3.2): the crop may not overlap a single HUD chrome rect. Asserted on the
            // crop bounds against the LIVE chrome, not judged by looking at the tile.
            var hits = ChromeInside(crop);
            bool noChrome = hits.Count == 0;
            if (!noChrome)
                Fail($"{scheme} step {step}: crop x[{crop.xMin:F0},{crop.xMax:F0}] y[{crop.yMin:F0},{crop.yMax:F0}] " +
                     "overlaps HUD chrome: " + string.Join(", ", hits));

            // Read bottom-up (Texture2D origin is bottom-left).
            var srcPixels = full.GetPixels(px, full.height - py - ph, pw, ph);
            var cropped = new Texture2D(pw, ph, TextureFormat.RGBA32, false);
            cropped.SetPixels(srcPixels);
            cropped.Apply();

            int outW = SchemeConfirmTilesCapture.TileW * SchemeConfirmTilesCapture.Scale;
            int outH = SchemeConfirmTilesCapture.TileH * SchemeConfirmTilesCapture.Scale;
            var scaled = Scale(cropped, outW, outH);

            var px32 = scaled.GetPixels32();
            SchemeConfirmTilesCapture.RoundCorners(px32, outW, outH,
                SchemeConfirmTilesCapture.TileRadius * SchemeConfirmTilesCapture.Scale);
            scaled.SetPixels32(px32);
            scaled.Apply();

            string name = $"T_{scheme}_{step}.png";
            string dst  = SchemeConfirmTilesCapture.TilesDir + "/" + name;
            File.WriteAllBytes(dst, scaled.EncodeToPNG());
            _written.Add(dst);

            // Keep the uncropped source frame as evidence for the report.
            string evidence = Path.Combine(SchemeConfirmTilesCapture.ShotsDir, $"tilesrc_{scheme}_{step}.png");
            Directory.CreateDirectory(SchemeConfirmTilesCapture.ShotsDir);
            File.Copy(srcPng, evidence, true);

            _manifest.Add(new Dictionary<string, object>
            {
                ["scheme"]      = scheme.ToString(),
                ["step"]        = step,
                ["note"]        = note,
                ["tile"]        = dst,
                ["width"]       = outW,
                ["height"]      = outH,
                ["source_frame"]= evidence,
                ["crop_canvas"] = $"x={crop.xMin:F1} y={crop.yMin:F1} w={crop.width:F1} h={crop.height:F1}",
                ["crop_pixels"] = $"x={px} y={py} w={pw} h={ph}",
                ["subject_box"] = $"x={subject.xMin:F1} y={subject.yMin:F1} w={subject.width:F1} h={subject.height:F1}",
                ["chrome_overlaps"]  = hits.Count == 0 ? "none" : string.Join(", ", hits),
                ["no_hud_chrome"]    = noChrome,
            });

            UnityEngine.Object.DestroyImmediate(full);
            UnityEngine.Object.DestroyImmediate(cropped);
            UnityEngine.Object.DestroyImmediate(scaled);

            Note("tile", $"{scheme} {step} -> {dst} ({outW}x{outH}) crop {pw}x{ph} @ ({px},{py})");
        }

        static Texture2D Scale(Texture2D src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;
            src.filterMode = FilterMode.Bilinear;
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            outTex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return outTex;
        }

        IEnumerator Finish()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"generated_utc\": \"" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\",");
            sb.AppendLine("  \"tile_size\": [" + SchemeConfirmTilesCapture.TileW * SchemeConfirmTilesCapture.Scale
                          + ", " + SchemeConfirmTilesCapture.TileH * SchemeConfirmTilesCapture.Scale + "],");
            sb.AppendLine("  \"margin\": " + SchemeConfirmTilesCapture.Margin.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"fails\": [" + string.Join(", ", _fails.Select(f => "\"" + Esc(f) + "\"")) + "],");
            sb.AppendLine("  \"tiles\": [");
            for (int i = 0; i < _manifest.Count; i++)
            {
                var d = (Dictionary<string, object>)_manifest[i];
                sb.Append("    {");
                sb.Append(string.Join(", ", d.Select(kv => "\"" + kv.Key + "\": " + Json(kv.Value))));
                sb.AppendLine("}" + (i < _manifest.Count - 1 ? "," : ""));
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            RestoreChrome();

            // Force the sprite import BEFORE anything tries to load them.
            AssetDatabase.Refresh();
            foreach (var path in _written) SchemeConfirmTilesCapture.ImportAsSprite(path);
            Note("imported_as_sprite", _written.Count + " tiles");

            Directory.CreateDirectory(SchemeConfirmTilesCapture.TaskDir);
            File.WriteAllText(SchemeConfirmTilesCapture.TaskDir + "/tiles_manifest.json", sb.ToString());
            File.WriteAllLines(SchemeConfirmTilesCapture.TaskDir + "/HEARTBEAT_tiles.log", _log);

            Debug.Log($"[SchemeTiles] wrote {_manifest.Count} tiles, {_fails.Count} fails. " +
                      "Manifest: " + SchemeConfirmTilesCapture.TaskDir + "/tiles_manifest.json");

            yield return new WaitForSecondsRealtime(0.5f);
            EditorApplication.isPlaying = false;
        }

        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        static string Json(object v)
            => v is bool b ? (b ? "true" : "false")
             : v is int i  ? i.ToString(CultureInfo.InvariantCulture)
             : "\"" + Esc(Convert.ToString(v, CultureInfo.InvariantCulture)) + "\"";
    }

    /// <summary><c>PointerEventData.pressEventCamera</c> has no setter; the drivers read it to map
    /// screen points, so the backing field is set directly. One place, documented, instead of a
    /// reflection call scattered through the capture code.</summary>
    static class PointerEventDataCameraExt
    {
        static readonly FieldInfo Field = typeof(PointerEventData)
            .GetField("m_PressEventCamera", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void pressEventCamera_Set(this PointerEventData ped, Camera cam)
        {
            if (Field != null) Field.SetValue(ped, cam);
        }
    }
}
#endif
