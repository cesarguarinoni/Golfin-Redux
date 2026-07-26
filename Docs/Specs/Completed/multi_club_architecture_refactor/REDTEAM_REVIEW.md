# Red-Team Review — `multi_club_architecture_refactor`

**Gate:** `golfin-redteam-reviewer` (adversarial, last gate before Cesar)
**Iteration reviewed:** iter-6 (`READY_FOR_REDTEAM`)
**Written:** 2026-07-25 00:35 JST
**Verdict:** `ARCHITECT_REVIEW_PASS`

> **This file supersedes my iter-4 verdict in full.** That review FAILed the task on a fabricated
> Hole 8 tree count (correct, and fixed), and separately asserted a "third live consumer" of the
> `HoleImages` path that **does not exist** (my error, retracted below with the derivation).

---

## Verdict

**PASS.** My iter-4 blocker is properly closed, my own erroneous claim has been retracted chain-wide,
and I re-derived the entire acceptance list from primary sources this pass — including things no gate
had ever derived. I attacked it three ways and found one genuine latent issue that is **out of this
task's scope and pre-existing** (§6). I am not going to manufacture a blocker out of it.

The migration is correct: **all 18 holes, all 89 real artifacts, bit-exact and GUID-preserved; zero
test regressions on a suite I ran myself; Phase 3 live and course-filtering correctly.**

---

## 1. My iter-4 blocker — CLOSED, verified at source

`evidence/hole8_state.txt` was **superseded, not silently overwritten**: it carries a header naming
the failure mode, the genuine scene-state block preserved unchanged, and the real console line with
full JSON envelope, timestamp and stack trace.

Re-derived by me this pass, from primary source, not from the corrected report:

| Quantity | Method | Result |
|---|---|---|
| Hole_08 tree instances | production `TreeObstacleLoader.LoadInstances` + `TreeObstacleProvider.Create` via `script-execute` | `instances=3926 provider=OK` |
| Hole_07 tree instances | same | `instances=1343 provider=OK` |
| Genuine console line | raw `Temp/mcp-server/ai-editor-logs.txt` | `"[PhysicsLab] Tree obstacles loaded for Hole_08: 3926 trees."` @ `2026-07-24T22:07:56.781144+09:00`, stack `PhysicsLabController.cs:1490 → :1513 → :409` |

All four report quotes now read `3926`. `golfin-reviewer` corrected the stale `1343` in its own file
and self-logged the miss. The false "Hole 1 played to completion" sentence is gone; the modal is now
accurately described as a synthetic `HoleCompleteModalController.Show()` on a live Hole 1 state.
Every surviving `1343` string in the folder is explanatory ("this was the wrong number"), which is
correct. **Both iter-4 blockers RESOLVED.**

## 2. My own iter-4 error — CONFIRMED WRONG, retracted

I claimed the HUD mini-map was "a third live consumer" of the namespaced sprite path. **It is not.**

```
grep -rn "HoleImages" --include="*.cs" Assets/Scripts/   (runtime, Editor excluded)
  HoleCompleteModalController.cs:376   ← CONSUMER #1  (fallback Missing at :379)
  HoleCardController.cs:157            ← CONSUMER #2  (fallback Missing at :160)
```

`holeImageName` has exactly the same two readers. `MapViewController.cs:106` is explicit —
`// v2: direct overlay Camera — NO RenderTexture, NO RawImage, NO targetTexture.` The mini-map
renders the loaded `Hole_NN_Geo` scene live; it proves the **geo scene** loaded, not that the
**sprite path** resolved. iter-6 removed the claim and rewrote both screenshot bullets accurately.

**§1.7 rests on exactly TWO proofs, and I verified both by eye this pass** (I had only opened one at
iter-4 — my gap):
- **Consumer #1** — `hole_complete_modal_hole1.jpg`: two *different* real aerials (Hole 1's long
  curved fairway on SUCCESS, Hole 2's narrower one on NEXT). Not `Missing`, not the same sprite twice.
- **Consumer #2** — `s1_7_holeselection_images_ok.jpg`: Hole Selection, expanded NEXT card renders
  the real Hole 1 aerial. (Collapsed LOCKED cards show no art by design, not a missing sprite.)

**Second error of mine, found this pass:** my iter-4 review said "leaves 55 paths; I enumerated all
55." The true count is **57** — same set, miscounted. No verdict depended on it, but it is the same
eyeball-instead-of-derive shape, and it was mine. Disclosing it rather than quietly fixing it.

---

## 3. The sampling question — retired by derivation instead of reconstruction

`golfin-reviewer` said 04/06/07/12/16 were never covered; the coordinator reconstructed 07/10/18.
Both are reconstructions from summaries — the weak method this task keeps punishing, and the iter-2
sample they both lean on lives in a file that has since been rewritten end-to-end.

So I did not reconstruct the union. **I verified all 18 holes exhaustively** — every artifact,
SHA-256 against the HEAD blob *and* `.meta` GUID against the HEAD meta:

```
18 holes × (4 artifacts + 1 HoleImages PNG) = 90 checks
  pass = 89      fail = 0      absent-both = 1
```

The single non-check is `Hole_17/tree_obstacles.csv`, **absent at HEAD and absent now** — the
documented asymmetry that `Scenarios.cs:3381` explicitly asserts (`Hole_17 has no tree_obstacles.csv`).
Hole_17 correctly holds exactly 3 artifacts.

**Coverage is 18/18, derived. The sampling thread is closed permanently.**

Reconciliation checks nobody had run:

| Check | HEAD | Now | Delta | Verdict |
|---|---|---|---|---|
| files under `Resources/HoleData/` | 163 | 165 | +2 = the two new folder `.meta`s | correct |
| files under `Resources/HoleImages/` | 40 | 41 | +1 = `lomond-country-club.meta` | correct |
| `HoleData/` root entries | — | `_test`, `lomond-country-club` **only** | — | **SPEC §1.1 invariant holds exactly** |
| `HoleImages/` root | — | `Missing.png` + unrelated `MissionsBackground.png` | — | §1.7 step 3 satisfied |

Nothing left behind at a flat path, nothing orphaned, no stray file at either root.

---

## 4. Rule 5 — full independent re-walk

First I **derived** that iters 5–6 were evidence-only rather than accepting it:
`find Assets -newermt "2026-07-24 23:20"` → **empty**. No Assets file touched since my iter-4 pass.
I re-ran the code-side derivations anyway.

### Runtime resolution (my `script-execute`, edit mode)
All namespaced `HoleData/lomond-country-club/Hole_NN/{zones,heightmap,green,tree_obstacles}` non-null;
**every old flat `HoleData/Hole_NN/zones` returns null**; all 18 `HoleImages/lomond-country-club/Hole_NN`
sprites resolve and all 18 old flat paths are null; `HoleImages/Missing` at root OK;
`HoleData/_test/TestGreen/zones` OK, old `HoleData/TestGreen/zones` null.

### §5.6 — 13 `CourseSlugResolver` sites, my classification
8 production + 4 test + 1 comment. Five bake sites use `ResolveOrThrow`
(`HoleGeoImporter:2500`, `PhysicsHeightmapBaker:160`, `BakeZoneJsonTool:153`, `GreenJsonWriter:109`,
`TreeObstacleBaker:162`). `TreeObstacleBaker:107` is the `OnSceneSaving` hook — I read the body:
`Resolve` → `if (slug == null) { LogWarning; return; }`. It **aborts**; it does not fall back.
`grep -rn '?? "lomond'` returns **exactly two** hits in the whole tree, both
`GreenTopologyEditor:217/258` editor read-visualization, explicitly permitted by SPEC §1.2 line 72.
**Zero silent bake-side fallbacks.**

### §1.3 — variable-composed writers traced to final path
`PhysicsHeightmapBaker`: `courseSlug` → `exportRoot = …/UHoleGeo/output/{courseSlug}/export` (:164)
→ `exportPath` (:167) → `outPath` (:168). `BakeZoneJsonTool`: `outDir = Path.Combine(ResourcesRoot,
courseSlug, holeId)` (:154) → `outPath` (:156). Both course-scoped.

### §1.3 mirror — an audit nobody had run: variable-composed *readers*
The SPEC's trap is stated for writers. I audited the read side too. Every `Resources.Load` in the
tree taking a bare variable was traced to its construction: `GreenTopology.cs:150` ← `:149`
course-scoped; `GreenTopologyEditor.cs:219/260` ← `:218/:259` course-scoped; the remaining three
(`TournamentService`, `PhysicsConfigLoader`, `TournamentCsvLoader`) are unrelated subsystems.
**No reader was missed by the literal grep.**

### §3 — Phase 3, derived end-to-end live (nobody had done this)
Everyone verified the CSV and the parser separately. I ran the real parser:
```
Parse("lomond-country-club")      → 18 holes, 72 tee rows, holes without exactly 4 tees: NONE
  Hole1: Back 531 blue / Regular 509 green / Front 480 white / Ladies 441 red
Parse("taiheiyo-club-gotenba")    → 0     Parse("bogus-course") → 0
TryGetTee(Back)   → True  (531, blue)
TryGetTee(Middle) → False    ← SPEC §3.1 "absent means the course does not offer that tee"
ActiveCourseContext.CurrentCourseSlug = lomond-country-club
```
Matches SPEC §3.1's Lomond model exactly. Structural checks: `List<TeeData>` not Dictionary
(`HoleData.cs:54`), `TryGetTee` (:76), all 19 CSV rows carry exactly 20 fields, header's last column
is `courseId` at index 19, `HoleDatabaseLoader:122/125/141` reads `fields[19]` and filters on
`ActiveCourseContext.CurrentCourseSlug`. **I proved indices 0–18 are byte-identical to HEAD** with a
paste/awk row diff neutralising col 4 and truncating the appended column — zero row diffs.

Independent corroboration from the pixels: `s1_7_holeselection_images_ok.jpg` reads `LOMOND 28/72`
with tabs `LADIES / FRONT / REGULAR / BACK` — 72 = 18 × 4, matching the parsed tee set exactly.

### ShellScene wire — derived to the GUID, not accepted as "wired"
`git diff` = **one** inserted line, `holeTeesCsv: {… guid: 91abf3bc4a34f40df88bf8e7947da660 …}`.
That GUID is owned by exactly one asset in the project: `Assets/Data/HoleTees.csv.meta`. The wire
points at the real file. No `m_IsActive`, no `sizeDelta`, no transform change.

### Tests — full suite run by me; the +23 delta derived, not inherited
The `Total=938` figure had been carried from an iter-2 run through six iterations without re-derivation.
That is exactly the shape this task keeps failing on, so I ran the suite:

```
EditMode, live, this pass:  TotalTests 938 | Passed 933 | Failed 2 | Skipped 3
HEARTBEAT.log:20 baseline:  Total     915 | Pass    910 | Fail   2 | Skipped 3
                            ---------------------------------------------------
                            +23 tests, +23 passing, failures unchanged, skips unchanged
Per-class (PassedTests):  ActiveCourseContextTests 5 + CourseSlugResolverTests 11 + TeeDataTests 7 = 23
```
The delta reconciles **exactly**. The 2 failures are the pre-existing `StaminaLiveWiringTests`
(`gacha_history` schema bump, not this task); the 3 skips are `HoleCompleteDriverTests`.
**Zero regressions**, derived against the real baseline rather than NOTES' unverified 248.

### Capture-mechanism audit
`Scenarios.cs` diff = a single +1/-1 log string at :3381. **Zero** new `*Gate` methods
(`git diff | grep -c "^+.*Gate("` = 0), no `LoadSceneAsync("LabScaffold", Single)`, no capture menu
item. Gameplay evidence came through `ShellScene → SetUnlockedOverride → SeedSession →
BeginGameplayLoad(n)`. **No bespoke scenario. PASS.**

### Standing bans
`git diff --stat HEAD -- Assets/Scripts/Physics/` = 3 files, all `Physics/Viewer/` (the sanctioned
viewer/bot exceptions). `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs` → **0 dirty each**.
`M_Splash*` → 0. Font atlas clean.

### Rule 13 — 474 paths
474 total; 57 outside the migration bulk and the task folder — I enumerated all 57 and every one is
in the report's Files table or the HEARTBEAT baseline drift list. The set is **identical** to iter-4
(my earlier "55" was the miscount, not a change). +1 total vs iter-4 = `REDTEAM_REVIEW.md`.
Zero mystery drift. HEARTBEAT carries iter-5 and iter-6 kickoff baseline blocks.

### Phase 2 guard
`grep -c MenuItem HoleGeoImporter.cs` = **40**, intact. `CourseImporterWindow` adds
`GOLFIN/Course Importer` + `Repeat Last` with EditorPrefs persistence. The "don't delete until
verified on ≥2 holes incl. Flat" guard is documented in **two** places
(`## Phase 2 close-out follow-up` and `## Spec deviations` #1) — held, not quietly dropped. **ACCEPT.**

### `CourseSlugResolver` location deviation
Re-adjudicated fresh: the 13-site grep shows **zero runtime call sites**, so no runtime assembly
gains an importer dependency. Placing a pure regex utility in `Golfin.Course.Runtime` is what lets
`Golfin.Course.Tests` reference it without the implicit `Assembly-CSharp-Editor`. Documented as
deviation #3. **ACCEPT — improves on the SPEC.**

---

## 5. The three soft findings — re-decided fresh

**1. Putt-in-cup capture — NOT required.** Re-decided from scratch, same conclusion.
§1.7 is a *sprite-resolution* gate and it has exactly two consumers, **both visually proven** — that
is 2/2, not a sample. `HoleCompleteModalController.cs` is not in the 474-path dirty set; this task
changes only the CSV *value* it reads, so the code path exercised is byte-identical whether the modal
is reached by putting out or by `Show()`. The `map_view_aiming` scar is about a feature whose *entry
point* was never exercised — here the entry point **is** exercised (`BeginGameplayLoad` on Holes 1/7/8
with authentic HUDs, plus the real Hole Selection screen rendering correct art). Requiring a
putt-in-cup capture would gate this task on the completion subsystem, which it does not touch.

**2. Hole 8 visual evidence — NOT required.** I re-traced the chain: `_treeProvider = null` at
`PhysicsLabController.cs:1447` (kills any stale-provider hypothesis), `Create` returns null on
null/empty, the log is gated on `_treeProvider != null`. A collision screenshot would prove *less* —
it cannot separate `TreeObstacleProvider` colliders from scene-baked ones. And I now hold something
stronger than the log line: **I ran the production loader against the namespaced Hole 8 path myself
and got `instances=3926 provider=OK`** — a first-party derivation of the exact claim, independent of
any log or screenshot.

**3. Spot-check what `golfin-reviewer` did not sample** — done, and superseded: I checked all 18.

---

## 6. New finding — `GreenTopologyCache` is course-blind (NOT a blocker; follow-up)

Found while auditing the read side. `Assets/Scripts/Course/Runtime/GreenTopologyCache.cs`:

```csharp
private static readonly Dictionary<int, GreenTopology> _cache = new();   // keyed by hole number ONLY
private static readonly HashSet<int> _missingHoles = new();              // negative cache, same key
```

`GreenTopology.LoadFromResources` *is* course-aware (`:149` uses `ActiveCourseContext`), but the cache
in front of it is not. With a second course, `GetForHole(1)` would return **Lomond's** green topology
for Taiheiyo's Hole 1 — silently. Runtime consumers are `MapViewController.cs:1485, :1769`. Worse,
the class doc claims `HoleSessionDriver.OnHoleUnloaded` calls `Invalidate` — **that call does not
exist**; every `Invalidate` in the codebase is from tests or editor authoring tools, so the
process-lifetime cache genuinely persists across holes and would persist across courses.

**Why this is not a FAIL for this task:**
- **Pre-existing and untouched** — `git status` on the file is empty; not a regression.
- **Correct for every reachable state today** — one course exists; `Assets/Golf/Courses/` holds only
  `lomond-country-club`.
- **Outside the SPEC's enumerated surface** — absent from §1.2's "ALL must be updated" call-site
  table, from §1.3's trap list, and from §1.8/§6. The spec authors audited the surface and did not
  include it; expanding scope at the last gate on a 474-path uncommitted refactor is the wrong move.
- Failing six-times-verified sound work on this would be manufacturing a blocker.

**What Cesar needs to know:** SPEC §7 states Taiheiyo content becomes *"follow ADD_HOLE 18 times
under the new slug — no code."* **That claim is false while this cache is course-blind.** The fix
(key by `(courseSlug, holeNumber)`, or invalidate on `ActiveCourseContext.OnCourseChanged` — the event
already exists) belongs in `Docs/Specs/Queued/taiheiyo_club_gotenba_content` as a prerequisite, and
that spec should be updated before a second course ships.

---

## 7. My three break attempts

**1. Visual — find a frame that doesn't show what it claims.** I opened **all five** screenshots at
full resolution this pass, including the two I had not opened at iter-4. `hole1_ball_at_rest_turn2`
(TURN 2, PAR 5, ball at rest, 429 yds), `hole7_tee_view` (HOLE 7, TURN 1, PAR 4, 407 yds, on tee),
`hole7_trees_turn9` (TURN 9, ball buried in canopy), `hole_complete_modal_hole1` (two distinct real
aerials), `s1_7_holeselection_images_ok` (real Hole 1 card art). Every label matches its pixels, and
the iter-6 rewrites of the descriptions are accurate. **Attack failed.**

**2. Numeric — re-derive every quantity and hunt a second transposition.** This is what caught
iter-4. This pass: exhaustive 18/18 bit-exact + GUID (89/90, 0 fail), file-count reconciliation
(163→165, 40→41), full test suite (938/933/2/3 vs 915/910/2/3), per-class test counts summing to 23,
Phase 3 parsed live (18×4=72), ShellScene GUID resolved to its unique owner, 57 dirty paths counted
rather than eyeballed. Every number reconciles exactly. The one discrepancy I found was **my own**
iter-4 path count. **Attack failed on the work; succeeded on my own prior report.**

**3. Spec-intent — could a second course still silently break?** I audited the read side, which the
SPEC only specifies for writers. Every variable-composed `Resources.Load` traced clean. **This found
`GreenTopologyCache`** — a real latent multi-course defect, correctly ruled pre-existing and
out-of-scope, and surfaced above with the §7 correction. On the enumerated surface, §5.6's intent
holds: a malformed path throws at all five bake sites and warns-and-aborts at the one hook that
cannot throw. **Attack failed on this task's scope; produced a genuine follow-up.**

---

## 8. Prior-defect replay

| Event | Verdict |
|---|---|
| iter-1 — fabricated `git diff` on `Scenarios.cs:3381` | **GONE.** Real +1/-1 hunk; no `*Gate` added. |
| iter-2 — EditMode tests cited as *gameplay* proof | **GONE.** Gameplay smoke is real-flow `BeginGameplayLoad`; I ran the EditMode suite separately as its own evidence. |
| iter-3 — four false claims about `hole8_tee_turn1_clean.jpg` | **GONE.** File deleted; only historical mentions remain. |
| iter-4 — fabricated `1343` tree count (mine to catch) | **GONE.** Superseded with provenance; `3926` re-derived by me two independent ways. |
| iter-5 — "third live consumer" mini-map (**my** error) | **GONE.** Retracted chain-wide; exactly 2 consumers, both proven. |

Five bad claims, all the same shape, all now closed. The chain's correction of my *own* error is the
strongest signal here — the loop caught its reviewer, not just its implementer.

---

## STATUS

`ARCHITECT_REVIEW_PASS` → to Cesar for final approval.

**For Cesar's eye:** canonical `screenshots/hole_complete_modal_hole1.jpg`; the second §1.7 proof is
`screenshots/s1_7_holeselection_images_ok.jpg`. Nothing is committed beyond scaffold `27148bf0d`;
the ~474-path tree is uncommitted behind tag `restore/pre_multi_club_refactor_2026-07-24`.

**One thing to carry forward, not a blocker:** `GreenTopologyCache` is keyed by hole number only, so
SPEC §7's "Taiheiyo is content-only, no code" is not yet true. Fix it in the Taiheiyo spec before a
second course ships (§6 above).
