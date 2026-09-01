# SPEC — `score_upload_flow`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Starts at `SPEC_READY`.

## Goal

The first real PLAYLIFE feature in the game: a player photographs (or picks) a scorecard, the backend's AI reads it, the player corrects the holes, GPS proves the course, and the score is posted for points and Trust. Six steps, one screen, built from the approved Figma frames. This is the first consumer of `Golfin.Gps` (`GpsScoreAttachment` on the GPS Proof step) and it wires the two hub entry points that are inert today: the **camera centre button** and the **SCREENSHOT tile**.

Decisions of record (Cesar, 2026-09-01): list-only v1 (no map) · iOS first · the hub's bottom nav carries the GPS tabs · `gps_trust_core` open question 2 → the screens that read location call `RecordFix` (this flow's GPS step does, via `Capture`).

## Reference

- **Figma:** file `5gEAHjl6xAtW8iYY7NMvWd`, page **GPS / PLAYLIFE**, section *Score Upload flow (1→6)*:
  1 Capture `14022:32576` · 2 AI Reading `14023:32666` · 3 Edit Score `14024:32751` · 3b Edit Score (9 holes) `14035:101905` · 4 GPS Proof `14024:33189` · 5 Confirm `14024:101470` · 6 Posted `14024:101792`. Drop all seven renders into `reference/`.
- **Shell already built:** `GpsHubScreen.prefab` + `GpsHubScreenController` (`gps_hub_entry`) — the hub nav bar, the panel atoms (`Next Hole Panel.png` 9-slice with the `+inset` rule in `UI_ELEMENT_PALETTE.md`, `S_PillStadium`, the `GPS Icons` sprites in `Assets/Art/UI/Gps/`), the step-strip style (`#091b33` @ 0.7, r28). **Reuse them; do not re-export.**
- **Module already built:** `Golfin.Gps` — `GpsScoreAttachment.Capture(onDone)` (5 s fetch → RecordFix → SessionNear → `/venue/auto-register`), `ToJson()` (the 11 Trust fields), `LocationFailReason` + `LocationFailReasonKeys`, `VenueService`, `ActivityDto`; `Golfin.Net.ApiClient` / `Endpoints`; `Golfin.Economy.PointsService.Instance.RefreshBalanceAsync()` (`PointsService.cs:124`) for the post-submit RP re-read.
- **Backend contracts (`/Users/cesar/Documents/playlife/backend/routers/`):**
  - `POST /recognition/analyze` body `{image_base64, sport_type?}` (`recognition.py:26-28`). `image_base64` may be a bare base64 string or a `data:image/jpeg;base64,…` URL (`:270-290` sniffs the media type). Response `{data:{id, sport_type, extracted_data, confidence, recognized_at, user_id, raw_response}}` (`:405-414`). For golf, `extracted_data = {score:int, course:str, holes:9|18, date:"YYYY-MM-DD"|null, par:int}` (`RECOGNITION_SYSTEM_PROMPT`, "## golf") — **no per-hole scores and no putts come back**; the AI reads the total.
  - `POST /score/submit` `ScorePostRequest` (`score.py:117-140`): `score, score_type ("18"|"9"), course_name, venue_id?, input_method ("screenshot"|"manual"), gps_verified, latitude?, longitude?, screenshot_data?, holes?:[dict], photo_url?, create_vote=false, vote_question?, vote_pts=500, visibility="public", gps_check_count, gps_start_lat/lon, gps_end_lat/lon, gps_is_mock, client_platform`. Validation: score 50–200 for 18, 25–100 for 9 (`SCORE_BOUNDS_*`, 400 with a JA `detail`); hard rate limit 10 posts / 24 h (429). Points: `PTS_SCREENSHOT 50` / `PTS_MANUAL 20`, `+PTS_GPS 30` when verified (`:18-20`); Trust: screenshot 50 / manual 30, +30 GPS, +20 K4 (`gps_check_count ≥ 3`), −40 mock. Response `{data:{activity, points_earned, trust, gps_verified, gps_distance_m, avatar_level, leveled_up, vote, newly_earned_badges, referral_reward, tournaments_affected}}` (`:337-356`).
- **Native:** the project has no image picker/camera plugin (`Packages/manifest.json` has git-URL UPM deps already, so adding two is in-pattern).

## Figma Fidelity (enumerate EVERY element — Rule 18)

Shared on every step: shared top bar (`ShowTopBarOnly()`, title `SCORE_UPLOAD_TITLE`), the hub nav bar instance from `GpsHubScreen.prefab` with the **camera centre button in its pressed/active ring** and the four others inert, the step strip (`Step Bar`: `‹ BACK` / `✕ CLOSE` left, gold step title centre, `n/5` right, 5 segments 8 px r4 gold ≤ step / white @ 0.25 after). Buttons are `Main Buttons` Gold / Silver instances that hug their label — **≤ 18 characters per label** (a longer label overflows the 978 column and clips the corners; learned on GPS Proof).

| Step / element | Figma node | Property → value |
|---|---|---|
| **1 Capture** step strip | `14022:32576` › Step Bar | left = `✕ CLOSE` (→ hub), title `CAPTURE`, `1/5` |
| Viewfinder panel | Viewfinder Panel | 978×1080, fill `#0a0f16`, r50; live camera preview **fills the panel** (`WebCamTexture` on a RawImage, aspect-fill, cropped — or the plugin's preview if it provides one); scorecard guide 740×460 dashed gold 4 px r24 centred; helper text `SU_ALIGN_HINT` Rubik Medium 30 muted; a `Screenshot` icon 120 @ `#3c4a5c` behind the text when no preview is running |
| Shutter | Shutter Button | 170 ring with Camera icon 84 → capture |
| Source pill | Source Row | strip `#091b33`@0.7 r100, three items: Camera icon + `SU_SRC_CAMERA` · Screenshot icon + `SU_SRC_LIBRARY` · `✎` + `SU_SRC_MANUAL`; Rubik Medium 26 |
| **2 AI Reading** strip | `14023:32666` | `‹ BACK` (→ step 1), `AI READING`, `2/5` |
| Reading stage | Reading State | 978×560, green-tinted `#7ed488`@0.10; 300 gold halo (`#eedc9a` 0.10 fill / 0.35 stroke 3), 220 spinner ring (gold, arc 0.86 inner, **rotates 1 rev/s while waiting**), Screenshot icon 84 gold centre; `SU_READING` Rubik SemiBold 34 green, `SU_READING_SUB` 24 muted. On result the ring stops and fills solid green; on failure red `#f08080` and `SU_READ_FAIL` |
| Result rows | Row TOTAL SCORE / OUT / IN / PUTTS / CONFIDENCE | key Rubik Medium 30 muted, value 42 white (TOTAL 54 gold); rows separated by 1 px white @ 0.12; CONFIDENCE → pill `SU_CONF_HIGH` ≥ 0.8 green / `SU_CONF_MED` ≥ 0.6 gold / `SU_CONF_LOW` red. **OUT / IN / PUTTS show `—`** — the API returns only the total (see contracts); course name row added below TOTAL: `extracted_data.course` |
| Buttons | | `SU_BTN_CONFIRM_SCORE` (Gold) → step 3 · `SU_BTN_RETAKE` (Silver) → step 1 |
| **3 Edit Score** strip | `14024:32751` | `‹ BACK` (→ step 2, or step 1 if manual), `EDIT SCORE`, `3/5` |
| Summary panel | Score Summary Panel | 4 stats: TOTAL (gold 52) · OUT · IN · PUTTS (white 52), labels 24 muted; `18 HOLES / 9 HOLES` segmented toggle (gold active) |
| Holes panel | Holes Panel | OUT header (gold 28 + section total) · 9 rows · IN header · 9 rows. Row: hole number in 44 circle @ white 0.15, **meta text is EMPTY in v1** (the Figma `Par 4 · 380y` needs hole data PLAYLIFE does not have — documented deviation), `−` / `+` steppers 50 circles, score 36 centred: blue < 4, white 4, gold 5, red ≥ 6 **only when a par is known; otherwise white** — v1 always white. Empty hole = `–` in muted. 9-hole mode = IN header + rows at opacity 0.35, non-interactable, summary IN `—` (frame `3b`) |
| Bottom button | | `SU_BTN_VERIFY_GPS` (Gold) → step 4; disabled (`Enabled=No` variant) until total is inside the server bounds |
| **4 GPS Proof** strip | `14024:33189` | `‹ BACK` (→ step 3), `GPS PROOF`, `4/5` |
| Locating panel | Locating Panel | 978×560; `● GPS ON` / `● GPS OFF` pill top-right (green / red); 300 green halo + 130 Pin ring; label `SU_LOCATING` → on success `SU_ACCURACY_FMT` (`Accuracy ±{0} m`) → on failure the `GPS_ERR_*` string for the reason |
| Detected strip | Found Row | `● ` + `SU_COURSE_FOUND` green in a `#091b33`@0.7 r24 strip; hidden until a venue resolves; `SU_COURSE_NONE` (muted) when auto-register returns null |
| Venue card | Venue Card | fill `#0f3d2a`@0.85 r50; name 40 green (`venue.name`), address 26 muted (**`SU_ADDRESS_UNKNOWN` — auto-register returns no address**), `SU_WITHIN_FMT` (`Within {0} m of your position`, from `distance_m`) 26 white |
| Course facts | Course Facts | three tiles PAR / YARDS / HOLES — **PAR and YARDS show `—`** (no course data), HOLES = the toggle value from step 3 |
| Buttons | | `SU_BTN_CONFIRM_COURSE` (Gold, enabled only with a venue) → step 5 · `SU_BTN_CHOOSE_MANUAL` (Silver) → opens the venue picker modal (below) · a third text link `SU_RETRY_GPS` under them when location failed → `Capture` again |
| Venue picker modal | (no Figma; `ModalController` subclass, Pop-up panel style) | title `SU_PICK_COURSE`, search field, list from `VenueService.List(lang)` filtered client-side (name contains), tap → sets `venue_id` + name, closes. Skipping (`SU_NO_COURSE`) posts with `venue_id = null` → `gps_verified` false (server decides) |
| **5 Confirm** strip | `14024:101470` | `‹ BACK` (→ step 4), `CONFIRM`, `5/5` |
| Score hero | Score Hero | green gradient `#1d6b46→#0f3d2a`; big score 140 white; `(+N)` vs par 34 `#bfe8cc` **only when `extracted_data.par` is known, else hidden**; OUT / IN / PUTTS 44 (`—` when unknown) |
| Course row | Course Row | Rounds icon gold 40, venue/course name 32, date 24 muted (`extracted_data.date` or today, `yyyy.MM.dd`) |
| Trust panel | Trust Panel | `SU_TRUST_LEVEL` gold + `{0}%` green 34 (**client estimate**, formula below), 900×16 green track; checklist: `✓ SU_CHK_SCREENSHOT` green (screenshot path) / `○` muted (manual), `✓ SU_CHK_GPS` green when `gps_verified` will be requested else `○`, `○ SU_CHK_FRIEND` muted always (v2) |
| Points panel | Points Panel | fill `#3b2f0f`@0.85; Star icon gold + `SU_POINTS_EARNED`; `+{0} pts` gold 44 — **client estimate**: 50 screenshot / 20 manual, +30 with GPS |
| Button | | `SU_BTN_POST_SCORE` (Gold) → submit; in-flight latch: `Enabled=No` + no second POST |
| **6 Posted** | `14024:101792` | top bar title `SCORE_POSTED_TITLE`; no step strip; hub nav; success block strip r40: Star ring 150 gold, `SU_POSTED` 52 gold, `SU_POSTED_PTS_FMT` (`+{0} activity pts earned`) green 30 — **from the response `points_earned`, never the estimate**; share card 760 wide green gradient: `GOLFIN GPS` 28 gold + `TRUST {0}%` pill (response `trust`), course 28, score 132, `(+N)` 30 when par known, date 24, `★ SU_ROUND_N_FMT` (`{0}th round` — ordinal from `activities_count + 1` via `/user/detail`, or hidden) |
| Vote prompt | Vote Prompt | panel with Heart icon pink, `SU_VOTE_PROMPT`, quoted question, `CREATE VOTE` (GoldSmall) + `SKIP` (SilverSmall). **v1: CREATE VOTE inert** (votes are v3) — logs `[ScoreUpload] vote — not wired yet`; SKIP just collapses the panel |
| Share block | Share Block | strip r40, `SU_SHARE_TO`, four 96 circles Instagram / X / TikTok / Copy link. **v1: inert, logs** (native share sheet is its own task) |
| Button | | `SU_BTN_BACK_HOME` (Gold) → `GoBack(ScreenId.GpsHub)` |
| Background | `Backgrounds` per frame | reuse the hub background for all steps (one image; per-step backgrounds are Figma-only) |

Client Trust estimate (Confirm step only, never sent): `50 (screenshot) | 30 (manual)` `+30` if a venue is set `+20` if `attachment.Session.CheckCount ≥ 3` `−40` if `attachment.Signals.IsMock`; clamp 0–100. The Posted step replaces it with the server's number.

## Architecture context

- **New UPM deps** (`Packages/manifest.json`, git URLs like the existing ones): `com.yasirkula.nativecamera` (`https://github.com/yasirkula/UnityNativeCamera.git`) and `com.yasirkula.nativegallery` (`https://github.com/yasirkula/UnityNativeGallery.git`). MIT. Pin the commit hash you resolve to in the report. They add `NSCameraUsageDescription` / `NSPhotoLibraryUsageDescription` through their own settings assets — set both strings (see §Strings, not localized; Info.plist is EN).
- **Asmdefs:** `Golfin.Gps` gains `RecognitionService`, `ScoreService` + DTOs (module code, refs `Golfin.Net` only). Screen + controller + modal live in `Assembly-CSharp` (`Golfin.Gps.UI`).
- **Existing code touched:** `ScreenManager.cs` (`ScreenId.ScoreUpload`, registration, `ShowTopBarOnly` rule, `NavTitleKeyFor` case), `GpsHubScreenController.cs` (camera nav button + SCREENSHOT tile → `ShowScreen(ScoreUpload)`; the hub nav prefab gets an "active slot" property so the camera ring can show pressed on this screen), `Endpoints.cs` (append `RecognitionAnalyze`, `ScoreSubmit` already exists — `Endpoints.cs:422`).
- **Reused untouched:** `GpsScoreAttachment`, `VenueService`, `GpsSessionTracker`, `LocationFailReasonKeys`, `ApiClient`, `PointsService`, `UserService`, `LocalizedText`, `ModalController`, `TelemetryService`.

## Implementation

### 1. Module — `Assets/Scripts/Gps/`

- `RecognitionService` (singleton, `PointsService` shape): `IEnumerator Analyze(byte[] jpeg, Action<ApiResult<RecognitionResult>>)` → body `{"image_base64": "data:image/jpeg;base64," + Convert.ToBase64String(jpeg), "sport_type": "golf"}` via `ApiClient.Post`. **Timeout 90 s for this call only** (`ApiClient.TimeoutSeconds` is 30; construct the `HttpRequest` through `SendRoutine` with `TimeoutSeconds = 90` — Vision on a cold Fly machine can take 20–40 s). Downscale before upload: longest edge ≤ 1600 px, JPEG quality 80 (`Texture2D.EncodeToJPG(80)` after a `Graphics.Blit` resize) — target ≤ 600 KB; log the size.
- DTOs: `RecognitionResult { id, sport_type, extracted_data (JObject), confidence (double), recognized_at, raw_response }` + a typed view `GolfExtraction { Score int?, Course string, Holes int?, Date string, Par int? }` parsed from `extracted_data` (all nullable — the model omits fields it cannot read).
- `ScoreService`: `IEnumerator Submit(ScoreSubmitRequest req, GpsScoreAttachment gps, Action<ApiResult<ScoreSubmitResult>>)` — serialises `req` to a `JObject`, merges `gps.ToJson()` over it (the GPS fields win), POSTs `Endpoints.ScoreSubmit`. `ScoreSubmitRequest { score, score_type, course_name, input_method, holes (List<HoleScore>{hole:int, score:int?}) , screenshot_data (JObject: the recognition `extracted_data` + `recognition_id`), visibility = "public" }`. `ScoreSubmitResult { activity (ActivityDto), points_earned int, trust int, gps_verified bool, gps_distance_m double?, avatar_level int?, leveled_up bool, newly_earned_badges (JArray) }`. Map 400 → `SU_ERR_SCORE_RANGE` with the server `detail` appended, 429 → `SU_ERR_RATE_LIMIT`, others → `SU_ERR_GENERIC`.
- `Endpoints.RecognitionAnalyze => BaseUrl + "/recognition/analyze"` (append to the GPS section).
- Tests (EditMode, `Golfin.Gps.Tests`): `RecognitionService` builds a `data:image/jpeg;base64,` body and unwraps a scripted golf result into `GolfExtraction` (all-fields and missing-fields cases); `ScoreService` merges the 11 GPS keys over the request (assert the sent body has `score, score_type, input_method, gps_verified, client_platform`, and that a GPS key present in both takes the attachment's value); 400/429 mapping; in-flight latch (second `Submit` while one is pending → no second HTTP call).

### 2. Screen — `Assets/Prefabs/UI/Gps/ScoreUploadScreen.prefab`, `Assets/Scripts/UI/Gps/ScoreUploadFlowController.cs`

One `ScreenId.ScoreUpload`, one prefab with six step roots (`Step1_Capture … Step6_Posted`) toggled by a `ScoreUploadFlowController` state machine; `GoBack()` inside the flow steps back one step, `✕ CLOSE` / `SU_BTN_BACK_HOME` leave to the hub (`ScreenManager.GoBack(ScreenId.GpsHub)`). Discarding a captured photo on CLOSE needs no confirm in v1.

Flow state (`ScoreUploadDraft`, plain class): `Source (Camera|Library|Manual)`, `Photo (byte[] jpeg)`, `Recognition (RecognitionResult)`, `Holes (int? [18])`, `HoleCount (18|9)`, `Putts int?`, `Attachment (GpsScoreAttachment)`, `VenueOverride (id,name)`, `Result (ScoreSubmitResult)`.

- **Step 1** — shutter: `NativeCamera.TakePicture(path => …, maxSize: 1600)`; library: `NativeGallery.GetImageFromGallery(path => …, mediaType: Image)`; both → load bytes → `Draft.Photo` → step 2. Manual → `Draft.Source = Manual`, step 3 with empty holes. Permission denied → the plugin's return value → `SU_ERR_CAMERA_PERM` / `SU_ERR_LIBRARY_PERM` in the helper text, no modal. Live preview: `WebCamTexture` (rear camera, 1280×720 request) started `OnEnable` of step 1, stopped on leaving; on any exception (Editor, simulator) fall back to the static guide — never block the flow on preview.
- **Step 2** — `RecognitionService.Analyze` on entry; spinner rotates; on success fill rows from `GolfExtraction`; `Draft.HoleCount = Holes ?? 18`; the total pre-fills step 3 as an **unallocated total** (see step 3). `confidence < 0.6` → still proceed, pill red. Failure (network / 5xx / parse) → red stage, `SU_READ_FAIL`, buttons become `SU_BTN_RETRY` (Gold, re-POST same photo) + `SU_BTN_ENTER_MANUALLY` (Silver).
- **Step 3** — the holes editor. Because the API gives only a total: on entry from step 2 the TOTAL shows `Draft.Recognition` score and the hole cells are `–`; editing any hole switches TOTAL to `Σ holes` **and shows a muted note `SU_TOTAL_FROM_HOLES`**; while no hole is edited, TOTAL stays the AI's number and that is what gets posted (`holes` omitted from the request). Steppers clamp 1–15. 18/9 toggle re-validates against the bounds; VERIFY WITH GPS enables only when `total` is in bounds (`50–200` / `25–100`).
- **Step 4** — on entry run `GpsScoreAttachment.Capture(a => Draft.Attachment = a)` (this is the flow's `RecordFix` site). Bind: pill from `a.Position != null`; accuracy from `a.Position.AccuracyM`; venue card from `a.VenueName/VenueId/VenueDistanceM`; failure text from `LocationFailReasonKeys.For(a.PositionFailReason)`. `SU_RETRY_GPS` re-runs `Capture` (records another fix — that is the K4 path). Manual picker overrides `VenueId/VenueName` on the draft (the attachment's coords still go up).
- **Step 5** — build the estimate (formula above); POST on `SU_BTN_POST_SCORE` with the latch; on success → step 6 and `PointsService.Instance.RefreshBalanceAsync()` so the RP pill updates; on 400/429/other show the mapped string in a red strip under the button and re-enable.
- **Step 6** — bind from `Draft.Result`; `TelemetryService.Instance.RecordSafe("score_upload_posted", {input_method, gps_verified, trust, points_earned, holes})`. Also record `score_upload_open` on step 1 and `score_upload_abandon` (with `step`) on CLOSE.
- `GpsHubScreenController`: camera nav button + SCREENSHOT tile → `ScreenManager.Instance.ShowScreen(ScreenId.ScoreUpload)`; remove their "not wired" logs. After returning from a post, the hub's MY RECENT ROUNDS re-fetches (`OnEnable` already does).

### 3. Navigation — `ScreenManager.cs`

`ScreenId.ScoreUpload` (comment `// score_upload_flow — Figma 14022:32576…14024:101792`), registration like `GpsHub`, in the `ShowTopBarOnly` group with `GpsHub`, `NavTitleKeyFor` → `SCORE_UPLOAD_TITLE` (the Posted step overrides the title text to `SCORE_POSTED_TITLE` via `HighlightScreen` re-call or a direct `NavTitleKeyFor`-style setter — pick the smaller diff), menu music on, `AuthGate` post-auth, not on the demo allowlist. Android back inside the flow = step back.

### 4. iOS

`NSCameraUsageDescription = "GOLFIN uses the camera to photograph your scorecard."`, `NSPhotoLibraryUsageDescription = "GOLFIN reads scorecard screenshots you choose from your library."` — via the plugins' settings, quoted in the report. Location description already set (`gps_trust_core`).

### 5. Strings — CSV → importer (EN + JA in the same commit)

Prefix `SU_`. Keys: `SCORE_UPLOAD_TITLE, SCORE_POSTED_TITLE, SU_STEP_CAPTURE, SU_STEP_READING, SU_STEP_EDIT, SU_STEP_GPS, SU_STEP_CONFIRM, SU_BACK, SU_CLOSE, SU_ALIGN_HINT, SU_SRC_CAMERA, SU_SRC_LIBRARY, SU_SRC_MANUAL, SU_READING, SU_READING_SUB, SU_READ_FAIL, SU_ROW_TOTAL, SU_ROW_OUT, SU_ROW_IN, SU_ROW_PUTTS, SU_ROW_COURSE, SU_ROW_CONFIDENCE, SU_CONF_HIGH, SU_CONF_MED, SU_CONF_LOW, SU_BTN_CONFIRM_SCORE, SU_BTN_RETAKE, SU_BTN_RETRY, SU_BTN_ENTER_MANUALLY, SU_HOLES_18, SU_HOLES_9, SU_SECTION_OUT, SU_SECTION_IN, SU_TOTAL_FROM_HOLES, SU_BTN_VERIFY_GPS, SU_GPS_ON, SU_GPS_OFF, SU_LOCATING, SU_ACCURACY_FMT, SU_COURSE_FOUND, SU_COURSE_NONE, SU_ADDRESS_UNKNOWN, SU_WITHIN_FMT, SU_FACT_PAR, SU_FACT_YARDS, SU_FACT_HOLES, SU_BTN_CONFIRM_COURSE, SU_BTN_CHOOSE_MANUAL, SU_RETRY_GPS, SU_PICK_COURSE, SU_SEARCH_COURSE, SU_NO_COURSE, SU_TRUST_LEVEL, SU_CHK_SCREENSHOT, SU_CHK_GPS, SU_CHK_FRIEND, SU_POINTS_EARNED, SU_BTN_POST_SCORE, SU_POSTED, SU_POSTED_PTS_FMT, SU_ROUND_N_FMT, SU_VOTE_PROMPT, SU_VOTE_QUESTION_DEFAULT, SU_BTN_CREATE_VOTE, SU_BTN_SKIP, SU_SHARE_TO, SU_SHARE_COPY, SU_BTN_BACK_HOME, SU_ERR_SCORE_RANGE, SU_ERR_RATE_LIMIT, SU_ERR_GENERIC, SU_ERR_CAMERA_PERM, SU_ERR_LIBRARY_PERM`, plus the five `GPS_ERR_*` keys from `gps_trust_core` (`LocationFailReasonKeys`): JA from `current_location_notifier.dart:150-158`, EN authored. **EN copy = the Figma frames' text; JA copy = Ken's mockup where it exists (`score-upload-light.html`), authored otherwise.** ~80 rows. Same importer path and acceptance line as `gps_hub_entry`.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item MUST be `PASS`/`FAIL` with a one-sentence justification citing what was measured.

- [ ] EditMode: the §1 tests pass; suite count before/after; nothing pre-existing broken.
- [ ] Editor play mode, signed in, **library path with a real scorecard photo** (any golf app screenshot): step 2 shows the AI total + course + confidence — quote the `[ApiClient] POST /api/v1/recognition/analyze → 200 in N ms` line and the upload size.
- [ ] Manual path: step 3 with empty holes, VERIFY disabled until a valid total, 18/9 toggle re-validates (screenshots of both frames incl. the 9-hole dimmed IN section).
- [ ] Step 4 in the Editor: location fails (`Unknown`) → red pill, `GPS_ERR_UNKNOWN` text, RETRY link, CHOOSE MANUALLY opens the picker with the live `/venue/list` — screenshot.
- [ ] Full post from the Editor with a manually picked venue: `/score/submit → 200`, Posted step shows the server's `points_earned` and `trust`; the RP pill in the top bar moves by the same amount; the hub's MY RECENT ROUNDS shows the new row. **Then delete the test `activities` row + its `points_transactions` and reconcile `profiles.total_points`** (SQL quoted, as the gacha specs did).
- [ ] 400 path: post a 9-hole score of 20 → red strip with the server's JA/EN detail; 429 path only by inspection (do not post 10 times to prod).
- [ ] Double-tap POST → exactly one request (transport log quoted).
- [ ] Figma fidelity table per row with PASS/FAIL for all seven frames; every documented deviation (par/yards `—`, OUT/IN/PUTTS `—` after AI, inert vote/share) listed.
- [ ] Strings: all rows EN+JA; PLAN/APPLY quoted; `--check` clean; `texts` published; zero hardcoded literals (grep quoted).
- [ ] Telemetry: `score_upload_open`, `score_upload_posted` rows seen in prod `telemetry_events` from one Editor run, then deleted.
- [ ] **iOS Simulator build**: camera unavailable → library picker works from the simulator's Photos; `UnityClientPlatformProbe` boot log prints `… → ios-simulator` (the carried-over item — this spec finally requires the build). Quote both.
- [ ] Device (Cesar, not the implementer): camera capture → AI → GPS proof on a real course is the only way to see `gps_verified: true`; listed as manual on-device verification, not blocking DONE.
- [ ] All `[SerializeField]` wired; Console clean; deviations flagged.

## Files / hierarchy this task touches

- `Packages/manifest.json` (+ lock) — two yasirkula packages
- `Assets/Scripts/Gps/RecognitionService.cs`, `ScoreService.cs`, `ScoreDtos.cs` — NEW; `Tests/RecognitionServiceTests.cs`, `Tests/ScoreServiceTests.cs` — NEW
- `Assets/Scripts/Net/Endpoints.cs` — one URL appended
- `Assets/Prefabs/UI/Gps/ScoreUploadScreen.prefab`, `Assets/Prefabs/UI/Gps/VenuePickerModal.prefab` — NEW
- `Assets/Scripts/UI/Gps/ScoreUploadFlowController.cs`, `ScoreUploadDraft.cs`, `VenuePickerModalController.cs`, `HoleRowView.cs` — NEW
- `Assets/Scripts/UI/Gps/GpsHubScreenController.cs` — two entry points wired; hub nav "active slot"
- `Assets/Scripts/UI/ScreenManager.cs`, `PersistentUIManager.cs` (`NavTitleKeyFor` case) — `ScreenId.ScoreUpload`
- `Assets/Localization/LocalizationText.csv` — ~80 rows (+ importer run)
- iOS plist strings via the plugins' settings assets
- `Docs/AI_CONTEXT.md` — at close-out

## Smoke evidence

Editor: library-photo run end to end (AI → edit → manual venue → post → posted) with screenshots per step; the SQL cleanup; EditMode + telemetry evidence. Simulator: one build for the picker + the probe line. Device: Cesar's on-course run, recorded in the report as manual, not required for DONE.

## Out of scope (do NOT do these)

- Native share sheet / Instagram / X / TikTok / copy link (share row inert, logs) — own task.
- Creating a VOTE from the Posted step (v3) — inert.
- Per-hole par / yardage data, maps, course hole databases.
- Check-in (`/activity/checkin`) — `gps_checkin_screen`.
- Android camera/gallery testing and the Android mock-location plugin (iOS first; the plugins support Android, untested here).
- A discard-confirmation on CLOSE; offline queueing of a post (online-required, like spends).
- Editing `RECOGNITION_SYSTEM_PROMPT` or anything in the backend (per-hole recognition would be a backend task).
