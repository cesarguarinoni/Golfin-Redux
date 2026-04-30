# Architect Review — `8_5_b_lab_inventory_seeder`

**Reviewer:** golfin-architect
**Date:** 2026-04-30 JST
**Verdict:** **PASS**
**Screenshot reviewed:** `screenshots/playmode_main_08-33-38.png`

---

## Cross-cutting checks

### Architectural soundness

- **Asmdef boundary respected.** Implementer correctly identified that `Golfin.Physics.Viewer` cannot reference Assembly-CSharp types (`BagManager`, `BallManager`, `ClubDatabaseCSV`, `BallDatabaseCSV`, `Golfin.Inventory.*`) without inverting the dependency graph. Placing `LabInventoryStub.cs` in Assembly-CSharp under `Assets/Scripts/UI/HUD/` (namespace `Golfin.UI.HUD`) mirrors the existing `ClubContextPopulator` / `BallContextPopulator` pattern. This is the right call and matches Blueprint guidance on asmdef hygiene.
- **Reuse over duplication.** `MapClubTypeToLabIndex` mirrors the populator's mapping intentionally (the populator path is bypassed in lab mode, so the seeder must own the mapping). Acceptable duplication given the short table; if a third copy appears, extract to a helper.
- **Singleton checks tightened.** Replacing the spec's reflection-based `TryGetSingleton` with direct `BagManager.Instance != null` / `BallManager.Instance != null` is a strict improvement — type-safe, no string lookups, and the asmdef already references those types.
- **Event ordering correct.** `RaiseSelectedChanged()` before `RaiseBagChanged()` gives downstream UI a coherent snapshot when bag binding triggers a re-read.

### Visual fidelity

Per spec scope ("This spec only seeds the data… don't try to fix the selector layout in this spec"), no Figma reference applies. The relevant visual checks are the populated action buttons, both confirmed in the screenshot:

- DRIVER button (bottom-right): club silhouette sprite + "DRIVER 250 yrds" — correct.
- GOLFIN button (bottom-left): green-and-white ball thumbnail + "GOLFIN" + ∞ quantity — correct.
- No white-box placeholders on any of the four action buttons.

### Spec adherence in spirit

The intent — "give the lab real inventory entries so the selector has cards to render" — is met. The two deviations (file path, removed reflection) are documented and well-reasoned. Wood→Driver-slot mapping preserved per spec § "Wood club mapping". Lab-mode detection guard is in place so this code is provably inert in real gameplay.

### Latent issues

- **DontDestroyOnLoad warning** on ClubDatabaseCSV (it's a child of LabRoot, not a scene-root). Cosmetic only — singleton still initializes correctly. Worth promoting the GO to a scene-root sibling in a follow-up scene polish, but not a blocker.
- **`s_TestClubIds` is hardcoded.** Acceptable for a lab stub, but if the consolidated CSV's IDs change in 8.5.A's wake (unlikely but possible during follow-up tuning), a "skipped" warning will surface in the console. The existing `LogWarning` covers this gracefully.
- **Putter→LabClubIndex=3** but the spec notes `LabClubs[]` has 4 slots `0..3`. Confirmed in-bounds; no risk.

### Capture-helper compliance (Step 5 backstop)

- **Screenshot provenance:** filename prefix `playmode_main_*.png` matches the `CaptureHelper.SnapGameView` output convention; no evidence of the banned `ScreenCapture.CaptureScreenshot(path)`. Implementer's source file does not call any capture API. Compliant.
- **Maintenance protocol:** This task does not introduce a new static-bus context — `ClubContext` and `BallContext` already exist; the stub only writes to them. Therefore no `CaptureHelper.FakeMidAim` / `FakeReset` extension is required. Compliant.

The self-reviewer's Step 5 finding ("Compliant") is correct. No backstop trigger.

---

## Per-section verdicts

| Section | Verdict |
|---|---|
| Code | PASS |
| Scene | PASS |
| Runtime (play mode) | PASS |
| Visual (play mode) | PASS |
| Lab integration | PASS (data routing verified; live shot-physics test deferred to manual play, acceptable per spec) |
| Capture-helper compliance | PASS |

---

## Decision

**ARCHITECT_REVIEW_PASS.** Ready for Cesar's final approval. No fail items.

---

## Files touched in this review

| File | Action |
|---|---|
| `C:\Users\cesar\GolfinRedux\Docs\Specs\Active\8_5_b_lab_inventory_seeder\ARCHITECT_REVIEW.md` | Created — this verdict |
| `C:\Users\cesar\GolfinRedux\Docs\Specs\Active\8_5_b_lab_inventory_seeder\STATUS.md` | Updated → `ARCHITECT_REVIEW_PASS` |
