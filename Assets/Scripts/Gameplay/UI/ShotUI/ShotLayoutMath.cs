using UnityEngine;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Where the shot view's moving parts sit, as arithmetic (shot_view_layout §3.3).
    ///
    /// <para>PURE, AND SEPARATE FROM THE COMPONENT ON PURPOSE. Every number here is a claim about
    /// a 1170x2532 canvas that has to keep holding on a 16:9 phone and a 4:3 tablet, and the only
    /// honest way to pin that is an EditMode test that never opens a scene. The component
    /// (<see cref="ShotLayoutController"/>) reads rects and writes rects; this file decides what
    /// to write.</para>
    ///
    /// <para>All Y values are CANVAS px with the origin at the canvas CENTRE — the space a
    /// centre-anchored <c>RectTransform.anchoredPosition</c> already lives in — so the canvas
    /// bottom is <c>-height/2</c>. Canvas px are width-normalised by the CanvasScaler, which is
    /// why the constants are device-independent and the aspect clamp below is the only thing that
    /// has to care about screen shape.</para>
    /// </summary>
    public static class ShotLayoutMath
    {
        /// <summary>Clearance kept ABOVE the reported safe-area inset when that inset is deeper
        /// than <c>BottomBaselinePx</c>. The baseline is where a thumb finishes a 120% pull, so
        /// sitting exactly on the inset would put the release right on the home-gesture edge.</summary>
        public const float SafeAreaClearancePx = 60f;

        /// <summary>Canvas y for a viewport fraction (0 = bottom, 1 = top).</summary>
        public static float AnchorY(float viewportY, float canvasHeight)
            => (viewportY - 0.5f) * canvasHeight;

        /// <summary>
        /// The shared bottom baseline, in canvas px ABOVE the canvas bottom.
        ///
        /// <para>The configured value is a floor, not an answer: a device whose bottom inset is
        /// deeper than the authored 170 gets pushed up instead of having its buttons drawn under
        /// the home indicator.</para>
        /// </summary>
        public static float Baseline(float baselinePx, float safeBottomCanvasPx)
            => Mathf.Max(baselinePx, safeBottomCanvasPx + SafeAreaClearancePx);

        /// <summary>How far below the ball centre a pull lane's bottom edge sits at full pull —
        /// the same derivation <c>PendulumLaneView</c>/<c>FreeSwingLaneView</c> use for
        /// <c>LaneHeight</c>. Reported, and used by <see cref="LaneEndY"/>; NOT what the clamp
        /// guards — see <see cref="BallYForLane"/>.</summary>
        public static float LaneDepthBelowBall(float pull120Px, float handleRestBelowBall,
                                               float clubHalfHeight, float laneTailPx)
            => handleRestBelowBall + pull120Px + clubHalfHeight + laneTailPx;

        /// <summary>How far below the ball centre the club head's CENTRE sits at a 120% pull —
        /// which is where the finger is, and therefore the thing the home-gesture zone is about.
        /// </summary>
        public static float HandleDepthAtFullPull(float pull120Px, float handleRestBelowBall)
            => handleRestBelowBall + pull120Px;

        /// <summary>
        /// The LOWEST ball y that still keeps the 120% HANDLE on the baseline
        /// (shot_view_layout D3 + D6). On a short screen this is above the authored anchor and
        /// wins.
        ///
        /// <para>THE HANDLE, NOT THE LANE'S END. D3 says it plainly — "the 120% handle position
        /// is the thing that must clear the home-gesture zone (the flick starts there), not the
        /// lane's rounded end" — and D6's formula, written before that was settled, guarded the
        /// end instead. The difference is the club's lower half plus the pill's tail: ~170px on
        /// the current 3x club head, which is enough to push the ball 96px above the Figma anchor
        /// and cost ~4 points of horizon. The tail that now hangs below the baseline is 120px
        /// wide down the centre of the screen; the action buttons live at x +/-382..527, so it
        /// reaches nothing.</para>
        /// </summary>
        public static float BallYForLane(float canvasHeight, float baseline, float pull120Px,
                                         float handleRestBelowBall)
            => BallYForDepth(canvasHeight, baseline,
                             HandleDepthAtFullPull(pull120Px, handleRestBelowBall));

        /// <summary>The same clamp expressed on the DEPTH alone, for a scheme whose deepest drawn
        /// thing is not a club head at the end of a pull. Flick's is the cone BASE
        /// (flick_shot_view D6) — see <see cref="FlickLaneDepthBelowBall"/>.</summary>
        public static float BallYForDepth(float canvasHeight, float baseline, float depthBelowBall)
            => -canvasHeight * 0.5f + baseline + depthBelowBall;

        /// <summary>
        /// How far below the ball centre the Flick cone's BASE sits — apex gap plus cone height,
        /// which is exactly what <c>ShotConeView</c> writes to <c>ConeMesh.anchoredPosition.y</c>.
        ///
        /// <para>The gap is 0 today — the apex sits ON the ball, as the scheme has always shipped —
        /// so this is the cone height. It stays a separate term because the gap is a design choice
        /// somebody may want back, and a term you can see is one you can change.</para>
        ///
        /// <para>THE BASE, NOT THE HANDLE, and that is the difference from the three lane schemes.
        /// The lanes clamp on the 120% club head (D3) because the club overhangs a pill that is
        /// itself trimmed to the baseline. The cone has no separate drawn end to trim: the mesh IS
        /// the geometry, its base is where 100% lives, and a base past the baseline would put the
        /// full-power flick under the action-button row. The 3x club head still overhangs it at
        /// full pull, exactly as the capped pill lets it.</para>
        /// </summary>
        public static float FlickLaneDepthBelowBall(float coneApexGapPx, float coneHeightPx)
            => coneApexGapPx + coneHeightPx;

        /// <summary>
        /// The ball anchor actually applied, in canvas y.
        ///
        /// <para>Framing degrades gracefully rather than the flick release landing in the home
        /// gesture. On a 16:9 phone or a tablet there is not enough screen below 0.38 for a 648px
        /// pull, so the ball is RAISED until the 120% handle sits back on the baseline. The
        /// authored anchor is therefore a floor on tall screens and ignored on short ones — never
        /// the other way round.</para>
        ///
        /// <para>Needle has no lane (a ring drawn around the ball, which cannot run off the bottom
        /// of the screen) and takes the anchor unconditionally. Flick DOES take the clamp since
        /// flick_shot_view D6 — its cone reaches the baseline like a lane — but on the depth
        /// overload below, because the thing being guarded is the cone base, not a club head.</para>
        /// </summary>
        public static float ResolveBallY(float anchorViewportY, float canvasHeight, float baseline,
                                         bool schemeHasLane, float pull120Px, float handleRestBelowBall)
            => ResolveBallY(anchorViewportY, canvasHeight, baseline, schemeHasLane,
                            HandleDepthAtFullPull(pull120Px, handleRestBelowBall));

        /// <summary>
        /// <see cref="ResolveBallY(float,float,float,bool,float,float)"/> for a scheme that
        /// already knows its own depth below the ball. The three lane schemes keep the six-argument
        /// signature so a pull retune stays one edit; Flick comes through here with
        /// <see cref="FlickLaneDepthBelowBall"/>.
        /// </summary>
        public static float ResolveBallY(float anchorViewportY, float canvasHeight, float baseline,
                                         bool schemeHasLane, float laneDepthBelowBall)
        {
            float anchor = AnchorY(anchorViewportY, canvasHeight);
            if (!schemeHasLane) return anchor;

            return Mathf.Max(anchor, BallYForDepth(canvasHeight, baseline, laneDepthBelowBall));
        }

        /// <summary>Minimum drawn pill left BELOW the deepest tick, in canvas px. Small, but not
        /// zero: a tick sitting exactly on the rounded end reads as the end of the lane rather
        /// than as a line across it.</summary>
        public const float MinTailBelowDeepestTickPx = 8f;

        /// <summary>
        /// The pill's drawn height after the bottom cap (shot_view_layout_followup §1).
        ///
        /// <para>WHY THE PILL IS CAPPED AND THE PULL IS NOT. The baseline clamp guards the FINGER
        /// (D3), so a club head that scales up hangs below the pill instead of pushing the ball
        /// back up the screen — deliberate. What that leaves is a rounded tail poking past the
        /// row the buttons and the selector overlays sit on. This trims the DRAWN pill to the
        /// baseline; the drivers still clamp the pull on <c>cfg.*Pull120Px</c>, so 120% is exactly
        /// as reachable as it was and the club head simply overhangs the end at full pull.</para>
        ///
        /// <para>The floor wins over the cap, not the other way round: on a screen short enough
        /// that the 120% tick is itself at the baseline, 8px of pill below it beats a tick drawn
        /// on the rounded end.</para>
        /// </summary>
        /// <param name="derivedHeight">What the view would draw uncapped — deepest tick plus the
        /// club's lower half plus the tail.</param>
        /// <param name="deepestTickBelowTop">The 120% tick (or the 100% tick on a putt) measured
        /// down from the lane's TOP edge, which is what the height is relative to.</param>
        /// <param name="laneTopCanvasY">The lane's top edge in canvas-centre space.</param>
        /// <param name="laneEndCapY">Canvas y the pill may not extend past;
        /// <see cref="float.NegativeInfinity"/> for "uncapped".</param>
        public static float CappedLaneHeight(float derivedHeight, float deepestTickBelowTop,
                                             float laneTopCanvasY, float laneEndCapY)
        {
            // BOTH ends have to be real numbers. A view with no canvas answers NaN for its own top
            // edge, and NaN silently loses every Min/Max it touches — which would have collapsed an
            // uncapped lane onto the 8px floor rather than leaving it alone.
            if (float.IsNegativeInfinity(laneEndCapY) || !IsFinite(laneEndCapY) ||
                !IsFinite(laneTopCanvasY))
                return derivedHeight;

            float capped = Mathf.Min(derivedHeight, laneTopCanvasY - laneEndCapY);
            return Mathf.Max(capped, deepestTickBelowTop + MinTailBelowDeepestTickPx);
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        /// <summary>Canvas y of the club head's centre at a 120% pull — the point the baseline
        /// guard is about, and the one the acceptance run measures.</summary>
        public static float HandleYAtFullPull(float ballY, float pull120Px, float handleRestBelowBall)
            => ballY - HandleDepthAtFullPull(pull120Px, handleRestBelowBall);

        /// <summary>Canvas y of the lane's bottom edge for a ball at <paramref name="ballY"/>.</summary>
        public static float LaneEndY(float ballY, float pull120Px, float handleRestBelowBall,
                                     float clubHalfHeight, float laneTailPx)
            => ballY - LaneDepthBelowBall(pull120Px, handleRestBelowBall, clubHalfHeight, laneTailPx);

        /// <summary>
        /// <c>anchoredPosition.y</c> that puts a TOP-ANCHORED rect's CENTRE on a viewport
        /// fraction.
        ///
        /// <para><paramref name="pivotOffsetAboveCentre"/> is <c>height * (pivot.y - 0.5)</c> —
        /// how far the anchoredPosition's own reference point sits above the rect's middle. The
        /// power gauge is pivoted (1,1), so anchoredPosition places its TOP edge and the gauge's
        /// centre is a half-height lower; asking for "the gauge at 30% from the top" and writing
        /// the top edge there would hang it a full half-height too low.</para>
        /// </summary>
        public static float PowerHudAnchoredY(float gaugeViewportY, float canvasHeight, float pivotOffsetAboveCentre)
            => -((1f - gaugeViewportY) * canvasHeight - pivotOffsetAboveCentre);
    }
}
