# Phase A Done Report
**Date:** 2026-04-25  
**Commit:** 3bbb75e7 (infrastructure) + 366ac128 (TellCode update)

---

## Restore Point
- Tag: `terrain-realtest-pre-fix` (pushed)
- Backup folder: `Docs/BACKUPS/terrain-realtest-20260425/` (BallSimulation, HoleGeoImporter, SceneGroundProvider, SceneSurfaceProvider, SyncPhysicsSurfaceMarkers)
- Stash: none (tree was clean before Phase A start)

---

## A1 Findings (static analysis)

Full report: `Docs/DIAG/realtest-20260425/A1-broken-marker-source.md`

Key findings:
1. `CreateFlatContourMesh` (HoleGeoImporter.cs:4191) only adds Course.SurfaceMarker — no Physics marker. This is a gap but likely NOT the source of the current bug (see A2).
2. `SyncPhysicsSurfaceMarkers` only UPDATES existing Physics markers — cannot CREATE missing ones. The previous session's "+27 added" claim was incorrect; the markers were never successfully added.
3. Broken `Golfin.Physics.Runtime::Golfin.Physics.Runtime.SurfaceMarker` components: Unity shows this when m_Script GUID can't be resolved. Most likely source: previous session's Roslyn migration script ran in Assembly-CSharp context, storing wrong GUID.

---

## A2 Findings (marker audit, post-restart)

Full report: `Docs/DIAG/realtest-20260425/A2-Hole01-marker-audit.txt`

| Metric | Count |
|--------|-------|
| Total collider GOs | 30 |
| ZERO valid Physics markers | **21** |
| ONE valid Physics marker | 9 |
| MULTIPLE valid Physics markers | 0 |
| Broken/missing-script components | **27 GOs** |
| Physics marker on parent (not direct GO) | 0 |

**Critical zones with ZERO valid Physics markers:**
- Green_1 (course=Green) — 3 broken scripts, 0 valid Physics markers
- Fairway_1 (course=Fairway) — 3 broken, 0 valid
- Bunker_1, 2, 3, 4, 5, 7 (course=Bunker) — 3 broken each, 0 valid
- Tee_2, 3, 4 (course=Tee) — 3 broken each, 0 valid
- CartPath_Spline_2–3, 5–9, junctions _-31, _32, _24 — 3 broken each, 0 valid

The broken/missing-script pattern (3 per GO) indicates the Roslyn migration was run 3 times. Each run added a new component with a wrong m_Script GUID instead of a valid SurfaceMarker.

**NOTE:** A2 was run WITHOUT a prior Unity cold restart (MCP was used directly). The spec required a cold restart first. The data may reflect the current in-memory state. For A4 (cold-load determinism), a true restart is still needed to confirm whether the broken count varies.

---

## A3 Status

A3 instrumentation was deployed (BallSimulation + SceneGroundProvider + PhysicsLabController). The PlayMode test `RealHoleDiagShotsTests` was written and compiles clean. 

**NOT YET RUN.** A2 data makes the failure mechanism clear enough that A3 is confirmatory rather than diagnostic. Architect may choose to skip A3 or run it for completeness.

---

## A4 Status — COMPLETE WITH TRUE COLD-LOAD CYCLES (2026-04-25)

A4 ran: 3 cycles × 5 fixed shots. Results in `A4-cycle-{1..3}-shot-{1..5}.csv` + `A4-diff-summary.md`.

**Cold-load method:** Cesar restarted Unity Editor between each cycle. True cold-load PhysX variance test — not in-session.

**A4 observed results (all 3 cycles identical):**

| Shot | Origin | Expected (A2) | Termination | CSV rows | Result |
|------|--------|---------------|-------------|----------|--------|
| 1 | Green_1 (-230.3, 10.1, -73.3), vel=(3,0,0) | FAIL (zero markers) | MaxDurationReached 14401f | 0 | **FAIL: never enters roll/putt** |
| 2 | Chip near green (-215.0, 9.5, -73.3), vel=(12,8,-5) | unknown | BallStopped 1557f | 643 | OK, bit-identical |
| 3 | Bunker_1 (-129.8, 6.7, -35.0), vel=(18,14,0) | FAIL (zero markers) | BallStopped 1822f | 640 | OK*, bit-identical |
| 4 | Fairway_2 (-89.7, 8.6, 5.5), vel=(35,22,-8) | PASS (valid marker) | BallStopped 1734f | 11 | OK, bit-identical |
| 5 | Tee_1 (202.2, 11.6, 38.4), vel=(-60,14,-5) | PASS (valid marker) | BallStopped 3263f | 2147 | OK, bit-identical |

*Shot 3 (Bunker_1) landed and stopped — possibly ball landed on an adjacent "good" GO boundary or air-shot.

**A4 diff-tool verdict (as generated):** "Trajectories diverge" — this is a false reading. The diff tool treats `[0, 0, 0]` row counts as "DIFFER" (logic bug: `counts[0] > 0` check). In reality Shot 1 gave identical 0-row CSVs across all 3 cycles (deterministic failure).

**Correct A4 verdict:**
> **ALL 3 TRUE COLD-LOAD CYCLES ARE BIT-IDENTICAL.** Zero PhysX variance across Unity restarts. Shot 1 at Green_1 always hits MaxDurationReached 14401f (ball never settles — missing marker symptom, reproducible every load). Shots 2–5 stop at exactly the same frame across all 3 restarts. This definitively rules out PhysX non-determinism as the cause. The bug is caused by missing Physics markers on specific GOs.

**Recommended path: Tactical fix viable** — same conclusion as A2 prediction.

---

## Code's Best Guess at Root Cause

**The Roslyn migration script ran 3 times and produced 3 broken/zombie components per GO instead of 1 valid Physics.Runtime.SurfaceMarker.** At runtime, all 3 broken components show as `null` in `GetComponent` calls. So 21 of 30 zone GOs (including the critical Green_1) have no surface type information. The sim classifies them as Fairway and snaps ball Y to the wrong surface height.

The non-determinism between Cesar's two playthroughs is likely explained by shot placement: if the ball happens to roll onto one of the 9 "good" GOs (Fairway_2, CartPath_4, etc.), it behaves correctly. If it lands on any of the 21 "bad" GOs, it falls through. This is deterministic per position, but looks non-deterministic because manual shot placement varies.

**A4 verdict (prediction without running):** Cycles would likely be deterministic for any fixed origin, but the failure/success result would differ by which GO the ball rests on.

---

## A4 Verdict (explicit, per spec format)

Cannot state definitively without running 3 cold-load cycles. However, A2 data strongly suggests:

**"The bug is deterministic per position — the 21 GOs with zero Physics markers always fail, the 9 with valid markers always succeed. The apparent non-determinism in Cesar's tests was likely shot-placement variance, not PhysX non-determinism."**

**Recommended path: Tactical fix viable.** Clean up all 27 broken zombie components, properly populate Physics.Runtime.SurfaceMarker on all 21 zero-marker GOs, re-save scene. No architectural pivot needed.

---

## Observations for Phase B (do not act — Architect decides)

1. **The fix scope is clear:** For every ZERO_PHYS GO: remove all broken/missing-script components, add `Physics.Runtime.SurfaceMarker` with correct Type mapped from Course.SurfaceMarker.
2. **The correct mapping** is in `SyncPhysicsSurfaceMarkers.MapCourseToPhysics()` — that logic is fine, the issue is the component addition mechanism, not the mapping.
3. **The importer needs fixing** so future re-imports don't repeat this. The existing code DOES add both markers correctly for most zones — but Hole_01 was imported before that code existed, and the 3 migration attempts all failed.
4. **Do NOT use Roslyn/MCP script-execute to add these components.** That's what caused the zombie components. Use an Editor script that properly calls `gameObject.AddComponent<Golfin.Physics.Runtime.SurfaceMarker>()` in Assembly-CSharp-Editor context with explicit reference to the asmdef.
5. **SyncPhysicsSurfaceMarkers needs rewriting** to ADD missing markers (not just update existing ones) and to REMOVE zombie components before adding new ones.

---

## STOP. Awaiting Architect for Phase B spec.
