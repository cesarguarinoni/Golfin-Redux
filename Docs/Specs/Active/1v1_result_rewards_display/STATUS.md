# STATUS — 1v1_result_rewards_display (Order 347)

**State:** Stage 0 DONE (Cesar-approved) · **Stage 1 SPEC_READY — kickoff pending**
**Priority:** P2
**Spec:** `Docs/Specs/Active/1v1_result_rewards_display/SPEC.md`

## Stage ledger
- [x] **Stage 0** — `VersusResultScreen.prefab` built via `VersusResultScreenBuilder.cs`; win/lose
      states; real `MMModal` clone + portraits. **Approved by Cesar 2026-07-01 after iter-11**
      (3 rejections, final = RANK→separator 24px reposition). No handler/scene edits. Spawned a new
      always-on font-weight + rendered-size review gate (`ce5823a21`).
- [ ] **Stage 1** — present `VersusResultScreen` as a **modal** after the banner + live binding. ← KICKOFF
- [ ] Stage 2 — CSV-driven multi-reward grant + reward-row binding (shared `RewardGranter`) + NEW MATCH
- [ ] Stage 3 — polish (win/lose reward brightness, draw variant D2, transitions)

## Decisions
- **D1 ✅** reward = CSV-driven multi-reward (RP + repair kit + ball, +gacha future); reuse
  hole-complete `HoleData.RewardType` / grant system. (Stage 2)
- **D2 ⏳** draw visual — no Figma; proposed neutral columns + greyed rewards. (Stage 3)
- **D3 ✅** NEW MATCH = requeue same mode `versus_1v1` via matchmaking.
- **D4 ✅** presentation = **modal**, mirror `HoleCompleteModalController` (ShellScene-resident
  `ModalController`, event-bridge) — NOT a ScreenManager screen. `VersusResultHandler` already IS that
  event bridge.

## Stage 1 scope (kickoff target)
`VersusResultHandler`: drop silent-grant + auto-home → banner plays → present `VersusResultScreen.prefab`
as a modal → `VersusResultScreenController` binds outcome + both players (reuse
`MatchmakingModalController` portrait/username/level/rank binding; ranks from `LeaderboardManager`) +
played-hole line → NEW MATCH requeues `versus_1v1`. Reward row still placeholder until Stage 2.
