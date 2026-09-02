// gps_profile_pack §B4 — shared GPS palette, hoisted from ScoreUploadScreenBuilder.cs.
// ALL GPS screen builders and controllers pull colours from here; no magic hex literals.
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// Shared colour palette for all GPS screens. Values are the same as ScoreUploadScreenBuilder
    /// — hoisted here so Profile / Avatar / Badges builders don't duplicate them.
    /// All values are sRGB hex → linear Unity Color.
    /// </summary>
    public static class GpsUiColor
    {
        public static readonly Color Gold       = Hex("#EEDC9A");
        public static readonly Color GoldSoft   = Hex("#F3ECC2");
        public static readonly Color Green      = Hex("#7ED488");
        public static readonly Color Muted      = Hex("#B7C3D3");
        public static readonly Color BadgeNavy  = Hex("#112D4F");
        public static readonly Color BadgeRing  = Hex("#B2A379");
        public static readonly Color White      = Color.white;
        public static readonly Color Transparent= new Color(0f, 0f, 0f, 0f);

        // Rarity colours for badge cells (Figma §BADGES node)
        public static readonly Color RarityCommon  = Hex("#B7C3D3");
        public static readonly Color RarityRare    = Hex("#6FA5E8");
        public static readonly Color RarityEpic    = Hex("#B48CF0");
        public static readonly Color RarityLegend  = Hex("#EEDC9A");

        /// <summary>Convert sRGB hex string (with or without #) to Unity Color (linear).</summary>
        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c);
            return c;
        }

        /// <summary>
        /// "Dark" alpha composite: same as ADark() in ScoreUploadScreenBuilder.
        /// Matches how near-black overlays are perceived in linear space.
        /// </summary>
        public static Color ADark(Color c, float srgbAlpha)
            => new Color(c.r, c.g, c.b, 1f - Mathf.Pow(1f - srgbAlpha, 2.2f));

        /// <summary>Standard alpha — rgba(r,g,b, alpha).</summary>
        public static Color A(Color c, float alpha)
            => new Color(c.r, c.g, c.b, alpha);

        /// <summary>
        /// Set a progress bar by WIDTH against its track, never by <see cref="Image.fillAmount"/>.
        ///
        /// <para>
        /// Image.Type.Filled discards 9-slicing: it squashes the whole capsule into the bar's
        /// height and then clips, so the cap arrives as a thin wedge instead of a round end. The
        /// score-upload trust bar documents this at ScoreUploadScreenBuilder:844-847 and drives its
        /// fill by width for exactly this reason; every bar in the GPS pack now goes through here.
        /// </para>
        /// </summary>
        public static void SetBarFill(Image fill, float fraction)
        {
            if (fill == null) return;
            var rt = fill.rectTransform;
            var track = rt.parent as RectTransform;
            if (track == null) return;
            float w = track.rect.width * Mathf.Clamp01(fraction);
            rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
        }

        /// <summary>Rarity label → border colour for badge cells.</summary>
        public static Color RarityBorderColor(string rarity)
        {
            switch ((rarity ?? "").ToUpperInvariant())
            {
                case "RARE":   return RarityRare;
                case "EPIC":   return RarityEpic;
                case "LEGEND": return RarityLegend;
                default:       return RarityCommon;  // COMMON
            }
        }
    }
}
