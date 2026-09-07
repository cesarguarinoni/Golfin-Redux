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
        //
        // miss_grade_duff (2026-09-07, §3.4): the RED edge follows the other two out of the const
        // and into ControlsConfig. It is no longer the cone base — at/below it the flick is a
        // DUFF — so the line the player is looking at and the penalty they pay have to be the
        // same number for the same reason the gold and green edges do.
        public static float BandRedY01   => ControlsConfig.Default.TimingBandRedY01;
        public static float BandGoldY01  => ControlsConfig.Default.TimingBandGoldY01;
        public static float BandGreenY01 => ControlsConfig.Default.TimingBandGreenY01;

        // ── Grade-pop palette (miss_grade_duff §3.6, D8) ─────────────────────────
        // The pops, the Pendulum bar bands and the cone bands are now ONE palette: the colour
        // language a Flick player already reads is the colour language every scheme's grade word
        // is painted in. Three steps, not two sets of three — SchemeGradePop's two colour groups
        // (JUST/GOOD/MISS and PERFECT/HOOK/SLICE/SHANK) collapsed onto these.
        /// <summary>PURE — flush contact. Figma <c>#ADEBAD</c>.</summary>
        public static readonly Color GradePure = new Color(0xAD / 255f, 0xEB / 255f, 0xAD / 255f);
        /// <summary>GOOD / HOOK / SLICE / THIN — the near-miss amber. Figma <c>#FFEBA6</c>.</summary>
        public static readonly Color GradeNear = new Color(0xFF / 255f, 0xEB / 255f, 0xA6 / 255f);
        /// <summary>DUFF — the miss. Figma <c>#FF5A5A</c>.</summary>
        public static readonly Color GradeDuff = new Color(0xFF / 255f, 0x5A / 255f, 0x5A / 255f);

        /// <summary>The power gauge's DUFF flash (miss_grade_duff §3.5, D5). A deeper red than
        /// <see cref="GradeDuff"/> on purpose: the pop is a word on turf and has to stay legible,
        /// the gauge arc is chrome and has to read as an alarm.</summary>
        public static readonly Color GaugeMissFlash = new Color(0xFF / 255f, 0x3B / 255f, 0x3B / 255f);

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
