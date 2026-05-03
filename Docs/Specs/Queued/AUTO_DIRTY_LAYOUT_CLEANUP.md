# Auto-Dirty Layout Cleanup — Flow Optimization Spec

**Status:** Queued
**Created:** 2026-05-03 JST
**Owner:** Claude Code (Tier 2 TellCode task — diagnosis-first, then mechanical pass)
**Estimate:** S–M (half-day diagnosis, 1–2 days fix + visual verification)
**Phase:** 00. Foundations
**Roadmap section:** Tooling & workflow

---

## Why this exists

Code's pipeline is repeatedly blocked by the Unity "Open Scenes have been modified externally" / "Reload?" modal. Root cause investigation (2026-05-03 chat log) established:

1. Modal fires when an external file write hits a `.unity` file while Unity has the in-memory scene marked dirty.
2. The clean-scene approach (call `EditorSceneManager.SaveOpenScenes()` before any Code write) only works if scenes *stay* clean for at least the duration of the write.
3. **Verified bug present in the project:** `LabScaffold.unity` and `ShellScene.unity` contain multiple GameObjects with both `ContentSizeFitter` AND a `LayoutGroup` (Vertical / Horizontal / Grid) on the same GameObject. This combination triggers the long-standing Unity bug where the scene re-marks itself dirty within ~1 second of saving, even with no user action.
4. As long as those objects exist, no save-before-write strategy can eliminate the modal.

Reference: Unity issue tracker — *"Scene is instantly marked dirty when opened if any object is affected by both a ContentSizeFitter and VerticalLayoutGroup"* (Won't Fix by Unity).

**Confirmed instance** — `LabScaffold.unity` lines 1015–1049: GameObject `&51746023` has `ContentSizeFitter` (VerticalFit=PreferredSize) and `VerticalLayoutGroup` on the same node. Several similar pairs in `ShellScene.unity` based on line proximity (full audit is part of Step 1).

---

## Goal

Eliminate every `ContentSizeFitter + LayoutGroup` co-located pair across all scenes and prefabs **without changing any rendered layout**. After the cleanup, scenes should stay clean (no `*` in title bar) for at least 5 seconds after a save with no user interaction.

---

## Out of scope

- The editor-side auto-save helper (handshake script for Code) — separate follow-up spec, only worth building once this cleanup is done.
- Other modal sources (Reload Scripts, Importing Assets) — those are settings-side, not asset-side.
- Restructuring layouts for design reasons. **This task must be visually invariant.**

---

## Step 1 — Audit (diagnosis, no edits)

Produce `Docs/Diagnostics/AUTO_DIRTY_AUDIT.md` listing every offender. Scan every `.unity` and `.prefab` under `Assets/` (including `Assets/Resources/`, `Assets/UI/`, `Assets/Scenes/`, `Assets/Prefabs/`). Skip `Assets/Plugins/`, `Library/`, `Packages/`, `Docs/Backups/`.

For each match, record:

- File path and line number of the `ContentSizeFitter` block
- Line number of the co-located `LayoutGroup` (Vertical / Horizontal / Grid)
- The shared `m_GameObject: {fileID: …}` value (proves they're on the same GameObject)
- The GameObject's name (resolve by following the fileID back to its `GameObject` block and reading `m_Name`)
- The `ContentSizeFitter` settings: `m_HorizontalFit` and `m_VerticalFit` (0=Unconstrained, 1=MinSize, 2=PreferredSize)
- Whether the GameObject has children (children count > 0 means the LayoutGroup is doing real work; children count = 0 means it's likely vestigial)
- Parent's component list — specifically does the parent also have a LayoutGroup with `ChildForceExpandWidth/Height` set? (If yes, the ContentSizeFitter on this child is almost always redundant.)

**Detection method:** YAML parse, not regex line-distance. Build a fileID → component map per file, then for each GameObject collect its components; flag any GameObject whose component set contains BOTH a ContentSizeFitter AND any LayoutGroup. Line-proximity scanning was used during initial discovery but produces false positives — the audit must be GameObject-accurate.

**Output format:** Markdown table grouped by file, plus a summary count at the top (total offenders, breakdown by LayoutGroup type, breakdown by parent context).

**Stop point:** After audit lands, hand back to Cesar. Do not start Step 2 until Cesar approves the fix list. Each offender will get a fix classification (see Step 2 below) — Cesar may want to spot-check a few before greenlighting the full pass.

---

## Step 2 — Fix classification (still no edits)

For each offender in the audit, append a recommended fix from this menu:

**Fix A — Remove ContentSizeFitter (preferred when safe).**
The parent has a LayoutGroup with `ChildForceExpandWidth: 1` and/or `ChildForceExpandHeight: 1` matching the ContentSizeFitter's axes. The parent is already sizing this child. The ContentSizeFitter is redundant and removing it changes nothing visually.

**Fix B — Remove LayoutGroup (preferred when no children).**
The GameObject has 0 children. A LayoutGroup with no children does nothing. Removing it changes nothing visually.

**Fix C — Insert child wrapper (when both A and B are unsafe).**
The GameObject genuinely needs both: it has children laid out by the LayoutGroup AND its own size needs to fit content. Restructure: keep the LayoutGroup on the current GameObject, create a child "ContentWrapper" GameObject that owns the ContentSizeFitter and re-parents the children. This requires hand-verification.

**Fix D — Defer.**
Rare but possible: the layout is genuinely doing what only the buggy combo achieves (e.g., a TextMeshPro that needs both auto-fit and child-spacing). Document why and leave alone. These remain in `Docs/Pipeline/` as known offenders that can't be cleaned.

**Output:** Extend the audit table with a `Fix` column (A/B/C/D) and a one-line `Why` justification per row.

**Stop point:** Hand back. Cesar reviews and either approves the fix-classification pass or asks for re-classification on specific rows.

---

## Step 3 — Mechanical pass (Fix A + Fix B only)

These are safe and non-visual-invariant. Apply via direct YAML edit to `.unity` and `.prefab` files. **Do not open Unity during this step** — Unity is what causes the dirty-flag race in the first place. Pure YAML editing is safer.

For each Fix A row: locate the `MonoBehaviour` block whose `m_Script` GUID matches `ContentSizeFitter` and whose `m_GameObject` matches the offender's fileID. Delete the entire `--- !u!114 &<id>` block. Also remove the corresponding entry from the GameObject's `m_Component:` list (the ordered list of `componentType: {fileID: …}` references inside the GameObject block).

For each Fix B row: same procedure but for the `LayoutGroup` block.

**Verification per file:** After edits, run `git diff --stat` on each modified scene/prefab. Diff line count should be small (~10–30 lines per offender removed). If a single file shows a >200 line diff, abort that file and report — Unity may have re-serialized in an unexpected order.

---

## Step 4 — Visual verification (the "don't mess with layouts" gate)

For every scene touched: open in Unity, take a screenshot with `CaptureHelper`, compare to a pre-cleanup screenshot of the same scene. Use `screenshots/auto_dirty_audit/<scene_name>_before.png` and `_after.png`.

**Pass criteria:**
- No visible difference in any UI element position, size, or alignment
- No new console warnings on scene open
- After save and 5 seconds idle, no `*` reappears in the scene title bar

**For prefabs:** open in Prefab Mode, screenshot the Prefab Stage view at consistent zoom, compare. Same pass criteria minus the title-bar check (prefabs use a different dirty mechanism).

**If any Fix A or Fix B causes visible drift:**
- Revert that specific edit
- Reclassify as Fix C (needs wrapper) or Fix D (defer)
- Document why in the audit

**For Fix C rows:** these are NOT done in this task. Spin out a follow-up spec per Fix C row (or one combined spec if there are <5 of them). Each requires Architect spec because they change hierarchy.

---

## Step 5 — Validation

Run the full edit-mode test suite. **Pass gate: 198/198 EditMode PASS** (current baseline per TellCode.md). UI-only changes shouldn't affect any test, but verify.

Open `LabScaffold` and `ShellScene` (the two known-bad scenes). Save. Wait 10 seconds. Confirm no `*`. Switch focus away and back. Confirm no "Open Scenes have been modified externally" prompt.

Edit any `.cs` file from the OS (Code touches a sentinel file like `tasks/clean_scene_test.tmp` then deletes it). Switch back to Unity. Confirm no scene-modified prompt fires (script changes prompt is OK and governed by Recompile And Continue Playing).

---

## Step 6 — Report + close

Standard `IMPLEMENTER_REPORT.md` with:
- Audit summary (offenders found, fixes applied, fixes deferred to follow-up specs)
- Before/after screenshot pairs for each touched scene
- `git diff --stat` summary
- Test gate result
- The 10-second-idle clean-scene confirmation per scene
- List of Fix C rows that need Architect specs

Move spec to `Docs/Specs/Completed/AUTO_DIRTY_LAYOUT_CLEANUP/`. Update Roadmap.md status. Update Notion roadmap row.

---

## Risk notes

- **YAML editing is risky if not careful.** Recommend Code use a Python script with a real YAML parser (PyYAML with `unity_yaml_loader` shim — Unity uses non-standard YAML tags) rather than raw text manipulation. Document the script under `Docs/Scripts/`.
- **Prefab edits propagate.** Removing a component from a prefab affects every instance. Audit instance overrides before editing prefabs — if any instance has overridden the component being removed, the override needs to be cleared first or the component will linger as an orphan reference.
- **Source control safety.** Commit a `before` snapshot of every touched scene/prefab in a single commit before Step 3 starts. If anything goes wrong, revert the entire commit.
- **The bug's "Won't Fix" status is from old Unity versions.** Worth a quick check on current Unity version (2022 LTS as of project clone) — if Unity has silently fixed it in a recent patch, the cleanup is moot. Test: clone `LabScaffold` to a throwaway scene, save, watch for re-dirty. If clean, skip this whole spec and update TellCode flags.

---

## Why not just build the editor helper?

The editor-side `SaveOpenScenes()` handshake script was the alternative considered. It was rejected for now because:

- The auto-dirty bug means saved scenes don't stay saved long enough for the handshake to be reliable.
- A handshake is net-new infrastructure that needs maintenance forever.
- This cleanup is one-time and produces ancillary wins (smaller scene files, less version-control churn, removal of vestigial layout components).

Once this cleanup lands, the handshake may or may not be needed — re-evaluate then.
