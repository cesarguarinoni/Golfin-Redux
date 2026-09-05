using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Golfin.Gameplay.UI.Controls;
using Golfin.Gameplay.UI.Controls.Pendulum;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.EditorTools.ShotUI
{
    /// <summary>
    /// Builds (or REBUILDS) the Pendulum scheme's uGUI under <c>SchemeRoot_Pendulum</c> from the
    /// Figma node values — scheme_pendulum §3.3, Figma "Scheme — Pendulum" 14091:33885.
    ///
    /// <para>A SCRIPT AND NOT HAND-AUTHORING, for the reason every builder tool in this project
    /// exists: the geometry here is a table of numbers read off a Figma node, and a table is
    /// something you re-run when the node moves, not something you re-drag. It is idempotent —
    /// it deletes the children it owns and rebuilds them — so a fidelity fix is a one-line edit
    /// here plus a re-run, and the reviewer can diff the source instead of the scene YAML.</para>
    ///
    /// <para>EVERY SHAPE IS A STADIUM, so every shape is <c>S_PillStadium</c> — a 176×176 sprite
    /// whose 88px border makes it a circle sliced into four caps. The only thing that changes per
    /// element is <c>pixelsPerUnitMultiplier</c>, set to <c>88 / wantedRadius</c> so the caps come
    /// out at the node's radius instead of collapsing into an oval (the Rule 21 render-health
    /// failure). No new art is authored; the one imported PNG is the dashed ball ghost, which is
    /// a Figma node export because a dashed ring cannot be made from a stadium.</para>
    /// </summary>
    public static class PendulumSchemeBuilder
    {
        // ── Figma node values (canvas px; the ShotUI canvas is 1170×2532 at scale 1) ─────
        // All Y offsets are measured from the BALL REST CENTRE, positive = up, because that is
        // the one landmark the node and the live scene agree on. The node draws the ball 187px
        // lower on the frame than CentralBall actually sits, so absolute node Y would be wrong.
        private const float LaneWidth        = 120f;
        private const float TickHeight       = 6f;
        // Seed only — PendulumLaneView.ApplyGeometry DERIVES the real height at Activate from the
        // deepest tick plus the club's lower half, so the pill can never drift from its own lines.
        private const float LaneHeightSwing  = 596f;
        private const float LaneHeightPutt   = 520f;
        // Seed positions only. PendulumLaneView.ApplyGeometry re-places both ticks and both labels
        // at Activate from the LIVE config, as HandleRestBelowBall + PendulumPull*Px — which is
        // what makes a retune of the pull thresholds show up on the drawn lines.
        private const float Tick100Offset    = HandleRestBelowBall + 380f;
        private const float Tick120Offset    = HandleRestBelowBall + 456f;
        private const float LabelGapFromCentre = 76f;   // Label100 x=613 vs frame centre 537
        private const float LabelFontNodePx  = 28f;

        private const float BarAboveBall     = 128f;    // track centre 833 vs ball centre 961
        private const float TrackWidthSwing  = 720f;
        private const float TrackWidthPutt   = 520f;
        private const float TrackHeight      = 44f;
        private const float TrackRadius      = 22f;
        private const float BandHeight       = 36f;
        private const float BandRadius       = 18f;
        private const float PipWidth         = 10f;
        private const float PipHeight        = 60f;
        private const float MarkerSize       = 56f;
        private const float MarkerStroke     = 4f;

        private const float GhostSize        = 100f;
        private const float PopAboveBall     = 289f;    // GradePop centre 672 vs ball centre 961
        private const float PopWidth         = 360f;
        private const float PopHeight        = 142f;
        private const float PopFontNodePx    = 120f;

        /// <summary>Node px → TMP fontSize. The shell canvas is 1:1 in GEOMETRY but TMP renders
        /// ~20% large against a Figma px at this canvas (memory: shell_canvas_font_conversion).</summary>
        private const float FontDivisor      = 1.2f;

        /// <summary>Club-head CENTRE below the ball centre at rest. Centre, not top edge: a tick
        /// marks where the club LANDS, so the club's own reference point has to be the thing that
        /// travels — with a top-edge pivot the sprite hung a full 100px below every line it was
        /// supposed to be sitting on, and the pill had to be absurdly long to contain it.</summary>
        private const float HandleRestBelowBall = 70f;
        private const float ClubHalfHeight      = 50f;   // the 178x100 ClubHandle sprite
        private const float LaneTailPx          = 20f;

        // ── Colours (straight from get_design_context on 14091:33885) ────────────────────
        // NOTE: the lane's white-14% fill / white-50% stroke and the track's navy-78% fill /
        // white-35% stroke are NOT here — they live in make_pendulum_sprites.py, which bakes them
        // into the two sprites. Duplicating them as Colors would be two places to retune.
        private static readonly Color Tick100C    = Hex(0xFFD23A);
        private static readonly Color Tick120C    = Hex(0xFF5A5A);
        // PRE-COMPOSITED AND OPAQUE, not the node's #FFEBA6@75% / #ADEBAD@90%. Figma composites in
        // sRGB and Unity blends in LINEAR, so the node's alphas rendered the amber 28 RGB too
        // light (measured: built (224,208,148) vs reference (196,188,138)). The bands sit on the
        // track — a parent whose colour is known — so pre-compositing them is EXACT rather than
        // fitted, and it is backdrop-independent. These two values ARE the reference's own pixels
        // (reference/pendulum_timing.png at y=1325, x=500 and x=555).
        private static readonly Color BandGoodC   = new Color32(196, 188, 138, 255);
        private static readonly Color BandJustC   = new Color32(175, 230, 170, 255);
        private static readonly Color PipC        = Hex(0xFF3B3B);
        private static readonly Color MarkerCoreC = Color.white;
        private static readonly Color MarkerRingC = Hex(0x001E39);

        private const string PillPath  = "Assets/Art/Tournaments/S_PillStadium.png";
        // The two BORDERED pills. A translucent fill inside a translucent stroke cannot be made
        // from two tinted S_PillStadiums — the solid "stroke" layer paints the whole shape and the
        // fill on top cannot hide it (the lane came out a ~57%-white slab). Baked from the node's
        // own tokens by Docs/Scripts/make_pendulum_sprites.py; edit that script, never the PNG.
        private const string LaneSpritePath  = "Assets/Art/ShotUI/S_PendulumLane.png";
        private const string TrackSpritePath = "Assets/Art/ShotUI/S_PendulumTrack.png";
        /// <summary>Both bordered pills are baked at 2x, so their 9-slice border halves.</summary>
        private const float  BakedPpum = 2f;
        private const string GhostPath = "Assets/Art/ShotUI/S_PendulumBallGhost.png";
        private const string ScenePath = "Assets/Scenes/Physics/LabScaffold.unity";

        [MenuItem("GOLFIN/Build/Pendulum Scheme UI (LabScaffold)")]
        public static void BuildInOpenScene()
        {
            // GameObject.Find skips INACTIVE objects and SchemeRoot_Pendulum ships inactive
            // (ShotSchemeHost only turns on the live scheme's root), so this walks the scene.
            var root = FindInScene("SchemeRoot_Pendulum");
            if (root == null)
            {
                Debug.LogError("[PendulumSchemeBuilder] SchemeRoot_Pendulum not found — open " + ScenePath + " first.");
                return;
            }
            Build(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[PendulumSchemeBuilder] Built under " + root.name + ".");
        }

        public static void Build(GameObject root)
        {
            var pill  = AssetDatabase.LoadAssetAtPath<Sprite>(PillPath);
            var ghost = AssetDatabase.LoadAssetAtPath<Sprite>(GhostPath);
            var laneS = AssetDatabase.LoadAssetAtPath<Sprite>(LaneSpritePath);
            var trackS= AssetDatabase.LoadAssetAtPath<Sprite>(TrackSpritePath);
            if (pill == null) { Debug.LogError("[PendulumSchemeBuilder] missing " + PillPath); return; }
            if (laneS == null || trackS == null)
            {
                Debug.LogError("[PendulumSchemeBuilder] missing a baked pill — run " +
                               "python3 Docs/Scripts/make_pendulum_sprites.py");
                return;
            }

            var rootRt = root.GetComponent<RectTransform>();

            // Idempotent: this builder owns every child of the root, so a rebuild starts clean
            // rather than accumulating a second copy of the bar next to the first.
            for (int i = rootRt.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(rootRt.GetChild(i).gameObject);

            // The placeholder's job is over the moment a real driver exists.
            var placeholder = root.GetComponent<PlaceholderSchemeDriver>();
            if (placeholder != null) Object.DestroyImmediate(placeholder);

            var driver = root.GetComponent<PendulumSchemeDriver>();
            if (driver == null) driver = root.AddComponent<PendulumSchemeDriver>();

            // ── Lane ────────────────────────────────────────────────────────────────
            var laneRoot = MakeRoot(rootRt, "PendulumLaneRoot");
            var laneView = laneRoot.gameObject.AddComponent<PendulumLaneView>();

            // ONE image: the fill and its 3px stroke are both in the baked sprite, untinted
            // (Color.white), so the two translucencies composite the way the node draws them.
            var lane = MakeBaked(laneRoot, "PowerLane", laneS, LaneWidth, LaneHeightSwing);
            lane.pivot = new Vector2(0.5f, 1f);          // TOP edge sits on the ball rest centre
            lane.anchoredPosition = Vector2.zero;
            // The node clips the lane's children, which is what keeps a tick from drawing past
            // the rounded cap when a retune pushes it near the bottom.
            lane.gameObject.AddComponent<RectMask2D>();

            // Stadium at r = h/2 rather than a null-sprite Image: at 6px tall the rounded ends
            // are sub-pixel against the node's square tick, and a sprite-less flat fill is the
            // exact shape the UI-fidelity linter treats as fabricated art.
            var tick100 = MakeStadium(lane, "Tick100", pill, Tick100C, LaneWidth, TickHeight, TickHeight * 0.5f);
            AnchorToTop(tick100, -Tick100Offset);
            var tick120 = MakeStadium(lane, "Tick120", pill, Tick120C, LaneWidth, TickHeight, TickHeight * 0.5f);
            AnchorToTop(tick120, -Tick120Offset);

            var label100 = MakeText(laneRoot, "Label100", "100%", LabelFontNodePx, Color.white, FontStyles.Normal);
            SideLabel(label100, -Tick100Offset);
            var label120 = MakeText(laneRoot, "Label120", "120%", LabelFontNodePx, Tick120C, FontStyles.Normal);
            SideLabel(label120, -Tick120Offset);

            Wire(laneView, ("_lane", lane), ("_tick100", tick100), ("_tick120", tick120));
            // The fields are TextMeshProUGUI, not RectTransform — SerializedProperty silently
            // drops a reference of the wrong type, so the component is what gets wired.
            WireObj(laneView, "_label100", label100.GetComponent<TextMeshProUGUI>());
            WireObj(laneView, "_label120", label120.GetComponent<TextMeshProUGUI>());
            // The lane DERIVES its own height and tick positions from these three plus the config.
            WireFloat(laneView, "_handleRestBelowBall", HandleRestBelowBall);
            WireFloat(laneView, "_clubHalfHeight",      ClubHalfHeight);
            WireFloat(laneView, "_laneTailPx",          LaneTailPx);

            // ── Ball rest ghost ─────────────────────────────────────────────────────
            if (ghost != null)
            {
                var g = MakeImage(laneRoot, "BallRestGhost", ghost, Color.white, GhostSize, GhostSize);
                g.anchoredPosition = Vector2.zero;
                g.GetComponent<Image>().type = Image.Type.Simple;
            }
            else Debug.LogWarning("[PendulumSchemeBuilder] ghost sprite missing at " + GhostPath);

            // ── Bar ─────────────────────────────────────────────────────────────────
            var barRoot = MakeRoot(rootRt, "PendulumBarRoot");
            barRoot.anchoredPosition = new Vector2(0f, BarAboveBall);
            var barView = barRoot.gameObject.AddComponent<PendulumBarView>();

            var track = MakeBaked(barRoot, "PendulumTrack", trackS, TrackWidthSwing, TrackHeight);

            // Bands and marker are SIBLINGS of the track, not children: the track's fill would
            // otherwise draw over them, and the marker has to be able to reach the very ends.
            var bandGood = MakeStadium(barRoot, "BandGood", pill, BandGoodC, 288f, BandHeight, BandRadius);
            var bandJust = MakeStadium(barRoot, "BandJust", pill, BandJustC, 100f, BandHeight, BandRadius);
            var pip      = MakeStadium(barRoot, "CentrePip", pill, PipC, PipWidth, PipHeight, PipWidth * 0.5f);

            var marker = MakeStadium(barRoot, "PendulumMarker", pill, MarkerRingC,
                                     MarkerSize + MarkerStroke * 2f, MarkerSize + MarkerStroke * 2f,
                                     (MarkerSize + MarkerStroke * 2f) * 0.5f);
            var markerCore = MakeStadium(marker, "MarkerCore", pill, MarkerCoreC, MarkerSize, MarkerSize, MarkerSize * 0.5f);
            markerCore.anchoredPosition = Vector2.zero;

            Wire(barView, ("_track", track), ("_bandGood", bandGood), ("_bandJust", bandJust),
                          ("_centrePip", pip), ("_marker", marker));
            WireFloat(barView, "_swingTrackWidth", TrackWidthSwing);
            WireFloat(barView, "_puttTrackWidth",  TrackWidthPutt);

            // ── Grade pop ───────────────────────────────────────────────────────────
            var popRoot = MakeRoot(rootRt, "PendulumGradePop");
            popRoot.anchoredPosition = new Vector2(0f, PopAboveBall);
            popRoot.sizeDelta = new Vector2(PopWidth, PopHeight);
            var pop = popRoot.gameObject.AddComponent<PendulumGradePop>();
            var popText = MakeText(popRoot, "GradeText", "JUST!", PopFontNodePx, Hex(0xADEBAD), FontStyles.Bold);
            Stretch(popText, 0f);
            popText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            WireObj(pop, "_label", popText.GetComponent<TextMeshProUGUI>());
            WireObj(pop, "_group", popRoot.GetComponent<CanvasGroup>());

            // ── Handle — a copy of the flick's ClubHandle ───────────────────────────
            var source = FindInScene("ClubHandle");
            RectTransform handle;
            if (source != null)
            {
                var copy = Object.Instantiate(source, rootRt);
                copy.name = "PendulumHandle";
                // The flick's own behaviours come off: this handle is driven by the Pendulum
                // driver, and a second ClubHandleDragger would open a second external drag.
                StripAll<ClubHandleDragger>(copy);
                StripAll<TeeIdleGlowController>(copy);
                handle = copy.GetComponent<RectTransform>();
                var img = copy.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;
                copy.SetActive(true);
            }
            else
            {
                Debug.LogWarning("[PendulumSchemeBuilder] ClubHandle not found — building a bare handle. " +
                                 "The sprite binder will still paint it, but check the clone provenance.");
                handle = MakeSolid(rootRt, "PendulumHandle", Color.white, 178f, 100f);
                handle.gameObject.AddComponent<ClubHandleSpriteBinder>();
            }
            handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 0.5f);
            handle.pivot = new Vector2(0.5f, 0.5f);   // centre — see HandleRestBelowBall
            handle.sizeDelta = new Vector2(178f, 100f);
            handle.anchoredPosition = new Vector2(0f, -HandleRestBelowBall);
            handle.localScale = Vector3.one;
            handle.SetAsLastSibling();     // the club head reads on top of its own lane

            Wire(driver, ("_schemeRoot", rootRt), ("_handle", handle));
            WireObj(driver, "_laneView", laneView);
            WireObj(driver, "_barView",  barView);
            WireObj(driver, "_gradePop", pop);

            // Author the overlays INVISIBLE: the scheme root can be switched on at Idle, and a
            // bar sitting at full alpha over a shot nobody has started reads as a bug.
            laneRoot.GetComponent<CanvasGroup>().alpha = 0f;
            barRoot.GetComponent<CanvasGroup>().alpha  = 0f;
            popRoot.GetComponent<CanvasGroup>().alpha  = 0f;

            EditorUtility.SetDirty(root);
        }

        // ── Builders ────────────────────────────────────────────────────────────────

        /// <summary>Find by name INCLUDING inactive objects — <c>GameObject.Find</c> cannot.</summary>
        private static GameObject FindInScene(string name)
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }

        private static void StripAll<T>(GameObject go) where T : Component
        {
            foreach (var c in go.GetComponentsInChildren<T>(true)) Object.DestroyImmediate(c);
        }

        private static RectTransform MakeRoot(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);   // = the ball rest centre
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            go.GetComponent<CanvasGroup>().blocksRaycasts = false;   // chrome, never a touch target
            return rt;
        }

        private static RectTransform MakeImage(RectTransform parent, string name, Sprite sprite,
                                               Color color, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color  = color;
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>
        /// A rounded rect of the given RADIUS, from the one shared stadium sprite.
        /// <c>pixelsPerUnitMultiplier = spriteBorder / radius</c> is the whole trick: without it
        /// the 88px corner renders at 88 UI px and a 44-tall track comes out as an oval blob
        /// (UI_FIDELITY Rule 21's 9-slice-collapse check).
        /// </summary>
        private static RectTransform MakeStadium(RectTransform parent, string name, Sprite pill,
                                                 Color color, float w, float h, float radius)
        {
            var rt  = MakeImage(parent, name, pill, color, w, h);
            var img = rt.GetComponent<Image>();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Mathf.Max(pill.border.x / Mathf.Max(radius, 0.5f), 0.01f);
            return rt;
        }

        /// <summary>
        /// One of the two baked bordered pills, 9-sliced at its authored 2x border. Untinted:
        /// the node's fill AND stroke alphas are already in the PNG, and tinting would multiply
        /// them both.
        /// </summary>
        private static RectTransform MakeBaked(RectTransform parent, string name, Sprite sprite,
                                               float w, float h)
        {
            var rt  = MakeImage(parent, name, sprite, Color.white, w, h);
            var img = rt.GetComponent<Image>();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = BakedPpum;
            return rt;
        }

        private static RectTransform MakeSolid(RectTransform parent, string name, Color color, float w, float h)
        {
            var rt = MakeImage(parent, name, null, color, w, h);
            return rt;
        }

        private static RectTransform MakeText(RectTransform parent, string name, string preview,
                                              float nodePx, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font          = FindRubik(style);
            tmp.fontSize      = nodePx / FontDivisor;
            tmp.color         = color;
            tmp.fontStyle     = style;
            tmp.text          = preview;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.alignment     = TextAlignmentOptions.Left;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200f, 40f);
            return rt;
        }

        /// <summary>The two Rubik SDF assets the shell canvas already uses — never a new import.</summary>
        private static TMP_FontAsset FindRubik(FontStyles style)
        {
            string want = style == FontStyles.Bold ? "Rubik-SemiBold SDF" : "Rubik-VariableFont_wght SDF";
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (f != null && f.name == want) return f;
            }
            Debug.LogWarning("[PendulumSchemeBuilder] font '" + want + "' not found — TMP default used.");
            return null;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2( inset,  inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static void AnchorToTop(RectTransform rt, float y)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        /// <summary>A tick label: outside the lane's right edge, vertically ON the tick.</summary>
        private static void SideLabel(RectTransform rt, float y)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(120f, 40f);
            rt.anchoredPosition = new Vector2(LabelGapFromCentre, y);
        }

        // ── Wiring (SerializedObject — never a hand drag) ───────────────────────────

        private static void Wire(Object target, params (string field, Object value)[] pairs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in pairs)
            {
                var p = so.FindProperty(field);
                if (p == null) { Debug.LogError($"[PendulumSchemeBuilder] no field '{field}' on {target.GetType().Name}"); continue; }
                p.objectReferenceValue = value;
                // A reference of the wrong type is dropped SILENTLY by SerializedProperty, which
                // is exactly how a [SerializeField] ends up null after a "successful" build.
                if (value != null && p.objectReferenceValue == null)
                    Debug.LogError($"[PendulumSchemeBuilder] '{field}' on {target.GetType().Name} rejected " +
                                   $"{value.GetType().Name} '{value.name}' — wrong type for that field.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireObj(Object target, string field, Object value)
            => Wire(target, (field, value));

        private static void WireFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var p  = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[PendulumSchemeBuilder] no field '{field}' on {target.GetType().Name}"); return; }
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color Hex(int rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
