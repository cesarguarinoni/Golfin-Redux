# SPEC — `stamina_tournament_wiring` (Stamina Economy Phase 3: tournament pool)

**Tier:** 3 (full pipeline) — touches the tournament leaf asmdef, the save schema, and the round-context seam.
**Author:** Architect · **Date:** 2026-06-30 JST
**Depends on:** Phase 1 `stamina_model` (DONE), Phase 2 `stamina_live_wiring` (DONE — `34ccaf9f7`).
**Design doc:** `Docs/Design/STAMINA_ECONOMY.md` (locked decisions; phase plan §8).

---

## 0. CRITICAL FRAMING — the penalty is already live; this phase only fixes the pool it reads (read first)

Phase 2 **already wired Option-C degradation onto the tournament branch.** `LiveStatProviderHost.ResolveLive` (verified live):

```csharp
float tConditionPct = StaminaModel.ConditionPct(TournamentRoundContext.StaminaEnergyRemaining, snap.Stamina);
var tCharacterStats = BuildCharacterStats(snap.Strength, snap.ClubControl, snap.Recovery, snap.Stamina, tConditionPct);
```

So degraded Strength+ClubControl **already reach the swing in tournaments today.** The problem is the *pool* it reads is a placeholder (`TournamentRoundContext`): flat **100** tank, **per-shot** drain of **5**, and `BeginRound` **resets to full every hole**, so the penalty never meaningfully bites and never persists.

**Phase 3 = make the tournament pool correct. It does NOT touch `LiveStatProviderHost`** (the seam is done) and it does NOT touch the live/solo pool (Phase 2). Three changes only:
1. **Real tank** = `StaminaModel.MaxCondition(snapshot.Stamina)` instead of flat 100.
2. **Per-hole drain** = `StaminaModel.DrainForHole()` once at hole-complete, instead of per-shot 5.
3. **Persist + resume** the pool on the tournament **entry** (separate pool per the frozen-snapshot ruling), save schema **v4 → v5**.

---

## 1. Scope

**IN (Phase 3 — tournament pool only):**
1. `EntryState` gains `float ConditionRemaining` (the per-entry tournament pool).
2. `LocalTournamentBackend.Register` seeds `ConditionRemaining = MaxCondition(snapshot.Stamina)` at sign-up.
3. `LocalTournamentBackend.SubmitHoleResult` drains `ConditionRemaining -= DrainForHole()` (clamped ≥ 0), **atomic with the hole-result persist** (`_store.Save`).
4. `TournamentRoundContext.BeginRound` seeds the runtime pool **from the entry** (tank = `MaxCondition`, remaining = `entry.ConditionRemaining`) — no more flat-100 reset.
5. **Remove the per-shot drain:** delete the `TournamentRoundContext.DepleteStamina()` call at `ShotController.cs:393`. Drain is now per-hole (backend).
6. **Persist** `conditionRemaining` on `PersistedTournamentEntry`; map it in `SaveBackedEntryStore`; migrator **v4 → v5**.
7. **(D3)** offline regen between holes/sessions, accrued at `BeginRound` from the entry's existing `LastHoleUtc` anchor.

**OUT (later / untouched):**
- `LiveStatProviderHost` — **do not touch** (the penalty seam is already correct, Phase 2).
- The live/solo pool, `CharacterManager`, `PlayerCharacterData`, `StaminaRuntimeService` (Phase 2 — untouched).
- Roster UI: ghost bars + blue→yellow→red meter + portrait icon → **Phase 4**.
- Anti-cheat **re-sim implementation** → future. Phase 3 only ensures the per-hole condition is **reproducible from persisted data** (§6) — no replay engine built.

---

## 2. Decisions — Cesar's veto (D1 + D3 want your eye; D2/D4 have safe defaults)

**D1 — where do drain + tank live? (architectural)**
- **Option A — RECOMMENDED — backend references `Golfin.Core.Stamina`.** `Register`/`SubmitHoleResult` call `MaxCondition`/`DrainForHole` directly; drain is atomic with the persist. `Golfin.Core.Stamina` is a **true leaf** (`references:[]`), so `Golfin.Tournaments → Golfin.Core.Stamina` is **cycle-free** (same direction Phase 2 used). **No `ITournamentBackend` signature change.** Guard each call with `StaminaModel.IsConfigured` → on false, fall back to the current flat constants (`DefaultStaminaMax` / a flat per-hole default) so the existing StaminaModel-unaware backend EditMode tests still pass unchanged; production (configured at boot via `StaminaRuntimeService`) gets the real economy.
- **Option B — keep the leaf StaminaModel-free, pass values as params.** `Register(..., float conditionMax)` + `SubmitHoleResult(..., float conditionDrain)`; the Assembly-CSharp callers compute them. Cleaner separation, but an `ITournamentBackend` contract change + every caller must pass them. *Not recommended* — referencing a pure leaf is exactly the intended dependency direction.

> **Spec written assuming Option A.** If you pick B, only the backend signatures + call sites in §4 change; everything else is identical.

**D2 — legacy / fresh pool value (default: full).** New `conditionRemaining` defaults to **`-1f` (sentinel = "unseeded")** on `PersistedTournamentEntry`. On load, a `-1` (pre-v5 entries, or a just-registered entry before its first persist) is treated as **full** = `MaxCondition(snapshot.Stamina)`. No data transform in the migrator. Safe + backward-compatible.

**D3 — does the tournament pool regen between holes/sessions? (DESIGN CALL — your ruling wanted)**
- **Default: YES** — honors the locked design "SAME formula in/out of tournament." Accrue `RegenForElapsed(snapshot.Recovery, now − entry.LastHoleUtc)` onto the seeded pool at `BeginRound` (reusing the **existing** `LastHoleUtc` field — no new timestamp persisted; regen is recomputed each load, mirroring Phase 2's load-accrual). Fresh entry (no holes yet) = no regen.
- **Alternative: NO** — tournament pool only drains within the event (endurance feel), no time dependence. **Stronger anti-cheat:** with no regen, each hole's condition is reproducible from drain-count alone, so a future server re-sim never has to trust client clocks. The tradeoff: diverges from the locked "same formula."
- *Recommendation:* default **YES** (re-sim is deferred anyway), but if you want tournaments to be the clock-trust-free competitive surface, say **NO** and I'll drop the regen accrual + the `LastHoleUtc` read.

**D4 — remove per-shot drain + rewrite its tests (default: YES).** The `ShotController:393` call goes; `TournamentRoundLoopTests` per-shot assertions (`DepleteStamina_*`) are rewritten to the per-hole model. `TournamentRoundContext.DepleteStamina()` itself may stay as dead/test-only API or be deleted — implementer's call, flag in the report.

---

## 3. Files touched (literal)

| File (asmdef) | Change |
|---|---|
| `Assets/Scripts/Tournaments/EntryState.cs` (`Golfin.Tournaments`) | Add `float ConditionRemaining {get;}` + ctor param; thread through every `new EntryState(...)` |
| `Assets/Scripts/Tournaments/LocalTournamentBackend.cs` | `Register` seeds pool (full); `SubmitHoleResult` drains by `DrainForHole()` clamped ≥0, atomic w/ `_store.Save`; `IsConfigured` fallback (D1-A) |
| `Assets/Scripts/Tournaments/<Golfin.Tournaments>.asmdef` | Add `"Golfin.Core.Stamina"` to `references` (D1-A; cycle-free leaf) |
| `Assets/Scripts/Tournaments/SaveBackedEntryStore.cs` | Map `conditionRemaining` in both Load (`new EntryState`) and Save (`new PersistedTournamentEntry`) |
| `Assets/Scripts/Save/SaveData.cs` | `PersistedTournamentEntry` gains `public float conditionRemaining = -1f;` |
| `Assets/Scripts/Save/SaveSchemaMigrator.cs` | `CurrentSchemaVersion 4 → 5`; add `v4 → v5` block (no transform — sentinel-default safe) |
| `Assets/Scripts/Gameplay/TournamentContext/TournamentRoundContext.cs` | `BeginRound(tid, snapshot, tankMax, remaining)` seeds both from caller; drop the flat-100 reset; remove/retire per-shot `DepleteStamina` |
| `Assets/Scripts/Gameplay/Input/ShotController.cs` | Delete the `TournamentRoundContext.DepleteStamina()` call at :393 |
| `Assets/Scripts/UI/Tournaments/TournamentHoleSelectionScreenController.cs` | `BeginTournamentHole`: compute tank + (D3) regen'd remaining from `entry`, pass to `BeginRound` (replace the `staminaCost = 5f` placeholder block ~L284-286) |

No `ITournamentBackend` interface change (D1-A). No `LiveStatProviderHost` change.

---

## 4. Detailed changes

### 4.1 Entry pool (`EntryState` + backend)
- `EntryState`: add `public float ConditionRemaining { get; }`, last ctor param; both `new EntryState(...)` sites in `LocalTournamentBackend` (Register L136, SubmitHoleResult L189) supply it.
- **`Register`** (L116): after building `snapshot`, `float pool = Configured ? MaxCondition(snapshot.Stamina) : DefaultStaminaMax;` → pass as `ConditionRemaining`. (Snapshot is null only for legacy paths — guard: null snapshot → `-1` sentinel.)
- **`SubmitHoleResult`** (L162): `float drain = Configured ? DrainForHole() : FlatFallback; float next = Mathf.Max(0f, entry.ConditionRemaining - drain);` → pass `next` as the updated entry's `ConditionRemaining`. Clone preserves it just like `Snapshot`.

### 4.2 Round context (`TournamentRoundContext`)
- New signature: `BeginRound(string tournamentId, CharacterSnapshot snapshot, float tankMax, float remaining)`. Set `StaminaEnergyMax = tankMax`, `StaminaEnergyRemaining = remaining`. **Delete** the `= DefaultStaminaMax` resets.
- The per-shot `DepleteStamina()` is no longer called from gameplay (§4.4). The pool is **constant within a hole** and the penalty seam reads it unchanged — correct for per-hole drain (condition steps down once per hole boundary, not per shot).
- `EndRound` may keep clearing to a neutral default; it only runs on Finished/teardown.

### 4.3 BeginRound caller (`TournamentHoleSelectionScreenController.BeginTournamentHole`)
Replace the `float staminaCost = 5f; … BeginRound(tournamentId, entry.Snapshot, staminaCost)` block (~L284-286) with:
```csharp
if (entry.Snapshot != null)
{
    float tank      = StaminaModel.IsConfigured ? StaminaModel.MaxCondition(entry.Snapshot.Stamina)
                                                : TournamentRoundContext.DefaultStaminaMax;
    float remaining = entry.ConditionRemaining < 0f ? tank                       // sentinel = full (D2)
                                                    : Mathf.Min(tank, entry.ConditionRemaining);
    // (D3 = YES) offline regen since last hole:
    if (StaminaModel.IsConfigured && entry.LastHoleUtc.HasValue)
        remaining = Mathf.Min(tank, remaining + StaminaModel.RegenForElapsed(entry.Snapshot.Recovery,
                                                    DateTime.UtcNow - entry.LastHoleUtc.Value));
    TournamentRoundContext.BeginRound(tournamentId, entry.Snapshot, tank, remaining);
}
```
(If D3 = NO, drop the regen `if` block entirely.) Snapshot uses `Recovery` — already on `CharacterSnapshot`.

### 4.4 Remove per-shot drain (`ShotController.cs:393`)
Delete `TournamentRoundContext.DepleteStamina();` (and the surrounding `if (TournamentRoundContext.IsActive)` guard if it wraps only that call — verify at the line).

### 4.5 Persistence (`SaveData` + `SaveBackedEntryStore` + migrator)
- `PersistedTournamentEntry`: `public float conditionRemaining = -1f;` (sentinel).
- `SaveBackedEntryStore.Load` (`new EntryState`, L97): `ConditionRemaining = row.conditionRemaining`. `.Save` (`new PersistedTournamentEntry`, L140): `conditionRemaining = entry.ConditionRemaining`.
- Migrator: bump `CurrentSchemaVersion = 5`; add an empty `v4 → v5` block (new field defaults to `-1` → hydrate-to-full; fail-hard-on-newer preserved). Pre-v5 tournament entries load with full pools.

---

## 5. Persistence summary
One float added per tournament entry. Backward compatible: pre-v5 entries → `-1` sentinel → full pool. The live/solo `PersistedCharacter.conditionEnergy` (Phase 2) is untouched — these are **separate pools** by design.

---

## 6. Anti-cheat re-sim reproducibility (documentation, no engine built)
With per-hole deterministic drain (`DrainForHole()` is a CSV constant) and a frozen `snapshot`, the **condition at each hole is reconstructible** from the persisted entry timeline: `StartedUtc`, each `HoleResult.completedUtc`, `snapshot.Stamina` (tank) + `snapshot.Recovery` (regen). The Option-C `EffectiveStat` is pure → the per-hole degraded stats are reproducible. **Caveat (D3-dependent):** if regen = YES, reconstruction needs the inter-hole elapsed times (client timestamps); if regen = NO, condition is reproducible from drain-count alone (no clock trust). Document the chosen model in the backend XML-doc so the future server re-sim (GDD §8) can mirror it. **No replay code in Phase 3.**

---

## 7. Tests (EditMode)
1. **Register seeds full** — a registered entry's `ConditionRemaining == MaxCondition(snapshot.Stamina)` (configure StaminaModel in setup).
2. **Per-hole drain** — `SubmitHoleResult` reduces `ConditionRemaining` by exactly `DrainForHole()`, clamped ≥0; floor holds at empty.
3. **No per-shot drain** — rewrite `TournamentRoundLoopTests.DepleteStamina_*`: the pool is unchanged by shots within a hole; only hole-complete drains. (Delete the obsolete per-shot assertions.)
4. **BeginRound seeds from entry** — given an entry at `ConditionRemaining = X`, `TournamentRoundContext.StaminaEnergyRemaining == X` (or X+regen under D3) and `StaminaEnergyMax == MaxCondition(snapshot.Stamina)` after `BeginRound`; sentinel `-1` → full.
5. **Persistence round-trip** — Register → Submit (drain) → `_store.Save`/`Load` preserves `ConditionRemaining` within epsilon; pool carries across a simulated relaunch.
6. **Migration v4 → v5** — a v4 save loads, `schemaVersion == 5`, tournament entries get `conditionRemaining == -1` (→ full on use); fail-hard on v6.
7. **(D3=YES) regen between holes** — entry with `LastHoleUtc` 2h ago at Recovery 9 seeds `remaining + 60` (clamped to tank) at BeginRound; no `LastHoleUtc` → no regen.
8. **IsConfigured fallback** — with StaminaModel **not** configured, Register/Submit use the flat fallback and the legacy backend tests pass unchanged (no throw).

---

## 8. Acceptance criteria
1. Project compiles; new + existing EditMode tests green (per-shot tournament tests rewritten to per-hole).
2. A tournament entry's pool seeds to `MaxCondition(snapshot.Stamina)` at Register, drains by `DrainForHole()` per **hole** (not per shot), and **persists across relaunch** (resume restores the drained pool).
3. Degraded Str+ClubControl in tournaments reflect the **real** pool (verified: pool drops hole-over-hole; the Phase-2 seam already consumes it — no `LiveStatProviderHost` edit).
4. Per-shot drain removed; `ShotController:393` no longer calls `DepleteStamina`.
5. Save migrates v4 → v5 cleanly; pre-v5 entries load to full pools.
6. `Golfin.Tournaments` stays cycle-free after adding the `Golfin.Core.Stamina` leaf reference (D1-A).
7. Scope clean: no `LiveStatProviderHost` change, no live/solo-pool change (Phase 2), no roster UI (Phase 4).
8. D3 model (regen YES/NO) implemented as ruled + documented in the backend XML-doc for future re-sim.

---

## 9. Deferred / out-of-scope (explicit)
- **Phase 4:** roster ghost bars (Str/ClubControl) + blue→yellow→red Stamina meter (`StaminaModel.MeterState`) + portrait low-condition icon (`IsLowConditionFlag`; wiring already dormant) — for BOTH live + tournament displays.
- Server-side anti-cheat **re-sim engine** (this phase only guarantees the data + formula are reproducible).
- Any rebalance of `drain_per_hole` / tank / regen tunables beyond `stamina_economy.csv`.
- In-tournament live HUD for the condition pool (if wanted, Phase 4/5).
