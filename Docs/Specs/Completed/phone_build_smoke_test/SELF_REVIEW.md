# Self-Review — `phone_build_smoke_test` (iter-1)

**Reviewer:** golfin-self-reviewer
**Timestamp:** 2026-07-27 08:15 JST
**Verdict:** **FORWARD_TO_ARCHITECT**
**STATUS set to:** `SELF_REVIEW_PASS`

Task class: **iOS build preflight** (Player Settings + one additive MonoBehaviour). NOT a Figma/UI-fidelity task and NOT a mesh/terrain task, so Rules 16/18/19 (mesh metrics, Figma fidelity, clone provenance) do NOT apply and are correctly absent. Judged strictly against SPEC §2 (Phase A) and §6 (Implementer scope).

---

## Visual diff notes (Step 1 — pixel scan, screenshot only)

Canonical screenshot: `screenshots/portrait_boot_2026-07-27_07-59-19.jpg` (800 × 1731, long edge 1731 ≥ 900 → Rule 14 satisfied).

The frame is clearly **portrait-oriented** (aspect ~1:2.16, matching iPhone 14 1170×2532 downscaled). Top-left: white "GOLFIN presents" wordmark with a navy-and-gold shield + white-golf-ball emblem, and a script "The Invitational" flourish beside/below it. Center: a full-body illustration of a golfer mid-follow-through in a red-billed white cap and dark trousers, spraying tan bunker sand to the left; behind him a bright blue sky fades to a light-green fairway with a red pinflag on the right edge. Bottom third: turf-green foreground; a green rounded-rect "PLAY" button in white block caps; below that white "CREATE ACCOUNT" and "LOGIN" text. No magenta / missing-shader boxes, no clipped-off UI, no rotation artefacts, no error dialogs. It is the ShellScene title/splash screen rendering cleanly in portrait — exactly what A1 needs to prove.

There is no reference frame to A/B against for this task (correctly — no Figma node in SPEC), so Step 2 (Figma comparison) is N/A.

---

## Ground-truth verification — Phase A items

### A1 — Portrait lock — **PASS**

Read `ProjectSettings/ProjectSettings.asset` directly. Actual bytes on disk:

```
defaultScreenOrientation: 0                     # Portrait (was 4 = LandscapeRight)
allowedAutorotateToPortrait: 1                  # Portrait kept ON
allowedAutorotateToPortraitUpsideDown: 0        # was 1
allowedAutorotateToLandscapeRight: 0            # was 1
allowedAutorotateToLandscapeLeft: 0             # was 1
```

`git diff ProjectSettings/ProjectSettings.asset` shows **only** those four lines changed — no collateral edits to unrelated settings. SPEC A1 called for `defaultScreenOrientation → Portrait` and the two landscape flags zeroed; the implementer additionally zeroed `PortraitUpsideDown`, which is explicitly permitted by SPEC ("PortraitUpsideDown optional"). Canonical screenshot confirms portrait boot renders cleanly with no errors.

### A2 — SafeAreaFitter — **PASS**

- `Assets/Scripts/UI/Core/SafeAreaFitter.cs` exists (2,878 bytes), fully additive: pure `MonoBehaviour` that reads `Screen.safeArea` and rewrites its own `RectTransform` anchors. Reads no other project types, subscribes to no events, mutates no external state.
- `[ExecuteAlways]` + `[RequireComponent(typeof(RectTransform))]` + `[AddComponentMenu(...)]` — correct authoring for an editable-in-Editor safe-area component.
- Namespace `GolfinRedux.UI.Core`, sensible defaults (poll-on-change guarded by `_lastSafeArea`/`_lastOrientation`, zero-screen guard for pre-first-frame Editor).
- `.cs.meta` present alongside `.cs` with a real GUID `3380b2d48492d427b8989e759e3ac5f6` (Rule R / Lesson R sister rule satisfied).
- Folder meta `Assets/Scripts/UI/Core.meta` also created (GUID `e433234232d754a8aa992afaa9a4b725`).
- **Correctly left UNATTACHED** to any canvas — SPEC A2 explicitly defers the attachment/full inset pass to Cesar / Order 930. Not attaching is the right call, not a defect.
- Compile-verified: report cites `typeof(GolfinRedux.UI.Core.SafeAreaFitter)` resolved via script-execute reflection, 4 private fields enumerated. Console clean.
- Did **not** mutate any existing UI, canvas, prefab, or scene (git diff confirms no scene files touched).

### A3 — iOS Quality tier — **PASS (report-only, no change needed)**

Read `ProjectSettings/QualitySettings.asset` directly:

- `m_PerPlatformDefaultQuality:` block sets `iPhone: 0` → iOS defaults to quality-tier index 0.
- Tier 0's `customRenderPipeline` = `{fileID: 11400000, guid: 5e6cbd92db86f4b18aec3ed561671858, type: 2}`.
- `Assets/Settings/Mobile_RPAsset.asset.meta` GUID = `5e6cbd92db86f4b18aec3ed561671858` → **exact match**. iOS default quality points at Mobile_RPAsset, not PC_RPAsset.
- Corroborating signal: tier 1's `customRenderPipeline` GUID = `4b83569d67af61e458304325a23e5dfd` = `PC_RPAsset.asset.meta`, and that tier lists `excludedTargetPlatforms: - iPhone` — a second guarantee iOS never lands on the PC pipeline.

No change was required, and the report correctly did not change it. Finding is substantiated by real file reads.

### A4 / A5 — **PASS (deferred per SPEC)**

- A4 (MapViewCaptureDriver `#if UNITY_EDITOR` gate): deferred. SPEC §2.A4 explicitly says "Defer if it risks touching the MapViewController public surface — do NOT destabilise map view for a cleanup." No edits under `Assets/Scripts/Gameplay/UI/ShotUI/` — confirmed via `git diff --stat`. Not shipping today isn't a blocker because the driver has no runtime instantiator (SPEC-verified).
- A5 (MapViewController retail invariant-dump flag): deferred. SPEC §2.A5 explicitly says "skip if time-boxed." Path is `persistentDataPath` (writable on iOS) with try/catch and `Directory.Exists` guard — safe on device even undeferred. No edit needed for smoke test.

Neither deferral destabilises MapViewController or Physics — no code files under those trees were touched.

---

## Cross-cutting checks

### Scene-mutation audit (Step 7)

`git status --porcelain --untracked-files=all` + `git diff --stat HEAD`:

**Production code / assets modified this task:**
- `ProjectSettings/ProjectSettings.asset` — 8-line diff, exclusively the 4 orientation fields (A1). No other keys touched.

**Production code created this task:**
- `Assets/Scripts/UI/Core.meta` (folder meta, auto-generated by Unity)
- `Assets/Scripts/UI/Core/SafeAreaFitter.cs` (+ `.cs.meta`)

**Task folder (correctly scoped):**
- `Docs/Specs/Active/phone_build_smoke_test/HEARTBEAT.log`
- `Docs/Specs/Active/phone_build_smoke_test/IMPLEMENTER_REPORT.md`
- `Docs/Specs/Active/phone_build_smoke_test/STATUS.md`
- `Docs/Specs/Active/phone_build_smoke_test/screenshots/portrait_boot_2026-07-27_07-59-19.jpg`
- (this file: `SELF_REVIEW.md`)

**Pre-existing dirty paths NOT introduced by this task** — all match the pre-task baseline in the `gitStatus` snapshot Cesar's harness shows at kickoff, and every one is reported in IMPLEMENTER_REPORT.md's "Pre-existing dirty files" section per Rule 13:

- `Assets/Art/ResultScreen/Button - Retry.png` + `.meta`
- `Assets/Art/RosterScreen/ButtonCancel.png.meta`
- `Assets/Art/Shop/Background - Blurred.png`
- `Assets/Art/SplashScreen/Green Button.png.meta`
- `Assets/Plugins/NuGet/.nuget-installed.json`, `McpPlugin.Common.dll`, `McpPlugin.dll`, `ReflectorNet.dll`
- `Docs/KICKOFF_TOMORROW.md`
- `Packages/manifest.json`, `Packages/packages-lock.json`
- `.mcp.json.bak-23886`

**No scene files were touched.** `git diff --stat HEAD` shows zero `.unity` files in the diff. `Assets/Scripts/Physics/` untouched. `LabScaffold.unity` untouched. `M_Splash*.mat` untouched. Standing bans (PIPELINE_HARDENING Rule 7) all clean.

### Capture-helper compliance (Step 5)

- Screenshot generated via `CaptureCore` RT reflection path (per report Console output: `[CaptureCore] Using RT reflection path (GameView RenderTexture)` and `[ScreenshotTool] Compressed to Assets/Screenshots/screenshot_...jpg`). This is a sanctioned CaptureHelper/CaptureCore path — no banned `ScreenCapture.CaptureScreenshot` used.
- Task adds no new `*Context.cs` under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`, so `CaptureHelper` maintenance-protocol extensions do not apply.

### Bbox check (Step 6)

N/A — no containment claim ("X is inside Y") is made by the report. The A1 claim is orientation-flag equality (verified by direct file read); A2 claim is file existence + compile (verified by ls + reflection reference in report); A3 claim is GUID equality (verified by cross-reading the two meta files). All are deterministic non-visual assertions, no bbox math needed.

### Report integrity

Every PASS row in IMPLEMENTER_REPORT.md § Acceptance checklist is backed by a real, verifiable tool result:
- A1 rows → backed by grep output that I reproduced against the on-disk file.
- A2 rows → backed by `ls` (file+meta on disk), `git status` (untracked), and a Type reflection call.
- A3 row → backed by concrete YAML values + GUID comparison I re-verified.
- A4/A5 deferrals → cite specific SPEC clauses that permit deferral.
- No implementer-graded PARTIAL / "subtle but present" / uncertainty — every checklist item is a firm PASS or a SPEC-permitted DEFER. No basis to invoke the "PARTIAL → default FAIL" rule.
- No fabricated tool output detected.

### Rule 21 (UI fidelity linter)

N/A — this task creates zero UI prefabs and touches zero canvases. Linter would have nothing to lint.

---

## Verdict rationale

Every required Phase A item (A1, A2, A3) is verified against ground truth and passes. Deferrals of A4/A5 are explicitly permitted by SPEC §2. The production diff is minimal, scoped, and surgical: one 4-line YAML change in ProjectSettings plus one additive MonoBehaviour + metas. No scenes, no Physics, no MapView, no existing UI touched. Screenshot proves the app still boots cleanly in portrait after the orientation flip. No fabricated evidence.

This is the correct outcome for an iOS build preflight of this scope — hand-off to the reviewer / architect gate for the second-pass check.

---

## Files reviewed

| Path | Purpose |
|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/phone_build_smoke_test/SPEC.md` | Contract |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/phone_build_smoke_test/IMPLEMENTER_REPORT.md` | Implementer claims |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/phone_build_smoke_test/screenshots/portrait_boot_2026-07-27_07-59-19.jpg` | Canonical screenshot (Step 1 pixel scan) |
| `/Users/cesar/Documents/GolfinRedux/ProjectSettings/ProjectSettings.asset` | A1 verification |
| `/Users/cesar/Documents/GolfinRedux/ProjectSettings/QualitySettings.asset` | A3 verification |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/UI/Core/SafeAreaFitter.cs` | A2 code inspection |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/UI/Core/SafeAreaFitter.cs.meta` | A2 meta present |
| `/Users/cesar/Documents/GolfinRedux/Assets/Settings/Mobile_RPAsset.asset.meta` | A3 GUID cross-check |
| `/Users/cesar/Documents/GolfinRedux/Assets/Settings/PC_RPAsset.asset.meta` | A3 GUID cross-check |
