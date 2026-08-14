READY_FOR_ARCHITECT_REVIEW → ✅ ACCEPTED (Architect, 2026-08-14)

Both halves landed and reviewed: server `playlife ece354f` (deployed), client `506b55b75`.
All six review blockers closed and tripwired; spot-checked `TournamentArtPolicy.IsAllowed`
(Uri-parsed, userinfo + non-default port + escaped dot segments all rejected, fails closed)
and `TournamentScheduleMapper.MergePreservingEntered` (a real union, not an intersection).

Remaining, both Cesar-side and neither blocking:
- Acceptance 1 — brand tournament end-to-end on a device.
- Acceptance 3 — airplane-mode cold launch, first ever run.
- The art download path now has a live object to run against:
  `tournament-art/lomond_championship-8a7161e9de90.png` (260x360, 19 KB, public GET verified,
  carried in the /tournaments/golfin payload). Seeded by the Architect via curl, so it serves
  `cache-control: no-cache` — harmless, the client cache is keyed by the immutable URL.
