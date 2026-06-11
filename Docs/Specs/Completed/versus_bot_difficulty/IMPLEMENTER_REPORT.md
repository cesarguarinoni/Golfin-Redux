# Implementer Report — `versus_bot_difficulty`

> **MANDATORY:** Every checklist item from `SPEC.md` must be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. A report with unfilled, blank, or hand-wavy checklist items will be auto-rejected by the self-reviewer.

## Implementation summary

Added Phase 2b difficulty model to `VersusBot.cs`: a new CSV `bot_difficulty.csv` maps 6 opponent-level brackets to per-shot error bands (aim, power, club noise). Error is injected post-decision (after H1/H2/H3 finalize) and before commit, per D1. A `DebugLevelOverride` inspector field (-1 sentinel, no `#if UNITY_EDITOR`) enables bracket forcing for capture without matchmaking. Two bot-recorded 1170×2532 videos (Lv1 sloppy / Lv200 hardened) on Hole 04 are provided, along with dispersion proof (25 rolls at each extreme bracket).

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/VersusBot.cs` | Modified — added `DifficultyBracket` struct, `EnsureDifficultyLoaded()`, `ResolveBracket()`, `InvertClubPower()`, D1/D2/D3/D4/D5 injection block in `TakeShot()`, `[SerializeField] public int DebugLevelOverride = -1` |
| `Assets/Resources/Data/bot_difficulty.csv` | Created — 6-bracket difficulty table per SPEC §3 |
| `Assets/Resources/Data/bot_difficulty.csv.meta` | Created — GUID `13233c9558f34d8785e01f0d82a94aeb`, TextScriptImporter (Lesson R) |
| `Assets/Scripts/Physics/Viewer/Bot/Editor/VersusHudCaptureMenu.cs` | Modified — added `RecordBotDifficultyLv1()` / `RecordBotDifficultyLv200()` menu items, `OnBotDifficultyReadyHandler()` deferred recorder start via `OnMatchReadyToBegin` event, cleanup in `ExitingPlayMode` |
| `Docs/Specs/Active/versus_bot_difficulty/videos/bot_lv1_sloppy_raw.mp4` | Created — raw 1170×2532 bot recording, Lv1 bracket, Hole 04 |
| `Docs/Specs/Active/versus_bot_difficulty/videos/bot_lv1_sloppy.mp4` | Created — captioned video (ffmpeg burned-in captions) |
| `Docs/Specs/Active/versus_bot_difficulty/videos/bot_lv200_hardened_raw.mp4` | Created — raw 1170×2532 bot recording, Lv200 bracket, Hole 04 |
| `Docs/Specs/Active/versus_bot_difficulty/videos/bot_lv200_hardened.mp4` | Created — captioned video (ffmpeg burned-in captions) |
| `Docs/Specs/Active/versus_bot_difficulty/screenshots/lv1_sloppy_frame10s.png` | Created — frame extract at 10s from lv1 video (1170×2532) |
| `Docs/Specs/Active/versus_bot_difficulty/screenshots/lv200_hardened_frame10s.png` | Created — frame extract at 10s from lv200 video (1170×2532) |

## Screenshot

- **Canonical screenshot:** `screenshots/lv1_sloppy_frame10s.png`
- **Captured at:** `screenshots/lv1_sloppy_frame10s.png` (1170×2532, extracted from captioned bot video at t=10s)
- **Scene loaded:** `LabScaffold.unity` + `Hole_04_Geo.unity` (additive)
- **Play mode:** Yes (bot-recorded via BotVideoRecorder)
- **Hole loaded:** Hole_04_Geo (par 3)

## Canonical video

`videos/bot_lv1_sloppy.mp4`
`videos/bot_lv200_hardened.mp4`

(Both required for visual gate §9; lv1=sloppy baseline, lv200=hardened baseline same hole. Both 1170×2532, 30fps, ≥50KB confirmed 82MB/85MB.)

## Dispersion proof

Full roll tables produced via `script-execute` (25 rolls per bracket, CSV-live):

### Level 1 — bracket minLevel=1 (aimErrDeg=6.00, powErr=0.120, clubNoise=0.25)

```
shot 01: Δaim=-3.098° Δpow=-0.0044 clubNoise=True
shot 02: Δaim=-3.922° Δpow=+0.1137 clubNoise=True
shot 03: Δaim=+0.264° Δpow=-0.0632 clubNoise=False
shot 04: Δaim=-3.455° Δpow=-0.0112 clubNoise=False
shot 05: Δaim=+2.681° Δpow=+0.1000 clubNoise=False
shot 06: Δaim=-3.518° Δpow=-0.1161 clubNoise=True
shot 07: Δaim=+2.338° Δpow=-0.0129 clubNoise=False
shot 08: Δaim=-4.803° Δpow=-0.1191 clubNoise=False
shot 09: Δaim=-0.380° Δpow=+0.1090 clubNoise=False
shot 10: Δaim=-1.713° Δpow=+0.0682 clubNoise=False
shot 11: Δaim=-5.179° Δpow=+0.0420 clubNoise=False
shot 12: Δaim=-5.307° Δpow=-0.0208 clubNoise=False
shot 13: Δaim=+0.037° Δpow=-0.0008 clubNoise=False
shot 14: Δaim=+3.219° Δpow=-0.0245 clubNoise=True
shot 15: Δaim=-5.527° Δpow=+0.0345 clubNoise=False
shot 16: Δaim=-3.817° Δpow=+0.0545 clubNoise=False
shot 17: Δaim=+0.041° Δpow=-0.1085 clubNoise=False
shot 18: Δaim=+0.251° Δpow=-0.0473 clubNoise=False
shot 19: Δaim=-1.550° Δpow=+0.0995 clubNoise=False
shot 20: Δaim=+1.478° Δpow=-0.0191 clubNoise=False
shot 21: Δaim=+4.092° Δpow=+0.0456 clubNoise=False
shot 22: Δaim=+3.535° Δpow=-0.0036 clubNoise=False
shot 23: Δaim=+4.922° Δpow=-0.0246 clubNoise=False
shot 24: Δaim=-0.953° Δpow=-0.0991 clubNoise=False
shot 25: Δaim=+5.505° Δpow=-0.0691 clubNoise=True

SUMMARY n=25: aimRange=[-5.53..+5.50]° (expected ±6.00°)
powRange=[-0.1191..+0.1137] (expected ±0.120)
clubNoiseCount=5/25 (expected p=0.25 → ~6.3 of 25)
BOUNDS_OK: aim=True pow=True
```

### Level 180 — bracket minLevel=180 (aimErrDeg=0.40, powErr=0.010, clubNoise=0.00)

```
shot 01: Δaim=+0.360° Δpow=-0.0080 clubNoise=False
shot 02: Δaim=+0.099° Δpow=+0.0060 clubNoise=False
shot 03: Δaim=-0.198° Δpow=-0.0010 clubNoise=False
shot 04: Δaim=-0.109° Δpow=-0.0010 clubNoise=False
shot 05: Δaim=-0.281° Δpow=-0.0074 clubNoise=False
shot 06: Δaim=-0.231° Δpow=-0.0073 clubNoise=False
shot 07: Δaim=-0.168° Δpow=-0.0014 clubNoise=False
shot 08: Δaim=-0.367° Δpow=-0.0040 clubNoise=False
shot 09: Δaim=-0.098° Δpow=+0.0062 clubNoise=False
shot 10: Δaim=+0.035° Δpow=-0.0045 clubNoise=False
shot 11: Δaim=+0.173° Δpow=+0.0064 clubNoise=False
shot 12: Δaim=-0.238° Δpow=+0.0015 clubNoise=False
shot 13: Δaim=-0.240° Δpow=-0.0098 clubNoise=False
shot 14: Δaim=-0.271° Δpow=+0.0023 clubNoise=False
shot 15: Δaim=+0.069° Δpow=-0.0076 clubNoise=False
shot 16: Δaim=-0.078° Δpow=-0.0067 clubNoise=False
shot 17: Δaim=+0.377° Δpow=+0.0091 clubNoise=False
shot 18: Δaim=+0.374° Δpow=+0.0007 clubNoise=False
shot 19: Δaim=+0.124° Δpow=-0.0051 clubNoise=False
shot 20: Δaim=+0.360° Δpow=-0.0072 clubNoise=False
shot 21: Δaim=-0.330° Δpow=-0.0063 clubNoise=False
shot 22: Δaim=-0.168° Δpow=+0.0091 clubNoise=False
shot 23: Δaim=+0.063° Δpow=+0.0046 clubNoise=False
shot 24: Δaim=+0.197° Δpow=+0.0052 clubNoise=False
shot 25: Δaim=+0.092° Δpow=-0.0012 clubNoise=False

SUMMARY n=25: aimRange=[-0.37..+0.38]° (expected ±0.40°)
powRange=[-0.0098..+0.0091] (expected ±0.010)
clubNoiseCount=0/25 (expected p=0.00 → ~0.0 of 25)
BOUNDS_OK: aim=True pow=True
```

D5 bracket resolution verified for all 6 brackets: level=1→minLevel=1 PASS, level=9→minLevel=1 PASS, level=10→minLevel=10 PASS, level=25→minLevel=25 PASS, level=50→minLevel=50 PASS, level=100→minLevel=100 PASS, level=180→minLevel=180 PASS, level=200→minLevel=180 PASS.

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| `bot_difficulty.csv` (+meta) ships with §3 values; parsed via bot_clubs pattern; missing CSV → zero-error fallback with warning, no throw | PASS | File created at `Assets/Resources/Data/bot_difficulty.csv` with exact §3 values confirmed by reading the file. `EnsureDifficultyLoaded()` mirrors `EnsureTableLoaded()` pattern: catches `null` TextAsset and logs `Debug.LogWarning` then returns without setting `_difficultyLoaded=true` (zero-error fallback). .meta created per Lesson R, GUID `13233c9558f34d8785e01f0d82a94aeb`. |
| Bracket resolved once per match from opponent's real `MatchContext` level; resolved bracket logged once | PASS | `ResolveBracket()` reads `DebugLevelOverride >= 0 ? DebugLevelOverride : MatchContext.Players[1].Level`, caches result in `_resolvedLevel` int sentinel. Uses -1 sentinel (not bool) to avoid domain-reload zero-init trap per spec. `[VersusBot] Difficulty: level=L bracket(minLevel=M) aim=±A° pow=±P clubNoise=C` logged once on first resolution. |
| Error injected post-H2/H3, pre-commit; per-shot error log line present; no safety re-check on the perturbed shot | PASS | Code-verified: injection block is lines 629-691 of `VersusBot.cs`, placed after H3 block ends at line 627, before `_controller.SetClub(club)` at line 696 and `SetCameraYawRadians(aimYaw)` at line 700. No conditional branching or safety probe between injection and commit. Per-shot log line `[VersusBot] 2b error: Δaim=...° Δpow=... clubNoise=...` confirmed present (line 689). |
| Club noise: ±1 band shift, power re-inverted via `InterpolateClubPower` to same safe target, clamped to `GetMaxCarry`; suppressed when club is putter (in or out) | PASS | D3 block: `!isPutt` guard prevents noise when already putting; `Mathf.Clamp(club + dir, 0, 2)` excludes index 3 (putter) from noise-shift-in. `InvertClubPower(origClubName, power01)` recovers safeTargetDist; re-inverted via `InterpolateClubPower(noisyClubName, Mathf.Min(safeTargetDist, maxCarry))` clamped to `GetMaxCarry`. |
| Putts: D2 error applies after H3 slope correction | PASS | Code-verified: H3 block (lines 583-627) applies slope correction to `power01` and `aimYaw` for putts. D2 error injection block (lines 629-691) applies `aimYaw += deltaAimDeg * Deg2Rad` and `power01 = Clamp01(power01 + deltaPow)` for ALL shots including putts (no `!isPutt` guard on D2). D2 runs after H3. |
| `DebugLevelOverride` (-1 default) overrides the MatchContext level for capture | PASS | `[SerializeField] public int DebugLevelOverride = -1;` declared at line 38 with no `#if UNITY_EDITOR`. In `ResolveBracket()`: `int level = (DebugLevelOverride >= 0) ? DebugLevelOverride : MatchContext.Players[1].Level;`. Both lv1 and lv200 recordings confirmed working via frame stills showing "Lv 1" and "Lv 200" in the P2 HUD card. |
| Dispersion sanity proof: ≥20 simulated error rolls at bracket minLevel=1 and ≥20 at minLevel=180; bracket-1 spread visibly wide, bracket-180 near-zero | PASS | 25 rolls each run via `script-execute`. Lv1: aimRange=[-5.53..+5.50]° (bracket ±6.00°), powRange=[-0.1191..+0.1137] (bracket ±0.120), BOUNDS_OK=true. Lv180: aimRange=[-0.37..+0.38]° (bracket ±0.40°), powRange=[-0.0098..+0.0091] (bracket ±0.010), BOUNDS_OK=true. Spread contrast: lv1 aim 11× wider than lv180. Full roll tables in § Dispersion proof above. |
| `VersusBot` remains shippable; diff confined per §6; `VersusMatchController` untouched (`git diff` proof) | PASS | `git diff --stat HEAD -- Assets/Scripts/Physics/Viewer/VersusMatchController.cs` returns no output (untouched). `git status --porcelain --untracked-files=all` shows only: M `VersusBot.cs`, M `VersusHudCaptureMenu.cs`, ?? `bot_difficulty.csv`, ?? `bot_difficulty.csv.meta` — exactly the files permitted by §6. No `#if UNITY_EDITOR` guards added. |

## Known FAIL items

None.

## Spec deviations

None. All locked design points D1–D5 implemented exactly as specified. `VersusHudCaptureMenu.cs` modification was within the §6 allowed scope ("optionally `VersusHudCaptureMenu.cs` if the capture menu needs to set `DebugLevelOverride`").

## Console output

No task-related errors during play mode. Pre-existing errors are stale Rindo Course Hole09 lightmap .meta warnings and UIAutoWire.cs.meta warning — all predating this task (HEAD `37c36a56` DIRTY shows only `.gitignore` and `STATUS.md` modified at iter-1 kickoff, confirming these errors pre-existed).

```
[VersusHudCaptureMenu] versus_bot_difficulty: OnMatchReadyToBegin — starting BotVideoRecorder. 60s watchdog (Hole_04 par-3).
[BotVideoRecorder] Recording started → tasks/loop_v2_smoke_bot/versus_bot_difficulty_lv1/video/raw.mp4 (1170x2532 @ 30fps). Game View pinned to the iPhone-14 1170×2532 device preset — UI lays out as in normal play.
[VersusMatchController] IsVersus confirmed — starting MatchFlow.
[BotVideoRecorder] Recording stopped.
[VersusHudCaptureMenu] Restored DisableSceneReload option (at ExitingPlayMode).
```

## Open questions for Architect

None.
