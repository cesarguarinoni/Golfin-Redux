# Tournament Save Entry (T5) — Spec

> **Order:** EPIC 500 Phase C · the `Golfin.Save`-backed persistence for tournament entries: new `PersistedTournamentEntry` DTO + `SaveData` list field + `schemaVersion 2→3` migrator + the `SaveBackedTournamentEntryStore` that swaps T4's in-memory store.
> **Depends:** T1 ✓ (DTOs). **Parallel to T4** (both from T1; they meet at T6). T4 ships with an in-memory `ITournamentEntryStore`; T5 provides the disk-backed impl behind the **same seam** — so T5 can land independently and be wired in at T6.
> **Design source:** `Tournaments_GDD.md` §12 (save schema); `Tournaments_Implementation_Plan.md` T5 (lines 60–67).
> **Tier:** FULL PIPELINE — *save schema = risk*. Gated by an EditMode test suite (migration / round-trip / debounce / atomic-write), extending the existing `SaveLayerTests`. No visuals → Rule 8 N/A.

---

## 0. What T5 is — and the boundary
Two halves:
1. **Schema (in `Golfin.Save`):** add `PersistedTournamentEntry` + `PersistedHoleResult` flat DTOs and a `List<PersistedTournamentEntry> tournamentEntries` field to `SaveData`; bump `schemaVersion 2→3`; add the `v2→v3` migrator step (preserve fail-hard-on-newer).
2. **Store adapter (in `Golfin.Tournaments`):** `SaveBackedTournamentEntryStore : ITournamentEntryStore` (the seam T4 defines) that maps `EntryState`/`HoleResult`/`EntryStatus` ⇄ the persisted DTOs, reads/writes `SaveDataHost.Instance.Data.tournamentEntries`, and calls `MarkDirty()` after each mutation.

**Boundary:** T5 does **not** change tournament *logic* (that's T4) and does **not** drive the round loop (T6). It only persists/restores what T4 produces. The flat-DTO mandate is explicit in `SaveData.cs`: *"Do NOT serialize PlayerCharacterData… directly. Use the flat DTO types… to decouple storage from runtime."* — T5 follows the `PersistedCharacter` precedent exactly.

---

## 1. Verified anchors (on disk 2026-06-26)
| Fact | Detail |
|---|---|
| **Serializer** | **Newtonsoft.Json** (`JsonConvert.Serialize/DeserializeObject`, `Formatting.Indented`). `Dictionary`, `DateTime`, `DateTime?`, and enums **round-trip natively** — no ticks/string conversion needed (unlike Unity `JsonUtility`). |
| `SaveData` | `Golfin.Save`; `schemaVersion = 2`; flat fields + `List<PersistedCharacter>` + two `Dictionary<string,int>`. The DTO-mirror precedent for T5. |
| `PersistedCharacter` | the flat-DTO template: plain mutable public fields, parameterless (Newtonsoft-friendly). **T5's DTOs mirror this style.** |
| `SaveDataHost` | singleton MonoBehaviour (exec order −100); `.Instance`, `.Data` (live `SaveData`), `MarkDirty()` (debounced 250 ms write), `ReloadFromDisk()` (test: simulate restart), `SetPersister(ISavePersister)` (test injection), `OnSaved`. |
| `SaveSchemaMigrator` | `CurrentSchemaVersion = 2`; `Migrate(data)` fail-hard if file > code; sequential `if (data.schemaVersion < N)` steps; ends `data.schemaVersion = CurrentSchemaVersion`. v1→v2 is the pattern to copy (new fields default fine; just bump). |
| `EntryState` (to persist) | `TournamentId`, `CharacterId`, `PerHole : IReadOnlyList<HoleResult>`, `StartedUtc`, `LastHoleUtc : DateTime?`, `Status : EntryStatus`. |
| `HoleResult` (to persist) | `HoleId` (string), `Strokes` (int), `TimeSeconds` (float), `CompletedUtc` (DateTime), `RngSeed` (int), `InputLog : IReadOnlyList<ShotCommand>`. **Sealed, ctor-only** → must be mirrored by a flat DTO, not serialized directly. |
| Tests | `Assets/Scripts/Save/Tests/SaveLayerTests.cs` — extend its patterns. |
| Seam (from T4) | `ITournamentEntryStore` in `Golfin.Tournaments` — T5 implements it disk-backed. **Asmdef: `Golfin.Tournaments → Golfin.Save`** (one-way). |

---

## 2. Schema additions (in `Golfin.Save` — NO `Golfin.Tournaments` references)
> The asmdef points Tournaments→Save, never the reverse. So these DTOs use **primitives only** — `EntryStatus` is stored as **`int`** (cast happens in the store adapter on the Tournaments side), never the enum type.

```csharp
// Golfin.Save — flat, mutable, parameterless (Newtonsoft-friendly), no runtime-type refs
public class PersistedTournamentEntry
{
    public string tournamentId = "";
    public string characterId   = "";            // locked at sign-up
    public List<PersistedHoleResult> perHole = new();
    public string startedUtc = "";               // ISO-8601 (Newtonsoft DateTime ok; string keeps Save dep-free + explicit)
    public string lastHoleUtc = "";              // "" = none (maps to DateTime?)
    public int    status;                        // (int)EntryStatus — enum order frozen
    public bool   claimed;                       // prize-claim idempotency (see D2)
}

public class PersistedHoleResult
{
    public string holeId = "";
    public int    strokes;
    public float  timeSeconds;
    public string completedUtc = "";             // ISO-8601
    public int    rngSeed;                        // kept for future server re-sim (GDD §8)
    // inputLog intentionally omitted in v1 — see D1
}
```
Add to `SaveData`: `public List<PersistedTournamentEntry> tournamentEntries = new List<PersistedTournamentEntry>();`

*(DateTimes stored as ISO strings, not raw `DateTime`, so the `Golfin.Save` DTOs stay free of `DateTime?` ambiguity and remain trivially diff-able in the JSON; the adapter does `DateTime.Parse`/`ToString("O")`. If you prefer raw `DateTime`/`DateTime?` Newtonsoft handles both — see D3.)*

---

## 3. Migrator (`v2 → v3`)
- Bump `SaveSchemaMigrator.CurrentSchemaVersion = 3`.
- Add after the v1→v2 block:
```csharp
if (data.schemaVersion < 3)
{
    data.tournamentEntries ??= new List<PersistedTournamentEntry>(); // defensive; Newtonsoft leaves missing key at field default
    data.schemaVersion = 3;
    Debug.Log("[SaveSchemaMigrator] Migrated v2 → v3 (tournament entries list added, default empty).");
}
```
- **Preserve** fail-hard-on-newer (`> CurrentSchemaVersion` throws `SaveSchemaVersionException`). A v3 build reading a v2 save = empty entries list (correct); a v2 build reading a v3 save = hard fail (no silent loss).

---

## 4. Store adapter (in `Golfin.Tournaments`)
`SaveBackedTournamentEntryStore : ITournamentEntryStore` — the production impl T4 injects (tests still use T4's in-memory fake).
- `EntryState? Load(string tid)` → find `tournamentEntries` row by id → map to `EntryState` (rebuild `HoleResult`s with empty `InputLog`; `status = (EntryStatus)row.status`; parse ISO dates; `""` lastHoleUtc → null).
- `void Save(EntryState e)` → upsert the row (replace by `tournamentId`), map `EntryState`→`PersistedTournamentEntry`, then `SaveDataHost.Instance.MarkDirty()`.
- **Claim persistence (D2):** seam exposes `bool IsClaimed(string tid)` / `void MarkClaimed(string tid)` → read/set `row.claimed` + `MarkDirty()`. (T4's `ClaimPrize` calls these for once-only grants.)
- All reads/writes go through `SaveDataHost.Instance.Data.tournamentEntries`. No direct disk I/O — `MarkDirty()` owns the debounced atomic write.

---

## 5. Acceptance — EditMode suite (the gate; extend `SaveLayerTests`)
- **v2→v3 migration:** a hand-built v2 JSON (no `tournamentEntries`) loads → `schemaVersion==3`, `tournamentEntries` empty, all v2 fields intact. v3-reading-v2 path.
- **Fail-hard:** a v4 JSON throws `SaveSchemaVersionException` (regression guard on the existing Q2 rule).
- **Round-trip:** `EntryState` (multi-hole, with `DateTime?` set and null) → `Save` → serialize → deserialize → `Load` → field-equal (ids, per-hole strokes/time/completedUtc/rngSeed, status, startedUtc, lastHoleUtc, claimed).
- **Upsert:** two `Save` calls for the same `tournamentId` replace (not duplicate) the row.
- **Claim:** `MarkClaimed` persists; `IsClaimed` true after reload; idempotent.
- **Debounce coalescing:** N appends within 250 ms → one disk write (assert via `OnSaved` count / persister spy) — reuse `SaveLayerTests` debounce harness.
- **Atomic-write resilience:** simulate restart via `ReloadFromDisk()` after a hole append → entry survives (extend the existing durability test).

---

## 6. Staging
- **Stage 1** — `Golfin.Save` DTOs (`PersistedTournamentEntry`, `PersistedHoleResult`) + `SaveData.tournamentEntries` + migrator `v2→v3` + migration/fail-hard/round-trip tests *(the risk surface — land + prove first)*.
- **Stage 2** — `SaveBackedTournamentEntryStore` adapter (map ⇄, upsert, claim, `MarkDirty`) + store round-trip / upsert / claim / debounce / restart tests.
- *(Wiring into production `LocalTournamentBackend` happens at T6, or as a one-line swap once T4 lands — the seam makes it a constructor change, no logic touched.)*

---

## 7. Decisions for Cesar
- **D1 — persist `inputLog`?** **Rec: no (v1).** Store `rngSeed` only (1 int, cheap, future-proofs server re-sim). `HoleResult.InputLog` *"may be empty (cheap v1 implementation)"* — persisting full shot logs bloats every save before any server exists to verify them. Revisit when the remote backend lands (GDD §8).
- **D2 — claim-state shape (coordination with T4, in flight).** **Rec:** `bool claimed` on `PersistedTournamentEntry` + `IsClaimed`/`MarkClaimed` on `ITournamentEntryStore`. T4's `ClaimPrize` idempotency needs persisted claim state; the cleanest home is the entry row. → *confirm T4's `ITournamentEntryStore` carries (or T5 extends it with) the two claim methods.*
- **D3 — DateTime as ISO string vs raw `DateTime?`.** Spec uses **ISO strings** (explicit, diff-friendly, keeps `Golfin.Save` obviously primitive). Newtonsoft handles raw `DateTime`/`DateTime?` too — say the word if you'd rather store native types and drop the parse step.

---

## Source links
Plan: `Tournaments_Implementation_Plan.md` T5 (60–67), dep graph. GDD §12. Save: `Assets/Scripts/Save/{SaveData,SaveDataHost,SaveSchemaMigrator}.cs` + `Tests/SaveLayerTests.cs`. DTOs: `Assets/Scripts/Tournaments/{EntryState,HoleResult,TournamentEnums}.cs`. Seam: `ITournamentEntryStore` (T4, `Golfin.Tournaments`).
