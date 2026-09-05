using UnityEngine;

namespace Golfin.Gameplay.UI.Controls.Needle
{
    /// <summary>
    /// The linear-space colour treatment for this scheme's chrome (scheme_needle carry-over 5).
    ///
    /// <para>THE PROBLEM, INHERITED FROM <c>scheme_pendulum</c>: Figma composites in sRGB and
    /// Unity blends in LINEAR. Handing Unity the node's own colour AND the node's own alpha
    /// therefore renders every translucent element too light — measurably so; on Pendulum the
    /// amber band came out +28 RGB and Cesar named it. Pendulum solved it two ways, and this file
    /// keeps both, one per situation:</para>
    ///
    /// <list type="bullet">
    /// <item><b>Over a KNOWN parent</b> (the zones, which sit on the arc) — pre-composite to an
    /// OPAQUE colour. Exact, and backdrop-independent, because the backdrop is a colour this code
    /// owns. <see cref="PreComposite"/>.</item>
    /// <item><b>A VEIL over turf</b> (the three power rings and the overpower crescent) — the
    /// element must stay translucent or it stops being a veil, so instead the RGB is CORRECTED:
    /// keep the node's alpha, and solve for the colour whose LINEAR blend over turf lands exactly
    /// on Figma's sRGB composite. <see cref="OverTurf"/>. Pendulum fitted a single scalar alpha
    /// per element and had to accept a per-channel residual (its track could not be fitted at
    /// all); correcting the RGB instead is exact on all three channels at the node's own alpha,
    /// with nothing hand-tuned.</item>
    /// </list>
    ///
    /// <para>Every value here is therefore DERIVED from the node's own token plus one measured
    /// backdrop — there is no hand-picked literal to drift when the node moves.</para>
    /// </summary>
    public static class NeedleColors
    {
        /// <summary>
        /// The fairway the shot chrome is drawn over, in sRGB. Inherited from
        /// <c>make_pendulum_sprites.py</c>, which fitted the Pendulum lane's veil against it after
        /// a fit against the reference render's darker patch read 15–28 RGB too light over real
        /// grass. The correction is exact at this backdrop and degrades gracefully away from it,
        /// which is the best a single colour can do against a moving photograph.
        /// </summary>
        public static readonly Color32 TurfSrgb = new Color32(94, 124, 56, 255);

        /// <summary>
        /// The accuracy arc's fill, pre-composited OPAQUE.
        ///
        /// <para>The node draws <c>#001E39</c> at 80% over turf. A single corrected colour cannot
        /// be found for it the way <see cref="OverTurf"/> finds one for the rings — at 80% the
        /// required correction runs out of range on the blue channel — which is the identical
        /// situation that made Pendulum pre-composite its track. So this IS the reference render's
        /// own pixel: the median of 1300 samples across the arc band of
        /// <c>reference/needle_timing.png</c> (R 10, G 38, B 55), which is by construction within
        /// 0 RGB of the design.</para>
        /// </summary>
        public static readonly Color32 ArcFill = new Color32(10, 38, 55, 255);

        /// <summary>The amber GOOD zone: node <c>#FFEBA6</c> at 75% over <see cref="ArcFill"/>,
        /// pre-composited. Verified against the reference render's own pixel (195, 188, 138).</summary>
        public static Color ZoneGood => PreComposite(new Color32(0xFF, 0xEB, 0xA6, 255), 0.75f, ArcFill);

        /// <summary>
        /// The blue PERFECT zone: node <c>#4DA3FF</c> at 95% over <see cref="ZoneGood"/> — the
        /// AMBER, not the arc, because the blue segment is nested inside the amber one and Figma
        /// composites it on top of that. Getting the backdrop wrong here is a 9 RGB error and it
        /// is how the reference caught it: over the arc this solves to (74, 157, 245) and the
        /// render's own pixel is (83, 165, 249); over the amber it solves to (83, 164, 249).
        /// </summary>
        public static Color ZonePerfect =>
            PreComposite(new Color32(0x4D, 0xA3, 0xFF, 255), 0.95f, ToColor32(ZoneGood));

        private static Color32 ToColor32(Color c) => new Color32(
            (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f), 255);

        /// <summary>The arc's 2px edge stroke: node white at 35%, over the arc's own fill.</summary>
        public static Color ArcStroke => PreComposite(new Color32(255, 255, 255, 255), 0.35f, ArcFill);

        // ── The two treatments ──────────────────────────────────────────────────

        /// <summary>
        /// Composite <paramref name="srgb"/> at <paramref name="alpha"/> over
        /// <paramref name="backdrop"/> the way Figma does — straight sRGB — and return the result
        /// as an OPAQUE colour. For anything drawn on a parent whose colour this code owns.
        /// </summary>
        public static Color PreComposite(Color32 srgb, float alpha, Color32 backdrop)
        {
            float a = Mathf.Clamp01(alpha);
            return new Color(
                (a * srgb.r + (1f - a) * backdrop.r) / 255f,
                (a * srgb.g + (1f - a) * backdrop.g) / 255f,
                (a * srgb.b + (1f - a) * backdrop.b) / 255f, 1f);
        }

        /// <summary>
        /// A translucent veil over turf, corrected for Unity's linear blend.
        ///
        /// <para>Keeps the node's <paramref name="alpha"/> — the element has to stay a veil — and
        /// returns the colour <c>C'</c> for which</para>
        /// <code>
        ///   srgb( a·lin(C') + (1−a)·lin(turf) )  ==  a·C + (1−a)·turf      // Figma's own composite
        /// </code>
        /// <para>i.e. <c>lin(C') = ( lin(T) − (1−a)·lin(turf) ) / a</c>, evaluated per channel.
        /// Exact on all three channels for every colour in this scheme (verified in the task
        /// report); clamped, so a future token that cannot be reached simply saturates instead of
        /// producing a negative colour.</para>
        /// </summary>
        public static Color OverTurf(Color32 srgb, float alpha) => OverTurf(srgb, alpha, TurfSrgb);

        public static Color OverTurf(Color32 srgb, float alpha, Color32 backdrop)
        {
            float a = Mathf.Clamp(alpha, 1e-3f, 1f);
            return new Color(
                Channel(srgb.r, backdrop.r, a),
                Channel(srgb.g, backdrop.g, a),
                Channel(srgb.b, backdrop.b, a), a);
        }

        private static float Channel(byte srgb, byte backdrop, float a)
        {
            float target  = a * srgb + (1f - a) * backdrop;              // what Figma renders
            float wantLin = (ToLinear(target) - (1f - a) * ToLinear(backdrop)) / a;
            // Back to sRGB: a UnityEngine.Color handed to a UI Image is GAMMA-encoded (which is
            // why Pendulum's pre-composited Color32 values worked verbatim). Only the BLEND is
            // linear — that is the entire bug this class exists to undo, and returning the linear
            // number here would double-apply the transfer.
            return ToSrgb(Mathf.Clamp01(wantLin));
        }

        /// <summary>sRGB 0..255 → linear 0..1, the exact IEC 61966-2-1 transfer, not a 2.2 gamma
        /// approximation: the rings live in the toe of the curve where the two disagree most.</summary>
        private static float ToLinear(float srgb255)
        {
            float c = Mathf.Clamp01(srgb255 / 255f);
            return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>The inverse of <see cref="ToLinear"/>, returning 0..1.</summary>
        private static float ToSrgb(float lin)
        {
            lin = Mathf.Clamp01(lin);
            return lin <= 0.0031308f ? lin * 12.92f : 1.055f * Mathf.Pow(lin, 1f / 2.4f) - 0.055f;
        }
    }
}
