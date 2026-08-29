READY_FOR_SELF_REVIEW

Phase A only (data + content catalogs + server truth + admin). Phases B, C and D not started.

Phase A is deployable on its own with the mode still locked, and that is the state it is in:
`modes.missions.locked` is still `true` in the bundled CSV, so nothing built here is reachable
by a player.

Blocked on Cesar before Phase A can be called complete — see § Blocked on Cesar in
IMPLEMENTER_REPORT.md:
  1. apply 2026_08_29_missions.sql, then 2026_08_29_content_missions_seed.sql
  2. publish `texts`
  3. publish `missions` + `mission_tiers` (writes the two server mirrors; needs step 1 first)

Dashboard is deployed: Cloudflare Version ID 4ccabd61-e47c-402b-a9b8-1ac49f890088, from
commit 0ef3bd912.
