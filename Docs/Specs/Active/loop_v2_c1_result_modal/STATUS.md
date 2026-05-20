# STATUS — Stage C1 ShellScene Result Modal

| Field | Value |
|---|---|
| Status | **SPEC_READY** — awaiting Cesar's lock of Q1/Q2/Q3 (or "go" to accept recommended defaults) |
| Pipeline | FULL PIPELINE |
| Authored | 2026-05-20 ~15:00 CEST |
| Parent | `Docs/Specs/Active/loop_v2_scope/SPEC.md` lines 117–250 |
| Repo state | clean at `25cd7fd2` (Mac, on `main`, tree clean apart from gitignored captures + unrelated font/manifest drift) |
| Notion | Order 310 (C1) — to be added to GOLFIN_Roadmap |

## Architect's pre-flight summary

All 9 pre-flight items resolved in SPEC §3. Three repo-verified API gaps surfaced (none blockers):

1. `RewardPointsManager.EarnPoints` (NOT `AddPoints` as scoping SPEC line 246 implied) — locked.
2. `ItemManager.AddItems` requires an itemId; `HoleReward` has no tier — defaults to `repairkit_common` (Q2 lock).
3. `BallManager.AddBalls` **does not exist** — C1 ships a 12-line additive mutator + defaults to `ball_golfin` (Q3 lock).

Plus one architectural addition not pre-existing: a production `HoleCompletionBridge.cs` that owns the `OnShotComplete → MarkHoleComplete` translation including FAILED-via-stroke-cap. `HoleCompleteDriver` is stripped to lab-debug-only.

## Files in this folder so far
- `SPEC.md`

## Awaiting before kickoff
1. Cesar's answers (or "go") on Q1/Q2/Q3 in SPEC §4.
2. Notion entry added to GOLFIN_Roadmap (Order 310).

## Open paranoia carried forward
- Phase B Stage 3 deferred until Loop v2 ships.
- Demo videos drop at `Docs/Videos/loop_v2_c1_result_modal/`.
- Mac paths only; Code uses MCP scene wiring (never paste-for-Cesar).

## After kickoff (Code paste-into-Code prompt)

```
Read Docs/Specs/Active/loop_v2_c1_result_modal/SPEC.md and implement Stage C1 — ShellScene Result modal. FULL PIPELINE — pre-flight grep first (BallStateMachine.OnShotComplete signature, ShotResult fields, HoleDatabaseLoader.GetHole API, ModalController hierarchy on ShellScene). Use Unity MCP for ShellScene + LabScaffold wiring; never paste scene diffs for Cesar. Build prefabs (HoleCompleteModal, ShotHistoryRow, Toast) cloning visual structure from the lab HoleCompleteCardWidget where applicable but as ONE card (no Card 2 stacking). Run all 7 EditMode tests + 4 smoke-bot scenarios locally before commit. Report back when done.
```
