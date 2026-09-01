# Implementer Report — `score_upload_flow`

## Implementation summary

The first real PLAYLIFE feature in the game is live end to end: a player photographs or picks a
scorecard, Claude Vision reads it on the backend, the player corrects the holes, GPS is asked to
prove the course, and the score is posted for points and Trust. One `ScreenId.ScoreUpload`, one
prefab with six step roots driven by a `ScoreUploadFlowController` state machine, built at
node-exact geometry from the seven Figma frames and reusing the GPS-hub atoms. `Golfin.Gps` gains
`RecognitionService` (90 s timeout, ≤1600 px / q80 upload) and `ScoreService` (the two-owner body
merge + the in-flight latch), with 14 new EditMode tests. The two hub entry points that were inert
since `gps_hub_entry` — the camera centre button and the SCREENSHOT tile — now open it.

The whole flow was driven **against the live PLAYLIFE API from Editor play mode through the real
widgets' `onClick`**, and then built and booted on the iOS Simulator. Those two runs are what found
the four defects listed under § Defects found — including one in `gps_trust_core` that let every
simulator build escape the server's Trust penalty.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Gps/RecognitionService.cs` | **created** — `POST /recognition/analyze`, 90 s per-request timeout, ≤1600 px / q80 JPEG encode |
| `Assets/Scripts/Gps/ScoreService.cs` | **created** — `POST /score/submit`; merges `GpsScoreAttachment.ToJson()` over the request; 400/429 mapping; in-flight latch |
| `Assets/Scripts/Gps/ScoreDtos.cs` | **created** — `RecognitionResult`, `GolfExtraction`, `HoleScore`, `ScoreSubmitRequest`, `ScoreSubmitResult` |
| `Assets/Scripts/Gps/Tests/RecognitionServiceTests.cs` | **created** — 6 tests (body shape, 90 s override, full/partial extraction, quoted int, no-image guard) |
| `Assets/Scripts/Gps/Tests/ScoreServiceTests.cs` | **created** — 7 tests (11-key merge, merge direction, holes omission, 400/429/generic, latch, no-attachment) |
| `Assets/Scripts/Net/Endpoints.cs` | modified — `RecognitionAnalyze` appended to the GPS section |
| `Assets/Scripts/Net/Tests/NetTestDoubles.cs` | modified — `FakeHttpTransport.SentTimeouts`, so a per-request timeout override is observable |
| `Assets/Scripts/UI/Gps/ScoreUploadFlowController.cs` | **created** — the six-step state machine, all bindings, telemetry, the post |
| `Assets/Scripts/UI/Gps/ScoreUploadDraft.cs` | **created** — shared flow state + every derivation (total, bounds, course, estimates, request) |
| `Assets/Scripts/UI/Gps/HoleRowView.cs` | **created** — one hole row (number, steppers, score cell, 9-hole dimming) |
| `Assets/Scripts/UI/Gps/VenuePickerModalController.cs` | **created** — `ModalController` course picker over `VenueService.List`, pooled rows |
| `Assets/Scripts/UI/Gps/Editor/ScoreUploadScreenBuilder.cs` | **created** — re-runnable prefab builder; the geometry lives here as numbers |
| `Assets/Scripts/UI/Gps/Editor/ScoreUploadEditorRun.cs` | **created** — the play-mode acceptance harness (real `onClick`s, live API, 12 captures) |
| `Assets/Scripts/UI/Gps/GpsHubScreenController.cs` | modified — camera nav button + SCREENSHOT tile wired live, pruned from the inert arrays |
| `Assets/Scripts/UI/ScreenManager.cs` | modified — `ScreenId.ScoreUpload`, container field, activation, top-bar-only group |
| `Assets/Scripts/UI/PersistentUIManager.cs` | modified — `NavTitleKeyFor` → `SCORE_UPLOAD_TITLE` |
| `Assets/Scripts/Telemetry/TelemetryConfig.cs` | modified — 3 `TelemetryEventNames` constants for the flow |
| `Assets/Scripts/Gps/GpsTrustSignals.cs` | modified — the boot log, and the simulator-detection fix (defect 4) |
| `Assets/Scripts/Gps/Tests/GpsTrustSignalsTests.cs` | modified — 1 test pinning the new simulator rule with the measured values |
| `Assets/Prefabs/UI/Gps/ScoreUploadScreen.prefab` | **created** — six step roots + shared strip + cloned hub nav bar |
| `Assets/Prefabs/UI/Gps/VenuePickerModal.prefab` | **created** — the course picker |
| `Assets/Art/UI/Gps/S_SU_{HeroGradient,ShareGradient,RingThin,RingThick,GuideFrame}.png` | **created** — the five sprites the palette genuinely lacks (+ `.meta`) |
| `Docs/Scripts/make_score_upload_panels.py` | **created** — generates those five from the node tokens; edit this, not the PNGs |
| `Assets/Localization/LocalizationText.csv` | modified — 82 rows (77 `SU_*`, 2 titles, 5 `GPS_ERR_*`), EN + JA |
| `Assets/Localization/LocalizationTextTable.asset` | modified — re-imported from the CSV (791 rows) |
| `Assets/Resources/Data/content_version.txt` | modified — re-exported after the publish (`texts` v24) |
| `Assets/Scenes/ShellScene.unity` | modified — **254 pure insertions**: the `ScoreUploadScreen` instance, `ScreenManager._scoreUploadScreen`, the hub's two live-button overrides |
| `Packages/manifest.json` + `packages-lock.json` | modified — `com.yasirkula.nativecamera` @ `beb6cec1`, `com.yasirkula.nativegallery` @ `ad953068` |
| `ProjectSettings/NativeCamera.json`, `ProjectSettings/NativeGallery.json` | **created** — the plugins' iOS usage strings |
| `ProjectSettings/ProjectSettings.asset` | modified — `cameraUsageDescription` (see § Defects found #1) |
| `Docs/AI_CONTEXT.md` | modified — session status |

**Not mine, present in the tree** (declared per Rule 13, all in the iter-1 kickoff baseline in
`HEARTBEAT.log` except the last, which appeared mid-session from another session):
`Docs/TellCode.md`, `.claude/launch.json`, the `gps_trust_core` Active→Completed move, and the
`publish_blocked_catalogs` Queued→Active move.

## Screenshot

- **Canonical screenshot:** `screenshots/su_09_step5_confirm.png` — 1170×2532, the Confirm step
  bound from a real AI read and a real hand-picked venue. It is the frame that reveals the most at
  once: the AI's total and par, the venue the picker chose, the Trust estimate and its bar, the
  points estimate, and the shared top bar carrying `SCORE UPLOAD` and the RP pill.
- **All 12 frames:** `screenshots/su_01_…` → `su_12_hub_recent_rounds.png`, one per acceptance step.
- **Scene loaded:** `Assets/Scenes/ShellScene.unity`
- **Play mode:** Yes — Game View at iPhone 14, 1170×2532
- **Run log:** `Docs/Diagnostics/_capture/score_upload/editor_run.log`

## Defects found by the acceptance runs (all fixed)

Three of the four were invisible to code review and to the Editor; each was found by running the
thing for real.

1. **The iOS player build was broken.** `WebCamTexture class is used but Camera Usage Description is
   empty in Player Settings` — Unity refuses the build outright, and the NativeCamera plugin writes
   `NSCameraUsageDescription` in a POST-process hook that runs far too late to satisfy it. Fixed by
   setting `PlayerSettings.iOS.cameraUsageDescription` to the same string as
   `ProjectSettings/NativeCamera.json`. **Only the real build could have found this**; the live
   preview compiles and runs fine in the Editor.
2. **A hand-picked venue never reached the wire.** The picker wrote `VenueOverrideId` to the draft
   only, and `ScoreService` merges the *attachment* over the request — so the screen showed a course
   name and posted no `venue_id` at all (run 1: activity 29 has `venue_id: null`). Fixed by stamping
   the pick onto the attachment, which keeps ONE owner for that key. Run 3 onward:
   `"venue_id":1,"course_name":"東京ゴルフ倶楽部"` in the sent body.
3. **The "no course nearby" dot stayed green.** The Figma row draws the status dot as its own text
   node, so two objects carried one state and only the label got recoloured. Folded the dot into the
   label — one label, one colour.

4. **The `client_platform` probe called the Simulator `ios`.** `gps_trust_core` inferred the
   simulator from `SystemInfo.deviceModel`, documented as reporting the host CPU there; on Apple
   Silicon it reports `iPhone14,7` — the hardware identifier — so a simulator build escaped the
   server's simulator Trust penalty entirely. Fixed to key off the `SIMULATOR_*` environment
   variables plus the GPU name. Full evidence in § iOS Simulator. **Only the real simulator build
   could have found this**, which is exactly why the item was carried over.

Also corrected against the node before sign-off: the summary panel read `TOTAL SCORE` where the
node says `TOTAL` (new key `SU_SUM_TOTAL`), and four glyphs the shipped font stack renders as tofu
(see § Figma fidelity).

## Acceptance checklist

| Item | Verdict | Evidence |
|---|---|---|
| EditMode: the §1 tests pass; suite count before/after; nothing pre-existing broken | **PASS** | `2205 total / 2202 passed / 0 failed / 3 skipped` after; **2191 before** (14 added: 6 `RecognitionServiceTests`, 7 `ScoreServiceTests`, 1 `GpsTrustSignalsTests` for the simulator rule). The 3 skips are the pre-existing `HoleCompleteDriverTests` Stage-C1 skips, unchanged. |
| Editor play mode, signed in, **library path with a real scorecard photo**: step 2 shows the AI total + course + confidence; quote the transport line and the upload size | **PASS** | `[RecognitionService] uploading 89 KB JPEG (119 KB base64) to /recognition/analyze.` then `[ApiClient] SLOW: POST /api/v1/recognition/analyze → 200 in 1828 ms (cold start?)`. Harness: `AI: score=92 course=TOKYO GOLF CLUB holes=18 par=72 date=2026-04-09 confidence=0.98` — every field matches the card. `screenshots/su_03_step2_reading_result.png` shows TOTAL SCORE 92, COURSE TOKYO GOLF CLUB, `✓ HIGH`, and OUT/IN/PUTTS as `—`. 89 KB is well inside the ≤600 KB target. |
| Manual path: step 3 with empty holes, VERIFY disabled until a valid total, 18/9 toggle re-validates (both frames incl. the 9-hole dimmed IN section) | **PASS** | `su_04_step3_edit_18.png` (18 HOLES gold, IN section live) and `su_05_step3_edit_9.png` (9 HOLES gold, IN header + holes 10–18 at α 0.35 with steppers disabled — frame 3b). The gate is measured, not assumed: `VERIFY enabled (18) = True total=92`; switched to 9 holes, `total=92 VERIFY enabled = True` (92 IS inside `SCORE_BOUNDS_9` 25–100); then one hole edited to make `total=3` → `VERIFY enabled = False`. The manual entry point itself (`MANUAL ENTRY` → step 3 with `–` in every cell and VERIFY disabled) is the same `Total`/`TotalInBounds` path — `ScoreUploadDraft.Total` is null with no AI read and no edited hole, and `TotalInBounds` is false on a null total. |
| Step 4 in the Editor: location fails (`Unknown`) → red pill, `GPS_ERR_UNKNOWN` text, RETRY link, CHOOSE MANUALLY opens the picker with the live `/venue/list` | **PASS** | `su_06_step4_gps_failed.png`: `● GPS OFF` red pill, "Could not get your location." (`GPS_ERR_UNKNOWN`) in red, `Retry GPS` visible, CONFIRM COURSE dimmed. Harness: `attachment: position=null reason=Unknown`, `RETRY link visible = True`, `CONFIRM COURSE enabled = False`. Picker: `[ApiClient] GET /api/v1/venue/list → 200 in 285 ms`, `/venue/list rows shown = 60` — `su_07_step4_venue_picker.png` shows the real Japanese course names. |
| Full post with a manually picked venue: `/score/submit → 200`, Posted shows the server's `points_earned` and `trust`; the RP pill moves by the same amount; the hub's MY RECENT ROUNDS shows the new row. Then delete the rows. | **PASS** (one caveat, below) | `[ApiClient] POST /api/v1/score/submit → 200 in 769 ms` → `POSTED: activity=31 points_earned=50 trust=50`. Sent body: `{"score":92,"score_type":"18","course_name":"東京ゴルフ倶楽部","input_method":"screenshot","visibility":"public","screenshot_data":{…,"recognition_id":"…"},"gps_verified":false,"venue_id":1,"gps_is_mock":false,"client_platform":"editor"}`. RP pill **6,938 → 6,988 (+50)** across `su_09` → `su_10`, matching `points_earned` exactly — that is `PointsService.RefreshBalanceAsync()`, and `[ApiClient] GET /api/v1/points/balance → 200 in 83 ms` fires right after the submit. `su_12_hub_recent_rounds.png`: MY RECENT ROUNDS shows the posted rounds (`MY RECENT ROUNDS rows active = 3`). **Caveat, not this task's defect:** the per-row SCORE cell renders `—` because the `activities` table has **no `score` column** — the score lives in `screenshot_data` JSONB and `/score/history` is a raw `select("*")` (`score.py:419-436`). `ActivityDto.Score` is therefore always null. That is a `gps_hub_entry` / backend gap; the row itself (venue, "today", `● Trust 40%`) binds correctly, and the hero's BEST picked the score up from `/user/detail`. SQL cleanup below. |
| 400 path: a 9-hole score of 20 → red strip with the server's detail; 429 by inspection only | **PASS** | `[ApiClient] POST /api/v1/score/submit → 400 in 35 ms` → `400 PROBE: status=400 kind=Client detail=スコアが低すぎます (最小 25)`, mapped `SU_ERR_SCORE_RANGE`, shown as `That score is outside the allowed range. スコアが低すぎます (最小 25)` — rendered through the controller's own `OnPosted`, not a mock: `su_11_step5_post_error_400.png` shows the red strip and `POST re-enabled = True`. 429 by inspection only, as the spec requires (nothing was posted 10× to prod): `ScoreService.ErrorKeyFor` returns `SU_ERR_RATE_LIMIT` on 429, covered by `ScoreServiceTests.Submit_MapsA429ToTheRateLimitKeyAndEverythingElseToGeneric`. |
| Double-tap POST → exactly one request | **PASS** | The harness invokes `_postScoreButton.onClick` **twice in the same frame** (`DOUBLE TAP:` in the log) and the transport log for that run has exactly one `POST /api/v1/score/submit → 200 in 769 ms`. Two independent latches: the controller's `_postInFlight` fires first, and `ScoreService.IsSubmitting` is the floor under it (`[ScoreService] a submit is already in flight — the duplicate was dropped.`, and `ScoreServiceTests.Submit_SecondCallWhileOneIsPendingSendsNoSecondRequest` asserts `transport.CallCount == 1`). |
| Figma fidelity table per row for all seven frames; every deviation listed | **PASS** | § Figma fidelity below — 33/33 measured geometry sites PASS (`reference/nodes/ScoreUploadScreen_geometry.json`), plus a per-element table and the deviation list. |
| Strings: all rows EN+JA; PLAN/APPLY quoted; `--check` clean; `texts` published; zero hardcoded literals | **PASS** | § Strings below. `--check: clean — no file would change and no catalog has drifted.` |
| Telemetry: `score_upload_open`, `score_upload_posted` rows seen in prod, then deleted | **PASS** | § Telemetry below — all three event types landed with correct payloads, then deleted (0 remaining). |
| **iOS Simulator build**: library picker works from the simulator's Photos; the `UnityClientPlatformProbe` boot log prints `… → ios-simulator` | **PARTIAL — probe PASS, picker BLOCKED** | Build green (`** BUILD SUCCEEDED **`), installed and booted on iPhone 14 / iOS 18.6; all three plist strings read back off the BUILT `Info.plist`; boot log prints `… -> ios-simulator` — **after fixing the probe, which this item caught reporting `ios` on a simulator**. The picker itself is behind the LOGIN gate on a fresh install and I will not enter a password; § iOS Simulator states exactly what is and is not proven, and the one-tap step that closes it. |
| Device (Cesar): camera → AI → GPS proof on a real course is the only way to see `gps_verified: true` | **MANUAL, not blocking** | § Manual on-device verification below. |
| All `[SerializeField]` wired; Console clean; deviations flagged | **PASS** | `object refs: 117/117 wired` on the live scene instance (every object reference on `ScoreUploadFlowController`, arrays included). Console clean through six full play-mode runs — the only warnings are the deliberate degrade paths (`[ScoreUpload] /score/submit failed:` on the 400 probe). UI fidelity lint: **0 FAIL** on both prefabs. |

## Figma fidelity

Node re-pulled at step 0 and again before sign-off (Rule 9): `get_metadata` on all seven frames and
`get_design_context` on the shared Step Bar (`14022:32895`) and the Gold main button
(`14023:33023`). The seven node renders are in `reference/`. Geometry is verified **numerically**,
not by eye — `reference/nodes/ScoreUploadScreen_geometry.json`, **33 sites, 33 PASS, 0 FAIL**, each
comparing the built `RectTransform.rect` against the node box (±1 px).

Font conversion: the shell canvas is 1170×2532 at scale 1, so Figma px are Unity px and only TMP
sizes convert by the project's ÷1.2 rule.

| Frame / element | Node | Built | Verdict |
|---|---|---|---|
| **Shared** Step Bar | `14022:32895` — 958×94, `#091b33`@0.7 r28, pad 28/16/18, gap 12 | 958.0×94.0, `S_PillStadium` 9-sliced at ppum 88/28 → r28 exactly, same fill | **PASS** |
| Step Row: left / title / count | `✕ CLOSE` Rubik Medium 28 `#b7c3d3`; title SemiBold 34 `#eedc9a`; `1/5` Medium 28 | fs 23 / 28 / 23 (=28,34,28 ÷1.2), same fonts + colours; **`✕` → `×`** (deviation 1) | **PASS** |
| Segments (5 × 8 px r4, gap 10) | `14022:32900` — 902×8, seg 172.4 | 902.0×8.0, Seg1 172.4×8.0, gold `#eedc9a` ≤ step / white@0.25 after | **PASS** |
| Hub nav bar, camera slot active | `GPS Nav Bar Container` | the hub's `GpsNavBar` **cloned wholesale** from `GpsHubScreen.prefab`; camera keeps its ring, all five non-interactable | **PASS** |
| **1 Capture** Viewfinder Panel | `14022:32906` — 958×1080, `#0a0f16`, r50 | 958.0×1080.0, `S_PillStadium` ppum 88/50 | **PASS** |
| Scorecard Guide (740×460, dashed 4 px gold r24) | `14022:32907` | 740.0×460.0, `S_SU_GuideFrame` baked from those tokens, tinted `#eedc9a` | **PASS** |
| Screenshot icon 120 `#3c4a5c` + guide text | `14022:32908/32913` | `ICO_GpsScreenshot` 120 at `#3C4A5C`; `SU_ALIGN_HINT` fs 25 (=30÷1.2) muted, centred | **PASS** |
| Shutter 170 + camera icon 84 | `14022:32915` | 170.0×170.0, reuses `S_GpsNav_Camera` — the same navy disc + gold ring the nav bar draws | **PASS** |
| Source Row (`#091b33`@0.7 r100, 3 items, Medium 26) | `14022:32922` | 958.0×72.0 capsule (ppum 88/36 → r36 = h/2), icons 30, labels fs 22; **no pencil glyph** (deviation 2) | **PASS\*** |
| **2 AI Reading** Reading stage 300 halo / 220 spinner / 84 icon | `14035:33721-33724` | 300.0×300.0; halo `#eedc9a`@0.10 + `S_SU_RingThin` @0.35; spinner `S_SU_RingThick` gold, `Filled/Radial360` fillAmount **0.86**, 1 rev/s while waiting, green on result, red on failure | **PASS** |
| Result rows TOTAL/OUT/IN/PUTTS/CONFIDENCE | `14023:33000-33015` | RowTOTAL 958.0×108.0; keys fs 25 muted, values fs 35 (TOTAL 45 gold); 1 px `#FFFFFF1F` hairline between rows; pill 125×43 | **PASS** |
| — OUT / IN / PUTTS show `—` | spec-directed | `Unknown` constant; the endpoint returns a total only | **PASS** (documented deviation) |
| — COURSE row added under TOTAL | spec-directed | inserted; panel 1045 → 1139 to make room | **PASS** (documented addition) |
| Buttons CONFIRM SCORE / RETAKE | `14023:33023/33028` — 958×120 r20 | 958.0×120.0 ×2; `Play Button.png` at ppum 18/20 and `ButtonCancel.png` at 25/20 → r20 on both; label fs 55 (=66÷1.2) `#321506` | **PASS** |
| **3 Edit** Summary panel + 4 stats + toggle | `14024:33081-33094`, `14035:101731` | 958.0×182.0; values fs 43 (TOTAL gold), labels fs 20; segmented 315×50 at (321.5,118), active seg gold | **PASS** |
| — TOTAL label | node says `TOTAL` | was `TOTAL SCORE`; new key `SU_SUM_TOTAL` | **PASS** (corrected) |
| Holes panel: OUT header, 9 rows, IN header, 9 rows | `14024:33095`, `14035:101737-101904` | 958.0×1193.0; Hole1 958.0×60.0 at y 53; SectionIN 958.0×51.0 at y 593; rows at the node's 60 px pitch | **PASS** |
| — row meta text (`Par 4 · 380y`) | node draws it | **EMPTY** — PLAYLIFE has no hole-level par/yardage anywhere in the pipeline | **PASS** (documented deviation) |
| — score colour by par | node colours blue/white/gold/red | always white — the rule needs a per-hole par | **PASS** (documented deviation) |
| — 9-hole mode | frame 3b `14035:101905` | IN header + rows 10–18 at α 0.35, non-interactable, summary IN `—` | **PASS** |
| VERIFY WITH GPS, disabled until in bounds | `14024:33184` | 958.0×120.0; `interactable` follows `TotalInBounds` — measured both ways | **PASS** |
| **4 GPS Proof** Locating panel + GPS pill | `14024:33457/33458` | 958.0×560.0; pill 147.0×40.0 at (804,28), green/red by `Position != null` | **PASS** |
| 300 halo + 130 pin ring + label | `14024:33460-33467` | halo 300 green@0.12 + ring@0.35; ring 130 `#0f3d2a` + `ICO_GpsPin` 64; label `SU_LOCATING` → `SU_ACCURACY_FMT` → the `GPS_ERR_*` string | **PASS** |
| Found Row (`#091b33`@0.7 r24, ● + label) | `14024:33468` | 958.0×64.0; dot folded into the label so one colour drives both (defect 3) | **PASS** |
| Venue Card (`#0f3d2a`@0.85 r50, name 40 / address 26 / within 26) | `14024:33471` | 958.0×177.0; fs 33 / 22 / 22; address = `SU_ADDRESS_UNKNOWN` | **PASS** (documented: auto-register returns no address) |
| Course Facts PAR / YARDS / HOLES | `14024:33475` | 958.0×118.0, three 307.33 tiles; PAR and YARDS `—`, HOLES = the step-3 toggle | **PASS** (documented deviation) |
| Buttons + RETRY link | `14024:33486/33491` | both 958.0×120.0; `SU_RETRY_GPS` shown only when the fix failed. **Placed ABOVE the pair, not under** — "under them" is off the 1860 content box once both buttons sit at y 1596/1740 | **PASS\*** (documented deviation) |
| **5 Confirm** Score hero (gradient, 140 score, `(+N)`, 3 stats) | `14024:101738` | 958.0×386.0, `S_SU_HeroGradient` baked `#1d6b46→#0f3d2a` r50; score fs 117 (=140÷1.2); `(+20)` shown only with a par; stats fs 37 | **PASS** |
| Course row (Rounds icon 40 gold, name 32, date 24) | `14024:101751` | 958.0×110.0; `ICO_GpsRounds` gold; fs 27 / 20; `2026.04.09` from the AI's date | **PASS** |
| Trust panel (title, `{0}%`, 894×16 track, 3 checks) | `14024:101761` | 958.0×267.0; pct green fs 28; track `S_PillStadium` r8 with a Filled/Horizontal green fill at `trust/100`; `✓`/`○` per state | **PASS** |
| Points panel (`#3b2f0f`@0.85, star, `+{0} pts`) | `14024:101778` | 958.0×96.0; `ICO_GpsStar` gold; value fs 37 gold | **PASS** |
| POST SCORE + in-flight latch | `14024:101787` | 958.0×120.0; disabled on tap, no second POST | **PASS** |
| **6 Posted** Success block (150 ring, title 52, sub 30) | `14024:102049` | 958.0×320.0; ring `S_PillStadium`@0.15 + `S_SU_RingThin` gold + `ICO_GpsStar` 72; sub = the SERVER's `points_earned` | **PASS** |
| Share card (760×417 gradient, brand + TRUST pill, course, 132 score, `(+N)`, date, round pill) | `14024:102057` | 760.0×417.0, `S_SU_ShareGradient` r40; score fs 110; TRUST pill from the response `trust`; `★ {0}{1} round` from `/user/detail`'s `activities_count + 1` | **PASS** |
| Vote prompt (heart, title, question, 2 small buttons) | `14024:102068` | 958.0×197.0; `ICO_GpsHeart` pink; buttons 439×54 r14. **CREATE VOTE inert** — logs `[ScoreUpload] vote — not wired yet`; SKIP collapses the panel | **PASS** (spec: v1 inert) |
| Share block (label + 4 × 96 circles) | `14024:102086` | 958.0×215.0; row 505×130 at (226.5,65); **inert, each logs its name** | **PASS** (spec: v1 inert) |
| BACK TO HOME | `14024:102110` | 958.0×120.0 → `GoBack(ScreenId.GpsHub)` | **PASS** |
| Background | `Backgrounds` per frame | the hub's `Home Background` on all six steps | **PASS** (spec-directed) |

### Deviations (every one, listed)

1. **`✕` → `×`** in `SU_CLOSE`. U+2715 is in neither Rubik nor the NotoSansJP fallback and renders
   as a tofu box; U+00D7 is Latin-1 and renders in Rubik itself. Proven, not guessed:
   `Docs/Diagnostics/_capture/score_upload/glyph_probe.png` renders every candidate.
2. **No pencil glyph on MANUAL ENTRY.** U+270E is tofu in both fonts (probe row C) and the palette
   has no pencil sprite. The label stands alone rather than shipping a box.
3. **`𝕏` → `X`** and **`⛓` → `∞`** on the (inert) share row, same probe. `◎` and `♪` render and
   were kept.
4. **OUT / IN / PUTTS are `—` after an AI read**, and the hole rows' meta line is empty, and hole
   scores are always white — all three because the data does not exist (`/recognition/analyze`
   returns a total; PLAYLIFE has no per-hole par or yardage). Spec-directed.
5. **PAR and YARDS are `—`** on step 4 — `/venue/auto-register` returns a name, a distance and
   coordinates, no course facts. Spec-directed.
6. **Venue address is `SU_ADDRESS_UNKNOWN`** — same reason. Spec-directed.
7. **A COURSE row was added** to the step-2 result list (the node has no slot for the course name
   the AI returns); the panel grew 1045 → 1139 to fit it. Spec-directed.
8. **`SU_RETRY_GPS` sits above the two main buttons, not under them.** With both buttons at the
   node's y 1596 and 1740, "under them" is past the 1860-tall content box.
9. **Vote and share rows are inert** and log — spec §Out of scope.
10. **Three keys beyond the spec's list** were needed for elements the spec describes but does not
    name: `SU_POINTS_FMT` (`+{0} pts`), `SU_SHARE_BRAND` (`GOLFIN GPS`), `SU_SHARE_TRUST_FMT`
    (`TRUST {0}%`); plus `SU_SUM_TOTAL` from the node correction above.

## UI fidelity lint

Both prefabs re-linted after the final build:

| Prefab | JSON | fail | warn |
|---|---|---|---|
| `ScoreUploadScreen.prefab` | `Docs/Diagnostics/_capture/ScoreUploadScreen_lint.json` | **0** | 28 |
| `VenuePickerModal.prefab` | `Docs/Diagnostics/_capture/VenuePickerModal_lint.json` | **0** | 2 |

The 30 warnings are three explainable classes, none a defect: `flat-fill` on `#FFFFFF00` images
that exist only as button hit targets; `9slice-cap-kink` on wide panels, where the check's
"estimated cap radius" is `min(w,h)/2` (up to 252 px) but the node's real radius is 50 — a 50 px
effective border is exactly right; and `unlocalized-text` on the labels the controller owns
imperatively (a `LocalizedText` on those would revert RETRY/ENTER MANUALLY to the authored key on
the next language change) plus two format strings.

## Clone provenance

No whole-screen clone source exists for these frames, so every element maps to an atom from
`Docs/Architecture/UI_ELEMENT_PALETTE.md` or to the GPS hub:

| Element class | Source | GUID / path |
|---|---|---|
| Hub nav bar (5 slots) | **cloned wholesale** from `GpsHubScreen.prefab` › `GpsNavBar` at build time | `Assets/Prefabs/UI/Gps/GpsHubScreen.prefab` |
| Silver-edged navy panels (Recognition, Summary, Holes, Locating, Trust, Modal) | Pop-up panel atom, 9-sliced with the drawn-body inset rule | `Assets/Art/HomeScreen/Next Hole Panel.png` `3663aafeba2bd1f42a04eabf9d34c220` |
| Every flat rounded fill, pill, circle, track, stepper (≈40 objects) | Stadium pill atom, 9-sliced at `ppum = 88 / radius` — one sprite, every corner radius in the flow | `Assets/Art/Tournaments/S_PillStadium.png` `bb07d102185aa4f1ca51da13de9eeac6` |
| Gold main + small buttons | Gold button atom | `Assets/Art/HomeScreen/Play Button.png` `cff37a7f9ed6d134696ab92626c9a747` |
| Silver main + small buttons | Silver button atom | `Assets/Art/RosterScreen/ButtonCancel.png` `6021c639e9c124b44a06c8ccd977896f` |
| Background | the hub's | `Assets/Art/HomeScreen/Home Background.png` |
| Shutter button | the nav bar's own camera disc | `Assets/Art/UI/Gps/S_GpsNav_Camera.png` |
| Icons (screenshot, camera, pin, star, heart, rounds) | `gps_hub_entry`'s GPS icon set | `Assets/Art/UI/Gps/ICO_Gps*.png` |
| Fonts | project TMP SDF | `Rubik-SemiBold SDF`, `Rubik-VariableFont_wght SDF` |
| **5 net-new sprites** | generated from the node tokens by `Docs/Scripts/make_score_upload_panels.py` | `S_SU_HeroGradient`, `S_SU_ShareGradient`, `S_SU_RingThin`, `S_SU_RingThick`, `S_SU_GuideFrame` |

The five generated sprites are the only net-new art, and each covers something the palette
genuinely lacks: two **vertical gradients** (a gradient cannot survive 9-slicing — the stretched
middle row flattens it into three bands, so they are baked at node size and used `Type.Simple`), two
**annuli** (the project draws rings as an outer disc plus a smaller disc, which only works over a
flat background — these sit over a gradient panel, and the thick one doubles as the spinner under
`Filled/Radial360`), and one **dashed stroke** (a vector style with no raster equivalent to reuse).
Rule 21's `requireSprite` layer reports 0 fails, i.e. no flat fill stands where the node shows art.

## Strings

82 rows added, EN + JA, every one authored against a real source rather than machine-translated: the
five `GPS_ERR_*` are **verbatim** from the shipping Flutter app
(`lib/common/presentation/controller/current_location_notifier.dart:150-158`), and the JA vocabulary
for the rest (`スコアの投稿に失敗しました。通信環境を確認して、もう一度お試しください。`, `コース名`,
`ベストスコア`, `位置情報の許可が必要です。`) comes from the same codebase. Ken's
`score-upload-light.html` is not in this checkout, so the remainder is authored.

```
$ python3 Tools/content/import_content.py --env-file … --catalogs texts
catalog         add  change   same  conflict  csv
  texts          82       0    709         0  Assets/Localization/LocalizationText.csv
PLAN ONLY — 82 draft(s) would be written (82 new, at min_build 2553). Nothing was written.

$ python3 Tools/content/import_content.py --env-file … --catalogs texts --apply
Wrote 82 draft(s) as cesar.guarinoni@gmail.com (82 new, min_build 2553).

  content_publish -> 23          # texts v22 → v23, 791 rows
  … then SU_ROUND_N_FMT gained a {1} ordinal-suffix slot:
  texts           0       1    790         0
  content_publish -> 24          # texts v23 → v24

$ python3 Tools/content/export_content.py --env-file … --check
--check: clean — no file would change and no catalog has drifted.
```

`SU_ROUND_N_FMT` is `★ {0}{1} round` / `★ {0}ラウンド目`: `{0}` is the count and `{1}` the English
ordinal suffix, which Japanese simply ignores — so "23rd" never has to be decided by a language
branch in code. Verified live: `EN -> ★ 23rd round`, `JA -> ★ 23ラウンド目`.

**Zero hardcoded literals.** The only string literals assigned to a `.text` in the three UI scripts
are the status-dot glyph `"● "` and the `"(+N)"` numeric format — neither is translatable copy:

```
$ grep -nE '\.text\s*=\s*"' Assets/Scripts/UI/Gps/{ScoreUploadFlowController,VenuePickerModalController,HoleRowView}.cs
ScoreUploadFlowController.cs:813:  _foundLabel.text = "● " + LocalizationManager.Get(hasVenue ? "SU_COURSE_FOUND" : "SU_COURSE_NONE");
ScoreUploadFlowController.cs:915:  _heroVsPar.text  = "(" + (vsPar.Value >= 0 ? "+" : "") + vsPar.Value + ")";
ScoreUploadFlowController.cs:1011: _shareVsPar.text = "(" + (vsPar.Value >= 0 ? "+" : "") + vsPar.Value + ")";
```

All 46 keys the code reads exist in the CSV, and **no new key is an orphan** — every one of the 82
has a reader (checked by grepping what READS each key, not by trusting the CSV).

## Telemetry

`TelemetryConfig.DefaultSendsEnabled` is OFF in the Editor, so the harness turns
`TelemetryService.SendsEnabled` on for the run and flushes at the end. All three event types landed
in prod `telemetry_events` with the right payloads:

```
score_upload_open    ts 2026-09-01T04:26:28.766Z  {"source": "gps_hub"}
score_upload_abandon ts 2026-09-01T04:26:30.962Z  {"step": 1}
score_upload_open    ts 2026-09-01T04:26:31.376Z  {"source": "gps_hub"}
score_upload_posted  ts 2026-09-01T04:26:56.866Z  {"holes":18,"trust":40,"gps_verified":false,
                                                   "input_method":"screenshot","points_earned":50}
score_upload_abandon ts 2026-09-01T04:27:00.307Z  {"step": 5}
```

The names are now `TelemetryEventNames` constants rather than literals, matching that class's own
rule ("a typo is a compile error at the hook site rather than a silently-unqueryable row"). All five
rows deleted afterwards — see below.

## Prod cleanup (SQL)

Seven test rounds reached prod across six acceptance runs (`activities` 29–35, all
`client_platform = 'editor'`). Everything they wrote has been removed and the profile restored to
the baseline `gps_hub_entry`'s report recorded for this same account this morning
(`IMPLEMENTER_REPORT.md:144` — "POINTS 6,838, BEST —, TRUST 0%, AVATAR Lv.6 … best_score is null").

Executed against prod via the service key; the equivalent SQL, runnable as-is:

```sql
-- score_upload_flow — remove the acceptance-run rows and restore the profile.
-- FK order matters: feed_items and points_transactions both reference activities.id.
begin;

delete from public.feed_items          where related_activity_id in (29,30,31,32,33,34,35);
delete from public.points_transactions where related_activity_id in (29,30,31,32,33,34,35);
delete from public.activities          where id in (29,30,31,32,33,34,35);
delete from public.telemetry_events    where name like 'score_upload%';

-- 7 posts x 50 pts = 350. The on_activity_completed trigger writes best_score/avg_score on INSERT
-- and never reverts them on DELETE, and apply_score_submit raises trust_level by MAX — so all three
-- have to be put back by hand. This account had ZERO golf activities before the test.
update public.profiles
   set total_points     = 6838,   -- 7188 - 350
       activities_count = 0,
       best_score       = null,
       avg_score        = null,
       trust_level      = 0
 where id = 'f2636482-29aa-4233-a834-99526b202fe1';

commit;
```

Verified after: `leftover editor activities: []`, `leftover score_upload telemetry: []`,
`leftover tx: []`, profile
`{'total_points': 6838, 'activities_count': 0, 'best_score': None, 'avg_score': None, 'trust_level': 0, 'avatar_level': 6}`.

## iOS Simulator

Tier-2 of `Docs/Pipeline/IOS_SIMULATOR_LOOP.md`: append re-export → `Builds/iOS-Sim` → headless
`xcodebuild` against the seeded DerivedData. Nothing was wiped.

```
Unity export : Succeeded in 00:01:24.7, errors=0
burst refs   : 0   (stripped per the loop doc's append trap)
xcodebuild   : ** BUILD SUCCEEDED **
install      : xcrun simctl install CB1B2849-… Golfin.app   (iPhone 14, iOS 18.6)
```

**Both native plugins export and compile.** `Builds/iOS-Sim/Libraries/com.yasirkula.nativecamera/…/NativeCamera.mm`
and `…nativegallery/…/NativeGallery.mm` are in the Xcode project, and their IL2CPP output
(`NativeCamera.Runtime.cpp`, `NativeGallery.Runtime.cpp`) is in `Il2CppOutputProject`.

**§4 plist strings — read back off the BUILT `Info.plist`, not off my settings file:**

```
$ plutil -p Builds/iOS-Sim/Info.plist | grep -iE "UsageDescription|PHPhoto"
  "NSCameraUsageDescription"        => "GOLFIN uses the camera to photograph your scorecard."
  "NSLocationWhenInUseUsageDescription" => "GOLFIN uses your location to verify rounds at the golf course you are playing."
  "NSPhotoLibraryUsageDescription"  => "GOLFIN reads scorecard screenshots you choose from your library."
  "PHPhotoLibraryPreventAutomaticLimitedAccessAlert" => true
```

### The `ios-simulator` probe line — and the defect it exposed

The spec asks for a `UnityClientPlatformProbe` boot log. **There was none**, so one was added
(`[RuntimeInitializeOnLoadMethod]` in `GpsTrustSignals.cs`) — a label that costs a player Trust has
to be observable on the surface it runs on, and a TestFlight build has no console to read.

The first line it printed was wrong, and that is the point of the acceptance item:

```
BEFORE  [UnityClientPlatformProbe] deviceModel='iPhone14,7' os='iOS 18.6'
        gpu='Apple iOS simulator GPU' SIMULATOR_MODEL_IDENTIFIER='iPhone14,7'
        SIMULATOR_DEVICE_NAME='iPhone 14' SIMULATOR_UDID='CB1B2849-…'
        platform=IPhonePlayer -> ios              ← WRONG

AFTER   … same inputs …                          -> ios-simulator   ← required by the spec
```

`gps_trust_core` detected the simulator from `SystemInfo.deviceModel`, on the documented assumption
that "on the simulator it reports the HOST CPU (x86_64 / arm64)". **On Apple Silicon that is false**
— the simulator reports `iPhone14,7`, byte-identical to the hardware — so every simulator build was
labelling itself `ios` and walking straight past the server's simulator Trust penalty
(`score.py:183`), which is the exact thing the label exists to catch. It had never been run on a
simulator before; this spec's carried-over item is what caught it.

Fixed in `UnityClientPlatformProbe`: the simulator is now identified by the `SIMULATOR_*`
environment variables CoreSimulator injects into every process it hosts (absent on a device), with
the software GPU's name as an independent second signal, and the old model heuristic demoted to a
last resort that still errs toward "simulator". Pinned by a new EditMode test
(`GpsTrustSignalsTests.IsSimulator_TheAppleSiliconSimulatorLooksLikeHardwareAndIsCaughtByTheEnvironment`)
carrying the measured values above. Raw evidence:
`Docs/Diagnostics/_capture/score_upload/sim_probe.log` and the two full console captures
`sim_console_before_fix.txt` / `sim_console_after_fix.txt`.

### The library picker on the simulator — NOT verified, and why

**BLOCKED at the login gate, and I will not enter a password.** A device/simulator build has no
`DevAutoSignIn` (that is Editor-only tooling) and a fresh install has no session, so the app boots
to LOGIN / CREATE ACCOUNT. Everything past that — including the GPS hub and therefore the Score
Upload screen — is behind `AuthGate`.

What *is* proven about that path without a session:
- the picker's **plumbing** is in the build: `NativeGallery.mm` compiled in, and
  `NSPhotoLibraryUsageDescription` + `PHPhotoLibraryPreventAutomaticLimitedAccessAlert` in the
  shipped `Info.plist`;
- the picker's **callback path** — everything from `OnImagePicked(path)` through the resize, the
  encode, the upload and the AI read — ran end to end in the Editor against the live API, because
  that is the same method the `NativeGallery` callback invokes. The only unproven link is the
  native picker sheet itself.

**One-tap Cesar step to close it:** the simulator is booted with the build installed
(`CB1B2849-80AC-4E35-87DB-7810B690442C`); sign in once, then GPS hub → camera → LIBRARY. The
simulator's stock Photos library already has six images, so no `addmedia` is needed (`simctl
addmedia` hangs on this device — Photos daemon; unrelated to this task).

## Manual on-device verification (Cesar, not blocking DONE)

Three things cannot be proven off a real phone at a real course, and none of them blocks this spec:

1. **`gps_verified: true`.** The server's `_verify_gps` needs coordinates inside the venue's radius;
   the Editor and the Simulator have no location provider, so every run here posted
   `gps_verified: false` with `PositionFailReason.Unknown`. The GPS step's degrade path is what was
   tested, and it is the path most players hit indoors.
2. **The native camera.** `NativeCamera.TakePicture` needs a camera; the Simulator has none. The
   library path exercises everything after the picker callback, which is the same code.
3. **The live `WebCamTexture` viewfinder.** Editor and Simulator both fall through to the static
   guide frame by design — the preview is a nicety and never gates the shutter.

## Deviations from the spec, in one place

- `ScoreSubmitRequest` deliberately carries **no** `venue_id`: the attachment owns that key, and a
  manual pick is stamped onto the attachment (defect 2). One owner, tested.
- The step-2 buttons carry no `LocalizedText` because the controller swaps them between
  CONFIRM/RETAKE and RETRY/ENTER MANUALLY at runtime.
- The venue picker's rows are pooled from one authored template (60 max) rather than instantiated
  per venue — `/venue/list` is the whole country.

---

## Figma fidelity — rework pass (Cesar rejection, 2026-09-01)

Cesar rejected the first build on sight. Both his lists and my own audit converged on **one shape**,
so this pass names the shape and sweeps every site rather than fixing instances (PIPELINE_HARDENING
§22).

### The shape: `A()` corrected translucency in the wrong direction

The project renders in LINEAR colour space; Figma composites in sRGB. The builder's `A(colour,
alpha)` solved `((1-a)·B)^2.2 == (1-a')·B^2.2` — which drops the `a·F` term entirely. That is exact
for a **black** overlay and maximally wrong for a **white** one: it inflated every light chip's alpha
~2× where the correct solve *shrinks* it ~2.7×, a ~5.5× net error. Replaced with the real solve
`a' = (lin(T) − lin(B)) / (lin(F) − lin(B))`, which needs the backdrop — so `A()` now takes it, and
the F≈0 case is a separate, explicitly-scoped `ADark()`. The same bug was in the sprite baker
(`bake_pill`'s `linear_alpha`) and is fixed there too.

Per-site sweep, measured against the node renders (median RGB, built vs reference):

| Site | before | after | verdict |
|---|---|---|---|
| Trust track (grey), step 5 | +78 / +60 / +77 | +1 / −7 / 0 | FIXED |
| Hole-number badge, step 3 | +89 / +76 / +65 | +15 / +7 / +1 | FIXED |
| Stepper −/+, step 3 | filled light puck | ring, exact | FIXED (was a disc; node draws a rim) |
| GPS ON pill, step 4 | +30 / +58 / +32 | +6 / +19 / +22 | FIXED |
| Confidence pill, step 2 | same bake | same bake | FIXED (same sprite) |
| Reading stage tint, step 2 | +13 / +18 / +12 | +12 / −7 / −1 | FIXED |
| Trust pill, step 6 | +1 / +1 / +1 | unchanged | was already correct |
| Points bar, step 5 | −2 / 0 / 0 | unchanged | was already correct |
| `Navy70` panels | −6 / −15 / −22 | unchanged | dark overlay — `ADark` is valid here |
| Modal backdrop | — | → `ADark` | correct by construction |

### Second shape: circular badges built as accent tints instead of navy-disc-in-gold-ring

| Site | node | before | after |
|---|---|---|---|
| GPS marker, step 4 | navy `[17 45 79]` disc, gold ring, WHITE pin | deep-green disc, green pin, no ring | exact |
| Posted star badge, step 6 | navy `[21 54 91]`, ~6px gold ring | translucent gold tint, 1px hairline | exact |
| Share discs ×4, step 6 | gold ring on each | no ring | FIXED |

### Third shape: corner artifacts in the GPS icon alpha

The earlier alpha-recovery pass left a 230-px blob in a 28×28 canvas corner. Audited all eight icons
by connected-component analysis rather than sampling: **3 affected** (Heart, Pin, Rounds — identical
signature), 5 clean (the multi-part components in Camera/Gift/Screenshot/Sparkle are real glyph
content). Stripped the 3.

### Other defects found and fixed

- **Main button 12% too wide on every screen.** Measured 546/714/630 vs 498/646/572 — a constant
  1.122×, not a padding offset, so it is the label. Rubik SemiBold's advances are wider than the face
  the node renders; label set to 59 (from 66), calibrated against the render. Now 498/648/574.
- **Step 2's reading stage had rounded BOTTOM corners.** The node squares them where the result table
  butts up against it. Squared with a plain foot rect under the 9-sliced panel.
- **Buttons kept RETRY / ENTER MANUALLY after a successful retry** — `OnAnalyzed` never restored the
  labels `ShowReadFailure` had swapped. Real bug, not a fidelity issue.
- **The "★ 23rd round" pill could never appear.** The builder hid the PILL; the controller only ever
  toggled its child LABEL. Added `_shareRoundPill` and wired it.
- **`‹ BACK` and the `n/5` counter were regular/muted**, SemiBold white on the node. Same for the
  share-tile names.

### Result — full-frame mean |ΔRGB| vs the node renders, top bar excluded

| Screen | before | after | photo only | UI column |
|---|---|---|---|---|
| 1 CAPTURE | 3.2 | **3.2** | 0.8 | 3.5 |
| 2 AI READING | 14.3 | **8.7** | 1.1 | 11.2 |
| 3 EDIT SCORE | 9.7 | **7.3** | 2.3 | 9.3 |
| 4 GPS PROOF | 10.8 | **6.4** | 1.7 | 8.3 |
| 5 CONFIRM | 14.3 | **13.3** | **20.7** | 13.6 |
| 6 POSTED | 7.2 | **5.9** | 0.7 | 7.9 |

The fidelity pass now seeds the draft so each step renders the SAME state the node mocks (a clean
read, a landed fix, a matched course, a posted score) — the earlier comparison was measuring an empty
Editor state against a populated mock on three of six screens.

### Known-unequal, with cause

1. **Step 5's background photo is not in the project** (margin ΔRGB 20.7 where every other screen is
   0.7–2.3). Searched all 335 project backgrounds: the closest is
   `Assets/Art/HoleSelectScreen/Background.png` at 22.1 vs the spec-mapped `MissionsBackground.png`
   at 30.0. Kept the spec's mapping rather than change art direction unilaterally. This one asset
   drives most of step 5's remaining number — its most opaque element (the hero card) is at 7.8, and
   the translucent elements above it inherit the photo's error.
2. **No per-hole par data**, so step 3's `Par 4 · 380y` meta is empty and every score renders white.
   The node colours each score by `score − par` (gold bogey, red double+, blue birdie); the API
   returns a TOTAL only. Confirmed by Cesar.
3. **`OUT / IN / PUTTS` show `—` after an AI read**, and `PAR / YARDS` show `—` on step 4 — same
   cause: the endpoints return neither.
4. **Step 2 carries an extra COURSE row** the node has no slot for (the recognition returns a course
   name), shifting the rows below it by 94px. This is the largest single contributor to step 2's
   number and is a deliberate SPEC deviation.
5. **Glyph substitutions** for characters the font has no coverage for: `×` for `✕`, `X` for `𝕏`,
   `∞` for the chain, and no pencil on MANUAL ENTRY. Proven by a rendered-glyph probe.
6. **No backdrop blur** — Unity has no backdrop filter; the node's 2px blur is not reproduced.
7. **Instagram's disc is flat magenta**, the node's is a magenta→purple gradient.
