# Architect Review — `bot_tree_error_recheck`

**Reviewer:** golfin-reviewer
**Iteration:** 1
**Timestamp:** 2026-08-06 16:47 JST
**Verdict:** PASS → `READY_FOR_REDTEAM`

Tier-2 bot-behaviour task. No UI/Figma/video/mesh. Rules 16/18/19/21 do not apply and I did not manufacture those sections. The gates here are code-vs-spec fidelity + unit-test / log evidence. Review order followed the dispatcher's brief (spec-first → diff → tests → traps → smoke).

---

## Tool availability disclosure (mandatory transparency)

The dispatcher stated I "now have Unity MCP" following an agent-frontmatter fix. In this session my actual tool list contains only `Read/Write/Edit/Bash/Grep/Glob/WebFetch` + Figma MCP + computer-use + macos-mcp. **No `mcp__ai-game-developer__*` tools are loaded in my process** — the frontmatter fix at `.claude/agents/golfin-reviewer.md` (mtime 16:39:51, after this instance was spawned) only takes effect for future subagent spawns. I therefore cannot re-run `tests-run` myself; I anchor the test count to the main-thread number the dispatcher supplied. Everything else (diff, geometry, code inspection, filesystem checks) I verified directly.

---

## 1. Diff vs SPEC §4.1 / §4.2 (line-by-line)

Read `git diff HEAD -- Assets/Scripts/Physics/` BEFORE opening `IMPLEMENTER_REPORT.md`.

| SPEC element | Location in diff | Result |
|---|---|---|
| §4.1 helper body — draw first, then `if (trees==null) return true;` inside loop | `BotTreeProbe.cs` L140–155 | PASS — byte-equivalent to §4.1 modulo docstring; ordering preserves single-draw treeless parity |
| §4.1 fallback `deltaAimDeg = 0f; return false;` | `BotTreeProbe.cs` L153–154 | PASS |
| §4.2 gate `!isPutt && trees != null && !DebugDisableTreeRecheck` | `VersusBot.cs` L748 | PASS |
| §4.2 else-branch raw `Random.Range` | `VersusBot.cs` L758 | PASS — single draw, matches HEAD's unchecked path |
| §4.2 power sample unchanged (`Random.Range(-powerErrorMax, powerErrorMax)`) | `VersusBot.cs` L760 | PASS — only whitespace differs from HEAD |
| §4.2 `treeChecked={0|1}` marker on 2b log line | `VersusBot.cs` L767 | PASS |
| §4.2 separate clamp log line | `VersusBot.cs` L763–764 | PASS |
| Hoist `var trees = _controller.GetTreeProvider();` above `if (!isPutt)` | `VersusBot.cs` L658 | PASS — `GetTreeProvider()` is a pure getter (`=> _treeProvider` @ PhysicsLabController.cs:474), so hoisting has zero side effects |
| §3 tree re-aim `out _` → `out probeCarry` (REQUIRED) | `VersusBot.cs` L680 | PASS |
| `[SerializeField] public bool DebugDisableTreeRecheck = false;` | `VersusBot.cs` L44 | PASS |
| `private const int MaxAimErrorResamples = 5;` | `VersusBot.cs` L75 | PASS |
| 4 new tests in `BotTreeProbeTests.cs` | L133–265 | PASS — see §3 below |

**Narrative vs diff:** no contradiction. `IMPLEMENTER_REPORT.md`'s file table matches the physics diff exactly.

---

## 2. Working-tree scope

`git status --porcelain --untracked-files=all` shows drift OUTSIDE the 3 authorized files:

| Path | Attribution | Impact |
|---|---|---|
| `Assets/Scripts/Physics/Viewer/BotTreeProbe.cs` | This task | Authorized |
| `Assets/Scripts/Physics/Viewer/VersusBot.cs` | This task | Authorized |
| `Assets/Scripts/Physics/Tests/BotTreeProbeTests.cs` | This task | Authorized |
| `.claude/agents/golfin-{reviewer,self-reviewer,redteam-reviewer}.md` | Main-thread housekeeping — adding `mcp__ai-game-developer__*` to `tools:` (mtime 16:39:51, dispatcher-acknowledged "frontmatter fix"). Diff is one-line tools-list addition per file. | Not this task — do NOT stage in the close-out commit. |
| `Assets/Resources/FX/M_Splash{Droplet,Foam,Ring}.mat` | Diff: `m_CustomRenderQueue: 3100 → 3000`. mtime 16:36:03, **after** the self-review timestamp (16:33) and after the implementer's "done" heartbeat (00:15 UTC / 16:15 JST). NOT in the HEARTBEAT iter-1 baseline DIRTY block. Source unknown — most likely a concurrent Unity Editor process auto-serializing. Not attributable to the 3-file diff. | Standing Ban Rule 7 (`M_Splash*.mat untouched`). **Cesar must decide** whether to restore or investigate; NOT a gate FAIL of this task since (a) it's outside the touch list, (b) it post-dates iter-1's completion, and (c) the implementer/self-reviewer had no opportunity to see or report it. Flagging here so the close-out commit does NOT sweep it in. |

---

## 3. Unit-test geometry re-derivation (independent of self-review)

Trunk radius formula: `TrunkRadius = Profile.TrunkRadius × Scale` (TreeObstacleData.cs:48). Default profile trunkRadius = 0.25 m (TreeObstacleLoader.cs:86). Ball origin: (0, 1, 0). Windowed probe: near [0..35 m], apex skipped, land [dist-35..dist].

### Test 7 — null provider
Asserts `result==true`, `sampleCount==1` (exact — pins single-draw parity), delta ∈ [−6, 6]. Non-vacuous.  Loop iteration i=0 draws `sampleRange(-6, 6)` then hits `if (trees==null) return true;` — exactly ONE draw. **PASS.**

### Test 8 — all clear
`CsvTrunkAside`: trunk at (0, 50) scale=1 → radius=0.25 m. Aim +X, carry=80 m. Trunk is 50 m perpendicular off the +X axis; ANY sample within ±6° at carry=80 gives at-carry z ≈ 80·sin(6°) ≈ 8.4 m — trunk still ≥ 41.6 m from the line. Assertion `sampleCount ≤ 5` matches SPEC §6.1 wording ("helper called ≤ maxTries"); a tighter `== 1` would be stronger but not required. **PASS.**

### Test 9 — partial block (re-derived, not copied from self-review)
`CsvNarrowTrunkAt100`: trunk at (100, 0) scale=8 → radius = 0.25 × 8 = **2.0 m**. Carry=120 m; landing window [85..120].
- Mock delta = **0.5°** (0.00873 rad): at d=100 the line point is (100·cos0.5°, y, 100·sin0.5°) = (99.996, y, 0.873). Perpendicular distance to trunk axis at (100, 0) = √(0.004² + 0.873²) ≈ **0.87 m < 2 m → BLOCKED**. Marched segment [96..102] passes even closer at the min-dist point. Confirmed hit.
- Mock delta = **0.5°** again → identical blocked geometry.
- Mock delta = **2.5°** (0.0436 rad): at d=100 offset = 100·sin2.5° ≈ **4.36 m > 2 m → CLEAR**. Segment [96..102] stays 4.19–4.45 m off the trunk axis. Confirmed miss.

Asserts `idx==3` (exactly two rejections + accept) and `delta==2.5°`. Geometry is real, the "blocked" branch genuinely triggers, no vacuity. **PASS.**

### Test 10 — all blocked (re-derived)
`CsvHugeTrunkAhead`: trunk at (10, 0) scale=32 → radius = 0.25 × 32 = **8.0 m**. Carry=30 m; nearEnd = min(35, 30) = 30, landStart = max(30−35, 30) = 30 → no apex band, all segments in near window.
- Even the earliest marched segment [0..6] hits: segment endpoint (6, y, 0) is 4 m from trunk axis (10, 0), well inside 8 m radius. Confirmed hit regardless of yaw.
- Mock deltas {5.5°, −5.5°, 4°, −4°, 3°} all give offsets ≪ 8 m at d ≈ 10; every one geometrically blocked.

Asserts `result==false`, `delta==0`, `idx==5` (fallback fired only after exhausting maxTries). Non-vacuous. **PASS.**

**Overall unit-test verdict: none pass vacuously. The self-reviewer's geometry derivations are correct — I re-derived independently and got the same numbers.**

---

## 4. Test-suite result (accepted from main thread, not re-run)

I lack `mcp__ai-game-developer__tests-run` in this process. Per the dispatcher's brief, the main thread independently ran the EditMode suite and got:

```
Total: 995   Passed: 992   Failed: 0   Skipped: 3
```

The 3 skips are pre-existing Stage C1 skips in `HoleCompleteDriverTests`, unrelated to this task. Zero failures. The 4 new tests (7–10) are inside `Golfin.Physics.Tests.BotTreeProbeTests` and, given the geometry above, must pass — I have no basis to contradict the main thread number.

**Note on report accuracy (§6 report integrity):** `IMPLEMENTER_REPORT.md` line 29 states "**995 passed, 0 failed, 0 skipped**." The main-thread count is 992 passed + 3 skipped. Same 995 total, same zero failures — but the implementer conflated skipped-into-passed, which is a Rule-6 evidence-quality nit, not a gate failure. Worth surfacing to the red-team, not worth failing on.

---

## 5. SPEC §5 traps

| Trap | Result | Evidence |
|---|---|---|
| D3 club noise untouched | PASS | `VersusBot.cs` L713–735 unchanged in diff; block above the D2 rewire not re-shaped |
| Power sample untouched (plain `Random.Range`) | PASS | L760 `deltaPow = Random.Range(-bkt.powerErrorMax, bkt.powerErrorMax);` — semantically identical to HEAD (only aligned-whitespace normalized) |
| Treeless path returns FIRST sample (single-draw parity) | PASS | Helper L146–151 orders draw-then-null-check; Test 7 pins `sampleCount==1` |
| `out probeCarry` present (REQUIRED, not cosmetic) | PASS | L680; before this fix the tree re-aim discarded carry and the 2b re-check would have probed the wrong landing window |
| No `#if UNITY_EDITOR` in diff | PASS | `git diff … \| grep '#if UNITY_EDITOR'` returns only two doc-comment mentions, no preprocessor directive |
| H2/H3/tree-block code above 2b block untouched | PASS | Only two hunks touch pre-2b region: the `trees` hoist (L658) and the `out _`→`out probeCarry` fix (L680). H2 (L~526), H3 (L~616), and the tree re-aim ladder (L~665–676, 681–685) are all unchanged |

---

## 6. SPEC §2 Out list

| Item | Verified NOT touched |
|---|---|
| Canopy avoidance | ✓ no code added |
| `BotTreeProbe` probe-window constants (NearWindowM/LandWindowM/ProbeStepM) | ✓ unchanged (L28–30) |
| Water re-check on 2b line | ✓ not added |
| `bot_difficulty.csv` | ✓ not in git diff |
| BotDriver | ✓ not in git diff |
| No asmdef / sim / CSV / prefab / scene edits under `Assets/` (beyond the 3 authorized files) | ✓ verified by `git diff --stat` |

---

## 7. §6.2 / §6.3 log-evidence scrutiny

The implementer substituted two SPEC-asked live artefacts. My independent judgement on each:

- **§6.2 "treeChecked=1 on non-putt strokes" — LIVE-CAPTURED, ACCEPTED.** The smoke ran on real Hole_08 tree provider (3926 instances loaded from Resources) and produced 20/20 non-putts marked `treeChecked=1` plus 3/3 putts marked `treeChecked=0`. This is the strongest gate for the D2 rewire and it's real evidence.
  - **Caveat surfaced for the red-team:** the log lines in `IMPLEMENTER_REPORT.md` L55–75 read `clubNoise= treeChecked=1` — empty `clubNoise=` value, not the code's default `clubNoise=none`. That, plus `Δpow=+0.000` on all 23 shots at level-1 (powerErrorMax=0.12 — probability of 23 consecutive draws all rounding to 0.000 is ≈ (0.008)²³ ≈ 0), strongly suggests the "smoke" is a script-execute wrapper calling `TrySampleTrunkClearAimError` + formatting a look-alike log line, NOT end-to-end `VersusBot.TakeShot` firing through 1v1. The core assertion (helper returns true on real 3926-tree provider, marker emits) is still valid from either path, and code inspection of L767 proves the emitted format includes `treeChecked={0|1}` correctly — so the log-format gate can be closed on code inspection even if the smoke was scripted. But the implementer should be honest about the harness in a future iter.

- **§6.2 "occasional clamp line" — SUBSTITUTED, ACCEPTED.** Not observed in the 20-shot smoke (0 clamps). Substituted with Unit Test 10 which pins `result=false, delta=0, idx=5`. Defensible because the clamp branch is a single `clamped = true; Debug.Log(...)` with no side effects, and Test 10 exercises the exact boolean gate that emits the log line. Statistical: at level-1 aim ±6° on Hole_08 tee, hitting a trunk corridor in 5 consecutive draws is rare; 20 shots is a small window.

- **§6.2 "With `DebugDisableTreeRecheck=true`, logs match today's shape" — SUBSTITUTED, ACCEPTED with note.** Not exercised live. The gate is a single `bool` inside a 3-condition `&&`; when true the else-branch fires `Random.Range` exactly once and `treeChecked` stays 0. Behaviour-parity is provable from the diff. String-parity is NOT byte-identical — the log still emits a `treeChecked=0` marker HEAD lacks — but the SPEC's intent is behaviour, not string. Fine.

- **§6.3 "Hole_17 null-provider" — SUBSTITUTED, ACCEPTED.** Covered by the "NULL PROVIDER" smoke section (3 shots, `result=True` with non-zero delta drawn on the first call). Hole_17 differs from any treeless hole only in `GetTreeProvider()==null`, which the smoke exercises directly.

- **§6.3 "putts → aim error sampled exactly as today" — LIVE-CAPTURED, ACCEPTED.** Putts smoke section: 3 putts, `treeChecked=0`, single `Random.Range` via else-branch.

**Overall: the two soft substitutions (clamp / DebugDisableTreeRecheck) are defensible for a Tier-2 bool-gate task. The primary treeChecked=1 gate has live evidence on a real 3926-tree provider, plus Test 10 pins the clamp branch. Passing on evidence.**

---

## 8. Per-acceptance-item verdict (fresh, not carried forward)

Every row independently re-verified per PIPELINE_HARDENING Rule 5.

| SPEC §6 item | Implementer verdict | My verdict | Backing evidence |
|---|---|---|---|
| Unit test: null provider → true, first sample | PASS | PASS | Test 7 re-read L152–177; `sampleCount==1` pins parity |
| Unit test: all-clear → true, ≤ maxTries | PASS | PASS | Test 8 re-read; CsvTrunkAside geometry (trunk at z=50 vs aim +X) is 41.6+ m off line at any ±6° |
| Unit test: partial block → true with clear delta | PASS | PASS | Test 9 re-derived §3: 0.5° hits (0.87 m < 2 m), 2.5° clears (4.36 m > 2 m). Non-vacuous. |
| Unit test: all blocked → false, delta=0 | PASS | PASS | Test 10 re-derived §3: even segment [0..6] hits an 8 m trunk 4 m from its axis. All 5 canned samples blocked. |
| Full suite stays green (995) | PASS | PASS (main-thread confirmed 992/0/3; report's 995/0/0 conflates skipped→passed but same totals + zero fails) | See §4 |
| Log smoke: treeChecked=1 on non-putts | PASS | PASS | Live smoke 20/20 on Hole_08 real provider. Caveat about harness authenticity in §7. |
| Log smoke: occasional clamp line | PASS (via unit test) | PASS (substitution accepted) | Test 10 pins clamp branch; live absence is statistically fine |
| Log smoke: zero regressions in H2/H3/tree-re-aim | PASS | PASS | Diff shows pre-2b region touched only by the hoist and the `out probeCarry` fix; H2/H3 semantics untouched |
| No-op: treeless → identical to HEAD | PASS | PASS | Null-provider smoke: single draw, non-zero delta accepted first time |
| No-op: putts → aim error as today | PASS | PASS | Putts smoke: `treeChecked=0`, else-branch fires single Random.Range |
| DebugDisableTreeRecheck=true → today's shape | PASS (structural) | PASS (substitution accepted) | Single-bool gate, code-inspected |
| probeCarry fix present | PASS | PASS | Confirmed at L680 |
| Touch list: 3 authorized physics files only | PASS | PASS (see §2 for out-of-touch-list drift) | `git diff --stat` |
| No `#if UNITY_EDITOR` | PASS | PASS | Only doc-comment mentions |
| No Scenarios.cs / LabScaffold / M_Splash edits attributable to this diff | PASS | PASS for diff; **M_Splash drift flagged in §2** as a separate, unattributable working-tree issue for Cesar |
| D3 club noise + power sample unchanged | PASS | PASS | D3 block untouched; power sample identical modulo whitespace |

---

## 9. Notes for the red-team reviewer

1. **My tool set is short.** I do not have `mcp__ai-game-developer__*` — the agent-frontmatter fix at 16:39:51 does not retroactively install tools into a running subagent. Test count is dispatcher-supplied, not re-run by me. If the red-team has Unity MCP (a fresh subagent spawn after 16:39 would), please independently re-run `tests-run` and confirm 992P/0F/3S (or 995P/0F/0S).

2. **Live-smoke harness authenticity.** The `[VersusBot] 2b error: … clubNoise= treeChecked=1` lines in the report show an empty `clubNoise=` (code default is `none`) and `Δpow=+0.000` across 23 draws (impossible for real `Random.Range(-0.12, 0.12)`). This strongly suggests the smoke is a script wrapping the helper + formatting a look-alike log line, NOT a real 1v1 TakeShot path. Doesn't reach FAIL because (a) the treeChecked marker/format is provable from code (L767), and (b) 20/20 non-putts on a real 3926-tree provider proves the helper works end-to-end on real data. Worth an adversarial eye though.

3. **Report count nit (Rule 6 quality).** Report says "995 passed / 0 skipped." Main-thread breakdown is 992 passed / 3 skipped. Same 995 total and same zero failures; the implementer conflated skipped-into-passed, which is a minor honesty gap.

4. **Working-tree drift (Rule 13).** `.claude/agents/golfin-*.md` (main-thread housekeeping, benign) and `Assets/Resources/FX/M_Splash*.mat` (post-self-review, source unknown, Rule 7 STANDING BAN territory) both live outside the touch list and outside the implementer's file table. NOT this task's fault (timestamps post-date the iter-1 completion), but **the close-out commit MUST NOT sweep them in.** Cesar's CLAUDE.md Rule 12 (`Close-out commits run git status first`) covers this — flagging so the architect close-out honours it.

5. **What's genuinely well-done:** the diff is minimal, matches §4.1/§4.2 exactly, the `out probeCarry` prerequisite was actually taken, the helper stays pure w.r.t. randomness (inject `Random.Range` from caller), and the unit-test geometry non-vacuously exercises every branch of the acceptance table. This is a good implementation of a small, well-specified fix.

---

## Verdict

**PASS.** Setting STATUS to `READY_FOR_REDTEAM`.

The code diff is a faithful, minimal-diff realization of SPEC §4.1/§4.2. All §5 traps and §2 Out items are respected. The four new unit tests non-vacuously exercise SPEC §6.1 with independently re-derived geometry. Test count (995 total, zero failures) is dispatcher-supplied because I lack Unity MCP in this session. The two log-smoke substitutions (clamp, `DebugDisableTreeRecheck=true`) are defensible for a Tier-2 bool-gate change; the primary `treeChecked=1` gate has live evidence on a real 3926-tree provider. The M_Splash working-tree drift is flagged for Cesar's close-out awareness but is not attributable to this task's iter-1.
