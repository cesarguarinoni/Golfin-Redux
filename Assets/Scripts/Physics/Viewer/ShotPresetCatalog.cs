using System.Collections.Generic;
using System.Linq;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Hardcoded list of 16 canonical presets for the physics lab (10 Range + 5 Hole1 + 1 Dashboard).
    /// Range shots fire along +X. Carry is measured as XZ distance from origin.
    /// Backspin axis = (0,0,+1): Cross((0,0,1), (vx_hat, vy_hat, 0)) ≈ (0, vx_hat, 0) — upward Magnus lift.
    /// </summary>
    public static class ShotPresetCatalog
    {
        public static IReadOnlyList<ShotPreset> All { get; } = BuildAll();

        public static IEnumerable<ShotPreset> ForScene(PresetScene s)
            => All.Where(p => p.Scene == s);

        // ── helpers ───────────────────────────────────────────────────────────

        static fp3 BackspinAxis => new fp3(fp.Zero, fp.Zero, fp.One);

        static SpinState Backspin(float rpm)
        {
            const float TwoPI = 6.28318530718f;
            float rps = rpm * TwoPI / 60f;
            return new SpinState(BackspinAxis, fp.FromFloat(rps));
        }

        static WindConfig Headwind10()
        {
            var w = WindConfig.Calm;
            w.BaseVelocity = new fp3(fp.FromFloat(-10f), fp.Zero, fp.Zero);
            return w;
        }

        static WindConfig Tailwind10()
        {
            var w = WindConfig.Calm;
            w.BaseVelocity = new fp3(fp.FromFloat(10f), fp.Zero, fp.Zero);
            return w;
        }

        static WindConfig Crosswind10()
        {
            var w = WindConfig.Calm;
            w.BaseVelocity = new fp3(fp.Zero, fp.Zero, fp.FromFloat(10f));
            return w;
        }

        // ── catalog ───────────────────────────────────────────────────────────

        static IReadOnlyList<ShotPreset> BuildAll()
        {
            var list = new List<ShotPreset>();

            // ── Range (1–10) ──────────────────────────────────────────────────

            // 1 — driver_calm: ~275yd / ~251m carry  (75 m/s @ 10.9° — matches AerodynamicsTests reference)
            list.Add(new ShotPreset(
                "driver_calm", "Driver — calm", PresetScene.Range,
                origin: fp3.Zero,
                velocity: new fp3(fp.FromFloat(73.6f), fp.FromFloat(14.2f), fp.Zero),
                spin: Backspin(2686f),
                wind: WindConfig.Calm,
                notes: "~275yd (~251m) carry. Tour-level driver (75 m/s, 10.9°, 2686rpm) in calm air."
            ));

            // 2 — driver_headwind: ~245yd carry
            list.Add(new ShotPreset(
                "driver_headwind", "Driver — 10 m/s headwind", PresetScene.Range,
                origin: fp3.Zero,
                velocity: new fp3(fp.FromFloat(73.6f), fp.FromFloat(14.2f), fp.Zero),
                spin: Backspin(2686f),
                wind: Headwind10(),
                notes: "~245yd carry. Headwind visibly shortens flight. Compare with driver_calm."
            ));

            // 3 — driver_tailwind: ~305yd carry
            list.Add(new ShotPreset(
                "driver_tailwind", "Driver — 10 m/s tailwind", PresetScene.Range,
                origin: fp3.Zero,
                velocity: new fp3(fp.FromFloat(73.6f), fp.FromFloat(14.2f), fp.Zero),
                spin: Backspin(2686f),
                wind: Tailwind10(),
                notes: "~305yd carry. Tailwind extends flight. Compare with driver_calm."
            ));

            // 4 — driver_crosswind: ~275yd carry + lateral Z drift
            list.Add(new ShotPreset(
                "driver_crosswind", "Driver — 10 m/s crosswind", PresetScene.Range,
                origin: fp3.Zero,
                velocity: new fp3(fp.FromFloat(73.6f), fp.FromFloat(14.2f), fp.Zero),
                spin: Backspin(2686f),
                wind: Crosswind10(),
                notes: "~275yd carry, lateral Z drift visible. Crosswind pushes ball along +Z."
            ));

            // 5 — iron7_calm: ~155yd total
            list.Add(new ShotPreset(
                "iron7_calm", "Iron 7 — calm", PresetScene.Range,
                origin: fp3.Zero,
                velocity: new fp3(fp.FromFloat(46f), fp.FromFloat(22f), fp.Zero),
                spin: Backspin(6500f),
                wind: WindConfig.Calm,
                notes: "~155yd total (~142m). Classic mid-iron approach shape."
            ));

            // 6 — wedge_100_backspin: checks near landing
            list.Add(new ShotPreset(
                "wedge_100_backspin", "Wedge 100yd — backspin", PresetScene.Range,
                origin: fp3.Zero,
                velocity: new fp3(fp.FromFloat(32f), fp.FromFloat(28f), fp.Zero),
                spin: Backspin(9000f),
                wind: WindConfig.Calm,
                notes: "~91m. High backspin (9000rpm). Ball checks near landing; contrast with preset #7."
            ));

            // 7 — wedge_100_zerospin: rolls out
            list.Add(new ShotPreset(
                "wedge_100_zerospin", "Wedge 100yd — zero spin", PresetScene.Range,
                origin: fp3.Zero,
                velocity: new fp3(fp.FromFloat(32f), fp.FromFloat(28f), fp.Zero),
                spin: SpinState.None,
                wind: WindConfig.Calm,
                notes: "~91m. Zero spin. Rolls out visibly more than backspin preset #6."
            ));

            // 8 — cartpath_bounce: first bounce ≥60% of drop height
            list.Add(new ShotPreset(
                "cartpath_bounce", "Cart path — bounce", PresetScene.Range,
                origin: new fp3(fp.Zero, fp.FromFloat(10f), fp.Zero),
                velocity: new fp3(fp.FromFloat(25f), fp.FromFloat(-10f), fp.Zero),
                spin: SpinState.None,
                wind: WindConfig.Calm,
                notes: "Dropped onto cart path from 10m. Cr=0.70. First bounce height ≥60% of drop.",
                hasSurfaceOverride: true, surfaceOverride: SurfaceType.CartPath
            ));

            // 9 — rough_landing: ball plugs in rough
            list.Add(new ShotPreset(
                "rough_landing", "Rough — plugged lie", PresetScene.Range,
                origin: fp3.Zero,
                velocity: new fp3(fp.FromFloat(40f), fp.FromFloat(15f), fp.Zero),
                spin: SpinState.None,
                wind: WindConfig.Calm,
                notes: "All terrain is rough (Cr=0.25). Ball plugs; single bounce, very short roll.",
                hasSurfaceOverride: true, surfaceOverride: SurfaceType.Rough
            ));

            // 10 — water_terminates: sim ends at HitWater
            list.Add(new ShotPreset(
                "water_terminates", "Water — terminates", PresetScene.Range,
                origin: fp3.Zero,
                velocity: new fp3(fp.FromFloat(30f), fp.FromFloat(10f), fp.Zero),
                spin: SpinState.None,
                wind: WindConfig.Calm,
                notes: "All terrain is water. Sim terminates immediately at first contact: HitWater.",
                hasSurfaceOverride: true, surfaceOverride: SurfaceType.Water
            ));

            // ── Hole 1 (11–15) ────────────────────────────────────────────────
            //
            // Green center_local from greens.json: x ≈ -230, z ≈ -73 (Unity world space).
            // Y is ignored by RunPuttPhase (snapped to heightmap + ball radius).
            // Origins chosen empirically from green polygon bounds (-241..−219, -85..−62).

            fp gx = fp.FromFloat(-230f);
            fp gz = fp.FromFloat(-73f);

            // 11 — putt_flat_3m: 0.35 m/s, flat spot, target stop ~3m
            list.Add(new ShotPreset(
                "putt_flat_3m", "Putt — flat 3m", PresetScene.Hole1,
                origin: new fp3(gx, fp.Zero, gz),
                velocity: new fp3(fp.FromFloat(0.35f), fp.Zero, fp.Zero),
                spin: SpinState.None,
                wind: WindConfig.Calm,
                notes: "0.35 m/s on flat green. Target stop: 3.0m ±0.3m. Calibrated from k=0.10."
            ));

            // 12 — putt_uphill_6m: more power needed, stops short vs flat
            list.Add(new ShotPreset(
                "putt_uphill_6m", "Putt — uphill 6m", PresetScene.Hole1,
                origin: new fp3(gx + fp.FromFloat(3f), fp.Zero, gz),
                velocity: new fp3(fp.FromFloat(0.75f), fp.Zero, fp.Zero),
                spin: SpinState.None,
                wind: WindConfig.Calm,
                notes: "0.75 m/s uphill. Gravity fights the ball. Stops shorter than flat-6m equivalent."
            ));

            // 13 — putt_downhill_6m: less power needed, runs past flat equivalent
            list.Add(new ShotPreset(
                "putt_downhill_6m", "Putt — downhill 6m", PresetScene.Hole1,
                origin: new fp3(gx - fp.FromFloat(3f), fp.Zero, gz),
                velocity: new fp3(fp.FromFloat(0.45f), fp.Zero, fp.Zero),
                spin: SpinState.None,
                wind: WindConfig.Calm,
                notes: "0.45 m/s downhill. Gravity assists. Runs past flat-6m equivalent."
            ));

            // 14 — putt_crossslope_6m: curves to low side
            list.Add(new ShotPreset(
                "putt_crossslope_6m", "Putt — cross-slope 6m", PresetScene.Hole1,
                origin: new fp3(gx, fp.Zero, gz + fp.FromFloat(3f)),
                velocity: new fp3(fp.FromFloat(0.75f), fp.Zero, fp.Zero),
                spin: SpinState.None,
                wind: WindConfig.Calm,
                notes: "0.75 m/s across slope. Ball curves toward the low side of the green."
            ));

            // 15 — putt_off_back: green → GreenCollar → rough transition
            list.Add(new ShotPreset(
                "putt_off_back", "Putt — off back edge", PresetScene.Hole1,
                origin: new fp3(fp.FromFloat(-221f), fp.Zero, gz),
                velocity: new fp3(fp.FromFloat(3.5f), fp.Zero, fp.Zero),
                spin: SpinState.None,
                wind: WindConfig.Calm,
                notes: "3.5 m/s from near back edge (+X direction). Transitions: Green → GreenCollar → Rough."
            ));

            // ── Dashboard (reference shot) ─────────────────────────────────────

            list.Add(new ShotPreset(
                "driver_calm_dashboard", "Driver — calm (dashboard)", PresetScene.Dashboard,
                origin: fp3.Zero,
                velocity: new fp3(fp.FromFloat(73.6f), fp.FromFloat(14.2f), fp.Zero),
                spin: Backspin(2686f),
                wind: WindConfig.Calm,
                notes: "Dashboard reference shot. Change Aero/Surface sliders and re-fire to see effect."
            ));

            return list.AsReadOnly();
        }
    }
}
