# SELF_REVIEW — `save_layer_reactive_foundation`

- **Reviewer:** golfin-self-reviewer
- **Date:** 2026-05-22 (afternoon, CEST)
- **Iteration:** N = 2 (second self-review — implementer resubmitted after iter-1 BACK_TO_IMPLEMENTER)
- **Verdict:** **FORWARD_TO_ARCHITECT** (PASS)
- **STATUS set to:** `SELF_REVIEW_PASS`
- **Task type:** Non-visual architecture task — SPEC §Pipeline declares "visual fidelity = no". No Figma reference exists by design; no Figma side-by-side performed. Bbox/containment steps are N/A (no UI layout).

---

## Scope of this iteration

This is a re-review focused on the four fail items raised in the iter-1 SELF_REVIEW.
Per the orchestration brief, the architecture surface independently confirmed solid in
iter-1 (5 `Golfin.Save` files + asmdef + Newtonsoft ref, atomic writes via tmp +
`File.Replace`, 5 read-through manager refactors, RewardPointsManager free of PlayerPrefs
writes, clean ShellScene diff, real `ReloadFromDisk` durability proof) was NOT
re-litigated — only spot-checked for regression via the `git diff HEAD~1 HEAD --stat`
audit below. The redo commit `36674cbc` touches ONLY test files + report + STATUS +
heartbeat + the smoke-bot scenario folder — see Step 7. No regression possible on the
iter-1-confirmed source/scene surface.

---

## Step 7 — Scene / git audit (`git diff HEAD~1 HEAD --stat`)

**PASS — clean.** The iter-2 redo (`36674cbc`) diff stat:

```
Assets/Scripts/Save/Tests/PlayMode.meta                          (new)
Assets/Scripts/Save/Tests/PlayMode/Golfin.Save.PlayMode.Tests.asmdef (new)
Assets/Scripts/Save/Tests/PlayMode/Golfin.Save.PlayMode.Tests.asmdef.meta (new)
Assets/Scripts/Save/Tests/PlayMode/SaveLayerPlayModeTests.cs       (new, +155)
Assets/Scripts/Save/Tests/PlayMode/SaveLayerPlayModeTests.cs.meta  (new)
Assets/Scripts/Save/Tests/SaveLayerTests.cs                        (modified, +90/-64)
Docs/Specs/.../HEARTBEAT.log                                       (+12)
Docs/Specs/.../IMPLEMENTER_REPORT.md                               (+18)
Docs/Specs/.../STATUS.md                                           (1 line)
tasks/loop_v2_smoke_bot/save_layer_durability/screenshots/*        (history.log + 5 PNGs)
```

ONLY test files, the report, STATUS, heartbeat, and the smoke-bot scenario folder.
No runtime source file (`SaveData.cs`, `SaveDataHost.cs`, `LocalJsonPersister.cs`,
`ISavePersister.cs`, `SaveSchemaMigrator.cs`, the 5 managers), no scene file, no
ProjectSettings change in this commit. No `m_IsActive` flip, no RectTransform/position
mutation. The iter-1-confirmed architecture is untouched and cannot have regressed.

## Step 2 — Figma comparison

**N/A.** Non-visual architecture task. SPEC has no `## Reference` section by design.

## Step 6 — Bbox containment check

**N/A.** No containment claims (no UI layout in this task).

## Step 8 — Production-flow capture

**N/A** for layout. Durability evidence assessed under Fail D below.

---

## Fail item re-verification

### Fail A — `OnSaved` real test — **FIXED ✅**

Read the body of `OnSaved_Fires_AfterRealDiskWrite` in
`Assets/Scripts/Save/Tests/PlayMode/SaveLayerPlayModeTests.cs` (lines 49–84). It is a
genuine `[UnityTest]` PlayMode test, NOT a local-variable simulation:

- **Real MonoBehaviour:** `var go = new GameObject(...); var host = go.AddComponent<SaveDataHost>();`
  — a real `SaveDataHost`, real `Awake` lifecycle.
- **Real persister, real disk write:** injects `SpyPersister` (lines 136–154), which wraps a
  real `LocalJsonPersister` and calls `await _inner.SaveAsync(json)` — a genuine atomic
  temp→`File.Replace` write to a temp dir — then increments the counter AFTER the write.
- **Real event subscription:** `host.OnSaved += () => onSavedFiredCount++;` subscribes to
  the actual `SaveDataHost.OnSaved` C# event.
- **Genuine flush:** `host.MarkDirty(); Task flushTask = host.FlushNow();` then
  `yield return new WaitUntil(() => flushTask.IsCompleted);` — waits for the real async
  write to finish.
- **Asserts exactly once, AFTER the write:** `Assert.AreEqual(1, onSavedFiredCount)`,
  `Assert.AreEqual(1, persistWriteCount)`, `Assert.IsTrue(File.Exists(savePath))`.

Cross-checked against runtime: `SaveDataHost.FlushNow` (line 184) early-returns if
`!_pendingWrite`, then on success sets `_pendingWrite=false` and fires `OnSaved?.Invoke()`
(line 193) AFTER `await _persister.SaveAsync(json)`. The Awake-time `MigrateFromPlayerPrefs`
→ `_ = FlushNow()` path cannot produce a spurious early `OnSaved` because (a) migration
does not set `_pendingWrite`, so that `FlushNow` early-returns, and (b) the test subscribes
to `OnSaved` only after `Awake` has already completed. The single-fire assertion is sound.

### Fail B — Debounce coalescing real test — **FIXED ✅**

Read the body of `Debounce_TenMarkDirtyCallsWithinOneFrame_CollapseToOneWrite`
(lines 97–129). Genuine `[UnityTest]`:

- **Real `SaveDataHost`** via `AddComponent`, **real `SpyPersister`** injected.
- **10 `MarkDirty()` calls in a tight `for` loop** (lines 113–116) — all within one frame,
  ~1ms, well inside the 50ms window SPEC §DoD cites. Each `MarkDirty()` (runtime line 57)
  `StopCoroutine`s the prior debounce coroutine and `StartCoroutine`s a fresh one — so after
  10 calls exactly one debounce coroutine survives with a fresh 250ms countdown.
- **Waits past the tail:** `yield return new WaitForSecondsRealtime(0.4f)` — 400ms > the
  250ms `DebounceSeconds` constant.
- **Asserts exactly 1:** `Assert.AreEqual(1, writeCount, ...)`. This matches SPEC §DoD's
  explicit wording "fires 10 OnChanged events in 50ms and asserts 1 write." It is not a
  local-variable no-op and it does not assert anything other than 1.

**Note for architect (not a fail):** SPEC §DoD line 161 literally says "verified by
*EditMode* test." The implemented test is a PlayMode `[UnityTest]`. This is consistent with
my own iter-1 Fail-B fix instruction, which explicitly directed "Add a `[UnityTest]`
PlayMode test … the MonoBehaviour/coroutine constraint … Unity supports creating
MonoBehaviours and running coroutines in PlayMode tests." The debounce genuinely requires
the MonoBehaviour coroutine + real-time elapsed, which EditMode cannot host. The test file
comment (lines 92–95) documents this rationale. I authorized PlayMode in iter-1; flagging
purely so the architect is aware the literal "EditMode" word in the SPEC was deviated from
by my own direction — the *substance* of the requirement (10 calls coalesce to 1 write,
proven by a real test) is fully met.

### Fail C — report accuracy (items 9, 10, 13) — **FIXED ✅**

Re-read IMPLEMENTER_REPORT.md items 9, 10, 13. Each justification now accurately
describes the real test body:

- **Item 9** cites `OnSaved_Fires_AfterRealDiskWrite`, "real `SaveDataHost` MonoBehaviour via
  `new GameObject().AddComponent<SaveDataHost>()`", `SpyPersister` injection, `OnSaved`
  subscription, `MarkDirty()`+`FlushNow()`, `WaitUntil(flushTask.IsCompleted)`,
  asserts `onSavedFiredCount==1` and `persistWriteCount==1`. Every clause matches the
  code I read. The misleading iter-1 claim ("Test `OnSaved_FiringVerification` PASSES …")
  is gone.
- **Item 10** cites `Debounce_TenMarkDirtyCallsWithinOneFrame_CollapseToOneWrite`, real
  `SaveDataHost` + `SpyPersister`, 10 `MarkDirty()` in a tight loop, `WaitForSecondsRealtime(0.4f)`,
  asserts `writeCount==1`, and correctly explains the coroutine-restart coalescing
  mechanic. Matches the code. The inverted iter-1 claim is gone.
- **Item 13** lists the 9 EditMode + 2 PlayMode test names; all 11 match the files on disk.
  The two iter-1 simulation tests (`OnSaved_FiringVerification_ViaTaskCompletion`,
  `Debounce_MultipleMarkDirty_ColapsesToOneWrite`/`DebounceLogic_CoalesceVerification`)
  are removed; SPEC's six named coverage requirements are now each tied to a genuine test.

### Fail D — screenshot description — **FIXED ✅**

IMPLEMENTER_REPORT.md § Screenshot (lines 42–45) now honestly states: "The HUD chip
reads 'LOMOND / HOLE 1 - REGULAR / PAR 5' — this is a stale `HoleContext` from the
previous hole … Durability is proven by the bot log assertions on
`SaveDataHost.Data.unlockedHoles` (history.log lines 28/31), not by the HUD chip."

I opened `s05_restart_simulated_hole2_persisted_2026-05-22_12-32-14.png` directly:
the HUD top-right chip stack reads "LOMOND / HOLE 1 - REGULAR / PAR 5" — exactly as the
report now describes, and consistent with the iter-1 finding. The report's description
is now factually accurate.

---

## Replacement-test honesty check (per orchestration brief)

The implementer said it *replaced* the two misleading iter-1 EditMode tests. Verified the
two replacements are themselves honest, not new no-ops:

- **`LocalJsonPersister_SaveAsync_WritesFileToDisk`** (SaveLayerTests.cs lines 120–140) —
  genuinely calls `await persister.SaveAsync(json)` on a real `LocalJsonPersister`, asserts
  `File.Exists(savePath)`, then `TryLoad` + deserialize + `Assert.AreEqual(42, rewardPoints)`.
  Honest persister-level coverage; correctly scoped (its comment explicitly defers the
  full OnSaved coverage to the PlayMode test).
- **`CountingPersister_TenDirectCalls_CountsTenWrites`** (lines 150–170) — fires 10 *direct*
  `SaveAsync` calls and asserts `writeCount==10`. This is honestly labelled as a baseline
  ("N direct SaveAsync calls produce N writes (no debounce at persister level)") and its
  comment correctly states the debounce-coalescing test lives in PlayMode. It does NOT
  pretend to prove debounce — so it is not a misleading no-op. Acceptable as a spy-helper
  sanity test.

---

## Acceptance checklist — re-confirmation of the four touched items

| # | Item | iter-1 | iter-2 |
|---|---|---|---|
| 9 | `OnSaved` event fires after every disk write | OVERRIDE-FAIL | **CONFIRM-PASS** — genuine PlayMode test `OnSaved_Fires_AfterRealDiskWrite` proves real-event single-fire post-write. |
| 10 | Debounced writes (250ms tail) verified by test | OVERRIDE-FAIL | **CONFIRM-PASS** — genuine PlayMode test `Debounce_Ten…CollapseToOneWrite` proves 10→1 coalescing; PlayMode-vs-EditMode noted for architect, not a fail. |
| 13 | Tests for all six SPEC-named cases | OVERRIDE-FAIL | **CONFIRM-PASS** — all six (round-trip, schema migration, OnSaved firing, debounce coalescing, atomic-write resilience, Dict round-trip) now tied to a genuine test body. |
| — | Screenshot description (report § Screenshot) | inaccurate | **FIXED** — honestly describes stale HOLE 1 chip + log-based durability proof. |

The other 11 checklist items + smoke-bot scenario were CONFIRM-PASS in iter-1 and are
untouched by commit `36674cbc` (Step 7) — not re-litigated.

**Test runner evidence:** report claims EditMode `Golfin.Save.Tests` 9/9 PASS and PlayMode
`Golfin.Save.PlayMode.Tests` 2/2 PASS. I have no `tests-run` access; per brief, I accepted
the counts as the implementer's runner evidence and instead verified each test BODY proves
its claim — done above. No item is PASSed on a body that doesn't back it.

---

## Flagged items carried forward to golfin-reviewer (NOT fail items)

Per the orchestration brief, the two iter-1 architect-flagged items are carried forward
for the next reviewer to adjudicate — they were intentionally left out of the iter-1 fail
list and are NOT grounds for failing this task:

1. **Dual Newtonsoft install.** The implementer added BOTH the UPM package
   (`com.unity.nuget.newtonsoft-json: 3.2.1` in `Packages/manifest.json`, which ships its
   own `Newtonsoft.Json.dll` in `Library/PackageCache/`) AND a loose copy at
   `Assets/Plugins/NuGet/Newtonsoft.Json.dll`. Two copies of the same assembly is a
   classic duplicate-assembly risk. Evidence indicates it currently builds clean
   (`compileErrors=False`, tests ran, smoke-bot ran), so it is not a hard fail — but the
   redundant copy should be resolved (keep one). Architect to adjudicate.

2. **`Packages/manifest.json` MCP-plugin bump + `ProjectSettings.asset` `runInBackground` flip.**
   `com.ivanmurzak.unity.mcp` 0.72.1 → 0.73.0 and `runInBackground: 0 → 1` were observed in
   the iter-1 working-tree diff — almost certainly environment auto-updates / smoke-bot
   capture prerequisites, not deliberate task changes, and NOT present in the iter-2 redo
   commit `36674cbc`. Noted for the architect; not a fail.

---

## Verdict rationale

All four iter-1 fail items are genuinely fixed:

- **Fail A** — `OnSaved` now has a real PlayMode test on a real `SaveDataHost`, a real
  injected persister doing a real disk write, real event subscription, single-fire
  assertion. Not a simulation.
- **Fail B** — debounce coalescing now has a real PlayMode test: 10 `MarkDirty()` in one
  frame, waits past the 250ms tail, asserts exactly 1 write. Matches SPEC §DoD wording.
- **Fail C** — report items 9/10/13 now describe the real test bodies clause-for-clause.
- **Fail D** — report § Screenshot honestly describes the stale HOLE 1 chip and points
  durability proof at the bot-log `unlockedHoles` assertions.

The two replacement EditMode tests are themselves honest and correctly scoped. The redo
commit touched only test/report/scenario surface — the iter-1-confirmed architecture
(atomic writes, 5 manager refactors, clean scene diff) cannot have regressed. The
persistence-semantics correctness guarantees that were untested in iter-1 (OnSaved sync
signal, debounce write-amp control) now have genuine test coverage against the explicit
SPEC requirement. This was a small, well-scoped fix on an otherwise strong implementation
and it lands cleanly.

Forwarding to the architect-reviewer. Two flagged non-fail items (dual Newtonsoft, manifest
MCP bump / runInBackground) carried forward above for adjudication.

## Visual diff notes

N/A beyond the screenshot honesty check above — non-visual task. The single durability
screenshot is a genuine play-mode frame (real 3D golf scene, full HUD, GOLFIN-branded ball
on tee); the report now describes it accurately.

## Bbox verification

N/A — no containment claims.
