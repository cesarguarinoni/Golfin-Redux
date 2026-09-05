using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Golfin.Gameplay.UI.Controls;
using Golfin.Gameplay.UI.Controls.Needle;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.EditorTools.ShotUI
{
    /// <summary>
    /// Builds (or REBUILDS) the Needle / "Tap Timing" scheme's uGUI under
    /// <c>SchemeRoot_Needle</c> from the Figma node values — scheme_needle §3.3, Figma section
    /// "2b — Needle (club handle)" 14091:102411.
    ///
    /// <para>A SCRIPT AND NOT HAND-AUTHORING, for the reason every builder in this project exists:
    /// the geometry is a table of numbers read off a node, and a table is something you re-run when
    /// the node moves, not something you re-drag. It is idempotent — it deletes the children it
    /// owns and rebuilds them — so a fidelity fix is a one-line edit here plus a re-run, and the
    /// reviewer diffs the source rather than the scene YAML.</para>
    ///
    /// <para>ALMOST NOTHING HERE IS A SPRITE. Every curved element is a
    /// <see cref="NeedleArcGraphic"/> mesh, because its radius is derived from the pull thresholds
    /// and its angle from the accuracy windows — both move at runtime, so neither can be a
    /// fixed-size PNG. The flat rounded shapes (needle bar, hub, tap pip) are <c>S_PillStadium</c>
    /// tinted, with <c>pixelsPerUnitMultiplier = 88 / radius</c> so the caps come out at the node's
    /// radius instead of collapsing into an oval. Exactly one new PNG is baked
    /// (<c>make_needle_sprites.py</c> → the result chip, a vertical gradient inside a translucent
    /// border, which no tinted stadium can draw), and the ball ghost is the Pendulum's.</para>
    ///
    /// <para>EVERY POSITION IS MEASURED FROM THE BALL REST CENTRE, positive = up. That is the one
    /// landmark the node and the live scene agree on: the node draws the ball at local (537, 961)
    /// inside <c>Shoot Controls</c>, and <c>SchemeRoot_Needle</c> is a full-canvas stretched rect
    /// whose centre is where <c>CentralBall</c> sits, so a child at anchoredPosition zero IS the
    /// ball. Absolute node Y would be wrong by 187px.</para>
    /// </summary>
    public static class NeedleSchemeBuilder
    {
        // ── Figma node values (canvas px; the ShotUI canvas is 1170×2532 at scale 1) ─────

        /// <summary>Club-head CENTRE below the ball centre at rest. The same 70 the Pendulum uses,
        /// because it is the same <c>ClubHandle</c> clone at the same size — and because the power
        /// rings are drawn at this PLUS the pull thresholds, i.e. where the club head lands.</summary>
        private const float HandleRestBelowBall = 70f;
        private const float ClubHalfHeight      = 50f;   // the 178x100 ClubHandle sprite

        // Ring strokes and the crescent's angular width: the node fixes these (Ring80 r=238.5
        // stroke 3, Ring100 r=298 stroke 4, Ring120 r=358.5 stroke 3; the crescent spans the
        // bottom 34.38 deg each side). The RADII are not here — NeedlePowerCircleView derives them.
        private const float Stroke80  = 3f;
        private const float Stroke100 = 4f;
        private const float Stroke120 = 3f;
        private const float CrescentHalfAngleDeg = 34.38f;
        private const float Label100OffsetX = 120f;      // Label100 x=657 vs ball centre 537
        private const float LabelFontNodePx = 28f;

        // AccuracyArc 460x460 (outer r 230), band r 186..230; the zones are r 190..230, i.e. flush
        // outside and 4px short inside, so the navy reads as a lip. Putt frame: 460x300.
        private const float ArcRadius      = 230f;
        private const float ArcRadiusYPutt = 150f;
        private const float ArcThickness   = 44f;
        private const float ZoneThickness  = 40f;
        private const float ArcStrokePx    = 2f;         // node stroke 4, masked inside = 2 visible

        private const float NeedleWidth    = 10f;
        private const float NeedleRadius   = 5f;
        private const float NeedleOverhang = 10f;        // 240px needle on a 230px arc
        private const float HubOuter       = 36f;        // white r16 + 4px #001E39 ring
        private const float HubRing        = 4f;
        private const float PipOuter       = 28f;
        private const float PipRing        = 4f;
        private const float TapHintFontPx  = 44f;
        private const float TapHintBelowBall = 90f;      // TapHint top 1051 vs ball centre 961

        private const float GhostSize      = 100f;

        // ResultChip 420x120 centred at (537, 601) = 360px above the ball; text Rubik Bold 64.
        private const float ChipAboveBall  = 360f;
        private const float ChipWidth      = 420f;
        private const float ChipHeight     = 120f;
        private const float ChipShadowPad  = 24f;        // baked into the sprite by the baker
        private const float ChipFontNodePx = 64f;

        /// <summary>The node's <c>Shoot Controls</c> frame, which is the tap area: 1074x1396 at
        /// Content-Container y 334..1730, i.e. 961px above the ball down to 435px below it.</summary>
        private const float TapAreaWidth  = 1074f;
        private const float TapAreaHeight = 1396f;
        private const float TapAreaTopAboveBall    = 961f;
        private const float TapAreaBottomBelowBall = 435f;

        /// <summary>Node px → TMP fontSize. The shell canvas is 1:1 in GEOMETRY but TMP renders
        /// ~20% large against a Figma px at this canvas (memory: shell_canvas_font_conversion).</summary>
        private const float FontDivisor = 1.2f;

        /// <summary>Bounding rect for every arc graphic. Not geometry — the mesh is built in local
        /// space — just a rect large enough that nothing can ever clip the deepest ring.</summary>
        private const float ArcGraphicBoundsPx = 1200f;

        // ── Colours ─────────────────────────────────────────────────────────────────────
        // The ring / crescent / arc / zone colours are NOT here: they live in NeedleColors, which
        // derives each from the node's own token plus its alpha. Duplicating them as Color literals
        // would be two places to retune, and the linear-space correction would be in only one.
        private static readonly Color Label100C = Hex(0xFFD23A);
        private static readonly Color HubRingC  = Hex(0x001E39);
        private static readonly Color ChipTextC = Hex(0x4DA3FF);

        private const string PillPath  = "Assets/Art/Tournaments/S_PillStadium.png";
        private const string GhostPath = "Assets/Art/ShotUI/S_PendulumBallGhost.png";
        private const string ChipPath  = "Assets/Art/ShotUI/S_NeedleResultChip.png";
        private const string ScenePath = "Assets/Scenes/Physics/LabScaffold.unity";

        [MenuItem("GOLFIN/Build/Needle Scheme UI (LabScaffold)")]
        public static void BuildInOpenScene()
        {
            // GameObject.Find skips INACTIVE objects and SchemeRoot_Needle ships inactive
            // (ShotSchemeHost only turns on the live scheme's root), so this walks the scene.
            var root = FindInScene("SchemeRoot_Needle");
            if (root == null)
            {
                Debug.LogError("[NeedleSchemeBuilder] SchemeRoot_Needle not found — open " + ScenePath + " first.");
                return;
            }
            Build(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[NeedleSchemeBuilder] Built under " + root.name + ".");
        }

        /// <summary>
        /// NAMES ARE PREFIXED WHERE THE PENDULUM ALREADY OWNS ONE. The verification bot (and every
        /// diagnostic in this project) finds objects by NAME across the whole scene INCLUDING
        /// inactive ones, and both scheme roots are inactive most of the time — so a shared
        /// "GradeText" / "Label100" / "BallRestGhost" resolves to whichever the walk reaches first.
        /// The first acceptance run asserted this scheme's grade pop against the PENDULUM's, and
        /// read back "JUST!". A colliding name does not fail loudly; it passes quietly.
        /// </summary>
        public static void Build(GameObject root)
        {
            var pill  = AssetDatabase.LoadAssetAtPath<Sprite>(PillPath);
            var ghost = AssetDatabase.LoadAssetAtPath<Sprite>(GhostPath);
            var chip  = AssetDatabase.LoadAssetAtPath<Sprite>(ChipPath);
            if (pill == null) { Debug.LogError("[NeedleSchemeBuilder] missing " + PillPath); return; }
            if (chip == null)
            {
                Debug.LogError("[NeedleSchemeBuilder] missing " + ChipPath +
                               " — run python3 Docs/Scripts/make_needle_sprites.py");
                return;
            }

            var rootRt = root.GetComponent<RectTransform>();

            // Idempotent: this builder owns every child of the root, so a rebuild starts clean
            // rather than accumulating a second arc next to the first.
            for (int i = rootRt.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(rootRt.GetChild(i).gameObject);

            // The placeholder's job is over the moment a real driver exists.
            var placeholder = root.GetComponent<PlaceholderSchemeDriver>();
            if (placeholder != null) Object.DestroyImmediate(placeholder);

            var driver = root.GetComponent<NeedleSchemeDriver>();
            if (driver == null) driver = root.AddComponent<NeedleSchemeDriver>();

            // ── Power circle ────────────────────────────────────────────────────────
            var circleRoot = MakeRoot(rootRt, "NeedleCircleRoot");
            var circleView = circleRoot.gameObject.AddComponent<NeedlePowerCircleView>();

            // The dim group is a SECOND CanvasGroup under the fading one: the base view owns
            // "visible while swinging", this one owns "faded back once the power is committed",
            // and the two multiply. Everything the dim applies to hangs off it.
            var dim = MakeRoot(circleRoot, "CircleDim");

            var ring80    = MakeArc(dim, "Ring80");
            var ring100   = MakeArc(dim, "Ring100");
            var ring120   = MakeArc(dim, "Ring120");
            var crescent  = MakeArc(dim, "OverpowerCrescent");
            // Under the rings: the crescent is a filled band and the gold ring reads on top of it.
            crescent.transform.SetAsFirstSibling();

            var label100 = MakeText(dim, "NeedleLabel100", "100%", LabelFontNodePx, Label100C, FontStyles.Normal);
            label100.sizeDelta = new Vector2(160f, 40f);
            label100.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;

            RectTransform ghostRt = null;
            if (ghost != null)
            {
                ghostRt = MakeImage(dim, "NeedleBallRestGhost", ghost, Color.white, GhostSize, GhostSize);
                ghostRt.GetComponent<Image>().type = Image.Type.Simple;
            }
            else Debug.LogWarning("[NeedleSchemeBuilder] ghost sprite missing at " + GhostPath);

            Wire(circleView, ("_ring80", ring80), ("_ring100", ring100), ("_ring120", ring120),
                             ("_crescent", crescent));
            WireObj(circleView, "_label100",  label100.GetComponent<TextMeshProUGUI>());
            WireObj(circleView, "_ballGhost", ghostRt);
            WireObj(circleView, "_dimGroup",  dim.GetComponent<CanvasGroup>());
            WireFloat(circleView, "_handleRestBelowBall",  HandleRestBelowBall);
            WireFloat(circleView, "_clubHalfHeight",       ClubHalfHeight);
            WireFloat(circleView, "_stroke80",             Stroke80);
            WireFloat(circleView, "_stroke100",            Stroke100);
            WireFloat(circleView, "_stroke120",            Stroke120);
            WireFloat(circleView, "_crescentHalfAngleDeg", CrescentHalfAngleDeg);
            WireFloat(circleView, "_label100OffsetX",      Label100OffsetX);

            // ── Accuracy arc ────────────────────────────────────────────────────────
            var arcRoot = MakeRoot(rootRt, "NeedleArcRoot");
            var arcView = arcRoot.gameObject.AddComponent<NeedleArcView>();

            var arc         = MakeArc(arcRoot, "AccuracyArc");
            var strokeOuter = MakeArc(arcRoot, "AccuracyArcStrokeOuter");
            var strokeInner = MakeArc(arcRoot, "AccuracyArcStrokeInner");
            var zoneGood    = MakeArc(arcRoot, "ZoneGood");
            var zonePerfect = MakeArc(arcRoot, "ZonePerfect");

            // The needle pivots at the BALL, so its rect's pivot is its own bottom edge and its
            // anchored position is zero — the rotation the view sets is then literally the angle
            // the player reads off the arc.
            var needle = MakeStadium(arcRoot, "Needle", pill, Color.white,
                                     NeedleWidth, ArcRadius + NeedleOverhang, NeedleRadius);
            needle.pivot = new Vector2(0.5f, 0f);
            needle.anchoredPosition = Vector2.zero;

            var hub     = MakeDisc(arcRoot, "NeedleHub", pill, HubOuter, HubRing, HubRingC, Color.white);
            var tapPip  = MakeDisc(arcRoot, "TapPip",    pill, PipOuter, PipRing, HubRingC, Color.white);

            // The word is a LAYOUT PLACEHOLDER only. NeedleArcView.ShowTapHint resolves
            // SHOT_TAP_HINT at show time — authoring the real string here is how a
            // hardcoded literal ships, and the UI fidelity linter flags exactly that.
            var tapHint = MakeText(arcRoot, "TapHint", "(SHOT_TAP_HINT)", TapHintFontPx, Color.white, FontStyles.Normal);
            tapHint.sizeDelta = new Vector2(400f, 60f);
            var tapHintTmp = tapHint.GetComponent<TextMeshProUGUI>();
            tapHintTmp.alignment = TextAlignmentOptions.Center;
            // The node's text-shadow 0 2 6 rgba(0,30,57,.9), as TMP's own underlay — not an
            // Outline component, which Rule 21 reads as a fabricated border.
            ApplyTextShadow(tapHintTmp, HubRingC);

            Wire(arcView, ("_arc", arc), ("_arcStrokeOuter", strokeOuter), ("_arcStrokeInner", strokeInner),
                          ("_zoneGood", zoneGood), ("_zonePerfect", zonePerfect),
                          ("_needle", needle), ("_hub", hub), ("_tapPip", tapPip));
            WireObj(arcView, "_needleBar", needle.GetComponent<Image>());
            WireObj(arcView, "_tapHint",   tapHintTmp);
            WireFloat(arcView, "_swingRadius",      ArcRadius);
            WireFloat(arcView, "_puttRadiusY",      ArcRadiusYPutt);
            WireFloat(arcView, "_arcThickness",     ArcThickness);
            WireFloat(arcView, "_zoneThickness",    ZoneThickness);
            WireFloat(arcView, "_arcStrokePx",      ArcStrokePx);
            WireFloat(arcView, "_needleOverhang",   NeedleOverhang);
            WireFloat(arcView, "_needleWidth",      NeedleWidth);
            WireFloat(arcView, "_tapHintBelowBall", TapHintBelowBall);

            // ── Result chip ─────────────────────────────────────────────────────────
            var popRoot = MakeRoot(rootRt, "NeedleGradePop");
            popRoot.anchoredPosition = new Vector2(0f, ChipAboveBall);
            popRoot.sizeDelta = new Vector2(ChipWidth, ChipHeight);
            var pop = popRoot.gameObject.AddComponent<SchemeGradePop>();

            // The sprite carries its own blurred drop shadow in transparent padding, so the IMAGE
            // is the body plus that padding; the pop root stays the node's 420x120 body.
            var chipImg = MakeImage(popRoot, "ChipBg", chip, Color.white,
                                    ChipWidth + ChipShadowPad * 2f, ChipHeight + ChipShadowPad * 2f);
            chipImg.GetComponent<Image>().type = Image.Type.Simple;

            var popText = MakeText(popRoot, "NeedleGradeText", "PERFECT", ChipFontNodePx, ChipTextC, FontStyles.Bold);
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
                copy.name = "NeedleHandle";
                // The flick's own behaviours come off: this handle is driven by the Needle driver,
                // and a second ClubHandleDragger would open a second external drag.
                StripAll<ClubHandleDragger>(copy);
                StripAll<TeeIdleGlowController>(copy);
                handle = copy.GetComponent<RectTransform>();
                var img = copy.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;
                copy.SetActive(true);
            }
            else
            {
                Debug.LogWarning("[NeedleSchemeBuilder] ClubHandle not found — building a bare handle. " +
                                 "The sprite binder will still paint it, but check the clone provenance.");
                handle = MakeImage(rootRt, "NeedleHandle", null, Color.white, 178f, 100f);
                handle.gameObject.AddComponent<ClubHandleSpriteBinder>();
            }
            handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 0.5f);
            handle.pivot = new Vector2(0.5f, 0.5f);   // centre — see HandleRestBelowBall
            handle.sizeDelta = new Vector2(178f, 100f);
            handle.anchoredPosition = new Vector2(0f, -HandleRestBelowBall);
            handle.localScale = Vector3.one;
            handle.SetAsLastSibling();     // the club head reads on top of its own circle

            // ...but the hint reads on top of the club, as the node's Timing frame draws it. It has
            // to leave the arc root to do that, which costs it the arc's fade — acceptable, since
            // the prompt is a hard on/off state and the driver toggles it directly.
            tapHint.SetParent(rootRt, worldPositionStays: false);
            tapHint.SetAsLastSibling();
            tapHint.anchoredPosition = new Vector2(0f, -(TapHintBelowBall + tapHint.rect.height * 0.5f));
            tapHint.gameObject.SetActive(false);     // shown by the driver for the needle phase only

            // ── Tap catcher ─────────────────────────────────────────────────────────
            // The node's Shoot Controls frame, not the whole canvas: it deliberately stops short of
            // the Spin / Fade-Draw / club buttons below and the HUD above, so a catcher that
            // somehow outlived its phase still could not swallow those taps.
            //
            // LAST SIBLING, ABOVE THE CLUB HEAD. The handle is a raycast target whose events bubble
            // to this same root's IPointerDownHandler, so a catcher underneath it would leave a
            // 178x100 dead spot over the ball — dead centre of the tap area, and exactly where a
            // thumb that just released the club already is. It is SetActive(false) between swings,
            // so it is outside the raycast entirely except during the needle phase.
            var catcherRt = MakeImage(rootRt, "NeedleTapCatcher", null, new Color(0f, 0f, 0f, 0f),
                                      TapAreaWidth, TapAreaHeight);
            catcherRt.anchoredPosition =
                new Vector2(0f, (TapAreaTopAboveBall - TapAreaBottomBelowBall) * 0.5f);
            catcherRt.SetAsLastSibling();
            var catcherImg = catcherRt.GetComponent<Image>();
            catcherImg.raycastTarget = true;      // the whole point; alpha 0, never a visible fill
            var catcher = catcherRt.gameObject.AddComponent<NeedleTapCatcher>();
            WireObj(catcher, "_raycastTarget", catcherImg);
            catcherRt.gameObject.SetActive(false);   // armed by the driver at the release

            Wire(driver, ("_schemeRoot", rootRt), ("_handle", handle));
            WireObj(driver, "_circleView", circleView);
            WireObj(driver, "_arcView",    arcView);
            WireObj(driver, "_tapCatcher", catcher);
            WireObj(driver, "_gradePop",   pop);

            // Author the overlays INVISIBLE: the scheme root can be switched on at Idle, and an
            // arc sitting at full alpha over a shot nobody has started reads as a bug.
            circleRoot.GetComponent<CanvasGroup>().alpha = 0f;
            arcRoot.GetComponent<CanvasGroup>().alpha    = 0f;
            popRoot.GetComponent<CanvasGroup>().alpha    = 0f;

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

        /// <summary>A <see cref="NeedleArcGraphic"/> at the ball centre. Its own fields are set by
        /// the view at Activate — a builder that authored radii here would be the second place
        /// they live, and the derived one would silently lose.</summary>
        private static NeedleArcGraphic MakeArc(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            // The mesh is drawn in local space about the pivot, so the rect is only a bound. Big
            // enough for the deepest ring (2 x 526px) with room to spare, so a future mask or a
            // culling pass can never clip a ring that a retune has grown.
            rt.sizeDelta = new Vector2(ArcGraphicBoundsPx, ArcGraphicBoundsPx);
            rt.anchoredPosition = Vector2.zero;
            var g = go.AddComponent<NeedleArcGraphic>();
            g.raycastTarget = false;
            return g;
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
        /// A rounded rect of the given RADIUS from the one shared stadium sprite.
        /// <c>pixelsPerUnitMultiplier = spriteBorder / radius</c> is the whole trick: without it
        /// the 88px corner renders at 88 UI px and a 10-wide needle comes out an oval blob
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

        /// <summary>A ringed disc — the hub and the tap pip: an outer stadium in the ring colour
        /// with an inner one inset by the ring width, which is how a 4px stroke is drawn without an
        /// <c>Outline</c> component.</summary>
        private static RectTransform MakeDisc(RectTransform parent, string name, Sprite pill,
                                              float outer, float ring, Color ringColor, Color coreColor)
        {
            var rt   = MakeStadium(parent, name, pill, ringColor, outer, outer, outer * 0.5f);
            float ci = outer - ring * 2f;
            var core = MakeStadium(rt, name + "Core", pill, coreColor, ci, ci, ci * 0.5f);
            core.anchoredPosition = Vector2.zero;
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

        /// <summary>The node's text-shadow, as TMP's own underlay on a per-object material.</summary>
        private static void ApplyTextShadow(TextMeshProUGUI tmp, Color shadow)
        {
            if (tmp == null || tmp.fontSharedMaterial == null) return;
            var mat = new Material(tmp.fontSharedMaterial);
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(shadow.r, shadow.g, shadow.b, 0.9f));
            mat.SetFloat("_UnderlayOffsetX", 0f);
            mat.SetFloat("_UnderlayOffsetY", -0.35f);
            mat.SetFloat("_UnderlaySoftness", 0.25f);
            tmp.fontMaterial = mat;
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
            Debug.LogWarning("[NeedleSchemeBuilder] font '" + want + "' not found — TMP default used.");
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

        // ── Wiring (SerializedObject — never a hand drag) ───────────────────────────

        private static void Wire(Object target, params (string field, Object value)[] pairs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in pairs)
            {
                var p = so.FindProperty(field);
                if (p == null) { Debug.LogError($"[NeedleSchemeBuilder] no field '{field}' on {target.GetType().Name}"); continue; }
                p.objectReferenceValue = value;
                // A reference of the wrong type is dropped SILENTLY by SerializedProperty, which
                // is exactly how a [SerializeField] ends up null after a "successful" build.
                if (value != null && p.objectReferenceValue == null)
                    Debug.LogError($"[NeedleSchemeBuilder] '{field}' on {target.GetType().Name} rejected " +
                                   $"{value.GetType().Name} '{value.name}' — wrong type for that field.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireObj(Object target, string field, Object value)
            => Wire(target, new[] { (field, value) });

        private static void WireFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var p  = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[NeedleSchemeBuilder] no field '{field}' on {target.GetType().Name}"); return; }
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color Hex(int rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
