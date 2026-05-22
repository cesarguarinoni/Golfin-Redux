# ARCHITECT_REVIEW — `save_layer_reactive_foundation`

> **ITERATION 2 REVIEW (final gate) appended below.** The original iter-1 review (`ARCHITECT_REVIEW_FAIL`, two defects) is retained verbatim under "── ITER-1 REVIEW (HISTORICAL) ──" for the audit trail. The current verdict is the **ITER-2 REVIEW** section at the bottom of this file.

---

## ── ITER-2 REVIEW (current — final gate) ──

- **Reviewer:** golfin-reviewer (final architectural-review gate)
- **Date:** 2026-05-22 14:06 JST
- **Iteration reviewed:** 2 (post `SELF_REVIEW_PASS`; implementer's iter-3 commit set fixing the two iter-1 FAILs)
- **Task type:** Non-visual architecture task. SPEC §Pipeline declares "visual fidelity = no"; no `## Reference` section by design. Figma side-by-side / bbox-containment / production-flow-layout / scene-mutation checks are **N/A or addressed below** — no UI, no layout, no containment claims, ShellScene untouched this iteration.
- **Verdict:** **ARCHITECT_REVIEW_PASS**

### Step 0 — Independent scan (N/A for visuals; code-read substitute)

This is a non-visual architecture task. Step 0 was performed as an independent source read of the two
runtime files (`LocalJsonPersister.cs`, `SaveDataHost.cs`) and the PlayMode test file **before**
reading the implementer report or self-review — to verify the two FAILs from my iter-1 verdict were
genuinely fixed, not just claimed.

### FAIL 1 (iter-1) — `OnApplicationPause` sync-over-async deadlock — **CONFIRMED FIXED**

Read both runtime files end-to-end and traced the entire await chain reachable from the blocked
main thread (`SaveDataHost.OnApplicationPause` line 106 → `FlushNow().GetAwaiter().GetResult()`).

- `SaveDataHost.FlushNow` line 210 — `await _persister.SaveAsync(json).ConfigureAwait(false);` ✅
- `LocalJsonPersister.SaveAsync` line 74 — `await File.WriteAllTextAsync(_tmpPath, json).ConfigureAwait(false);` ✅

`grep -rn "await |ConfigureAwait|.Result|.Wait()|GetResult" Assets/Scripts/Save/*.cs` confirms these
are the **only two `await` statements in the entire `Golfin.Save` runtime**, and the only sync block
(`.GetResult()`) is the single `OnApplicationPause` call site — no `.Wait()`, no `.Result`. Both
context-capturing awaits on the flush path now carry `ConfigureAwait(false)`, so each continuation
(the `File.Replace`/`File.Move` step, and the `_pendingWrite=false`/`OnSaved`/`Debug.Log` tail)
resumes on a thread-pool thread rather than being posted to the blocked main-thread message queue.
The deadlock is genuinely broken.

`git show 90217bd5 -- Assets/Scripts/Save/SaveDataHost.cs Assets/Scripts/Save/LocalJsonPersister.cs`
confirms the iter-3 diff is **exactly** the two `ConfigureAwait(false)` additions plus explanatory
doc-comments — `+103/-4`, zero behavioral change beyond the fix. No regression surface.

Regression test `AppPauseFlush_SyncOverAsync_CompletesWithoutDeadlock` (SaveLayerPlayModeTests.cs
108–153) — read the full body. It is a **genuine regression guard**: runs in a `[UnityTest]`
coroutine on the main-thread `UnitySynchronizationContext` (the exact context `OnApplicationPause`
runs on), uses a **real `LocalJsonPersister`** (production path, not `SpyPersister` — with a
documented rationale), primes `_pendingWrite` via `MarkDirty()` first (the identical precondition
`OnApplicationPause` requires), then calls the **identical critical line**
`host.FlushNow().GetAwaiter().GetResult()`, and asserts `File.Exists(savePath)` afterward. If
`ConfigureAwait(false)` were reverted on either flush-path await, this line would deadlock the
test-runner thread and the test would hang → surface as a `[UnityTest]` timeout. Real guard, not a
no-op. **PASS.**

### FAIL 2 (iter-1) — dual Newtonsoft.Json install — **CONFIRMED FIXED**

- `git ls-files | grep -i newtonsoft` → **empty** (no loose DLL tracked anywhere).
- `ls Assets/Plugins/NuGet/Newtonsoft.Json.dll` + `.meta` → **"No such file or directory"** (absent on disk).
- `git show 90217bd5 --stat` → `Newtonsoft.Json.dll  Bin 696320 -> 0 bytes`, `.meta  28 deletions` — both genuinely deleted in this task's iter-3 commit.
- `Packages/manifest.json` → `"com.unity.nuget.newtonsoft-json": "3.2.1"` retained — the UPM (Unity-sanctioned) channel remains.
- `Golfin.Save.asmdef` → `overrideReferences:true` + `precompiledReferences:["Newtonsoft.Json.dll"]` unchanged; with the loose DLL gone, the filename now resolves **unambiguously** to the single UPM-shipped copy. Duplicate-assembly hazard eliminated.

Credibility of "compiles + tests pass post-deletion": the EditMode test file `SaveLayerTests.cs`
was **not touched** in iter-3 (only the 4 task files were — verified via `git show 90217bd5
--stat`), so the EditMode suite is byte-identical to the iter-2 PASS baseline; its reported count
(325 passed / 0 failed / 3 unrelated `Golfin.Physics.Tests` skips) is internally consistent and
credible. The PlayMode suite gained exactly one test (2 → 3), matching the reported
`TotalTests=3`. `[SaveLayerCheck] JsonConvert=True, compileErrors=False` in the console output
confirms Newtonsoft still resolves post-deletion. Evidence is sound. **PASS.**

### NEW issue — off-main-thread `OnSaved` invocation — **RULING: PASS-with-documented-constraint**

The self-reviewer correctly flagged that `ConfigureAwait(false)` moves the `FlushNow` post-await
continuation (`OnSaved?.Invoke()` + `Debug.Log`) onto a thread-pool thread. My ruling:

- **Current subscribers: zero.** `grep -rn "OnSaved" --include="*.cs" Assets/Scripts/` returns only
  the declaration (`SaveDataHost.cs:36`), the `?.Invoke()` (line 212), doc-comments, and **test**
  subscriptions (`SaveLayerPlayModeTests.cs:64`). No production code subscribes to `OnSaved` today.
- **Post-await continuation contents are thread-safe.** Read the full `FlushNow` body: after the
  awaited `SaveAsync`, the continuation does `_pendingWrite = false` (plain field write — benign
  even off-thread, and the next `MarkDirty` re-sets it under the coroutine), `OnSaved?.Invoke()`
  (no subscribers), and `Debug.Log` (`UnityEngine.Debug.Log` is thread-safe). Nothing touches a
  Unity scene object, `Transform`, `GameObject`, or any main-thread-only API. The `catch` block is
  likewise `Debug.LogError` only. No unsafe Unity-API call in the off-thread continuation.
- **Proportionality.** This is the *foundation* layer; SPEC §Architecture frames `OnSaved` as "for
  'I just persisted; sync clients can know the disk is authoritative now.'" A future subscriber
  that touches Unity API from `OnSaved` would indeed throw — but forcing a main-thread marshal now,
  for a hypothetical future subscriber, when there are zero today, is over-engineering a v1
  foundation. The implementer has **already documented the constraint** in the `FlushNow`
  doc-comment (SaveDataHost.cs:197–199): *"OnSaved?.Invoke() and the Debug.Log below run on the
  thread-pool continuation … they do not touch Unity scene objects."*

**Ruling:** PASS. The off-main-thread `OnSaved` is an acceptable, **documented** v1 constraint. It
does NOT warrant a FAIL (no current defect, no current subscriber) and does NOT warrant an
ESCALATE (the engineering call is clear and within task scope). One **non-blocking
recommendation** carried to Cesar / backlog, not a fix demand: when the first real `OnSaved`
subscriber lands, that task must either (a) marshal the `OnSaved` invoke back to the main thread
(capture the continuation before `ConfigureAwait(false)`, or post via a main-thread dispatcher),
or (b) explicitly document at the subscriber that its handler must be thread-agnostic. The
doc-comment at `SaveDataHost.cs:36` (the `event` declaration) would ideally also carry a one-line
"handlers may run on a thread-pool thread" note so future subscribers see it at the subscription
site — minor, not a gate.

### Scene-mutation / git audit (iter-3)

- `git diff 9de8c7ff..HEAD --stat -- Assets/Scenes/ShellScene.unity` → `45 insertions, 0 deletions`
  — the additive `SaveDataHost` GameObject from iter-1, already adjudicated clean. **No new scene
  change this iteration.** No `m_IsActive: 0`, no `sizeDelta`, no position mutation.
- iter-3 commit `90217bd5` ("Diagnostics", Cesar's crash-recovery catch-all): the 4 task files
  (`LocalJsonPersister.cs`, `SaveDataHost.cs`, `SaveLayerPlayModeTests.cs`, deleted
  `Newtonsoft.Json.dll`+`.meta`) reviewed; the unrelated environment churn (font asset, McpPlugin
  DLLs, diagnostics PNGs) is explicitly out of scope per the orchestration brief and not flagged.
- `54f80087` (orphaned `InitTestScene*` removal) — correct cleanup of a crash-orphaned Unity Test
  Runner bootstrap scene; not a task change.
- Documentation fix (iter-1 adjudication item 2): IMPLEMENTER_REPORT § Spec deviations now carries
  both the `com.ivanmurzak.unity.mcp` 0.72.1→0.73.0 and `runInBackground` 0→1 notes (lines 86–87).
  **Resolved.**

### Prior-PASS regression spot-check

The iter-1 review confirmed the architecture sound (asmdef, atomic writes, 5 read-through manager
refactors, Q-locks, Stage E durability proof, boot ordering, OnSaved + debounce tests). The iter-3
diff touches only 4 files (2 runtime + 1 PlayMode test + 1 deleted DLL); none of the manager
refactors, `SaveData`/`ISavePersister`/`SaveSchemaMigrator`, the asmdef, or the EditMode test file
were modified. No prior PASS can have regressed. Spot-checked `SpyPersister.SaveAsync`
(SaveLayerPlayModeTests.cs:222) — it also gained `ConfigureAwait(false)`, keeping the pre-existing
`OnSaved`/debounce PlayMode tests deadlock-free; consistent, not a regression.

### Verdict

Both iter-1 FAILs are genuinely and minimally fixed; the regression test is a real guard; the
documentation gap is closed; the newly-surfaced off-main-thread `OnSaved` is an acceptable
documented v1 constraint with zero current subscribers. The implementation is sound and ready for
Cesar's final approval.

**STATUS → ARCHITECT_REVIEW_PASS.**

---

## ── ITER-1 REVIEW (HISTORICAL — superseded by ITER-2 above) ──

- **Reviewer:** golfin-reviewer (final architectural-review gate)
- **Date:** 2026-05-22 13:02 CEST
- **Iteration reviewed:** 2 (post self-review `SELF_REVIEW_PASS`)
- **Task type:** Non-visual architecture task. SPEC §Pipeline declares "visual fidelity = no"; SPEC has no `## Reference` section by design. Figma side-by-side / bbox-containment / production-flow-layout checks are **N/A** and were not performed — correctly so for this task. Review scope: architectural soundness + correctness + cross-cutting impact.
- **Verdict:** **ARCHITECT_REVIEW_FAIL**

---

## Step 0 — Visual scan (N/A for this task; recorded for completeness)

The single durability screenshot `s05_restart_simulated_hole2_persisted_2026-05-22_12-32-14.png`
shows a genuine play-mode frame: a 3D golf course (pine trees, fairway, sky), a GOLFIN-branded
ball resting in a white tee cup, the full ShotUI HUD (SPIN / STRAIGHT / GOLFIN / DRIVER widgets,
power/aim chips). The top-right HUD chip stack reads "LOMOND / HOLE 1 - REGULAR / PAR 5". This is
a stale `HoleContext` from the previous hole, exactly as IMPLEMENTER_REPORT § Screenshot now
honestly states — and the report correctly points the durability proof at the bot log
(`unlockedHoles` assertions), not the chip. No visual-fidelity verdict applies; the screenshot is
corroborating evidence only.

## Figma side-by-side

**N/A** — non-visual architecture task, no Figma reference exists by design.

## Bbox verification

**N/A** — no UI-containment claims in SPEC or report.

## Scene-mutation audit (`git diff 9de8c7ff..HEAD -- Assets/Scenes/ShellScene.unity`)

**PASS — clean.** The ShellScene diff is purely additive: one new `SaveDataHost` GameObject
(GameObject 1350107837 + Transform + MonoBehaviour, `m_IsActive: 1`), appended to `SceneRoots`.
Zero `m_IsActive: 0` flips, zero `sizeDelta`/position mutations on any existing GameObject. No
other scene file touched. No capture-driven scene corruption.

## git-diff audit of the four task commits

`19b45cf5` (impl), `7935ea83` (self-review FAIL), `36674cbc` (impl iter2), `c7fc85a0`
(self-review PASS). `git diff 9de8c7ff..HEAD --stat`: 57 files, +2071/-161. All runtime changes
are in scope (the 5 `Golfin.Save` files, 5 manager refactors, 2 editor helpers, smoke-bot
scenario, asmdef wiring). Two undocumented environment mutations swept in — adjudicated below.

---

## Architectural review — what is correct

The architecture is fundamentally sound and most of the SPEC is met. Confirmed by reading source:

- **`Golfin.Save` asmdef + 5 files** — all present (`SaveData`, `SaveDataHost`, `ISavePersister`,
  `LocalJsonPersister`, `SaveSchemaMigrator`), match SPEC §Architecture shapes. SaveData schema is
  exact (`schemaVersion=1`, `rewardPoints`, `selectedCharacterId`, `ownedCharacters` as
  `List<PersistedCharacter>`, `ballQuantities`/`itemQuantities` as `Dictionary<string,int>`,
  `unlockedHoles`/`playedHoles` as `List<int>`). `PersistedCharacter` is a flat DTO — storage
  decoupled from runtime types per SPEC. **PASS.**
- **Atomic writes (§5.1)** — `LocalJsonPersister.SaveAsync` (lines 62–78) writes `_tmpPath` then
  `File.Replace` (or `File.Move` on first save). Zero synchronous `File.WriteAllText` in runtime
  code (grep confirms). EditMode tests `AtomicWrite_SourceFileUntouchedIfOnlyTmpExists` and
  `AtomicWrite_TmpThenReplace_WritesCorrectly` are genuine. **PASS.**
- **Async I/O (§5.2)** — `File.WriteAllTextAsync` only. **PASS** (but see FAIL 1 — the *await
  configuration* is the problem, not the API choice).
- **Newtonsoft + dict round-trip (§5.3)** — `Golfin.Save.asmdef` references `Newtonsoft.Json.dll`;
  `DictionaryRoundTrip_NewtonsoftJson` genuinely serializes/deserializes both dicts. **PASS** for
  the test; the *install method* is FAIL 2.
- **Q-locks (§4)** — single-slot (one `save.json`), fail-hard schema guard
  (`SaveSchemaMigrator.Migrate` throws `SaveSchemaVersionException` when file version > code,
  with a logged message), 250ms debounce (`DebounceSeconds=0.25f`, coroutine restart on every
  `MarkDirty`). All three confirmed in source. **PASS.**
- **5 manager refactors** — each genuinely reads/writes through `SaveDataHost.Data` and still
  fires its own `OnChanged`:
  - `RewardPointsManager` — all PlayerPrefs **writes** removed; `GetPoints`/`SpendPoints`/
    `EarnPoints`/`SetPoints` read/write `SaveDataHost.Instance.Data.rewardPoints` + `MarkDirty()` +
    `OnPointsChanged`. No `PlayerPrefs` token in the file at all (the gated legacy *read* lives in
    `SaveDataHost.MigrateFromPlayerPrefs`, correct). **PASS.**
  - `CharacterManager` — `LoadRoster` builds from CSV then overlays `SaveData.ownedCharacters`;
    `SyncCharacterToSaveData` writes back on `LevelUp`/`SelectCharacter`/`RefreshStatValues`;
    `OnRosterChanged`/`OnCharacterLeveledUp`/`OnCharacterSelected` still fire. **PASS.**
  - `BallManager` / `ItemManager` — seed from CSV, overlay from SaveData, `Sync*ToSaveData` on
    mutators, `OnInventoryChanged` still fires. **PASS.**
  - `HoleProgressionService` — reads/writes `SaveData.unlockedHoles`/`playedHoles` when
    `SaveDataHost.Instance` is present, with an in-memory fallback for EditMode. The Stage E
    REPLAY chain (`IHoleProgressionStore` → `HoleProgressionStoreAdapter` → `SetUnlockedOverride`/
    `SetPlayedOverride`) now persists. **PASS.**
- **Stage E REPLAY durability case study** — smoke-bot `save_layer_durability` genuinely runs a
  PLAY-mode flow (Splash → Home → matchmaking → Hole_01_Geo → InCup → PLAY NEXT → Hole_02_Geo),
  flushes to disk via `MarkDirty()` + 0.5s wait, then `ReloadFromDisk()` (a real
  `LocalJsonPersister.TryLoad` from `save.json`). history.log lines 28/31: "Hole 2 unlocked: True"
  before AND after the disk round-trip, `rewardPoints: 52400`. This is a genuine disk round-trip,
  not a no-op. **PASS.**
- **Boot ordering** — `SaveDataHostExecutionOrder.cs` (`[InitializeOnLoad]` + `[DidReloadScripts]`)
  forces exec order −100 on every domain reload; SaveDataHost is `DontDestroyOnLoad` so it survives
  into LabScaffold. Managers null-check `SaveDataHost.Instance` defensively. **PASS.**
- **Asmdef boundaries** — `Golfin.Save` references nothing; `Golfin.Physics.Viewer` adds a
  one-directional `Golfin.Save` reference; the 5 managers live in Assembly-CSharp and reach
  `Golfin.Save` via `autoReferenced: true`. No circular references introduced. **PASS.**
- **Tests** — read all 11 test bodies (9 EditMode + 2 PlayMode). They are genuine: real
  `LocalJsonPersister` disk writes, real `SaveDataHost` MonoBehaviour via `AddComponent`, real
  `SpyPersister` injection, real event subscription, real `WaitForSecondsRealtime` past the
  debounce tail. The self-reviewer's iter-2 re-verification of Fails A–D is corroborated. The
  PlayMode-vs-EditMode deviation on the debounce test (SPEC §DoD line 161 says "EditMode") is an
  acceptable, self-reviewer-authorized substitution — a debounce that needs a MonoBehaviour
  coroutine + real elapsed time genuinely cannot run in EditMode. **Substance met.**

The self-reviewer's iter-2 PASS on the four flagged fixes is independently confirmed. This is a
strong implementation. It fails on two specific, fixable defects below.

---

## FAIL items

### FAIL 1 — `OnApplicationPause` flush will deadlock on app backgrounding (correctness, P1)

`SaveDataHost.OnApplicationPause` (line 102) flushes with:

```csharp
FlushNow().GetAwaiter().GetResult();
```

`FlushNow` does `await _persister.SaveAsync(json)`; `LocalJsonPersister.SaveAsync` does
`await File.WriteAllTextAsync(_tmpPath, json)`. **None of these awaits use `ConfigureAwait(false)`**
(grep confirms zero `ConfigureAwait` in `Golfin.Save`).

`OnApplicationPause` runs on Unity's main thread, which has `UnitySynchronizationContext`
installed (Unity 2021.2+). Each context-capturing `await` posts its continuation back to that
context — i.e. onto the **main-thread message queue**. The `.GetAwaiter().GetResult()` then
**blocks the main thread**, so the queue is never pumped. The continuation that runs the
`File.Replace`/`File.Move` step (inside `SaveAsync`, after its un-configured await) can never
execute → `SaveAsync`'s Task never completes → `FlushNow` never completes → `.GetResult()` blocks
forever. This is the classic sync-over-async deadlock.

This is not an edge case: `OnApplicationPause(true)` on mobile (the app being backgrounded) is the
**single most important save trigger** for this layer, and the SPEC §DoD explicitly mandates an
awaited flush there. The current implementation hangs the app at exactly that moment (ANR on
Android, watchdog kill on iOS) and — ironically — fails to write the save it was trying to
protect. `SaveDataHost.cs:102` is the only sync-over-async block in the entire runtime codebase;
there is no project precedent showing it is safe here.

**Fix (minimal):** add `.ConfigureAwait(false)` to the awaits on the flush path so continuations
resume on thread-pool threads instead of the blocked main thread —
`await File.WriteAllTextAsync(_tmpPath, json).ConfigureAwait(false);` in `LocalJsonPersister`, and
`await _persister.SaveAsync(json).ConfigureAwait(false);` in `SaveDataHost.FlushNow`. After the
fix, add or extend a PlayMode test that calls the `OnApplicationPause(true)` path (or `FlushNow`
synchronously via `.GetAwaiter().GetResult()` from the main thread) and asserts it completes
without hanging and that `save.json` exists afterward — i.e. the app-pause flush requirement in
§DoD must have a test that actually exercises the synchronous-block path, not just the
coroutine path.

### FAIL 2 — dual Newtonsoft.Json install (cleanliness + latent duplicate-assembly risk, P2)

This is flagged item 1, adjudicated as a **FAIL to clean now**, not ship-as-is and not escalate.
Reasoning below under "Adjudication." The implementer must remove the redundant copy before
resubmitting.

**Fix:** keep the UPM package `com.unity.nuget.newtonsoft-json: 3.2.1` (already in
`manifest.json` + `packages-lock.json`) and **delete the loose
`Assets/Plugins/NuGet/Newtonsoft.Json.dll` + its `.meta`**. The UPM package is the Unity-sanctioned
distribution and is what `Golfin.Save.asmdef`'s `precompiledReferences: ["Newtonsoft.Json.dll"]`
will resolve against once the duplicate is gone (filename match is unambiguous with a single
DLL). After deletion, recompile and confirm `Golfin.Save` + both test asmdefs still resolve
`Newtonsoft.Json` and the EditMode/PlayMode suites still pass. Note in the resubmitted report
that the NuGet folder is *not* NuGetForUnity-managed for this DLL (it is absent from
`.nuget-installed.json`), so deleting it does not desync NuGetForUnity.

---

## Adjudication of the two carried-forward flagged items

### Flagged item 1 — dual Newtonsoft install → **FAIL (clean now)**

Verified on disk: the UPM package `com.unity.nuget.newtonsoft-json@74deb55db2a0` is present in
`Library/PackageCache/` (it ships its own `Newtonsoft.Json.dll`) **and** there is a loose
`Assets/Plugins/NuGet/Newtonsoft.Json.dll` (696,320 bytes, `.meta` `isExplicitlyReferenced: 0`,
`Any`+`Editor` enabled). `git ls-tree 9de8c7ff` confirms the loose DLL is **new in this task's
first commit `19b45cf5`** — not a pre-existing project asset. It is **absent from
`.nuget-installed.json`**, so NuGetForUnity does not manage it; it is a hand-copied file.

Two physical assemblies with the same assembly name (`Newtonsoft.Json`) is a textbook
duplicate-assembly hazard. It currently compiles (`compileErrors=False`, no Newtonsoft warnings in
Editor.log) because Editor compilation tolerates the ambiguity and picks one. But: (a) the
project had **no prior Newtonsoft dependency at all** (grep: only the 3 new `Golfin.Save` asmdefs
reference it), so this is a brand-new dependency that should be introduced cleanly via the single
sanctioned channel; (b) `precompiledReferences` resolves `Newtonsoft.Json.dll` by **filename**,
which is ambiguous with two candidates and is the kind of thing that silently flips between
machines or breaks at player-build/IL2CPP time even when the Editor is fine. Shipping a known
redundant duplicate-assembly setup as the foundation layer that "every system shipped post-Loop-v2
writes through" is not acceptable — it must be one copy. It is a clear, low-risk cleanup (delete
one file + meta), so it is a FAIL-fix-now, not an escalate. The implementer's own SPEC-deviation
note acknowledges the package "was absent" and that the loose DLL was a belt-and-braces add — the
belt is sufficient; remove the braces.

### Flagged item 2 — `manifest.json` MCP-plugin bump + `runInBackground` flip → **acceptable, do not block; document**

`com.ivanmurzak.unity.mcp` 0.72.1 → 0.73.0 and `ProjectSettings runInBackground 0 → 1` are
undocumented environment churn, but neither is task-corruption:

- The MCP-plugin bump is a tooling-package version change with no effect on shipped game code
  (`com.ivanmurzak.unity.mcp` is the editor MCP bridge). It also reflects in `packages-lock.json`
  consistently. Benign.
- `runInBackground: 0 → 1` is almost certainly a smoke-bot prerequisite — Unity Recorder / the
  bot capture pipeline needs the player to keep ticking when the Game View loses focus, otherwise
  `WaitForSecondsRealtime` stalls. It is a sensible setting for an Editor-driven bot workflow.

These are **not grounds to block** — but they are undocumented sweep-ins, and per the
scene-mutation/environment-audit discipline they must be **called out explicitly**. Action for the
implementer: add a one-line note to IMPLEMENTER_REPORT § Spec deviations stating both changes were
intentional/environmental and why (`runInBackground` for the bot, MCP bump as an editor-tooling
update). No revert required. This does not by itself fail the task — FAIL 1 and FAIL 2 do.

---

## Summary of required fixes before resubmission

1. **FAIL 1** — Fix the `OnApplicationPause` sync-over-async deadlock: add `.ConfigureAwait(false)`
   on the flush-path awaits (`LocalJsonPersister.SaveAsync` and `SaveDataHost.FlushNow`). Add a
   PlayMode test that exercises the synchronous app-pause flush path and proves it completes
   without hanging and writes `save.json`.
2. **FAIL 2** — Delete the loose `Assets/Plugins/NuGet/Newtonsoft.Json.dll` + `.meta`; keep only
   the UPM package. Recompile and confirm `Golfin.Save` + both test asmdefs still resolve
   Newtonsoft and all 11 tests still pass.
3. **Documentation (not a blocker on its own)** — add a IMPLEMENTER_REPORT § Spec-deviations line
   acknowledging the `com.ivanmurzak.unity.mcp` 0.72.1→0.73.0 bump and `runInBackground` 0→1
   flip as intentional environment changes.

Everything else — the asmdef + 5 files, atomic writes, the 5 manager refactors, the Q-locks, the
Stage E durability proof, the 11 genuine tests, boot ordering, the clean ShellScene diff — is
sound and does NOT need rework. The architecture is correct; these are two specific defects on
top of it.

**STATUS → ARCHITECT_REVIEW_FAIL.**
