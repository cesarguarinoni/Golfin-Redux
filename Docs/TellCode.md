# TellCode.md — handoff channel (POINTER + KICKOFF FILE)

> **Spec-sized tasks live in `Docs/Specs/Active/<slug>/SPEC.md` — this file only points at them.**
> **Kickoff-sized tasks (no spec folder) live HERE in full**, in the PENDING KICKOFFS section, so they survive the chat session that produced them. (Rule updated 2026-08-04 by Cesar: chat-only kickoffs die with the session; every kickoff the Architect produces is written here at the time it is produced.)

---

## ▶ CURRENT STATE — update this block at every session boundary

- **Last updated:** 2026-08-05 12:45 JST (Architect — **DEVICE ERA.** Game builds+runs on physical iPhone since 2026-07-27; signing SOLVED (do not re-litigate); on-device smoke found 7 issues. Fixed since: `centralball_device_invisible` (device-verified `1a4ad15ca`), `hole6_tree_collision_profiles` (`c1d38e280`), `camera_drag_touch_origin`/K1 (CLOSED — `bb59d32dd` 08-03, device-verified per commit + Cesar's session; block deleted 2026-08-05), `nav_bar_edge_gaps` (K4) (CLOSED — `49825e867` + ticket-cluster follow-up `26ceeb051`, 08-03 — PRE-DATED the batch write, same drift class as K1; cause was H1: fixed-width 1178px center-anchored bars under a **ConstantPixelSize** canvas, fix = stretch anchors + proportional icon re-anchor; NOT the CanvasScaler — the `loading_bar_inset` (K14) hold on that question is resolved; block deleted 2026-08-05, flagged by Cesar). Shipped: `build_version_stamp` (3 defects → hardening kickoff below). **iOS Simulator three-tier verification loop VALIDATED** — canonical doc `Docs/Pipeline/IOS_SIMULATOR_LOOP.md`; standing rules: never wipe the seeded DerivedData, never `BuildPipeline.BuildPlayer` via MCP script-execute. Full story: `Docs/Reports/2026-08-04_ios_simulator_build_blocker.md` §§10–13 + `Docs/AI_CONTEXT.md` top block. **OPEN = the PENDING KICKOFFS below** (6 smoke issues + build-stamp hardening + housekeeping; K9 `ui_frame_pacing` smoke #8 added 2026-08-05; K10 `ob_recovery_fixes` **CLOSED 2026-08-05** (`90dd574ff` camera+drop rule, `ed65f5726` permanent capture Y-flip fix; CupZoom same-class wedge found+fixed; OB now stops chasing with no aerial cut; ground-level settle built then reverted per Cesar); K1 closed. K11 `club_selection_green_gate` **CLOSED 2026-08-05** (`066df31f2` selector gate + `efa681acb` §2f re-decide after reposition — the item deferred pending K10; ⚠️ K10's close-out swept K11's in-flight lines and briefly broke `main`, repaired forward — see the K11 block). K12 `matchmaking_scan_pacing` added 2026-08-05 — find-opponent animation: decelerating scan + total cut ~5.6s→~3.1s, NO scene edit (new-serialized-field technique), queued AFTER K11 per Cesar — **now NEXT UP**. K13 `boot_loading_screen_removal` **CLOSED 2026-08-05** (`d3bf00026`) — measured first as instructed: zero real progress ever fed (`_useExternalProgress` never true, max `_realProgress` 0.000 across 2 runs), boot init done at t=3.8s vs Splash interactive at t=9.0s, real work behind the transition ~0.23s (Main Theme decode, already under the 0.25s fade) → REMOVED per the <2s rule. **click→Home 2.72s → 0.48s.** HoleLoad path verified byte-identical + live-regression-passed (real bar 0→1 via the real ModeHomeCard PlayButton). ⚠️ Adjacent knob still open: `minLoadingTime` (2s, scene-serialized) is also the hole-load screen's MINIMUM — measured 2.586s with progress already at 1.0; same scene-serialization trap as K12. K14 `loading_bar_inset` added 2026-08-05 — hole-load bar ≈8px narrower per side; ONE-VALUE ShellScene YAML edit (LoadingBarRoot sizeDelta.x 0→-16); CanvasScaler question resolved by the `nav_bar_edge_gaps` (K4) closure (ConstantPixelSize, per-bar fix) — sole remaining gate is `safe_area_top_bar` (K7) freeing ShellScene. ⚠️ RECONCILIATION PENDING: repo log shows K6-core `cd0ef6ed4` (arrow F13 + floor clamp) and K9 `7380baf67` already COMMITTED, plus `b702e1a41` wind→ball-flight landed outside the documented queue — K6/K9 blocks need close-out review with Cesar) plus `putter_aim_blue_line` (413, SPEC_READY in `Specs/Active/`, awaiting Cesar go) and a device pass on `demo_build_slice` (426). Everything below this bullet predates the device era and is historical.)

- **Last updated:** 2026-07-02 (Architect — `1v1_result_rewards_display` (347) DONE. NEXT-at-the-time = `stamina_boost_shop` (517) design pass. STALE — superseded by the device-era bullet above.)
- Older narrative bullets (2026-06-11 → 2026-06-24): preserved in git history of this file — all tasks named in them are closed in `Docs/Specs/Completed/`. Trust `Docs/Specs/Active/` + the AI_CONTEXT headline, not old bullets.

---

## 📋 PENDING KICKOFFS — 2026-08-04 batch

Paste any block below into Code as-is. Produced by the Architect during the 2026-08-03/04 sessions; grounded against source at time of writing. Delete a block (and log it in CURRENT STATE) when its task closes.

**Sequencing constraints:**
- ~~`nav_bar_edge_gaps` BEFORE `safe_area_top_bar`~~ — SATISFIED: `nav_bar_edge_gaps` (K4) landed 08-03 (`49825e867`); `safe_area_top_bar` (K7) is in flight on top of it.
- `tree_wind_device` verification is DEVICE-ONLY (sim false-passes it — measured, report §11). `arrow_speed_retune` and `safe_area_top_bar` are editor/sim-verifiable. `ob_recovery_fixes` (K10) is EDITOR-verifiable — state-machine logic; the camera wedge repros in the editor with a mouse.
- `ui_frame_pacing` (K9) should LAND before `arrow_speed_retune` (K6) LOCKS — 60 fps changes perceived arrow smoothness/speed; Cesar should calibrate at shipping frame pacing. K9 feel-verify is DEVICE-ONLY (perf class — sim renders at the Mac's refresh and false-passes smoothness).
- ~~`club_selection_green_gate` (K11) may run IN PARALLEL with K10~~ — **K11 CLOSED 2026-08-05** (`066df31f2` gate + `efa681acb` the deferred §2f-after-reposition item, which K10's merge unblocked). Both shipped; see the K11 block below, including the process scar where K10's close-out swept K11's in-flight lines and briefly broke `main`.
- `matchmaking_scan_pacing` (K12): queued AFTER K11 per Cesar. Single file (MatchmakingModalController.cs), no overlap with K10/K11 — technically parallel-safe if the queue frees up. ⚠️ NO ShellScene edit: the modal's tunables are scene-serialized (K7 is mid-flight in that scene); K12 uses new serialized fields so code defaults take effect without touching the scene. EDITOR-verifiable.
- `loading_bar_inset` (K14): ShellScene YAML edit — the `nav_bar_edge_gaps` (K4) CanvasScaler question is RESOLVED (fix was per-bar anchors; canvas is ConstantPixelSize, no global width change), so the ONLY remaining gate is the in-flight `safe_area_top_bar` (K7) work freeing ShellScene. Isolated one-value commit, sim-valid (layout class). UNITS: ConstantPixelSize ⇒ 8 canvas units = 8 DEVICE pixels — subtle on a 3× panel; if Cesar meant 8 points, the dial is ~24. Start at -16, screenshot, he calls it.
- ~~`boot_loading_screen_removal` (K13): parallel-safe with everything open~~ — **CLOSED 2026-08-05** (`d3bf00026`). The parallel-safety prediction held: the commit used an explicit 2-file pathspec and left K7's ShellScene/SafeAreaFitter/PersistentUIManager and K12's MatchmakingModalController drift untouched (the K10→K11 sweep scar did NOT repeat). SHARED LoadingScreenController never edited, as designed.

### K2 · map_view_bottom_anchor (smoke #5) — TellCode

```
Task: map view should open with its bottom edge anchored to the bottom of the
screen.

FILE: Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs, Open() framing.

Currently the map opens with its initial framing computed such that the bottom
of the hole map does not sit flush to the bottom of the screen. It should.

CROSS-REF — read before touching Open():
POLISH_BACKLOG.md P-010 records an existing open defect in this exact method:
the camera recenters/reframes for 1–2 frames on open before settling. The
recorded fix direction is to compute the bounds-fit framing BEFORE the first
rendered frame, inside Open(), prior to enabling the overlay.

That is the same code path this change touches. Compute the new bottom-anchored
framing in the same place, so the correct framing is live on frame 1. If the
open pop disappears as a side effect, say so explicitly in the report — do not
silently claim P-010; the Architect will verify and close it separately.

ALSO ON FILE, do not regress: P-007 (landing zone / rings project onto trees),
P-008 (zoom-out feels limited), P-009 (distance bands missing). None are in
scope here. If the framing change alters how any of them read on screen,
report it rather than fixing it in this pass.

VERIFY: open the map on a long hole and a short hole. Bottom edge flush both
times, no reframe pop on open. Sim-valid (layout class).
```

### K3 · build_stamp_hardening — Surgical (defect B AMENDED 2026-08-04)

```
Task: build_stamp_hardening — three defects in the shipped build_version_stamp.

FILE (all three): Assets/Editor/BuildStampGenerator.cs

The implementation is correct and working; do NOT restructure it. These are
three bounded fixes. Nothing else in the file needs to change.

────────────────────────────────────────────────────────────────
DEFECT A — the dirty check is blind to untracked files
────────────────────────────────────────────────────────────────
ComputeStampString() derives `dirty` and `diffHash` from `git diff HEAD`, which
reports modifications to TRACKED files only.

When a NEW .cs file is added and not committed — routine during implementation
work — the tree reads clean, no "+diffHash" is emitted, and two builds either
side of that addition differ only by timestamp.

FIX: fold untracked files into the hash input. `git status --porcelain`
covers both modifications and untracked files in one call, or add
`git ls-files --others --exclude-standard` alongside the existing diff.
Hash the combined output.

VERIFY A: build, note the stamp. Add a NEW .cs file WITHOUT committing. Build
again. The stamp MUST now carry a +diffHash that was not there before. Then
edit an EXISTING tracked file without committing and confirm the hash changes
again — do not fix additions by breaking modifications.

────────────────────────────────────────────────────────────────
DEFECT B (AMENDED — broader than first specced) — the restore never
persists to disk, on success OR failure
────────────────────────────────────────────────────────────────
Evidence (report 2026-08-04 §12–§13, third observed instance):
ProjectSettings.asset was left dirty with buildNumber hunks after a
SUCCESSFUL build. Assigning PlayerSettings.* updates the in-memory object
only — it does not reach disk. Additionally, OnPostprocessBuild does not fire
at all when a build FAILS.

FIX REQUIREMENTS:
- Restore the two fields (iOS.buildNumber, Android.bundleVersionCode) AND
  call AssetDatabase.SaveAssets().
- VERIFY by re-reading ProjectSettings.asset from disk after the restore and
  asserting the values match the pre-build snapshot — never trust assignment.
- Run the restore on BOTH outcomes (finally / report.summary.result check).
- Keep the narrow-restore discipline: ONLY those two fields. Never revert the
  whole file (it carries other live settings — data-loss bug).

ACCEPTANCE: git status shows ProjectSettings.asset CLEAN after (a) a
successful build AND (b) a deliberately failed build. Both, not either.

────────────────────────────────────────────────────────────────
DEFECT C — the upload guard blocks ordinary iteration builds
────────────────────────────────────────────────────────────────
After GOLFIN/Build/Mark Current Commit As Uploaded runs at commit N, the
`buildNumber <= lastUploaded` check throws for EVERY build at commit N — all
platforms, all profiles, including Dev-iOS.

The guard's purpose is protecting App Store Connect upload slots, which only
store-bound builds can burn. As written, after a TestFlight upload Cesar
cannot rebuild Dev-iOS without inventing a dummy commit.

FIX: scope the guard to store-bound builds only. Skip it for development /
iteration builds — BuildOptions.Development via report.summary.options is the
cheapest discriminator; prefer a more explicit profile check if available.
Keep the guard's failure message as-is when it DOES fire. Non-store builds
still write and bake the build number normally; only the refuse-to-build
check is skipped.

VERIFY C: run Mark Current Commit As Uploaded. Without committing anything, a
Dev-iOS build must SUCCEED. A store-bound build at the same commit must still
FAIL with the existing message.

DO NOT:
- Change the display string format.
- Touch the gitignore entries for build_stamp.txt.
- Move the guard file (Docs/Versioning/last_uploaded_build.txt is deliberately
  outside any Build/ dir — .gitignore's "[Bb]uild/" rule would untrack it).
- Alter the git-executable fallback list or the stderr drain — both correct.
```

### K5 · tree_wind_device (smoke #6) — TellCode (verification AMENDED per report §11)

```
Bug: trees do not sway on a physical iPhone. Wind animation works in the Unity
editor AND in the iOS Simulator build (measured, report §11 — 54–57% canopy
pixel change with bit-identical controls). This is a DEVICE-TARGET issue.

⚠️ VERIFICATION IS DEVICE-ONLY. The sim build targets iphonesimulator, the
device build targets iphoneos, and different SDKs strip different variant
sets. The sim's trees sway, so any sim check of this fix is a guaranteed
false PASS.

⚠️ FORWARD REQUIREMENT — factor into the fix choice:
A quality-tier setting that DISABLES tree wind on low-end devices is a known
upcoming requirement (`9a — Quality settings presets`, Order 900, already
updated with this dependency). Runtime toggling needs BOTH the _WIND-on and
_WIND-off variants present in the shipped build. That constrains the fix.

STEP 0 — IDENTIFY WHICH SHADER THE HOLE TREES USE. Two packs exist:
  Assets/Packs/BSP Trees Package/Shaders/Vegetation.shader
    → "Custom/Vegetation", Amplify-generated, URP, HAS wind.
  Assets/Packs/Mobile_Tree_Bundle/Shaders/Standard/*NoWind.shader
    → literally NoWind, built-in-RP Standard shaders in a URP project.
Select a swaying tree in a hole scene, report exact material + shader. If any
hole trees are on the NoWind shaders, that is a separate finding — report it.

HYPOTHESES (re-ranked by the §11 measurement — the sim IS a real il2cpp iOS
player build off the same scenes/settings, and its wind survives):

H1 — DEVICE-SDK shader variant stripping. FRONT-RUNNER.
  Vegetation.shader gates wind behind:
    [Toggle(_WIND)] _Wind("Wind", Float) = 1
    #pragma shader_feature _WIND        ← shader_feature, NOT multi_compile
  shader_feature variants ship ONLY if a material has the keyword enabled at
  bake time; the editor compiles on demand, so it always works there.
  _WIND is a GLOBAL shader_feature (neighbours are shader_feature_local).
  CHEAP DISCRIMINATOR FIRST: open the tree material's .mat YAML and read
  m_ShaderKeywords. If _WIND is absent, stripping drops the variant.
  FIX OPTIONS — report the tradeoff, do NOT pick unilaterally:
    a) Serialize _WIND enabled on the shipping material.
       → ON variant only. NO runtime toggle. Fails Order 900.
    b) Always Included Shaders / ShaderVariantCollection. → same limitation.
    c) shader_feature → multi_compile _WIND. Both variants ship; a single
       Shader.DisableKeyword("_WIND") then kills tree wind game-wide — the
       exact hook Order 900 needs. Costs build size; edits a third-party pack
       file. MEASURE the build-size delta of (c) vs (a)/(b) and report it as
       a number. If (a)/(b) is chosen, state that Order 900 still needs the
       shader change later.

H2 — static batching on iPhone: WEAKENED HARD by §11 (the same batching
  settings bake into the sim build, which sways). Check only if H1's
  discriminator comes back clean. Lead if needed: an "iPhone static-batching
  entry" was once observed as uncommitted ProjectSettings churn (AI_CONTEXT
  housekeeping 2026-07-29).

H3 — quality tier / LOD bias: WEAKENED but alive pending ONE unverified fact —
  whether the sim build resolves to the same quality tier as device. One
  Debug.Log of QualitySettings.GetQualityLevel() through the tier-2 sim loop
  settles it cheaply before any H3 time is spent. If H3 IS the cause, it
  likely also explains the two dark-green LOD-impostor spheres seen on Hole 1
  — say so, it merges two open issues AND Order 900's LOD-bias tier setting.

CONSTRAINTS:
- Do NOT edit hole scenes or ShellScene (no merge driver; Order 429 queued).
- Do NOT modify Assets/Packs/ third-party files without reporting first
  (includes fix option c).
- Do NOT re-author tree materials or replace the pack.
- Do NOT implement the quality tier here — Order 900 owns it. This task only
  preserves the ability to build it.

VERIFY: physical iPhone, trees visibly sway on Hole 1. Report which
hypothesis was correct and the evidence. If fix (c) landed, also confirm
Shader.DisableKeyword("_WIND") at runtime stops the sway — that proves the
Order 900 hook exists.
```

### K6 · arrow_speed_retune (smoke #7) — Surgical, Cesar-in-the-loop calibration

```
Task: arrow_speed_retune — timing arrow is too fast at low ClubControl.
Editor and device agree (data-driven, same CSV ships) → tuning, not a bug.
Editor play mode is a VALID verification surface for this task.

⚠️ GOVERNANCE: this retunes the Order 732 / F11 calibration.
- New F-entry in Docs/Physics/PHYSICS_TUNING_CHANGELOG.md (next free
  F-number): old → new values + rationale.
- TWO mirrors change together or they silently diverge:
    Assets/Resources/Gameplay/controls.csv          (runtime truth)
    ControlsConfig.Default in ControlsConfig.cs     (code fallback)
  Update notes/comments in BOTH, following the Order 732 precedent in-file.

THE KNOBS (current values):
  BaseArrowSpeedHzAtCC0    = 3.0     arrowHz at CC 0 (fastest end)
  ArrowSpeedHzPerCC        = -0.05   slope; CC 50 → 0.5 Hz
  PuttArrowSpeedMultiplier = 0.8     putt = swing × 0.8 (COMPOUNDS)

⚠️ CONSTRAINT — base and slope move TOGETHER:
The floor at CC 50 must stay positive and playable. Base 2.0 with slope -0.05
→ CC50 = -0.5 Hz (arrow runs backwards). Pick (base, slope) as a pair.
Round-1 candidate: base 2.0, slope -0.03 → 2.0 Hz (CC 0) → 0.5 Hz (CC 50).
Halves low-CC speed, keeps the CC-50 feel identical.

CALIBRATION LOOP (Cesar is the scorer; nothing locks without his say-so):
1. Confirm the CSV hot-path: edit controls.csv → editor play mode → observe.
   Note re-enter cost if the loader caches across play sessions.
2. Round 1 at the candidate pair. Cesar plays a full swing at LOW CC and, if
   available, a high-CC character near the cap.
3. Iterate per his verdict. Feel is his call — do not argue numbers.
4. On lock: write both mirrors + the F-entry, THEN the hardening below, then
   commit (scoped files only).

HARDENING (same pass, one line — closes a recorded latent hazard):
F11 notes flag that arrowHz has NO floor and goes negative above CC 60,
"safe only because caps enforce CC ≤ 50." Caps are a different file's promise.
Add a floor clamp in ShotController.TickArrow:
    arrowHz = Mathf.Max(arrowHz, <floor>);
floor = the locked CC-50 value (or 0.25 minimum). Update the F11 caveat text
in controls.csv notes + changelog to record the floor exists.

KNOCK-ONS — check, report numbers, do not silently absorb:
- PUTT COMPOUNDING: putt = swing × 0.8. Order 732 already burned once here
  (0.5 multiplier → 4 s putt cycles). After lock, report putt cycle times at
  CC 0 and CC 50; if the low end exceeds ~2.5–3 s per sweep, flag Cesar
  before locking.
- AUTO-CANCEL WINDOW: MaxTotalPasses = 10 is a time window in disguise —
  slower arrows stretch it (10 passes at 0.5 Hz = 20 s already). Report the
  new worst case; changing MaxTotalPasses is Cesar's call.
- TESTS: ShotControllerPuttModeTests exercises arrow-speed relations. Run the
  Gameplay test assembly; update any test hard-coding 3.0 / -0.05 in this
  task, not a follow-up. The F1 relation (putt slower than swing at equal CC)
  must still hold.

VERIFY: editor play mode at locked values — low- and high-CC sweeps feel
right to Cesar; putt still slower than swing. git diff = controls.csv +
ControlsConfig.cs + TickArrow clamp + changelog + (possibly) test file,
nothing else. Device confirm: one build via the normal loop when convenient —
expected identical, same CSV ships. Do not block close-out on it.

NOTE: if 2.0 Hz still feels fast at low CC, the fix may be curve SHAPE, not
the base — difficulty leaning on CleanPassesPerCC instead of speed. That is a
design conversation with the Architect, not a number tweak. Say so and stop.
```

### K7 · safe_area_top_bar (smoke #2) — TellCode · RUN AFTER K4 · AMENDED 2026-08-04 (scene + PersistentUIManager.cs, Cesar-approved Option A)

```
Task: safe_area_top_bar — tickets counter is eaten by the Dynamic Island on
iPhone 14 Pro Max. Smoke issue #2.

SCOPE (RULING 2026-08-04): scene-only is IMPOSSIBLE — show/hide and chrome
logic in PersistentUIManager.cs couples to the current hierarchy. Approved
plan = Option A: ShellScene.unity + PersistentUIManager.cs, ONE isolated
commit. Two new serialized refs approved: topBarContent, bottomNavContent.

THE COMPONENT ALREADY EXISTS — do not write a new one:
Assets/Scripts/UI/Core/SafeAreaFitter.cs (GolfinRedux.UI.Core).
[ExecuteAlways], polls Screen.safeArea, converts to anchors.

⚠️ THE TRAP — inset the CONTENT, not the bar BACKGROUNDS:
- Bar background art: FULL-BLEED on the existing roots (topBarPanel /
  bottomNavPanel), extending under the Dynamic Island / into the
  home-indicator zone.
- Bar CONTENT: canvas-level "SafeArea" node (stretch anchors, zero offsets,
  SafeAreaFitter attached) containing TopBarContent + BottomNavContent;
  re-parent the content sub-objects into those.

CODE TOUCHPOINTS — four, not two. All in PersistentUIManager.cs:
1. ShowTopBar(bool) / ShowBottomNav(bool): toggle BOTH the root panel AND
   the matching content ref. Content-only inverts the bug (Splash/Loading
   would show floating backgrounds); root-only strands the chrome (the bug
   that forced this amendment). Null-guard the new refs.
2. SetTopBarChromeVisible: retarget the child loop from topBarPanel to
   topBarContent. UsernameText MOVES INTO topBarContent (it is top-bar
   content and must sit inside the safe area — account-screen titles would
   otherwise be under the island). The skip-by-name UsernameText logic
   carries over unchanged, so ShowAccountTitleBar keeps working.
3. ApplyDemoTopBarTrim: currently topBarPanel.transform.Find(
   "RewardPointsBackground") — after the reparent this returns null and
   NO-OPS SILENTLY, regressing demo_build_slice §3.4 (demo would show RP
   chrome). Retarget the Find to topBarContent.
4. EnsureTicketPill: resolves via ticketCountText.transform.parent — it
   survives IF RewardPointsBackground, TicketIcon, ShopPlusButton and the
   count text all move together as SIBLINGS into TopBarContent. Keep that
   cluster intact; verify the pill still spawns (its center-anchor math
   assumes the cluster centers as the bar stretches).

SURVIVES UNTOUCHED (do not modify): HideIfScreenBlocked and every serialized
Button/Text/Image ref — Unity object refs, not paths. The two Find calls
above are the only path-based lookups in the file.

SCENE EDIT RULES: isolated commit (ShellScene.unity + PersistentUIManager.cs
together, nothing else), minimal diff, diff the scene YAML before committing,
revert unrelated drift. No merge driver yet (Order 429 queued).
PersistentUIManager's topBarPanel / bottomNavPanel serialized refs must
survive — re-parent CHILDREN only, never rename/move the panel roots.

SEQUENCING: run AFTER nav_bar_edge_gaps (K4) — same bars, same scene; K4's
outcome determines the bars' final geometry.

SCOPE LIMITS: shell canvas only. In-game HUD (player card / hole info) also
crowds the notch but was NOT the reported issue — CHECK visually and report,
don't fix. Build stamp handles its own inset; leave it alone. Other screens'
notch-kissing content = the deferred full inset pass, its own row.

VERIFY — Simulator VALID (safe-area class; ShellScene ships in build data →
tier-1 data swap covers iteration):
- Sim (iPhone 14): tickets pill fully below the notch; NO blank strip
  between notch and top-bar background; bottom nav icons clear of the
  home-indicator band; backgrounds still reach all screen edges.
- Show/hide matrix — every row, this is where the amendment bites:
  Logo/Splash/Loading → NO bar backgrounds AND no chrome visible.
  Account/login screens → banner + centered title ONLY (chrome stripped,
  title visible and inside the safe area).
  Home → full bars, chrome restored.
  In-hole → shell bars fully hidden.
  Demo define (GOLFIN_DEMO, PointsEnabled=false) → RP chrome hidden
  (touchpoint 3 regression check).
- Editor Game view at 16:9: layout unchanged (safe area is zero there — any
  visible difference is a regression).
- Final confirm on Cesar's iPhone 14 Pro Max (taller Dynamic Island than the
  sim's notch, and it is the reporting device). One launch, Cesar's eyeball.
```

### K8 · housekeeping_batch — Surgical, four bounded items

```
Housekeeping addendum — four bounded items, no investigation:

1. .gitignore: add the recurring iOS-export residue:
   Assets/Resources/PerformanceTestRunInfo.json (+.meta),
   Assets/Resources/PerformanceTestRunSettings.json (+.meta),
   Assets/packages-merged-link/
   Verify a fresh export then leaves git status clean.

2. Orphan hygiene → IOS_SIMULATOR_LOOP.md: il2cpp leaks a hung child on EVERY
   build, successful ones included (34 reaped, report §13 addendum). Add a
   post-session check — pgrep -fl il2cpp; reap by start time (ps -o lstart=),
   NEVER by pid comparison (pids wrap). Also: it is the FIRST check if a
   headless build ever fails again.

3. IOS_SIMULATOR_LOOP.md, two missing rules:
   - The §13 rule verbatim: never BuildPipeline.BuildPlayer via MCP
     script-execute (10× retry = build storm); fire-and-forget via
     EditorApplication.delayCall / menu item + marker file. The doc explains
     tier-2's append re-export but not how to invoke it without the storm.
   - Under the standing rule: "The bootstrap/seed requirement is a local
     workaround, not pipeline design — standard CI runs xcodebuild cold on
     fresh exports. The §§1–7 anomaly must be root-caused before any CI
     adoption (testflight_distribution may eventually want CI)."

4. For the record, no action: the §13 orphan hypothesis is logged in
   AI_CONTEXT as the first cheap check on recurrence. Investigation closed.
```

### K9 · ui_frame_pacing (smoke #8) — Surgical · LAND BEFORE K6 LOCKS

```
Task: ui_frame_pacing — UI animations feel choppy on the physical iPhone,
mode-slide carousel especially; smooth in editor.

ROOT CAUSE (source-verified, Architect 2026-08-05):
NOTHING in runtime code sets Application.targetFrameRate — all 18 repo hits
are Editor capture tools (MapViewCaptureBotMenu, BotVideoRecorder,
AudioFidelityCapture, the demo recorders). Unity's MOBILE default when unset
is 30 fps. The whole game renders at 30 on device; the editor runs at 60+.
The carousel slide (ModeCarouselController.LerpToTargetLayout, 0.22 s cubic
ease-out on unscaledDeltaTime) gets ~6–7 rendered frames per slide at 30 fps,
with the largest positional steps front-loaded by the ease-out — that IS the
choppiness. The animation code is frame-rate independent and correct: do NOT
retune durations, do NOT rewrite the carousel.

FIX — one new bootstrap file, additive, no scene edits:
  Assets/Scripts/Core/FramePacingBootstrap.cs (Assembly-CSharp)
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  → Application.targetFrameRate = 60;
Follow the existing bootstrap pattern (SfxBusReset.cs / StaminaRuntimeService
Boot / BuildStamp.Bootstrap). One knob, one place; comment WHY (mobile
defaults to 30 when unset). Do NOT touch QualitySettings.vSyncCount —
ignored on iOS. Applies to Android too (same 30-fps default); fine.

DO NOT (scope):
- 120 Hz / ProMotion: Cesar's 14 Pro Max can do 120, but it needs
  targetFrameRate=120 + the CADisableMinimumFrameDurationOnPhone Info.plist
  key, and the battery/thermal cost is real. Per-tier fps is an Order 900
  quality-tier decision — note the hook in a comment, don't build it.
- Per-scene pacing (60 menus / 30 in-hole): only if the knock-on below
  bites; report first.
- Any carousel / animation / ModeCardController code changes.

KNOCK-ON — REPORT, don't absorb:
60 fps halves the frame budget (33.3 → 16.6 ms). Menus will hold trivially;
HOLE scenes may not on device — a hole that drops frames at 60 feels WORSE
than a steady 30. After the fix, explicitly report in-hole frame feel on
device (one hole is enough). If holes can't hold 60, SAY SO — per-scene
pacing or the Order 900/940 perf phase owns that call; do not silently
revert menus to 30.

FALLBACK H2 (only if slides still hitch at 60 — spikes, not low rate):
LerpToTargetLayout writes sizeDelta + anchoredPosition on all 12 card
instances (4 modes × 3 virtual passes) every frame — a full layout dirty
per frame. A mitigation exists (animate only the visible ±2 cards) but do
NOT build it preemptively. Measure first, report numbers.

SEQUENCING: land BEFORE the K6 arrow_speed_retune calibration LOCKS —
arrow rendering at 60 fps changes perceived speed; Cesar should calibrate
feel at the shipping frame rate.

VERIFY — feel is DEVICE-ONLY (sim renders at the Mac's refresh rate and
false-passes smoothness; perf class = INVALID sim surface). The mechanism
IS editor/sim-checkable: Debug.Log Application.targetFrameRate at boot → 60.
Device: mode slides visibly smoother, Cesar's eyeball is the gate; spot-check
one hole for the knock-on above.
```

### K10 · ob_recovery_fixes (smoke #9) — ✅ DONE 2026-08-05 (Cesar-approved)

**Shipped:** `90dd574ff` (camera + drop rule) · `ed65f5726` (capture Y-flip fix + harness).
Folder moved to `Docs/Specs/Completed/ob_recovery_fixes/`; clip in `Docs/Reports/Media/2026-08-05_ob_recovery_fixes.mp4`.

- **Part A (symptoms 2+3):** `OBFreeze`/`CupZoom` are focus-based modes with no null-target
  early-return, so they kept running through the next aim phase and overwrote the pin-facing
  re-aim + orbit drag (both Chase-gated). Director now exits them → `Chase` on entry to `Aiming`.
  **Same-class finding: `CupZoom` was broken identically** (every hole-out wedged the next
  aim phase) — fixed in the same conditional.
- **Part A follow-up (Cesar ruling):** OB no longer cuts to an aerial view — `ModeMap[OB]` = `Chase`,
  pivot teleport + `ComputeOBFreezePivot` deleted; the camera just stops chasing (0.00 m drift, on video).
  A ground-level "horizon settle" variant was built, reviewed and **reverted at Cesar's call** — the
  plain freeze is what ships.
- **Part B (symptom 1, Cesar ruling = real golf):** boundary OB is **stroke and distance**
  (drop at previous origin → first-shot OB re-tees); water keeps last-dry-touch via the untouched
  `OBDropResolver.Resolve`. **Known approximation:** a long carry over land that splashes drops at the
  last *bounce*, which can sit behind the true crossing point — refining that is a separate design row.
- **Tests:** 250 pass / 0 fail, incl. an end-to-end test on the real `ChaseCamera` + Director + SM.

**Spun out of this task — permanent capture fix (`ed65f5726`):** mid-recording `ScreenCapture`
reads were flipping Recorder frames on Metal (proven 1:1 by frame-pts↔capture-log correlation).
`CaptureCore.RecordingActive` now hard-refuses every snap while recording; stills are extracted
from the finished mp4. **Rule for all future capture bots: never snap a still during a recording.**

<details><summary>Original kickoff (historical)</summary>

```
Task: ob_recovery_fixes — three symptoms on the shot AFTER an OB; one camera
root cause + one design-rule change. Smoke #9 (device, Hole 1, first-shot OB
into the right tree line; build 10fc22e+595c, 08-05 09:29).

SYMPTOMS (Cesar, device):
1. Ball not returned to the tee after a first-shot OB (dropped at green edge).
2. Aiming line points BACKWARDS (toward the tee).
3. Camera cannot be dragged sideways during that aim phase. The next shot
   fires → everything recovers.

NOT K1. camera_drag_touch_origin (`bb59d32dd`) is fixed + device-verified;
normal-shot drag works. Do NOT touch InputSystemSource or the orbit input
read.

────────────────────────────────────────────────────────────────
PART A — camera wedge after OB (symptoms 2+3, ONE root cause,
source-verified by the Architect — re-verify the chain, then fix)
────────────────────────────────────────────────────────────────
Chain, all in Assets/Scripts/Physics/Viewer/:

LoopCameraDirector.HandleStateChanged:
  →OB: ResetToOrigin(LastShotOrigin,…) ← _shotOrigin = the TEE on shot 1
       SetOBFreezePivot(pivot)         ← OB crossing point
       ModeMap[OB] = Mode.OBFreeze
       SetTarget(null)                 ← terminal clear; its comment claims
                                         "aim owner takes over via ChaseCamera
                                         LateUpdate null-target early-return"
  →Aiming (from ReArm): ModeMap[Aiming] = null = "leave whatever was set"
       → mode STAYS OBFreeze through the entire next aim phase.

ChaseCamera.RunLateUpdateLogic: the null-target early-return exists ONLY for
Chase/GroundLevel. OBFreeze keeps running every frame with
focus = _target ?? _shotOrigin = THE TEE:
  desiredPos = _obFreezePivot (out at the OB crossing)
  desiredRot = LookRotation(tee − pivot)  ← camera looks BACKWARDS
LateUpdate therefore overwrites, every frame:
  – the pin-facing re-aim (ApplyCameraYaw committed in
    PhysicsLabController.RepositionBallWithLookDir)   → symptom 2
  – the orbit drag written in Update (HandleCameraOrbit) → symptom 3
Why AtRest shots are fine: ModeMap[AtRest] = Chase → null-target early-return
→ aim owner runs. The Aiming=null entry predates OBFreeze (§2b); OBFreeze
broke the invariant that terminal modes are inert during aim.

FIX (director-side, minimal — respect the single-writer rule; do NOT
restructure ChaseCamera):
In HandleStateChanged, on change.Next == BallState.Aiming:
  if (setter.CurrentMode == ChaseCamera.Mode.OBFreeze)
      ApplyMode(ChaseCamera.Mode.Chase);
Chase + null target = dormant → the aim camera owner takes the view back.
⚠️ Do NOT blanket-map Aiming→Chase: the null entry protects putter mode
(EnterPutterMode sets GroundLevel; re-arms happen while putting).

SAME-CLASS CHECK (report; fix only if same-shape): InCup → CupZoom is also a
pivot/focus mode with no null-target early-return. If the NEXT hole's first
aim phase can run with mode still CupZoom (does anything reset it before
SetupAtTee?), it wedges identically. Check and report; if broken, include
CupZoom in the same conditional exit.

────────────────────────────────────────────────────────────────
PART B — drop rule (symptom 1): Cesar RULING 2026-08-05 = REAL GOLF
────────────────────────────────────────────────────────────────
Current behavior: OBDropResolver.Resolve drops at the LAST in-bounds terrain
hit; falls back to _lastShotOrigin only when no safe hit exists. Deliberate
§2e design — now ruled against.

New rule (real golf):
– Boundary OB (result.OBReason != Water): STROKE AND DISTANCE — drop at the
  previous shot origin (_lastShotOrigin). First-shot OB → back on the tee.
– Water: KEEP current behavior (last dry touch ≈ lateral relief near entry,
  never nearer the hole). KNOWN APPROXIMATION: a long carry over land that
  splashes drops at the last BOUNCE, which can sit well behind the real
  crossing point. Accepted for now — note it in the report; refining to the
  actual water-crossing point is a separate design row if Cesar wants it.

Implementation: branch on OBReason at the §2e call site in
PhysicsLabController.HandleShotComplete (BallState.OB case) — water path
keeps OBDropResolver.Resolve; boundary path uses _lastShotOrigin directly.
Leave OBDropResolver itself unchanged (water still uses it). The
aim-toward-pin yaw computation stays as-is — it is correct once the camera
stops fighting it (re-tee drop → ComputeYawTowardPin(tee, pin) = down the
fairway). Penalty/turn arithmetic: DO NOT touch — TURN counting is already
correct (Cesar's TURN 3 after a first-shot OB = shot + penalty + 1).

CONSTRAINTS:
– No changes to ChaseCamera internals beyond (at most) the CupZoom finding;
  no changes to BallStateMachine / ReArm semantics; keep the OB hold beats
  (water 1.2 s, boundary 2.0 s) — shipped behavior.
– Run the Physics test assembly; add a test for the boundary→origin branch
  wherever the OB drop is covered (NextShotHandoffTests neighborhood).

VERIFY — EDITOR-VALID (state-machine logic, not device-only):
1. Editor: fire a deliberate boundary OB (ObBoundaryCaptureBot menu or
   manual). After the drop: ball at the previous origin, camera behind the
   ball facing the pin, aim line forward, mouse orbit drag WORKS. The drag
   check is the wedge regression test — it FAILS on HEAD today.
2. Water OB: ball still drops at last dry touch; camera/aim/drag equally
   healthy afterward.
3. Device: one boundary-OB repro on iPhone for confidence (drag is the
   K1-verified path; expected to just work once LateUpdate stops fighting).
4. Report the CupZoom same-class finding either way.
```
</details>

### K11 · club_selection_green_gate — ✅ DONE 2026-08-05 (Cesar-approved)

**Shipped:** `066df31f2` (selector gate) · `efa681acb` (the deferred §2f-after-reposition item).
TellCode-dispatched — no `Docs/Specs/` folder for this one.

- **Gate at the UI layer, reusing §2f — not a second rule.** `EnterPutterMode`/`ExitPutterMode`
  publish to `ClubSelectionBroadcast` (same static-bus/asmdef-isolation precedent as `Raise`);
  eligibility is one pure `IsSelectable(labClubIndex, putterLabClubIndex, inPutterMode)` shared by
  `Populate` and `Scroll`. Reading the same decision that flipped the club is what stops the gate
  and the auto-switch from fighting the player. Bots / map view / debug stay **ungated**;
  `SetClub`, `PutterModeSurfaceController`, `ClubContext.RequestSelection` untouched.
- **Shipped disabled, not hidden** — alpha 0.5 + non-interactive (ball-selector precedent), with
  `CanvasGroup` added to runtime clones so no prefab is dirtied. Every commit path guarded; `Scroll`
  steps over ineligible clubs and returns `bool` so the hold-scroll coroutine exits instead of spinning.
- **Needed beyond the brief:** `Enter/ExitPutterMode` only fire on a club *change*, so a boot-time
  publish in `Start()` was required or the gate would have been inert for the whole first hole.
  `IsSelectable` fails open on an unpublished index so it can never soft-lock the selector.
- **Deferred item — now done (`efa681acb`):** hooked the §2f re-decide into `RepositionBallWithLookDir`,
  the single seam `PlaceBallAt` + both OB hold coroutines funnel through, classifying the drop point
  with the same baked classifier the sim uses for `EndSurface`.
- 🔵 **Scope correction:** K10's stroke-and-distance rule made **boundary OB self-correcting** (the drop
  returns to the previous origin, where the club was already right). Real exposure is **water relief
  crossing the green boundary**, plus `PlaceBallAt` — narrower than the kickoff implied.
- **Tests:** Gameplay 252 / Physics 257, 0 fail. `RepositionClubReDecideTests` runs against **real baked
  Hole 6 zone data** with points discovered by scanning, so a re-bake that moved the green fails it.

⚠️ **Process scar — parallel close-outs stage whole files.** K10's close-out (`90dd574ff`) staged all of
`PhysicsLabController.cs` and swept in three in-flight K11 lines, leaving `origin/main` calling a
`ClubSelectionBroadcast.SetPutterMode` that did not exist yet (CS0117 on a fresh checkout; invisible
locally because the working tree had both halves). Repaired forward by `066df31f2` — no history rewrite,
`90dd574ff` was already pushed. **Rule: a publisher/consumer pair split across two asmdefs must be
committed together.** CLAUDE.md Rule 12 guards the reverse direction only; nothing guards this one.

<details><summary>Original kickoff (historical)</summary>

```
Task: club_selection_green_gate — the putter is selectable ONLY on the
green, and non-putter clubs are NOT selectable on the green. Player-facing
selection gate (Cesar, 2026-08-05).

CONTEXT — the rule already exists; the UI just doesn't enforce it:
§2f auto-switch (PutterModeSurfaceController.DecideTargetClub, called from
PhysicsLabController.HandleShotComplete AtRest branch) already flips to the
putter when the ball rests on Green and back to _lastNonPutterClubIndex when
it rests elsewhere. The classification is GREEN-STRICT: SurfaceType.Green
only — GreenCollar counts as OFF-green. The gate must reuse THIS
classification — do not invent a second rule; if the gate and §2f disagree
they will fight the player.

WHAT'S UNGATED TODAY (all paths funnel into ClubSelectionBroadcast.Raise →
PhysicsLabController.OnClubBroadcastReceived → SetClub):
1. SelectorOverlayWidget.Populate() Kind.Club — builds a selectable card for
   EVERY club in ClubContext.EquippedBag, no surface awareness.
2. SelectorOverlayWidget.Scroll(±1) — arrow buttons + hold-scroll over the
   full bag.
Both in Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs.

DESIGN — gate at the UI layer, NOT inside SetClub:
Bots (BotDriver/VersusBot), map view, and debug paths call SetClub
programmatically and must stay ungated — §2f keeps the player state correct;
the defect is only that the SELECTOR lets the player override it.

IMPLEMENTATION:
a) Putt-mode flag visible to UI: PhysicsLabController.EnterPutterMode /
   ExitPutterMode are the existing single entry/exit (driven by SetClub via
   OnClubIndexChanged). Publish a static flag there that Gameplay.UI can
   read — follow the ClubSelectionBroadcast static-bus precedent (same
   asmdef-isolation reason): e.g. ClubSelectionBroadcast.InPutterMode plus
   PutterLabClubIndex (UI must not hardcode 3 and must not reference
   ShotController directly).
b) Eligibility as a pure static (testable, shared by Populate + Scroll):
   IsSelectable(labClubIndex, putterLabClubIndex, inPutterMode)
     inPutterMode  → only the putter
     !inPutterMode → everything EXCEPT the putter
c) Populate(): ineligible cards render DISABLED — grayed, non-interactive
   (match the ball-selector putter-mode precedent: alpha 0.5, no
   interaction). Guard EVERY commit path for disabled cards: the card's
   selection callback, CommitHighlighted, UpdateHoldHover (no highlight on
   disabled), EvaluateRelease returning OnCard over a disabled card → treat
   as Outside. If SelectorCardWidget makes a disabled state awkward, HIDING
   ineligible cards is an acceptable fallback — report which you shipped.
d) Scroll(): skip ineligible indices (off-green: skip the putter; on-green:
   arrows effectively no-op). Mind ArrowScrollRoutine — the hold-scroll
   coroutine must not spin trying to reach a skipped index.

⚠️ K10 OVERLAP — EXPLICITLY DEFERRED, DO NOT DO IN THIS PASS:
Repositioned balls (OB water drop, PlaceBallAt) never run the §2f decision,
so putter-mode can be stale after a drop onto/off the green. The fix (run
DecideTargetClub after reposition) lands in the SAME PhysicsLabController
region K10 is editing. Keep K10/K11 parallel-safe: SKIP it here; it is a
one-line follow-up AFTER K10 merges. Log it in your report so it isn't
lost.

DO NOT:
- Gate Bags/Inventory screens — out-of-round club management stays free.
- Touch SetClub, bots, the map-view SHOOT repurpose (iter-38 router guard),
  or ClubContext.RequestSelection semantics.
- Change §2f / PutterModeSurfaceController.

TESTS: pure-logic tests for IsSelectable (both modes). Run the Gameplay
test assembly (ShotControllerPuttModeTests neighborhood) — no regressions.

VERIFY — EDITOR-VALID:
- Off green: putter card disabled (or hidden); arrows skip it; hold-drag
  release over it commits nothing.
- On green (§2f flipped to putter): other clubs disabled; arrows no-op;
  putter still commits fine.
- Ball selector (Kind.Ball) untouched in both modes.
- Bot smoke: one BotDriver hole plays through unchanged (bots bypass UI).
- Device pass optional — pure UI logic, editor/sim sufficient.
```

</details>

### K12 · matchmaking_scan_pacing — Surgical · AFTER K11 per Cesar · EDITOR-verifiable

```
Task: matchmaking_scan_pacing — the 1v1 "FINDING OPPONENT" animation should
start FAST and decelerate before landing on the opponent (slot-machine
feel), and the total wait is too long — shorten it. (Cesar, 2026-08-05.)

MEASURED CURRENT BEHAVIOR (source-grounded):
FILE: Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs
OpponentScanRoutine cycles opponents at a CONSTANT
opponentCycleIntervalSeconds (0.3 s) for searchDurationSeconds (5 s), then
holds 0.6 s on "OPPONENT FOUND" before GameplaySceneLoader.BeginGameplayLoad.
Total wait before the load even starts ≈ 5.6 s. No easing of any kind.

⚠️ SCENE-SERIALIZATION TRAP — read before coding:
The tunables are [SerializeField] and ShellScene.unity SERIALIZES them
(searchDurationSeconds: 5 at ~line 131153). Changing the C# defaults does
NOTHING — the scene values win. And ShellScene is OFF-LIMITS right now (K7
mid-flight, no merge driver). Fix: introduce NEW serialized fields — absent
from the scene YAML, so the script defaults take effect with ZERO scene
edit. Deprecate the old two in a comment; a later housekeeping pass can
remove them + their scene entries when ShellScene is next legally edited.

IMPLEMENTATION (one file, one coroutine):
New fields (script defaults become live immediately):
  scanTotalSeconds       = 2.5f   // was 5 via scene — total cut ≈ 5.6→~3.1 s
  scanStartIntervalSeconds = 0.10f  // fast flicker at start
  scanEndIntervalSeconds   = 0.50f  // slow holds before the find
OpponentScanRoutine: replace the constant wait with a decelerating ramp —
  t = elapsed / scanTotalSeconds  (0→1)
  interval = Mathf.Lerp(scanStartIntervalSeconds, scanEndIntervalSeconds,
                        t * t)    // ease-out: fast early, slow late
  yield WaitForSeconds(interval); elapsed += interval  (accumulate the
  ACTUAL interval used — the old code added the constant).
≈ 9–10 name flips: ~5–6 in the first second, 2–3 slow holds at the end.
The deceleration lands naturally on the final opponent — finalPick already
tracks the last displayed entry; keep that mechanism untouched.

KEEP UNCHANGED:
- The 0.6 s "OPPONENT FOUND" beat (Stage C0 staging — deliberate, the modal
  hides at the FadeController midpoint; do not shorten without Cesar).
- DotCycleRoutine (status dots) — independent, fine at 0.4 s.
- Phase enum transitions (BotDriver test seam reads Phase; loop_v2 bots just
  get a faster wait — no seam change).
- GameSession seeding / MatchContext population order.

VERIFY — EDITOR-VALID:
1. Editor: open 1v1 matchmaking from the mode carousel. Scan visibly starts
   fast and decelerates; last displayed name/card == the opponent the match
   starts against; total scan ≈ 2.5 s (log elapsed at OpponentFound).
2. Cancel mid-scan still works (coroutines stop, home panels restore —
   OnHide path untouched).
3. Bot smoke: loop_v2 matchmaking-dependent bot run passes (Phase seam).
4. Report before/after totals; Cesar tunes the three fields in the
   Inspector afterward if the feel is off — they are serialized for exactly
   that.
```

### K13 · boot_loading_screen_removal — **CLOSED 2026-08-05** (`d3bf00026`, Cesar-approved)

**Outcome: REMOVED** (the branch the kickoff expected). Step-0 measurement, two baseline
runs driving the real `StartButton.onClick`, confirmed the static read exactly:

| Metric | run 1 | run 2 |
|---|---|---|
| boot init complete (AfterSceneLoad) | t=3.75s | t=3.88s |
| Splash interactive | t=9.27s | t=8.99s |
| Loading screen visible | 2.502s | 2.476s |
| click → Home total | 2.749s | 2.723s |
| `_useExternalProgress` ever true | **False** | **False** |
| max `_realProgress` | **0.000** | **0.000** |
| worst frame during Loading | 21.5ms | 17.9ms |

Zero real progress, ever. Boot init finished 5.2 s *before* START was tappable. The only
real cost behind the transition is a ~225 ms `AudioManager.PlayMusic(Main Theme)` decode
running synchronously inside `ApplyScreen(Home)` — shorter than the 0.25 s fade that
already covers it. Real work ≈ 0.23 s ≪ 2 s → remove.

**After: click → Home = 0.468–0.483 s** (was 2.72 s). The music spike is unchanged
(224.7 ms vs 230.3 ms baseline), so removal added no cost — it stopped hiding 2.2 s of
nothing. Changed files: `SplashScreenController.cs` (+ rationale/re-entry comment) and
`HomeScreenController.cs` (PLAY fallback → `Debug.LogError`). `LoadingScreenController`,
`GameplaySceneLoader`, `ScreenManager` verified byte-identical to HEAD.

Verification: (1) Logo→Splash→START→Home direct, bars/chrome correct, no flash ✅
(2) GOLFIN_DEMO — **static only**, the define is build-profile-scoped so it is inactive in
the Editor; Home is on `DemoGate.Allowed` so Loading never opens ✅ (by construction, not
by run) (3) HoleLoad gate via the real `ModeHomeCard(Clone)/PlayButton`: OPPONENT FOUND →
`BeginGameplayLoad`, `_useExternalProgress=True`, `_realProgress` 0.00→0.45→0.95→1.00 over
112 frames, 2.586 s, hands off to gameplay ✅ (4) `CreateUsername → Home` unchanged ✅

⚠️ **Adjacent knob still open (reported, not changed):** `FinishLoadingCoroutine` enforces
`minLoadingTime` as the MINIMUM for the HOLE-LOAD screen too — measured live at **2.586 s
with real progress already at 1.0**. Scene-serialized in ShellScene (~line 111701, value
`2`); changing it later needs K12's new-serialized-field technique or a scene edit.

<details><summary>Original kickoff (kept for reference)</summary>

```
Task: boot_loading_screen_removal — the initial loading screen is a
hardcoded timer. Cesar's rule (2026-08-05): make it reflect REAL loading,
or REMOVE it if the real wait is under 2 seconds.

GROUNDED CURRENT BEHAVIOR:
Boot flow: Splash START/Play → SplashScreenController.OnStartClicked →
ScreenManager.ShowScreen(ScreenId.Loading) → LoadingScreenController in
LegacyBootHome mode → auto-navigates to Home.
LegacyBootHome is 100% FAKE: target = timer / minLoadingTime
(minLoadingTime scene-serialized = 2, ShellScene ~line 111701), display bar
chases at 0.5/s, finish requires timer ≥ 2s AND bar ≥ 0.999 → ~2.0–2.2 s of
pure theater. NOTHING feeds it real progress — the only
SetProgress/SetRealProgress callers in the repo are GameplaySceneLoader's
(HoleLoad path). Heavyweight boot init (CSV singletons, CharacterManager,
save load) runs in Awake/RuntimeInitializeOnLoad — done before the Splash
screen is even interactive.
→ Real remaining work at Loading-show ≈ 0 s → per the <2s rule: REMOVE.

STEP 0 — MEASURE FIRST (cheap, guards against invisible async work):
Log Time.realtimeSinceStartup at ShowScreen(Loading) and log any work still
running at that moment. Expected: nothing but the timer. IF measurement
finds ≥2 s of real async boot work the static read missed, STOP — wire
SetRealProgress from that work instead of removing, and report. Otherwise
proceed with removal.

IMPLEMENTATION (removal branch, expected):
1. SplashScreenController.OnStartClicked: ShowScreen(ScreenId.Loading) →
   ShowScreen(ScreenId.Home). That is the entire boot change.
2. HomeScreenController.OnPlayClicked legacy fallback (~line 454): when
   matchmakingModal is unwired it shows ScreenId.Loading — a fake screen
   that bounces back to Home. Replace the fallback with a Debug.LogError
   (it only fires on a wiring bug; navigating to a fake loader helps
   nobody). Do not touch the modal path above it.
3. DO NOT touch LoadingScreenController, LoadingBar, GameplaySceneLoader,
   or ScreenManager — the HoleLoad path (real progress: host op 0–50%,
   hole op 50–100%, FinishLoadingCoroutine) reuses the same screen and
   must keep working byte-identically. This also keeps K13 conflict-free
   with K12 (in flight in MatchmakingModalController).
4. Keep the Loading screen GameObject + ScreenId — it is the HoleLoad
   surface, and a future real boot dependency (backend login is on the
   roadmap) can re-enter the flow via the existing SetRealProgress
   plumbing. Leave a comment at the OnStartClicked call site saying so.

KNOWN ADJACENT KNOB — report, do not change:
FinishLoadingCoroutine enforces minLoadingTime (2 s) as the MINIMUM for the
HOLE-LOAD screen too. That path is real-progress-driven and the floor is
deliberate anti-flash staging. If Cesar wants the hole-load handoff
snappier later, that scene-serialized field is the knob (same
scene-serialization trap as K12 — flag only).

VERIFY — EDITOR-VALID:
1. Editor, full game: Logo → Splash → START → lands DIRECTLY on Home; bars/
   chrome correct per the ScreenManager show/hide matrix; no Loading flash.
2. Demo define (GOLFIN_DEMO): Splash "Play" → Home works (DemoGate's
   allowed-screens list includes Home; Loading simply never shows).
3. 1v1 matchmaking → OPPONENT FOUND → hole-load Loading screen still
   appears with a REAL progress bar and hands off to gameplay — the
   HoleLoad regression gate.
4. Login/CreateUsername → Home paths unchanged (they never used Loading).
5. Report the Step-0 measurement numbers either way.
```

</details>

### K14 · loading_bar_inset — TellCode · SHELLSCENE QUEUE (after `safe_area_top_bar` (K7) lands) · sim-valid

```
Task: loading_bar_inset — the loading bar is too wide, nearly touching the
phone's sides. Inset it ≈ 8 px per side. Do not break the fill behavior.
(Cesar, 2026-08-05. Post-K13 this bar only appears on the HOLE-LOAD
screen.)

GROUNDED TARGET:
ShellScene.unity → GameObject "LoadingBarRoot" (GO fileID 342786665,
RectTransform fileID 342786666, ~line 18530): full-stretch anchors
(0,0)–(1,1), sizeDelta (0,0), centered pivot → spans its parent's full
width. Children (fill bar 957728170 + progress text 2073758602) follow it.

THE EDIT — one value:
LoadingBarRoot RectTransform m_SizeDelta.x: 0 → -16.
Stretch anchors + centered pivot make that exactly 8 units off EACH side.
Functionality is safe by construction — LoadingBar drives Image.fillAmount,
which is proportional to whatever width the rect has.

DIAGNOSE FIRST (30 seconds): confirm the parent (fileID 1921467785) is not
itself inset and that the fill child uses stretch anchors inside the root.
If the fill child has FIXED width instead, inset THAT rect's sizeDelta
instead and say so.

UNITS NOTE (updated with the `nav_bar_edge_gaps` (K4) finding): the canvas
is ConstantPixelSize ⇒ 8 canvas units = 8 DEVICE pixels — subtle on a 3×
panel. If Cesar meant 8 visual points, the dial is ~24 (-48 total). Start
at -16 per his words, screenshot on the sim at iPhone-14 resolution, he
calls it; the single value is the dial either way.

⚠️ SEQUENCING — THIS IS A SHELLSCENE EDIT:
- ShellScene is occupied (`safe_area_top_bar` (K7) in flight). Run this
  ONLY when the scene frees up, as its own ISOLATED commit (one-value YAML
  diff; diff the scene before committing, revert unrelated drift —
  standing rule, no merge driver).
- RESOLVED 2026-08-05: `nav_bar_edge_gaps` (K4) closed `49825e867` — cause
  was per-bar fixed-width anchors, canvas is ConstantPixelSize, NOT the
  CanvasScaler. No global width shift; no extra wait beyond the scene
  freeing up.
IF CESAR WANTS IT SOONER: fallback that avoids the scene — in
LoadingScreenController.Awake, nudge the bar root at runtime
(offsetMin.x += 8; offsetMax.x -= 8). Works today, but it is a code shim
for a scene concern; prefer the scene edit unless Cesar explicitly asks.
State which route shipped.

VERIFY — sim-valid (layout class):
1. Hole-load screen (1v1 flow): bar visibly ≈ 8 px off each side; progress
   text position still correct.
2. Full fill sweep 0% → 100%: fill reaches BOTH rounded ends exactly — no
   undershoot/overshoot (fillAmount is proportional; this is the
   don't-break-functionality gate).
3. 19.5:9 and 16:9 Game view: inset reads correctly at both aspects.
4. Screenshot to Cesar for the final look call.
```

---

## How current work is actually tracked

1. **Live queue = `Docs/Specs/Active/`** — spec-sized tasks are folders there; authoritative for what's open at spec scale.
2. **Kickoff-sized tasks = PENDING KICKOFFS section above** — full fenced blocks, written when produced (rule confirmed by Cesar 2026-08-04).
3. **Completed tasks = `Docs/Specs/Completed/<slug>/`.**
4. **Session state headline = `Docs/AI_CONTEXT.md`** — upload at session start.
5. **Pre-2026-05-01 narrative history = `Docs/Archive/TELLCODE_HISTORY.md`** + git history of this file.

## Rules

- Spec-sized tasks (>50 lines): per-task folders under `Docs/Specs/Active/<slug>/SPEC.md`; this file gets a pointer only.
- Kickoff-sized tasks: full fenced kickoff block in PENDING KICKOFFS, at the time it is produced — chat-only kickoffs are forbidden (they die with the session).
- Chat delivery to Cesar = the FULL fenced block as well, not a one-line pointer (rule confirmed by Cesar 2026-08-05: he wants to see the info inline). TellCode is the durable copy; the chat block is the readable one. Both, every kickoff.
- Task references use the TASK NAME first, K-number in brackets: `nav_bar_edge_gaps` (K4). Never a bare K-number — in chat or in new text in this file (Cesar 2026-08-05). Existing headers already carry both; no retro-rewrite needed.
- Refresh the CURRENT STATE bullet whenever touching this file.
- New UI tasks use the multi-agent pipeline at `.claude/agents/` (see `CLAUDE.md` § Multi-Agent Workflow).
- Live course importer is `HoleGeoImporter.cs` (NOT `HoleLiteImporter.cs` — deprecated, banner header, commit 980cc122). Verify via `grep MenuItem` before touching importer internals.
