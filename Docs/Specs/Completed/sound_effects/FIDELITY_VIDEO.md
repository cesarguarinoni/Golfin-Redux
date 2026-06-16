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

### v2 real-gameplay clips (2026-06-15) — the ones for Cesar's ear
- **`videos/audio_ui_and_music_slider.mp4`** (20s) — Home (music plays) → Settings → Sound; **Music slider dragged 70→5** (music quiets, SFX stays 70) → UI taps audible. Verifies the slider works + the music-quiet flow Cesar asked for. (mean −29.7dB / max −7.4dB, non-silent.)
- **`videos/audio_gameplay_shots_short.mp4`** (29.5s, 11MB; trimmed from the 89.5s `audio_gameplay_shots.mp4` to drop 47s of boot/nav dead air) — real Driver + Wedge shots on Hole 4 (par 3) via `BotDriver.FireShot`, chase camera; **swing + hit + landing** SFX matching the visible ball action, music quieted to 8%. (max −6.2dB, 13 sound events.)
- **Coverage still missing in real-gameplay video:** cup/ball-in drop, water splash, match win/lose stinger (these were in the v1 sound-board but not yet shown in-context). Pending Cesar's call on whether to capture a short par-3-to-cup + Hole-6 water + 1v1-result clip.
- **Capture tooling (committed by subagent, `15539d18`):** `BotVideoRecorder` `CaptureAudio`/`CustomOutputPath`/`MaxRecordSecondsSessionOverride`; `Scenarios.AudioGameplayShots`/`AudioUiMusicSlider`; dispatch + menu wiring. `AudioFidelityCapture.cs` (v1 sound-board tool) remains uncommitted architect drift.
- **GPU:** 2 full-res records this session (55s + 90s caps); Editor.log shows no AGX/WindowServer/kernel stress. Music PlayerPref restored 8→70 after the run.

### v2 Cesar feedback (2026-06-16) — "sounds ok, video not"
1. **Upside-down frame** — a transient flip (likely Recorder scene-init artifact). Fix: deferred recording start (after hole loaded + settled), trim first ~0.3s, post-record flip scan.
2. **Ball shot out of bounds** — prior `AudioGameplayShots` fired a Driver at full power on a par-3 → OOB. Fix: reuse `PlayHoleToCup` (aims at cup, power-appropriate, in-bounds) per Lesson "fix the shot, not the camera."
3. **One hit sound at club-ball contact** — DONE in code: `ShotController.PublishShotSfx()` now publishes a single `Hit*` at contact; the separate `Swing*` whoosh is removed (Swing SfxIds reserved/unpublished). 4 CommitFlick tests updated to assert one Hit / zero Swing; AudioEmitterTests 34/34 green.
4. **Capture the rest in video** — cup-in, water splash (Hole 6 Geo), 1v1 match stinger — properly recorded.

### v3 — hit-audibility fix + proper recording (2026-06-16)
**Correction:** the swing whoosh is KEPT (the earlier swing-removal experiment was reverted; `ShotController.PublishShotSfx` + 4 CommitFlick tests back to swing+hit, 34/34 green). Cesar's real issue = the **hit was inaudible (too quiet)**.
**Root cause (measured):** hit clips are 7–15 dB quieter (mean) than the swing and fire on the same frame → swing masks hit. Swing mean ≈−18 dB; Hit_Default mean −25.6 dB, Hit_Strong mean −31 dB, Hit_Weak −33 dB.
**Fix in `sfx.csv`:** Swing 1.0→0.55 (Default 0.85→0.45); Hit raised (HitDefault/HitStrong/HitBunker/HitBallIn=1.0, HitWeak/HitPutt=0.9) → the hit now sits ABOVE the swing.
**Clip `videos/audio_gameplay_v3_short.mp4`** (32s, 38.6 MB, trimmed from 45s `audio_gameplay_v3.mp4`): real Hole-4 tee shot at **mid power (~0.5)** → ball lands IN-BOUNDS on fairway, triggering **HitDefault** (punchy, now full volume). Mid-power fixes BOTH prior defects: in-bounds (full power flew OOB) + audible hit (mid power → HitDefault, not the quiet HitStrong). **Flip scan PASS** (deferred recording start avoided the Y-flip); orientation correct; GPU clean.
**Open:** full-power shots still use the inherently-quiet `Hit_Strong` clip — if Cesar wants strong hits punchier, boost that clip or remap it (his clip-choice call). Cup/water/match clips pending Cesar's OK on the hit balance.

### v4 — strong-hit + putt fixes (2026-06-16, Cesar round)
1. **Hit_Strong punchier** — re-encoded `Golfin_SFX - Hit_Strong.ogg` from its own loud `.wav` master: peak **−6.4 → −0.4 dB**. Full-power hits now punch. (orig backed up at `/tmp/sfx_orig/`.)
2. **Putt hit audible** — `Golfin_SFX - Hit_Putt.ogg` was very quiet (peak −10.6 dB); boosted **+9 dB → −1.6 dB**. The putter strike is now hearable over SwingPutt.
3. **No putt "ground thud"** — `BallAudioEmitter` now suppresses the per-roll land sounds AND the AtRest settle sound when `ShotController.IsPutt` (the `shot` arg `Configure()` already received was previously discarded). A putt rolling to a stop no longer thuds; a sunk putt still plays the cup sound. New test `Putt_AtRestAndRollHits_SuppressGroundSound`; AudioEmitterTests **35/35** green.
4. **Physics bugs (out of scope)** logged for the architect in `ARCHITECT_FOLLOWUP.md` (ball through Hole-4 fringe; ball fell through terrain into a Hole-4 bunker) — to fold into the DONE report.

### v5 — cup/water/match capture pass (2026-06-16)
- **`audio_water_splash_sfx.mp4`** (9s) — GOOD: Hole 6 real shot into the lake → `LandWater` splash SFX + VFX. Deliverable.
- **`audio_match_stinger.mp4`** (21s) — PARTIAL: real 1v1 (James vs Nori); MatchWin stinger fires in the AUDIO at ~17s, but the result MODAL never rendered (forced `MarkMatchComplete` skipped the modal flow). Stinger audible; win/lose visual missing.
- **`audio_putt_to_cup.mp4`** (22s) — INVALID: came out as a **Driver** shot at 70% on Hole 6, NOT a putt (subagent mislabeled). Does not demonstrate the putt fixes (audible HitPutt / no settle thud). Discard; redo as a real putt OR verify live.
- **Capture pollution reverted:** `M_SplashDroplet/Foam/Ring.mat` (`m_CustomRenderQueue` 3100→3000, an Order-349 VFX regression) and `NotoSansJP …SDF.asset` (TMP atlas 136KB→2.2MB) restored to HEAD.
- **GPU:** many full-res records this session, all clean (no AGX/WindowServer/kernel). Recommend relaunching Unity before any further capture batch (releases GPU/encoder state — the sanctioned reset).
