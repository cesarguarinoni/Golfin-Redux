# Architect Review — `multi_club_architecture_refactor`

Iter-6 review. Written 2026-07-24 23:57 JST by `golfin-reviewer`. **This file supersedes the iter-4 verdict of 2026-07-24 23:04 JST in full** — the iter-4 review asserted `1343 trees` for Hole 8 at two locations (its Finding 2 and its Rule 6 fabrication watch); that number is Hole 7's data-row count. Hole 8's real count is `3926`. The red-team caught the transposition at iter-4, iter-5 superseded the evidence file, and I have logged my own miss to `.claude/review_misses.log` under the `[2026-07-25 iter-6-review JST] SOURCE-OF-TRUTH-NOT-VERIFIED (self-log)` line. No stale `1343` reference to Hole 8 remains in this file.

Full acceptance re-walk per PIPELINE_HARDENING Rule 5, `derive don't confirm`; four evidence-integrity events are on record for this task (iter-1 fabricated `git diff`; iter-2 EditMode-tests-cited-as-gameplay-proof; iter-3 four self-caught screenshot mislabelings; iter-4 tree-count transposition red-team-caught) plus a fifth (iter-5 "third live consumer" mini-map claim relayed by the coordinator into the report, self-reviewer-caught iter-6). My iter-4 PASS is NOT evidence and every quantitative claim below was re-derived from primary source this pass.

## Independent visual scan (Step 0 — before reading any prior verdict)

Every image in `screenshots/` re-opened this pass because the coordinator specifically noted iter-6 rewrote the descriptions of `hole1_ball_at_rest_turn2.jpg` and `hole7_tee_view.jpg`, and one image on this task has already been mislabeled.

- **`hole_complete_modal_hole1.jpg` (canonical) — modal composited over the pre-auth title screen.** Background: GOLFIN "presents" logo, ghost "PLAY" button, `CREATE ACCOUNT / LOGIN` at bottom — this is the login page, not a gameplay ScreenId. **Top card `✓ SUCCESS`:** `Lomond Country Club - Hole 1 - Par 5`, a real Hole 1 aerial thumbnail (green fairway with contour lines and dark-green tree border, unambiguously not the flat-grey `Missing` placeholder), stats `TEE OFF: REGULAR / STROKES: 5 (PAR) / BEST: — / TIME: 00:00:00 / BEST: —`, rewards `x100/x10/x5`, silver `REPLAY`. **Bottom card `NEXT`:** `Lomond Country Club - Hole 2 - Par 4`, a real Hole 2 aerial thumbnail (different geometry from Hole 1 — matches the "nearly straight, fairway tight on both sides" description Hole 2 carries in the card), rewards, gold `PLAY`. Two independent sprite loads render real art; neither is a placeholder — §1.7's silent-failure surface is closed. `TIME: 00:00:00` and modal-over-LOGIN remain the same soft signal I flagged iter-4: real completion would accumulate elapsed time; the modal was almost certainly `Show()`-invoked on an active Hole 1 game state.

- **`s1_7_holeselection_images_ok.jpg`** — Hole Selection portrait. `LOMOND 28/72` active tab, `YAITA-KIKYOU` locked. Tee-filter row `LADIES 18/18 / FRONT 10/18 / REGULAR 0/18 lock / BACK 0/18 lock`. `NEXT` card for Hole 1 with real hole art thumbnail on the left, description, gold `PLAY`. Three `LOCKED` cards Holes 2/3/4 below with art hidden as normal. Bottom nav 5 icons. Sprite path `HoleImages/lomond-country-club/Hole_01` resolves at `HoleCardController.cs:157` — this is §1.7 proof #1.

- **`hole1_ball_at_rest_turn2.jpg`** — genuine gameplay HUD: character card `JAMES / Lv 10 / TURN 2`, hole card `LOMOND / HOLE 1 - REGULAR / PAR 5`, wind readout `0.0 mph`, distance-to-flag `429 yds`, ball visible at rest on fairway between trees with cart path to right, bottom cards `SPIN / STRAIGHT / GOLFIN∞ / DRIVER 250 yds`, mini-map thumbnail top-right showing hole 1 layout. Real playmode through real entry path, post-tee-drive on Hole 1. The mini-map thumbnail is a live overhead camera render (independently verified below) — NOT a `HoleImages/` sprite consumer.

- **`hole7_tee_view.jpg`** — same HUD framing. Character `TURN 1`, hole card `HOLE 7 - REGULAR / PAR 4`, `407 yds`, ball at tee, mini-map thumbnail top-right showing a different hole layout from Hole 1's mini-map (independently proves the correct `Hole_07_Geo` scene loaded; scene-load proof only, per iter-6's correction).

- **`hole7_trees_turn9.jpg`** — HUD `TURN 9`, ball resting **inside** a dense tree canopy that occludes the distance-to-flag readout. Direct visual evidence trees are physical, colliding, blocking objects in play. Ball has come to rest between branches and foliage — this is not a fly-over, the ball has settled here.

Five files in `screenshots/`, all inspected. None mislabeled this pass. The iter-4 mislabeled `hole8_tee_turn1_clean.jpg` remains deleted; independent check `grep -r hole8_tee_turn1_clean IMPLEMENTER_REPORT.md` returns nothing.

## Verdict

`PASS` → STATUS `READY_FOR_REDTEAM`. The migration's specific silent-failure risks are closed on both the sprite-load path (visual proof, two hole aerials on the Hole Complete modal + one on the Hole Selection card) and the tree-obstacles data path (real console log line `[PhysicsLab] Tree obstacles loaded for Hole_08: 3926 trees.` from `Temp/mcp-server/ai-editor-logs.txt` line 41942, stack trace to `PhysicsLabController.cs:1490`, count re-derived independently by `wc -l Hole_08/tree_obstacles.csv` = 3928 lines = 3926 data rows). Two soft findings and one self-log are passed forward to the red-team.

## Correction to my own iter-4 file (per coordinator's mandate)

**iter-4 line 39 and line 145 asserted `1343 trees` for Hole 8. That number is Hole 7's.** Coordinator's derivation independently confirmed this pass: `wc -l Hole_07/tree_obstacles.csv` = 1345 lines = 1343 data rows (after 2-line comment+header); `wc -l Hole_08/tree_obstacles.csv` = 3928 lines = 3926 data rows. Both counts derived directly from primary source, not confirmed against an assertion.

**Root-cause of my iter-4 miss:** I verified the STRING `1343 trees` was present in `evidence/hole8_state.txt` and I correctly traced the null-propagation contract through `TreeObstacleLoader.LoadInstances` and `TreeObstacleProvider.Create` — but I never derived the number from the source-of-truth CSV. Same failure shape the self-reviewer logged retrospectively for iter-3 (verify-string-in-artifact instead of verify-truth-of-content). Logged this pass to `.claude/review_misses.log` under `[2026-07-25 iter-6-review JST] SOURCE-OF-TRUTH-NOT-VERIFIED (self-log)`.

**All quantitative Hole-8-tree references in this file quote `3926`, derived from `Hole_08/tree_obstacles.csv`.** The genuine console log line's timestamp (`2026-07-24T22:07:56.781144+09:00`) + stack trace (`PhysicsLabController.cs:1490 → :1513 → :409`) are cited from `Temp/mcp-server/ai-editor-logs.txt` line 41942 (I re-pulled and verified this pass; the JSON message text reads `"[PhysicsLab] Tree obstacles loaded for Hole_08: 3926 trees."`).

## Independent Rule 5 re-walk (derive-don't-confirm; nothing carried forward, including my own iter-4 walk)

### §5.1 bit-exact + GUID preservation on holes NOT previously sampled

Coordinator's ask: pick least-covered holes and verify GUID preservation, not just file presence. Prior review-chain coverage: iter-2 architect 01/05/08/10/14/17/18; iter-4 architect 02/11/15; iter-4 self-review 05/14. **This pass I picked 03, 09, 13** — all three never sampled before.

| Hole | Artifact SHA-256 new-vs-HEAD (all 4 per hole) | heightmap.bytes.meta GUID | Hole_NN.png.meta GUID |
|---|---|---|---|
| Hole_03 | green.json/heightmap.bytes/tree_obstacles.csv/zones.json ALL MATCH | `485473fb94679f149b87ca030f5f6aa3` MATCH | `b51e4bb30a60342c19e7feb3cfdc1a9d` MATCH |
| Hole_09 | same 4 ALL MATCH | `d81727f97a71c43408e36ef53dc6095e` MATCH | `1d7623e911ae34ec49517d0e44e5a04d` MATCH |
| Hole_13 | same 4 ALL MATCH | `3040356a9cf96624b84169ee909469a2` MATCH | `0a05f5855389b4a71a52d567494a0fca` MATCH |

Combined chain coverage across all passes = Holes 01/02/03/05/08/09/10/11/13/14/15/17/18 = **13 of 18 holes** SHA-256 bit-exact + `.meta` GUID preserved against HEAD. `AssetDatabase.MoveAsset` semantics confirmed across three review passes. Zero drift on any migrated file. PASS.

### All 13 CourseSlugResolver call sites — my classification (bake vs read; §5.6 fail-loud gate)

`grep -rn "CourseSlugResolver\." Assets/Scripts/` (filtered to non-test) returns 8 production hits (plus 4 test hits + 1 comment):

| File:line | Call | My classification | My verdict |
|---|---|---|---|
| `HoleGeoImporter.cs:2500` | `ResolveOrThrow(activePath, "HoleGeoImporter.ImportGeoHole")` | Bake site — drives which green.json feeds CDT/cut-contour in mesh import | PASS |
| `PhysicsHeightmapBaker.cs:160` | `ResolveOrThrow(EditorSceneManager.GetActiveScene().path, "PhysicsHeightmapBaker.BakeActiveScene")` | Bake site — heightmap → `Tools/UHoleGeo/output/{slug}/export/hole-NN/heightmap.bytes` (§1.3 variable-composed) | PASS |
| `BakeZoneJsonTool.cs:153` | `ResolveOrThrow(holeScenePath, "BakeZoneJsonTool.BakeOne")` | Bake site — zones.json → `{ResourcesRoot}/{slug}/{holeId}/zones.json` (§1.3 variable-composed) | PASS |
| `GreenJsonWriter.cs:109` | `ResolveOrThrow(activeScenePath, "GreenJsonWriter.SaveToResources")` | Bake site — green.json | PASS |
| `TreeObstacleBaker.cs:162` | `ResolveOrThrow(scene.path, "TreeObstacleBaker.BakeActiveScene")` | Primary bake site — menu-triggered | PASS |
| `TreeObstacleBaker.cs:107` | `Resolve(path)` + `if(slug==null) LogWarning + return` | Auto-save hook (`OnSceneSaving`) — throwing inside a scene-save callback would crash Unity. Aborts cleanly on null. NOT a silent Lomond fallback | ACCEPTABLE |
| `GreenTopologyEditor.cs:217` | `Resolve(...) ?? "lomond-country-club"` | Editor READ site (green-topology visualization) — SPEC §1.2 line 72 explicitly exempts editor read sites from §5.6 | ACCEPTABLE per SPEC |
| `GreenTopologyEditor.cs:258` | Same as :217 (zones/heightmap visualization) | Editor READ site | ACCEPTABLE per SPEC |

Zero silent-fallback bake sites remain. All 5 primary bake sites use `ResolveOrThrow`. The two `?? "lomond-country-club"` fallbacks are both in editor-visualization paths explicitly permitted by SPEC §1.2 line 72. The `OnSceneSaving` hook at :107 is not a silent Lomond fallback — it logs and aborts. §5.6 fail-loud gate holds.

### §1.3 variable-composed writers — both traced

- **`PhysicsHeightmapBaker.cs:155-170`** — `courseSlug = CourseSlugResolver.ResolveOrThrow(...)`, `holeFolder = isFlat ? "hole-NN-flat" : "hole-NN"`, `exportRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", $"Tools/UHoleGeo/output/{courseSlug}/export"))`, `exportPath = Path.Combine(exportRoot, holeFolder)`, `outPath = Path.Combine(exportPath, "heightmap.bytes")`. Course-scoped output path. PASS.
- **`BakeZoneJsonTool.cs:150-160`** — `courseSlug = CourseSlugResolver.ResolveOrThrow(holeScenePath, "BakeZoneJsonTool.BakeOne")`, `outDir = Path.Combine(ResourcesRoot, courseSlug, holeId)` where `ResourcesRoot = "Assets/Resources/HoleData"`, `outPath = Path.Combine(outDir, "zones.json")`. Course-scoped. PASS.

Both variable-composed writers now include the course slug in the resolved path. Trap defused.

### Runtime consumers of `HoleImages/` — coordinator's exact grep syntax

`grep -rn "HoleImages" Assets/Scripts --include="*.cs" | grep -v "/Editor/\|/Tests/"` returns:
```
Assets/Scripts/UI/HoleData.cs:49                                    // Path within Resources/HoleImages/...  (field definition, not consumer)
Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs:178  // comment about Resources/HoleImages/
Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs:376  Resources.Load<Sprite>($"HoleImages/{hole.holeImageName}")     ← CONSUMER #1
Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs:379  Resources.Load<Sprite>("HoleImages/Missing")                    (Missing fallback)
Assets/Scripts/UI/HoleSelection/HoleCardController.cs:157           Resources.Load<Sprite>($"HoleImages/{hole.holeImageName}")     ← CONSUMER #2
Assets/Scripts/UI/HoleSelection/HoleCardController.cs:160           Resources.Load<Sprite>("HoleImages/Missing")                    (Missing fallback)
```

Exactly TWO runtime `Resources.Load<Sprite>` sites. §1.7 rests on TWO proofs, not three. Confirmed by direct grep this pass — the iter-6 correction to remove the "third live consumer" mini-map claim is factually right. Independent inspection of `MapViewController.cs:22-107` confirms the HUD mini-map is a "direct overlay Camera — NO RenderTexture, NO RawImage, NO targetTexture" (verbatim doc-comment at :106) — zero `HoleImages/` consumption, orthogonal to the sprite migration.

### Genuine Hole 8 tree count — full derivation chain

Three independent primary sources agree on `3926`:

1. **Source file:** `wc -l Assets/Resources/HoleData/lomond-country-club/Hole_08/tree_obstacles.csv` = `3928` lines. File format: line 1 = `# bake_hash=<hex>`, line 2 = `worldX,worldZ,baseY,scale,profileName`, lines 3-3928 = data rows. Data-row count = 3928 - 2 = **3926**.
2. **Console log:** `Temp/mcp-server/ai-editor-logs.txt` line 41942, JSON message `"[PhysicsLab] Tree obstacles loaded for Hole_08: 3926 trees."`, timestamp `2026-07-24T22:07:56.781144+09:00`, stack trace `UnityEngine.Debug:Log ... PhysicsLabController:TryLoadBakedProviders (at Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:1490) → OnHoleLoaded (:1513) → ScanForLoadedHoleSceneAtStartup (:409)`. Re-pulled this pass, verbatim confirmed.
3. **Evidence file:** `evidence/hole8_state.txt` (iter-5 supersession) preserves the genuine scene-state block (`LoadedSceneCount:5, Scene[4]:'Hole_08_Geo'` loaded, `IsHoleReady:True, _treeProvider:TreeObstacleProvider`) and the real log line with the `3926` count. Explicit supersession header names the failure mode (Hole 7 transcribed as Hole 8) and prescribes the self-check that would have caught it (`wc -l` on the CSV before quoting the count).

Cross-derivation: Hole 7's data-row count from `wc -l Hole_07/tree_obstacles.csv = 1345 lines = 1343 data rows` — this matches the fabricated Hole 8 assertion, confirming the transposition mechanism.

### §3 Phase 3 — re-derived from primary source

- **`HoleData.cs:54`** — `public List<TeeData> tees = new();` (List, not Dictionary — SPEC §3.1 correction respected).
- **`HoleData.cs:76`** — `public bool TryGetTee(TeeSet teeSet, out TeeData result)`. Present.
- **`HoleTees.csv`** — 73 lines total = 1 header + 72 data rows. `awk` distribution: 18 back / 18 front / 18 ladies / 18 regular (evenly split); all 72 rows courseId = `lomond-country-club`. Schema matches SPEC §3.2. PASS.
- **`HoleDatabase.csv`** — cols 5+20 verified fresh across rows 1-5 and 14-18: col 5 = `lomond-country-club/Hole_NN` for every row, col 20 = `lomond-country-club` for every row. PASS.
- **`HoleDatabaseLoader.cs:16`** — `[SerializeField] private TextAsset holeTeesCsv;` field wired.
- **`HoleDatabaseLoader.cs:121-125`** — `string courseId = fields.Length > 19 ? fields[19].Trim() : string.Empty; if (string.IsNullOrEmpty(courseId)) courseId = "lomond-country-club"; ... if (courseId != ActiveCourseContext.CurrentCourseSlug) continue;`. Index 19 (col 20 1-based) is correct; blank defaults to Lomond; filter present.
- **`HoleDatabaseLoader.cs:139-145`** — `if (holeTeesCsv != null) PopulateTees(_runtimeDatabase.holes, holeTeesCsv.text, ActiveCourseContext.CurrentCourseSlug);` — PopulateTees signature at :145 accepts (holes, csvText, courseSlug).
- **`MatchmakingModalController.cs:256`** — `// TODO(multi-course): hardcoded to Lomond Hole 5; use ActiveCourseContext + hole selection once a second course ships.` at line 256 (not 257 as iter-2 report claimed; corrected in iter-3+).

### Scene-mutation audit

`git diff HEAD -- Assets/Scenes/ShellScene.unity` = one insert line at :47775-47778 wiring `holeTeesCsv: {fileID: 4900000, guid: 91abf3bc4a34f40df88bf8e7947da660, type: 3}`. No `m_IsActive`, no `sizeDelta`, no position/anchor changes. CLEAN.

### Standing bans

`git diff --stat HEAD -- Assets/Scripts/Physics/`:
```
Bot/Scenarios.cs         | 2 +-
PhysicsLabController.cs  | 14 +++++++++++---
TestGreenLabSetup.cs     | 10 +++++-----
```
Three files, all under `Physics/Viewer/` (sanctioned viewer/bot exceptions per SPEC §1.2/§1.5). `Scenarios.cs` diff = the one +1/-1 log-string change at :3381 (`HoleData/Hole_17/` → `HoleData/lomond-country-club/Hole_17/`); no new `*Gate` methods. Zero touch to `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs`, aero CSVs, `BallStateMachine` asmdef, `LoopCameraDirector`. `M_Splash*.mat` absent from status. `git status --porcelain -- "Assets/Fonts/"` returns empty (font atlas restore from iter-2 still holds; iter-6 did no play-mode capture). PASS.

### Rule 13 file accounting across 474 dirty paths

Full `git status --porcelain --untracked-files=all` = 474 paths. Every path is either:
- Migration bulk pattern under `HoleData/` and `HoleImages/` (~430 D+?? pairs).
- In the report's `Files modified or created` table (27 modified + 15 new code/data files, `HoleDatabase.asset`+`csv`, `ShellScene.unity`, `HoleTees.csv`+meta, 5 screenshots, 5 evidence files — ~55 paths).
- Baseline drift documented in HEARTBEAT.log iter-1/iter-4 kickoff blocks (Art PNGs ×4, NuGet DLLs ×4, Packages ×2, Docs/KICKOFF_TOMORROW, .mcp.json.bak, .claude/review_misses.log — 11 paths).
- Self-modified task-folder docs (SPEC/HEARTBEAT/IMPLEMENTER_REPORT/SELF_REVIEW/STATUS/ARCHITECT_REVIEW/evidence/screenshots subfolder — ~10 paths).

Zero mystery drift. `evidence/` folder = exactly 5 files (`h8postshot.txt`, `hole8_load.txt`, `hole8_state.txt` iter-5 supersession, `tree_state2.txt`, `fine_grid.txt`). `screenshots/` folder = exactly 5 files as listed in the visual scan above. PASS.

### EditMode baseline

`HEARTBEAT.log` iter-1 line 20: `EditMode test baseline: Total=915 Pass=910 Fail=2 Skipped=3` at pre-migration HEAD `27148bf0d`. Zero regressions required against **910**, never NOTES' unverified 248. iter-2 test run recorded 938/933/2/3 (+23 new tests: `CourseSlugResolverTests` 11 + `ActiveCourseContextTests` 5 + `TeeDataTests` 7 `[TestCase]`-parameterized = 23). Same 2 pre-existing `StaminaLiveWiring` failures (attributed to `gacha_history` schema bump — different git-log lineage). I cannot re-run tests myself (no test runner tool in reviewer scope); accept the arithmetic + zero fresh test-count fabrication reported this iter.

### Rule 6 fabrication watch — iter-6

Every quantitative citation in the iter-6 report + iter-5 supersession re-run against primary source this pass:

- `3926 trees` for Hole 8 → derived by `wc -l Hole_08/tree_obstacles.csv` = 3928 lines − 2 (comment + header) = 3926. PASS.
- Real console log line `[PhysicsLab] Tree obstacles loaded for Hole_08: 3926 trees.` at `2026-07-24T22:07:56.781144+09:00` → verified verbatim at `Temp/mcp-server/ai-editor-logs.txt:41942`, stack trace to `PhysicsLabController.cs:1490` present. PASS.
- Exactly 2 runtime `HoleImages/` consumers → verified via coordinator's grep syntax. PASS.
- MapViewController is a live overlay Camera with no `HoleImages/` consumption → verified via `grep -nE "Camera|RenderTexture|targetTexture|Resources.Load.*Hole|HoleImages" MapViewController.cs` returning 0 `HoleImages/` matches + explicit "NO RenderTexture, NO RawImage, NO targetTexture" comment at :106. PASS.
- `evidence/hole8_state.txt` supersession header is coherent — supersession block preserves genuine scene-state (LoadedSceneCount:5, Hole_08_Geo loaded, IsHoleReady:True, _treeProvider:TreeObstacleProvider) + adds real log line. PASS.
- Hole 7 tree count 1343 (used only for cross-derivation of the transposition) → derived by `wc -l Hole_07/tree_obstacles.csv` = 1345 − 2 = 1343. PASS.
- Bit-exact SHA-256 on 3 fresh holes (03/09/13) + 6 fresh GUID checks (3 heightmap.meta + 3 png.meta) → all MATCH HEAD, this pass. PASS.

Zero new fabrications this iteration. **My own iter-4 assertion `1343 trees for Hole 8` at ARCHITECT_REVIEW.md lines 39 and 145 is the last false quantitative claim in the pipeline; both are corrected by this iter-6 supersession, which rewrites the file end-to-end and quotes `3926` throughout.**

## Coordinator's seven seeded findings — my independent adjudication (each judged, not inherited)

### Finding 1 — Hole Complete modal `TIME: 00:00:00` split ruling → ACCEPT (same call as iter-4)

- **(a) `HoleImages/lomond-country-club/Hole_NN` resolves at runtime for the Hole Complete modal → PASS unambiguous.** Both aerials render real art; `Missing` fallback at :379 did not fire.
- **(b) SPEC §4 "complete a hole" via real entry → WEAK-but-acceptable.** Evidence against real completion (TIME:00:00:00 + LOGIN background) is real; evidence for acceptance is (i) `HoleCompleteModalController.cs` was NOT modified by this task (git diff confirms), (ii) real-entry for Hole 1 separately proven by `hole1_ball_at_rest_turn2.jpg` with genuine HUD, (iii) the multi-stage-accept memory pattern permits later stages to accept on code + prior real-flow proof when there's no gameplay `ScreenId`. I do NOT let (a) carry (b); surfaced explicitly below.

### Finding 2 — Hole 8 load-proof-not-collision-event ruling → ACCEPT ruling; CORRECTED number

Coordinator asked me to test the reasoning. The null-propagation chain remains as I traced in iter-4:
- `Resources.Load<TextAsset>($"HoleData/{courseSlug}/{holeId}/tree_obstacles")` returns null on any load failure.
- `TreeObstacleLoader.LoadInstances(null)` returns null (`asset == null` guard at :112).
- `TreeObstacleProvider.Create(null_or_empty)` returns null with a log at :75.
- Line 1489-1490 fires the `Tree obstacles loaded` log **only** on `_treeProvider != null`, requiring all three above to succeed.

So the presence of `[PhysicsLab] Tree obstacles loaded for Hole_08: 3926 trees.` in the real console log is direct proof that (i) `HoleData/lomond-country-club/Hole_08/tree_obstacles` resolved via `Resources.Load`, (ii) `LoadInstances` parsed the CSV into 3926 valid tree instances, (iii) `_treeProvider` was assigned non-null.

**What I got wrong iter-4:** I asserted the count as `1343` when the real count is `3926`. The RULING (load-proof is definitive for the SPEC's specific concern) survives the correction; the number was wrong. This pass I re-derived from primary source and quote `3926` throughout.

Supporting: `hole7_trees_turn9.jpg` shows a ball settled inside dense tree canopy → direct visual evidence trees are physical, colliding objects. Different hole, same provider system.

### Finding 3 — `CourseSlugResolver.cs` at `Course/Runtime/` (SPEC §1.4 said `Editor/CourseImporter/`) → ACCEPT

Verified fresh: file at `Assets/Scripts/Course/Runtime/CourseSlugResolver.cs`; absent from `Assets/Scripts/Editor/CourseImporter/`. Zero runtime call sites (grep this pass — every hit is Editor or Test). Justification (asmdef test visibility) holds. Documented in report `## Spec deviations` entry #3.

### Finding 4 — Phase 2 close-out follow-up → ACCEPT

`grep -c MenuItem HoleGeoImporter.cs` = 40 (36 Geo/GeoFlat one-liners + 4 others). All old menu items preserved per SPEC §2. `CourseImporterWindow.cs` compiles + has menu + repeat-last + course dropdown + hole list + Flat toggle + EditorPrefs — but no evidence in the pipeline it was exercised on a real import. SPEC §2 explicitly permits preservation until verification on ≥2 holes. Report `## Phase 2 close-out follow-up` (line 311) documents the follow-up correctly.

### Finding 5 — tree-count supersession → ACCEPT

`evidence/hole8_state.txt` supersession is coherent: header explicitly names the failure mode (Hole 7 transcribed as Hole 8), preserves the genuine scene-state block from the original `/tmp/hole8_state.txt` (unchanged), and adds the real console log line with `3926` count + full JSON envelope + timestamp + stack trace. All four `1343` occurrences in `IMPLEMENTER_REPORT.md` corrected to `3926`. False "played to completion" sentence removed in iter-5 (line 219 of iter-6 report now correctly describes the modal as reached via synthetic `HoleCompleteModalController.Show()` on an active Hole 1 game state).

### Finding 6 — §1.7 rests on exactly TWO proofs → ACCEPT

Coordinator's exact grep independently confirms 2 runtime consumers. `MapViewController.cs:22-107` inspection confirms live overlay Camera with zero `HoleImages/` consumption. Iter-5's "third live consumer" claim was factually wrong and iter-6's correction is factually right. The two remaining §1.7 proofs (Hole Selection cards via `HoleCardController.cs:157`, Hole Complete modal via `HoleCompleteModalController.cs:376`) stand independently on visual evidence I re-scanned this pass.

### Finding 7 — my own stale `1343` in ARCHITECT_REVIEW.md → RESOLVED IN THIS FILE

Iter-4 lines 39 and 145 are gone; this iter-6 file replaces the entire iter-4 review and quotes `3926` throughout with primary-source derivation. Miss logged to `.claude/review_misses.log`. The file cannot reach the red-team asserting a number the red-team disproved; that is no longer true of this file.

## Architectural / cross-cutting checks

| Check | Result | Notes |
|---|---|---|
| Asmdef boundaries | PASS | `Golfin.Course.Runtime` cleanly holds `CourseSlugResolver`/`TeeData`/`HoleTeesCsvParser`; `Golfin.Course.Tests` explicitly references `Golfin.Course.Runtime`. |
| Existing-pattern reuse | PASS | `TeeData` mirrors `HoleReward` (both plain `[Serializable]` in a `List<>`); `ActiveCourseContext` mirrors static-bus family (PlayerContext/HoleContext); `CourseSlugResolver` centralizes the slug regex. |
| No duplicated logic | PASS | Single resolver, no per-call-site regex copy. |
| Spec intent (not just letter) | PASS | Silent-failure surfaces closed on both sprite path (visual proof, TWO consumers proven) and tree-obstacles data path (log-line proof with independently verified null-propagation contract and independently verified count 3926). §4 gameplay smoke ran through real `BeginGameplayLoad`, not `LabScaffold` direct-load. |
| Cross-feature implications | PASS | `HoleDatabaseLoader` `courseId` filter defaults to `lomond-country-club` on blank; existing single-course flow unchanged. `HoleCompleteModalController` untouched by this task. |
| Latent bugs the screenshot doesn't show | ADVISORY | GAP B (b) — modal invoked synthetically. Cannot rule out a latent bug in completion-to-modal handoff, but orthogonal to migration scope. Surfaced to red-team below. |

## Soft findings surfaced to the red-team

1. **Hole Complete modal invoked synthetically, not by natural completion.** TIME:00:00:00 + composited over LOGIN screen. Migration's silent-failure gate closed by (a) sprite path resolves + (b) real-entry separately proven. Multi-stage-accept pattern permits this; red-team may still choose to require a putt-in-cup capture for Cesar-level assurance.

2. **Hole 8 has no chase-cam screenshot** — only tool-output logs in `evidence/`. The genuine `3926 trees` log line is stronger evidence for the migration's specific concern than a screenshot would be (per Finding 2 analysis, null-propagation contract independently verified), but red-team should independently trace `PhysicsLabController.cs:1486-1492` + `TreeObstacleLoader.cs:110-114` + `TreeObstacleProvider.cs:65-79` and decide whether the missing screenshot is acceptable for this task.

3. **This is my second review pass on this task.** My iter-4 asserted a wrong number (Hole 8 = 1343, should be 3926) and was caught by the red-team. Same-shape derive-not-confirm failures have occurred across the whole chain (implementer iter-1/2/3/4, me iter-4, coordinator iter-5). I applied derivation-first discipline this pass — every quantity above was derived from primary source, not confirmed against an assertion — but the red-team should assume a similar transposition may exist that this pass has not surfaced, and spot-check something I did not touch (my sample was holes 03/09/13; consider 04/06/07/12/16 which no pass in the chain has covered).

## Iteration circuit-breaker status

- iter-1/2 shape `data-path:course-namespace-migration` — resolved iter-2.
- iter-3 shape `gameplay-smoke:missing-runtime-verification` — new shape.
- iter-4 shape `report-artifact:mislabeled-file` — new shape.
- iter-5 shape `evidence-integrity:tree-count-transposition` — new shape (red-team-caught).
- iter-6 shape `evidence-integrity:false-attribution` — new shape (self-review-caught).

Every shape distinct; none at iter-3 of a single shape. Circuit-breaker not near escalation.

## Open questions for Cesar (only if ESCALATE)

None. Not escalating.

## Lessons captured

- **Derive from the source-of-truth file, not from an assertion about it.** When a review claim cites a count, wc-l/awk/shasum the source before recording PASS. Verifying a string is *present in* an artifact is not the same as verifying the string is *true*. This chain-wide failure has now consumed 4 iterations across implementer + reviewer + red-team + coordinator; every occurrence had the same simple derivation available.
- **When the same shape of failure crosses agent boundaries, the failure is procedural, not agent-specific.** iter-1 (implementer fabrication), iter-3 (implementer mislabel), iter-4 (implementer transposition + reviewer confirmation), iter-5 (coordinator mini-map assumption relayed) are all the same shape. Fix must be procedural (derive-first checklist), not agent-specific.
- **Superseded evidence files with explicit provenance headers are a healthy pattern.** `evidence/hole8_state.txt` iter-5 supersession names the failure mode, prescribes the self-check that would have caught it, preserves the genuine content, and adds the true log line with full JSON envelope + timestamp + stack trace. Better than deletion + rewrite (which would leave the pipeline unable to see what was fixed).

## Cesar's final approval

Cesar fills this section after eyeballing the screenshots one last time.

- [ ] Approved by Cesar — task moves to `Docs/Specs/Completed/`
- [ ] Rejected by Cesar — reason: <...>
