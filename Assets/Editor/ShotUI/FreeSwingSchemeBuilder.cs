using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Golfin.Gameplay.UI.Controls;
using Golfin.Gameplay.UI.Controls.FreeSwing;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.EditorTools.ShotUI
{
    /// <summary>
    /// Builds (or REBUILDS) the Free Swing scheme's uGUI under <c>SchemeRoot_FreeSwing</c> from
    /// the Figma node values — scheme_freeswing §3.3, Figma section "3b — Free Swing (club
    /// handle)" 14091:102934.
    ///
    /// <para>A SCRIPT AND NOT HAND-AUTHORING, for the reason every builder in this project exists:
    /// the geometry is a table of numbers read off a node, and a table is something you re-run
    /// when the node moves, not something you re-drag. It is idempotent — it deletes the children
    /// it owns and rebuilds them — so a fidelity fix is a one-line edit here plus a re-run, and
    /// the reviewer diffs the source rather than the scene YAML.</para>
    ///
    /// <para>EVERY POSITION IS MEASURED FROM THE BALL REST CENTRE, positive = up. That is the one
    /// landmark the node and the live scene agree on: the node draws the ball at local (537, 961)
    /// inside <c>Shoot Controls</c>, and <c>SchemeRoot_FreeSwing</c> is a full-canvas stretched
    /// rect whose centre is where <c>CentralBall</c> sits, so a child at anchoredPosition zero IS
    /// the ball. Absolute node Y would be wrong by 187px.</para>
    ///
    /// <para>THE PILL'S LENGTH IS NOT THE NODE'S 560. It is DERIVED, in
    /// <see cref="FreeSwingLaneView.ApplyGeometry"/>, from the follow-through plus the deepest
    /// tick plus the club head's lower half — the lengthened-pill fix Cesar asked for on the
    /// Pendulum and asked to see imitated here. The node's 560 is one sample of that formula at
    /// the OLD 300px pull thresholds; authoring it would let a CSV retune move the shot without
    /// moving the line the player is aiming at.</para>
    ///
    /// <para>NAMES ARE ALL <c>FreeSwing*</c>. The verification bot (and every diagnostic in this
    /// project) finds objects by NAME across the whole scene INCLUDING inactive ones, and all four
    /// scheme roots are inactive most of the time — so a shared "Tick100" / "BallRestGhost" /
    /// "GradeText" resolves to whichever the walk reaches first. The Needle's first acceptance run
    /// asserted its grade pop against the PENDULUM's and read back "JUST!". A colliding name does
    /// not fail loudly; it passes quietly.</para>
    /// </summary>
    public static class FreeSwingSchemeBuilder
    {
        // ── Figma node values (canvas px; the ShotUI canvas is 1170×2532 at scale 1) ─────

        /// <summary>SwingLane is 140 wide with <c>rounded-[70px]</c> — a true stadium.</summary>
        private const float LaneWidth  = 140f;
        private const float LaneRadius = 70f;

        private const float TickHeight         = 6f;    // Tick100/Tick120/ImpactLine are all 140x6
        private const float ImpactWindowHeight = 16f;   // ImpactWindow 92x16, rounded-[8px]
        private const float ImpactWindowRadius = 8f;

        /// <summary>Club-head CENTRE below the ball centre at rest. The same 70 Pendulum and
        /// Needle use, because it is the same <c>ClubHandle</c> clone at the same size — and
        /// because the ticks are drawn at this PLUS the pull thresholds, i.e. where the club head
        /// lands. It is ALSO the crossing offset: see
        /// <see cref="FreeSwingLaneView.ImpactCrossOffsetPx"/>.</summary>
        private const float HandleRestBelowBall = 70f;
        private const float ClubHalfHeight      = 50f;   // the 178x100 ClubHandle sprite
        private const float LaneTailPx          = 20f;
        /// <summary>Lane above the ball on a PUTT. The node halves the follow-through as well as
        /// the depth: SwingLane top 861 on the Putt frame against 801 on the others, ball at 961.</summary>
        private const float PuttFollowThroughPx = 100f;

        /// <summary>Tick labels sit outside the lane's right edge: node x 623 against a ball
        /// centre of 537, i.e. 16px clear of the 70px half-width.</summary>
        private const float LabelGapFromCentre = 86f;
        private const float LabelFontNodePx    = 28f;

        private const float GhostSize = 100f;

        // AnalyzerChip 840x150 at local (117, 521) => centre (537, 596), i.e. 365px above the ball.
        // Its four columns are evenly spaced 200px apart starting at 110 from the chip's left edge,
        // which is ±110 / ±310 from its centre. Labels centre 40px down the 150px body, values 85.
        private const float ChipAboveBall    = 365f;
        private const float ChipWidth        = 840f;
        private const float ChipHeight       = 150f;
        private const float ChipShadowPad    = 24f;     // baked into the sprite by the baker
        private const float ChipColInner     = 110f;
        private const float ChipColOuter     = 310f;
        private const float ChipLabelY       =  35f;    // above the chip centre (75 - 40)
        private const float ChipValueY       = -10f;    // below it (75 - 85)
        private const float ChipLabelFontPx  =  24f;
        private const float ChipValueFontPx  =  32f;
        private const float ChipCellWidth    = 190f;
        private const float ChipCellHeight   =  44f;

        /// <summary>The grade pop clears the analyzer chip's top edge (365 + 75) plus its own half
        /// height plus a gap. The node draws no pop in this section — it is the shared
        /// <c>SchemeGradePop</c>, sized as the Pendulum's — so its PLACE is derived from the one
        /// thing it must not collide with rather than copied from a frame.</summary>
        private const float PopAboveBall  = 540f;
        private const float PopWidth      = 360f;
        private const float PopHeight     = 142f;
        private const float PopFontNodePx = 120f;

        /// <summary>Node px → TMP fontSize. The shell canvas is 1:1 in GEOMETRY but TMP renders
        /// ~20% large against a Figma px at this canvas (memory: shell_canvas_font_conversion).</summary>
        private const float FontDivisor = 1.2f;

        /// <summary>Bounding rect for the trace graphic. Not geometry — the mesh is built in local
        /// space about the pivot — just a rect large enough that nothing can clip a gesture that
        /// ran the length of the screen.</summary>
        private const float TraceBoundsPx = 2600f;

        /// <summary>Node <c>stroke-width 8</c>, <c>stroke-linecap/linejoin round</c>.</summary>
        private const float TraceWidth = 8f;

        // ── Colours ─────────────────────────────────────────────────────────────────────
        // Nothing translucent is a literal here: those live in FreeSwingColors (linear-corrected)
        // or are baked into the two PNGs. Duplicating them would be two places to retune, and the
        // linear correction would be in only one of them.

        private const string PillPath  = "Assets/Art/Tournaments/S_PillStadium.png";
        private const string LanePath  = "Assets/Art/ShotUI/S_FreeSwingLane.png";
        private const string ChipPath  = "Assets/Art/ShotUI/S_FreeSwingAnalyzerChip.png";
        private const string GhostPath = "Assets/Art/ShotUI/S_PendulumBallGhost.png";
        /// <summary>Both baked sprites are authored at 2x, so their 9-slice border halves.</summary>
        private const float  BakedPpum = 2f;
        private const string ScenePath = "Assets/Scenes/Physics/LabScaffold.unity";

        [MenuItem("GOLFIN/Build/Free Swing Scheme UI (LabScaffold)")]
        public static void BuildInOpenScene()
        {
            // GameObject.Find skips INACTIVE objects and SchemeRoot_FreeSwing ships inactive
            // (ShotSchemeHost only turns on the live scheme's root), so this walks the scene.
            var root = FindInScene("SchemeRoot_FreeSwing");
            if (root == null)
            {
                Debug.LogError("[FreeSwingSchemeBuilder] SchemeRoot_FreeSwing not found — open " +
                               ScenePath + " first.");
                return;
            }
            Build(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[FreeSwingSchemeBuilder] Built under " + root.name + ".");
        }

        public static void Build(GameObject root)
        {
            var pill  = AssetDatabase.LoadAssetAtPath<Sprite>(PillPath);
            var laneS = AssetDatabase.LoadAssetAtPath<Sprite>(LanePath);
            var chipS = AssetDatabase.LoadAssetAtPath<Sprite>(ChipPath);
            var ghost = AssetDatabase.LoadAssetAtPath<Sprite>(GhostPath);
            if (pill == null) { Debug.LogError("[FreeSwingSchemeBuilder] missing " + PillPath); return; }
            if (laneS == null || chipS == null)
            {
                Debug.LogError("[FreeSwingSchemeBuilder] missing a baked sprite — run " +
                               "python3 Docs/Scripts/make_freeswing_sprites.py");
                return;
            }

            var rootRt = root.GetComponent<RectTransform>();

            // Idempotent: this builder owns every child of the root, so a rebuild starts clean
            // rather than accumulating a second lane next to the first.
            for (int i = rootRt.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(rootRt.GetChild(i).gameObject);

            // The placeholder's job is over the moment a real driver exists.
            var placeholder = root.GetComponent<PlaceholderSchemeDriver>();
            if (placeholder != null) Object.DestroyImmediate(placeholder);

            var driver = root.GetComponent<FreeSwingSchemeDriver>();
            if (driver == null) driver = root.AddComponent<FreeSwingSchemeDriver>();

            // ── Lane ────────────────────────────────────────────────────────────────
            var laneRoot = MakeRoot(rootRt, "FreeSwingLaneRoot");
            var laneView = laneRoot.gameObject.AddComponent<FreeSwingLaneView>();

            // ONE image: the fill and its 3px stroke are both in the baked sprite, untinted
            // (Color.white), so the two translucencies composite the way the node draws them.
            // Its HEIGHT is a placeholder — ApplyGeometry derives the real one at Activate.
            // Both the SIZE and the POSITION below are placeholders. FreeSwingLaneView.ApplyGeometry
            // derives the real ones from the LIVE config at Activate — reading ControlsConfig here
            // would put the numbers in two places and let a CSV retune move the ticks without
            // moving the pill they sit in. (It would also drag Golfin.Gameplay.Config into the
            // editor assembly for two constants.)
            var lane = MakeBaked(laneRoot, "FreeSwingLane", laneS, LaneWidth, LaneWidth);
            lane.pivot = new Vector2(0.5f, 1f);
            lane.anchoredPosition = Vector2.zero;
            // The node clips the lane's children (overflow-clip), which is what keeps a tick from
            // drawing past the rounded cap when a retune pushes it near the bottom.
            lane.gameObject.AddComponent<RectMask2D>();

            // Stadiums at r = h/2 rather than null-sprite Images: at 6px tall the rounded ends are
            // sub-pixel against the node's square tick, and a sprite-less flat fill is the exact
            // shape the UI-fidelity linter treats as fabricated art.
            var tick100 = MakeStadium(lane, "FreeSwingTick100", pill, FreeSwingColors.Tick100,
                                      LaneWidth, TickHeight, TickHeight * 0.5f);
            var tick120 = MakeStadium(lane, "FreeSwingTick120", pill, FreeSwingColors.Tick120,
                                      LaneWidth, TickHeight, TickHeight * 0.5f);
            // The green window goes UNDER the white impact line, as the node stacks them: the
            // 16px bar reads as a halo around the 6px line rather than swallowing it.
            var window  = MakeStadium(lane, "FreeSwingImpactWindow", pill, FreeSwingColors.ImpactWindow,
                                      92f, ImpactWindowHeight, ImpactWindowRadius);
            var impact  = MakeStadium(lane, "FreeSwingImpactLine", pill, FreeSwingColors.ImpactLine,
                                      LaneWidth, TickHeight, TickHeight * 0.5f);
            foreach (var rt in new[] { tick100, tick120, window, impact }) AnchorToLaneTop(rt);

            var label100 = MakeText(laneRoot, "FreeSwingLabel100", "100%", LabelFontNodePx,
                                    Color.white, FontStyles.Normal);
            var label120 = MakeText(laneRoot, "FreeSwingLabel120", "120%", LabelFontNodePx,
                                    FreeSwingColors.Tick120, FontStyles.Normal);
            // A LAYOUT PLACEHOLDER only — FreeSwingLaneView.RefreshLabels resolves
            // SWING_IMPACT_LINE at Activate. Authoring the real word here is how a hardcoded
            // literal ships, and the UI fidelity linter flags exactly that.
            var labelImp = MakeText(laneRoot, "FreeSwingImpactLabel", "(SWING_IMPACT_LINE)",
                                    LabelFontNodePx, Color.white, FontStyles.Normal);
            foreach (var t in new[] { label100, label120, labelImp }) SideLabel(t);

            Wire(laneView, ("_lane", lane), ("_tick100", tick100), ("_tick120", tick120),
                           ("_impactLine", impact), ("_impactWindow", window));
            // The fields are TextMeshProUGUI, not RectTransform — SerializedProperty silently
            // drops a reference of the wrong type, so the COMPONENT is what gets wired.
            WireObj(laneView, "_label100",    label100.GetComponent<TextMeshProUGUI>());
            WireObj(laneView, "_label120",    label120.GetComponent<TextMeshProUGUI>());
            WireObj(laneView, "_impactLabel", labelImp.GetComponent<TextMeshProUGUI>());
            // The lane DERIVES its own height and every offset from these four plus the config.
            WireFloat(laneView, "_handleRestBelowBall",  HandleRestBelowBall);
            WireFloat(laneView, "_clubHalfHeight",       ClubHalfHeight);
            WireFloat(laneView, "_laneTailPx",           LaneTailPx);
            WireFloat(laneView, "_puttFollowThroughPx",  PuttFollowThroughPx);
            WireFloat(laneView, "_impactWindowHeight",   ImpactWindowHeight);

            // ── Ball rest ghost ─────────────────────────────────────────────────────
            if (ghost != null)
            {
                var g = MakeImage(laneRoot, "FreeSwingBallRestGhost", ghost, Color.white,
                                  GhostSize, GhostSize);
                g.anchoredPosition = Vector2.zero;
                g.GetComponent<Image>().type = Image.Type.Simple;
            }
            else Debug.LogWarning("[FreeSwingSchemeBuilder] ghost sprite missing at " + GhostPath);

            // ── Finger trace ────────────────────────────────────────────────────────
            // A SIBLING of the lane, not a child: the node draws FingerTrace outside SwingLane's
            // clip rect, and a gesture that ran past the pill has to stay visible.
            var traceRoot = MakeRoot(rootRt, "FreeSwingTraceRoot");
            var traceView = traceRoot.gameObject.AddComponent<FreeSwingTraceView>();
            var traceGo   = new GameObject("FreeSwingTrace", typeof(RectTransform), typeof(CanvasRenderer));
            var traceRt   = (RectTransform)traceGo.transform;
            traceRt.SetParent(traceRoot, false);
            traceRt.anchorMin = traceRt.anchorMax = traceRt.pivot = new Vector2(0.5f, 0.5f);
            traceRt.sizeDelta = new Vector2(TraceBoundsPx, TraceBoundsPx);
            traceRt.anchoredPosition = Vector2.zero;   // the ball — the driver's own sample space
            var traceG = traceGo.AddComponent<FreeSwingTraceGraphic>();
            traceG.raycastTarget = false;
            traceG.color = FreeSwingColors.Trace;
            WireFloat(traceG, "_width", TraceWidth);
            WireColor(traceG, "_shadowColor", FreeSwingColors.TraceShadow);
            WireVector2(traceG, "_shadowOffset", new Vector2(0f, FreeSwingColors.TraceShadowOffsetY));
            WireObj(traceView, "_graphic", traceG);
            WireObj(traceView, "_group", traceRoot.GetComponent<CanvasGroup>());

            // ── Analyzer chip ───────────────────────────────────────────────────────
            var chipRoot = MakeRoot(rootRt, "FreeSwingAnalyzerChip");
            chipRoot.anchoredPosition = new Vector2(0f, ChipAboveBall);
            chipRoot.sizeDelta = new Vector2(ChipWidth, ChipHeight);
            var chip = chipRoot.gameObject.AddComponent<FreeSwingAnalyzerChip>();

            // The sprite carries its own blurred drop shadow in transparent padding, so the IMAGE
            // is the body plus that padding; the chip root stays the node's 840x150 body.
            var chipImg = MakeImage(chipRoot, "FreeSwingChipBg", chipS, Color.white,
                                    ChipWidth + ChipShadowPad * 2f, ChipHeight + ChipShadowPad * 2f);
            chipImg.GetComponent<Image>().type = Image.Type.Simple;

            // The label's own backdrop is the chip gradient SAMPLED at the label's height, so the
            // pre-composite is exact rather than taken off one end of a 150px ramp.
            Color labelC = FreeSwingColors.ChipLabel((ChipHeight * 0.5f - ChipLabelY) / ChipHeight);

            var lPower  = ChipCell(chipRoot, "FreeSwingLblPOWER",  "(SWING_POWER)",  -ChipColOuter, ChipLabelY, ChipLabelFontPx, labelC, FontStyles.Normal);
            var lImpact = ChipCell(chipRoot, "FreeSwingLblIMPACT", "(SWING_IMPACT)", -ChipColInner, ChipLabelY, ChipLabelFontPx, labelC, FontStyles.Normal);
            var lPath   = ChipCell(chipRoot, "FreeSwingLblPATH",   "(SWING_PATH)",    ChipColInner, ChipLabelY, ChipLabelFontPx, labelC, FontStyles.Normal);
            var lTempo  = ChipCell(chipRoot, "FreeSwingLblTEMPO",  "(SWING_TEMPO)",   ChipColOuter, ChipLabelY, ChipLabelFontPx, labelC, FontStyles.Normal);

            var vPower  = ChipCell(chipRoot, "FreeSwingValPOWER",  "0%", -ChipColOuter, ChipValueY, ChipValueFontPx, FreeSwingColors.ValueWhite, FontStyles.Bold);
            var vImpact = ChipCell(chipRoot, "FreeSwingValIMPACT", "0 px", -ChipColInner, ChipValueY, ChipValueFontPx, FreeSwingColors.ValueGreen, FontStyles.Bold);
            var vPath   = ChipCell(chipRoot, "FreeSwingValPATH",   "(SWING_PATH_STRAIGHT)", ChipColInner, ChipValueY, ChipValueFontPx, FreeSwingColors.ValueGreen, FontStyles.Bold);
            var vTempo  = ChipCell(chipRoot, "FreeSwingValTEMPO",  "(SWING_TEMPO_GOOD)", ChipColOuter, ChipValueY, ChipValueFontPx, FreeSwingColors.ValueAmber, FontStyles.Bold);

            WireObj(chip, "_group",       chipRoot.GetComponent<CanvasGroup>());
            WireObj(chip, "_labelPower",  lPower);
            WireObj(chip, "_labelImpact", lImpact);
            WireObj(chip, "_labelPath",   lPath);
            WireObj(chip, "_labelTempo",  lTempo);
            WireObj(chip, "_valuePower",  vPower);
            WireObj(chip, "_valueImpact", vImpact);
            WireObj(chip, "_valuePath",   vPath);
            WireObj(chip, "_valueTempo",  vTempo);
            // _holdSeconds is left at its own inspector default here: the driver pushes
            // ControlsConfig.FreeSwingAnalyzerSeconds into it at Activate, so the CSV stays the
            // single tuning surface.

            // ── Grade pop ───────────────────────────────────────────────────────────
            var popRoot = MakeRoot(rootRt, "FreeSwingGradePop");
            popRoot.anchoredPosition = new Vector2(0f, PopAboveBall);
            popRoot.sizeDelta = new Vector2(PopWidth, PopHeight);
            var pop = popRoot.gameObject.AddComponent<SchemeGradePop>();

            var popText = MakeText(popRoot, "FreeSwingGradeText", "PURE", PopFontNodePx,
                                   Color.white, FontStyles.Bold);
            Stretch(popText, 0f);
            var popTmp = popText.GetComponent<TextMeshProUGUI>();
            popTmp.alignment = TextAlignmentOptions.Center;
            ApplyTextShadow(popTmp, FreeSwingColors.LabelShadow);
            WireObj(pop, "_label", popTmp);
            WireObj(pop, "_group", popRoot.GetComponent<CanvasGroup>());

            // ── Handle — a copy of the flick's ClubHandle ───────────────────────────
            var source = FindInScene("ClubHandle");
            RectTransform handle;
            if (source != null)
            {
                var copy = Object.Instantiate(source, rootRt);
                copy.name = "FreeSwingHandle";
                // The flick's own behaviours come off: this handle is driven by the Free Swing
                // driver, and a second ClubHandleDragger would open a second external drag.
                StripAll<ClubHandleDragger>(copy);
                StripAll<TeeIdleGlowController>(copy);
                handle = copy.GetComponent<RectTransform>();
                var img = copy.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;   // the gesture starts on the club head
                copy.SetActive(true);
            }
            else
            {
                Debug.LogWarning("[FreeSwingSchemeBuilder] ClubHandle not found — building a bare " +
                                 "handle. The sprite binder will still paint it, but check the " +
                                 "clone provenance.");
                handle = MakeImage(rootRt, "FreeSwingHandle", null, Color.white, 178f, 100f);
                handle.gameObject.AddComponent<ClubHandleSpriteBinder>();
            }
            handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 0.5f);
            handle.pivot = new Vector2(0.5f, 0.5f);   // centre — see HandleRestBelowBall
            handle.sizeDelta = new Vector2(178f, 100f);
            handle.anchoredPosition = new Vector2(0f, -HandleRestBelowBall);
            handle.localScale = Vector3.one;
            handle.SetAsLastSibling();     // the club head reads on top of its own lane

            // ...but the chip and the pop read on top of the club, as the node's Result frame
            // draws them.
            chipRoot.SetAsLastSibling();
            popRoot.SetAsLastSibling();

            Wire(driver, ("_schemeRoot", rootRt), ("_handle", handle));
            WireObj(driver, "_laneView",     laneView);
            WireObj(driver, "_traceView",    traceView);
            WireObj(driver, "_analyzerChip", chip);
            WireObj(driver, "_gradePop",     pop);
            // The button row, AND the row's own reference to the FADE/DRAW widget inside it. The
            // second half matters: SetFadeDrawVisible resolves the button's CanvasGroup through
            // that field, and an unwired field makes the whole hide a silent no-op — the button
            // would sit there, visible and tappable, while IsFadeDrawVisible cheerfully reported
            // false. Wired HERE rather than left for a hand drag, per the never-manual-wiring rule.
            var buttons = FindComponentInScene<ActionButtonsRoot>();
            WireObj(driver, "_actionButtons", buttons);
            if (buttons != null)
            {
                var fd = buttons.GetComponentInChildren<FadeDrawButtonWidget>(true);
                if (fd != null) WireObj(buttons, "_fadeDrawButton", fd);
                else Debug.LogWarning("[FreeSwingSchemeBuilder] no FadeDrawButtonWidget under " +
                                      buttons.name + " — the toggle cannot be hidden.");
            }
            WireFloat(driver, "_handleLateralClampPx", LaneWidth * 0.5f);

            // Author the overlays INVISIBLE: the scheme root can be switched on at Idle, and a
            // lane sitting at full alpha over a shot nobody has started reads as a bug.
            laneRoot.GetComponent<CanvasGroup>().alpha  = 0f;
            traceRoot.GetComponent<CanvasGroup>().alpha = 0f;
            chipRoot.GetComponent<CanvasGroup>().alpha  = 0f;
            popRoot.GetComponent<CanvasGroup>().alpha   = 0f;

            EditorUtility.SetDirty(root);

            SnapshotForLint(root);
        }

        /// <summary>
        /// Where the UI-fidelity lint snapshot lives. Under <c>Assets/Editor/</c> so it is excluded
        /// from player builds outright.
        /// </summary>
        public const string LintSnapshotPath =
            "Assets/Editor/UIFidelity/Snapshots/SchemeRoot_FreeSwing.prefab";

        /// <summary>
        /// Save the built subtree as a prefab for <c>UIFidelityLinter</c> to lint.
        ///
        /// <para>WHY IT IS PERSISTED, AND WHY THE BUILDER OWNS IT. Rule 21's gate re-runs
        /// <c>UIFidelityLinter.LintPrefab</c> live and refuses a cached JSON as evidence, and the
        /// linter's only entry point takes a PREFAB — but this scheme is scene-authored, because
        /// the SPEC mandates <c>SchemeRoot_FreeSwing</c> in <c>LabScaffold</c>. A throwaway
        /// snapshot taken by hand satisfies the linter once and then cannot be re-run, and a
        /// snapshot committed by hand rots the moment the scene moves — the gate would then be
        /// linting last week's layout while reporting a fresh run.</para>
        ///
        /// <para>Writing it HERE removes both failure modes: the snapshot and the scene subtree
        /// come out of the same call, so they cannot disagree. It is a lint fixture and nothing
        /// instantiates it.</para>
        /// </summary>
        public static void SnapshotForLint(GameObject root)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LintSnapshotPath));
            // A prefab cannot be saved from an inactive root's hierarchy state cleanly, and the
            // scheme roots ship inactive (ShotSchemeHost turns on only the live one).
            bool wasActive = root.activeSelf;
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, LintSnapshotPath);
            root.SetActive(wasActive);
            AssetDatabase.ImportAsset(LintSnapshotPath);
            Debug.Log("[FreeSwingSchemeBuilder] lint snapshot -> " + LintSnapshotPath);
        }

        /// <summary>
        /// Snapshot the live subtree and lint it. Menu so the reviewers can re-run the gate
        /// themselves rather than trusting the cited JSON (Rule 21).
        /// </summary>
        [MenuItem("GOLFIN/ShotUI/Lint Free Swing Scheme")]
        public static void LintInOpenScene()
        {
            var root = FindInScene("SchemeRoot_FreeSwing");
            if (root == null)
            {
                Debug.LogError("[FreeSwingSchemeBuilder] SchemeRoot_FreeSwing not found — open " +
                               ScenePath + " first.");
                return;
            }
            SnapshotForLint(root);
            // No spec.json: the node-spec layer would compare the lane length and the tick offsets
            // against the node's own 560/300/360, which this scheme deliberately DERIVES from the
            // pull thresholds. Render-health + localisation-health are the layers that apply.
            Debug.Log(Golfin.EditorTools.UIFidelity.UIFidelityLinter.LintPrefab(LintSnapshotPath, null));
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

        private static T FindComponentInScene<T>() where T : Component
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            Debug.LogWarning($"[FreeSwingSchemeBuilder] no {typeof(T).Name} in the scene — the " +
                             "Fade/Draw toggle will not be hidden.");
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
        /// A rounded rect of the given RADIUS from the one shared stadium sprite.
        /// <c>pixelsPerUnitMultiplier = spriteBorder / radius</c> is the whole trick: without it
        /// the 88px corner renders at 88 UI px and a 6px-tall tick comes out an oval blob
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

        /// <summary>The baked bordered pill, 9-sliced at its authored 2x border. Untinted: the
        /// node's fill AND stroke alphas are already in the PNG, and tinting would multiply
        /// them both.</summary>
        private static RectTransform MakeBaked(RectTransform parent, string name, Sprite sprite,
                                               float w, float h)
        {
            var rt  = MakeImage(parent, name, sprite, Color.white, w, h);
            var img = rt.GetComponent<Image>();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = BakedPpum;
            return rt;
        }

        /// <summary>A lane child, anchored to the lane's TOP edge. The view sets the y.</summary>
        private static void AnchorToLaneTop(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>A tick label: outside the lane's right edge. The view sets the y.</summary>
        private static void SideLabel(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(200f, 40f);
            rt.anchoredPosition = new Vector2(LabelGapFromCentre, 0f);
        }

        private static TextMeshProUGUI ChipCell(RectTransform parent, string name, string preview,
                                                float x, float y, float nodePx, Color color,
                                                FontStyles style)
        {
            var rt = MakeText(parent, name, preview, nodePx, color, style);
            rt.sizeDelta = new Vector2(ChipCellWidth, ChipCellHeight);
            rt.anchoredPosition = new Vector2(x, y);
            var tmp = rt.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
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

        /// <summary>The node's <c>text-shadow 0 2 5 rgba(0,30,57,.9)</c>, as TMP's own underlay on
        /// a per-object material — not an <c>Outline</c> component, which Rule 21 reads as a
        /// fabricated border.</summary>
        private static void ApplyTextShadow(TextMeshProUGUI tmp, Color shadow)
        {
            if (tmp == null || tmp.fontSharedMaterial == null) return;
            var mat = new Material(tmp.fontSharedMaterial);
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor",
                         new Color(shadow.r, shadow.g, shadow.b, FreeSwingColors.LabelShadowAlpha));
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
            Debug.LogWarning("[FreeSwingSchemeBuilder] font '" + want + "' not found — TMP default used.");
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
                if (p == null) { Debug.LogError($"[FreeSwingSchemeBuilder] no field '{field}' on {target.GetType().Name}"); continue; }
                if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                p.objectReferenceValue = value;
                // A reference of the wrong type is dropped SILENTLY by SerializedProperty, which
                // is exactly how a [SerializeField] ends up null after a "successful" build.
                if (value != null && p.objectReferenceValue == null)
                    Debug.LogError($"[FreeSwingSchemeBuilder] '{field}' on {target.GetType().Name} rejected " +
                                   $"{value.GetType().Name} '{value.name}' — wrong type for that field.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireObj(Object target, string field, Object value)
            => Wire(target, new[] { (field, value) });

        private static void WireFloat(Object target, string field, float value)
            => WithProperty(target, field, p => p.floatValue = value);

        private static void WireColor(Object target, string field, Color value)
            => WithProperty(target, field, p => p.colorValue = value);

        private static void WireVector2(Object target, string field, Vector2 value)
            => WithProperty(target, field, p => p.vector2Value = value);

        private static void WithProperty(Object target, string field, System.Action<SerializedProperty> set)
        {
            var so = new SerializedObject(target);
            var p  = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[FreeSwingSchemeBuilder] no field '{field}' on {target.GetType().Name}"); return; }
            set(p);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
