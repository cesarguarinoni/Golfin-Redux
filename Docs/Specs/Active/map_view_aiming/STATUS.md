# STATUS — `map_view_aiming` (Order 352)

**Tier:** FULL PIPELINE (Tier 3)
**State:** RESET — SPEC v2 authored 2026-06-19 after iter-15 escalation. Awaiting fresh implementer run from SPEC §A.

## Log
- **2026-06-19** — **iter-15 escalation adjudicated (Architect).** Pipeline twice marked PASS on a feature unopenable in real play + upside-down. Decisions: (1) **drop RenderTexture → 2nd full-screen overlay camera** (kills the Metal-flip tar pit at the source); (2) **verification gate replaced**: bot-video-as-gate → **world→screen invariant JSON assertions** (`map_view_invariants.json`, SPEC §11) that work without Cesar; bot must drive the **real `HoleCardWidget`** (synthetic button banned → entry-point bug un-hideable); (3) flag → **in-game hole indicator WITH line to hole** (not a flag icon on the pin, not an 18× mesh); (4) ground visuals → **projected decal/shader over terrain** (rings + landing zone), not clipped quads/under-terrain lines. SPEC rewritten to v2. Pipeline-wide fixes → `Docs/PIPELINE_HARDENING.md` (iteration circuit-breaker, real-entry rule, math-not-pixels gate, `ffmpeg -ss` ban, reviewer full-list re-run, fabricated-claim auto-FAIL). Architect owns: v1 SPEC over-locked the RT path. Notion 352 stays In Progress.
- **2026-06-18** — Scoping complete, SPEC v1 authored (RT path — now withdrawn).

## Reference
- `ARCHITECT_ESCALATION.md` (Code's iter-15 post-mortem) — the record that triggered v2.
- `reference_old_ui.png` (if dropped) — hole-indicator-with-line treatment.
- Kickoff (superseded): `Docs/Specs/Queued/map_view_aiming_KICKOFF.md`.
