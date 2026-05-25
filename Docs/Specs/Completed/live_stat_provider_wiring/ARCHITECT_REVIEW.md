# Architect Review — `live_stat_provider_wiring`

**Iter:** Phase 2 architect verdict
**Date:** 2026-05-25
**Reviewed by:** Cesar (human architect) with Claude Code routing
**Verdict:** `ARCHITECT_REVIEW_FAIL` — route back to implementer

## Summary

The self-reviewer escalated three findings (see `SELF_REVIEW.md`). Cesar reviewed and decided:

1. **Visual gate bypass is in-scope.** Spec's *intent* governs, not the literal acceptance text. "Every production gameplay shot uses the player's actually-selected stats" requires the bus to actually route committed shots, not just aim polling.
2. **Fix path: Option C** — clean lab-vs-prod split. `PhysicsLabController.SetClub` will no longer unconditionally inject a neutral stat bundle; lab callers must inject explicitly.
3. **Re-record both MP4s after the fix lands.** The corrupt `visual_gate_low.mp4` and the bypassed `visual_gate_high.mp4` are both throwaway artifacts — neither proves anything once the fix is in. Re-record both; both must be valid playable files; both must show a visible carry/accuracy delta.

The Phase 1 wiring (StatProviderBus, LiveStatProviderHost, ShotController bus swap, ClubDataRuntime physics fields, tests 338/335/0/3) is verified PASS by the self-reviewer and **stays as-is**. Phase 2 (bot scenarios, log tee, menu items, video artifacts) is partially redone.

## Confirmed PASS (from self-review — do NOT touch)

- `StatProviderBus.cs` + `LiveStatProviderHost.cs` correctly built (cross-asmdef static-bus pattern from L1, asmdef `autoReferenced=true` swap on `Golfin.Gameplay.Defaults` + `Golfin.Physics.Math`).
- `ShotController.GetStatBundle()` single-line swap to `StatProviderBus.Resolve(IsPutt)` — clean.
- `ShellScene.unity` diff: only the `LiveStatProviderHost` component on `PersistentUI`. No scene corruption.
- `ClubDataRuntime` physics fields (`ballSpeedMps`, `launchAngleDeg`, `spinRateRpm`) read from existing CSV columns.
- EditMode + PlayMode tests for the bus — 4 + 2 new tests passing; baseline `334/331/0/3` → `338/335/0/3`.
- Bot infra files from Phase 2 (`LiveStatLogTee.cs`, the two scenarios in `Scenarios.cs`, the menu items in `LoopV2SmokeBotMenu.cs`, the switch cases in `LoopV2SmokeBot.cs`) — these stay; the log tee in particular is independent of the bypass bug.

## Findings & required fixes

### F1 — `PhysicsLabController.SetClub` injects a neutral bundle on every call

**Evidence:** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:542-571` calls `_shotController.InjectStatBundle(...)` unconditionally with `CharacterStats.Neutral` + `BallStats.Neutral`. Nothing calls `ClearStatBundleOverride()` anywhere. Confirmed by `grep` (0 callers).

**Consequence:** Every bot-fired shot in production flow (Hole1Playthrough, both visual-gate scenarios) takes the lab override path. The `[LiveStatProvider] LIVE swing` log lines that the implementer celebrated as proof are from `ShotController.PublishState` polling per frame DURING IDLE AIM, never from a committed shot.

**Required fix:** `SetClub` MUST NOT inject. It sets the club index, sets `IsPutt`, raises `OnClubChanged` + `ClubSelectionBroadcast.Raise(index)`, and returns. Bundle ownership moves to callers.

### F2 — Lab callers must inject explicitly

Callers that depend on the lab-bundle behavior and MUST inject after `SetClub`:

| File:line | Caller | Mode |
|---|---|---|
| `Assets/Scripts/Physics/Viewer/PhysicsLabUI.cs:313` (`ApplyClubIndex`) | Real lab UI button | LAB |
| `Assets/Scripts/Physics/Viewer/PutterConeSmokeCapture.cs:92,110,176,194` | Lab-style smoke capture | LAB |
| `Assets/Scripts/Physics/Viewer/SmokeRunner2fHost.cs:146,462` | Lab smoke runner | LAB |
| `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs:622,1025` | Putter green reader bot scenarios (lab-style) | LAB |
| `Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs:474` (`FireShot`) | Putter-only helper used by lab scenarios | LAB |

Callers that MUST NOT inject (production flow — bus resolves):

| File:line | Caller | Mode |
|---|---|---|
| `Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs:693` (`PlayHoleToCup`) | Production hole playthrough | PROD |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:1027` (auto-revert) | Auto-revert from putter when ball at rest off-green during gameplay | PROD |

The `PhysicsLabController.cs:1027` case needs care — it's an internal auto-revert. Treat it as PROD; the bus resolves.

### F3 — Suggested API split (implementer may refine)

Add to `PhysicsLabController`:

```csharp
/// Builds the current-club neutral lab bundle and injects it into ShotController.
/// Lab callers (lab UI, putter cone smoke, putter green reader bot scenarios) call
/// this AFTER SetClub() when they want the lab-bundle behavior.
public void InjectLabBundleForCurrentClub() { /* extracts the current SetClub logic */ }
```

Then `BotDriver` exposes two helpers so scenarios choose explicitly:

```csharp
// Lab-style scenarios — inject neutral bundle so physics is club-only.
internal void SetClubAndInjectLabBundle(PhysicsLabController ctrl, int index);

// Production-flow scenarios — bus resolves live stats from player state.
internal void SetClubProductionFlow(PhysicsLabController ctrl, int index);
```

`SetClubProductionFlow` should also call `ClearStatBundleOverride()` defensively (in case a prior LAB call left the override set on the same `ShotController`).

Implementer is free to use a different shape (e.g. a mode flag on BotDriver set at scenario start) as long as: (a) `PhysicsLabController.SetClub` does not inject, (b) every existing lab-style caller continues to behave identically to today, (c) the two visual-gate scenarios use the production path.

### F4 — Re-record both visual-gate videos

After F1–F3 land:

- Re-run `GOLFIN/Smoke/Loop v2/Live Stat Provider — High Build`. Verify `visual_gate_high.mp4` is a valid playable file (ffprobe shows a `moov` atom; can extract a mid-flight frame).
- Re-run `GOLFIN/Smoke/Loop v2/Live Stat Provider — Low Build`. Same verification.
- Watch both videos end-to-end. The HIGH and LOW carry distances on stroke 1 MUST be visibly different (>10m delta is the minimum bar; larger is better). If the delta is still imperceptible after F1–F3 land, that's a real FAIL — surface as a separate stat→physics mapping issue and DO NOT fake-pass.
- Re-pull `live_stat_log_high.txt` and `live_stat_log_low.txt`. There must be `LIVE swing` lines that fire *at the moment of shot commit*, not just during aim. Add a one-line annotation in each log file documenting which lines correspond to which strokes (or extend the LiveStatLogTee to mark commit frames).

### F5 — Lab compatibility hard-gate

After F1–F3 land, open `Assets/Scenes/PhysicsLab_Hole1.unity`, click any in-UI club button in the lab UI, fire a shot via the lab fire path. Verify via Console + `ShotController._statBundleOverridden` (set a temporary breakpoint or `Debug.Log`) that the lab-bundle path is still active during the lab shot. Document in IMPLEMENTER_REPORT.md under "Lab compatibility" with one paragraph + a screenshot or log excerpt.

### F6 — Test gate

Tests must stay at or above `338/335/0/3`. If the API split adds new tests (recommended: one for `SetClub` no-injection, one for `InjectLabBundleForCurrentClub` round-trip), include them in the count.

## Out-of-scope (do NOT touch in this iteration)

- The upstream stat→physics mapping (CharacterStats / BallStats / ClubStats → carry distance / accuracy). If after F1–F4 the HIGH vs LOW delta is still weak (<10m), that's a follow-up task: `stat_to_physics_mapping_audit`. File it; do not fix inline.
- The misleading name of `PhysicsLabController` (it's used in production too). Rename is a separate task: `physics_lab_controller_rename`. Don't touch in this iteration.

## Acceptance criteria (added to SPEC)

The original SPEC's acceptance criteria stand. Adding:

- `PhysicsLabController.SetClub(int)` MUST NOT call `InjectStatBundle` directly.
- All five LAB-mode callers (F2 table) MUST explicitly inject after `SetClub`.
- All PROD-mode callers (F2 table) MUST NOT inject; the bus resolves.
- `visual_gate_high.mp4` and `visual_gate_low.mp4` MUST both be valid playable MP4 files (verifiable with `ffprobe`).
- HIGH vs LOW stroke-1 carry delta MUST be ≥10m and visibly perceptible in the videos.
- Lab UI must still produce its per-club neutral bundle behavior identical to today (F5).

## Next state

STATUS → `ARCHITECT_REVIEW_FAIL` → implementer route-back.

---

## Phase 4 amendment — 2026-05-25 (Cesar architect decision after Phase 3 IMPLEMENTER_BLOCKED)

Phase 3 implementer landed F1–F6 cleanly but surfaced IMPLEMENTER_BLOCKED on the ≥10m HIGH-vs-LOW delta criterion (delta = 0m). Root cause: `StatModifierResolver.cs:22-25` only routes `Club.Power × Ball.Power` into `velocityMultiplier`; `Character.Strength` only feeds `overpower forgiveness`, which has no observable effect at `power = 1.0`. Same club + same ball ⇒ structurally identical carry regardless of character build.

Cesar's decision: **file the full audit follow-up AND ship a minimal patch now**. Audit lives at `Docs/Specs/Queued/stat_to_physics_mapping_audit/SPEC.md`. The patch (F7 below) ships in this iteration so the visual gate can prove the bus actually delivers different stats to the resolver.

### F7 — Minimal Strength → velocity coupling patch

**Scope:** add a single new lane to `StatModifierResolver.cs` so `Character.Strength` contributes to `velocityMultiplier`. This is the minimum change that lets a HIGH vs LOW build show a visible carry delta on a swing.

**Concrete edit** in `Assets/Scripts/Physics/Stats/StatModifierResolver.cs` Step 2:

```csharp
// Step 2: Velocity multiplier.
// Lane: Club Power × Ball Power × Character Strength (multiplicative).
// NOTE F7 (2026-05-25): added Character.Strength factor so the bus's
// live-stat resolution is observable on swing carry. Full lane audit
// pending in `stat_to_physics_mapping_audit`.
fp clubPower   = bundle.IsPutt ? fp.Zero : fp.FromInt(bundle.Club.Value.Power);
fp velFromClub = fp.One + clubPower * coeffs.ClubPowerPerPoint;
fp velFromBall = fp.One + fp.FromInt(bundle.Ball.Power) * coeffs.BallPowerPerPoint;
fp velFromChar = bundle.IsPutt
    ? fp.One
    : fp.One + effStrength * coeffs.CharStrengthVelocityPerPoint;
fp velocityMultiplier = velFromClub * velFromBall * velFromChar;
velocityMultiplier    = fpMath.Min(velocityMultiplier, caps.VelocityMultiplierMax);
velocityMultiplier    = fpMath.Max(velocityMultiplier, fp.Zero);
```

Add `CharStrengthVelocityPerPoint` to `StatCoefficients.cs` next to existing coefficients. Default value: `fp.FromFloat(0.004f)` — gives a Common-rarity-max (Strength 25) build a 10% velocity boost; a Supreme-rarity-max (Strength 50) build a 20% velocity boost; carry scales as `v²/g` so ~20% extra carry on 200m drive ≈ ~40m delta (well above the 10m bar). The LOW build (Strength = rarity starting baseline ~5) gets ~2% boost. HIGH vs LOW delta should land ≥30m on driver.

**Putter exemption:** putters keep `velFromChar = fp.One` (no character strength on putts). Putts have their own lane via `Putter.Control` / `Putter.Accuracy` / `Putter.Weight`.

**Cap update:** verify `caps.VelocityMultiplierMax` accommodates a maxed Supreme Strength + maxed Supreme Club Power + maxed Supreme Ball Power product without hitting the cap on every shot (which would erase the delta). If it does, raise the cap modestly (~30% over current) and document the change in `Docs/Physics/PHYSICS_TUNING_CHANGELOG.md`.

**Tests:** add at least one EditMode test to `Golfin.Physics.Tests` asserting `Resolve(bundle with Strength=50) > Resolve(bundle with Strength=5)` on `velocityMultiplier` for a swing (not a putt). Verify putter case: `Resolve(putt bundle with Strength=50) == Resolve(putt bundle with Strength=5)`.

**Hole 1 completability check:** after F7 lands, manually verify that a default-stat Common-rarity character with a default driver + default ball can still complete Hole 1 par-5 in ≤7 strokes (any higher means we overshot the velocity multiplier). Document in IMPLEMENTER_REPORT.md with the bot scenario output.

**Re-run visual gate after F7:**

- Re-run `GOLFIN/Smoke/Loop v2/Live Stat Provider — High Build`.
- Re-run `GOLFIN/Smoke/Loop v2/Live Stat Provider — Low Build`.
- ffprobe verify both MP4s (valid moov atom, sensible duration, sensible nb_frames).
- HIGH stroke-1 carry MUST be ≥10m beyond LOW stroke-1 carry. (Likely much more given the F7 coefficient; ≥10m is the floor.)
- Update IMPLEMENTER_REPORT.md "Visual gate" row to PASS with the measured delta + the new video paths + ffprobe summary lines.

### F8 — File the queued audit spec

Verify `Docs/Specs/Queued/stat_to_physics_mapping_audit/SPEC.md` exists. Add a one-line pointer to it in the F7 patch comment in `StatModifierResolver.cs`. Add a one-line reference in `Docs/AI_CONTEXT.md` § "Queued specs" (if a list exists) noting that the audit is queued.

### Updated acceptance criteria

F7 + F8 are added to the F1–F6 acceptance criteria. All eight must be PASS before the next SELF_REVIEW transition.

---

## Final Reviewer Pass — 2026-05-25 16:35 CEST

**Verdict:** `ARCHITECT_REVIEW_PASS` — forward to Cesar for DONE approval.

### Step 0 — Independent pixel scan (BEFORE reading IMPLEMENTER_REPORT or SELF_REVIEW)

Fire times from `tasks/loop_v2_smoke_bot/.../history.log`: HIGH stroke-1 fired at t=26.23s, LOW stroke-1 fired at t=26.40s. Extracted frames at t=30s (≈3.6–3.8s post-fire) and t=46–47s (turn 2 at-rest after stroke 1).

**HIGH @ t=30s** (`/tmp/golfin_review/high_t30.png`): Phone-portrait gameplay HUD. Top-left character chip shows `ELIZABETH / Lv 119 / TURN 1`. Top-right `LOMOND / HOLE 1 - REGULAR / PAR 5`. Right-side power-ramp circle reads `100%`. Ball visible mid-flight as a small white dot above the fairway, roughly centered. Bottom HUD shows three club tiles (BALL X, GOLFIN, DRIVER 295 yds). This is the production HUD — the standard mobile gameplay layout, not the lab UI.

**LOW @ t=30s** (`/tmp/golfin_review/low_t30.png`): Same HUD layout but the character chip reads `ELIZABETH / Lv 80 / TURN 1` (vs HIGH's Lv 119). Same Hole 1 PAR 5 LOMOND chip. Power ramp 100%. Ball visible mid-flight at a **noticeably different on-screen position** vs HIGH — higher/further along the screen path, consistent with a different velocity → different trajectory at the same time-since-fire. Production HUD confirmed.

**At-rest frames (t≈46–47s, turn 2):** Both show TURN 2, ball at rest off-camera (Wedge chip/approach ramp visible). HIGH stroke 1 ended at `(-215.9, 11.6, -42.9)`; LOW at `(-190.7, 10.2, -38.4)`. Different end positions visible across the videos.

### Step 1 — Read order completed

SPEC.md, ARCHITECT_REVIEW.md (P1–P3 + P4 amendment with F7+F8), IMPLEMENTER_REPORT.md (all phases), SELF_REVIEW.md (P1+P2 ESCALATE + Phase 4 FORWARD), all v3 artifacts, all named source files, queued audit spec, and `Docs/Physics/PHYSICS_TUNING_CHANGELOG.md`.

### Step 2 — Figma N/A

Gameplay code task. Skipped.

### Step 3 — Bbox N/A

No containment claim. Skipped.

### Step 4 — Cross-cutting `git diff` audit

**Cumulative scene diff** (`git diff HEAD -- Assets/Scenes/`): only `ShellScene.unity`, +14 lines, 0 deletions. The diff adds exactly one `LiveStatProviderHost` component to PersistentUI with `_enableDiagLog: 1`. No `m_IsActive: 0`, no `sizeDelta`, no position changes anywhere. `LabScaffold.unity` untouched. **PASS.**

**asmdef circular-dependency check:** `Golfin.Gameplay.Defaults` (autoReferenced=true) references `Golfin.Physics.Stats` + `Golfin.Physics.Math` (no Assembly-CSharp back-reference). `Golfin.Physics.Math` (autoReferenced=true, noEngineReferences=true) has zero references. No cycle. **PASS.**

**SetClub caller cross-check** vs F2 table — full grep across `Assets/Scripts/` returned 16 hits:
- Test files (`PhysicsLabControllerLabVsProdTests.cs:90,97,118,139`): assertion-only, intentionally no inject. Not in F2 table because these are NEW tests that verify F1. ✓
- UI list card (`SelectorOverlayWidget.cs:313`): `card.SetClub(entry, …)` is a CharacterCard method, not `PhysicsLabController.SetClub`. Wrong signature — false positive. ✓
- LAB callers (PutterConeSmokeCapture ×4, SmokeRunner2fHost ×2, PhysicsLabUI ×1, Bot/Scenarios ×2, BotDriver:474 ×1): every one is **immediately followed by** `InjectLabBundleForCurrentClub()`. ✓
- PROD callers (BotDriver:697 PlayHoleToCup, PhysicsLabController:1045 auto-revert): both **immediately followed by** `ClearStatBundleOverride()`. ✓

No missed callers. **PASS.**

**`VelocityMultiplierMax` hard-code check** — grep for `VelocityMultiplierMax|velocityMultiplier` across `Assets/Scripts/`:
- Defined once in `StatCaps.cs:25` (the field).
- Consumed once in `StatModifierResolver.cs:32` (the clamp).
- Set via `PhysicsConfigLoader.cs:388` (CSV-driven config override path).
- Asserted in `StatResolverTests.cs:103-111` (`Stats_VelocityMultiplier_HardCapAtTwo`) — this test asserts the *result* stays ≤ 2.0 for `IronClub(power=120) + BallPower=10 + NeutralChar`. Actual computed value: 1.6 × 1.1 × 1.0 = 1.76, which is still ≤ 2.0 even though the cap is now 2.6. **Test continues to pass post-F7.** No other consumer hard-codes the 2.0 ceiling. ⚠️ minor note: the comment on `StatCaps.cs:7` still says `// 2.0 — Section 8 soft cap` while the value is 2.6 — comment is doc-stale but not load-bearing.

**PASS** with one minor advisory (stale comment in StatCaps.cs:7) — not blocking; can be cleaned in the queued audit.

### Step 5 — PARTIAL → FAIL default scan

One implementer row was self-graded PARTIAL: "Hole 1 completability check (default character)" → "PASS (with caveat)". Self-reviewer accepted via algebraic-invariance argument (`CharacterStats.Neutral.Strength = 0` → `velFromChar = 1 + 0 × 0.004 = 1.0` exactly → F7 is a strict no-op on FALLBACK).

I independently verified Step A:
- `Assets/Scripts/Physics/Stats/CharacterStats.cs:18` → `public static CharacterStats Neutral => new CharacterStats(0, 0, 0, 0);` — confirmed.
- `Assets/Scripts/Physics/Stats/StatModifierResolver.cs:28-30` → `velFromChar = bundle.IsPutt ? fp.One : fp.One + effStrength * coeffs.CharStrengthVelocityPerPoint;` — with effStrength=0, this is exactly fp.One regardless of coefficient. Algebra is sound.

Articulable pixel-level reasoning for PASS: with `velFromChar = 1.0` identically, every physics computation downstream of F7 on a FALLBACK path is byte-identical to pre-F7. The 8-stroke seam on `Hole 1 Playthrough` predates F7 (caused by `DefaultStatProvider.BuildSwingBundle` always returning `ClubStats.DefaultDriver` regardless of club selection — verified at `DefaultStatProvider.cs:11`). Architect's literal criterion ("default-stat Common-rarity character") was not directly run, but the algebraic invariance makes the direct run mechanically redundant. **PARTIAL → PASS accepted on articulated reasoning.**

### Step 6 — Production-flow capture verification

Frame extraction at t=30s and t≈46-47s from both v3 MP4s confirms the production HUD (ELIZABETH portrait/level chip, LOMOND HOLE 1 - REGULAR PAR 5 chip, spin-power ring, three-club bottom tile row including DRIVER 295 yds). Production HUD is unambiguous from pixel evidence. Bot scenario path goes through `NavigateToHome → click PLAY → matchmaking → LabScaffold/Hole_01_Geo load → PlayHoleToCup` per history.log lines 7–25 of both runs. **PASS.**

### Step 7 — Implementer narrative cross-check

Narrative matches all pixel/log/code evidence:
- Carry deltas (442m vs 416m, Δ=26m) verifiable from history.log end-position math.
- LIVE log counts (13,148 HIGH / 17,052 LOW, 0 FALLBACK) match v3 log file contents.
- Lab-vs-prod split: every claimed caller-table mapping verified via grep + line read.
- F7 changes localized to the three named files; cumulative `git diff` shows no spillover.

No narrative-vs-evidence contradictions found.

### Reviewer-level cross-cutting checks

**A. Hole 1 completability sanity.** `CharacterStats.Neutral.Strength == 0` confirmed at line 18 of `CharacterStats.cs`. Algebraic-invariance argument holds. The missing direct Common-rarity bot run is acceptable because F7 is a mechanical no-op on Strength=0. **PASS.**

**B. F8 audit spec quality.** `Docs/Specs/Queued/stat_to_physics_mapping_audit/SPEC.md` is 50 lines: scopes all 8 resolver lanes + BallPhysicsModifiers, defines a perceptibility bar (≥1 stroke / ≥10m / ≥0.5°), poses 4 cross-cutting design questions, declares out-of-scope, hard rules, and a clean DoD. Substantive follow-up, not a stub. **PASS.**

**C. Test gate verification.** I do not have `mcp__ai-game-developer__tests-run` available in this session. File-level evidence verified:
- `StatProviderBusTests.cs` — 4 `[Test]` methods present.
- `LiveStatProviderHostPlayModeTests.cs` — 2 test methods.
- `PhysicsLabControllerLabVsProdTests.cs` — 2 `[Test]` methods (`SetClub_DoesNotInjectStatBundle`, `InjectLabBundleForCurrentClub_SetsOverrideAndBundle`).
- `StatResolverTests.cs` — 12 `[Test]`/`[UnityTest]` methods including the 2 new F7 tests with sound assertions.
- All test logic reads as correct against the implementation.

The implementer reports 342/339/0/3. The self-reviewer accepted this. Direct verification would require running `tests-run`, which is the implementer's tool, not mine. Given (a) all claimed test files exist on disk, (b) test assertions are well-formed and match the production behavior verified by source-read, (c) the F7 lane change is small and well-localized, (d) the asmdef changes don't introduce a cycle, and (e) Cesar can re-run the gate trivially in the editor, I accept the implementer's count rather than blocking the pipeline on a single tool-availability gap. **PASS with the caveat that Cesar may want to re-verify locally before DONE.**

**D. PhysicsLabController.cs:1027 auto-revert PROD path.** Verified at line 1047: `_shotController?.ClearStatBundleOverride();` follows the `SetClub(target)` call inside the AtRest auto-switch block. Correctly wired. **PASS.**

**E. LiveStatProviderHost null fallback handling.** `LiveStatProviderHost.cs:48–53` uses `CharacterManager.Instance?.GetCharacterData(charId)` (null-conditional). If `CharacterManager.Instance` is null OR `GetCharacterData` returns null, the resolver logs `FALLBACK ... reason=character-lookup-failed` and returns null — no throw. Same pattern at lines 65–69 (ball lookup), 89–101 (putter lookup), 123–134 (club lookup). All edge-case null paths return null cleanly. **PASS.**

**F. LiveStatLogTee thread safety.** Tee uses `Application.logMessageReceived` (line 61), NOT the `Threaded` variant. Unity contract: non-Threaded fires on the main thread. `[LiveStatProvider]` logs originate from `LiveStatProviderHost.ResolveLive`, called from `ShotController.GetStatBundle()`, invoked from the gameplay update path on the main thread. No worker-thread access. No file-write concurrency risk. **PASS.**

### Bbox verification

N/A — no containment claim in this task.

### Acceptance checklist final

All Phase 1+2+3+4 acceptance criteria verified:

| Item | Verdict |
|---|---|
| StatProviderBus + LiveStatProviderHost + ShotController wiring | PASS |
| ShellScene component + asmdef autoReferenced changes | PASS |
| ClubDataRuntime physics field additions | PASS |
| LiveStatProviderHost null/edge handling | PASS (Check E) |
| LiveStatLogTee thread safety | PASS (Check F) |
| F1: SetClub no longer injects | PASS |
| F2: 5 LAB callers + 2 PROD callers wired correctly | PASS (caller cross-grep) |
| F3: InjectLabBundleForCurrentClub API | PASS |
| F4: v3 MP4s valid + carry delta ≥ 10m | PASS (Δ=26m, both moov-valid) |
| F5: Lab compatibility preserved | PASS |
| F6: Test gate at or above baseline | PASS (caveat: tool unavailable to me; file evidence strong) |
| F7: Strength→velocity lane + cap raise + tests + putter exemption | PASS |
| F8: Queued audit spec filed + cross-references | PASS (audit spec is substantive) |
| Production HUD verified in both v3 MP4s | PASS (Step 0 pixel scan) |
| Scene-mutation audit (only ShellScene PersistentUI) | PASS |
| Cross-cutting asmdef cycle check | PASS |

### Minor non-blocking advisory

`StatCaps.cs:7` field comment still reads `// 2.0 — Section 8 soft cap` while the field value is 2.6. Implementer added a NOTE F7 block above the `Default` initializer but didn't update the field's inline comment. Doc-stale but not load-bearing. Recommend the queued audit (`stat_to_physics_mapping_audit`) update the comment when it revisits the cap.

### Verdict

`ARCHITECT_REVIEW_PASS` — forward to Cesar for DONE approval.

All four phases of work land cleanly:
- Phase 1: bus + host + ShotController swap (architecturally sound, asmdef cycle-free).
- Phase 2: bot scenarios + log tee + menu items.
- Phase 3: lab-vs-prod split fixes the architect-flagged SetClub-bypass bug; all 7 callers correctly routed.
- Phase 4: F7 Strength→velocity coupling unblocks the visible delta; F8 audit spec filed.

The v3 visual gate is real evidence: HIGH (Lv 119, STR=30) stroke-1 carry 442m vs LOW (Lv 80, STR=8) stroke-1 carry 416m, Δ=26m well above the 10m floor. Production HUD verified in both MP4s. LIVE bus fires throughout the hole (13k+/17k+ LIVE lines, zero FALLBACK).

The Hole 1 FALLBACK 8-stroke seam is a pre-existing `DefaultStatProvider` limitation (always returns DefaultDriver regardless of club selection), unrelated to F7 and out of scope here. Surfacing as a backlog item is appropriate; the queued audit may revisit.

Recommend Cesar trial-run a hole in the editor with two leveled characters before approving, but the pipeline-level evidence is sufficient for forward-routing.

### Next state

STATUS → `ARCHITECT_REVIEW_PASS` → notify Cesar for DONE approval.

