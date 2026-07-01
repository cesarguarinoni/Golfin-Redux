# SPEC — `stamina_roster_live_meter`

> **Authoritative spec.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.
>
> Stamina/Condition Economy **Phase 5 (UX polish)** — the live meter deferred by `stamina_roster_ux` SPEC §8. Tier 3 (visual + new runtime behaviour). Phases 1–4 are DONE and merged (Phase 4 = `dd41af4c9`).

## Status
See `STATUS.md`. Set to `SPEC_READY`.

## Goal
Make the roster `CharacterDetailPanel` Condition display **update live while the panel is open**, instead of only on discrete events (carousel-select / level-up / selection / points / language).

Cesar's locked scope (2026-06-30):
1. **Real-time tick** — while the panel is open, recompute Condition from elapsed real-time so the meter + degraded stats stay fresh without re-navigating (e.g. after the app is backgrounded and resumed, or a round drains the pool).
2. **Smooth animation** — the Stamina meter fill (and the Strength/Club Control ghost/effective fills) **lerp smoothly** to their new targets on every change (live tick, character-select, level-up, return-from-a-drained-round), rather than snapping.
3. **Update the numbers on the right too — not just the bar** (Cesar, explicit). As Condition recovers, the Strength + Club Control **effective numbers climb back** (e.g. `5/25 → 6/25`) and the ghost tails shrink; when Condition crosses back above comfort the numbers return to base and the ghost hides. The Stamina row number stays the Stamina **stat** (unchanged) — only its meter fill/colour tick.
4. **Demo time-accelerator** — a debug/demo toggle that fast-forwards regen so the meter **visibly climbs on screen** for video/review (real regen ≈24 pts/hr ⇒ imperceptible per session). Code-only (see §Hard constraints); MUST default OFF and never ship enabled.

## Hard constraints (learned from Phase 4 — `stamina_roster_ux`)
- **ZERO scene / prefab mutation.** Phase 4 burned 2 extra iterations on Unity scene-save sweeping override drift into unrelated modals. This task is a **single-file C# change** to `CharacterDetailPanel.cs` (+ EditMode tests + the demo menu). Do **NOT** add any `[SerializeField]`, do **NOT** open/save any scene or prefab, do **NOT** touch `ShellScene.unity`. At task close the ONLY non-doc diff must be `CharacterDetailPanel.cs`, the new test file, and the new demo-menu editor script. If you believe a SerializeField is unavoidable, set `IMPLEMENTER_BLOCKED` and surface — do not mutate the scene.
- **Display-only — never persist, never mutate persistent state from the display path.** Do **NOT** call `StaminaRuntimeService.AccrueRegen(...)` from the panel (it mutates `currentStaminaEnergy` AND advances `conditionUpdatedUtc`, and persistence must move those together). Do **NOT** call `CharacterManager.PersistCondition` / write save. The live value is a **read-only projection** (see §3). The real regen is still accrued authoritatively on load/persist by the existing `StaminaRuntimeService` path — this task only *displays* the live-projected value.
- **No `Assets/Scripts/Physics/` edits. No `Scenarios.cs` `*Gate` additions.** (Standing bans.)

## Reference
- Visuals are **unchanged from Phase 4** — same bars, same Condition meter, same locked gradients. No new Figma surface. The meter colours remain the Phase 4 locked decisions:
  - HIGH (≥0.60) blue `#5792E6→#2775DD→#1A55A4` · MID (0.30–0.60) amber `#E6B847→#D6961E→#A46E14` · LOW (<0.30) red `#D16A47→#C04000→#8E2D00`, selected by `StaminaModel.MeterState`.
- This task adds **motion**, not new pixels. The Figma-fidelity gate (Rule 18) is satisfied by confirming the animated end-states still match the Phase 4 colours/fills (carry the Phase 4 table forward, re-stated).

## Architecture context (exact APIs — verified in code)
- **File:** `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` (the ONLY runtime file edited). Existing members:
  - `OnEnable()` / `OnDisable()` — event sub/unsub (lines ~94/111). `currentCharacterId` holds the selected id.
  - `UpdatePanel(string characterId)` (~139) — computes `conditionPct = StaminaModel.ConditionPct(playerData.currentStaminaEnergy, playerData.currentStamina)` (line ~203) then calls `UpdateGhostStatBar(...)` for STR (~215) and CC (~223) and `UpdateConditionMeter(playerData.currentStamina, stamCap, conditionPct)` (~234).
  - `UpdateGhostStatBar(...)` (~280) — sets effective bar fill, ghost fill+enabled, and the number `{effective}/{cap}` where `effective = StaminaModel.EffectiveStat(base, conditionPct)`.
  - `UpdateConditionMeter(int staminaStatValue, int staminaStatCap, float conditionPct)` (~339) — `staminaBar.fillAmount = conditionPct; staminaBar.color = ApplyMeterColor(conditionPct);`.
  - `ApplyMeterColor(float conditionPct)` (~359) — `MeterState` → colour.
  - There is **no** `Update()` / coroutine today.
- **Model / data APIs (exact):**
  - `StaminaModel.RegenForElapsed(int recoveryStat, TimeSpan elapsed) → float` (clamped ≥0; `RegenPerHour = 12 + recovery*2`).
  - `StaminaModel.ConditionPct(float condition, int staminaStat) → float` · `StaminaModel.EffectiveStat(int base, float pct) → int` · `StaminaModel.MeterState(float pct)` · `StaminaModel.IsConfigured`.
  - `PlayerCharacterData`: `currentStaminaEnergy` (float, live pool) · `maxStaminaEnergy` (float) · `conditionUpdatedUtc` (DateTime, `default` ⇒ treat as fresh/full) · `currentRecovery` · `currentStamina` · `currentStrength` · `currentClubControl`.
  - `StaminaRuntimeService.AccrueRegen(pcd, nowUtc)` — **referenced for understanding only; the panel MUST NOT call it.**

## Locked decisions
| # | Decision | Value |
|---|----------|-------|
| **L1** | Live value source | **Read-only projection**, recomputed each tick: `displayEnergy = (conditionUpdatedUtc == default) ? currentStaminaEnergy : min(maxStaminaEnergy, currentStaminaEnergy + StaminaModel.RegenForElapsed(currentRecovery, simElapsed))`, then `displayPct = StaminaModel.ConditionPct(displayEnergy, currentStamina)`. `simElapsed = (DateTime.UtcNow − conditionUpdatedUtc) + demoExtra` (see L4). NEVER writes back. |
| **L2** | Tick mechanism | A coroutine started when the panel is enabled with a selected character, stopped in `OnDisable`. Tick ~15–30 Hz (e.g. `WaitForSeconds(0.05f)` or per-frame in a guarded `Update`). Guard on `currentCharacterId != null` and not in CompareController compare-mode (same guard `UpdatePanel` uses). |
| **L3** | Smooth animation | Lerp the **displayed** values toward target each tick: `staminaBar.fillAmount`, the STR/CC effective bar fills, and the STR/CC ghost fills move via `Mathf.MoveTowards`/`Lerp` (pick a rate that reaches target in ~0.25–0.4s — tune for feel). The STR/CC **number** = `EffectiveStat(base, lerpedOrLivePct)` recomputed each tick (snaps at integer boundaries as the lerp crosses them — acceptable). Meter colour = `ApplyMeterColor(currentLerpedPct)` each tick so it changes as the fill crosses 0.30/0.60. Crossfading the colour is optional polish; snap-on-lerped-value is the baseline. |
| **L4** | Demo accelerator | **Code-only**, no SerializeField. A `public static bool DemoAccelerate` + `public static float DemoHoursPerRealSecond` on the panel (or a small static helper), toggled by a new editor menu `GOLFIN > Stamina > Toggle Live-Meter Demo Accel`. When ON, accumulate `demoExtra += Time.unscaledDeltaTime * DemoHoursPerRealSecond` (hours) and feed it into L1's `simElapsed` so the meter climbs visibly; the projection stays **read-only** (still never persists). Toggling OFF resets `demoExtra` to zero. MUST default OFF; do not gate any production behaviour on it. |
| **L5** | No-regression on events | The existing `UpdatePanel` event path still fires and sets the **target**; the tick animates the *displayed* value toward that target. On a fresh `UpdatePanel` (character switch), snap (no cross-character tween) so switching characters doesn't show the previous character's bar draining into the new one. |
| **L6** | `!IsConfigured` fallback | If `!StaminaModel.IsConfigured`, the live tick is inert: full blue meter, base stats, no ghost, no exceptions (identical to Phase 4's fallback). |

## Implementation
1. **Extract a pure projection helper** (testable, no Unity types): e.g. `internal static float LiveDisplayEnergy(float currentEnergy, float maxEnergy, int recovery, bool hasTimestamp, TimeSpan simElapsed)` returning the clamped projected energy per L1. Keep it `internal` so the EditMode test asmdef can see it (add `[assembly: InternalsVisibleTo(...)]` if needed, or make it `public static`). This is the gate-able core.
2. **Add the tick** (L2): coroutine `LiveMeterTick()` started in `OnEnable` (guard: a character is selected) and on each `UpdatePanel`; stopped/cleared in `OnDisable`. Each tick:
   - resolve the current `PlayerCharacterData` for `currentCharacterId`;
   - compute `targetPct` from the read-only projection (L1) using `simElapsed = (now − conditionUpdatedUtc) + demoExtraHours`;
   - lerp the displayed fills/ghost toward targets (L3); recompute STR/CC numbers + meter colour from the lerped pct (L3, Cesar's "numbers too");
   - never write `currentStaminaEnergy` / `conditionUpdatedUtc` / save.
3. **Refactor the bind to separate "compute target" from "apply displayed"** so both the event path (`UpdatePanel`) and the tick feed the same apply logic. Keep `UpdateGhostStatBar` / `UpdateConditionMeter` / `ApplyMeterColor` as the apply primitives; add lerp state (current displayed pct/fills) as private fields.
4. **Demo menu** (L4): new editor script (e.g. `Assets/Scripts/UI/Roster/Editor/StaminaLiveMeterDemoMenu.cs`) with `[MenuItem("GOLFIN/Stamina/Toggle Live-Meter Demo Accel")]` flipping `CharacterDetailPanel.DemoAccelerate`. Editor-only (under an `Editor/` folder or `#if UNITY_EDITOR`).
5. **Cleanup / no-regression:** snap on character switch (L5); inert when `!IsConfigured` (L6); restore: the demo accel must leave no residue when toggled off / on `OnDisable` (reset `demoExtra`).

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)
- [ ] While the panel is open, the Condition meter + STR/CC effective fills update **live** (read-only projection) without re-selecting the character.
- [ ] **Numbers update too:** STR/CC effective numbers (`{effective}/{cap}`) recompute live and climb back as Condition recovers; ghost tails shrink/hide accordingly. (Cesar's explicit requirement.)
- [ ] Fills **lerp smoothly** (no snap) on live tick + level-up + return-from-drained-round; switching characters **snaps** (no cross-character tween) per L5.
- [ ] Meter colour transitions blue↔amber↔red as the lerped pct crosses 0.60 / 0.30 (`StaminaModel.MeterState`-driven, not hardcoded).
- [ ] **Display-only proof:** the panel never calls `AccrueRegen` / `PersistCondition` / writes save; `currentStaminaEnergy` and `conditionUpdatedUtc` are never mutated by the panel (cite the projection is read-only). No double-regen vs the authoritative load/persist path.
- [ ] **Demo accelerator:** `GOLFIN > Stamina > Toggle Live-Meter Demo Accel` makes the meter visibly climb on screen; defaults OFF; toggling OFF stops/resets; no production behaviour depends on it.
- [ ] `!StaminaModel.IsConfigured` → inert (full blue, base stats, no ghost, no exceptions).
- [ ] **ZERO scene/prefab mutation** — `git status` shows no `.unity`/`.prefab` diff; the only runtime diff is `CharacterDetailPanel.cs`. (Hard constraint.)
- [ ] EditMode tests for the pure projection helper (zero elapsed → no gain; 2h Recovery 9 → +60 clamped to max; `default` timestamp → returns currentEnergy unchanged; demoExtra accelerates). Full suite stays green.
- [ ] No Unity Console errors; no per-frame GC spikes from the tick (cache the pcd lookup; avoid per-tick allocations).
- [ ] Figma fidelity: animated end-states still match the Phase 4 locked colours/fills (carry-forward table, re-stated).
- [ ] Spec deviations (if any) flagged at the bottom of the report.

## Files this task touches
- `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` — live tick coroutine, read-only projection helper, smooth lerp of fills + live STR/CC numbers + meter colour, static demo-accel toggle.
- `Assets/Scripts/UI/Roster/Editor/StaminaLiveMeterDemoMenu.cs` (NEW, editor-only) — the demo-accel menu item.
- A new/existing EditMode test file for the projection helper (e.g. `Assets/Scripts/UI/Roster/Tests/...` or fold into an existing roster test asmdef).
- **NO scene, NO prefab, NO SerializeField, NO Physics, NO Packages.**

## Smoke evidence (video required)
PlayMode over the real game flow (boot ShellScene → Roster → select a character). Capture a **captioned video** at iPhone 14 1170×2532 showing, with the **demo accelerator ON**, the Stamina meter climbing red→amber→blue while the **STR/CC numbers tick back up** (`5/25 → 6/25`, `6/25 → 7/25`) and the ghost tails shrink — then the smooth lerp on a character-select snap. Clip → `videos/`; frame-extract stills → `screenshots/`; designate one canonical screenshot ≥900px. Also state, with the accelerator OFF, that the meter holds steady (real regen imperceptible) — proving the accelerator is the only thing forcing visible motion and nothing persists.

## Out of scope
- Any change to drain / regen / persistence math (Phases 1–3) — display only.
- `StatBar.cs` (Compare/carousel component) — its live treatment is a separate follow-up.
- New visual surface / Figma redesign — motion only, same Phase 4 pixels.
- Shipping the demo accelerator enabled, or gating production behaviour on it.
