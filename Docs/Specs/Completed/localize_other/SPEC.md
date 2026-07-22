# SPEC — `localize_other`

> **Authoritative spec.** Implementer reads this and ONLY this. STATUS.md tracks pipeline state.

## Status

`SPEC_READY`.

## Goal

**Batch 6 (final) of the localization sweep.** The audit's `Other` group is a catch-all (282 rows / 21 assets). Most of it is **not** safe/appropriate to convert in an automated batch: it is dominated by `ShellScene.unity` (213 rows — high scene-mutation risk, mostly already code-localized), plus dev/debug scenes, editor builders, and asmdef-gated gameplay code. This batch converts ONLY the small, low-risk, clearly-actionable slice, and **triages + defers the rest with documentation** (measure-first — the ShellScene inventory makes a future dedicated task speccable).

## In scope — CONVERT (low-risk)

1. **`Assets/Prefabs/UI/Modals/HoleCompleteModal.prefab`** — static labels via `LocalizedText` binder (verify each is static, not controller-written; this modal is a distinct asset from the batch-2 HoleCompleteWidget):
   - `SUCCESS` → reuse `RESULT_SUCCESS`; `FAILED` → reuse `RESULT_FAILED`; `RETRY` → reuse `RESULT_RETRY`; `PLAY` → reuse `BTN_START`; `MENU` → reuse `STAMINA_MENU` (EN="MENU", cross-context reuse of an identical-English key); `PLAY NEXT` → NEW key `RESULT_PLAY_NEXT`.
   - SKIP (dynamic): course/hole/par string, `TEE OFF: REGULAR\nSTROKES:…`, `x10` counts.
2. **`Assets/Prefabs/UI/Modals/Toast.prefab`** — `COURSE CLEARED!` → NEW key `TOAST_COURSE_CLEARED` (verify static; if the toast text is runtime-set per-event, SKIP and document).
3. **`Assets/Scripts/UI/LoadingScreenController.cs`** — its 1 static label → code-site `Get()` with a new key (verify: it's in `Assembly-CSharp`, so `LocalizationManager` is reachable — no asmdef change). If the label is a runtime/dynamic status string, SKIP and document.

Reuse-casing verified: `RESULT_SUCCESS`="SUCCESS", `RESULT_FAILED`="FAILED", `RESULT_RETRY`="RETRY", `BTN_START`="PLAY", `STAMINA_MENU`="MENU" — all EN-exact.

## Out of scope — DEFER (do NOT touch; document in `## Deferred` with the reason)

1. **`Assets/Scenes/ShellScene.unity` (213 rows) — DEFER to a dedicated `localize_shellscene` task.** Reasons: (a) editing the boot-critical main scene carries severe corruption risk (project scar tissue; multiple CLAUDE.md hard rules); (b) most ShellScene text is ALREADY code-localized by screen controllers (the Persistent/Home pilot proved HomeScreen's text was already `Get()`-driven), so the genuine scene-binder work is far smaller than 213 and needs per-screen controller analysis. **Deliverable for this batch:** a COARSE categorization of the 98 distinct ShellScene texts into buckets — `LIKELY_ALREADY_CODE_LOCALIZED` / `LIKELY_STATIC_NEEDS_SCENE_BINDER` / `LIKELY_DYNAMIC` — enough to scope the future task. Do NOT edit the scene. (You may read ShellScene YAML + grep controllers to categorize; no `scene-save`.)
2. **`Assets/Scripts/Gameplay/UI/ShotUI/FadeDrawButtonWidget.cs`, `MapViewController.cs`** — in the `Golfin.Gameplay.UI` asmdef; can't reach the global `LocalizationManager` without the asmdef-access decision (deferred sweep-wide since batch 2's reverted asmdef change). DEFER to the future "gameplay localization asmdef access" task. Do NOT restructure assemblies.

## Out of scope — SKIP (not shipping player UI; document briefly)

- **Dev/debug/test scenes:** `Assets/Scenes/Physics/LabScaffold.unity`, `ShotConeTest.unity`, `PhysicsLab_Hole1.unity`, `Assets/Scenes/Tests/CanvasScalerTest.unity` — physics/test scaffolding, not shipping. (Also: Physics scene edits are under a standing ban.)
- **Debug HUDs in the Physics asmdef:** `CameraModeDebugHUD.cs`, `PhysicsLabUI.cs` — debug-only + `Golfin.Physics.Viewer` asmdef + standing Physics-edit ban. SKIP.
- **All 9 editor/archive builders** (`Assets/Scripts/Editor/**`, `Assets/Scripts/**/Editor/**`, `Assets/Editor/Localization/LocalizationAudit.cs`, `Archive/*`) — edit-time scaffolding, not shipping code. SKIP.

## Recipe / JP policy / anti-fabrication (from batches 1–5b)

- Code-path-first; static prefab label → binder; controller-written label → SKIP or code-site `Get()`. Never bind a runtime-written label. Verify live-surface.
- Reused keys keep JP. New keys (`RESULT_PLAY_NEXT`, `TOAST_COURSE_CLEARED`, the loading key): EN exact + JP = EN + ` [JP-TODO]`. No invented Japanese. JP via Noto fallback.
- **Anti-fabrication:** EN/JP captures byte-distinct real play-mode captures; keep the screenshots folder clean (no stale/dup files); gates md5 + open JP. **Capture code-site (loading) conversions JP-FIRST.** `[JP-TODO]` overflow EXPECTED, not a FAIL.

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] **HoleCompleteModal + Toast + LoadingScreen** converted per the in-scope list; binders/Get() with correct keys (read-back/diffs); no binder on a controller-written label; live-surface cited.
- [ ] **Reuse-casing audit** for the 5 reused keys (EN-exact verdicts).
- [ ] **CSV:** new keys (`RESULT_PLAY_NEXT`, `TOAST_COURSE_CLEARED`, loading key) EN-exact + `[JP-TODO]`; reused pre-existing; no dup; importer re-run; count reported.
- [ ] **`## Deferred` section:** ShellScene (with the coarse 98-text categorization) + the 2 gameplay-asmdef files, each with the reason.
- [ ] **`## Skipped` section:** dev/debug/test scenes, debug HUDs, 9 builders — briefly.
- [ ] **EN + JP captures** (byte-distinct, real): the HoleComplete modal (SUCCESS + FAILED states if reachable), the Toast (if reachable), the loading screen (if reachable, JP-first). If a surface is genuinely unreachable in play mode, document honestly — do NOT fabricate.
- [ ] **Scope:** `git status` shows only HoleCompleteModal.prefab, Toast.prefab, LoadingScreenController.cs, CSV, table (+ task folder). **NO `.unity` scene mutation** (ShellScene must be UNCHANGED — this is a hard gate; quote `git status` proving no `.unity` is modified), NO Physics edit, NO asmdef, NO editor-builder. Quote it.
- [ ] Compiles clean; HEARTBEAT baseline. Spec deviations flagged.

## Not a Figma task

No Figma node — Rules 16/17/18/21 N/A. Visual gate: EN unchanged, JP renders translated/placeholder (never a raw key), no layout shift.

## Out of scope / Deferred (summary)

ShellScene binders (dedicated task), gameplay-asmdef strings (asmdef-access task), dev/debug/test scenes, debug HUDs, editor builders, inventing Japanese, asmdef changes, `Assets/Scripts/Physics/`, any scene mutation, `M_Splash*.mat`.

---
