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
            => -canvasHeight * 0.5f + baseline
               + HandleDepthAtFullPull(pull120Px, handleRestBelowBall);

        /// <summary>
        /// The ball anchor actually applied, in canvas y.
        ///
        /// <para>Framing degrades gracefully rather than the flick release landing in the home
        /// gesture. On a 16:9 phone or a tablet there is not enough screen below 0.38 for a 648px
        /// pull, so the ball is RAISED until the 120% handle sits back on the baseline. The
        /// authored anchor is therefore a floor on tall screens and ignored on short ones — never
        /// the other way round.</para>
        ///
        /// <para>Flick and Needle have no lane (a cone and a ring, both drawn around the ball)
        /// and take the anchor unconditionally.</para>
        /// </summary>
        public static float ResolveBallY(float anchorViewportY, float canvasHeight, float baseline,
                                         bool schemeHasLane, float pull120Px, float handleRestBelowBall)
        {
            float anchor = AnchorY(anchorViewportY, canvasHeight);
            if (!schemeHasLane) return anchor;

            return Mathf.Max(anchor, BallYForLane(canvasHeight, baseline, pull120Px, handleRestBelowBall));
        }

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
