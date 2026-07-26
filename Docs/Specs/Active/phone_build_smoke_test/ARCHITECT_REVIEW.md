# Architect Review — `phone_build_smoke_test` (iter-1)

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-07-27 08:22 JST
**Verdict:** **PASS** → hand to red-team
**STATUS set to:** `READY_FOR_REDTEAM`

Task class: **iOS build preflight** (Player Settings + one additive MonoBehaviour). Not a Figma/UI-fidelity task, not a mesh/terrain task. Rules 16 (mesh metrics), 18 (Figma fidelity), 19 (clone provenance), 21 (UI fidelity lint) do NOT apply and are correctly absent. Judged against SPEC §2 (Phase A) and §6 (Implementer scope).

---

## Independent visual scan (Step 0, before reading any report)

Canonical: `screenshots/portrait_boot_2026-07-27_07-59-19.jpg` (800×1731, long edge ≥ 900 → Rule 14 satisfied). Aspect ~1:2.16 = iPhone-14 portrait. Top-left: white "GOLFIN presents" wordmark with navy-and-gold shield containing a white golf ball on a tee, and a script "The Invitational" flourish across the right. Center: a full-body golfer illustration mid-follow-through in a white "POWER" red-billed cap and dark trousers, spraying tan bunker sand to the left; blue sky top-left fading to a green fairway with a red pinflag on the right edge. Bottom third: turf-green foreground, a green rounded "PLAY" button in white block caps, then "CREATE ACCOUNT" and "LOGIN" in white text below. No magenta / missing-shader boxes, no clipped UI, no rotation artefacts, no error dialogs — the ShellScene title screen rendering cleanly in portrait. This is exactly the artefact A1 needs to prove: the app still boots after the orientation lock change and lands in the correct orientation.

(No Figma reference to A/B against — correct for an iOS preflight task with no Figma node in SPEC.)

---

## Re-verified acceptance (Rule 5 — full re-run, not "carried forward")

### A1 — Portrait lock — PASS

`git diff ProjectSettings/ProjectSettings.asset` output (independently re-run this pass):

```
-  defaultScreenOrientation: 4
+  defaultScreenOrientation: 0
...
   allowedAutorotateToPortrait: 1
-  allowedAutorotateToPortraitUpsideDown: 1
-  allowedAutorotateToLandscapeRight: 1
-  allowedAutorotateToLandscapeLeft: 1
+  allowedAutorotateToPortraitUpsideDown: 0
+  allowedAutorotateToLandscapeRight: 0
+  allowedAutorotateToLandscapeLeft: 0
```

Exactly the four orientation lines the SPEC calls for. `defaultScreenOrientation: 0` = Portrait (was 4 = LandscapeRight); `allowedAutorotateToPortrait: 1` retained; the three non-portrait autorotate flags all zeroed. SPEC A1 requires LandscapeLeft+LandscapeRight → 0 and Portrait retained; zeroing PortraitUpsideDown is SPEC-explicitly-permitted ("PortraitUpsideDown optional"). **No collateral edits** — the diff hunk contains only these four fields; every other Player Setting is byte-identical. Boot screenshot corroborates that the app renders in portrait post-change.

### A2 — SafeAreaFitter — PASS

Read the file directly (73 lines, 2878 bytes at `Assets/Scripts/UI/Core/SafeAreaFitter.cs`):

- Namespace `GolfinRedux.UI.Core`; pure `MonoBehaviour` with only `using UnityEngine;`.
- `[ExecuteAlways]`, `[RequireComponent(typeof(RectTransform))]`, `[AddComponentMenu("GolfinRedux/UI/Safe Area Fitter")]` — correct authoring.
- `Apply()` reads `Screen.safeArea`, converts to normalised anchors, and writes ONLY its own `_rectTransform.anchor{Min,Max}` + zeroed offsets. Zero external state mutated. Zero events/singletons touched. Guarded for `Screen.width/height == 0` (pre-first-frame editor case). Update re-applies only on `safeArea`/`orientation` change.
- `.cs.meta` present with GUID `3380b2d48492d427b8989e759e3ac5f6` (Lesson R satisfied).
- Folder meta `Assets/Scripts/UI/Core.meta` present with GUID `e433234232d754a8aa992afaa9a4b725`.
- **Correctly UNATTACHED** to any production canvas — SPEC A2 explicitly defers the attachment/full inset pass to Cesar/Order 930. No scene mutations elsewhere corroborates this.
- Compile is verified by the report via `typeof(GolfinRedux.UI.Core.SafeAreaFitter)` reflection resolving with 4 private fields; the class body I read is syntactically valid C# with no unresolved symbols.

### A3 — iOS Quality tier → Mobile_RPAsset — PASS (report-only)

Cross-verified directly:

- `ProjectSettings/QualitySettings.asset` line 134: `iPhone: 0` (iOS default quality tier index 0).
- Tier 0 is named `Mobile` (line 10). Its `customRenderPipeline` (line 51) GUID = `5e6cbd92db86f4b18aec3ed561671858`.
- `Assets/Settings/Mobile_RPAsset.asset.meta` GUID = `5e6cbd92db86f4b18aec3ed561671858` → **exact match**. iOS default resolves to Mobile_RPAsset.
- Corroborating negative signal: tier 1 (`PC`) `customRenderPipeline` GUID = `4b83569d67af61e458304325a23e5dfd` = `PC_RPAsset.asset.meta`, and tier 1's `excludedTargetPlatforms` explicitly lists `- iPhone` (line 117) — a second layer of protection against iOS falling into the PC tier.

No change was required, and the implementer correctly did not make one.

### A4 — MapViewCaptureDriver `#if UNITY_EDITOR` gate — PASS (deferred per SPEC)

SPEC §2.A4 explicitly permits deferral: *"Defer if it risks touching the MapViewController public surface it reads — do NOT destabilise map view for a cleanup."* Verified no edits under `Assets/Scripts/Gameplay/UI/ShotUI/` via `git diff --stat HEAD` (empty for that path). Driver has no runtime instantiator per SPEC §5 verification, so it cannot fire during the smoke test. Deferral is correct.

### A5 — MapViewController retail invariant-dump flag — PASS (deferred per SPEC)

SPEC §2.A5 explicitly permits skip ("skip if time-boxed"). Path is `persistentDataPath` (writable on iOS) with try/catch + `Directory.Exists` guard — safe on device even without gating. No edit needed for smoke test.

---

## Cross-cutting audits

### Scene-mutation / drift audit (Rule 5 / Rule 13)

`git status --porcelain --untracked-files=all` (re-run this pass):

**Introduced by this task (matches IMPLEMENTER_REPORT):**
- `M ProjectSettings/ProjectSettings.asset` — the four A1 orientation lines only (verified via `git diff`).
- `?? Assets/Scripts/UI/Core.meta` — folder meta, GUID `e433234232d7...`.
- `?? Assets/Scripts/UI/Core/SafeAreaFitter.cs` (+ `.cs.meta`) — A2 additive script.
- `?? Docs/Specs/Active/phone_build_smoke_test/{HEARTBEAT.log, IMPLEMENTER_REPORT.md, SELF_REVIEW.md, STATUS.md, screenshots/portrait_boot_2026-07-27_07-59-19.jpg}` — task-folder scoped.

**Pre-existing baseline drift (declared in report, matches the top-of-conversation `gitStatus` snapshot exactly, Rule 13 satisfied):**
- `M Assets/Art/ResultScreen/Button - Retry.png{,.meta}`
- `M Assets/Art/RosterScreen/ButtonCancel.png.meta`
- `M Assets/Art/Shop/Background - Blurred.png`
- `M Assets/Art/SplashScreen/Green Button.png.meta`
- `M Assets/Plugins/NuGet/{.nuget-installed.json, McpPlugin.Common.dll, McpPlugin.dll, ReflectorNet.dll}`
- `M Docs/KICKOFF_TOMORROW.md`
- `M Packages/{manifest.json, packages-lock.json}`
- `?? .mcp.json.bak-23886`

Every path outside `Docs/Specs/Active/phone_build_smoke_test/` is either a declared A1/A2 asset or a documented pre-existing baseline entry — no undeclared drift.

**No scene diffs.** `git diff --stat HEAD -- '*.unity' '*.mat'` returns empty. `LabScaffold.unity` untouched. `M_Splash*.mat` untouched. Standing bans (PIPELINE_HARDENING Rule 7) all clean.

**No Physics diffs.** `git diff HEAD -- Assets/Scripts/Physics/` returns empty. `Scenarios.cs` untouched.

### Capture-helper compliance

Report cites `CaptureCore RT reflection path` + `ScreenshotTool` compression — sanctioned CaptureHelper/CaptureCore family, no banned `ScreenCapture.CaptureScreenshot`. Task adds no new `*Context` under `Gameplay/UI/ShotUI/HUD/`, so CaptureHelper Maintenance-protocol extensions do not apply.

### Bbox / containment claims

N/A — this task makes no "X inside Y" claim. All assertions are equality checks on YAML fields, file existence, and GUID matches — deterministic non-visual, so no bbox math needed.

### Report integrity

Every PASS row is backed by evidence I independently re-verified:
- A1 rows → git diff hunk shows exactly the claimed 4-line change.
- A2 rows → file on disk + meta on disk + code matches the described behavior.
- A3 row → QualitySettings.asset line 134 (`iPhone: 0`) + line 51 GUID match to Mobile_RPAsset.asset.meta line 2 GUID.
- A4/A5 → cite explicit SPEC clauses that permit deferral; git diff confirms nothing was touched in those areas.
- No PARTIAL / "subtle but present" / uncertainty in the report — no basis to invoke the PARTIAL → FAIL default (Lesson 2026-05-13).
- No fabricated tool output detected.

---

## Verdict rationale

Every required Phase A item (A1, A2, A3) is verified against ground truth on disk this pass and passes. A4/A5 deferrals are explicitly permitted by SPEC §2/§6. Production diff is minimal, scoped, surgical: one 4-line YAML change in `ProjectSettings.asset` (orientation flags only), one additive `MonoBehaviour` + its `.cs.meta`, and one auto-generated folder meta. No scenes, no Physics, no MapView, no existing UI, no material touched. Screenshot proves the app boots cleanly in portrait after the change. No fabricated evidence, no undeclared drift.

Hand-off to `golfin-redteam-reviewer` for the adversarial gate.

---

## Files reviewed

| Path | Purpose |
|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/phone_build_smoke_test/SPEC.md` | Contract |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/phone_build_smoke_test/IMPLEMENTER_REPORT.md` | Implementer claims |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/phone_build_smoke_test/SELF_REVIEW.md` | Self-reviewer verdict |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/phone_build_smoke_test/screenshots/portrait_boot_2026-07-27_07-59-19.jpg` | Canonical (Step 0 pixel scan) |
| `/Users/cesar/Documents/GolfinRedux/ProjectSettings/ProjectSettings.asset` (+ git diff) | A1 verification |
| `/Users/cesar/Documents/GolfinRedux/ProjectSettings/QualitySettings.asset` | A3 verification |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/UI/Core/SafeAreaFitter.cs` | A2 code inspection |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/UI/Core/SafeAreaFitter.cs.meta` | A2 meta present |
| `/Users/cesar/Documents/GolfinRedux/Assets/Settings/Mobile_RPAsset.asset.meta` | A3 GUID cross-check |
| `/Users/cesar/Documents/GolfinRedux/Assets/Settings/PC_RPAsset.asset.meta` | A3 GUID cross-check |

---

# RED-TEAM REVIEW (adversarial gate)

**Reviewer:** golfin-redteam-reviewer
**Timestamp:** 2026-07-27 08:12 JST
**Verdict:** **ARCHITECT_REVIEW_PASS** — tried hard to break it; could not.

Task class: iOS build preflight (Player Settings + one additive MonoBehaviour). No Figma node, no mesh — Rules 16/18/19/21 correctly N/A. No `CESAR_REJECTION.md` in folder (first iteration) → Step-1 rejection-replay N/A.

## Independent evidence I generated (not carried forward)

**A1 — Portrait lock — GONE/CORRECT.** Re-ran `git diff ProjectSettings/ProjectSettings.asset` myself. The diff contains exactly two hunks, four changed lines, all orientation:
- `defaultScreenOrientation: 4 → 0`. Unity `UIOrientation` enum (this field's type): 0=Portrait, 1=PortraitUpsideDown, 2=LandscapeRight, 3=LandscapeLeft, 4=AutoRotation. **0 = Portrait — correct.** (Note: implementer + first-reviewer prose glosses the old `4` as "LandscapeRight"; it is actually AutoRotation. Cosmetic annotation error of the OLD value only — the delivered new value 0=Portrait is exactly what SPEC A1 wants. Not a blocker, not a fabricated tool result. Worth correcting in prose.)
- `allowedAutorotateToPortrait: 1` retained; `PortraitUpsideDown/LandscapeRight/LandscapeLeft` all `1 → 0`. Exactly SPEC A1 (LandscapeL+R→0, Portrait kept, PUD-optional-zeroed). **Zero collateral** in the 6000-line asset — the two hunks are the only change.

**A2 — SafeAreaFitter — CLEAN.** Read all 73 lines myself. Insets ONLY its own RectTransform (anchorMin/Max + offsetMin/Max = 0); mutates zero external state; guards `Screen.width/height == 0` (no div-by-zero) and `_rectTransform == null`; re-applies only on safeArea/orientation change (no layout thrash). `.cs.meta` GUID `3380b2d48492d427b8989e759e3ac5f6` valid.
- **Unattached — verified by me:** `grep -rl 3380b2d48492d427b8989e759e3ac5f6` across all `*.unity/*.prefab/*.asset` → **zero hits.** No undeclared scene mutation.
- **Compiles — verified independently of the report:** project is **Unity 6000.3.9f1**, where `ScreenOrientation.AutoRotation` is a valid enum member. Editor.log tail shows no `error CS`. The implementer's live-domain `typeof(GolfinRedux.UI.Core.SafeAreaFitter)` resolution is dispositive — a type cannot resolve via reflection in a running Unity domain unless its assembly compiled. Code is trivially valid C# (only `using UnityEngine;`, all symbols in-namespace).

**A3 — iOS Quality tier → Mobile_RPAsset — CORRECT.** Resolved the GUID chain myself end-to-end:
- `QualitySettings.asset` `iPhone: 0` → tier 0 `name: Mobile` → `customRenderPipeline` guid `5e6cbd92db86f4b18aec3ed561671858`.
- `Mobile_RPAsset.asset.meta` guid = `5e6cbd92db86f4b18aec3ed561671858` → **exact match.**
- Corroboration: tier 1 `PC` guid `4b83569d67af61e458304325a23e5dfd` = `PC_RPAsset.asset.meta`, and PC tier `excludedTargetPlatforms: - iPhone`. iOS cannot fall into PC. No change needed — correctly none made.

**Drift — CLEAN.** `git status` production paths = {`ProjectSettings.asset` (orientation only), `Assets/Scripts/UI/Core.meta`, `SafeAreaFitter.cs`+`.meta`}. All declared. Pre-existing baseline matches the kickoff snapshot exactly. `git diff --stat HEAD -- '*.unity' '*.mat'` empty; `-- Assets/Scripts/Physics/` empty; `Scenarios.cs` untouched. No bespoke `*Gate` scenario (N/A — not a capture task).

**Screenshot — REAL.** `portrait_boot_2026-07-27_07-59-19.jpg`, 800×1731 (long edge 1731 ≥ 900, Rule 14). Pixel-scanned before reading narrative: high-variance genuine boot frame — GOLFIN/The Invitational title, full-body golfer mid-bunker-swing, PLAY/CREATE ACCOUNT/LOGIN, portrait aspect, no magenta/missing-shader, no rotation artefact. Not a fabricated flat fill.

## Three break-attempts (all failed)

1. **Visual:** hunted the boot frame for clipped/rotated/magenta UI at full size — none; clean portrait render.
2. **Config/geometric:** looked for threshold-adjacent fragility or a mis-mapped enum/GUID — A1 diff is surgical (4 lines), A3 GUID is an *exact* string match with a second PC-exclusion guard, no drift. Nothing marginal.
3. **Spec-intent:** SPEC A1/A2/A3 all delivered as scoped; A4/A5 explicitly deferrable per §2/§6; unattached SafeAreaFitter is the SPEC-sanctioned "Cesar decides attachment" path, correctly surfaced. Compile risk (AutoRotation-removed-in-newer-Unity) chased down and disproven for Unity 6.3. Could not find a missed intent.

**Only finding:** cosmetic prose slip ("was 4 = LandscapeRight" → should read "= AutoRotation") in the implementer report and first review. Does not affect the correct delivered value. Not FAIL-worthy.
