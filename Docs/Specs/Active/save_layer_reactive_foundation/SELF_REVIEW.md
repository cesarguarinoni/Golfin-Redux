# SELF_REVIEW — `save_layer_reactive_foundation`

- **Reviewer:** golfin-self-reviewer
- **Date:** 2026-05-22 13:37 CEST
- **Iteration:** N = 3 (third self-review — implementer resubmitted after the **final reviewer's** `ARCHITECT_REVIEW_FAIL`, not a prior self-review FAIL)
- **Verdict:** **FORWARD_TO_ARCHITECT** (PASS)
- **STATUS set to:** `SELF_REVIEW_PASS`
- **Task type:** Non-visual architecture task — SPEC §Pipeline declares "visual fidelity = no". No Figma reference, no `## Reference` section by design. Figma side-by-side (Step 2) and bbox-geometry containment (Step 6) are **N/A** — there is no UI layout and no containment claim in this task. Production-flow capture (Step 8) is **N/A** — no layout change. Step 1 visual-description is replaced by source-code reading per the brief.

---

## Scope of this iteration

Re-review focused on the **two reviewer fail items** raised in `ARCHITECT_REVIEW.md` (iter-2 review) plus the one documentation fix. Per the orchestration brief, the architecture surface confirmed solid in iter-1/iter-2 and re-confirmed by the architect-reviewer (5 `Golfin.Save` files, atomic writes, 5 read-through manager refactors, Q-locks, Stage E durability proof, boot ordering, the OnSaved + debounce PlayMode tests, clean ShellScene diff) was NOT re-litigated — only spot-checked for regression via the git audit below. The architect-reviewer's verdict was explicit: "Everything else … is sound and does NOT need rework."

This is a post-rejection-by-architect iteration, not a post-Cesar-rejection iteration; `CESAR_REJECTION.md` does not exist, so a full acceptance re-walk is not mandated — but the git audit confirms the iter-3 diff touches only the 4 task files, so no prior PASS can have regressed.

---

## Step 7 — Scene / git-diff audit (unusual three-commit history)

Unity Editor crashed mid-test-run during iter-3; the changes are spread across three commits exactly as the brief described. Audited each:

- **`90217bd5` "Diagnostics"** (Cesar's catch-all crash-recovery commit, 824 files). The **only** files relevant to this task — confirmed via `git show 90217bd5 --stat -- Assets/Scripts/Save/ Assets/Plugins/NuGet/Newtonsoft.Json.dll*`:
  - `Assets/Scripts/Save/LocalJsonPersister.cs` (+11 / genuine `ConfigureAwait(false)` change)
  - `Assets/Scripts/Save/SaveDataHost.cs` (+23 / genuine `ConfigureAwait(false)` change)
  - `Assets/Scripts/Save/Tests/PlayMode/SaveLayerPlayModeTests.cs` (+73 / new deadlock regression test)
  - `Assets/Plugins/NuGet/Newtonsoft.Json.dll` + `.meta` (`Bin 696320 -> 0 bytes` — deleted)
  The other ~819 files in this commit (font asset, McpPlugin DLLs, `.nuget-installed.json`, the pile of `Docs/Diagnostics/_capture/_compressed/*.png` and `tasks/.../screenshots/*`) are **pre-existing environment churn swept in by Cesar's manual catch-all commit** — explicitly out of scope per the brief; not reviewed, not failed.
- **`2d03e1f3` "save_layer iter3 (impl)"** — IMPLEMENTER_REPORT.md + STATUS.md + HEARTBEAT.log only. Clean.
- **`54f80087` "chore: remove orphaned Unity Test Runner bootstrap scene"** — deletes `Assets/InitTestScene772c6a1c-*.unity` (a Unity Test Framework PlayMode bootstrap scene orphaned by the crash and wrongly swept into `90217bd5`). Correct cleanup per the brief; not a task change, not failed.

**No scene-mutation defect.** `ShellScene.unity` is untouched this iteration (was audited clean in prior iterations). No `m_IsActive` flip, no `sizeDelta`/position mutation. The InitTestScene removal is the *deletion of an orphaned scene*, not a mutation of a live scene — and is explicitly explained as correct cleanup. **PASS.**

## Step 2 — Figma comparison

**N/A** — non-visual architecture task, no Figma reference exists by design.

## Step 6 — Bbox containment check

**N/A** — no UI-containment claims in SPEC or report.

## Step 8 — Production-flow capture

**N/A** — no layout change in this task.

---

## FAIL 1 (P1) — `OnApplicationPause` sync-over-async deadlock — **FIXED ✅**

Read both runtime files end-to-end and traced the complete flush path the blocked main thread reaches.

**The deadlock-prone call site:** `SaveDataHost.OnApplicationPause` (SaveDataHost.cs:106) — `FlushNow().GetAwaiter().GetResult();` — runs on Unity's main thread, which carries `UnitySynchronizationContext`. `.GetResult()` blocks that thread.

**The complete await chain reached by that blocked thread, and the `ConfigureAwait(false)` coverage of each link** (verified by `grep -rn "ConfigureAwait\|await " Assets/Scripts/Save/`):

1. `SaveDataHost.FlushNow` (line 210) — `await _persister.SaveAsync(json).ConfigureAwait(false);` ✅
2. `LocalJsonPersister.SaveAsync` (line 74) — `await File.WriteAllTextAsync(_tmpPath, json).ConfigureAwait(false);` ✅

Both context-capturing awaits on the path now carry `ConfigureAwait(false)`. There are exactly two `await` statements in the entire `Golfin.Save` runtime (lines 210 and 74) — both fixed. The reasoning holds: with `ConfigureAwait(false)`, each continuation (the `File.Replace`/`File.Move` step inside `SaveAsync`, and the `_pendingWrite=false`/`OnSaved`/`Debug.Log` tail of `FlushNow`) resumes on a **thread-pool thread**, not by being posted to the blocked main-thread message queue. The blocked main thread no longer needs to pump its queue for the flush Task to complete → `.GetResult()` unblocks. The classic sync-over-async deadlock is broken.

There is exactly one `.GetResult()` in runtime code (SaveDataHost.cs:106); no `.Wait()`, no `.Result`. The fix is complete and the threading reasoning is sound. The implementer's doc-comment (SaveDataHost.cs:188-199) also correctly notes that `OnSaved?.Invoke()` and the `Debug.Log` now run on a thread-pool continuation, and correctly observes this is safe because neither touches Unity scene objects (`OnSaved` is a plain C# event; `Debug.Log` is thread-safe). That caveat is accurate and worth the architect's awareness — subscribers to `OnSaved` must not assume main-thread context — but it is not a defect in this layer.

**Regression test `AppPauseFlush_SyncOverAsync_CompletesWithoutDeadlock`** (SaveLayerPlayModeTests.cs:108-153) — read the full body. It is a **genuine regression guard**, not a no-op:

- Runs inside a `[UnityTest]` coroutine, which executes on Unity's main thread carrying `UnitySynchronizationContext` — **the same context `OnApplicationPause` runs on**. This is the load-bearing property: the test reproduces the exact threading condition.
- Uses a **real `LocalJsonPersister`** (the production code path), deliberately NOT `SpyPersister`, with a documented rationale (lines 113-120).
- Calls `host.MarkDirty()` first so `_pendingWrite == true` — the identical precondition `OnApplicationPause` requires before flushing (without it `FlushNow` early-returns).
- Calls `host.FlushNow().GetAwaiter().GetResult();` (line 143) — the **identical critical line** as `OnApplicationPause` (SaveDataHost.cs:106).
- If `ConfigureAwait(false)` were removed from either flush-path await, this line would deadlock the test-runner thread and the test would hang → surface as a `[UnityTest]` timeout. That is a real failure signal. The test would genuinely hang/fail on the regression.
- After the sync block returns, asserts `File.Exists(savePath)` — proving the write actually completed, not just that the call returned.

The test calls `FlushNow().GetAwaiter().GetResult()` directly rather than invoking the private `OnApplicationPause(true)` Unity message via reflection. This is acceptable: it exercises the identical critical statement under the identical `_pendingWrite==true` precondition on the identical synchronization context. It faithfully reproduces the deadlock-prone path. **CONFIRM-PASS.**

## FAIL 2 (P2) — dual Newtonsoft install — **FIXED ✅**

- `git ls-files | grep -i newtonsoft` → **empty** (no loose DLL tracked anywhere in the repo).
- `ls Assets/Plugins/NuGet/Newtonsoft.Json.dll` → **"No such file or directory"** (gone from working tree).
- `git show 90217bd5` confirms `Assets/Plugins/NuGet/Newtonsoft.Json.dll` `Bin 696320 -> 0 bytes` and `.meta` `28 deletions` — both genuinely deleted in this task's iter-3 commit.
- `Packages/manifest.json` still has `"com.unity.nuget.newtonsoft-json": "3.2.1"` — the UPM package (the Unity-sanctioned distribution channel) remains.
- `Golfin.Save.asmdef` still has `"overrideReferences": true` + `"precompiledReferences": ["Newtonsoft.Json.dll"]`. With the loose DLL gone, the filename `Newtonsoft.Json.dll` now resolves **unambiguously** to the single copy shipped by the UPM package in `Library/PackageCache/` — the duplicate-assembly hazard the architect flagged is eliminated.

The implementer cannot run the build here and I have no Unity MCP; per the brief I accept the implementer's reported runner counts as runner evidence (EditMode `Golfin.Save.Tests` Passed 325/0/3-unrelated-skips; PlayMode `Golfin.Save.PlayMode.Tests` 3/3) and verified the report's evidence is internally consistent — the EditMode test file `SaveLayerTests.cs` was NOT touched in iter-3 (only the 4 task files were), so the EditMode suite is identical to the iter-2 PASS state and its count is credible. The 3-skip note ("3 Stage C1 skips in `Golfin.Physics.Tests`, unrelated") matches the iter-2 report. **CONFIRM-PASS.**

## Documentation fix — **PRESENT ✅**

IMPLEMENTER_REPORT.md § Spec deviations now contains the two required notes:
- Line 86 — `com.ivanmurzak.unity.mcp` 0.72.1 → 0.73.0 MCP-plugin bump, flagged intentional (editor tooling only, zero shipped-code impact).
- Line 87 — `ProjectSettings runInBackground` 0 → 1 flip, flagged intentional (smoke-bot capture prerequisite; mobile ignores the setting).

Both match the architect-reviewer's adjudication ("acceptable, do not block; document").

---

## Replacement / regression honesty check

- The new `SpyPersister.SaveAsync` (SaveLayerPlayModeTests.cs:218-224) also gained `ConfigureAwait(false)` on its internal `await _inner.SaveAsync(json)`. This is consistent and correct — it keeps the existing `OnSaved` and debounce PlayMode tests deadlock-free too. Not a regression.
- The two pre-existing PlayMode tests (`OnSaved_Fires_AfterRealDiskWrite`, `Debounce_TenMarkDirtyCallsWithinOneFrame_CollapseToOneWrite`) are untouched in body — confirmed by the `90217bd5` diff hunk boundaries (the new test was inserted between Test A and Test B; the only other change is the `SpyPersister` line). The iter-2-confirmed coverage is intact.
- `WriteAsync` (SaveDataHost.cs:175-182) — the debounced non-pause write path — calls `FlushNow()` and yields on `WaitUntil(flushTask.IsCompleted)` inside a coroutine; it does NOT block the main thread, so `ConfigureAwait(false)` is harmless-and-correct there too (it never needed the main-thread context). No regression.

## MCP-workaround assessment (per brief)

The iter-3 implementer reported the Unity MCP transport dropped after the crash and tests were run via direct HTTP to the Unity MCP server at `localhost:21573`. Per the brief I treat the reported results as runner evidence. I do not find this concerning enough to ESCALATE: direct HTTP to the MCP server is the same server the MCP tool wraps — the transport layer differs, not the test runner. The reported EditMode/PlayMode counts are internally consistent with the iter-2 baseline (EditMode file untouched; PlayMode gained exactly one test → 2 → 3, matching `TotalTests=3`). The evidence is not thin. PASS, not ESCALATE.

---

## Acceptance checklist — re-confirmation of the three touched concerns

| Concern | iter-2 (architect) | iter-3 |
|---|---|---|
| FAIL 1 — `OnApplicationPause` sync-over-async deadlock | ARCHITECT FAIL | **FIXED** — `ConfigureAwait(false)` on both flush-path awaits (FlushNow line 210, LocalJsonPersister line 74); genuine regression test `AppPauseFlush_SyncOverAsync_CompletesWithoutDeadlock` exercises the exact `.GetAwaiter().GetResult()` path on the main-thread sync context. |
| FAIL 2 — dual Newtonsoft install | ARCHITECT FAIL | **FIXED** — loose `Assets/Plugins/NuGet/Newtonsoft.Json.dll` + `.meta` deleted (`git ls-files` empty, `ls` absent, `90217bd5` shows `696320 -> 0 bytes`); UPM package retained; asmdef `precompiledReferences` now resolves unambiguously. |
| Documentation — MCP bump + runInBackground notes | ARCHITECT FAIL (doc) | **FIXED** — both notes present in IMPLEMENTER_REPORT.md § Spec deviations lines 86-87. |

All 17 SPEC §DoD items + smoke-bot scenario were CONFIRM-PASS in iter-1/iter-2 and re-confirmed by the architect-reviewer; the iter-3 diff touches only the 4 task files (2 runtime, 1 PlayMode test, 1 deleted DLL) — no prior PASS can have regressed.

---

## Verdict rationale

Both architect-raised fail items are genuinely and minimally fixed:

- **FAIL 1** — every context-capturing `await` on the `OnApplicationPause` → `FlushNow` → `LocalJsonPersister.SaveAsync` → `File.WriteAllTextAsync` chain now carries `ConfigureAwait(false)`. There are exactly two such awaits and both are fixed. The continuations resume on thread-pool threads, so the blocked main thread is no longer required to pump its message queue — the deadlock is broken. The new PlayMode test is a real regression guard that hangs/fails if the fix is reverted, and it exercises the identical synchronous main-thread `.GetAwaiter().GetResult()` path.
- **FAIL 2** — the redundant hand-copied DLL and its meta are deleted; the UPM package is the sole Newtonsoft source; the asmdef filename reference is now unambiguous.
- **Documentation** — the two environment-churn notes are present.

The fix is surgically scoped (4 files, +107/-32 of which the DLL is the bulk of the deletions), introduces no new defect, and does not disturb the iter-1/iter-2-confirmed architecture. This iteration lands cleanly.

Forwarding to the architect-reviewer.

## Visual diff notes

**N/A** — non-visual architecture task. No screenshot review applies to this iteration (the iter-2 durability screenshot was already verified honest by the prior self-review and architect-reviewer and is untouched here).

## Bbox verification

**N/A** — no containment claims.
