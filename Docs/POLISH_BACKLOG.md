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
