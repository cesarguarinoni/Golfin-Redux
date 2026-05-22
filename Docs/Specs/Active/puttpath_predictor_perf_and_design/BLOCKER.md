# BLOCKER — `puttpath_predictor_perf_and_design`

**State:** `IMPLEMENTER_BLOCKED`
**Set by:** Orchestrator (Claude Code main session), 2026-05-22
**Reason:** Unity MCP hub unresponsive — implementer's 3 verification FAIL items cannot be closed by any downstream agent.

---

## The blocker

The implementer finished the code (compiles, renders — screenshot shows 693 cells
on Hole 1's green) but the Unity MCP hub went down near the end of its run. It
shipped with **3 unverified FAIL items** and routed to `READY_FOR_ARCHITECT_REVIEW`.

That route is a dead end: the `golfin-reviewer` has **no Unity MCP tools**
(Read/Write/Edit/Glob/Grep/Bash/WebFetch/Figma only), so it physically cannot
close the 3 gaps. The implementer also tried to delegate `tests-run` downstream
("Reviewer runs EditMode tests…") — but `tests-run` is the implementer's own
responsibility and must never be delegated to manual.

**The MCP hub is confirmed down right now.** Probe history:
- Implementer: `tests-run` returned `Response data is null` ×3 (17:50 / 18:00 / 18:05).
- Implementer HEARTBEAT.log: "MCP hub down >12min — writing IMPLEMENTER_REPORT".
- Orchestrator: `unity-tool-list` ×3 + `console-get-logs` ×1, all `Response data is null`.

That is 7+ failed probes across a 20+ minute window — not a transient transport
blip. The Unity Editor / AI-Game-Developer MCP plugin side is not alive.

## What Cesar must do to unblock

1. Confirm the **Unity Editor is open** for this project.
2. Confirm the **AI Game Developer (unity-mcp) plugin is connected** (the MCP
   bridge — the implementer did 579 successful MCP calls earlier, then it died
   ~17:48, likely an Editor crash / sleep / close).
3. Reply in chat ("Unity's back" or similar). The orchestrator will then resume
   the implementer to close the 3 gaps — no manual test-running needed from you.

## The 3 MCP-blocked verifications (close on resume)

From `IMPLEMENTER_REPORT.md` — all blocked solely by the MCP outage, not code defects:

1. **EditMode tests** — re-run `tests-run` on `Golfin.Physics.Tests`; the 4
   `PutterGreenReader` bake tests must PASS and be captured.
2. **Profiler frame-time capture** — enter play mode, putter aim on Hole 1,
   capture `PutterGreenReader.Update()` cost (expect sub-1ms, ~693 visible cells).
3. **Frame Debugger** — confirm a single `RenderMeshInstanced` draw call covers
   all visible cells (SRP Batcher opt-out / GPU Instancing verification).

Also from the report's Open Question #4: verify a **fresh play-mode entry** has
no NRE in `FlushBatch` (`_mpb` was null in an old compiled assembly; the `Awake()`
fix is in source — confirm it sticks after a clean recompile).

## Repo-state notes for the resumed implementer (housekeeping, no MCP needed)

The implementer's commit `3aaccdcf` was **partial**. The following are real,
correct, REQUIRED changes left **uncommitted** and **undocumented** in the report:

- `Assets/Scripts/Physics/Viewer/PhysicsLabUI.cs` — Q5 HeatmapMode dashboard-toggle
  wiring (+13 lines). The report claims this item PASS but never committed the file.
- `Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs` — `putter_aim_green_reader_visible`
  scenario dispatch case (+4 lines).
- `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` — scenario menu
  item + validation (+9 lines).
  → Without these three, committed state compiles but the Q5 toggle is unwired and
    the smoke-bot scenario is unreachable. **Commit them and add them to the
    IMPLEMENTER_REPORT.md file table.**

Cruft to clean before re-submitting:

- `Assets/Data/GreenSlopeConfig.csv` (+ `.meta`) — a **stray** earlier copy.
  The correct file is committed at `Assets/Resources/Data/GreenSlopeConfig.csv`
  (the only path `Resources.Load` can reach). Delete the `Assets/Data/` copy.
- `Docs/Diag/baked-pivot/M0-regression-DriverFromGreen.md` /
  `M0-regression-PutterFromGreen.md` — polluted with ~19 duplicate copies of the
  same 8 result rows from repeated smoke-bot runs. Revert to their committed
  state (they belong to the `baked-pivot` task, not this one). Note for follow-up:
  the smoke bot **appends** to these regression files instead of overwriting —
  that is a bot bug worth a separate Quick task.
- `Docs/Diagnostics/_capture/` — ~12 task-named PNGs left behind
  (`green_arrows_FINAL_*`, `putter_aim_arrows_*`, etc.). Per CLAUDE.md screenshot
  rule #5, don't litter `_capture/` with task-specific names. Clean them; the
  canonical screenshot is already copied to `screenshots/`.

## Resume procedure (orchestrator)

When Cesar confirms the Unity MCP hub is back:
1. `SendMessage` to implementer agent **`a0025d65a3c6c4b24`** (context intact —
   it knows the code it wrote). Instruct it to: close the 3 verifications above,
   commit the 3 omitted `.cs` files, clean the cruft, correct the
   IMPLEMENTER_REPORT.md file table, and re-set STATUS — `READY_FOR_SELF_REVIEW`
   if all 3 verifications PASS, otherwise stay on the architect path.
2. If the MCP hub is still flaky after restore, the implementer keeps retrying
   per `feedback_unity_mcp_transport_recovers.md`; it does not delegate `tests-run`.
