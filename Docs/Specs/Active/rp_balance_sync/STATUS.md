# STATUS — rp_balance_sync

- **2026-08-13 — SPEC_READY.** Filed from Cesar's observation that the nav-bar RP counter doesn't reflect the backend. Root cause diagnosed in code: `PointsService.OnBalanceChanged` has no subscribers, `RefreshBalanceAsync` has exactly one non-test caller (the editor menu), and `RewardPointsManager.SetPoints` is flag-OFF-only — so with the flag ON no server balance can reach the UI. Kickoff in TellCode.md.
- Next state: `IN_PROGRESS` when Claude Code starts.
