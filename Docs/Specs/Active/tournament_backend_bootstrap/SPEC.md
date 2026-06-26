# tournament_backend_bootstrap — Compose + expose the live tournament backend

**Tier:** FULL PIPELINE — integration wiring (runtime singleton init-order, asmdef boundaries, the
silent-null-snapshot trap). Gated by a **composition wireup test** + adapter-mapping tests + an
on-device smoke. No visual fidelity → Rule 8 N/A.
**Depends:** T4 ✓ (`LocalTournamentBackend`), T5 ✓ (`SaveBackedEntryStore`), `tournament_character_snapshot` ✓
(`CharacterManagerStatsProvider`). **Unblocks:** T7/T9 screen Stage-2 binds (live data) **and** T6 (round loop).

---

## 0. What this is — and the boundary
The backend is fully built + tested but **nothing in production ever constructs it** — only tests
`new LocalTournamentBackend(...)`. The screens sit on static data (*"Stage 2 replaces static data with
`ITournamentBackend.GetTournaments()`"*). This task builds the **composition root**: the three missing
Unity adapters + a `TournamentService` that news up `LocalTournamentBackend` with all real seams and
exposes `ITournamentBackend` to the game.

**In scope:** 3 new adapters (RP/items/par), `TournamentService` singleton + a testable `Compose()`,
scene placement, wireup + adapter tests, an on-device smoke.
**Out of scope:** rebinding the screens to `Backend` (that's the T7 Stage-2 / T9 follow-ups — they just
read `TournamentService.Instance.Backend` once this lands); the round loop (T6).

---

## 1. Verified anchors (on disk 2026-06-26)
| Fact | Detail |
|---|---|
| **Backend ctor** | `LocalTournamentBackend(IReadOnlyList<TournamentDefinition> definitions, IReadOnlyDictionary<string,PrizeTable> prizeTables, IReadOnlyDictionary<string,BotFieldConfig> botFields, BotFieldGenerator botGen, ITournamentClock clock, ITournamentEntryStore store, IRewardPointsService rp, IItemRewardService items, IHoleParProvider pars, ICharacterStatsProvider? stats = null)`. **`stats` is optional** → if not passed, snapshots are silently null. **The compose MUST pass it.** |
| Loader | `new TournamentCsvLoader()` → `LoadTournaments()`, `LoadPrizeTables()`, `LoadBotFields()`. Reads `Resources.Load<TextAsset>("Data/<name>").text`. |
| Bot gen | `new BotFieldGenerator(roster, scoreBrackets)`; `FakePlayerRosterParser.Parse(string csvText)`, `BotScoreBracketsParser.Parse(string csvText)` (both `static`, in `BotFieldGenerator.cs`). CSVs: `Resources.Load<TextAsset>("Data/fake_players").text`, `Resources.Load<TextAsset>("Data/bot_score_brackets").text`. |
| Clock | T1 concrete in `Assets/Scripts/Tournaments/ITournamentClock.cs` (confirm the type name — `TimeProviderClock`); `new` it. |
| Store | `new SaveBackedEntryStore()` (T5; wraps `SaveDataHost.Instance.Data.tournamentEntries`). |
| Stats | `new CharacterManagerStatsProvider()` (`Assets/Scripts/TournamentsRuntime/`). |
| **RP target** | `RewardPointsManager.Instance` (`Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs`): `int GetPoints()`, `bool CanAfford(int)`, `bool SpendPoints(int)` (guards negative + affordability), `void EarnPoints(int)`. |
| **Items target** | `SaveDataHost.Instance.Data.itemQuantities` (`Dictionary<string,int>`, `SaveData.cs:80`). *(Note `ballQuantities:77` is separate — tournament item rewards go to `itemQuantities`.)* |
| **Par target** | `HoleDatabaseLoader.RuntimeDatabase` (`static HoleDatabase`, `Assets/Scripts/UI/HoleDatabaseLoader.cs`); each `HoleData.par` (int). Loaded via its `LoadFromCSV()`. |

---

## 2. Three new production adapters
All adapters **resolve their singleton lazily per-call** (read `.Instance` / `RuntimeDatabase` inside
each method, never cache in a field) — resilient to init order + domain reload. Co-locate with
`CharacterManagerStatsProvider` (see §5 asmdef).

**`RewardPointsServiceAdapter : IRewardPointsService`**
- `long Balance` → `RewardPointsManager.Instance.GetPoints()`.
- `bool TrySpend(long amt)` → `RewardPointsManager.Instance.SpendPoints(ToInt(amt))` (SpendPoints already
  guards affordability + returns false if short — no double-check needed).
- `void Grant(long amt)` → `RewardPointsManager.Instance.EarnPoints(ToInt(amt))`.
- **`ToInt(long)`** — RP is int-based; fees/prizes are `long`. Clamp/guard the narrowing: if
  `amt > int.MaxValue` log error + clamp (values are small in practice; never silently overflow).

**`ItemRewardServiceAdapter : IItemRewardService`**
- `void Grant(string itemId, int qty)` → `var d = SaveDataHost.Instance.Data.itemQuantities;
  d[itemId] = (d.TryGetValue(itemId, out var n) ? n : 0) + qty; SaveDataHost.Instance.MarkDirty();`
- Guard `qty <= 0` (no-op) and null/empty `itemId`.

**`HoleParProviderAdapter : IHoleParProvider`**
- `IReadOnlyList<int> ParsFor(string clubId, IReadOnlyList<string> holeSet)` → for each holeId in
  `holeSet`, look it up in `HoleDatabaseLoader.RuntimeDatabase` and take `HoleData.par`.
- **Implementer must bind to the real `HoleDatabase` accessor** (e.g. `GetHole(id)` / `TryGet`) — cite it
  (Rule 8). Confirm the tournament `HoleSet` hole-ids (post-`ExpandHoleSet`) align with `HoleDatabase`
  keys; on a missing hole **throw** with the offending id (a silent default par would corrupt scoring).
- If `HoleDatabase` is not club-scoped, `clubId` is advisory — resolve by hole-id alone; note that in a
  comment.

---

## 3. Composition root — `TournamentService`
A singleton MonoBehaviour (same shape as `CharacterManager`/`RewardPointsManager`) that holds the live
backend, **plus a static `Compose()` so the wiring is unit-testable** (the MonoBehaviour is a thin shell).

```csharp
public sealed class TournamentService : MonoBehaviour
{
    public static TournamentService Instance { get; private set; } = null!;
    public ITournamentBackend Backend { get; private set; } = null!;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        Backend = Compose();
    }

    // Pure-ish factory — testable. Reads CSV/Resources + news up adapters.
    public static ITournamentBackend Compose()
    {
        var loader = new TournamentCsvLoader();
        var defs   = loader.LoadTournaments();
        var prizes = loader.LoadPrizeTables();
        var fields = loader.LoadBotFields();

        var roster   = FakePlayerRosterParser.Parse(LoadText("Data/fake_players"));
        var brackets = BotScoreBracketsParser.Parse(LoadText("Data/bot_score_brackets"));
        var botGen   = new BotFieldGenerator(roster, brackets);

        return new LocalTournamentBackend(
            defs, prizes, fields, botGen,
            new TimeProviderClock(),               // confirm concrete name
            new SaveBackedEntryStore(),
            new RewardPointsServiceAdapter(),
            new ItemRewardServiceAdapter(),
            new HoleParProviderAdapter(),
            new CharacterManagerStatsProvider());  // ← MUST be passed (else snapshots null)
    }

    private static string LoadText(string path)
    {
        var asset = Resources.Load<TextAsset>(path);
        if (asset == null) throw new InvalidOperationException($"Missing TextAsset: {path}");
        return asset.text;
    }

    private void OnDestroy() { if (Instance == this) Instance = null!; }
}
```

**Init-order:** `Compose()` only touches `Resources` + the headless loaders/parsers at construction —
the singleton-backed adapters resolve lazily at call-time, so `TournamentService` has **no Awake-order
dependency** on `SaveDataHost`/`CharacterManager`/`HoleDatabaseLoader`/`RewardPointsManager`. Default
exec order is fine. (If `HoleDatabaseLoader.RuntimeDatabase` is null when `GetLeaderboard` first runs,
that's a load-ordering bug in the par adapter's lazy read — surface it loudly, don't default the par.)

---

## 4. Scene placement
Add a `TournamentService` component to the persistent/bootstrap GameObject in the shell scene (alongside
`CharacterManager`, `RewardPointsManager`, `SaveDataHost`) so `DontDestroyOnLoad` keeps one instance.
*(Scene edit = Unity MCP / implementer.)*

## 5. Asmdef
Place the 3 adapters + `TournamentService` in the **same assembly as `CharacterManagerStatsProvider`**
(`Assets/Scripts/TournamentsRuntime/`) — it already bridges `Golfin.Tournaments` (headless) and the
Unity managers. Ensure that assembly references: `Golfin.Tournaments`, `Golfin.Save`, and the
assemblies holding `CharacterManager` / `RewardPointsManager` / `HoleDatabaseLoader` / `SaveDataHost`.
If `TournamentsRuntime` has no `.asmdef` yet (it currently doesn't), either add one with those refs or
confirm Assembly-CSharp placement compiles. **Confirm the final asmdef graph compiles before PASS.**

---

## 6. Acceptance
**Wireup test (the gate — convert the silent-null trap into a caught regression):**
- `TournamentService.Compose()` returns non-null; `GetTournaments().Count == 6` (the CSV roster).
- A `Register("kasumigaseki_open", 0, someCharId)` yields an entry whose **`Snapshot` is non-null** with
  the character's level + 4 stats — proves the stats provider was actually wired (guards the
  `_stats?.` optional-injection trap).
- *(Runs in PlayMode if the adapters need live singletons; if `Compose()` can run headless with the CSVs
  present in test Resources, prefer EditMode. Implementer picks; document which.)*

**Adapter-mapping tests (EditMode where feasible):**
- RP `ToInt` clamps/guards overflow; `TrySpend` returns false when short (stub or live RewardPointsManager).
- Items: `Grant` increments existing key, creates missing key, no-ops on `qty<=0`; `MarkDirty` called.
- Par: `ParsFor` returns pars in hole-set order; throws on an unknown hole id.

**On-device smoke (manual, report in IMPLEMENTER_REPORT):** launch the build → the live backend
constructs without exceptions → `GetTournaments()` returns the 6 tournaments.

## 7. Decisions for Cesar (non-blocking — recommend + proceed)
- **D1 — service shape:** MonoBehaviour singleton `TournamentService.Instance.Backend` (rec — matches
  `CharacterManager`/`RewardPointsManager`, what the screens already expect). Alt: a static service
  locator. **Rec: MonoBehaviour singleton.**
- **D2 — screen rebinds in-scope?** **Rec: NO** — keep this task to adapters+service+smoke; let T7
  Stage-2 / T9 bind to `Backend` as fast-follows (smaller, independently reviewable). Say the word to
  fold the T7/T9 binds in.

## Source links
Backend ctor: `Assets/Scripts/Tournaments/LocalTournamentBackend.cs:61`. Loader: `TournamentCsvLoader.cs`.
Parsers: `BotFieldGenerator.cs:55,91`. Adapters' targets: `RewardPointsManager.cs`, `SaveData.cs:80`,
`HoleDatabaseLoader.cs` + `HoleData.par`. Existing adapter pattern: `TournamentsRuntime/CharacterManagerStatsProvider.cs`.
Screens awaiting bind: `UI/Tournaments/TournamentSelectionScreenController.cs:15`.
