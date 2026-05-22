# SPEC — `save_layer_reactive_foundation`

**Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status

See `STATUS.md`. **PIPELINE_READY** — Q-locks recorded in §4, best-practice patches applied in §5. Fires FULL PIPELINE on Implementer kickoff.

## Goal

Replace the patchwork of in-memory + PlayerPrefs state across CharacterManager / RewardPointsManager / BallManager / ItemManager / HoleProgressionService with a single **reactive SaveData layer**: one canonical save record, one persister abstraction, one `OnChanged` event. All systems become read-through facades over SaveData. First persister = local JSON. Cloud sync = a later swap of the persister, zero refactor of consumers.

Architecturally this is the foundation. Every system shipped post-Loop-v2 (Rankings, real Matchmaking, Shop, Gacha) writes through this layer. Doing it now lets every downstream system land cleanly; doing it later means refactoring all of them once SaveData arrives.

## Pre-flight findings (locked 2026-05-22)

Audit of current state-holders:

| System | Current persistence | What's lost on app restart |
|---|---|---|
| `RewardPointsManager` | **PlayerPrefs** (`GOLFIN_REWARD_POINTS` int key) | Nothing — points already persist |
| `CharacterManager` | None — `ownedCharacters` dict rebuilt from CSV on Awake | Levels, SP spent, selected character |
| `BallManager` | None — `ownedBalls` dict re-seeded from CSV on Awake (test quantities `99` / `-1`) | Ball quantities, anything earned |
| `ItemManager` | None — `ownedItems` dict re-seeded from CSV on Awake (test quantity `99`) | Item quantities, anything earned |
| `HoleProgressionService` | **None** — POCO singleton with in-memory dicts; the file's own comment says "When real save state lands (Loop v2), this service becomes the read API over the save layer" | **Hole unlocks + first-clear flags** (this is the big one — the Stage E REPLAY fix we just shipped writes to a runtime dict that resets every launch) |
| `GameSession` | None | Current hole / current character / shot history — **correctly transient**, must NOT be persisted (resets at hole boundaries) |

**Critical finding:** the Stage E REPLAY fix (`OnReplay` writes progression) currently writes to in-memory state only. Until this SPEC ships, "Hole 2 unlocks after clearing Hole 1" survives only as long as the app is open. Save layer makes our recent work meaningful long-term.

**Existing PlayerPrefs usage:** only `RewardPointsManager`. Migration is one-time: on first SaveData load, if SaveData JSON file is absent but the legacy PlayerPrefs key exists, hydrate `rewardPoints` from it and immediately write SaveData. Then strip PlayerPrefs writes from `RewardPointsManager`. No data loss for existing players (only Cesar's local dev install — but right thing to do anyway).

## Architecture (locked by Cesar)

### Top-level shape

```
Golfin.Save (new asmdef under Assets/Scripts/Save/)
├── SaveData.cs              // pure C# class — all persisted state
├── SaveDataHost.cs          // thin MonoBehaviour singleton on Managers GO
├── ISavePersister.cs        // interface; LocalJsonPersister implements
├── LocalJsonPersister.cs    // Application.persistentDataPath/save.json
├── SaveSchemaMigrator.cs    // versioned migrations on load
└── Tests/                   // EditMode tests for migrator + round-trip
```

### SaveData shape

```csharp
[Serializable]
public class SaveData
{
    public int schemaVersion = 1;                                      // bump on any schema change
    public int rewardPoints;
    public string selectedCharacterId;
    public List<PersistedCharacter> ownedCharacters;                   // level, SP spent per stat, isSelected
    public Dictionary<string, int> ballQuantities;                     // ballId → qty
    public Dictionary<string, int> itemQuantities;                     // itemId → qty
    public List<int> unlockedHoles;                                    // serialize as List for JSON, hydrate to HashSet on load
    public List<int> playedHoles;
}
```

`PersistedCharacter` is a flat DTO mirroring the persisted subset of `PlayerCharacterData` (do NOT serialize `PlayerCharacterData` itself — separating storage from runtime types prevents migration pain when runtime types add UI-only fields).

### Reactive event model

- **Single event**: `SaveDataHost.OnSaved` (fires after a successful write to disk, not on every in-memory mutation).
- **Why not per-field events**: each system already has its own `OnChanged` (RewardPointsManager.OnPointsChanged, BallManager.OnInventoryChanged, etc.) that's the right grain for UI. The save event is for "I just persisted; sync clients can know the disk is authoritative now."
- **Pattern**: every mutation in a system → system fires its own OnChanged → SaveDataHost listens to all systems' OnChanged events → debounces writes (250ms tail) → on flush, writes JSON + fires OnSaved.
- **Debounce avoids write-amp**: 18 hole loads in quick succession (worst case bot run) become 1 disk write at the tail.

### Persister abstraction

```csharp
public interface ISavePersister
{
    bool TryLoad(out string json);
    Task SaveAsync(string json);
}
```

`LocalJsonPersister` writes to `Application.persistentDataPath + "/save.json"`. Future `CloudSyncPersister` swaps in here. SaveDataHost owns one `ISavePersister` reference; injection happens at boot via a single line in `Bootstrap` (or `ManagersBootstrap` — whatever the singleton-host GameObject is called).

**CRITICAL: Atomic file writes (non-negotiable).** A naive `File.WriteAllText(path, json)` is a well-documented save-corruption vector: power loss or app kill mid-write leaves a partially-written or zero-byte file, and cloud-sync layers (Steam, iCloud, Google Play Saves) will happily overwrite the previous good save with the corrupted one. Industry-standard fix is the temp-file-rename pattern, which is what `LocalJsonPersister` MUST use:

1. `await File.WriteAllTextAsync(tmpPath, json)` where `tmpPath = path + ".tmp"`
2. `File.Replace(tmpPath, path, destinationBackupFileName: null)` — atomic on both POSIX (`rename`) and Windows (`MoveFileExW(MOVEFILE_REPLACE_EXISTING)`)
3. If `path` doesn't exist yet (first save), use `File.Move(tmpPath, path)` instead

No `fsync` needed — .NET's `WriteAllTextAsync` flushes its stream on dispose, and `File.Replace` is durable across kernel-level write reorderings on the platforms we ship to.

**Async disk I/O.** All persister writes run async on a background thread (`File.WriteAllTextAsync`) so the main thread never blocks on disk. The in-memory mutation in each manager stays sync (it's just dict updates); only the disk write goes async. This is critical on mobile where a sync write during a hole-complete can cause a perceptible frame hitch.

**Dictionary serialization.** Unity's `JsonUtility` does NOT serialize `Dictionary<TKey, TValue>` directly. SaveData uses **Newtonsoft.Json** (`com.unity.nuget.newtonsoft-json`, ships with Unity 6, already available in this project) for native dict + nested-object support. SaveData class itself does not need `[Serializable]` — Newtonsoft picks up public fields by default. This is cleaner than the alternative of wrapping every dict as a `List<{key, value}>` pair list.

### Schema versioning

- `schemaVersion` field in every JSON file. Day-1 value = `1`.
- `SaveSchemaMigrator` is a switch on the version number. v1 → v2 migration is a function. New version = new function added, no version is ever removed. Reading older saves works forever.
- On load, if the JSON file is missing, we try the legacy PlayerPrefs key for `GOLFIN_REWARD_POINTS` and seed `rewardPoints` from it (one-time migration). Else, fresh SaveData with defaults.

### System refactors

Each existing manager becomes a read-through facade. The mutations stay on the manager (because the manager owns business logic like "level-up costs RP"), but the manager writes to SaveData under the hood and fires its own existing OnChanged.

**RewardPointsManager:**
- Delete the PlayerPrefs read/write code entirely.
- `currentPoints` becomes a property reading/writing `SaveDataHost.Instance.Data.rewardPoints`.
- `OnPointsChanged` still fires from `SpendPoints` / `EarnPoints` / `SetPoints` exactly as today.

**CharacterManager:**
- `ownedCharacters` dict now hydrates from BOTH the CSV (template/base data) AND SaveData (player-specific level/SP). On Awake: build dict from CSV templates, then overlay player data from SaveData.ownedCharacters by characterId.
- After `LevelUp` mutates `playerChar.currentLevel`/`totalSPEarned`, sync the change to SaveData.ownedCharacters entry for that id.

**BallManager / ItemManager:**
- Same pattern — InitializeBalls/Items still seeds defaults from CSV, then overlays quantities from SaveData. Mutators (`AddBalls`, `AddItems`, `UseItem`) write through to SaveData.

**HoleProgressionService:**
- Stop being a POCO singleton. Becomes either (a) a static facade over SaveData.unlockedHoles/playedHoles, or (b) keeps the singleton shape but the dicts become views over SaveData. (b) is less churn — recommend (b).
- The comment in the file ("When real save state lands (Loop v2), this service becomes the read API over the save layer") becomes literal — delete the comment after wiring.

### Boot ordering

Script Execution Order matters. New requirement:
1. `SaveDataHost` — must Awake **before** any manager that reads SaveData
2. CSV databases (CharacterDatabaseCSV, BallDatabaseCSV, etc.) — unchanged, already first
3. Managers (CharacterManager, BallManager, ItemManager, RewardPointsManager) — after both

`SaveDataHost` is configured in Project Settings → Script Execution Order with priority `-100` (or whatever slot is one before the CSVs at `-50`).

## §4 — Q-LOCKS (locked by Cesar 2026-05-22 ~14:30 CEST)

| # | Question | Lock | Notes |
|---|---|---|---|
| Q1 | SaveData identity model. | **Single slot for v1.** | Multi-slot is its own future task; cloud sync makes the account-vs-slot distinction the cloud's problem. |
| Q2 | Migration on schema bump — fail-hard or fail-soft? | **Fail-hard.** | If schemaVersion in file > schemaVersion in code, refuse to load and surface a toast/log. Silent data loss is worse than a clear "please update" message. |
| Q3 | Write trigger granularity. | **Debounced 250ms.** | Every system OnChanged event triggers a tail-debounced write; app-pause still hooks a final synchronous flush. |

## §5 — Best-practice scan (locked 2026-05-22 ~14:30 CEST)

Before committing this SPEC, Architect ran a best-practice scan against current Unity-mobile save-system literature. Three additions landed in §Architecture + §Definition of done:

1. **Atomic file writes via temp + `File.Replace`.** A naive `File.WriteAllText` is a documented save-corruption vector when the app is killed mid-write (Steam Cloud / iCloud / Google Play Saves will then overwrite the previous good save with the corrupted one). Mandatory.
2. **Async I/O (`File.WriteAllTextAsync`) on the disk path.** Prevents frame hitches on mobile during hole-complete writes.
3. **Newtonsoft.Json over JsonUtility.** Project is on Unity 6 + `com.unity.nuget.newtonsoft-json` ships with the engine. Newtonsoft serializes `Dictionary<TKey, TValue>` natively; JsonUtility doesn't.

All three are additive. Architecture and Q-locks unchanged.

## Definition of done

- [ ] `Assets/Scripts/Save/` exists with `Golfin.Save` asmdef and the 5 files listed above
- [ ] `SaveData.cs` defines the schema in §Architecture; `schemaVersion = 1`
- [ ] `LocalJsonPersister` round-trips: write → read → struct-equal
- [ ] **Atomic writes verified:** `LocalJsonPersister.SaveAsync` writes to `save.json.tmp` then `File.Replace`s; an EditMode test that kills the write mid-stream (or simulates by writing only the temp file then asserting `save.json` is untouched) proves the source file is never partially overwritten
- [ ] **Async I/O verified:** persister uses `File.WriteAllTextAsync`; no `File.WriteAllText` synchronous calls anywhere in `Golfin.Save`
- [ ] **Newtonsoft.Json** referenced from the `Golfin.Save` asmdef; `Dictionary<string, int>` round-trip test passes
- [ ] One-time PlayerPrefs migration: if save.json missing AND `GOLFIN_REWARD_POINTS` key present, hydrate rewardPoints and write save.json on first Awake
- [ ] All 5 systems refactored: RewardPointsManager / CharacterManager / BallManager / ItemManager / HoleProgressionService — each reads/writes through SaveData
- [ ] PlayerPrefs write code removed from RewardPointsManager (the legacy read can stay for migration only — gated to "save.json doesn't exist yet")
- [ ] `OnSaved` event fires after every disk write (post-`File.Replace`)
- [ ] Debounced writes (250ms tail) — verified by EditMode test that fires 10 OnChanged events in 50ms and asserts 1 write
- [ ] App-pause hook flushes pending writes (Application.focusChanged or OnApplicationPause handler in SaveDataHost) — must `await` the final flush before returning from OnApplicationPause(true)
- [ ] Script Execution Order updated; SaveDataHost slot is `-100` (or earliest manager slot)
- [ ] EditMode tests: SaveData round-trip, schema v1 migration from PlayerPrefs, OnSaved event firing, debounce coalescing, **atomic-write resilience**, **Dictionary round-trip via Newtonsoft**
- [ ] Smoke-bot scenario added (or extends existing Hole1PlayNext): play hole, exit to menu, restart bot, confirm hole 2 is unlocked + rewards are persisted

## Out of scope

- Cloud sync — separate task, just adds a CloudSyncPersister
- Multiple save slots — separate task
- Save file encryption / tamper-detection — separate task, not P1 for offline single-player
- Settings persistence (audio levels, language) — keep PlayerPrefs for those; this layer is for game-state data, settings are platform-bus territory
- HoleData CSV schema extension for per-reward tier IDs — separate task

## Pipeline

FULL PIPELINE recommended (visual fidelity = no, but cross-system architecture surface + new asmdef + 5 system refactors + persistence semantics = beyond TELLCODE scope).

Estimate: 1.5–2 days for the full pipeline including pipeline overhead.
