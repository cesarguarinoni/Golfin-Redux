ARCHITECT_DIRECTED

# STATUS — `map_view_aiming` (Order 352)

**Tier:** FULL PIPELINE (Tier 3)
**State:** ARCHITECT_DIRECTED (2026-06-20) — iter-21's 6 issues are all visual-model defects on a CORRECT, now-FROZEN v2 architecture (§F). Architect authored the fix: **§6-MODEL** anchors guide line / rings / labels / landing zone / flag-position / open-aim to ONE shared landing endpoint **L**; **§11+** adds deterministic asserts for each of the six so the gate can no longer be green while they're wrong. Surgical delta = overlay-drawing methods of `MapViewController.cs` + the validator + one `controls.csv` field (`RING_FRAC`=0.15). DO NOT touch §F (camera/render/entry/carry/framing/capture/ball-cull). One implementer pass against the extended gate. Report that triggered this: `ARCHITECT_REPORT_iter21.md`.

**Model clarified w/ Cesar (2026-06-20), checked vs `reference_old_ui.jpg`:** (a) the guide line **HAS a gentle bow** — kept; it foreshortens to near-straight from the near-axial camera, do NOT flatten it. (b) The rings are **dark, semi-transparent, PROJECTED onto the terrain** (clearest where they cross the green), NOT white strokes — kept concentric at L. §6-MODEL + §11+ updated; an earlier Architect misread (flatten line / drop rings) was reverted.

## Cesar feedback on iter-20 canonical (2026-06-19) → iter-21
1. **Ring labels (120%/100%/80%) overlap** — they're stacked on top of each other. Put EACH label ON its own ring line (one label per ring, positioned at that ring).
2. **Shot-UI ball still visible in the map** — the "G" GOLFIN central ball is bleeding through. iter-20's cull claim was FALSE. It's Shot-UI chrome → add it to the hide-on-open set (the v2 rewrite dropped it). Confirm it's gone in the map view.
3. **Concentric rings = ACCEPTED.** Keep. Keep tight framing too.

## iter-20 result + iter-21 fixes (2026-06-19)
- ✅ Rings concentric/nested at landing (120 outer → 80 inner) — matches reference. KEEP.
- ✅ Camera tight, hole fills frame, no off-field visible. KEEP.
- ❌ **Carry regressed to 154yd (driver fallback).** iter-20 capture equips NO club → `ClubContext.SelectedDistance=0` → fallback `MaxCarryYardsForMap=154`. iter-19 correctly showed 124 (club). The §11 validator does NOT assert carry, so it slipped through. iter-21: the capture MUST equip a real club so the map shows the CLUB's carry (≈124, like iter-19 / reference 7-Wood). If `SelectedDistance` cannot hydrate even with a club equipped, that's the `task_6d0326e9` ClubContext blocker → IMPLEMENTER_BLOCKED + surface it; do NOT silently fall back to 154.
- ❌ **Stale/mixed JSON not cleaned.** Folder has iter-19 (124yd) + iter-20 (154yd) state files. iter-21: delete ALL prior invariant JSON; emit ONLY current-model states (club carry, concentric rings, tight frame).
- ⚠️ **Verify guide-line/ball coherence:** confirm the cyan guide line runs FROM the ball UP to the landing/rings (rings sit at the landing toward the green), ball marker is at the ball — not inverted.

## Carried context
- Hole indicator yellow icon = ACCEPTED v1; real flag-widget+line = future `task_7d4fdd3a`.
- §11 gate = `validate_invariants.py` (do NOT edit/weaken; reviewer+redteam run it, ignore implementer booleans). Iteration breaker live in route_subagent.py.
- SPEC.md v2 + v2.1 MODEL CORRECTION (top) authoritative. reference_old_ui.jpg = target look.
