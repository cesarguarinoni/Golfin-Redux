using UnityEngine;
using Golfin.Gameplay.Config;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Shared constants for cone band lines and timing slab colors.
    // ConeMeshGraphic uses these as field defaults; ShotConeView reads them for slab coloring.
    // Edit here to change the visual language without hunting two files.
    public static class ConeBandPalette
    {
        // Y positions within the cone (0 = base, 1 = apex).
        //
        // F15 (shot_timing_power, D3): the gold/green edges are no longer literals here — they
        // are read from ControlsConfig, which is also what ShotController.TimingPowerMultiplier
        // reads. The band the player sees and the power penalty they pay are now one number, so
        // they cannot drift apart. ControlsConfig.Default (not the CSV-loaded config) is the
        // right source: ShotController's _config is ControlsConfig.Default in production too —
        // nothing calls InjectConfig outside tests, so controls.csv is documentation today.
        public const  float BandRedY01   = 0.00f;
        public static float BandGoldY01  => ControlsConfig.Default.TimingBandGoldY01;
        public static float BandGreenY01 => ControlsConfig.Default.TimingBandGreenY01;

        // Band line half-height in canvas pixels (2px = 4px total — matches Figma reference line weight)
        public const float BandHalfHeightPx = 2f;

        // Semi-transparent grey cone fill
        public static readonly Color FillColor  = new Color(200f / 255f, 200f / 255f, 200f / 255f, 90f / 255f);

        // Band line colors — dark, thin, zone-marker lines on the cone fill
        public static readonly Color ColorRed   = new Color(0x8B / 255f, 0x2A / 255f, 0x2A / 255f); // maroon
        public static readonly Color ColorGold  = new Color(0xA7 / 255f, 0x7C / 255f, 0x2A / 255f); // amber
        public static readonly Color ColorGreen = new Color(0x58 / 255f, 0x69 / 255f, 0x44 / 255f); // dark olive

        // Timing slab colors — pastel / semi-transparent (Figma "Timing Arrows" reference)
        public static readonly Color SlabColorRed   = new Color(1.00f, 0.60f, 0.60f, 0.70f); // light salmon
        public static readonly Color SlabColorGold  = new Color(1.00f, 0.92f, 0.65f, 0.70f); // light cream
        public static readonly Color SlabColorGreen = new Color(0.68f, 0.92f, 0.68f, 0.70f); // light mint
    }
}
