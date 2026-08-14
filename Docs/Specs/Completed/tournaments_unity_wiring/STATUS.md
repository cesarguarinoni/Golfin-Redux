DONE

# tournaments_unity_wiring — Phase 3 + 3b — ✅ DONE (Cesar, 2026-08-14)

Accepted by the Architect, then approved by Cesar. Both halves shipped:
server `playlife ece354f` (deployed), client `506b55b75`.

## What shipped

The game reads its tournament schedule from the server. A tournament created in the dashboard —
name, dates, prizes and **artwork** — reaches players on the next launch with no build.

- Boot applies cache-or-CSV **synchronously**, then fetches and recomposes through
  `TournamentService.ComposeFrom`. `ITournamentBackend`, `LocalTournamentBackend` and `DeriveState`
  are untouched; state stays client-derived. `Golfin.Net` was never added to `Golfin.Tournaments`.
- Display name resolves `localize(name_key) → title → slug` on the card and both modals.
- Art resolves `banner_url` → `Resources/TournamentImages/{course_id}` → placeholder, behind a
  Uri-parsed host allowlist, with a disk cache and a bounded LRU sweep.
- The positional `_courseImages[csvIndex]` fallback is gone from the class and the prefab.

## Final verification

- **EditMode 1233 total / 1230 passed / 0 failed** (3 pre-existing skips), swept per assembly and
  reconciling exactly to 1230.
- All three headline fixes **tripwired** — reverted one at a time, the matching test went red each
  time, then restored.
- **All three schedule sources observed live:** `BUNDLED CSV`, `DISK CACHE`, `SERVER (live fetch)`.
- **Acceptance 9 exercised in production** — real save entries produced two `DEFERRED update` lines
  while the server schedule was applied.
- **Acceptance 4 closed with real art** — cache wiped → launch 1 downloaded and cached 19158 B under
  the SHA-256-derived key → launch 2 logged `Cache HIT … no download`, arriving via
  `WarmArt → Prefetch` on the DISK CACHE boot path (which also proves B6 in production).

## Left open, deliberately

- Acceptance 1 (a brand-led row end-to-end) and 3 (airplane-mode first-ever run) want a device.
- `_placeholderImage` intentionally unwired — an art call, accepted in review §D.
- Phase 4 note from review §A: `list_golfin` pops the uuid but `POST /tournaments/{id}/enter` is
  uuid-keyed. Decide slug-vs-uuid when entries are built.
