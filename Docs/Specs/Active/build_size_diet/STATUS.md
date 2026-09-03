SPEC_READY

# STATUS — `build_size_diet` (GAME track — Notion 2121)

**Current:** `SPEC_READY` — Architect, 2026-09-03 evening. Brief: `Docs/BUILD_SIZE_AUDIT.md`.
Target: install ≤ 1.0 GB (from ~1.9 GB), `Golfin.ipa` ≤ 350 MB (from 711), zero visible change,
byte-identical physics (heightmap is int32 Q16.16 → lossless `GHM2`).

**Order:** after the GPS queue (`gps_profile_prompt_server_flag` → `gps_standalone_shell` round 2)
unless Cesar runs it in the game session in parallel. Phase 5 (terrain alphamaps) is
measurement-only until Cesar writes "go" here.

**Open on Cesar:** Phase 1 spruce crops (visual sign-off); Phase 4 static-font verdict (a/b);
Phase 5 A/B.

| Date | State | Note |
|---|---|---|
| 2026-09-03 | `SPEC_READY` | 5 phases: pack-texture overrides + prototype audit; HoleData lossless GHM2 + gzip zones; 93-texture sweep; static JP atlas (measured); terrain resolution table + A/B. |
