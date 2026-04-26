using UnityEngine;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Shared constants for cone band lines and timing slab colors.
    // ConeMeshGraphic uses these as field defaults; ShotConeView reads them for slab coloring.
    // Edit here to change the visual language without hunting two files.
    public static class ConeBandPalette
    {
        // Y positions within the cone (0 = base, 1 = apex)
        public const float BandRedY01   = 0.00f;
        public const float BandGoldY01  = 0.45f;
        public const float BandGreenY01 = 0.85f;

        // Band line half-height in canvas pixels
        public const float BandHalfHeightPx = 1f;

        // Semi-transparent grey cone fill
        public static readonly Color FillColor  = new Color(200f / 255f, 200f / 255f, 200f / 255f, 90f / 255f);

        // Band / slab colors (hex from Figma — Aiming Cone.png reference)
        public static readonly Color ColorRed   = new Color(0x8B / 255f, 0x2A / 255f, 0x2A / 255f); // maroon
        public static readonly Color ColorGold  = new Color(0xA7 / 255f, 0x7C / 255f, 0x2A / 255f); // amber
        public static readonly Color ColorGreen = new Color(0x58 / 255f, 0x69 / 255f, 0x44 / 255f); // dark olive
    }
}
