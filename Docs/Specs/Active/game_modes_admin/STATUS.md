READY_FOR_SELF_REVIEW

Red-team iter-3 ESCALATED (no defect; it declined to rule on shipping an untested
live-on-save payout path). Cesar chose: add the vitest suite first.

Done. `Tools/admin-dashboard` now has vitest (`npm test`) and 36 tests over the
pure surfaces — contentValidate (the modes rules + the versus_1v1-only drift
warning + row-id bounds), the Rewards number guards, and the golfin_mode_fees row
mapping. Tripwire-verified: generalising the drift warning fails exactly the test
that forbids it; allowing a negative entryFee fails too; both reverted -> 36 pass.

Also closed the evidence gap red-team could not reach: all six malformed PATCHes
fired at the DEPLOYED /api/rewards route were refused (-5, 1.5, "20", negative
caps, unknown action -> 404 with NO row created), and game_point_actions read
back at baseline with 4 rows.

Scope note for the gates: the dashboard diff since the last deploy is test infra
only. Redeployed anyway so the live stamp still equals HEAD.
