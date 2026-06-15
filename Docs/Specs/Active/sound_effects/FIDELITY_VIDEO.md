# Fidelity-gate artifact — `sound_effects` (Order 350)

Author: Architect (main Claude Code thread), 2026-06-15. Produced after Cesar chose
"I produce an audio video" to close the Tier-3 fidelity gate the implementer left open.

## What it is
A captioned **audio fidelity tour** recorded over a live ShellScene play session: every
`SfxId` is fired through the **real** `SfxBus → SfxPlayer → AudioManager → GolfinAudio.mixer`
chain, in sequence, with an on-screen label per sound, over the menu-music bed. This is the
clip Cesar plays and **hears** to judge clip choice + that every event produces audio.

- **Canonical video:** `videos/audio_fidelity_tour.mp4` (faststart) — raw source `videos/audio_tour_raw.mp4`
- **Tool:** `AudioFidelityCapture.cs` (new, `Golfin.Physics.Viewer.BotEditor`) — `GOLFIN ▸ Capture ▸ Record Audio Fidelity Tour`. Reusable for future audio work. Mirrors `BotVideoRecorder`'s GPU posture (full 1170×2532, 30fps real-time, render-loop capped) with the one difference that matters here: `MovieRecorderSettings.AudioInputSettings.PreserveAudio = true`.

## Verification (ffprobe / ffmpeg)
- Video stream: **h264, 1170×2532** (full iPhone-14), 42.04 s.
- Audio stream: **AAC, 48 kHz, stereo**, 42.03 s — **present**.
- `volumedetect`: **non-silent** — mean −18.8 dB, max −3.2 dB, 4,036,480 samples. (Peak ≈ the −3.1 dB mixer attenuation observed at runtime, confirming audio is routed through the new mixer.)
- Runtime probe (pre-record): `SfxBus.Play(HitStrong)` drove `SFXSource_0` (vol 1.00, group **SFX**); mixer `SFXVol`/`MusicVol` readable; PlayerPrefs→dB migration applied. 29/29 SfxIds resolve to non-null clips.

## What Cesar should listen for (clip-choice judgments)
These are the by-ear calls the automated gates can't make:
- **Placeholders** (flagged): `RpEarn`, `LevelUp` → `Hit_BallIn`. No bespoke clip exists; confirm or request new audio.
- **Match Lose / Draw** → `Clapping_02` (Win → `Clapping_01`). Clapping-on-loss may feel off; confirm.
- **SwingPutt** → `Swing_Default` (no bespoke putter swing).
- Swing/Hit/Landing all map 1:1 to the bespoke `Golfin_SFX` set — confirm levels + that each surface reads right.

## Known cosmetic note
On-screen caption separator `·` renders as a missing-glyph box in the legacy UI font (e.g. "Swing ▯ Putter"). Captions are fully legible; audio unaffected. One-character fix (`·`→`-`) on next record if Cesar wants it polished.

## Stills (supporting)
`screenshots/tour_t8.png` (Swing·Putter), `tour_t21.png` (Land·Road), `tour_t33.png` (RP Earned placeholder), plus t14/t27/t39.

## ⚠️ Cesar feedback on v1 (2026-06-15) — superseded by real-gameplay clips
The captioned sound-board (`audio_fidelity_tour.mp4`) does NOT let Cesar verify a sound MATCHES its action,
and the music bed buries the SFX. Redo: **real-gameplay clips with audio**, music started then **quieted via the
Settings music slider**, then the actual actions played out so each SFX is heard against its on-screen action.
Multiple short clips OK. Driven via the bot infra (`BotDriver.FireShot`/`FireDriverShot` + chase camera,
`BotDriver.SetSliderValue` for the music slider) with `PreserveAudio=true`. See `videos/` for the new clips.
