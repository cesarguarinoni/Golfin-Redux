IMPLEMENTER_WORKING

# STATUS — `gps_checkin`

**Current:** `IMPLEMENTER_WORKING` — iter-1, 2026-09-03. **The backend and admin halves are DONE,
DEPLOYED AND PROVEN LIVE.** One gate remains, plus one small pre-req:

1. **Unity is held by another session.** Every Unity-side artefact — running `GpsRoundsBuilder`,
   the two prefabs, the ShellScene `_gpsRoundsScreen` wire, play-mode capture, the
   geometry/invariants JSON, the UI-fidelity lint, `tests-run`, and the motion-parity video —
   waits on Cesar's say-so. All five affected assemblies compile clean.
2. **"Maps Static API" is still not enabled** on the Google key `playlife-api` uses. `/venue/map`
   is deployed and reachable; it returns Google's `403 This API is not activated`, surfaced
   verbatim. Everything else works without it — the Rounds panel falls back to the stylised
   placeholder with the attribution hidden, exactly as §C4 specifies.

**Shipped and verified this session:** both migrations applied by Cesar; `e2e_activity_economy.py`
**ALL PASS** (38 assertions, invariant 0 violations before and after); Fly **v68**; the same flow
re-proven through the deployed routers over HTTPS (+30 → +15, replay 0, `409 already_active`);
admin deployed (`golfin-admin` `e92cc304`) with all three § B1 round-trips driven through the real
panel UI *and* against live data; `texts` v32 with 64 rows live and `--check` clean.

**Two real bugs found and fixed on the way, neither introduced by this task:**
* a bare `%` in a PostgREST filter makes Supabase's Cloudflare edge throw error 1101 — this had
  broken `/venue/nearby`, `/venue/search` and **user search** in production. All seven
  `like`/`ilike` sites audited and routed through `backend/pgrest.py` (Rule 15).
* the admin's mock mode reported writes it never performed, because venue fixtures were
  module-level state that Next dev does not share across route bundles. Moved onto `mockStore`.

**Deviations flagged:** D-1 (admin geocode is local — the dashboard has no bearer token for the
API), D-2 (a modal shell is baked rather than reusing `S_SU_ModalPanel`, a different aspect ratio),
D-3 (DETAILS raises a toast — there is NO venue-detail screen in the project), D-4 (one line added
to another session's `GpsNavBarHighlight.cs`). Decisions D1–D6 are implemented as written.

| Date | State | Note |
|---|---|---|
| 2026-09-03 | `SPEC_READY` | Backend A1–A6, admin B1–B2, Unity C1–C5; decisions D1–D6. |
| 2026-09-03 | `IMPLEMENTER_WORKING` | Backend + admin + all Unity C# written and compile-clean; texts published v32. Awaiting Cesar's migrations and a free Unity. |
| 2026-09-03 | `IMPLEMENTER_WORKING` | Migrations applied. E2E ALL PASS, Fly v68, admin deployed, all three panel round-trips proven. Two production bugs found + fixed. Only Unity (and the Maps key) left. |
