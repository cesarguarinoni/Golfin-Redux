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
        /// <c>LaneHeight</c>, so the clamp below can never disagree with the drawn pill.</summary>
        public static float LaneDepthBelowBall(float pull120Px, float handleRestBelowBall,
                                               float clubHalfHeight, float laneTailPx)
            => handleRestBelowBall + pull120Px + clubHalfHeight + laneTailPx;

        /// <summary>The LOWEST ball y whose lane still ends on the baseline (shot_view_layout D6).
        /// On a short screen this is above the authored anchor and wins.</summary>
        public static float BallYForLane(float canvasHeight, float baseline, float pull120Px,
                                         float handleRestBelowBall, float clubHalfHeight, float laneTailPx)
            => -canvasHeight * 0.5f + baseline
               + LaneDepthBelowBall(pull120Px, handleRestBelowBall, clubHalfHeight, laneTailPx);

        /// <summary>
        /// The ball anchor actually applied, in canvas y.
        ///
        /// <para>D6: framing degrades gracefully rather than the lane running under the action
        /// buttons. On a 16:9 phone or a tablet there is not enough screen below 0.38 for a
        /// 648px pull, so the ball is RAISED until the lane end sits back on the baseline. The
        /// authored anchor is therefore a floor on tall screens and ignored on short ones —
        /// never the other way round, which would put the 120% handle in the home gesture.</para>
        ///
        /// <para>Flick and Needle have no lane (a cone and a ring, both drawn around the ball)
        /// and take the anchor unconditionally.</para>
        /// </summary>
        public static float ResolveBallY(float anchorViewportY, float canvasHeight, float baseline,
                                         bool schemeHasLane, float pull120Px,
                                         float handleRestBelowBall, float clubHalfHeight, float laneTailPx)
        {
            float anchor = AnchorY(anchorViewportY, canvasHeight);
            if (!schemeHasLane) return anchor;

            return Mathf.Max(anchor, BallYForLane(canvasHeight, baseline, pull120Px,
                                                  handleRestBelowBall, clubHalfHeight, laneTailPx));
        }

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
