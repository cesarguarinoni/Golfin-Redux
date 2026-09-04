# POLISH_BACKLOG.md — deferred polish-phase items

Items consciously deferred to the polish phase (Roadmap item 9: UI/UX Polish). Not ship-blockers. Each entry records enough context + findings to resume cold without re-deriving. Do NOT pick these up during active ship work — they're parked here on purpose.

---

## P-001 — H10 green/collar small carve-edge artifact
**Filed:** 2026-06-02 (Architect). **Area:** `HoleGeoImporter.cs` green/collar terrain carve. **Severity:** minor, cosmetic.

**Context:** The collar↔terrain seam on the two terrain-bordered greens (H10, H18) was originally a ~1 m rasterized-`SetHoles` sawtooth. Resolved NOT with the mesh apron (that approach was dropped — a Lit mesh can't be made invisible against TerrainLit terrain: terrain lighting normals always point up, plus a known mobile TerrainLit-lighter-than-Lit bug; see the apron-invisibility spike). Instead the **terrain carve was moved slightly further inside the fringe** so the collar covers the carved edge. H18 came out clean; **H10 has a very small residual artifact** at the carve edge (Cesar: "leave for polish").

**When resumed:** look at H10's collar↔terrain edge specifically (H10 has the larger ~0.19 m proud terrain rim, which is why it's the one with a residual where H18 is clean). Likely a small over/under-coverage where the inward-moved carve meets the collar on the proud-rim side. Check from the grazing arc. Reference: `Docs/Specs/.../green_ship_polish/` apron-invisibility spike + findings, and the carve-inward change that superseded it.

---

## P-002 — Fringe/collar steeper than desired on fairway-bordered greens
**Filed:** 2026-06-02 (Architect). **Area:** `HoleGeoImporter.cs` collar Y-blend. **Severity:** realism polish.

**Context:** Cesar finds the collar/fringe rings steeper than ideal. Investigation findings (so they don't get re-derived):
- **Do NOT widen the collar.** Real-world collars are ~0.9 m / 3 ft (USGA: championship collars ≤36"); `GreenCollarWidth` is already 0.9 m. Widening makes it read as an unrealistic wide apron skirt, not a collar. (Web-researched 2026-06-02.) The real fix is to reduce the vertical drop the 0.9 m collar must shed, not to lengthen the ramp.
- **Collar slope = (green-edge height − outer-ring height) ÷ 0.9 m.** Steepness comes from how much height the collar sheds over its fixed 0.9 m.
- **Terrain-bordered greens (H10/H18) are already gentle** — H18 measured offline at 2% mean / 5–6% edge collar slope from terrain fall. NOT the problem.
- **The steepness is almost certainly on the 16 FAIRWAY-bordered greens**, at the green-edge↔fairway transition (collar meets the fairway mesh, not terrain). This was NOT measurable offline: the export (`fairway-contours.json`, `greens.json`) carries only 2D (x,z) contours — both the fairway surface height and green-edge height are importer-computed in Unity, absent from export data.

**When resumed — required first step (Unity-side, can't be done offline):** add a diagnostic to `HoleGeoImporter.cs` that, per fairway-bordered green, reports collar inner-ring Y (green edge) minus outer-ring Y (fairway edge) per vertex → the real collar slope distribution. That determines whether the fix is (a) green edge sheds too much height into the collar, (b) fairway sits too low relative to the green edge, or (c) other. Spec the realism fix against those real numbers — do NOT extrapolate from H18 (it's terrain-bordered, different case). Likely fix direction: move the height transition so the 0.9 m collar stays near-flat (like a real mown collar) and the elevation change happens as the green's own fall-off / a separate gentle run-off beyond the collar — NOT by fattening the collar.

**Guardrails for whoever takes it:** must not disturb the B1 fitted-plane seat, the blessed collar↔fairway CDT weld, `relH`/slopes/tiers, or re-introduce the iter-14 mound. Per Lesson AC, don't touch collar code while any other green task is mid-run.

---

## P-003 — Vestigial resolver output: AimConeReductionFraction computed but never consumed
**Filed:** 2026-06-02 (Architect). **Area:** `StatModifierResolver` / `ResolvedShotModifiers` aim-cone path. **Severity:** code-health / consistency.

**Context:** While investigating `club_control_aim_arrow_speed` (closed as already-implemented), found that `ResolvedShotModifiers.AimConeReductionFraction` is computed by the resolver and assigned in the struct ctor, but **nothing functionally consumes it**. Grep across Assets returns only the struct assignment + a stale `ShotInputBuilder.cs:26` comment claiming "consumed by the aim reticle UI." The cone width the player actually sees is computed independently in `ShotController.HalfConeAngleRad()` as a lerp on **Club.Accuracy / 120** via `ControlsConfig` — not from the resolver.

**Why it matters:** two parallel sources of truth for aim-cone behavior (resolver vs ControlsConfig) invite drift; the resolver value looks authoritative but is dead. The audit's "CC → sub-perceptible cone reduction" finding was measuring this dead lane.

**When resumed:** decide one of — (a) delete `AimConeReductionFraction` from `ResolvedShotModifiers` + resolver if the ControlsConfig path is canonical (simplest; fix the stale ShotInputBuilder comment too), or (b) re-route `HalfConeAngleRad()` to consume the resolver output if the resolver is meant to be canonical (larger; unifies Club.Accuracy + Char.ClubControl into one cone computation). Lean (a) unless there's a reason the resolver must own cone geometry. Not a ship-blocker; no gameplay effect either way since the value is currently unused.

---

## P-004 — Ball passes through fringe/border on Hole 4
**Filed:** 2026-06-16 (Architect). **Area:** `Hole_04_Geo` fringe/border collider (`HoleGeoImporter.cs` collider gen). **Severity:** functional (ball leaves playable surface), Hole-4 specific. Found during Order 350 audio play-testing — NOT an audio bug.

**Context:** A normal shot on Hole 4 sent the ball **through** the fringe/border mesh instead of colliding/resting on it. Likely a collider gap, or the fringe/border submesh not carrying a `MeshCollider` on `Hole_04_Geo`. Cross-ref `Docs/Pipeline/LESSONS_FRINGE_BORDER_MESHES.md`.

**When resumed:** dump `Hole_04_Geo`'s collider coverage on the fringe/border submesh — confirm whether that submesh has a collider at all, and whether there's a seam gap at the fringe↔terrain join. Compare against a hole that behaves correctly. Verify with a bot/manual shot across the Hole-4 fringe after the fix.

---

## P-005 — Ball falls through terrain into Hole-4 bunker
**Filed:** 2026-06-16 (Architect). **Area:** `Hole_04_Geo` bunker terrain / physics heightmap (`Resources/HoleData/Hole_04/heightmap.bytes`) + bunker-lip colliders. **Severity:** functional (ball drops below world), Hole-4 specific. Found during Order 350 audio play-testing — NOT an audio bug.

**Context:** Shooting into a Hole-4 bunker dropped the ball **out of the world / below the surface** — classic stale-heightmap / missing-collider fall-through. Matches the recurring project pattern: balls fall through terrain outside baked zones because `heightmap.bytes` is stale.

**When resumed:** re-bake `Hole_04_Geo`'s physics heightmap via `PhysicsHeightmapBaker`, copy to `Resources/HoleData/Hole_04/`, then audit the bunker-lip colliders (see bunker-lip collider notes in `Docs/Pipeline/`). The standard fix for this pattern is re-bake + copy-to-Resources. Verify with a bot shot landing in the Hole-4 bunker.

**Note:** P-004 + P-005 are both `Hole_04_Geo` collider/terrain issues — likely worth resuming together as one Hole-4 collider/heightmap pass (the "physics stress test" umbrella).

---

> **Order 352 caveats consolidated here (canonical).** The implementer also left stubs at `Docs/Specs/Queued/club_bag_population_concern/` and `Docs/Specs/Queued/map_view_polish/` — same items; treat P-006–P-010 as the source of truth.

## P-006 — CONCERN: map club carry populated as a stopgap, not from save state
**Filed:** 2026-06-22 (Architect, at Order 352 close). **Area:** `MapViewController` / `ClubContext` club-carry hydration (cf. `task_6d0326e9`). **Severity:** CONCERN (not cosmetic) — open question; resolve before trusting map distances.

**Context:** During `map_view_aiming` the club bag / carry was populated as a **stopgap** rather than read from the player's **save state**. Cesar's flag: we HAVE save states — unclear why they weren't used. The landing zone + power bands hang off `_maxCarryYards`, so if that's a stopgap (not the real equipped-club value), the map shows wrong distances.

**When resumed:** trace where the map's club carry comes from vs the real save-state loadout; confirm `_maxCarryYards` is fed from the player's actual equipped club via the save system, not a hardcoded/stopgap default. Cross-ref Order 421 (`rp_save_test_isolation`) + the `ClubContext` `SelectedDistance` hydration blocker in the 352 STATUS. Likely a source swap once the save-state read is confirmed available at map-open time.

---

## P-007 — Landing zone / rings project onto trees
**Filed:** 2026-06-22 (Architect, Order 352 close). **Area:** map overlay landing-zone + ring decals (`MapViewController`). **Severity:** cosmetic.

**Context:** The red→green landing-zone decal (and likely the ring decals) project onto tree canopies/props instead of clipping to ground/terrain only.

**When resumed:** mask the decal projector to terrain/course layers so it doesn't paint trees/props; confirm zone + rings land on ground surfaces only.

---

## P-008 — Map zoom-out distance feels limited
**Filed:** 2026-06-22 (Architect, Order 352 close). **Area:** map camera pinch-zoom range (`MapViewController`). **Severity:** UX polish.

**Context:** Max zoom-out doesn't pull back far enough.

**When resumed:** extend the zoom-out clamp / camera distance bounds; tune against long holes.

---

## P-009 — Distance bands missing on the map view
**Filed:** 2026-06-22 (Architect, Order 352 close). **Area:** map overlay (`MapViewController` / `ShotConeView` distance bands). **Severity:** UX polish (regression vs prior behavior).

**Context:** The yardage distance bands shown elsewhere are NOT on the map — Cesar wants them back. Distinct from the 80/100/120 power rings.

**When resumed:** confirm which distance-band reference is meant (the cone's bands), then render them on the map as ground-projected bands consistent with the §6-MODEL anchoring to L.

---

## P-010 — Map-open recenter hiccup (1–2 frames)
**Filed:** 2026-06-22 (Architect, Order 352 close). **Area:** `MapViewController.Open()` camera framing. **Severity:** cosmetic.

**Context:** On opening the map, the camera recenters/reframes for a frame or two (a brief pop) before settling.

**When resumed:** compute the bounds-fit framing BEFORE the first rendered frame (set the map cam position/zoom in `Open()` prior to enabling the overlay) so frame 1 is already centered — no post-open recenter.

- **Mode entry fee is charged on PLAY, before hole selection** (surfaced 2026-08-13 during `rp_balance_sync` acceptance — a 10 RP practice fee was debited, then the player returned to Home). If backing out of hole select forfeits the fee, decide refund-on-abandon before players see it: refund on cancel, or charge at hole-start instead of at PLAY. Cheap now; a review complaint later. Undecided — Cesar's call.

---

## Game polish track (Architect, 2026-09-03) — deferred from `design_consistency_audit` / `game_polish`

Rows added when the spec that deferred them was delivered (WORKFLOW_NOTES rule). When taken up, move the row into the new spec and delete it here.

| Id | Item | Deferred from | Needs |
|---|---|---|---|
| P-011 | **In-game HUD / shot UI consistency audit** (power gauge, club selector, spin selector, map overlay, hole-complete widget, in-game settings modal are audited only as modals). The shell-canvas screens are the audit's scope; the 1080×1920 `ShotUI_Canvas` has its own divisor history (`FIGMA_UNITY_SIZE_MISMATCH.md`). | `design_consistency_audit` scope | its own audit pass with the same dumper, on the gameplay scene, after the shell audit's fix list lands |
| P-012 | **Rubik Medium static font import** — the variable face renders Medium ~5 % narrow; 208 GPS sites listed in `Completed/gps_polish` IMPLEMENTER_REPORT (iteration 1, A10); the game's `Rubik-VariableFont_wght` sites (525 serialized) are the other half | `gps_polish` D9 → `design_consistency_audit` out-of-scope | one import + a font-asset swap spec, after the audit says which sites are Medium by design |
| P-013 | **New linter rules the audit will want** (candidates: `UnityEngine.UI.Shadow` present; `Image.Type.Filled` on a 9-sliced sprite; serialized size not on the type scale; font asset is `LiberationSans`) — the audit reports these by hand, the rule makes them a gate | `design_consistency_audit` out-of-scope (Rule 21 gates every task on the linter) | a `UIFidelityLinter` spec with a tripwire per rule (§20) and a re-run of every `_lint.json` baseline |
| P-014 | **Auth / Loading / Splash screens** — Tier 2 in the audit (inventory + lint only, no crop sheet) | `design_consistency_audit` | crop sheets against Splash `2032:327` / Loading `4096:1181` / login `4062:4971` if the Tier-2 dump shows S1 findings |
| P-015 | **Haptics** — game + GPS together behind a Settings toggle | Cesar, 2026-09-03 (Notion 2130, parked) | its own spec after `game_polish`; `UiMotion` stays haptics-free |
