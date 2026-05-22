# SELF_REVIEW — `save_layer_reactive_foundation`

- **Reviewer:** golfin-self-reviewer
- **Date:** 2026-05-22 (afternoon, CEST)
- **Iteration:** N = 1 (first self-review for this task)
- **Verdict:** **BACK_TO_IMPLEMENTER** (FAIL)
- **STATUS set to:** `SELF_REVIEW_FAIL`
- **Task type:** Non-visual architecture task — SPEC §Pipeline declares "visual fidelity = no". No Figma reference exists by design; no Figma side-by-side performed.

---

## Step 1 — Independent screenshot scan (`s05_restart_simulated_hole2_persisted`)

The screenshot shows a genuine play-mode 3D golf scene: a tree-lined fairway, the GOLFIN-branded ball teed up in a white tee cup, and a full HUD. Top-left chip stack reads "JAMES / Lv 10 / TURN 1" with a character portrait; top-right chip stack reads "LOMOND / **HOLE 1 - REGULAR** / PAR 5". Bottom shows shot controls (SPIN, GOLFIN ball, STRAIGHT, DRIVER 0 yds) and a gear button top-right. It is a real rendered frame — NOT a white box, error screen, or stale/blank capture.

**Discrepancy noted:** the file is named `s05_restart_simulated_hole2_persisted` and the IMPLEMENTER_REPORT § Screenshot says it is "the Hole 2 armed state after simulated restart" with "Scene loaded: LabScaffold + Hole_02_Geo". The HUD plainly reads **HOLE 1 PAR 5** (Lomond). `s04_hole2_armed` shows the identical Hole 1 HUD too (different md5, so not byte-identical, but visually the same Hole 1 frame). The smoke-bot history.log line 26 confirms `Hole_02_Geo` did load, so the *scene* is Hole 2 — the HUD's `HoleContext` simply did not repopulate on the lab scene swap (a known lab-scaffold stale-HUD artifact). This does NOT invalidate the SaveData durability proof (which is verified by log assertions on `SaveDataHost.Data.unlockedHoles`, not by the screenshot), but the report's screenshot description is factually inaccurate and should be corrected to "Hole 2 scene loaded; HUD shows stale Hole 1 chip — lab-scaffold artifact".

## Step 2 — Figma comparison

**N/A.** Non-visual architecture task. SPEC has no `## Reference` section by design. No Figma frame expected, none missing — correct.

## Step 6 — Bbox containment check

**N/A.** No containment claims in this task (no UI layout).

## Step 7 — Scene-mutation audit (`git diff HEAD~1 HEAD -- Assets/Scenes/ShellScene.unity`)

**PASS.** The only ShellScene change is the addition of one root GameObject `SaveDataHost` (GO 1350107837) with a `Transform` (1350107839) and the `SaveDataHost` MonoBehaviour (1350107838, script guid `a7ef01dc...`), plus one new entry appended to `SceneRoots`. No `m_IsActive: 0` flips, no RectTransform/sizeDelta/position changes to any pre-existing GameObject. Clean. The documented SaveDataHost addition is the entire diff.

`ProjectSettings/ProjectSettings.asset` changed `runInBackground: 0 → 1` (1 line) — undocumented in the report but benign/expected for smoke-bot capture under MCP; not a fail item. `Packages/manifest.json` also bumped `com.ivanmurzak.unity.mcp` 0.72.1 → 0.73.0 — unrelated, undocumented, almost certainly an environment auto-update; noted, not a fail item.

## Step 8 — Production-flow capture

**N/A** for layout, but durability evidence assessed below in checklist item 13.

---

## Acceptance checklist walk (13 items)

| # | Item | Implementer | Self-review |
|---|---|---|---|
| 1 | `Save/` exists with `Golfin.Save` asmdef + 5 files | PASS | **CONFIRM-PASS** — `ls` confirms SaveData.cs, SaveDataHost.cs, ISavePersister.cs, LocalJsonPersister.cs, SaveSchemaMigrator.cs + asmdef all on disk. asmdef has `overrideReferences:true`, `precompiledReferences:["Newtonsoft.Json.dll"]`. |
| 2 | `SaveData.cs` defines schema; `schemaVersion = 1` | PASS | **CONFIRM-PASS** — all 8 fields present, `schemaVersion = 1`, `PersistedCharacter` flat DTO present. Matches SPEC §Architecture exactly. |
| 3 | `LocalJsonPersister` round-trips | PASS | **CONFIRM-PASS** — `RoundTrip_WriteReadStructEqual` genuinely writes a SaveData (12345 RP, char_alice@lv42, unlockedHoles=[1,2]), saves via persister, `TryLoad`, deserializes, asserts field equality. Test body backs the claim. |
| 4 | Atomic writes verified | PASS | **CONFIRM-PASS** — `LocalJsonPersister.SaveAsync` line 65 `await File.WriteAllTextAsync(_tmpPath, json)` then `File.Replace` (line 71) / `File.Move` first-save (line 76). `AtomicWrite_SourceFileUntouchedIfOnlyTmpExists` genuinely writes good content to `save.json`, partial content to `.tmp` only, asserts `save.json` unchanged — satisfies SPEC's "simulates by writing only the temp file" alternative. `AtomicWrite_TmpThenReplace_WritesCorrectly` asserts tmp is cleaned up. Legit. |
| 5 | Async I/O; no sync `File.WriteAllText` in `Golfin.Save` | PASS | **CONFIRM-PASS** — grep of `Assets/Scripts/Save/` shows the only `File.WriteAllText` (sync) calls are inside `Tests/SaveLayerTests.cs` (test-fixture setup, not runtime). Runtime `LocalJsonPersister` uses only `WriteAllTextAsync`. SPEC scopes the ban to the runtime layer; tests writing fixture files is acceptable. |
| 6 | Newtonsoft referenced; Dictionary round-trip test passes | PASS | **CONFIRM-PASS (with a flagged risk)** — `Golfin.Save.asmdef` references `Newtonsoft.Json.dll`; `manifest.json` has `com.unity.nuget.newtonsoft-json:3.2.1`; `DictionaryRoundTrip_NewtonsoftJson` genuinely round-trips `ballQuantities`/`itemQuantities`. **Risk to flag (not a fail):** the implementer added BOTH the UPM package (which ships its own `Newtonsoft.Json.dll` in `Library/PackageCache/com.unity.nuget.newtonsoft-json@.../`) AND a loose copy at `Assets/Plugins/NuGet/Newtonsoft.Json.dll`. Two copies of the same assembly is a classic duplicate-assembly compile-error vector. The report's evidence (compileErrors=False, 329 tests run, smoke-bot ran) indicates it currently builds — so this is not a hard fail — but the redundant loose DLL should be removed in favour of the package, or the package removed in favour of the loose DLL. Pick one. Architect should confirm. |
| 7 | All 5 systems refactored as read-through facades | PASS | **CONFIRM-PASS** — grep confirms each of RewardPointsManager / CharacterManager / BallManager / ItemManager / HoleProgressionService has `using Golfin.Save;`, reads `SaveDataHost.Instance.Data.*`, and calls `MarkDirty()` on mutation. CharacterManager overlays SaveData on `LoadRoster` + `SyncCharacterToSaveData` on LevelUp/SelectCharacter; Ball/Item overlay on Initialize + Sync on mutators; HoleProgressionService reads/writes `unlockedHoles`/`playedHoles`. Pattern correct. |
| 8 | PlayerPrefs write code removed from RewardPointsManager | PASS | **CONFIRM-PASS** — grep shows zero `PlayerPrefs.*` calls in `RewardPointsManager.cs` (only doc-comment mentions). Spend/Earn/Set/Reset all write `SaveDataHost.Instance.Data.rewardPoints` + `MarkDirty()`. Minor non-blocking note: `Awake` re-seeds 50000 when `Data.rewardPoints == 0`, which would clobber a legitimate "player spent down to 0" save — acceptable for a dev build but a latent bug; recommend a separate `seeded` flag eventually. |
| 9 | `OnSaved` event fires after every disk write | PASS | **OVERRIDE-FAIL** — see Fail item A. The runtime code (`SaveDataHost.FlushNow` line 193 `OnSaved?.Invoke()` after `await _persister.SaveAsync`) is correct, BUT the report cites test `OnSaved_FiringVerification_ViaTaskCompletion` as proof, and that test never references `SaveDataHost` or `OnSaved` — it calls `persister.SaveAsync` and increments a local `int savedCount`. The test's own comment says "caller fires OnSaved (simulated here)". SPEC §Definition of done requires "`OnSaved` event fires after every disk write" be verified; there is ZERO test coverage (EditMode or smoke) of the actual event firing. |
| 10 | Debounced writes (250ms tail), verified by EditMode test | PASS | **OVERRIDE-FAIL** — see Fail item B. SPEC is explicit: "verified by EditMode test that fires 10 OnChanged events in 50ms and asserts 1 write." `Debounce_MultipleMarkDirty_ColapsesToOneWrite` does the OPPOSITE — calls `SaveAsync` 10× directly and asserts `writeCount == 10`; its own comment admits "10 SaveAsync calls → 10 writes (no debounce at persister level)". `DebounceLogic_CoalesceVerification` sets a local `bool pendingWrite` 10× and asserts `markCount==10` — it never instantiates `SaveDataHost`, never runs the coroutine, never proves coalescing; its own comment says "actual coroutine coalescing is verified by integration (smoke bot)". The 250ms-tail-collapses-to-1-write behavior has NO EditMode test, contrary to an explicit SPEC line. |
| 11 | App-pause hook flushes; `await`s before returning from `OnApplicationPause(true)` | PASS | **CONFIRM-PASS** — `SaveDataHost.OnApplicationPause` (line 90) cancels the debounce coroutine then calls `FlushNow().GetAwaiter().GetResult()` — a synchronous block on the final flush, exactly per SPEC. No test for it, but SPEC §Definition of done does not mandate a test for the pause hook specifically; code inspection satisfies the item. |
| 12 | Script Execution Order = -100 | PASS | **CONFIRM-PASS** — `SaveDataHost.cs.meta` has `executionOrder: -100`. `SaveDataHostExecutionOrder.cs` `[InitializeOnLoad]+[DidReloadScripts]` re-asserts it on every reload. (Note: this lives in the `.cs.meta`, not `ProjectSettings`; that is the correct/canonical location for per-script exec order in Unity.) |
| 13 | EditMode tests for all named cases | PASS | **OVERRIDE-FAIL** — see Fail items A & B. 10 tests exist on disk and the report's `TotalTests=329, Passed=10, Failed=0` is accepted as runner evidence. BUT two of the six SPEC-named test requirements ("OnSaved event firing", "debounce coalescing") are NOT genuinely covered — the tests bearing those names are simulations/no-ops, not verifications. SPEC §Definition of done lists "OnSaved event firing, debounce coalescing" as required EditMode coverage. Item fails on substance. |
| — | Smoke-bot durability scenario | PASS | **CONFIRM-PASS** — `Scenarios.SaveLayerDurability` plays Hole 1, clears via `ForceShotComplete("InCup")`, taps PLAY NEXT → `Hole_02_Geo` loads (history.log line 26), `MarkDirty()` + 0.5s wait flushes to disk, then `ReloadFromDisk()` → `LoadData()` → `_persister.TryLoad()` reads the real `save.json` from disk → `JsonConvert.DeserializeObject` → assigns a fresh `_data`, fully discarding in-memory state. This genuinely replays the disk-load path; it is NOT a no-op or in-memory shortcut. The proof would fail if persistence were broken (a stale file lacking hole 2 would make `hole2UnlockedAfterRestart` false). history.log lines 28/31 confirm "Hole 2 unlocked: True" before AND after restart, rewardPoints=52400. Durability proof is genuine. (Caveat: `ReloadFromDisk` is not a true process kill, but the SPEC's headline requirement is "persisted state survives" and the disk-roundtrip proves that.) |

---

## Spec-deviation judgements

1. **Newtonsoft not pre-installed.** SPEC claimed it ships with Unity 6; it didn't. Implementer added the UPM package + a loose DLL. **Acceptable functionally**, but the dual-install (package + loose plugin DLL) is redundant and a latent duplicate-assembly risk — see checklist item 6. Resolve before forward by removing one of the two copies. Flagged for architect.
2. **No "Managers" GO in ShellScene; standalone `SaveDataHost` root GO.** **Acceptable.** SPEC §Persister abstraction itself hedged ("`Bootstrap` or `ManagersBootstrap` — whatever the singleton-host GameObject is called"). A standalone root GO with `DontDestroyOnLoad` + exec order -100 achieves the boot-ordering requirement. Note: `SaveDataHostSetup.cs` still references a "Managers" GO it would create — dead/inconsistent with what actually shipped, but harmless editor tooling.
3. **`ReloadFromDisk()` instead of real app kill.** **Acceptable.** Verified the method genuinely replays `LoadData()` → `TryLoad` → file read → deserialize → fresh `_data`. It is not a hollow in-memory shortcut. A true kill is not achievable inside a running coroutine; this is the right substitute and the durability proof stands.

---

## Fail list (concrete, one fix per failure)

**Fail A — `OnSaved` event firing has no real test (checklist items 9 & 13).**
The test `OnSaved_FiringVerification_ViaTaskCompletion` does not reference `SaveDataHost` or the `OnSaved` event at all — it simulates with a local counter. SPEC §Definition of done requires "`OnSaved` event fires after every disk write" to be verified.
*Fix:* Add a real test that instantiates `SaveDataHost` (EditMode: `new GameObject().AddComponent<SaveDataHost>()`, or a `[UnityTest]` PlayMode test — `Golfin.Save.Tests.asmdef` already has `TestAssemblies`), injects a test persister via `SetPersister`, subscribes to `OnSaved`, triggers a flush (`MarkDirty()` then await/yield past the debounce, or call `FlushNow()` directly after setting a pending write), and asserts the `OnSaved` handler fired exactly once after the write completed.

**Fail B — Debounce coalescing has no real test (checklist items 10 & 13).**
SPEC §Definition of done is explicit: "verified by EditMode test that fires 10 OnChanged events in 50ms and asserts 1 write." Neither shipped test does this — `Debounce_MultipleMarkDirty_ColapsesToOneWrite` asserts 10 writes (the inverse), `DebounceLogic_CoalesceVerification` is a local-variable no-op. Both test bodies' own comments admit they don't prove coalescing.
*Fix:* Add a `[UnityTest]` PlayMode test that instantiates `SaveDataHost` with an injected `CountingPersister`, calls `MarkDirty()` 10 times within ~50ms (well inside the 250ms tail), yields past the debounce window, and asserts the persister's write count == 1. The MonoBehaviour/coroutine constraint cited in the report is not a valid excuse — Unity supports creating MonoBehaviours and running coroutines in PlayMode tests, and the test asmdef is already TestAssemblies-enabled.

**Fail C — Report misrepresents test evidence (checklist items 9, 10, 13).**
The IMPLEMENTER_REPORT marks items 9, 10, 13 PASS with justifications that describe behavior the test bodies do not exercise (e.g. "Test `OnSaved_FiringVerification` PASSES. SaveDataHost.FlushNow() calls `OnSaved?.Invoke()`" — the test never calls `FlushNow`). After Fails A & B are fixed with genuine tests, rewrite these three checklist justifications to cite what the new tests actually assert.

**Fail D (minor — fix alongside) — Screenshot description inaccurate.**
IMPLEMENTER_REPORT § Screenshot describes `s05_...` as "the Hole 2 armed state" with HUD proof; the HUD visibly reads "HOLE 1 - REGULAR PAR 5". The Hole 2 *scene* did load (history.log line 26) but the HUD chip is stale.
*Fix:* Correct the report's screenshot description to "Hole 2 scene (`Hole_02_Geo`) loaded; HUD chip shows stale Hole 1 — lab-scaffold HoleContext does not repopulate on scene swap. Durability is proven by history.log assertions on `SaveDataHost.Data.unlockedHoles`, not by the HUD."

---

## Visual diff notes

N/A beyond Step 1 — non-visual task. The single durability screenshot is a sanity-check only and is a genuine play-mode frame.

## Bbox verification

N/A — no containment claims.

## Verdict rationale

The architecture is genuinely solid: 5 files on disk, 5 managers correctly refactored as read-through facades, atomic temp-then-replace writes correct, async I/O correct, no sync runtime writes, Newtonsoft wired, exec order -100, scene change clean and minimal, durability smoke-bot genuinely replays the disk path. **But** the SPEC §Definition of done explicitly mandates EditMode test coverage for "OnSaved event firing" and "debounce coalescing", and BOTH are absent in substance — the tests bearing those names are simulations whose own comments admit they don't prove the behavior. The report claimed PASS on these with justifications that misrepresent the test bodies — exactly the implementer false-PASS this gate exists to catch. Persistence semantics (debounce write-amp control, the OnSaved sync signal) are the load-bearing correctness guarantees of a save layer; shipping them with no real test coverage against an explicit SPEC requirement is not acceptable. Routing back for two genuine tests + report corrections. This is a small, well-scoped fix on an otherwise strong implementation.
