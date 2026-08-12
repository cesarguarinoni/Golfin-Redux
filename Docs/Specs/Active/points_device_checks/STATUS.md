AWAITING_DEVICE_PASS

---

# STATUS — points_device_checks

- **2026-08-13 — AWAITING_DEVICE_PASS (created).** Split OUT of `reward_points_backend` at Cesar's
  close-out so that task could move to `Docs/Specs/Completed/` and the `admin_dashboard` hold
  could lift. Contents: the 3 manual device checks from `reward_points_backend`'s
  `IMPLEMENTER_REPORT.md` Part 3 § "Needs Cesar (manual, on device)" — (1) signed-out launch
  cannot pass Login when you tap around the art, not just the buttons; (2) flag-ON shop purchase
  stays debited after a refresh, and an airplane-mode purchase grants nothing; (3) double-tap BUY
  is a no-op on a slow connection.

  **No code is pending and nothing is known to be broken** — the RP backend shipped green
  (EditMode 1172/0/3 of 1175, flag ON in prod). These are the three things the Editor structurally
  cannot prove: a real touch test, a real network drop, a real double-tap. **Only Cesar can run
  them** (needs a device build with the `GOLFIN_POINTS_BACKEND` define + a live signed-in account
  with a non-zero balance — new accounts start at 0 by design).

  Next state: `DONE` (→ move to `Docs/Specs/Completed/`) if all three pass, or `DEVICE_FAIL` +
  a `DEVICE_FAIL.md` if any fails, which then becomes its own fix task. See SPEC §4.

---

## States for this task

```
AWAITING_DEVICE_PASS  - waiting on Cesar to run the 3 checks on a device
DEVICE_FAIL           - a check failed; DEVICE_FAIL.md records it, fix task spawns
DONE                  - all three passed; move folder to Docs/Specs/Completed/
```

This task does **not** run the subagent pipeline (no implementer / self-reviewer / reviewer /
red-team). There is nothing to build — it is a manual verification record.
