READY_FOR_ARCHITECT_REVIEW

# STATUS — `gps_profile_prompt_server_flag` (Quick)

**Current:** `READY_FOR_ARCHITECT_REVIEW` — Claude Code, 2026-09-04. Built and deployed.

The Golf Profile offer is now once per ACCOUNT. Backend is **live**: Fly image
`deployment-01M1MNKVRKBW4SGFQTAPC316DD`, machines 68 → 69, and the deployed `openapi.json` carries
`golf_profile_prompted`. Proven end to end against the deployed API (clear → stamp → fresh GET
echoes → `false` does not clear), and both ways in the Editor (server-stamped account → hub;
cleared column → capture once).

Building this found a real defect in `gps_standalone_shell` round 1: the shell's boot resolved the
offer itself and jumped over the account-flag wait, so a fresh install of an already-answered
account still saw the capture. Fixed in the same change.

EditMode 2398 / 2395 pass / 0 fail. Remaining: the two cross-app device rows (`GPS_DEVICE_PASS.md`
§1b), which need two installs on one phone.

| Date | State | Note |
|---|---|---|
| 2026-09-03 | `SPEC_READY` | `profiles.golf_profile_prompted_at` + `PUT /user/update golf_profile_prompted`; ShouldOffer reads the server flag; Skip now writes. |
| 2026-09-04 | `READY_FOR_ARCHITECT_REVIEW` | Endpoint deployed + verified; Unity reads the account flag behind a bounded one-round-trip wait; Skip writes; local flag re-cached. Round-1 boot defect found and fixed. |
