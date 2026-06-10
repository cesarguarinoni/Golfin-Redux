# Architect Review — `1v1_match_flow` (Phase 2a)

**Reviewer:** Cesar (architect, direct decision) · **Date:** 2026-06-09 · **Verdict:** ARCHITECT_REVIEW_FAIL (redo with 2 concrete fixes)

The Phase-2a code is sound — all 11 §14 acceptance items pass on code review, the turn-flow/courtesy logic and asmdef-boundary RP bridge are correctly built, and the canonical frame confirms the alternation + OPPONENT'S TURN banner render correctly. Two concrete items must be resolved before re-review; **do not re-architect anything else.**

## Fix 1 — §15 visual gate: defer the recorder start (do NOT touch the GPU guardrail)

The match resolves correctly (a prior play-mode run logged `[VersusMatchController] P2 WIN` at ~25s), but the 30s BotVideoRecorder watchdog cut the clip before the resolution banner because ~5s of the window was spent on hole-load before the first shot.

**Decision:** DEFER the recorder start until AFTER the hole finishes loading / the match is ready to begin its first AnnounceTurn, so the full 30s window points at the match itself. The match fits in ~25s, so removing the ~5s of load slack should capture through the WIN/LOSE/DRAW banner inside the existing window.

- **Do NOT** change the watchdog duration or weaken the GPU-saturation guardrail (`b06547d5`). The whole point is to satisfy §15 *without* increasing GPU-reboot risk.
- Re-record at full **1170×2532**, showing: opening banner → P1 shot → OPPONENT'S TURN → bot shot → alternation → **a sink → the courtesy shot → the WIN/LOSE/DRAW banner**.
- Caption with `Docs/Scripts/build_bot_video.py` (textfile= idiom), frame-extract one still per video into `screenshots/` to verify the caption + that the resolution banner is on screen, then declare `Canonical video:` in the report.
- If the deferred-start still can't capture resolution inside 30s for some reason, STOP and report back rather than bumping the watchdog — escalate to Cesar.

## Fix 2 — §11 safety cap: real CSV row, not a `[SerializeField]`

`versusStrokeCapOverPar` must be a real CSV-keyed lookup (default 5), not the bare `[SerializeField] _strokeCapOverPar` currently on the component. Wire the CSV value into `VersusMatchController`'s cap logic (mirror how `versus_1v1.rewards` is read from `ModesDatabaseCSV`, or add a row to an appropriate existing config CSV — pick the cleanest seam and cite it). Keep default 5 when the row is absent.

## Not required / accepted as-is
- The `_suppressOpeningBanner` timing (no double banner observed in the canonical frame) is accepted.
- Everything else in IMPLEMENTER_REPORT §14 stands; re-verify the two touched areas after the fixes and re-run the SOLO regression once more before transitioning.

## Routing
After both fixes + the re-recorded resolution video, transition STATUS to `READY_FOR_SELF_REVIEW` (the §15 FAIL is now resolvable, so the self-review path is appropriate). Update IMPLEMENTER_REPORT (§15 → PASS with the new video, §11 deviation removed, add a `## Rejection follow-up` section addressing both fixes with the new video citation).

---

## Iter-3 decision (Cesar, 2026-06-09) — capture the §15 video on a short par-3

FIX 2 (CSV cap) landed correctly. FIX 1's deferred-recorder-start mechanism is correct, but it surfaced the true bottleneck: **Hole 18's physics sim is ~16.8s wall-clock per driver shot (321m carry), so a full match can't resolve inside the 30s GPU-guardrail window** even with a deferred start.

**Decision: capture the full-match resolution video on a SHORT PAR-3 hole** (tee near the green) so the whole match — tee shot → approach → sink → courtesy → WIN/LOSE/DRAW banner — resolves comfortably inside the existing 30s window.
- Keep the deferred-recorder-start mechanism already built.
- Do **NOT** bump the watchdog and do **NOT** weaken the GPU-saturation guardrail (`b06547d5`). This path satisfies §15 with zero added GPU-reboot risk.
- Pick the shortest par-3 in the Lomond course for the capture scenario (query `HoleContext.Par` / the hole DB; choose the one whose tee→pin distance keeps each shot's sim short). The §15 gate cares about the match FLOW + resolution, not which hole — a par-3 is in fact a cleaner demo of the full loop.
- This is a CAPTURE-scenario change only (in `VersusHudCaptureMenu.cs` / the recorder scenario), not a gameplay change — the shipped 1v1 route is untouched.

---

## Iter-4 decision (Cesar, 2026-06-10) — fix both bugs found during the par-3 attempt

The par-3 capture surfaced two compounding bugs. Both are authorized to fix.

**BUG A (capture tool — authorized, clearly in-scope).** The `versus_full_match_flow` scenario seeds `MatchContext.Players[i]` with display data but never sets `Lie`, so the ball spawns at world origin `(0,14.6,0)` instead of the tee. Fix: set `Players[0].Lie` and `Players[1].Lie` to the chosen par-3's actual tee position in the capture-scenario seeding block (`VersusHudCaptureMenu.cs`). Editor-only capture code; no production impact.

**BUG B (production bot — authorized; minor deviation from spec §8.2's literal 'port first-stroke=Driver').** `VersusBot.SelectShot` hard-codes Driver at full power on every first stroke, so it overshoots any par-3 by ~170m and plays par-3s badly in the REAL shipped game. Decision: **make the first stroke distance-aware**, matching the spec's higher-order goal (decision #6: "competent, straight shots toward the cup"). Replace the `isFirstStroke → Driver full power` override with a club ladder by distance that applies on stroke 1 too:
- long tee (e.g. dist > ~180m) → Driver (club 0), full/high power
- mid (e.g. dist > ~110m) → Iron7 (club 1), distance-scaled power
- short approach (> 40m) → Wedge (existing tier)
- then the existing chip/long-putt/short-putt tiers
(Clubs: 0=Driver, 1=Iron7, 2=Wedge, 3=Putter.) Tune thresholds so the chosen par-3 (~110m) reaches the green competently in 1–2 shots AND a long par-5 still gets a Driver off the tee (verify both — do NOT regress long-hole play). This is a real competence fix, not just a capture hack.

After both fixes: re-record the full par-3 match (sink → courtesy → WIN/LOSE/DRAW banner) at 1170×2532 inside the existing 30s window (no watchdog change), update IMPLEMENTER_REPORT (§15 PASS, Rejection follow-up covering BUG A + BUG B + the new video), and transition to `READY_FOR_SELF_REVIEW`.

---

## BUG C — first shot goes UNDER the terrain on the par-3 capture (Cesar caught live, 2026-06-10) — BLOCKER

While watching the iter-4 par-3 capture run (Hole 04), Cesar saw the **first shot fly a normal-ish distance toward the hole but pass UNDER the rendered terrain surface — every time.** This is NOT the BUG-B driver overshoot (the ball did not sail far past the hole). It is a **simulation-ground vs visual-mesh collision mismatch**: the sim's ground surface is below the rendered terrain, so the ball arcs and ends up under the visible mesh.

**Important scoping facts:**
- The versus work (`VersusMatchController`/`VersusBot`/etc.) never touched `PhysicsLabController`'s ground-provider, `SurfaceSnap`, or sim path. So BUG C is a deeper issue *exposed* by loading Hole 04, not introduced by the turn-flow code.
- `OnHoleLoaded(sceneName)` → `TryLoadBakedProviders(holeId)` is supposed to load the baked ground provider for the loaded hole (`Hole_04_Geo` → `Hole_04`). The lab's serialized default/fallback is Hole 18.
- The working tree has **pre-existing uncommitted `TerrainData_Hole04Geo.asset` drift** (plus holes 03,05,07,08,09,11,12,13,14,15,16). A stale/mismatched baked bake vs the current visual mesh on Hole 04 is a prime suspect.

**Leading hypotheses (must be confirmed at runtime, NOT guessed):**
1. The additive hole-swap / capture path doesn't actually fire `OnHoleLoaded` for Hole 04 (or fires for the wrong hole), so the sim keeps Hole 18's baked ground provider while rendering Hole 04's mesh → vertical mismatch → ball under terrain.
2. `TryLoadBakedProviders("Hole_04")` fails or loads a stale bake (pre-existing TerrainData drift) that doesn't match the current visual mesh.
3. A first-shot timing race: the shot fires before the baked/scene ground provider is rebuilt for the new hole.

**Required diagnostic (run on the main thread the moment Unity MCP reconnects — it is currently DISCONNECTED):**
- On the Hole 04 versus capture, log at the first shot: which baked provider / TerrainData is active, `_useSceneProviders`, the sim ground Y vs a downward raycast onto the visual mesh at the ball's xz, and the ball's at-rest Y vs the visual surface Y.
- Compare **SOLO Hole 04** first shot vs **VERSUS Hole 04** first shot: does solo also tunnel? (If solo is clean and versus tunnels → versus-path timing. If both tunnel → Hole-04 baked-provider/TerrainData issue, likely the pre-existing drift.)
- Only after the mismatch is measured do we fix (rebuild/recommit the Hole 04 bake, fix the provider re-resolve on hole-swap, or add a provider-ready gate before the first shot), then re-measure to confirm.

Do NOT proceed with any further §15 capture until BUG C is root-caused and fixed.

### BUG C root cause — CODE-CONFIRMED (2026-06-10), pre-existing & solo-reproducible

Cesar reproduced it in **solo Practice on Hole 4**: shot straight at the green from the tee at 50% power; ball bounced fine on the green and fairway, then went **through the terrain behind the green on the 3rd bounce**. Reading the sim ground path:

- Sim ground = `Golfin.Physics.Runtime.Baked.BakedHeightProvider`, loaded per-hole from `Resources/HoleData/Hole_04/{zones,heightmap}` (`PhysicsLabController.TryLoadBakedProviders`). This is **separate** from the visual `TerrainData_Hole04Geo.asset`.
- `BakedHeightProvider.SampleHeight(x,z)`:
  - **Path A — inside a baked zone polygon** (tee/fairway/green/bunker): returns the polygon's interpolated **mesh Y** → exact match to the visible surface. ✅ ball bounces correctly on green + fairway.
  - **Fallback — outside ALL zone polygons** (rough behind/around the green): returns the raw **baked heightmap** Y + offset. The code comment states the heightmap is deliberately NOT trusted inside zones because *"heightmap.bytes captures the post-depression terrain, while zone meshes are built on un-depressed terrain"* — i.e. the heightmap is KNOWN to disagree with the mesh. Outside zones there is no mesh-Y to use, so the misaligned (lower) heightmap wins → **sim ground sits below the visible mesh → ball passes through the rendered terrain.**
- `HeightmapData.SampleHeight` **clamps** OOB lookups to the grid edge (never 0), so this is a value-misalignment, not a missing-coverage gap.

**Scope:** pre-existing; affects ANY hole; triggers whenever a ball comes to rest / bounces **outside the baked zone polygons** (rough behind a green, wide of a fairway, etc.); reproduces in **solo Practice**, so it is NOT 1v1-specific and the versus work did not cause it. It is its own physics/ground-provider bug.

**Still needs MCP for:** measuring the exact heightmap-Y vs mesh-Y delta behind Hole 4's green, and validating whichever fix we choose. (As of 2026-06-10 the Unity MCP server is up on Unity's side but the Claude Code harness still has the `ai-game-developer` tools deregistered — needs a reconnect before any runtime work.)

**Candidate fixes (decide after measuring; do NOT blind-fix):**
1. Out-of-zone fallback samples the **visual mesh** (raycast) instead of the heightmap — restores mesh-accurate ground in the rough (note: scene-raycast was "removed in Phase F", so this is a partial reversal scoped to OOB-of-zones).
2. Add a full-hole **rough/terrain zone polygon** (with mesh Y) to the bake so Path A always has a mesh Y.
3. Re-bake the heightmap to match the visual mesh everywhere (eliminate the depression discrepancy) — biggest/riskiest.

**Recommendation:** spin this out as its own physics task (`green_surround_ground_tunnel` or similar); it is broader than 1v1 and deserves a focused fix + regression. 1v1's §15 capture can proceed independently IF the competent bot (BUG B fix) lands on the green and sinks while staying inside the zone polygons — but that is fragile until BUG C is fixed.
