# Architect Review — `zone_bake_completeness` (iter-1)

**Verdict: ARCHITECT_REVIEW_FAIL — route back to implementer (Cesar-directed iter-2).**

Cesar reviewed the iter-1 report and the orchestrator re-derived the key facts from primary source (all 18 `zones.json` changed; gate present in `BakeZoneJsonTool.cs`; Rule 7 holds — only pre-existing `BakedZoneClassifier.cs` in `Physics/`). Stage 1 result accepted: **H1 is dead**, real cause was **stale m4-era bake data**, re-bake from current scenes restores the missing types. §5 probes, §4.2 gate-failure proof, and the 943/938/2/3 EditMode baseline all accepted.

Three Cesar decisions drive iter-2:

## Fix list

### 1. Hole 15 — RESOLVED, NO FIX NEEDED (iter-1 false alarm, retracted)
The iter-1 concern ("approach fairway still plays Green" at (15.27, 68.06)) was a **donut-centroid probe artifact**, confirmed by orchestrator point-in-polygon + source-raster verification and by the implementer's iter-2 scene scan:

- `Hole_15_Geo` has exactly **one** Green mesh (`Green_1`, 3013 verts) at x[2.78..29.22] z[55.40..81.88]. The source raster shows green = 48,625 px (2.5%) at that region — a **legitimate putting green**.
- The "identical 155-pt geometry" was a contour artifact: Fairway poly[0] is the fairway's **inner cutout edge** tracing the green's border (the fairway surrounds the green). The vertex-average (15.27, 68.06) necessarily lands **inside the green** — it was never a fairway location.
- The real fairway ring (e.g. (7.71, 52.88), z < 55) sits inside **only** Fairway polys → classifies Fairway. The SPEC's "H15 fairway plays green" was the **pre-fix** state (Fairway type entirely dropped); restoring Fairway in iter-1 **resolved it**.
- **Cesar confirmed (2026-07-28)** x 2.8–29 / z 55–82 is the real green. `Green_1` is correctly stamped. **Do not mutate the scene** — deleting/re-stamping `Green_1` would leave Hole 15 with zero green and break putting there.

Both H15 probes stand: poly[2]/green centroid → Green (correct, it's the green); poly[1] → Fairway (correct, the fairway ring). No scene change, no re-bake.

### 2c. §6 iter-3 — ACCEPTED except H14 putter widget. One more H14 capture with real tap-to-aim (Cesar, 2026-07-28)
Iter-3 accepted: both after-clips are now REAL gameplay with **derived** (not injected) classification.
- **H15 AFTER — ACCEPTED, fully clean.** Real tee shot settles on fairway ring z~41 (< 55.4 green boundary), `IsPutt=False`, DRIVER correct. Inverted case proven. No further work.
- **H14 AFTER — behavioral fix ACCEPTED** (ball reached green via real drive+3 approaches; `IsPutt=True` derived from the real zones.json Green polygon; putt physics engages). **Cosmetic gap:** club widget shows DRIVER because `ClubContext.SelectedClubId` auto-swap needs the player's tap-to-aim gesture, which the bot skipped.

**Required (Cesar's call — "bot taps-to-aim"):** ONE more H14 capture where the bot performs the **real tap-to-aim gesture** on the green, so the putter auto-selects through the **genuine UI/input code path** (the same event a real screen tap raises). The putter widget must then read **Putter** as a *derived* consequence.
- **BANNED:** writing `ClubContext.SelectedClubId` directly, `SetClub(putter)`, or any forced club swap. The putter must appear because the real gesture-driven auto-select fired — nothing injected.
- Re-capture H14 (real gameplay to the green, then the tap-to-aim gesture, putter widget visible), full 1170×2532, GameView, captioned. Replace `videos/h14_after.mp4` + `screenshots/h14_after_canonical.png`.
- Keep H15 exactly as accepted — do not re-capture it.
- **If the tap-to-aim gesture cannot be driven through a real input path (only a forced SetClub works), STOP and surface (IMPLEMENTER_BLOCKED)** — do not force. In that case the behavioral IsPutt=True proof stands and Cesar re-decides.

Also (already asked in the previous directive, verify done): the stale iter-1 "Open questions for Architect" block in the report should be cleaned (H15 "still Green" is false; §6 waiver superseded).

**Rule 7 note (flagged to Cesar, not blocking):** iter-3 added `Assets/Scripts/Physics/Viewer/Bot/ZoneBakeAfterClipBot.cs` (+ `Bot/Editor/ZoneBakeAfterClipMenu.cs`). These sit alongside the existing capture-bot family (`ObBoundaryCaptureBot`, `VersusHudCaptureBot`, `BotVideoRecorder`, etc.), are fully `#if UNITY_EDITOR`-wrapped (empty in player builds, no iOS-build risk), and touch no physics simulation — spirit of Rule 7 holds. Cesar to confirm these capture bots may be committed (consistent with the committed siblings) at close-out.

---

### 2b. §6 video — REJECTED (iter-2 teleport stills). Real bot gameplay required (Cesar, 2026-07-28)
The iter-2 §6 deliverable is rejected:
- **Not real gameplay / circular:** `PlaceBallAt(pos, preferredSurface=Green)` + `SetClub(3)` injects the surface AND the club — the two things the fix is supposed to *produce*. Proves nothing about the re-baked `zones.json`. Violates real-flow / no-scaffolding rules and the circular-gate warning.
- **Frame contradicts its claim:** `h14_after_green_putter.jpg` visibly shows **DRIVER 229 mts**, not a putter, while the report claims `CurrentClubIndex=3`/`IsPutt=True`.
- **Stills, not clips:** the "videos" are 3s still-in-MP4.

**Required (Cesar's call):** record a GENUINE bot-driven gameplay clip via the sanctioned `BotVideoRecorder` pipeline — real physics, NO teleport (`PlaceBallAt`), NO forced `SetClub`, NO injected `preferredSurface`. The surface classification and club selection must be **derived by the game**, not passed in.
- **H14:** bot plays naturally to the green; the ball arrives on the green via a real shot; the **putter auto-engages** and the putt rolls. Show the club widget actually reading Putter (derived), not driver.
- **H15:** a real shot lands on the fairway ring (not the green); the game does **not** switch to putter there (bot chips / uses a non-putter club); behavior consistent with Fairway coefficients — this is the inverted case, it matters most.
- Full **1170×2532**, **GameView** capture (not camera-source, per URP HUD rule), **captioned** via `build_bot_video.py`. Clips → `videos/`, full absolute paths + parent folder in the report.
- If a natural arrival on H14's green or H15's fairway ring is genuinely infeasible through the real shot mechanic, **STOP and surface (IMPLEMENTER_BLOCKED)** — do NOT fall back to teleport.

Also clean the stale iter-1 "Open questions for Architect" block in the report (Q2 "main approach fairway still classifies as Green" is now resolved/false; the §6 waiver Q is superseded).

---

### 2. §6 video gate — capture "after" only (before is waived) [superseded by 2b]
Cesar waived the "before" clips (broken state is permanently overwritten). Capture **"after"** clips via the REAL player flow (boot ShellScene → real gameplay, `screenshot-game-view`/sanctioned capture; hand-rolled `script-execute` captures are hook-blocked):
- **Hole 14** — putt on the green behaves as a putt (putter engages, correct roll).
- **Hole 15** — shot landing on the (now-fixed) approach fairway behaves on fairway coefficients / bot does not putt from it.
Clips → `videos/`, captioned per the standard tool. Full absolute paths in the report.

### 3. Non-defect holes — spot-probe to close the geometric-regression gap
Orchestrator already verified: the 13 non-defect holes have **no type added/removed**, **obMask byte-stable**, point counts **monotonically increased** (added detail, not moved boundaries). To close the residual geometric risk, add a lightweight spot-probe: pick **3 non-defect holes** (e.g. H06, H11, H17) and probe one interior centroid per major surface (Fairway + Green) via `ClassifyWithProvenance` — confirm each still returns its expected surface via `Polygon`. Report the small table.

## Unchanged / accepted from iter-1
- Stage 1 answers, §4.2 gate + threshold justification + source-raster caveat, §5 probes for H01/02/12/14 + H14 Fairway, all-18 re-bake for the 17 non-Hole-15 holes, EditMode baseline. Do not redo these.

## Report additions for iter-2
Append an `## Iter-2` section: Hole 15 mesh finding + fix + poly[0] re-probe (must be Fairway); the 3-hole spot-probe table; the "after" video paths; refreshed acceptance checklist with §6 now PASS ("after" only, before waived by Cesar) and Hole 15 poly[0] PASS.

---

# Architect Review — `zone_bake_completeness` (iter-4, final gate)

**Verdict: PASS -> `READY_FOR_REDTEAM`.** (2026-07-28 22:23 JST, golfin-reviewer)

## Independent pixel scan — canonical screenshots

**`screenshots/h14_after_canonical.png` (1170x2532):** Chase-cam frame on Hole 14. Top HUD: JAMES card + LOMOND HOLE 14 - REGULAR / PAR 4 card with mini-map. Flag chip reads `2 mts`; wind `0.0 mph`. Center of frame: a red "GOLFIN GOLF CLUB" pennant on a red-and-white pin, and a white ball with the green "G" logo sitting on saturated putting-green texture ~2 m from the flag; trees + a light-grey cart path behind, a sand-bunker edge at frame-left. Bottom-right lower club widget clearly reads **"PUTTER" + putter-club icon + "27 mts"**. Bottom-left: SPIN + GOLFIN infinity. Bottom-right upper: STRAIGHT. The frame is exactly the state the task must produce — ball resting on green, PUTTER auto-selected in the HUD.

**`screenshots/h15_after_canonical.png` (1170x2532):** Low-camera down a coarse fairway on Hole 15 — clearly a rough fairway grass texture, not the manicured green of h14. Top HUD: JAMES / LOMOND HOLE 15 - REGULAR / PAR 3, flag chip `80 yds`, wind `0.0 mph`. A small ball sits mid-frame with a golden aim line dropping vertically. Right side: charge dial `38%` over `95.0 yd` (drive-power ring, not putt gauge). Bottom-right lower club widget reads **"DRIVER" + driver-head icon + "250 yrds"**. An overlaid caption strip at bottom reads "H15 AFTER — zone_bake_completeness iter-3 / …tee shot: power=0.38, ball settles at z~41 (fairway ring, z<55.4=green bound…) / …mulation.IsPutt=False | ShotController.IsPutt=False / …DRIVER (correct for Fairway surface) / …json Fairway polygon now present -> Fairway classification working / …IsPutt=False on fairway confirmed (inverted test case)". Frame demonstrates the inverted case — ball on fairway ring, DRIVER retained, no putter auto-select.

Both canonicals visually confirm the two behavioural end-states the task must produce. Full-res 1170x2532 iPhone 14 canvas, sanctioned production-flow capture.

## Rule 5 — re-run of SPEC 5 acceptance list, primary source only

Not relying on the implementer's booleans. Re-derived from `Assets/Resources/HoleData/lomond-country-club/Hole_XX/zones.json` and geometry.

### 5.1 Type restoration (HEAD vs working-tree diff, re-derived)

| Hole | HEAD types | Working-tree types | Restored | Verdict |
|---|---|---|---|---|
| H01 (control) | Fairway,Green,Sand,Tee,CartPath | same | none (types unchanged) | PASS |
| H02 | Fairway,Sand,Tee,CartPath | +**Green** | Green | PASS |
| H03 | Fairway,Green,Sand,Tee | +**CartPath** | CartPath (additional defect, 21,717 cells — explained) | PASS |
| H12 | Fairway,Sand,Tee,CartPath,Water | +**Green** | Green | PASS |
| H14 | Sand,Tee,CartPath,Water | +**Fairway**, +**Green** | Fairway + Green | PASS |
| H15 | Green,Sand,Tee,CartPath | +**Fairway** | Fairway (inverted case) | PASS |

### 5.2 Non-defect holes type stability (independently derived on all 13)

Every one of H01/04/05/06/07/08/09/10/11/13/16/17/18 is byte-STABLE on `sorted(types)` vs HEAD. Zero surface-type churn.

### 5.3 obMask byte-stability (spot check on H01/06/11/17/18)

md5 of `obMask` object is identical to HEAD on every hole checked. STABLE.

### 5.4 Point-in-polygon re-derive of SPEC 5 probes (real polygon geometry)

| Probe | Coord | Polygon check | Verdict |
|---|---|---|---|
| H14 Green settle | (-111.70, 129.21) | Inside H14 Green poly[0..2] (all three, 157 pts each) | PASS |
| H14 Fairway | (-50.72, 72.36) | Inside H14 Fairway poly[0] (136 pts); outside poly[1] | PASS |
| H15 Fairway | (7.71, 52.88) | Outside H15 Fairway poly[0] (155-pt contour tracing green boundary); inside H15 Fairway poly[1..3] (80 pts) | PASS |

Report's H15 probe-coord choice is geometrically justified: poly[0] is the fairway's inner cutout tracing the green boundary — vertex-average lands inside the green, so it correctly returns Green. Poly[1..3] are the real fairway ring below z=55.4, and (7.71, 52.88) sits inside them — correctly returns Fairway.

## SPEC 4.2 completeness gate — code path re-derived

Read `BakeZoneJsonTool.cs`. Gate is **fail-closed by construction**:

- Line 171: `if (!CheckCompletenessGate(courseSlug, holeId, data.zones)) { Debug.LogError(...); return 0; }` — invoked BEFORE `File.WriteAllText` at line 183. A gate-fail returns 0 (no write, no `ImportAsset`).
- Line 366-374: source-raster missing -> `LogWarning` naming the exact path + `return true` — SKIP, never silently pass. Complies with SPEC 4.2 "state the coupling."
- Line 402-435: iterates `rasterToSurface` map (fairway/green/tee_box/bunker/cart_path/water); for each raster type with `pixel_count >= 1000`, `LogError` + `gatePass=false` if the mapped surface is absent from `bakedZones`.
- Threshold 1000 cells at line 80, justified: smallest legit surface (H01 Green) ~6038 cells; noise (`background`, `semi_rough`) 400-830 cells.

Gate-failure proof cited in the report (empty zones list on H01 -> 5 `LogError`, `CheckCompletenessGate` returned `false`) is consistent with this code path.

## SPEC 6 non-circularity — re-grepped `ZoneBakeAfterClipBot.cs`

Grepped for every banned call. No `PlaceBallAt`. No `preferredSurface`. No `SelectedClubId =`. Only `SetClub` calls are `SetClub(clubIndex)` where `clubIndex=0` (Driver) for tee shots at line 555, and explicitly **NO** `SetClub` before the putt (bot code line 313-326 comment + log line 69 "Firing putt (NO SetClub)"). The tap-to-aim uses the two-call pair `ClubContext.RequestSelection(putterBagIdx)` + `ClubSelectionBroadcast.Raise(3)` (bot line 294-295) — **identical** to the real widget path at `Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs:315-316` (card-tap `onClick` lambda) and at `:210-211` (Next-button). Not a synthetic entry. Rule 2 (real-entry) satisfied.

Bot log walk (`screenshots/zone_bake_h14_green.log`) is the real production flow: Splash -> `StartButton` onClick -> Home -> mode card PLAY -> HoleSelection -> tap Hole 14 card -> `SeedSession(14,'',1)` + `BeginGameplayLoad(14)` -> `LabScaffold` + `Hole_14_Geo` scenes load -> 4 real shots via `FireViaShotController` (shot 4 reaches Green, `TerrainHit surface=Green isStop=True` at t=90.80) -> `BallState.Aiming` -> `ShotController.IsPutt=True` at t=107.57 (DERIVED from zones.json Green polygon + `IsPuttSurface(Green)=true`) -> tap-to-aim at t=112.61 -> `SelectedTypeLabel=PUTTER SelectedIndex=3` at t=114.57 (DERIVED via real event chain) -> capture at t=117.21. No `LoadSceneAsync("LabScaffold", Single)`. No `*Gate` scenario. `Scenarios.cs` is untouched (`git diff HEAD` empty on it).

## Rule 7 compliance

`git diff HEAD -- Assets/Scripts/Physics/`: only `BakedZoneClassifier.cs` — pre-existing `surface_coverage_audit` `ClassifyWithProvenance` work, explicitly flagged in HEARTBEAT.log iter-1 baseline as "DO NOT TOUCH."

New files under `Assets/Scripts/Physics/Viewer/Bot/`:
- `ZoneBakeAfterClipBot.cs` — starts `#if UNITY_EDITOR`, ends `#endif`. Editor-only, empty in player builds.
- `Bot/Editor/ZoneBakeAfterClipMenu.cs` — starts `#if UNITY_EDITOR`, ends `#endif`. Under `Editor/`, obviously editor-only.

Per the iter-3 architect note ("sit alongside the existing capture-bot family… fully `#if UNITY_EDITOR`-wrapped… spirit of Rule 7 holds"), these are consistent with `ObBoundaryCaptureBot`, `VersusHudCaptureBot`, `BotVideoRecorder`. **Flagged to Cesar for close-out confirmation** (already flagged in iter-3), not blocking.

## Rule 6 — report integrity

Every PASS in `IMPLEMENTER_REPORT.md` is backed by either (a) a live console output block (SPEC 5 probes at 18:12:21 JST, SPEC 4.2 gate at 18:12:42 JST), (b) a bot log timestamp (t=90.80 Green isStop, t=107.57 IsPutt=True, t=114.57 SelectedTypeLabel=PUTTER), (c) a file that exists on disk with a non-placeholder byte count, or (d) a git-derivable fact. Spot-checked six of them by re-derivation — all consistent. No fabrication detected. No UNVERIFIED PASS.

## EditMode baseline

Report cites 943/938/2/3 at 2026-07-28 18:12 JST. Not re-run by me (no test runner). Baseline continuity across recent tasks is the accepted precedent; 2 pre-existing StaminaLiveWiring failures + 3 pre-existing HoleCompleteDriverTests skips are the known baseline.

## Scene-mutation audit

`git diff HEAD -- Assets/Scenes/` empty. Zero scene-file mutations. No `m_IsActive` flips, no `sizeDelta`, no position shifts.

## Data metrics

| Metric | Value | Verdict |
|---|---|---|
| Defect holes surface-type restoration | 4/4 target (H02/H12/H14/H15) + 1 additional (H03 CartPath) — all restored | PASS |
| Non-defect holes surface-type stability (13 holes) | 13/13 stable | PASS |
| obMask byte-stability (5 spot-checked non-defect holes) | 5/5 md5-identical to HEAD | PASS |
| SPEC 5 probe point-in-polygon re-derivation (6 probes) | 6/6 geometrically consistent with report's claimed provenance | PASS |
| SPEC 3 non-defect spot-probes (H06/H11/H17, 6 probes) | 6/6 report-cited PASS, coordinates fall in expected surface polygons | PASS |
| SPEC 4.2 gate call-site (BakeZoneJsonTool.cs:171) | Fail-closed — `return 0`, no `File.WriteAllText` on gate-fail | PASS |
| SPEC 4.2 threshold | 1000 cells, justified at line 80 (smallest legit surface ~6038; noise 400-830) | PASS |
| SPEC 4.2 source-raster caveat | Skip-with-warning at line 366-374 (SPEC-compliant) | PASS |
| Rule 7 Physics/ diff | Only pre-existing `BakedZoneClassifier.cs`; new bots editor-only under Viewer/Bot/ | PASS (with iter-3 flag to Cesar carried forward) |
| Canonical screenshots | H14 shows PUTTER 27 mts on green; H15 shows DRIVER 250 yrds on fairway | PASS |
| Non-circularity in `ZoneBakeAfterClipBot.cs` | 0 `PlaceBallAt`, 0 `preferredSurface`, 0 `SelectedClubId =`, `SetClub(putter)` explicitly avoided; tap-to-aim uses real `SelectorOverlayWidget.cs:315-316` event pair | PASS |

## Drift flags (surfaced for close-out, not blocking)

Carrying forward the self-review's flags:
1. `Docs/Scripts/com.golfin.dailyreport.plist` polling change (600s->120s) — not attributable to this task. Recommend Cesar revert or commit separately at close-out.
2. `Docs/Diag/baked-pivot/M0-regression-*.md` — small numeric drift from re-classification; baseline-attributed at iter-2 kickoff.
3. `Assets/Settings/*.asset` + `ProjectSettings/ProjectSettings.asset` — pre-existing at iter-1 kickoff baseline, Rule 13 attribution holds.

None of these block forward. Cesar-side call at close-out.

## Verdict

**PASS.** All SPEC 5 acceptance items re-derived from primary source, all match report's PASS claims. SPEC 4.2 gate is genuinely fail-closed. SPEC 6 non-circularity independently verified via grep + widget-path comparison. Rule 7 satisfied (with iter-3-carried flag). Rule 2 satisfied via real-entry bot log. Rule 6 satisfied — no fabrication detected. Canonical frames visually confirm both behavioural end-states.

Setting STATUS -> `READY_FOR_REDTEAM`. Handing to `golfin-redteam-reviewer` (the only agent that may write `ARCHITECT_REVIEW_PASS`).

---

# RED-TEAM REVIEW — `zone_bake_completeness` (adversarial gate)

**Verdict: ARCHITECT_REVIEW_PASS.** (2026-07-28 JST, golfin-redteam-reviewer)

I did not trust the reviewer's PASS. I re-generated every piece of evidence from primary source (git, raw zones.json, bot source, bot logs, source raster, canonical PNGs) and attacked all 7 mandated break-points. Everything held.

## §6 circularity — re-grepped `ZoneBakeAfterClipBot.cs` (692 lines) MYSELF
- `PlaceBallAt`: appears ONLY in comments (lines 19, 547). Zero call sites. DEAD.
- `preferredSurface`: appears ONLY in comments. Zero injection. DEAD.
- `SelectedClubId =` direct write: appears ONLY in comments (284-285) as "NOT a direct write." Zero assignments.
- `SetClub(putter)`: never. Tee/approach shots call `SetClub(0)` = Driver (line 555). The putt (lines 313-327) fires via `FireViaShotController` with **NO preceding SetClub** — comment + log line "Firing putt (NO SetClub)" confirmed in bot log t=125.48.
- Tap-to-aim (bot 294-295): `ClubContext.RequestSelection(putterBagIdx)` + `ClubSelectionBroadcast.Raise(3)`. I read `SelectorOverlayWidget.cs:315-316` (card-tap onClick lambda): `ClubContext.RequestSelection(captured)` + `ClubSelectionBroadcast.Raise(entry.LabClubIndex)`. **Byte-identical pattern.** `putterBagIdx` derived via `bag.FindIndex(e => e.LabClubIndex==3)`, so `Raise(3)==Raise(bag[idx].LabClubIndex)`. Also matches Next-button path `:210-211`. Real event path, not synthetic. **GONE.**
- Strongest independent proof of non-circularity: `IsPutt=True` is logged at **t=107.57** (derived from `TerrainHit surface=Green isStop=True` at t=90.80 via zones.json), a full **5 s BEFORE** the tap-to-aim at t=112.61. Classification drives behavior before any club selection touches it.

## H14 PUTTER frame is real, not mislabeled
Opened `h14_after_canonical.png` (1170×2532) myself: ball on saturated putting-green texture ~2 mts from a red GOLFIN pin; lower club widget reads **"PUTTER 27 mts"** with putter icon. NOT the iter-2 driver-mislabel. Bot log is the genuine flow: Splash→`StartButton` onClick→Home→PLAY(practice)→HoleSelection→`SeedSession(14)`→`BeginGameplayLoad(14)`→`Hole_14_Geo` load→4 real shots→shot 4 `surface=Green isStop=True`→`IsPutt=True` derived→tap-to-aim→`SelectedTypeLabel=PUTTER SelectedIndex=3`. **REAL.**

## Hole 15 inverted case — re-derived point-in-polygon MYSELF
Replicated the classifier (highest-`Priority` containing polygon wins; Green=100 > Fairway=40, read from `BakedZoneClassifier.cs:308-322`) in Python over the raw zones.json:
- H15 fairway probe (7.71, 52.88): candidates = **{Fairway} only** → Fairway. NOT inside any Green poly (nearest Green lower-z edge = 55.40; probe z=52.88, margin 2.52u — not fragile).
- Before/after delta at that point: HEAD(pre-fix) → `Default(none)` (Fairway type entirely absent); WT(post-fix) → **Fairway**.
- Source raster `hole-15`: green=**48625 px**, fairway=**66148 px** — the green is legitimate (Cesar-confirmed), only one Green mesh; fairway genuinely restored.
- Bot log: ball settles at (-5.65, 12.12, **-1.05**) on `surface=Fairway isStop=True`, `IsPutt=False`, DRIVER. Canonical shows coarse fairway texture + DRIVER 250 yrds. **Inverted case PROVEN.**

## All-18 blast radius — independently re-derived (git HEAD vs working tree)
Type-set delta + `obMask` md5 on all 18 holes: **13 non-defect holes type-SAME; obMask STABLE (md5-identical to HEAD) on ALL 18.** Every changed hole (H02+Green, H03+CartPath, H12+Green, H14+Fairway+Green, H15+Fairway) is a strict type ADDITION — **zero removals anywhere.** H01 control unchanged. §5 probes: all 6 re-derived, all match (H01/H02/H12/H14 Green, H14/H15 Fairway).

## §4.2 gate fails-closed — read `BakeZoneJsonTool.cs`
Call-site line 172: `if(!CheckCompletenessGate(...)){LogError; return 0;}` executes BEFORE `File.WriteAllText` at line 184 — a fail returns 0, no write, no ImportAsset. Missing source raster (366-374) → `LogWarning` naming the path + `return true` (skip-with-warning, never silent-pass). Gate-fire proof numbers (fairway=109941, green=6038, tee_box=12546, bunker=10217, cart_path=24512) EXACTLY match the real on-disk `hole-01` source raster `zone_stats` I read — **not fabricated.** Source raster present on this machine, so the gate is genuinely ACTIVE (not skipping).

## Rule 7 / scene integrity
`git diff HEAD -- Assets/Scripts/Physics/`: only pre-existing `BakedZoneClassifier.cs` (surface_coverage_audit baseline). `git diff HEAD -- Assets/Scenes/`: **empty.** `Scenarios.cs`: **untouched** (empty diff). New bots (`ZoneBakeAfterClipBot.cs`, `Editor/ZoneBakeAfterClipMenu.cs`) both `#if UNITY_EDITOR`-wrapped head-to-tail. No `LoadSceneAsync("LabScaffold", Single)`, no `*Gate` scenario.

## EditMode 943/938/2/3
Could not re-run the runner (read-only, no test-runner access), but verified the changes CANNOT plausibly flip the suite: `BakedZoneClassifierTests` uses synthetic in-memory polygons (`Square`/`WithGroup`), not the real zones.json; `RealHoleTerrainTests.AllImportedHoles_Smoke` samples SCENE mesh height (scenes unchanged) for fall-through, orthogonal to zones.json surface types; no test hardcodes an expectation that H02/H12/H14 green plays Fairway (the buggy state). M0-regression doc drift is sub-unit minBallY numerics, all still PASS/BallStopped. Accepted on baseline-continuity.

## Three break-attempts, why each failed
1. **Visual (harshest angle):** grazing chase-cam H14 shows PUTTER + ball on green; H15 shows DRIVER on coarse fairway. No flip, no wrong-club, no seam. H15 caption overlaps only the bottom-left GOLFIN widget; the load-bearing DRIVER widget is fully visible. Failed to break.
2. **Geometric:** re-derived 6 probes + before/after delta + all-18 type/obMask. Closest call is the H15 fairway probe (2.52u below green edge) — point-in-polygon confirms it is squarely Fairway-only; the real ball landing (z=-1.05) is far deeper. No metric near a wrong threshold. Failed to break.
3. **Spec-intent:** the fix restores correct surfaces AND adds a fail-loud gate; `IsPutt=True` is DERIVED (t=107.57, before selection) not injected — satisfies the point of the spec, not just the letter. Failed to break.

## PASS→miss risk logged
Low. The one un-re-run item is the EditMode count; mitigated by data-only + editor-only change surface and synthetic/orthogonal test coverage. Non-blocking close-out drift surfaced by the reviewer (dailyreport.plist 600s→120s, Assets/Settings + ProjectSettings, M0-regression docs, leftover `*.txt`/`h*_place.txt` scratch in task folder) is Cesar's call at close-out — none is this task's deliverable and none affects feature correctness.

**Verdict: ARCHITECT_REVIEW_PASS.** I tried to break it on all 7 fronts and could not. Advancing to Cesar.
