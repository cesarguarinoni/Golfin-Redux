SPEC_READY

# STATUS — `gps_profile_prompt_server_flag` (Quick)

**Current:** `SPEC_READY` — Architect, 2026-09-03. Cesar: the Golf Profile screen is once per ACCOUNT —
completed or skipped in the game, the standalone app, or another phone → never offered again anywhere.

**Order:** run BEFORE `gps_standalone_shell`'s device check (the shell's first launch is exactly the
case this fixes). Migration + backfill APPLIED by Cesar 2026-09-03 (3 of 19 profiles already prompted). Code writes the migration file as the record (idempotent) and goes straight to the endpoint + Fly deploy.

| Date | State | Note |
|---|---|---|
| 2026-09-03 | `SPEC_READY` | `profiles.golf_profile_prompted_at` + `PUT /user/update golf_profile_prompted`; ShouldOffer reads the server flag; Skip now writes. |
