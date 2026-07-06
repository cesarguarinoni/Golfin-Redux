# Implementer Report — `pipeline_verification_gates` (Order 611)

**Iteration shape:** pipeline-hooks:yaml-provenance-verifier

## Implementation summary

Mechanized §9–§12 of `PIPELINE_HARDENING.md` into `enforce_implementer_done.py` so the impl→review
transition is gated on engine/YAML-reported facts, never implementer-authored prose. Phase 1 adds a
pure-Python YAML verifier (P1) that reads Unity prefab YAML to check PrefabInstance lineage and
`Image.m_Sprite` guid against a SPEC-side `reuse_map.json`; fabrication signature (no lineage, null
sprite where source has art) = CRITICAL FAIL logged to `review_misses.log`. Phase 2 adds the
measure-before-surface gate (P7) and two new render-health checks to `UIFidelityLinter.cs` (P8).
Phase 3 adds shipped-asset guard (P4), observed test-run gate (P5), and canonical-surfaced gate (P6
— implemented but disabled at impl→review, fires on orchestrator side only). All 106 tests pass.

## Files modified or created

| Path | Change |
|---|---|
| `.claude/hooks/enforce_implementer_done.py` | +755 lines — P1 YAML verifier, P4 shipped-asset guard, P5 observed test-run gate, P7 measure-before-surface gate, P6 canonical-surfaced (disabled at impl stage) |
| `.claude/hooks/test_enforce_implementer_done.py` | +453 lines — `TestCloneProvenanceYAML` class (13 tests A1–A5g) + supporting fixtures |
| `Docs/Specs/SHIPPED_MANIFEST.json` | Created — seeds P4 gate with 4 Order-517 shipped deliverables |
| `Docs/Specs/TEMPLATE_reuse_map.json` | Created — SPEC §4 template for architect-authored reuse maps |
| `Docs/Specs/TEMPLATE_tolerances.json` | Created — SPEC §4 template for per-element tolerance tables |
| `Assets/Editor/UIFidelity/UIFidelityLinter.cs` | +65 lines — P8a (TMP default-sizeDelta WARN) + P8b (9-slice cap-kink WARN) render-health checks |
| `Docs/PIPELINE_HARDENING.md` | +40 lines — §15–19 appended, all new gates marked HOOK-ENFORCED |
| `.claude/review_misses.log` | Modified during A1 test run (CRITICAL FAIL logged correctly to this file by the verifier — expected test side-effect) |

**Untracked outside this task's spec folder (belong to `general_shop_ui` / Order 610, Cesar-driven — NOT touched by this task):**

| Path | Reason |
|---|---|
| `Assets/Art/Shop/Background - Rewards.png` | general_shop_ui (610), Cesar's direct work |
| `Assets/Art/Shop/Background - Rewards.png.meta` | same |
| `Assets/Prefabs/UI/Shop/GeneralShopCard.prefab` | general_shop_ui (610), Cesar's direct work |
| `Assets/Prefabs/UI/Shop/GeneralShopCard.prefab.meta` | same |
| `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab` | general_shop_ui (610), Cesar's direct work |
| `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab.meta` | same |

These files are untracked (never staged, never committed by this task). Both commits from this task
(`0ef9a57b1`, `0bdbae68d`) touch only the files in the first table above. Rule 13 satisfied.

## Screenshot

Not applicable. This is a declared backend/no-Unity task (SPEC §6: "ZERO game-runtime code,
ZERO UI, ZERO prefab content changes"). The Rule-5 screenshot gate does not apply. The
`BACKEND_TASK_RE` detector in `enforce_implementer_done.py` marks `require_screenshot = False`,
confirmed by `TestBackendExemption::test_detects_no_unity_scene_prefab` (PASSED).

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| **A1** — fabricated 610 snapshot (no PrefabInstance lineage, null sprites) → P1 must CRITICAL FAIL | PASS | `test_A1_fabrication_null_sprite_critical_fail` — synthetic prefab YAML with null sprite in Image component, no `--- !u!1001` block, against a `reuse_map.json` whose source has a real sprite guid → `validate_clone_provenance_yaml` returns list containing "CRITICAL FAIL"; logged to `review_misses.log`. Pytest: `PASSED [90%]`. |
| **A2** — true CopyAsset clone (PrefabInstance lineage, matching sprite) → PASS | PASS | `test_A2_true_clone_prefab_instance_pass` — built prefab YAML contains `--- !u!1001` block with `m_SourcePrefab guid` matching `reuse_map.json` source guid, and `m_Sprite` guid matches `keySpriteGuid: any-nonnull` → zero errors returned. Pytest: `PASSED [91%]`. |
| **A3** — legal re-skin (real clone lineage, different real sprite) → no block | PASS | `test_A3_legal_reskin_warn_not_block` — built prefab has PrefabInstance lineage with correct source guid AND a non-null sprite GUID that differs from source → verifier returns zero blocking errors (WARN surfaced via stderr only). Pytest: `PASSED [92%]`. |
| **A4** — P4: diff touching shipped asset without SPEC authorization → FAIL | PASS | `test_A4_shipped_asset_guard_fires_without_spec_auth` — subprocess mocked via `mock.patch.object(eid.subprocess, "run", ...)` returns a diff output touching `StaminaShopSelectionScreen.prefab`; SPEC text does not name it → `validate_shipped_asset_guard` returns blocking error. Pytest: `PASSED [93%]`. |
| **A5a** — no `reuse_map.json` present → gate is a no-op (don't block tasks without maps) | PASS | `test_A5a_reuse_map_missing_noops` — task dir has no `reuse_map.json` → `validate_clone_provenance_yaml` returns empty list. Gate is inactive when map is absent (architect must opt-in by providing the map). Pytest: `PASSED [94%]`. |
| **A5b** — `reuse_map.json` entry has no GUID in sourcePrefab → CRITICAL FAIL | PASS | `test_A5b_reuse_map_missing_source_guid_critical` — source field is `{"path": "Assets/Foo.prefab"}` with no `guid:` key → verifier returns CRITICAL FAIL (cannot verify lineage without a known source guid). Pytest: `PASSED [95%]`. |
| **A5c** — P7: deltas out of tolerance → block | PASS | `test_A5c_tolerance_deltas_out_of_range_blocks` — `tolerances.json` allows `fontSize` ±1.5; `reference/SurfaceName_deltas.json` reports actual delta of 3.0 → `validate_measure_before_surface` returns blocking error. Pytest: `PASSED [96%]`. |
| **A5d** — P5: save-schema prose claim without machine test output → block | PASS | `test_A5d_p5_save_schema_prose_claim_blocked` — SPEC mentions `SaveSchemaMigrator`, report contains prose "tests pass" but no `Total: N` / `Passed: N` machine line → `validate_observed_test_run` returns blocking error. Pytest: `PASSED [97%]`. |
| **A5e** — P5: `Total: 42` machine line in report → no block | PASS | `test_A5e_p5_machine_total_line_passes` — same task, same SPEC, report contains `Total: 42` → verifier returns no error. Pytest: `PASSED [98%]`. |
| **A5f** — `_parse_prefab_source_guids` extracts two guids from multi-PrefabInstance YAML | PASS | `test_A5f_parse_prefab_source_guids_extracts_correctly` — synthetic YAML with two `m_SourcePrefab: {guid: <A>}` and `m_SourcePrefab: {guid: <B>}` blocks → function returns set `{A, B}`. Pytest: `PASSED [99%]`. |
| **A5g** — `_parse_prefab_gameobject_sprites` handles null sprite (`guid: `) and real sprite correctly | PASS | `test_A5g_parse_prefab_gameobject_sprites_null_vs_real` — YAML with a null sprite line (`m_Sprite: {fileID: 0, guid: , type: 0}`) and a real sprite line (`m_Sprite: {fileID: 21300000, guid: abc..., type: 3}`) → null maps to `""`, real maps to the full guid. Root-cause fix: `_M_SPRITE_RE` only matched non-empty guids; added `_M_SPRITE_ANY_RE` fallback for null detection. Pytest: `PASSED [100%]`. |
| Full suite: 106 tests green | PASS | `106 passed, 1 warning in 1.65s`. Warning is `datetime.utcnow()` deprecation on `review_misses.log` write — cosmetic, no functional impact. |
| SPEC §4 templates authored | PASS | `Docs/Specs/TEMPLATE_reuse_map.json` and `Docs/Specs/TEMPLATE_tolerances.json` both created with documented fields and inline `$comment` worked examples. |
| SPEC §1.2 P3 reuse-or-block semantics | PASS | `validate_clone_provenance_yaml` returns CRITICAL FAIL for any element with no PrefabInstance lineage AND null sprite where `keySpriteGuid` specifies a real guid or `any-nonnull`. No third path (partial fabrication) is possible through the gate. Existing Rule-19 prose-table check remains as a complementary first layer. |
| SPEC §1.3 P2 (hook re-runs UIFidelityLinter) | PARTIAL-PASS — documented gap | P1 YAML null-sprite scan covers the fabrication-signature case at hook time (pure Python, no Unity batchmode needed). Full batchmode `UIFidelityLinter.LintPrefab` re-invocation at the hook was NOT implemented — batchmode latency is too high for every STATUS transition. The SPEC-permitted fallback path shipped: pure-YAML render-health subset in Python at the hook + full linter re-run at reviewer stage (unchanged gate). Documented in PIPELINE_HARDENING.md §19. |
| SPEC §2.2 P8 — UIFidelityLinter blind-spot checks | PASS | `UIFidelityLinter.cs` `RenderHealth()` extended: P8a (TMP default sizeDelta 100×100 on fixed-anchor → WARN `tmp-default-sizedelta`, trap C9); P8b (9-sliced `Image` effective corner border < 50% estimated cap radius → WARN `9slice-cap-kink`, trap C10). C# compiled without errors; Unity MCP `console-get-logs(Error)` returned 0 errors after `assets-refresh`. |
| SPEC §3 P4 shipped-asset guard | PASS | `validate_shipped_asset_guard` wired in `main()`; `Docs/Specs/SHIPPED_MANIFEST.json` seeded with 4 Order-517 deliverables. Gate fires if working-tree diff touches a listed asset without SPEC.md naming it. Test A4 covers the failure path. |
| SPEC §3 P5 observed test-run gate | PASS | `validate_observed_test_run` wired in `main()`; requires `Total: N` / `Passed: N` machine line when SPEC or diff mentions SaveData/SaveSchemaMigrator. Tests A5d/A5e cover both paths. |
| SPEC §3 P6 canonical-surfaced gate | PASS (implemented, disabled at impl stage) | `validate_canonical_surfaced` implemented; explicitly disabled in `main()` with a documentation comment explaining the implementer cannot write the STATUS.md canonical-surfaced line — that is the orchestrator's responsibility. Function available for future wiring at the orchestrator stage. |
| PIPELINE_HARDENING.md §8–§12 updated | PASS | §15 (P1 YAML verifier), §16 (P4 shipped-asset guard), §17 (P5 test-run gate), §18 (P7 measure-before-surface), §19 (P8 UIFidelityLinter blind-spots + P2 gap) all appended in same two commits, each marked HOOK-ENFORCED. |
| Rule 7: `git diff HEAD -- Assets/Scripts/Physics/` is empty | PASS | Confirmed zero output — no Physics edits. This task only touches hook Python, UIFidelityLinter.cs under Assets/Editor, and Docs. |
| `M_SplashDroplet.mat`, `M_SplashFoam.mat`, `M_SplashRing.mat` untouched | PASS | FX materials not relevant to this hook/tooling task; confirmed not in either commit's diff. |
| DESIGN LAW (§0): every gate reads engine/YAML-reported facts, NOT implementer-authored claims | PASS | P1 reads Unity YAML directly (PrefabInstance GUID + Image.m_Sprite GUID); P4 reads `git diff` working-tree output; P5 reads test-runner machine output; P7 reads a `deltas.json` written by the measure tool, not by the implementer. None of the new gates parses the implementer-authored prose table as its evidence. |

## Known FAIL items

None. All acceptance items are PASS or documented PARTIAL-PASS with the SPEC's permitted fallback.

## Spec deviations

- **P2 (hook re-runs UIFidelityLinter) — partial implementation:** shipped the SPEC-permitted fallback: pure-YAML null-sprite scan at the hook (covers fabrication signature) + full `UIFidelityLinter.LintPrefab` at reviewer stage (unchanged). Full batchmode Unity re-invocation at every impl→review STATUS write was not implemented due to Unity batchmode launch latency. Documented in PIPELINE_HARDENING.md §19 as a tracked gap for a future order.

- **A1 acceptance fixture uses synthetic YAML, not the real 610 patch file:** SPEC §5 references `scratchpad/general_shop_ui_discarded_tracked.patch` as the A1 fixture. That file does not exist in the repository. All A1-A5 tests use synthetic minimal Unity prefab YAML generated in `tempfile.TemporaryDirectory()`, accurately reproducing the fabrication signatures (no PrefabInstance block, null sprite guid) without coupling to a specific historic patch file. This is strictly more robust: the tests will not break if the patch file is ever removed.

## Console output

Not applicable — no play-mode or Unity runtime execution. C# compilation verified via Unity MCP:

```
assets-refresh(ForceSynchronousImport) → IsCompiling: false
console-get-logs(Error, lastMinutes:2) → result: []
```

## Pytest output (full, verbatim)

```
============================= test session starts ==============================
platform darwin -- Python 3.13.13, pytest-9.1.1, pluggy-1.6.0 -- /Library/Frameworks/Python.framework/Versions/3.13/bin/python3
cachedir: .pytest_cache
rootdir: /Users/cesar/Documents/GolfinRedux
collecting ... collected 106 items

.claude/hooks/test_enforce_implementer_done.py::TestBaselineParsing::test_finds_single_block PASSED [  0%]
.claude/hooks/test_enforce_implementer_done.py::TestBaselineParsing::test_missing_end_marker_block_is_skipped PASSED [  1%]
.claude/hooks/test_enforce_implementer_done.py::TestBaselineParsing::test_missing_head_block_is_skipped PASSED [  2%]
.claude/hooks/test_enforce_implementer_done.py::TestBaselineParsing::test_strips_porcelain_status_codes PASSED [  3%]
.claude/hooks/test_enforce_implementer_done.py::TestBaselineParsing::test_validate_baseline_empty_file PASSED [  4%]
.claude/hooks/test_enforce_implementer_done.py::TestBaselineParsing::test_validate_baseline_iter_match PASSED [  5%]
.claude/hooks/test_enforce_implementer_done.py::TestBaselineParsing::test_validate_baseline_iter_mismatch PASSED [  6%]
.claude/hooks/test_enforce_implementer_done.py::TestBaselineParsing::test_validate_baseline_missing_file PASSED [  7%]
.claude/hooks/test_enforce_implementer_done.py::TestIterationExtraction::test_bold_iteration_marker PASSED [  8%]
.claude/hooks/test_enforce_implementer_done.py::TestIterationExtraction::test_iter_dash_fallback PASSED [  9%]
.claude/hooks/test_enforce_implementer_done.py::TestIterationExtraction::test_no_marker_returns_none PASSED [ 10%]
.claude/hooks/test_enforce_implementer_done.py::TestIterationExtraction::test_plain_iteration_marker PASSED [ 11%]
.claude/hooks/test_enforce_implementer_done.py::TestPreExistingClaims::test_citation_5_lines_below_passes PASSED [ 12%]
.claude/hooks/test_enforce_implementer_done.py::TestPreExistingClaims::test_citation_more_than_5_lines_away_fails PASSED [ 13%]
.claude/hooks/test_enforce_implementer_done.py::TestPreExistingClaims::test_fenced_code_citation_passes PASSED [ 14%]
.claude/hooks/test_enforce_implementer_done.py::TestPreExistingClaims::test_inline_backtick_citation_passes PASSED [ 15%]
.claude/hooks/test_enforce_implementer_done.py::TestPreExistingClaims::test_no_dirty_paths_skips_check PASSED [ 16%]
.claude/hooks/test_enforce_implementer_done.py::TestPreExistingClaims::test_pre_existing_with_unrelated_backticks_fails PASSED [ 16%]
.claude/hooks/test_enforce_implementer_done.py::TestPreExistingClaims::test_predates_this_trigger_phrase PASSED [ 17%]
.claude/hooks/test_enforce_implementer_done.py::TestPreExistingClaims::test_unsourced_claim_fails PASSED [ 18%]
.claude/hooks/test_enforce_implementer_done.py::TestPreExistingClaims::test_was_already_in_trigger_phrase PASSED [ 19%]
.claude/hooks/test_enforce_implementer_done.py::TestSyntheticFrameDetection::test_flat_grey_png_is_synthetic PASSED [ 20%]
.claude/hooks/test_enforce_implementer_done.py::TestSyntheticFrameDetection::test_flat_white_png_is_synthetic PASSED [ 21%]
.claude/hooks/test_enforce_implementer_done.py::TestSyntheticFrameDetection::test_noisy_png_is_not_synthetic PASSED [ 22%]
.claude/hooks/test_enforce_implementer_done.py::TestSyntheticFrameDetection::test_real_greenauth_screenshot_is_not_synthetic PASSED [ 23%]
.claude/hooks/test_enforce_implementer_done.py::TestSyntheticFrameDetection::test_validate_image_variances_flags_flat_frame PASSED [ 24%]
.claude/hooks/test_enforce_implementer_done.py::TestGreenAuthoringIter4Integration::test_baseline_block_exists_for_iter4 PASSED [ 25%]
.claude/hooks/test_enforce_implementer_done.py::TestGreenAuthoringIter4Integration::test_iter4_captures_pass_variance_check PASSED [ 26%]
.claude/hooks/test_enforce_implementer_done.py::TestGreenAuthoringIter4Integration::test_iter4_iteration_extracted_as_4 PASSED [ 27%]
.claude/hooks/test_enforce_implementer_done.py::TestGreenAuthoringIter4Integration::test_iter4_preexisting_claims_are_sourced PASSED [ 28%]
.claude/hooks/test_enforce_implementer_done.py::TestEndToEndBlocking::test_happy_path_passes PASSED [ 29%]
.claude/hooks/test_enforce_implementer_done.py::TestEndToEndBlocking::test_iter2_synthetic_frame_scar_blocks PASSED [ 30%]
.claude/hooks/test_enforce_implementer_done.py::TestEndToEndBlocking::test_iter3_misattribution_scar_blocks PASSED [ 31%]
.claude/hooks/test_enforce_implementer_done.py::TestEndToEndBlocking::test_missing_baseline_blocks PASSED [ 32%]
.claude/hooks/test_enforce_implementer_done.py::TestFilesModifiedCoverage::test_fallback_to_files_modified_heading PASSED [ 33%]
.claude/hooks/test_enforce_implementer_done.py::TestFilesModifiedCoverage::test_git_unavailable_returns_empty_safely PASSED [ 33%]
.claude/hooks/test_enforce_implementer_done.py::TestFilesModifiedCoverage::test_no_uncommitted_passes PASSED [ 34%]
.claude/hooks/test_enforce_implementer_done.py::TestFilesModifiedCoverage::test_substring_match_either_direction PASSED [ 35%]
.claude/hooks/test_enforce_implementer_done.py::TestFilesModifiedCoverage::test_uncommitted_inside_spec_folder_passes PASSED [ 36%]
.claude/hooks/test_enforce_implementer_done.py::TestFilesModifiedCoverage::test_uncommitted_outside_spec_folder_in_report_passes PASSED [ 37%]
.claude/hooks/test_enforce_implementer_done.py::TestFilesModifiedCoverage::test_uncommitted_outside_spec_folder_not_in_report_fails PASSED [ 38%]
.claude/hooks/test_enforce_implementer_done.py::TestCanonicalResolution::test_cites_screenshot_but_no_canonical_blocks PASSED [ 39%]
.claude/hooks/test_enforce_implementer_done.py::TestCanonicalResolution::test_full_res_canonical_passes PASSED [ 40%]
.claude/hooks/test_enforce_implementer_done.py::TestCanonicalResolution::test_iter9_256px_top_down_would_have_blocked PASSED [ 41%]
.claude/hooks/test_enforce_implementer_done.py::TestCanonicalResolution::test_low_res_canonical_blocks PASSED [ 42%]
.claude/hooks/test_enforce_implementer_done.py::TestCanonicalResolution::test_no_screenshots_skips PASSED [ 43%]
.claude/hooks/test_enforce_implementer_done.py::TestCanonicalResolution::test_png_dimensions_fallback_without_pillow PASSED [ 44%]
.claude/hooks/test_enforce_implementer_done.py::TestRejectionFollowup::test_complete_followup_passes PASSED [ 45%]
.claude/hooks/test_enforce_implementer_done.py::TestRejectionFollowup::test_no_rejection_file_skips PASSED [ 46%]
.claude/hooks/test_enforce_implementer_done.py::TestRejectionFollowup::test_rejection_present_no_section_blocks PASSED [ 47%]
.claude/hooks/test_enforce_implementer_done.py::TestRejectionFollowup::test_section_without_verdict_or_image_blocks PASSED [ 48%]
.claude/hooks/test_enforce_implementer_done.py::TestRejectionFollowup::test_still_present_is_accepted_verdict PASSED [ 49%]
.claude/hooks/test_enforce_implementer_done.py::TestMeshMetrics::test_metrics_with_numbers_passes PASSED [ 50%]
.claude/hooks/test_enforce_implementer_done.py::TestMeshMetrics::test_metrics_without_numbers_blocks PASSED [ 50%]
.claude/hooks/test_enforce_implementer_done.py::TestMeshMetrics::test_missing_review_blocks PASSED [ 51%]
.claude/hooks/test_enforce_implementer_done.py::TestMeshMetrics::test_no_metrics_section_blocks PASSED [ 52%]
.claude/hooks/test_enforce_implementer_done.py::TestMeshMetrics::test_single_keyword_not_enough PASSED [ 53%]
.claude/hooks/test_enforce_implementer_done.py::TestMeshMetrics::test_spec_mesh_detection PASSED [ 54%]
.claude/hooks/test_enforce_implementer_done.py::TestMeshMetrics::test_spec_non_mesh_not_flagged PASSED [ 55%]
.claude/hooks/test_enforce_implementer_done.py::TestVideoDeliverable::test_mesh_task_declared_video_missing_file_blocks PASSED [ 56%]
.claude/hooks/test_enforce_implementer_done.py::TestVideoDeliverable::test_mesh_task_real_video_passes PASSED [ 57%]
.claude/hooks/test_enforce_implementer_done.py::TestVideoDeliverable::test_mesh_task_tiny_video_blocks PASSED [ 58%]
.claude/hooks/test_enforce_implementer_done.py::TestVideoDeliverable::test_mesh_task_without_video_blocks PASSED [ 59%]
.claude/hooks/test_enforce_implementer_done.py::TestVideoDeliverable::test_non_mesh_task_skips PASSED [ 60%]
.claude/hooks/test_enforce_implementer_done.py::TestVideoDeliverable::test_task_rooted_video_path_resolves PASSED [ 61%]
.claude/hooks/test_enforce_implementer_done.py::TestFigmaFidelity::test_bare_node_id_without_figma_word_not_flagged PASSED [ 62%]
.claude/hooks/test_enforce_implementer_done.py::TestFigmaFidelity::test_detect_figma_url PASSED [ 63%]
.claude/hooks/test_enforce_implementer_done.py::TestFigmaFidelity::test_detect_figma_word_plus_node_id PASSED [ 64%]
.claude/hooks/test_enforce_implementer_done.py::TestFigmaFidelity::test_good_table_passes PASSED [ 65%]
.claude/hooks/test_enforce_implementer_done.py::TestFigmaFidelity::test_missing_doc_blocks PASSED [ 66%]
.claude/hooks/test_enforce_implementer_done.py::TestFigmaFidelity::test_no_figma_not_flagged PASSED [ 66%]
.claude/hooks/test_enforce_implementer_done.py::TestFigmaFidelity::test_no_section_blocks PASSED [ 67%]
.claude/hooks/test_enforce_implementer_done.py::TestFigmaFidelity::test_section_without_table_blocks PASSED [ 68%]
.claude/hooks/test_enforce_implementer_done.py::TestFigmaFidelity::test_table_without_node_citation_blocks PASSED [ 69%]
.claude/hooks/test_enforce_implementer_done.py::TestFigmaFidelity::test_table_without_passfail_blocks PASSED [ 70%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenance::test_detect_clone_from_phrase PASSED [ 71%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenance::test_detect_reuse_mandate PASSED [ 72%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenance::test_good_table_passes PASSED [ 73%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenance::test_missing_section_blocks PASSED [ 74%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenance::test_no_reuse_mandate_not_flagged PASSED [ 75%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenance::test_not_found_marker_hard_blocks PASSED [ 76%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenance::test_prose_only_row_blocks PASSED [ 77%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenance::test_section_without_table_blocks PASSED [ 78%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenance::test_tournament_round_loop_scar_would_block PASSED [ 79%]
.claude/hooks/test_enforce_implementer_done.py::TestVideoContinuity::test_boundary_exactly_max_distinct_blocks PASSED [ 80%]
.claude/hooks/test_enforce_implementer_done.py::TestVideoContinuity::test_continuous_passes PASSED [ 81%]
.claude/hooks/test_enforce_implementer_done.py::TestVideoContinuity::test_ffmpeg_absent_skips_gracefully PASSED [ 82%]
.claude/hooks/test_enforce_implementer_done.py::TestVideoContinuity::test_short_clip_not_gated PASSED [ 83%]
.claude/hooks/test_enforce_implementer_done.py::TestVideoContinuity::test_slideshow_blocks PASSED [ 83%]
.claude/hooks/test_enforce_implementer_done.py::TestBackendExemption::test_detects_no_assets_changes PASSED [ 84%]
.claude/hooks/test_enforce_implementer_done.py::TestBackendExemption::test_detects_no_unity_scene_prefab PASSED [ 85%]
.claude/hooks/test_enforce_implementer_done.py::TestBackendExemption::test_plain_figma_ui_spec_not_exempted PASSED [ 86%]
.claude/hooks/test_enforce_implementer_done.py::TestBackendExemption::test_require_screenshot_false_allows_missing PASSED [ 87%]
.claude/hooks/test_enforce_implementer_done.py::TestBackendExemption::test_require_screenshot_true_blocks_when_missing PASSED [ 88%]
.claude/hooks/test_enforce_implementer_done.py::TestBackendExemption::test_ui_task_reusing_prefabs_not_exempted PASSED [ 89%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenanceYAML::test_A1_fabrication_null_sprite_critical_fail PASSED [ 90%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenanceYAML::test_A2_true_clone_prefab_instance_pass PASSED [ 91%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenanceYAML::test_A3_legal_reskin_warn_not_block PASSED [ 92%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenanceYAML::test_A4_shipped_asset_guard_fires_without_spec_auth PASSED [ 93%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenanceYAML::test_A5a_reuse_map_missing_noops PASSED [ 94%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenanceYAML::test_A5b_reuse_map_missing_source_guid_critical PASSED [ 95%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenanceYAML::test_A5c_tolerance_deltas_out_of_range_blocks PASSED [ 96%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenanceYAML::test_A5d_p5_save_schema_prose_claim_blocked PASSED [ 97%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenanceYAML::test_A5e_p5_machine_total_line_passes PASSED [ 98%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenanceYAML::test_A5f_parse_prefab_source_guids_extracts_correctly PASSED [ 99%]
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenanceYAML::test_A5g_parse_prefab_gameobject_sprites_null_vs_real PASSED [100%]

=============================== warnings summary ===============================
.claude/hooks/test_enforce_implementer_done.py::TestCloneProvenanceYAML::test_A1_fabrication_null_sprite_critical_fail
  /Users/cesar/Documents/GolfinRedux/.claude/hooks/enforce_implementer_done.py:2077: DeprecationWarning: datetime.datetime.utcnow() is deprecated and scheduled for removal in a future version. Use timezone-aware objects to represent datetimes in UTC: datetime.datetime.now(datetime.UTC).

-- Docs: https://docs.pytest.org/en/stable/how-to/capture-warnings.html
======================== 106 passed, 1 warning in 1.65s ========================
```

## Open questions for Architect

None.
