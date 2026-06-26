# tournament_character_snapshot — Freeze character state per tournament entry

**Tier:** TELLCODE (additive seam, established pattern) — **gated by an EditMode freeze-invariant test.**
**Reopens:** shipped T4 (`LocalTournamentBackend`) + T1 DTO (`EntryState`). Additive only; does not change T4 scoring.
**Depends on:** T1 ✓, T4 ✓. **Blocks:** T5 (must persist the snapshot) and T6 (consumes it).

---

## 1. Decision being implemented (owner ruling)

> **Fixed character AND fixed stats for the whole tournament.** Freeze the character state at
> sign-up and treat the tournament character as a **separate, immutable snapshot.** Changing the
> selected character or leveling up the roster character must **not** affect an in-flight tournament.

So a tournament **entry carries a frozen `CharacterSnapshot`** captured at **`Register` (sign-up)**,
persisted with the entry, and read for every round. Freeze point = sign-up, which can precede play by
days — so capture happens at `Register`, **not** at round-start.

---

## 2. What gets frozen (grounded in the real model)

Gameplay reads a character's **effective stats** off `Golfin.Roster.PlayerCharacterData`
(`Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs`):

- `currentLevel` (int)
- `currentStrength`, `currentClubControl`, `currentRecovery`, `currentStamina` (int)

These `current*` values are already the **final capped effective stats** — `RefreshStatValues`
computes `Mathf.Min(base + spent, RarityStatCaps.GetStatCaps(rarity).cap)`. So freezing the four
`current*` + `currentLevel` captures the full gameplay state; **no need to store raw SP or rarity.**

**Out of scope (do NOT freeze):**
- **Stamina ENERGY** (`currentStaminaEnergy` / `maxStaminaEnergy`, `[NonSerialized]`) — the depleting
  per-hole bar. That is per-tournament **runtime** state owned by T6's round loop, not part of the
  frozen stat block. (The frozen Stamina **STAT** = `currentStamina` sets the ceiling; how energy
  depletes/refills across a tournament — and whether the tournament character gets its own pool — is a
  **T6 sub-decision**, flagged not resolved here.)

---

## 3. New type — `CharacterSnapshot` (Golfin.Tournaments, headless)

Immutable value; primitives only (trivially serializable for T5). No `UnityEngine`.

```
public sealed class CharacterSnapshot   // or readonly struct
{
    public string CharacterId { get; }
    public int    Level        { get; }
    public int    Strength     { get; }
    public int    ClubControl  { get; }
    public int    Recovery     { get; }
    public int    Stamina      { get; }   // the STAT, not energy
    // ctor-only assignment; value-equality helps tests
}
```

---

## 4. New seam — `ICharacterStatsProvider`

Pure interface (no `UnityEngine`) so the backend stays headless — same pattern as
`IRewardPointsService` wrapping `RewardPointsManager`.

```
public interface ICharacterStatsProvider
{
    CharacterSnapshot SnapshotFor(string characterId);
}
```

- **Production adapter** `CharacterManagerStatsProvider` (the only Unity-touching part): reads
  `CharacterManager.Instance.GetCharacterData(characterId)` → `PlayerCharacterData?`
  (`Assets/Scripts/CharacterManager.cs`; alias `GetPlayerCharacter`), copies `currentLevel` +
  the four `current*` into a `CharacterSnapshot`. If the id is unknown (`null`), throw or return a
  documented zero-snapshot — **throw** (registering an unknown character is a programmer error).
  Implementer may call `RefreshStatValues(id)` first defensively, but `current*` are maintained on
  every SP confirm so a direct read is acceptable; cite the chosen path.
- **Test fake** `FakeStatsProvider` — returns a caller-supplied snapshot per id; **mutable source**
  so a test can change it after `Register` to prove the freeze.

---

## 5. Wiring into T4 `LocalTournamentBackend`

- **Ctor** gains `ICharacterStatsProvider stats` (append param; production passes
  `new CharacterManagerStatsProvider()`, tests inject `FakeStatsProvider`).
- **`EntryState`** (T1 DTO, `Assets/Scripts/Tournaments/`) gains additive field
  `CharacterSnapshot Snapshot` alongside existing `CharacterId` (which stays; equals
  `Snapshot.CharacterId`). Justified additive DTO change — same precedent as T2 adding
  `ResolveDelayMinutes` to `TournamentDefinition`.
- **`Register(id, entryPaymentRP, characterId)`** — at the point it constructs the new `EntryState`
  (after RP debit + idempotency check), call `stats.SnapshotFor(characterId)` and store the result on
  `EntryState.Snapshot`. **This is the freeze.** No other method captures stats.
- Scoring/leaderboard logic is **untouched** — T4 ranks on strokes/time, never on stats.

---

## 6. Tests (extend `LocalTournamentBackendTests` — EditMode gate)

1. **Captures from provider:** `Register` with a `FakeStatsProvider` returning a known snapshot →
   `GetMyEntry(id).Snapshot` equals it (id, level, 4 stats).
2. **Freeze invariant (the gate):** Register; then **mutate the FakeStatsProvider's source** for that
   character (simulate level-up / re-allocation / swap); `GetMyEntry(id).Snapshot` is **unchanged**.
3. **Round-trip:** snapshot survives `ITournamentEntryStore.Save` → `Load` intact (uses
   `InMemoryEntryStore`; the persisted-store mapping is T5's responsibility and gets its own
   round-trip test there).
4. **Unknown character:** `SnapshotFor` on an unknown id throws (adapter contract).

---

## 7. Scope boundaries

- **In:** `CharacterSnapshot`, `ICharacterStatsProvider` + adapter + fake, `EntryState.Snapshot`,
  `Register` populates it, the four tests above, ctor wiring at production call sites.
- **Out:** persistence of the snapshot (**T5**), consumption for gameplay physics + stamina-energy
  model (**T6**), any UI showing the frozen character.

## 8. Open items (non-blocking)

- **D-snapshot-shape:** include rarity in the snapshot? **Rec: NO** — caps are already baked into
  `current*`; `CharacterId` lets UI re-derive rarity/name/art for display. (Decide if T6 needs it.)
- **T6 sub-decision (flagged):** does the tournament character get its **own** stamina-energy pool
  (depletes within the tournament, independent of the roster character)? "Treat like a separate
  character" implies yes. Resolve in the T6 spec.
