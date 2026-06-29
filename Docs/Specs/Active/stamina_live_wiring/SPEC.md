# SPEC — `stamina_live_wiring` (Stamina Economy Phase 2: live wiring)

**Tier:** 3 (full pipeline) — touches the stat seam, the deterministic physics resolver, the save schema, and runtime hole-complete wiring.
**Author:** Architect · **Date:** 2026-06-30 JST
**Depends on:** Phase 1 `stamina_model` (DONE — `Golfin.Core.Stamina`, commit `da35e931e`).
**Design doc:** `Docs/Design/STAMINA_ECONOMY.md` (locked decisions §; phase plan §8).

---

## 0. CRITICAL DISCOVERY — the gameplay penalty already exists (read first)

The physics layer **already degrades exactly Strength + ClubControl by the stamina fraction.** It has been dormant only because the live pool is hard-coded full (100/100) and never drains.

`Assets/Scripts/Physics/Stats/StatModifierResolver.cs` (Step 1):

```csharp
fp staminaFraction = (bundle.MaxStamina > fp.Zero) ? bundle.CurrentStamina / bundle.MaxStamina : fp.One;
fp staminaMultiplier = fpMath.Min(fpMath.Max(coeffs.StaminaFloorFraction, staminaFraction), fp.One);
fp effStrength    = fp.FromInt(bundle.Character.Strength)    * staminaMultiplier;
fp effClubControl = fp.FromInt(bundle.Character.ClubControl) * staminaMultiplier;
```

`effStrength`/`effClubControl` then drive velocity-from-character, aim-cone reduction, and overpower forgiveness. `StaminaFloorFraction` defaults to **0.20** (hardcoded in `Assets/Scripts/Physics/Stats/StatCoefficients.cs:47`; the CSV key `stamina_floor_fraction` is supported by `PhysicsConfigLoader.cs:355` but is **not currently present** in the physics tuning CSV, so the 0.20 default is live).

`LiveStatProviderHost.ResolveLive` **already feeds the pool into the bundle** on both paths:
- Solo: `new StatBundle(..., fp.FromFloat(charData.currentStaminaEnergy), fp.FromFloat(charData.maxStaminaEnergy))`
- Tournament: `..., fp.FromFloat(TournamentRoundContext.StaminaEnergyRemaining), fp.FromFloat(TournamentRoundContext.StaminaEnergyMax)`

**Consequence:** the moment the pool actually drains, the physics resolver penalizes Str+ClubControl **with no new penalty code.** This means:
1. The earlier "apply the penalty at `LiveStatProviderHost`" framing is **wrong / would double-dip.** Do **not** add a second penalty on top of the resolver.
2. There are now **two stamina models** that disagree, and Phase 2 must pick one (→ **D1**):
   - **Physics (live):** linear `max(floor, frac)`, floor **0.20** (→ 80% max reduction), no comfort zone, deterministic `fp`.
   - **`StaminaModel` (locked design):** comfort-curve (penalty 0 above 70%, then `0.33·pow(t,1.6)`), floor **0.33** (→ 67% reduction min), `double` math.

The linear model **violates locked design decision #3** (early holes negligible): with per-hole drain 8 on a Sta-9 tank of 114, by hole 6 the pool is 58% → linear multiplier 0.58 → **42% degraded by hole 6**, the exact "bites too early" outcome the comfort curve exists to prevent.

---

## 1. Scope

**IN (Phase 2 — LIVE / solo + versus path):**
1. **Boot-load** `StaminaConfigLoader.Load()` so `StaminaModel` is configured before first use (it throws otherwise).
2. **Real tank size:** `maxStaminaEnergy = StaminaModel.MaxCondition(currentStamina_stat)` instead of flat 100.
3. **Per-hole drain** on the live path: subscribe to `GameSession.OnHoleComplete`, drain the selected character's `currentStaminaEnergy` by `StaminaModel.DrainForHole()`.
4. **Passive regen** over real time (offline/between-session), accrued from a persisted timestamp via `StaminaModel.RegenForElapsed(recoveryStat, elapsed)`.
5. **Persistence:** persist `conditionEnergy` + `conditionUpdatedUtc` per character; save schema **v3 → v4**.
6. **Single gameplay model (D1):** make the gameplay degradation honor the locked comfort-curve (recommended: pre-degrade at the seam via `StaminaModel.EffectiveStat` + **neutralize** the physics resolver multiplier to avoid double-dip).

**OUT (later phases — do NOT touch):**
- Tournament pool tank-size / per-hole drain relocation / tournament-pool persistence → **Phase 3** (`TournamentRoundContext`, `OnTournamentHoleComplete`, the per-shot `ShotController.cs:393 DepleteStamina()` call). The per-shot tournament drain **stays as-is** this phase.
- Roster UI: ghost bars on Str/ClubControl + blue→yellow→red meter in `CharacterDetailPanel`/`StatBar`; portrait icon → **Phase 4**.
- In-session live regen tick (regen while the app is open and idle). v1 regen is load/save-boundary only (offline). Defer a runtime tick to polish.
- Any rebalance of drain/tank/regen tunables beyond what's in `stamina_economy.csv`.

---

## 2. Decisions — Cesar's veto (D1 is pivotal; the rest have safe defaults)

**D1 — which stamina model wins, and how (PIVOTAL — reshapes §6/§7):**
- **Option A — keep physics as authority, tune floor 0.20→0.33.** One CSV row. ❌ Still linear → violates locked decision #3 (early holes bite). Not recommended.
- **Option B — port the comfort-curve into the `fp` resolver.** Matches design in-sim + stays deterministic, but is a parallel `fp` re-implementation of `StaminaModel` (non-integer `pow(t,1.6)` in `fp` is the hard part) — two implementations to keep in sync. More risk/work.
- **Option C — RECOMMENDED — `StaminaModel` is the single source of truth.** Pre-degrade Strength+ClubControl at the seam via `StaminaModel.EffectiveStat(raw, conditionPct)` (`double` math, runs **outside** the deterministic sim, yields an int), and **neutralize** the resolver's stamina multiplier (`stamina_floor_fraction = 1.0`) so it passes the already-degraded stat through untouched. Honors the comfort curve exactly, keeps the sim deterministic (int input), no double-dip, one model everywhere.
  - *Live-only caveat:* the `double` penalty runs outside the replayable sim. Fine for solo/versus (no re-sim). For tournament anti-cheat re-sim (Phase 3) the same formula must be reproducible server-side — documented, deferred to Phase 3.

> **✅ LOCKED 2026-06-30 (Cesar): Option C.** `StaminaModel` is the single source of truth — pre-degrade Strength+ClubControl at the seam via `StaminaModel.EffectiveStat`, and neutralize the resolver (`stamina_floor_fraction = 1.0`) so degradation lives in exactly one place. §6–§7 are **authoritative as written**; Options A and B are NOT in play (do not implement either).

**D2 — regen accrual model (default: load **and** save boundaries).** The persisted timestamp = "as of when `conditionEnergy` was last authoritative." Accrue regen to *now* both when loading and when persisting, so frequent unrelated saves never silently reset the regen clock and lose offline recovery. (A single shared `AccrueRegen(playerData, nowUtc)` helper.)

**D3 — apply the penalty to the tournament branch now? (default: YES, shared helper).** The seam helper (§6) runs on both the solo `BuildCharacterStats` and the tournament `tCharacterStats` construction — same code path. The tournament *pool model* (tank/drain/persist) stays placeholder until Phase 3; only the penalty *application* is shared now. (Alternative: gate the helper to the solo path until P3 — cleaner separation but duplicates the call later.)

**D4 — boot mechanism (default: `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`).** Matches the convention already used in 8 files (e.g., `SfxBus`). No scene edit. Loads config + wires the hole-complete subscription once.

**D5 — does versus (1v1) drain the live pool? ✅ LOCKED 2026-06-30: YES** (default kept). `GameSession.OnHoleComplete` fires for solo **and** versus (tournament uses the separate `OnTournamentHoleComplete`). Both are the live character playing a real hole → both drain. If you want versus exempt, gate on `!GameSession.IsVersus`.

**D6 — "now" source for regen (default: device `DateTime.UtcNow`, with `NetworkTimeProvider` if already initialized).** Network UTC (from `leaderboard_wiring`) is preferable for anti-clock-cheat but may not be ready at first load; fall back to device UTC. v1 acceptable; harden in a later pass.

---

## 3. Files touched (literal)

| File | Change |
|---|---|
| `Assets/Scripts/Save/SaveData.cs` | Add 2 fields to `PersistedCharacter`: `float conditionEnergy`, `string conditionUpdatedUtc` |
| `Assets/Scripts/Save/SaveSchemaMigrator.cs` | `CurrentSchemaVersion 3 → 4`; add `v3 → v4` block (no data transform — fields default-safe) |
| `Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs` | Add runtime `System.DateTime conditionUpdatedUtc` (`[NonSerialized]`); keep `currentStaminaEnergy`/`maxStaminaEnergy` (already exist) |
| `Assets/Scripts/CharacterManager.cs` | `LoadRoster()` hydrate (set tank size + energy + accrue regen); `SyncCharacterToSaveData()` dehydrate (accrue + write energy/timestamp); `RefreshStatValues()` recompute tank size on stat change |
| `Assets/Scripts/LiveStatProviderHost.cs` | `BuildCharacterStats` → shared penalty helper (Option C); apply on solo + tournament branches |
| `Assets/Scripts/<new> StaminaRuntimeService.cs` | New Assembly-CSharp static/MonoBehaviour-free service: `[RuntimeInitializeOnLoadMethod]` → `StaminaConfigLoader.Load()` + subscribe `GameSession.OnHoleComplete` → drain; the `AccrueRegen` helper |
| `<physics tuning CSV loaded by PhysicsConfigLoader>` | Add row `stamina_floor_fraction,1.0,...` (Option C neutralization). Locate via `PhysicsConfigLoader`'s `Resources.Load` path |

No asmdef changes: the new service + the `LiveStatProviderHost` edit live in **Assembly-CSharp**, which auto-references `Golfin.Core.Stamina` (`autoReferenced:true`) and already sees `GameSession` (`Golfin.Gameplay.Session`) and `CharacterManager` (`Golfin.Roster`).

---

## 4. Detailed changes

### 4.1 Boot + hole-complete (`StaminaRuntimeService.cs`, new)
- `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` → `StaminaConfigLoader.Load()` (guard: if `!StaminaModel.IsConfigured` log + return-safe).
- Subscribe to `GameSession.OnHoleComplete` **once** (idempotent: unsubscribe-then-subscribe, guard `_wired`).
- On hole complete:
  ```
  if (string.IsNullOrEmpty(GameSession.SelectedCharacterId)) return;
  var pcd = CharacterManager.Instance?.GetCharacterData(GameSession.SelectedCharacterId);
  if (pcd == null) return;
  AccrueRegen(pcd, nowUtc);                       // bring current first (D2)
  pcd.currentStaminaEnergy = Mathf.Max(0f, pcd.currentStaminaEnergy - StaminaModel.DrainForHole());
  pcd.conditionUpdatedUtc = nowUtc;
  CharacterManager.Instance.PersistCondition(GameSession.SelectedCharacterId);  // see §4.3
  ```
- `AccrueRegen(PlayerCharacterData pcd, DateTime nowUtc)`:
  ```
  if (pcd.conditionUpdatedUtc == default) { pcd.conditionUpdatedUtc = nowUtc; return; }
  var elapsed = nowUtc - pcd.conditionUpdatedUtc;
  if (elapsed.TotalHours <= 0) return;
  float regen = StaminaModel.RegenForElapsed(pcd.currentRecovery, elapsed);
  pcd.currentStaminaEnergy = Mathf.Min(pcd.maxStaminaEnergy, pcd.currentStaminaEnergy + regen);
  pcd.conditionUpdatedUtc = nowUtc;
  ```

### 4.2 Tank size (`CharacterManager`)
Whenever `currentStamina` (the STAT) is set/refreshed — `LoadRoster()` overlay block and `RefreshStatValues()` — set:
```
playerData.maxStaminaEnergy = StaminaModel.MaxCondition(playerData.currentStamina);
```
On a stat change that **raises** the tank, leave `currentStaminaEnergy` unchanged (don't auto-fill); it simply can't exceed the new max (already clamped by regen).

### 4.3 Persistence (`CharacterManager` + `SaveData` + migrator)
- `PersistedCharacter` gains `float conditionEnergy` and `string conditionUpdatedUtc` (ISO-8601, `""` = never — mirrors the tournament-DTO string-date convention).
- **Hydrate** (`LoadRoster` overlay): after setting `currentStamina` and tank size:
  ```
  if (string.IsNullOrEmpty(persisted.conditionUpdatedUtc)) {
      playerData.currentStaminaEnergy = playerData.maxStaminaEnergy;   // fresh/pre-v4 = full
      playerData.conditionUpdatedUtc  = nowUtc;
  } else {
      playerData.currentStaminaEnergy = Mathf.Clamp(persisted.conditionEnergy, 0f, playerData.maxStaminaEnergy);
      playerData.conditionUpdatedUtc  = DateTime.Parse(persisted.conditionUpdatedUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
      AccrueRegen(playerData, nowUtc);   // offline recovery since last save (D2)
  }
  ```
  Wrap the parse in try/catch → on failure, treat as fresh (full + now). (Matches the T5 "harden date-parse" deferral.)
- **Dehydrate** — add a public `PersistCondition(characterId)` on `CharacterManager` (or extend `SyncCharacterToSaveData` to also write the two new fields): `AccrueRegen(pcd, nowUtc)` then write `existing.conditionEnergy = pcd.currentStaminaEnergy; existing.conditionUpdatedUtc = nowUtc.ToString("o");` then `MarkDirty()`. Existing `SyncCharacterToSaveData` callers (select/levelup/refresh) should preserve condition (write current energy + timestamp), not stamp a clean pool.
- **Migrator** `v3 → v4`: no transform needed (new fields default `0f`/`""`; the empty-timestamp hydrate path treats them as "full & fresh"). Add the block + bump `CurrentSchemaVersion = 4`, fail-hard-on-newer preserved.

### 4.4 New-save default
`SaveData.schemaVersion` field default is stale (`2`) but the migrator forces to current on every load, and new saves are written at `CurrentSchemaVersion`. No change needed beyond the migrator bump. (Optional tidy: update the field default to `4`.)

---

## 5. Persistence summary
- One float + one ISO string added per character. Backward compatible: pre-v4 saves load full (no condition data → empty timestamp → full pool). Forward fail-hard preserved.

---

## 6. The gameplay penalty (Option C — D1 default)

### 6.1 Seam (`LiveStatProviderHost`)
Replace the raw `BuildCharacterStats` with a shared helper that pre-degrades the two degraded stats:
```csharp
static CharacterStats BuildCharacterStats(int str, int ctrl, int rec, int sta, float conditionPct)
{
    int eStr  = StaminaModel.IsDegraded("Strength")    ? StaminaModel.EffectiveStat(str,  conditionPct) : str;
    int eCtrl = StaminaModel.IsDegraded("ClubControl") ? StaminaModel.EffectiveStat(ctrl, conditionPct) : ctrl;
    return new CharacterStats(strength: eStr, clubControl: eCtrl, recovery: rec, stamina: sta);
}
```
- **Solo path:** `conditionPct = StaminaModel.ConditionPct(charData.currentStaminaEnergy, charData.currentStamina)`; feed `charData.current*`.
- **Tournament branch (D3 = yes):** `conditionPct = StaminaModel.ConditionPct(TournamentRoundContext.StaminaEnergyRemaining, snap.Stamina)`; feed `snap.*`.
- Guard: if `!StaminaModel.IsConfigured`, skip degradation (pass raw) and log once — never throw on the shot path.

### 6.2 Neutralize the resolver (avoid double-dip)
Add `stamina_floor_fraction,1.0` to the physics tuning CSV so `StatModifierResolver`'s `staminaMultiplier = min(max(1.0, frac),1.0) = 1.0` always — the resolver passes the already-degraded stat through. **Degradation now lives in exactly one place (the seam).**

> **NOTE (determinism):** this changes deterministic physics behavior (the multiplier becomes inert). Any existing physics test asserting the 0.20-floor stamina multiplier must be updated or removed; flag in the report. The bundle still carries `CurrentStamina/MaxStamina` (unused by the resolver now, still available to display/HUD).

---

## 7. Tests (EditMode)

New `StaminaLiveWiringTests` (+ extend `SaveLayerTests` for migration):
1. **Tank size** — `maxStaminaEnergy == MaxCondition(currentStamina)` after `LoadRoster`/`RefreshStatValues` (Sta 9 → 114, Sta 0 → 60).
2. **Per-hole drain** — firing `GameSession.OnHoleComplete` reduces the selected char's energy by exactly `DrainForHole()` (8), clamped ≥ 0; non-selected chars untouched.
3. **Regen accrual** — `AccrueRegen` over a 2h gap at Recovery 9 adds 60, clamped to max; 0/negative elapsed = no-op; empty timestamp = stamp-and-return.
4. **Hydrate full on empty timestamp** — pre-v4 / blank `conditionUpdatedUtc` loads to full pool.
5. **Round-trip** — drain → dehydrate → re-hydrate (no elapsed) preserves energy within epsilon; with elapsed, regen applied once (no loss across multiple intervening saves — D2).
6. **Migration v3 → v4** — a v3 save loads, `schemaVersion == 4`, condition fields default-safe (full); fail-hard on v5.
7. **Penalty seam (Option C)** — `BuildCharacterStats` degrades Strength+ClubControl by `EffectiveStat`, leaves Recovery+Stamina raw; at pct ≥ 0.70 no change; at pct 0 → `round(base·0.67)`. (Reflection or an exposed test hook on `LiveStatProviderHost`, per existing `LiveStatProviderHostPlayModeTests` conventions.)
8. **Neutralization parity** — with `stamina_floor_fraction = 1.0`, `StatModifierResolver` output is independent of `CurrentStamina/MaxStamina` (no second degradation).

---

## 8. Acceptance criteria
1. Project compiles; all new + existing EditMode tests green (physics stamina-multiplier tests updated for neutralization).
2. `StaminaModel` is configured at boot (no "not configured" throw on first shot).
3. Playing a hole in solo reduces the selected character's Condition by `DrainForHole()`, persists across app restart, and recovers by `RegenForElapsed` on reload — verified by a test (and human spot-check on device is welcome but not gating).
4. Degraded Strength+ClubControl reach the swing through **exactly one** model (Option C: comfort-curve at the seam; resolver neutralized) — no double-dip.
5. Tank size scales with the Stamina stat (`MaxCondition`).
6. Save migrates v3 → v4 cleanly; pre-v4 saves load to a full pool.
7. Tournament *pool model* untouched (Phase 3); only the shared penalty helper is reused (D3).
8. Scope clean: no roster-UI changes (Phase 4), no tournament-pool drain/persist changes (Phase 3).

---

## 9. Deferred / out-of-scope (explicit)
- **Phase 3:** tournament pool tank-size + per-hole drain relocation (`ShotController.cs:393` per-shot → `OnTournamentHoleComplete`) + tournament-pool persistence + anti-cheat re-sim reproducibility of the penalty formula.
- **Phase 4:** roster ghost bars (Str/ClubControl) + blue→yellow→red Stamina meter (`StaminaModel.MeterState`) + portrait low-condition icon (`IsLowConditionFlag`; the wiring already exists, dormant).
- In-session (idle) regen tick; network-UTC hardening for regen "now"; if D1 ≠ Option C, the curve-unification work implied by A/B.
