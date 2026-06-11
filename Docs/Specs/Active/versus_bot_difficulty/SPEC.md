# SPEC — `versus_bot_difficulty` (1v1 Phase 2b)

**Notion:** Order 346 (P2, Loop v2) · **Follows:** `versus_bot_hardening` (Order 345, shipped `4e700ae5`, in `Docs/Specs/Completed/versus_bot_hardening/`) · **Parent design:** `Docs/Specs/Completed/1v1_match_flow/SPEC.md` §13
**Tier:** FULL PIPELINE (randomized gameplay behavior on shipped code + video-gated)
**Prepared:** 2026-06-11 (Architect, Cesar-approved design 2026-06-11)

---

## 1. Why

The hardened `VersusBot` (345) now plays a competent, near-error-free hole on any of the 18 holes: calibrated club/power from `bot_clubs.csv`, water layup / fly-over / retarget, reactive OBReason recovery, and a green-slope read. That's the **ceiling**. Phase 2b adds the **difficulty model**: the opponent character's level maps to CSV error bands, and per-shot randomized error within those bands makes a low-level bot play like a sloppy human and a high-level bot play like the hardened baseline. The P2 card already shows the opponent's real level (Phase 1), so the displayed level finally means something.

---

## 2. Locked design (Cesar-approved 2026-06-11)

### D1 — Error injection is post-decision (execution error, never intent)

The bot's full decision pipeline runs UNCHANGED first — H1 club/power selection, H2 safety/layup/fly-over/retarget, H3 slope read — producing a final intended `(club, power01, aimYaw)`. Error is injected **after** that, immediately before the commit steps (camera yaw + drag ramp). **No safety re-check runs on the perturbed shot — it fires as-is.**

Rationale (locked, do not redesign): H2 is *intent*, the error band is *execution*. A low-level bot aims at the safe spot and misses into water — it never *aims* into water. Re-checking safety post-perturbation would correct big errors back and erase the difficulty model exactly where it matters; perturbing pre-H2 would make low-level bots play *safer* near hazards. The existing 2a drop/penalty flow handles the consequence of an errored shot finding water.

### D2 — Per-shot error rolls (from 1v1_match_flow SPEC §13)

Per shot, with band values from the bracket (see §3):

- `aimYaw += Random.Range(-aimErrorDegMax, +aimErrorDegMax) * Mathf.Deg2Rad`
- `power01 = Mathf.Clamp01(power01 + Random.Range(-powerErrorMax, +powerErrorMax))`
- With probability `clubNoiseChance`: club noise (D3).

Uniform distribution, `UnityEngine.Random` (same RNG the bot already uses for retarget bias). Rolls are independent per shot — no streaks, no state.

### D3 — Club noise preserves H2 intent by construction

On a club-noise hit: shift the selected club **±1 band** (random direction; clamp at ends — driver can only shift down, putter never shifts, see D4), then **re-invert `power01` for the new club to the SAME safe target distance** via the existing `InterpolateClubPower(clubName, targetDist)`, clamped to that club's `GetMaxCarry`. Result: club noise yields undershoots (club can't reach) or a different trajectory/roll-out — it can never overshoot past the safe target, so it can never blow through an H2 layup into the hazard the bot just avoided. The D2 aim/power error then applies ON TOP of the re-inverted power.

### D4 — Putts

- Aim/power error (D2) applies to putts too, injected **after** the H3 slope correction (the bot reads the break correctly, then executes imperfectly).
- **Club noise is suppressed on putts** (selected club == putter): no putter→wedge blade-overs on the green. Likewise no other club may noise INTO putter.
- No putt-specific error columns for now. If putt dispersion feels wrong in the demo, a `puttErrorScale` column is the designated follow-up knob — flag it, don't add it speculatively.

### D5 — Level source & bracket lookup

- Level = the opponent character's real level: `MatchContext.Players[1].Level` (P1 human is always index 0 per 2a; the bot occupies slot 1). NOTE: confirm the bot's slot index at implementation time rather than hardcoding a literal if a cleaner accessor exists.
- Bracket lookup: row with the **highest `minLevel` ≤ opponent level**. Levels range 1–240 (`Assets/Data/LevelUpCosts.csv`).
- Resolve the bracket **once at match start** (or first shot) and cache it — level doesn't change mid-match.

---

## 3. `bot_difficulty.csv` (new, CSV-first)

`Assets/Resources/Data/bot_difficulty.csv` (+ .meta, Lesson R). Same comment-tolerant parsing style as `bot_clubs.csv` (`EnsureTableLoaded` pattern).

```csv
# 1v1 bot difficulty bands — bracket = highest minLevel <= opponent character level.
# aimErrorDegMax: per-shot yaw error, uniform ±[0..max] degrees.
# powerErrorMax: per-shot power01 error, uniform ±[0..max].
# clubNoiseChance: per-shot probability of ±1 club-band shift (power re-inverted; suppressed on putts).
minLevel,aimErrorDegMax,powerErrorMax,clubNoiseChance
1,6.0,0.12,0.25
10,4.5,0.09,0.18
25,3.0,0.06,0.12
50,2.0,0.04,0.08
100,1.0,0.02,0.04
180,0.4,0.01,0.0
```

(Cesar-approved starting values. Feel reference: 6.0° ≈ 19m lateral spread at 180m; bracket `minLevel=10` covers the current matchmaking bots Camila Lv 13 / Taro Lv 17; `minLevel=180` is effectively the hardened baseline.)

---

## 4. Implementation sketch

All changes in `VersusBot.cs` (+ the new CSV). Mirror existing patterns; minimal diff.

1. **CSV load:** `EnsureDifficultyLoaded()` alongside the existing `EnsureTableLoaded()` — parse `bot_difficulty.csv` into an ordered bracket list. Missing/unparsable CSV → **zero-error fallback** (bot plays hardened baseline) + one `Debug.LogWarning`, never throw.
2. **Bracket resolve:** on first `TakeShot` of the match (or lazily with a cached `int _resolvedLevel = -1` sentinel — beware the Reload-Domain-Only zero-init trap; use a sentinel check, not a bool guard), read opponent level per D5, pick bracket, log it once: `[VersusBot] Difficulty: level=L bracket(minLevel=M) aim=±A° pow=±P clubNoise=C`.
3. **Injection point:** in `TakeShot()`, after the H3 block finalizes `(club, power01, aimYaw)` and **before** step 4 (`_controller.SetClub(club)`) / step 5 (`SetCameraYawRadians(aimYaw)`):
   - Roll club noise (D3) first — it re-derives `power01` via `InterpolateClubPower` for the noisy club at the same target distance (the target distance variable already in scope from the H1/H2 selection — the SAFE target, not raw pin distance, when a layup fired).
   - Then roll D2 aim/power error onto the (possibly re-derived) values.
   - One per-shot log line: `[VersusBot] 2b error: Δaim=+X.X° Δpow=+0.0XX clubNoise=wedge→iron7` (or `clubNoise=none`).
4. **Debug level override:** serialized inspector field `int DebugLevelOverride = -1` on `VersusBot` (-1 = off, use MatchContext). Needed for the visual gate (record bracket-1 vs bracket-6 matches without grinding matchmaking). Runtime-harmless; no `#if UNITY_EDITOR` needed for a plain field.

---

## 5. Code anchors (verified 2026-06-11)

| Need | Anchor |
|---|---|
| Decision pipeline + commit | `VersusBot.TakeShot()` — `Physics/Viewer/VersusBot.cs:330`; H2 sets `aimYaw = safeYaw` (~:447); H3 block adjusts aimYaw/power (~:484–510); commit = `SetClub` → `SetCameraYawRadians(aimYaw)` → drag ramp to `power01` |
| Club/power selection | `SelectShotCalibrated(float targetDist, out int club, out float power01, out string label)` — `VersusBot.cs:111`; bands >200m driver / 80–200 iron7 / 20–80 wedge / ≤20 putter |
| Power re-inversion (club noise) | `InterpolateClubPower(string clubName, float targetDist)` — `VersusBot.cs:168`; `GetMaxCarry(string clubName)` — `:160` |
| CSV parse pattern | `EnsureTableLoaded()` — `VersusBot.cs:69` (bot_clubs.csv) |
| Opponent level | `MatchContext.Players[1].Level` (`public int Level` — `Gameplay/UI/ShotUI/HUD/MatchContext.cs:18`); `Golfin.Physics.Viewer.asmdef` already references `Golfin.Gameplay.UI` |
| Reactive OB state (unchanged) | `VersusBot.LastOBReason` — `VersusBot.cs:36` |
| RNG precedent | `Random.value` retarget bias — `VersusBot.cs:354` |
| Level range | `Assets/Data/LevelUpCosts.csv` — levels 1–240 |

---

## 6. Constraints

- **No change** to the H1/H2/H3 decision logic itself — error wraps it, never re-orders or re-runs it. No safety re-check after perturbation (D1, locked).
- `VersusBot` stays shippable: no `#if UNITY_EDITOR`, no `ForceShotCompleteForBot`, production `ShotController` drag path — unchanged.
- All tunables CSV-first (`bot_difficulty.csv`). No magic numbers in code beyond the -1 sentinel/off values.
- No changes to `VersusMatchController`, resolution, HUD, RP bridge, solo play, or `bot_clubs.csv`.
- Diff confined to: `VersusBot.cs`, `bot_difficulty.csv` (+meta), optionally `VersusHudCaptureMenu.cs` if the capture menu needs to set `DebugLevelOverride`.

---

## 7. Out of scope

- Elevation compensation in the carry table (still the H1 flat-carry approximation; separate item).
- Putt-specific error columns (`puttErrorScale`) — designated follow-up knob only if demo dispersion warrants.
- Difficulty-driven *strategy* changes (aggression, layup preference by level) — error model only.
- Pacing/cadence tuning; 2c result modal; matchmaking opponent-level selection logic.

---

## 8. Acceptance checklist (implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] `bot_difficulty.csv` (+meta) ships with the §3 values; parsed via the bot_clubs pattern; missing CSV → zero-error fallback with warning, no throw.
- [ ] Bracket resolved once per match from the opponent's real `MatchContext` level; resolved bracket logged once.
- [ ] Error injected post-H2/H3, pre-commit; per-shot error log line present; **no safety re-check** on the perturbed shot (verify by code reading — the perturbed aimYaw/power01 flow straight to `SetCameraYawRadians`/drag ramp).
- [ ] Club noise: ±1 band shift, power re-inverted via `InterpolateClubPower` to the same safe target, clamped to `GetMaxCarry`; suppressed when club is putter (in or out).
- [ ] Putts: D2 error applies after H3 slope correction.
- [ ] `DebugLevelOverride` (-1 default) overrides the MatchContext level for capture.
- [ ] Dispersion sanity proof: for ONE fixed shot setup, log ≥20 simulated error rolls at bracket minLevel=1 and ≥20 at minLevel=180 (editor script-execute is fine); bracket-1 spread must be visibly wide, bracket-180 near-zero. Paste the two roll tables in the report.
- [ ] `VersusBot` remains shippable; diff confined per §6; `VersusMatchController` untouched (`git diff` proof).

---

## 9. Visual gate

Per `feedback_prefer_bot_videos` + full-size rule: **two bot-recorded videos at 1170×2532** on the SAME hole (pick a 345 coverage hole, e.g. Hole 04 or Hole 18):

- (a) `DebugLevelOverride = 1` (bracket minLevel=1) — visibly sloppy: wandering aim, inconsistent distances, occasional wrong club, more strokes.
- (b) `DebugLevelOverride = 200` (bracket minLevel=180) — plays like the hardened 345 baseline.

Same-hole pairing is the point: the difficulty delta must be readable side-by-side. Two-clip / natural-exit conventions from 344/345 apply.

---

## 10. Tier & kickoff

**FULL PIPELINE** — randomized behavior on shipped gameplay code; video-gated; dispersion must be proven, not eyeballed.

Kickoff (Cesar pastes into Claude Code):
```
Use the implementer subagent on "versus_bot_difficulty"
```
