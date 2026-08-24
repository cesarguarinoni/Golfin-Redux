# Skybox assets — sources and licensing

## Poly Haven HDRIs (CC0 / public domain)

The eight rotating skies come from [Poly Haven](https://polyhaven.com/hdris), which
publishes every asset under **CC0 1.0 Universal (public domain dedication)**. CC0 imposes
**no attribution requirement** and permits commercial use, modification and
redistribution. This file records provenance as good practice, not as a licence
obligation.

| Preset | File in repo | Poly Haven asset | Sun elevation |
|---|---|---|---|
| Morning | `T_Sky_MorningClear.hdr` | `qwantani_morning_puresky` | 20.2° |
| Morning (Cloudy) | `T_Sky_MorningCloudy.hdr` | `kloofendal_misty_morning_puresky` | overcast, no disc |
| Noon | `T_Sky_NoonClear.hdr` | `qwantani_noon_puresky` | 49.8° |
| Noon (Cloudy) | `T_Sky_NoonCloudy.hdr` | `kloppenheim_05_puresky` | 74.5° |
| Afternoon | `T_Sky_AfternoonClear.hdr` | `qwantani_afternoon_puresky` | 40.8° |
| Afternoon (Cloudy) | `T_Sky_AfternoonCloudy.hdr` | `kloofendal_28d_misty_puresky` | 28.5° |
| Evening | `T_Sky_EveningClear.hdr` | `qwantani_late_afternoon_puresky` | 19.1° |
| Evening (Cloudy) | `T_Sky_EveningCloudy.hdr` | `qwantani_sunset_puresky` | 6.1° (light clamped to 12°) |

All are "pure sky" variants — the lower hemisphere is clean sky rather than photographed
ground, which is what we want when terrain covers the bottom half of the view. The clear
row is deliberately the `qwantani_*` series: one location photographed across a single
day, so the four times of day read as one place rather than four.

## Sun placement — why the presets rotate the sky

Poly Haven normalises every HDRI so the sun sits at the **same image longitude**
(measured: sun peak at px/w = 0.600 across all eight plates). Left alone, every sky would
light the course from an identical compass bearing, and Morning (20.2°) and Evening
(19.1°) would be visually indistinguishable.

Each preset therefore carries a `_Rotation` on its material that places the sun at a
bearing appropriate to its time of day — Morning east (100°), Noon south (175°),
Afternoon southwest (235°), Evening west (310°) — with the directional light aimed to
match.

Two measured facts underpin this, both verified against the project's own assets rather
than assumed:

1. **`_Rotation` moves the sky the opposite way.** Skybox/Cubemap rotates the *sampling*
   direction, so apparent sun bearing = `36.1 - _Rotation` (checked at 0/45/90/180/270,
   exactly linear). Any code applying a yaw offset must therefore **subtract** it from
   `_Rotation` while **adding** it to the sun's euler Y, or the two drift apart at double
   rate.
2. **Unity's lat-long → cubemap conversion applies a −90° yaw** relative to the standard
   equirectangular convention. A sun bearing derived from the raw `.hdr` must have 90°
   subtracted before it describes what Unity actually renders.

Sky/sun alignment is verified per preset by sweeping a probe camera for the rendered sun
disc and comparing to the light's bearing; all seven presets with a visible disc agree
within 2°. The overcast Morning plate has no disc (peak luminance 2 vs ~10⁵ for the
others), so its sun direction is plausible rather than matched.

Evening sits at 310° rather than due west because Hole 6 plays almost exactly west
(bearing ~270°); at 275° the sun was straight down the play line and blew out 18.5% of
the frame.

## Local modifications

Each file was downsampled 2048×1024 → 1024×512 by a 2×2 box average **in linear float**.

This was deliberate rather than using Unity's `maxTextureSize`, which does **not**
constrain the face size of a cubemap generated from a lat-long source — the face is
derived from the source width, so shrinking the source is the only lever. Result: 512px
faces, ASTC 6×6, ~0.9 MB each (~7.3 MB for all eight) instead of 1024px faces at ~3.6 MB
each.

Do **not** re-scale these with ffmpeg. Its Radiance path clamps to [0,1] and destroys the
sun disc (measured: sun-to-sky contrast collapsed 1702 → 2.9, derived sun elevation
shifted 5.6°). The box average preserves it: contrast 1702.3 → 1701.4, peak luminance
76 000 intact.

## Exposure

Exposures are solved, not authored. URP tonemapping makes output luminance a non-linear
function of `_Exposure`, so each preset is bisected until the median luminance of the
whole sky (sampled at four azimuths — a one-direction sample biases sunsets, which have a
bright and a dim side) hits a per-time-of-day target. A final guard lowers exposure until
less than 1% of the player's view clips to pure white.

## Sky-2.hdr (pre-existing)

`Sky-2.hdr` / `Sky-2.mat` predate this work and are the sky the game shipped with. Kept as
the **Classic** preset but **disabled in rotation**, so the original look can be restored
by ticking one checkbox without adding a ninth entry that breaks the time-of-day scheme.

> **Note:** `Sky-2.hdr` is 4096×2048 with mipmaps disabled and costs roughly 10–21 MB of
> runtime memory on its own (the editor profiler reports both figures depending on load
> state) — more than all eight new skies combined. Downsampling it the same way would
> recover most of that. Not done here because it changes the sharpness of the shipped
> look, which is Cesar's call.
