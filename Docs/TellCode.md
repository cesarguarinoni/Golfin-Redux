# TellCode.md — handoff channel (POINTER + KICKOFF FILE)

> **Spec-sized tasks live in `Docs/Specs/Active/<slug>/SPEC.md` — this file only points at them.**
> **Kickoff-sized tasks (no spec folder) live HERE in full**, in the PENDING KICKOFFS section, so they survive the chat session that produced them. (Rule updated 2026-08-04 by Cesar: chat-only kickoffs die with the session; every kickoff the Architect produces is written here at the time it is produced.)

---

## ▶ CURRENT STATE — update this block at every session boundary

- **Last updated:** 2026-08-04 15:28 JST (Architect — **DEVICE ERA.** Game builds+runs on physical iPhone since 2026-07-27; signing SOLVED (do not re-litigate); on-device smoke found 7 issues. Fixed since: `centralball_device_invisible` (device-verified `1a4ad15ca`), `hole6_tree_collision_profiles` (`c1d38e280`). Shipped: `build_version_stamp` (3 defects → hardening kickoff below). **iOS Simulator three-tier verification loop VALIDATED** — canonical doc `Docs/Pipeline/IOS_SIMULATOR_LOOP.md`; standing rules: never wipe the seeded DerivedData, never `BuildPipeline.BuildPlayer` via MCP script-execute. Full story: `Docs/Reports/2026-08-04_ios_simulator_build_blocker.md` §§10–13 + `Docs/AI_CONTEXT.md` top block. **OPEN = the PENDING KICKOFFS below** (5 smoke issues + build-stamp hardening + housekeeping) plus `putter_aim_blue_line` (413, SPEC_READY in `Specs/Active/`, awaiting Cesar go) and a device pass on `demo_build_slice` (426). Everything below this bullet predates the device era and is historical.)

- **Last updated:** 2026-07-02 (Architect — `1v1_result_rewards_display` (347) DONE. NEXT-at-the-time = `stamina_boost_shop` (517) design pass. STALE — superseded by the device-era bullet above.)
- Older narrative bullets (2026-06-11 → 2026-06-24): preserved in git history of this file — all tasks named in them are closed in `Docs/Specs/Completed/`. Trust `Docs/Specs/Active/` + the AI_CONTEXT headline, not old bullets.

---

## 📋 PENDING KICKOFFS — 2026-08-04 batch

Paste any block below into Code as-is. Produced by the Architect during the 2026-08-03/04 sessions; grounded against source at time of writing. Delete a block (and log it in CURRENT STATE) when its task closes.

**Sequencing constraints:**
- `nav_bar_edge_gaps` BEFORE `safe_area_top_bar` (same two bars, same scene; #1's outcome determines the bars' final geometry). Back-to-back isolated commits, no other ShellScene work interleaved.
- `camera_drag_touch_origin` verification is DEVICE-ONLY (sim false-passes it). `tree_wind_device` verification is DEVICE-ONLY (sim false-passes it — measured, report §11). `arrow_speed_retune` and `safe_area_top_bar` are editor/sim-verifiable.

### K1 · camera_drag_touch_origin (smoke #3) — Surgical

```
Bug: on a physical iPhone, touch-and-drag no longer moves the camera sideways
during aim. Works correctly in the Unity editor with a mouse. Editor-vs-device
divergence, not a missing feature.

START HERE — primary hypothesis, verify before fixing:
Assets/Scripts/Gameplay/Input/InputSystemSource.cs

HandlePressStarted() sets `_origin = _currentPosition`, but `_currentPosition`
is only written in Update(). Input System callbacks fire before MonoBehaviour
Update() in the same frame, so at press time `_currentPosition` holds the
PREVIOUS frame's value.

With <Mouse>/position that is harmless — the cursor was already at that point,
so `_origin` is correct. With <Touchscreen>/primaryTouch/position there is no
position before the finger lands: it holds the LAST RELEASED touch's position,
or (0,0) at launch. So `_origin` is stale and every `current - origin` delta
carries a spurious offset on press.

Related, same root: <Mouse>/position updates continuously while unpressed;
primaryTouch/position does not. Any consumer reading TouchPositionPx while
IsTouching is false gets live data in the editor and stale data on device.

Fix direction: sample the live position inside HandlePressStarted rather than
reusing the cached field, so `_origin` is correct on the frame of the press.

ALREADY RULED OUT — do not re-investigate:
Shot.inputactions bindings are correct. Both Touch and TouchPress have proper
<Touchscreen>/primaryTouch/{position,press} bindings alongside the <Mouse>
ones. Do not edit the .inputactions asset.

IF H1 IS DISPROVEN BY MEASUREMENT, fall back in this order:
H2 — the "don't move camera while touching the club handle" gate. Check
     ClubHandleDragger.cs and ShotController.cs for
     EventSystem.current.IsPointerOverGameObject() called WITHOUT a fingerId.
     The parameterless overload is mouse-semantics and is unreliable under
     touch. Unverified; it is a grep, not an investigation.
H3 — a full-screen GraphicRaycaster swallowing touches the mouse path misses.
     Least likely, check last.

CONSTRAINTS:
- Do NOT rewrite ChaseCamera / LoopCameraDirector. The bug is in the input
  layer, not the camera. Camera code is single-writer and off limits here.
- Minimal diff. Additive guard preferred over restructuring the source.

VERIFICATION — this is the important part:
This bug is INVISIBLE IN THE EDITOR by definition, and the iOS SIMULATOR
FALSE-PASSES it (sim input arrives from the trackpad down the mouse path —
IOS_SIMULATOR_LOOP.md validity boundary). The only valid gate is a build on
the physical iPhone showing sideways drag working, plus confirmation that
dragging ON the club handle still does NOT move the camera.

If you cannot run on device, report the fix as UNVERIFIED and say so plainly.
```

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

### K4 · nav_bar_edge_gaps (smoke #1) — TellCode · RUN BEFORE K7

```
Bug: on a physical iPhone, the top bar and bottom nav bar do not reach the left
and right screen edges — visible gaps on both sides of both bars.

⚠️ THIS IS NOT SAFE-AREA. Safe area on a portrait iPhone insets TOP and BOTTOM,
not left/right. Do not attach SafeAreaFitter as the fix for this. (The notch
issue is a SEPARATE kickoff — K7 — that does use SafeAreaFitter.)

⚠️ Cesar has sliced both bar background images in advance. DO NOT USE THEM YET.
Slicing controls how a bar looks when stretched; it does not make a bar stretch.
Diagnose the width first, then use the slice only if the fix is "stretch it."

STEP 1 — DIAGNOSE. Cheap, decisive, do this before touching anything.
The bars are `topBarPanel` and `bottomNavPanel`, serialized GameObjects on
PersistentUIManager (Assets/Scripts/UI/PersistentUIManager.cs), in
ShellScene.unity. Read the RectTransform anchors on both bar roots.

H1 — anchorMin.x / anchorMax.x are NOT 0 / 1. Fixed-width bars sized to the
     reference resolution. Fix = stretch anchors. Most likely.
H2 — anchors ARE 0/1, but the canvas itself is narrower than the device.
     Check the CanvasScaler on the ShellScene canvas: reference resolution and
     match value. A match leaning toward height on a 19.5:9 device makes the
     canvas wider in reference units than the design assumed.
     ⭐ If this is the cause, it is ONE VALUE and it fixes every screen at once.
H3 — the bar Image is fixed by SetNativeSize / Preserve Aspect / a
     LayoutElement. Least likely; check if H1 and H2 both come back clean.

Report which one it is BEFORE implementing.

STEP 2 — REVIEW THE SLICE (only once stretching is confirmed as the fix):
- Border values non-zero on left and right.
- Mesh Type = Full Rect (Tight silently breaks 9-slice).
- Image Type = Sliced on the Image component.
- Pixels Per Unit Multiplier is the knob if corners render wrong-sized.
🔴 SPECIFIC CONCERN: the top bar has a CENTERED tab/nameplate shape. A
standard 9-slice stretches the middle region, which distorts that tab
horizontally. If the border marks put the tab inside the stretched middle,
the slice is wrong for this art and the tab needs to be a separate centered
child over a stretched background. If the slice is wrong, SAY SO and stop —
do not re-slice Cesar's art unilaterally.

STEP 3 — IMPLEMENT. Minimal diff, identified cause only.
🔴 SCENE RISK: ShellScene.unity edit — 4.1 MB, no YAML merge driver yet
(Order 429 queued). Isolated commit, diff the scene before committing, revert
unrelated default-override drift.

SCOPE: fix these two bars. Do NOT start a whole-game responsive audit. If the
cause is H2 (CanvasScaler), that IS the global fix — say so in the report.

VERIFY: Game view at 19.5:9, 16:9, 4:3 — bars flush both edges at every
aspect, no distorted corners/tab. Sim-valid (layout class). Device confirm.
Spot-check Home, Roster, and an in-hole screen for regressions.
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

### K7 · safe_area_top_bar (smoke #2) — TellCode · RUN AFTER K4

```
Task: safe_area_top_bar — tickets counter is eaten by the Dynamic Island on
iPhone 14 Pro Max. Smoke issue #2.

THE COMPONENT ALREADY EXISTS — do not write a new one:
Assets/Scripts/UI/Core/SafeAreaFitter.cs (GolfinRedux.UI.Core)
Written for exactly this, deliberately unattached until now (its header says
so). [ExecuteAlways], polls Screen.safeArea, converts to anchors.

⚠️ THE TRAP — inset the CONTENT, not the bar BACKGROUNDS:
If topBarPanel (or bottomNavPanel) is moved wholesale inside a SafeArea
wrapper, the bar BACKGROUND moves down too and a raw blank strip appears
between the notch and the bar.
Correct end state:
- Bar background art: FULL-BLEED, extending under the Dynamic Island / into
  the home-indicator zone. Backgrounds are decoration; allowed behind cutouts.
- Bar CONTENT (tickets pill, RP counter, settings gear, screen title; bottom
  nav icon row): inside the safe area.

IMPLEMENTATION:
Follow the component's own header usage: one full-screen "SafeArea" child
under the shell canvas (stretch anchors, zero offsets, SafeAreaFitter
attached), then re-parent the CONTENT sub-objects of the top bar and bottom
nav into it — backgrounds stay outside at full bleed.
- PersistentUIManager serializes topBarPanel / bottomNavPanel — those
  references must survive. Re-parenting CHILDREN is fine; do not rename or
  move the panel roots.
- If content and background are fused, separate minimally — a new empty
  "Content" RectTransform per bar is acceptable. Report before/after
  hierarchy.
- ShellScene.unity edit: isolated commit, minimal diff, diff the scene YAML
  before committing, revert unrelated drift. No merge driver yet (429).

SEQUENCING: run AFTER nav_bar_edge_gaps (K4) — same bars, same scene; K4's
outcome determines the bars' final geometry.

SCOPE: shell canvas only. The in-game HUD (player card / hole info) also
crowds the notch but was NOT the reported issue — CHECK visually and report,
don't fix. The build stamp handles its own inset; leave it alone. If other
screens' content also kisses the notch, that is the deferred full inset pass
— its own row, not scope creep here.

VERIFY — Simulator VALID (safe-area class; ShellScene ships in build data →
tier-1 data swap covers iteration):
- Sim (iPhone 14): tickets pill fully below the notch; NO blank strip between
  notch and top-bar background; bottom nav icons clear of the home-indicator
  band; backgrounds still reach all screen edges.
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
- Refresh the CURRENT STATE bullet whenever touching this file.
- New UI tasks use the multi-agent pipeline at `.claude/agents/` (see `CLAUDE.md` § Multi-Agent Workflow).
- Live course importer is `HoleGeoImporter.cs` (NOT `HoleLiteImporter.cs` — deprecated, banner header, commit 980cc122). Verify via `grep MenuItem` before touching importer internals.
