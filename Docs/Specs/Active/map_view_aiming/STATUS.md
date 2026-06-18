# STATUS — `map_view_aiming` (Order 352)

**Tier:** FULL PIPELINE (Tier 3)
**State:** SPEC_READY — awaiting `Use the implementer subagent on "map_view_aiming"`.

## Log
- **2026-06-18** — Scoping complete. 6 forks resolved by Cesar: hero-angle camera (overrides kickoff's ortho lock), drag+tap aim, pinch-zoom+pan, aim-only, all-buttons-hidden-except-club (relabel "SHOOT" = close), markers = ball/flag/landing-zone/mocked-trajectory+power-rings. Genre research (Golf Clash / Golf Rival) confirmed the draggable-landing-target + guide-line + ring-band idiom. Live code verified: entry widget + RT surface + landing/ring/trajectory visuals are net-new; carry from `_maxCarryYards`; no per-club accuracy field (rings = fixed % band for v1); reuse `AimLineBendRenderer.LateralAtT` curve math world-space. SPEC.md authored. Notion 352 → In Progress.

## Reference
- Old-UI screenshot (Cesar, this session) = visual reference. If dropped to disk: `reference_old_ui.png` in this folder.
- Kickoff (superseded): `Docs/Specs/Queued/map_view_aiming_KICKOFF.md`.
