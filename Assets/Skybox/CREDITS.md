# Skybox assets — sources and licensing

## Poly Haven HDRIs (CC0 / public domain)

The four rotating skies were downloaded from [Poly Haven](https://polyhaven.com/hdris),
which publishes every asset under **CC0 1.0 Universal (public domain dedication)**.
CC0 imposes **no attribution requirement** and permits commercial use, modification and
redistribution. This file records provenance as good practice, not as a licence
obligation.

| File in repo | Poly Haven asset | Source resolution | Shipped as |
|---|---|---|---|
| `T_Sky_ClearMidday.hdr` | `kloofendal_43d_clear_puresky` | 2k | 1024×512 latlong → 512px cube faces |
| `T_Sky_PartlyCloudyNoon.hdr` | `kloppenheim_05_puresky` | 2k | 1024×512 latlong → 512px cube faces |
| `T_Sky_GoldenAfternoon.hdr` | `qwantani_late_afternoon_puresky` | 2k | 1024×512 latlong → 512px cube faces |
| `T_Sky_MistyMorning.hdr` | `kloofendal_misty_morning_puresky` | 2k | 1024×512 latlong → 512px cube faces |

All four are "pure sky" variants — the lower hemisphere is a clean sky/horizon rather
than photographed ground, which is what we want when terrain covers the bottom half of
the view anyway.

### Local modifications

Each file was downsampled 2048×1024 → 1024×512 by a 2×2 box average **in linear float**.

This was done deliberately rather than via Unity's `maxTextureSize`, because that setting
does not constrain the face size of a cubemap generated from a lat-long source — the face
is derived from the source width, so the only way to get 512px faces is to shrink the
source. Result: 28.67 MB → 3.63 MB of runtime cubemap memory for the four skies.

Do **not** re-scale these with ffmpeg. Its Radiance path clamps to [0,1] and destroys the
sun disc (measured: sun-to-sky contrast collapsed 1702 → 2.9, and the derived sun
elevation shifted by 5.6°). The box average preserves it: contrast 1702.3 → 1701.4, peak
luminance 76 000 intact.

## Sky-2.hdr (pre-existing)

`Sky-2.hdr` / `Sky-2.mat` predate this work and are the sky the game shipped with. Kept
in rotation as the **Classic** preset so the original look is never lost. Original source
and licence not recorded at the time it was added.

> **Note:** `Sky-2.hdr` is 4096×2048 with mipmaps disabled, which costs **21.4 MB** of
> runtime memory on its own — roughly 6× all four new skies combined. Downsampling it to
> 1024×512 the same way would recover ~20 MB. Not done here because it changes the
> sharpness of the shipped look, which is a call for Cesar to make.
