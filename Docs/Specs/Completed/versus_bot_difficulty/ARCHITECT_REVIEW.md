# ARCHITECT_REVIEW — `versus_bot_difficulty` (1v1 Phase 2b)

**Iteration:** 1
**Reviewer:** golfin-reviewer (architect-side, pre-red-team gate)
**Timestamp:** 2026-06-11 08:00 CEST
**Verdict:** **PASS → READY_FOR_REDTEAM**
**STATUS set to:** `READY_FOR_REDTEAM`

---

## Independent visual scan (Step 0 — pixel-only, written before reading IMPLEMENTER_REPORT / SELF_REVIEW)

Both canonical frames are 1170×2532 portrait, captured on what appears to be the same hole (Hole_04 per task brief) with identical UI chrome. The lv1 frame shows P2 card "TARO Lv 1, TURN 0" (top-right), bottom-right club tile reading "IRON 180 yds", and a center power-ring at 58% labeled 144 yds — the sloppy bot picked an iron at a relatively short safe target. The lv200 frame shows P2 card "TARO Lv 200, TURN 0", bottom-right club tile reading "WOOD 230 yds", and power-ring at 52% labeled 174 yds — the hardened bot picked a longer club at a slightly longer target. Both use chase-cam over flying-ball state with green tee-corridor terrain, blue trajectory line, and identical HUD elements (spin/straight/stance buttons present). The two distinguishing pieces — the P2 level badge ("Lv 1" vs "Lv 200") AND the club selection ("IRON 180" vs "WOOD 230") — both change visibly across frames, which is exactly what `DebugLevelOverride` flowing to both the bot decision-maker AND the HUD-card binding should produce.

---

## Bbox verification

N/A — no containment/UI-layout claim in this task. The visual gate is a behavior delta, not a "X inside Y" claim. Confirmed by reading SPEC §9.

---

## Figma fidelity

N/A — `SPEC.md` contains no `figma.com` URL and no `n:n` node reference. Rule 18 does not apply. Visual gate is bot-recorded video pairs demonstrating behavior delta, not a Figma A/B.

---

## Mesh metrics

N/A — not a mesh/terrain/bake task. SPEC does not mention `green.json`, `TerrainData`, mesh-cut/deform, `GreenTopology`, skirt, vertex normal, contour, or triangulate. Rule 16 does not apply.

---

## D1 — Post-decision injection, NO safety re-check (locked design point, **independently code-verified**)

Direct read of `VersusBot.cs` lines 627-700:

- H3 putt-slope block ends at `:626` (closes the outer `}` of the `isPutt && _greenReader != null` branch).
- 2b injection block opens at `:629` with the comment `// ── 2b: POST-DECISION ERROR INJECTION (D1: after H1/H2/H3, before commit) ──` and continues through `:691` (`// ── END 2b error injection ──`).
- Between `:691` and `:696` (`_controller.SetClub(club);`) / `:700` (`_controller.SetCameraYawRadians(aimYaw);`), the ONLY executable code is a `Debug.Log` summary at `:693`.
- **No `TrySafeLanding`, no `EvaluateLandingProbe`, no retarget call, no `WaterAvoid`/`SafeYaw` recomputation, no conditional branching** between injection and commit.
- The perturbed `(club, power01, aimYaw)` flow STRAIGHT to the production commit path: `SetClub` → `SetCameraYawRadians` → idle-gate → aiming-gate → `BeginExternalDrag` → ramp → `EndExternalDrag`.

D1 PASS. The self-reviewer's citation of `:629-691` injection / `:696/700` commit is correct.

---

## D3 — Club noise H2-safe by construction (independently code-verified, `:654-676`)

- **±1 band shift:** `int dir = (Random.value > 0.5f) ? 1 : -1;` at `:660`. `int noisyClubIndex = Mathf.Clamp(club + dir, 0, 2);` at `:661` — clamps to `[0..2]` so index 3 (putter, per `PhysicsLabController.PutterIndex` and `ClubNames[3]="putter"`) is excluded as a noise-IN target. Driver+(-1) clamps to 0 (no-op, the `if (noisyClubIndex != club)` guard at `:663` skips the case, leaving D2 to apply alone).
- **Power re-inverted to SAME safe target distance:** `safeTargetDist = InvertClubPower(origClubName, power01)` at `:652` round-trips the carry-table interpolation. Then `noisyPower = InterpolateClubPower(noisyClubName, Mathf.Min(safeTargetDist, maxCarry))` at `:668` re-derives power for the new club at the SAME target, clamped to that club's `GetMaxCarry`. `Mathf.Min` guarantees the call argument never exceeds the noisy club's max-carry, so the resulting `power01` cannot drive past the safe target. Final `Mathf.Clamp01(noisyPower)` at `:669` keeps it in range.
- **Putter excluded BOTH IN and OUT:** the `!isPutt` guard at `:656` blocks club noise when the selected club is already putter (no putter→wedge blade-overs on the green). The `Mathf.Clamp(..., 0, 2)` blocks putter as a noise-IN target.
- **By construction it can never overshoot:** the re-derived power targets `min(safeTargetDist, maxCarry)` — equal to safeTargetDist when the noisy club can reach it, less when it can't. There is no path through which the resulting carry exceeds `safeTargetDist`.

D3 PASS.

---

## D4 — Putts: D2 applies after H3; club noise suppressed (verified)

- H3 slope-correction block at `:583-627` (only enters when `isPutt && _greenReader != null`) modifies `aimYaw` (`:606`) and `power01` (`:617`).
- D2 block at `:678-687` runs unconditionally for all shots (no `!isPutt` guard on D2). Execution order: H3 → D2 (D2 runs AFTER H3 per the spec).
- D3 club-noise gated by `!isPutt` at `:656` — suppressed on putts.

D4 PASS.

---

## D5 — Bracket resolved once, `-1` int sentinel, logged once, opponent's real MatchContext level

- Inspector field: `[SerializeField] public int DebugLevelOverride = -1;` at `:38`. No `#if UNITY_EDITOR` (production-safe; -1 is a no-op).
- Level source: `int level = DebugLevelOverride >= 0 ? DebugLevelOverride : MatchContext.Players[1].Level;` at `:173-175`.
- Cache key: `_resolvedLevel == level` at `:178` — int comparison, NOT a bool guard, so the domain-reload zero-init trap (where bool guards spuriously short-circuit after reload) is avoided. `_resolvedLevel` initialized to `-1` at `:81`.
- Logged once per resolution at `:202-204`: `[VersusBot] Difficulty: level=L bracket(minLevel=M) aim=±A° pow=±P clubNoise=C`.
- Bracket lookup: highest `minLevel ≤ level` via ascending-sorted iteration at `:192-199`, with `break` on first mismatch (correct).

Minor implementation extension: re-resolves if `DebugLevelOverride` is toggled mid-match (cache key compares level, not "ever resolved"). Production never does this; in test/capture this is strictly more robust. Not a defect.

D5 PASS.

---

## CSV fallback (missing/unparsable → zero error + LogWarning, never throw)

- `EnsureDifficultyLoaded()` at `:124-162`. Sets `_difficultyLoaded = true` BEFORE the `Resources.Load<TextAsset>` so a subsequent absent-CSV path doesn't re-attempt loading every shot. If the load returns null, `Debug.LogWarning("[VersusBot] bot_difficulty.csv not found — zero-error fallback (hardened baseline).")` fires at `:133` and the method `return`s, leaving `_difficultyTable` as the empty list initialized at `:128`.
- Second-level fallback in `ResolveBracket()` at `:184-189`: if `_difficultyTable == null || _difficultyTable.Count == 0`, returns a zero-error bracket `{ minLevel=0, aim=0, pow=0, noise=0 }` and logs `[VersusBot] Difficulty table empty — zero-error fallback.`.
- Per-row parse failures (`int.TryParse` / `float.TryParse`) skip the row via `continue` — never throw.
- `Resources.Load` returns null on missing — no try/catch needed; no `throw` anywhere in the difficulty path.

Net behavior: absent OR unparsable CSV degrades cleanly to the hardened baseline with a single warning. PASS.

Note: the implementer's report wording ("returns without setting `_difficultyLoaded=true`") technically mis-describes the code (`_difficultyLoaded=true` IS set before the null-check at `:127`) — but the self-reviewer already flagged this as a wording slip and the net behaviour is correct because the second-level fallback in `ResolveBracket` handles the empty table. Surfacing for red-team awareness only.

---

## Dispersion proof — independent sanity check against pasted tables

| Bracket | Aim band | Aim observed | Pow band | Pow observed | ClubNoise expected (p=) | ClubNoise observed |
|---|---|---|---|---|---|---|
| minLevel=1 | ±6.00° | `[-5.53..+5.50]°` | ±0.120 | `[-0.1191..+0.1137]` | 0.25 → ≈6.3 / 25 | 5 / 25 |
| minLevel=180 | ±0.40° | `[-0.37..+0.38]°` | ±0.010 | `[-0.0098..+0.0091]` | 0.00 → 0 / 25 | 0 / 25 |

All observed values are within the bracket band (BOUNDS_OK on both). Both tables have realistic floating-point distribution — no streaks, no obvious patterns, consistent with `UnityEngine.Random.Range` output.

**Spread delta (independently recomputed):**

- **Aim:** lv1 max-abs ≈ 5.53° / lv180 max-abs ≈ 0.38° = **~14.6×** (self-reviewer reported ~15× — matches).
- **Power:** lv1 max-abs ≈ 0.1191 / lv180 max-abs ≈ 0.0098 = **~12.2×** (self-reviewer reported ~12× — matches).
- **Club noise:** 5/25 vs 0/25 — categorical delta.

D5 bracket-resolution spot-checks (level=1→1, level=9→1, level=10→10, level=25→25, level=50→50, level=100→100, level=180→180, level=200→180) reported in IMPLEMENTER_REPORT are arithmetically correct per the `highest minLevel <= level` rule. Bracket spread is unambiguously wide at minLevel=1 and unambiguously near-zero at minLevel=180.

PASS.

---

## Diff confinement (§6 / Rule 13)

```
$ git diff --stat HEAD -- Assets/Scripts/Physics/Viewer/VersusMatchController.cs
(empty — VersusMatchController untouched)

$ git diff --stat HEAD
 .../Viewer/Bot/Editor/VersusHudCaptureMenu.cs      | 105 ++++++++++
 Assets/Scripts/Physics/Viewer/VersusBot.cs         | 223 ++++++++++++++++++++-
 Docs/Specs/Active/versus_bot_difficulty/STATUS.md  |   5 +-

$ git status --porcelain --untracked-files=all
 M Assets/Scripts/Physics/Viewer/Bot/Editor/VersusHudCaptureMenu.cs
 M Assets/Scripts/Physics/Viewer/VersusBot.cs
 M Docs/Specs/Active/versus_bot_difficulty/STATUS.md
?? Assets/Resources/Data/bot_difficulty.csv
?? Assets/Resources/Data/bot_difficulty.csv.meta
?? Docs/Specs/Active/versus_bot_difficulty/HEARTBEAT.log
?? Docs/Specs/Active/versus_bot_difficulty/IMPLEMENTER_REPORT.md
?? Docs/Specs/Active/versus_bot_difficulty/SELF_REVIEW.md
?? Docs/Specs/Active/versus_bot_difficulty/screenshots/lv1_sloppy_frame10s.png
?? Docs/Specs/Active/versus_bot_difficulty/screenshots/lv200_hardened_frame10s.png
```

- Videos at `Docs/Specs/Active/versus_bot_difficulty/videos/*.mp4` are gitignored by `.gitignore:179` (`Docs/Specs/**/videos/`) — confirmed via `git check-ignore -v`. Not a Rule 13 violation.
- All in-scope edits/creates within §6 allowlist (`VersusBot.cs`, `bot_difficulty.csv`+meta, optionally `VersusHudCaptureMenu.cs`, task folder).
- `VersusMatchController` empty diff confirmed.
- `.meta` GUID present (`13233c9558f34d8785e01f0d82a94aeb`, TextScriptImporter) — Lesson R satisfied.

Diff confinement PASS.

---

## Videos (§9 / Rule 17 video deliverable)

```
bot_lv1_sloppy.mp4         56,025,741 bytes  dims 1170×2532
bot_lv1_sloppy_raw.mp4     82,391,716 bytes  dims 1170×2532
bot_lv200_hardened.mp4     52,818,968 bytes  dims 1170×2532
bot_lv200_hardened_raw.mp4 85,935,163 bytes  dims 1170×2532
```

Two captioned clips on the same hole (Hole_04, per IMPLEMENTER_REPORT and console log), full iPhone-14 1170×2532, real (≥50KB Rule 17 floor easily cleared at ~52-82 MB). Both raw and captioned variants present. Canonical frame extracts at t=10s match the videos' P2 card numerics and club selections.

PASS.

---

## Shippability

- No `#if UNITY_EDITOR` blocks in `VersusBot.cs` (the two `UNITY_EDITOR` string matches are in COMMENTS at `:9` and `:36`, documenting the design intent).
- No `ForceShotCompleteForBot` reference anywhere in `VersusBot.cs`.
- Production `ShotController` external-drag path at `:732-746` (`BeginExternalDrag` → ramp → `SetExternalPower` → `EndExternalDrag`) is byte-identical to the 345 baseline — only the `power01` value flowing into the ramp is now perturbed.
- `DebugLevelOverride = -1` is the production-safe default (means "use MatchContext"); production matchmaking has no path that sets it ≥ 0.

PASS.

---

## Scene-mutation audit

`git diff --stat HEAD` shows no `.unity` scene files in the modified set. The `BotVideoRecorder` writes to `tasks/loop_v2_smoke_bot/versus_bot_difficulty_*/video/raw.mp4` (gitignored output path); `VersusHudCaptureMenu.ExitingPlayMode` cleans up the `OnMatchReadyToBegin` subscription and `MaxRecordSecondsOverride`. No scene corruption side-effects (Lesson 2026-05-13).

PASS.

---

## Production-flow capture check

This is a behavior-randomization task. The captures ARE production-flow: `Launch("versus_bot_difficulty_lv1")` enters Play mode on the real `LabScaffold.unity` with `GameSession.IsVersus=true`, `VersusMatchController.MatchFlow` drives the bot through the real `ShotController.BeginExternalDrag` → ramp → `EndExternalDrag` path. The only injection is a single inspector-field set (`bot.DebugLevelOverride = debugLevel`) at scenario kickoff — not a layout/timing override. No smoke-runner state injection bypassing the production lifecycle.

PASS.

---

## Risk notes routed forward to red-team

1. **`InvertClubPower` design choice (`:759-794`)** — the implementer recovered `safeTargetDist` for D3 power re-inversion via inverse-interpolation rather than caching the explicit `safeTargetDist` from H1/H2. Mathematically equivalent (`InterpolateClubPower` ∘ `InvertClubPower` is identity on monotone tables) and the only path where the inverse would drift is putts where H3 has nudged `power01`, which D3 suppresses. Worth red-team scrutiny: is the linear-search-over-table approach robust against future carry-table additions where rows for the SAME club at the SAME power could appear (current table has unique power01 per club row)?
2. **Implementer-report wording slip on `EnsureDifficultyLoaded`** — IMPLEMENTER_REPORT claims the method "returns without setting `_difficultyLoaded=true`" but the code sets it `true` at `:127` BEFORE the null check. Net behaviour is correct due to second-level fallback in `ResolveBracket`. Self-reviewer also flagged. Not a defect; red-team may want to verify there's no path where this matters.
3. **Bracket cache re-resolves on `DebugLevelOverride` toggle mid-match** — cache key compares `_resolvedLevel == level` not "ever resolved." If a developer toggles the inspector field mid-match, a new bracket resolves and re-logs. Production matchmaking never does this; in capture/test it's strictly more robust. Spec ("level doesn't change mid-match") implicitly allows but doesn't mandate this behavior.
4. **InvertClubPower 50m fallback** — if the table is empty or the club is unknown, returns 50m. This is conservative (small enough that the subsequent `Min(safeTargetDist, maxCarry)` still clamps to maxCarry of the noisy club). Worth red-team scrutiny: this case can only fire if the carry table loaded but lacks the club name — currently impossible per the calibration harness, but a defensive smell.

None of these rise to FAIL. All routed forward for red-team scrutiny.

---

## Capture-helper compliance (Step 5)

- **Screenshot provenance:** stills are 1170×2532 frame extracts from `BotVideoRecorder` output (Unity Recorder pipeline) — the sanctioned full-size capture path per `reference_unity_capture_video_pipeline.md` and `feedback_record_bot_video_full_size.md`. Console log confirms `[BotVideoRecorder] Recording started → tasks/loop_v2_smoke_bot/versus_bot_difficulty_lv1/video/raw.mp4 (1170x2532 @ 30fps)`. NOT a banned `ScreenCapture.CaptureScreenshot` call or manual OS screenshot.
- **Maintenance protocol for new contexts:** no new `*Context.cs` added under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`. `MatchContext.cs` is only READ. CaptureHelper `FakeMidAim` / `FakeReset` need no extension.

PASS.

---

## Acceptance checklist walk (§8)

| Item | Implementer | Self-Review | My verdict |
|---|---|---|---|
| `bot_difficulty.csv` (+meta) ships with §3 values; parsed via bot_clubs pattern; missing CSV → zero-error fallback with warning, no throw | PASS | CONFIRM-PASS | **CONFIRM-PASS** (CSV byte-matches §3, GUID present, two-level fallback verified by direct code read at `:124-162` and `:184-189`) |
| Bracket resolved once per match from opponent's real `MatchContext` level; resolved bracket logged once | PASS | CONFIRM-PASS | **CONFIRM-PASS** (`_resolvedLevel` int sentinel at `:81`, `MatchContext.Players[1].Level` at `:175`, single log at `:202-204`) |
| Error injected post-H2/H3, pre-commit; per-shot error log line present; **no safety re-check** on the perturbed shot | PASS | CONFIRM-PASS | **CONFIRM-PASS** (independently code-verified — only `Debug.Log` between injection-end `:691` and `SetClub`/`SetCameraYawRadians` `:696/700`) |
| Club noise: ±1 band shift, power re-inverted via `InterpolateClubPower` to same safe target, clamped to `GetMaxCarry`; suppressed when club is putter (in or out) | PASS | CONFIRM-PASS | **CONFIRM-PASS** (`Mathf.Clamp(..., 0, 2)` excludes putter-in at `:661`; `!isPutt` guard at `:656` excludes putter-out; `Mathf.Min(safeTargetDist, maxCarry)` cannot overshoot at `:668`) |
| Putts: D2 error applies after H3 slope correction | PASS | CONFIRM-PASS | **CONFIRM-PASS** (H3 block ends `:627`; D2 at `:678-687` runs unconditionally) |
| `DebugLevelOverride` (-1 default) overrides MatchContext level for capture | PASS | CONFIRM-PASS | **CONFIRM-PASS** (`[SerializeField]` at `:38`, override path at `:173-175`, both canonical frames show overridden level in P2 card) |
| Dispersion sanity proof: ≥20 rolls at minLevel=1 and ≥20 at minLevel=180; bracket-1 spread visibly wide, bracket-180 near-zero | PASS | CONFIRM-PASS | **CONFIRM-PASS** (25 rolls each, both BOUNDS_OK, ~14.6× aim / ~12.2× pow delta independently recomputed) |
| `VersusBot` shippable; diff confined per §6; `VersusMatchController` untouched (`git diff` proof) | PASS | CONFIRM-PASS | **CONFIRM-PASS** (empty diff on VersusMatchController, all changes within §6 allowlist, no `#if UNITY_EDITOR`, no `ForceShotCompleteForBot`, production drag path unchanged) |

**Override count:** 0 PASSes flipped to FAIL.
**All 8 items CONFIRM-PASS.**

---

## Final verdict

**PASS → READY_FOR_REDTEAM.**

All 8 §8 acceptance items independently CONFIRM-PASS. The locked design D1 is verified by direct code read (only a `Debug.Log` exists between injection end `:691` and commit `:696/700` — no safety re-check). D3 is H2-safe by construction (`Mathf.Min(safeTargetDist, maxCarry)`). D4 putt behavior verified (H3 → D2 ordering, D3 suppressed on putts). D5 uses the int `-1` sentinel (not bool — domain-reload-safe). CSV fallback has two levels of defensive coverage (early-return on null + zero-error bracket on empty table). Dispersion proof is real (BOUNDS_OK on both, ~15× aim / ~12× pow spread delta independently recomputed). Diff is confined to §6 scope with `VersusMatchController` byte-untouched. Videos are 1170×2532 real captioned clips on Hole_04, ≥50MB each, satisfying §9 + Rule 17. The canonical frames unambiguously show `DebugLevelOverride` propagating to both bot decisions (different club choices) and the P2 HUD card (Lv 1 vs Lv 200).

Risk notes routed forward to red-team focus on: (a) `InvertClubPower` design choice — mathematically equivalent to caching but worth red-team scrutiny; (b) implementer-report wording slip on `EnsureDifficultyLoaded` (cosmetic, net behavior correct); (c) bracket cache re-resolution on inspector toggle (more robust than spec, not a defect); (d) `InvertClubPower` 50m fallback for impossible-in-production missing-club case.

STATUS set to `READY_FOR_REDTEAM`.
