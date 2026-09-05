using UnityEngine;
using Golfin.Gameplay.UI.Controls.Needle;

namespace Golfin.Gameplay.UI.Controls.FreeSwing
{
    /// <summary>
    /// The linear-space colour treatment for this scheme's chrome (carry-over 5).
    ///
    /// <para>THE PROBLEM, INHERITED TWICE: Figma composites in sRGB and Unity blends in LINEAR,
    /// so handing Unity a node's colour AND its alpha renders every translucent element too
    /// light — measurably so; on Pendulum the amber band came out +28 RGB and Cesar named it.
    /// The treatment is per SITUATION, not per element, and this scheme has both:</para>
    ///
    /// <list type="bullet">
    /// <item><b>Over a KNOWN parent</b> (the analyzer chip's labels, which sit on the chip's own
    /// navy gradient) — pre-composite to an OPAQUE colour. Exact and backdrop-independent,
    /// because the backdrop is a colour this code owns.</item>
    /// <item><b>A VEIL over turf</b> (the impact window, the finger trace and its shadow) — the
    /// element must stay translucent or it stops being a veil, so the RGB is CORRECTED instead:
    /// keep the node's alpha, and solve for the colour whose LINEAR blend over turf lands on
    /// Figma's sRGB composite.</item>
    /// </list>
    ///
    /// <para>THE TRANSFER FUNCTIONS THEMSELVES ARE <see cref="NeedleColors"/>'s. That is not the
    /// cross-scheme sharing carry-over 1 forbids: IEC 61966-2-1 is not a tuning knob, it is how
    /// the renderer works, and a fourth copy of the sRGB curve would be a fourth place for it to
    /// drift. What is NOT shared is any VALUE — every token below is this node's own.</para>
    ///
    /// <para>The lane's own white-14% fill and white-50% stroke are not here either: they are
    /// baked into <c>S_FreeSwingLane.png</c> by <c>make_freeswing_sprites.py</c> at the alphas
    /// that solve over turf, because a translucent fill inside a translucent stroke cannot be
    /// drawn by stacking two tinted stadiums (the stroke layer paints the whole shape).</para>
    /// </summary>
    public static class FreeSwingColors
    {
        // ── Node tokens (section 3b, get_design_context on 14091:103259) ────────

        /// <summary>Tick100: <c>#FFD23A</c>, opaque — no correction needed.</summary>
        public static readonly Color32 Tick100 = new Color32(0xFF, 0xD2, 0x3A, 255);
        /// <summary>Tick120 and Label120: <c>#FF5A5A</c>, opaque.</summary>
        public static readonly Color32 Tick120 = new Color32(0xFF, 0x5A, 0x5A, 255);
        /// <summary>ImpactLine: white, opaque.</summary>
        public static readonly Color32 ImpactLine = new Color32(255, 255, 255, 255);

        /// <summary>The chip's vertical gradient, top and bottom (<c>#133453</c> → <c>#091B33</c>).
        /// Duplicated from the baker only in the sense that both read the same node token; the
        /// PNG carries the ramp and this pair exists so the LABEL's backdrop can be sampled off
        /// it rather than eyeballed.</summary>
        public static readonly Color32 ChipTop    = new Color32(0x13, 0x34, 0x53, 255);
        public static readonly Color32 ChipBottom = new Color32(0x09, 0x1B, 0x33, 255);

        /// <summary>The chip's value palette. Every one opaque in the node, so they ship as-is.
        /// GREEN is clean, AMBER is off, RED is a real miss — the same three-step ladder
        /// <c>SchemeGradePop</c> and the Pendulum's bands already use, so a player who has
        /// learned one scheme's colours has learned this one's.</summary>
        public static readonly Color32 ValueWhite = new Color32(255, 255, 255, 255);
        public static readonly Color32 ValueGreen = new Color32(0xAD, 0xEB, 0xAD, 255);
        public static readonly Color32 ValueAmber = new Color32(0xFF, 0xEB, 0xA6, 255);
        public static readonly Color32 ValueRed   = new Color32(0xFF, 0x5A, 0x5A, 255);

        /// <summary>The node's text-shadow on the lane labels: <c>0 2 5 rgba(0,30,57,.9)</c>.</summary>
        public static readonly Color32 LabelShadow = new Color32(0, 30, 57, 255);
        public const float LabelShadowAlpha = 0.9f;

        // ── The veils ───────────────────────────────────────────────────────────

        /// <summary>
        /// The green impact window: node <c>#ADEBAD</c> at 60% over turf, RGB-corrected.
        ///
        /// <para>A veil rather than a pre-composite because the bar sits directly on the fairway
        /// inside the lane's own translucent fill — there is no single opaque backdrop to
        /// composite against, and the ball ghost and the finger trace both pass under it.</para>
        /// </summary>
        public static Color ImpactWindow => NeedleColors.OverTurf(new Color32(0xAD, 0xEB, 0xAD, 255), 0.60f);

        /// <summary>The finger trace: the node SVG's own <c>stroke="white" stroke-opacity="0.85"</c>,
        /// corrected for the linear blend. The 0.6 the Result frame applies is a GROUP opacity, not
        /// a second stroke alpha, so it lives on the view's CanvasGroup and not here.</summary>
        public static Color Trace => NeedleColors.OverTurf(new Color32(255, 255, 255, 255), 0.85f);

        /// <summary>
        /// The trace's drop shadow: the SVG filter's <c>black at 40%</c>, offset 2px down.
        ///
        /// <para>A hard 2px offset, not a blur. The node's filter is a 2px Gaussian and a UI mesh
        /// cannot blur; the choice is between an offset copy and a uGUI <c>Shadow</c>/<c>Outline</c>
        /// component, and UI Rule 21 reads the latter as a fabricated border. The offset copy is
        /// drawn into the same mesh as the line so the two can never drift apart.</para>
        /// </summary>
        public static Color TraceShadow => NeedleColors.OverTurf(new Color32(0, 0, 0, 255), 0.40f);

        /// <summary>Vertical offset of that shadow, in canvas px (SVG <c>feOffset dy="2"</c>,
        /// i.e. DOWN the screen, which is −y in uGUI).</summary>
        public const float TraceShadowOffsetY = -2f;

        // ── The known-parent pre-composites ─────────────────────────────────────

        /// <summary>
        /// A chip label: node white at 70%, pre-composited over the chip gradient SAMPLED at the
        /// label's own height rather than over one end of it.
        ///
        /// <para>Sampled, because the ramp spans 150px and the labels sit near the top: taking the
        /// bottom colour would be a 10 RGB error on the darkest channel, which is the size of
        /// error the Needle's zone-over-zone backdrop mistake was.</para>
        /// </summary>
        /// <param name="y01">0 at the chip's top edge, 1 at its bottom.</param>
        public static Color ChipLabel(float y01)
            => NeedleColors.PreComposite(new Color32(255, 255, 255, 255), 0.70f, ChipGradientAt(y01));

        /// <summary>The chip's own fill at a normalised height — the same linear ramp the baker
        /// writes into the PNG, so a label's backdrop is read off the gradient rather than guessed.</summary>
        public static Color32 ChipGradientAt(float y01)
        {
            float t = Mathf.Clamp01(y01);
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Lerp(ChipTop.r, ChipBottom.r, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(ChipTop.g, ChipBottom.g, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(ChipTop.b, ChipBottom.b, t)), 255);
        }
    }
}
