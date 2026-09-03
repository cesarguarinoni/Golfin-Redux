IMPLEMENTER_WORKING

# STATUS — `gps_checkin`

**Current:** `IMPLEMENTER_WORKING` — iter-1, 2026-09-03. **Blocked on two external gates, in this
order**, and everything that does not depend on them is done and compile-verified:

1. **Cesar applies the two migrations** (`2026_09_03_venue_partners.sql`, then
   `2026_09_03_seed_demo_spots.sql`) and **enables "Maps Static API"** on the Google key
   `playlife-api` uses. Until then the Fly deploy would ship routers calling functions that do not
   exist, and the admin Partners panel would query columns that do not exist — so BOTH deploys
   wait, deliberately.
2. **Unity is held by another session** (`gps_navbar_selected_tab` / `gps_profile_prompt_on_entry`).
   Every Unity-side artefact — running `GpsRoundsBuilder`, the two prefabs, the ShellScene
   `_gpsRoundsScreen` wire, play-mode capture, the geometry/invariants JSON, the UI-fidelity lint,
   `tests-run`, and the motion-parity video — waits on Cesar's say-so.

**Done and verified without Unity:** the whole backend (A1–A5) and its E2E script, the admin
Partners panel (B1) and the demo seed (B2), every Unity C# file for C1–C4, the art bake, and the
localization publish (C5). All five affected assemblies compile clean against Unity's own Roslyn
(see `IMPLEMENTER_REPORT.md` § Compile verification); `texts` is published at **v32** with 64
`GPS_ROUNDS_*` rows live and `export --check` clean.

**Deviations flagged:** D-1 (admin geocode is local, not via `/venue/geocode` — the dashboard has
no bearer token for the API), D-2 (a modal shell is baked rather than reusing `S_SU_ModalPanel`,
which is a different aspect ratio), D-3 (DETAILS raises a toast — there is NO venue-detail screen
in the project), D-4 (one line added to another session's `GpsNavBarHighlight.cs`). Decisions
D1–D6 are implemented as written.

| Date | State | Note |
|---|---|---|
| 2026-09-03 | `SPEC_READY` | Backend A1–A6, admin B1–B2, Unity C1–C5; decisions D1–D6. |
| 2026-09-03 | `IMPLEMENTER_WORKING` | Backend + admin + all Unity C# written and compile-clean; texts published v32. Awaiting Cesar's migrations and a free Unity. |
