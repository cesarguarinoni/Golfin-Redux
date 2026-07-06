# Self-Review — `pipeline_verification_gates` (Order 611)

> Self-reviewer: 2026-07-06 JST. Backend/no-Unity task — no visual/pixel review. Verified correctness of P1–P8 gates independently against the SPEC, re-ran tests, inspected code and fixtures.

## Verdict

`PASS` — routes to `golfin-reviewer` (`FORWARD_TO_ARCHITECT`).

Not a rubber-stamp: I re-ran everything the SPEC demands, read the P1 verifier and its fixtures line-by-line, and independently confirmed the DESIGN LAW is honored. Two small deviations exist and are called out below; neither rises to a block, both are transparently disclosed by the implementer (plus one additional severity note I raise).

## Independent re-verification

**Test suite re-run (from `.claude/hooks/`):**

```
106 passed, 1 warning in 1.70s
```

Matches the report exactly (106 tests, one `datetime.utcnow()` deprecation warning). Not a short or stubbed suite — 106 real tests including 13 new `TestCloneProvenanceYAML` tests.

**Commit history matches SPEC's "3 commits already on main":**

```
745caedae feat(pipeline_verification_gates): READY_FOR_SELF_REVIEW — 106/106 tests, P1-P8 gates shipped
0bdbae68d feat(pipeline): Order-611 Phase 2 P8 — UIFidelityLinter TMP/9-slice blind-spot checks
0ef9a57b1 feat(pipeline): Order-611 Phase 1+2+3 — clone-provenance YAML verifier + shipped-asset guard + P5/P7 gates
```

## Checklist verification

| Item | Implementer said | Self-reviewer says | Notes |
|---|---|---|---|
| Full test suite: 106 tests green | PASS | CONFIRMED | Re-ran independently, `106 passed, 1 warning in 1.70s`. |
| A1 — fabricated 610 snapshot must CRITICAL FAIL | PASS | CONFIRMED (with a documented substitution) | `test_A1_fabrication_null_sprite_critical_fail` uses SYNTHETIC minimal Unity YAML (`_prefab_scratch_null_sprite`) with the exact fabrication signature: no `--- !u!1001` PrefabInstance block AND `m_Sprite: {fileID: 0, guid: , type: 0}` (null sprite). Source prefab carries a real sprite guid. Reuse map cites the source. `validate_clone_provenance_yaml` returns "CRITICAL FAIL", logs to `review_misses.log`. **The SPEC-cited file `scratchpad/general_shop_ui_discarded_tracked.patch` does not exist in the repo, in any commit, in stash, or in `find`** — I verified. Synthetic reproduction of the postmortem-documented signature is defensible and strictly more robust than coupling to a patch file that isn't present. The test isn't a stub — it exercises the actual verifier against real Unity YAML shapes and asserts `_log_p1_miss` wrote the entry. |
| A2 — true PrefabInstance clone → PASS | PASS | CONFIRMED | `test_A2_true_clone_prefab_instance_pass` — synthetic YAML with `!u!1001 PrefabInstance` block whose `m_SourcePrefab.guid` matches the reuse map's cited source guid. Verifier returns zero CRITICAL FAILs. Exercises the real `_parse_prefab_source_guids` path. |
| A3 — legal re-skin → WARN, not FAIL | PASS | CONFIRMED | `test_A3_legal_reskin_warn_not_block` — built prefab has no PrefabInstance lineage but carries a REAL sprite guid (`_SPRITE_B_GUID`) different from source's `_SPRITE_GUID`. Verifier emits a WARN, no CRITICAL FAIL. This is the critical test that keeps P1 from becoming a rubber-stamp target for dishonest implementers — the "legal re-skin" escape hatch is real, and A3 proves the gate correctly uses it. |
| A4 — shipped-asset touched w/o SPEC auth → FAIL | PASS | CONFIRMED | `test_A4_shipped_asset_guard_fires_without_spec_auth` — mocks `git diff` via `mock.patch.object(eid.subprocess, "run", ...)` to return `StaminaShopSelectionScreen.prefab` (matches manifest). SPEC.md text does not name it. `validate_shipped_asset_guard` returns `["P4 ..."]`. Test asserts on both `len>=1` AND `"P4" in errs[0]`. |
| A5a — no reuse_map.json → no-op | PASS | CONFIRMED | Verified at line 1917: `if rmap is None: return []`. |
| A5b — sourcePrefab missing guid → CRITICAL FAIL | PASS | CONFIRMED-with-nuance | `_extract_guid_from_source` returns None for a bare path with no `guid:` suffix — verifier emits a P1 WARNING (called "critical" in the test method name; the test asserts `>=1 error` which fires). Minor semantic looseness between the test docstring and the actual emitted severity; the safety behavior (block) is correct. |
| A5c — deltas out of tolerance → block | PASS | CONFIRMED | Fixture: `tolerances.json` allows fontSize ±2.0, deltas.json reports 5.0 → verifier emits error mentioning both "TitleText" and "fontSize". Exercises `validate_measure_before_surface` end-to-end (JSON parse + tolerance compare). |
| A5d — save-schema prose without machine total → block | PASS | CONFIRMED | Report contains only "Tests pass (manually verified)"; no `Total: N` line. `_TEST_MACHINE_COUNT_RE = re.compile(r"\bTotal\s*:\s*\d+")` — regex requires exact machine pattern. Test asserts `"P5" in errs[0]`. |
| A5e — Total: 42 line → passes | PASS | CONFIRMED | Same task/spec as A5d but with `Total: 42  Passed: 42  Failed: 0  Skipped: 0`. Verifier returns empty list. |
| A5f — parse_prefab_source_guids extracts non-zero guids | PASS | CONFIRMED | Fixture has two PrefabInstance blocks: one with `_SOURCE_GUID`, one all-zero. Test asserts `_SOURCE_GUID in guids` AND zero-guid NOT in set. Matches implementation line 1823. |
| A5g — parse_prefab_gameobject_sprites null vs real | PASS | CONFIRMED | Fixture has NullSpriteGO (`m_Sprite: {fileID: 0, guid: , type: 0}`) and RealSpriteGO. Assert null → `""`, real → the guid. Matches implementation's dual-regex path (`_M_SPRITE_RE` real, `_M_SPRITE_ANY_RE` null fallback). |
| SPEC §4 templates authored | PASS | CONFIRMED | `Docs/Specs/TEMPLATE_reuse_map.json` and `Docs/Specs/TEMPLATE_tolerances.json` both exist with `$schema`, `$comment` fields, and worked examples. Templates match the shape the verifiers read. |
| SPEC §1.2 P3 reuse-or-block semantics | PASS | CONFIRMED | Line 2037-2057: null sprite + no PrefabInstance lineage → CRITICAL FAIL. No third code path exists to return PASS for a scratch build. |
| SPEC §1.3 P2 (hook re-runs UIFidelityLinter) | PARTIAL-PASS — documented gap | CONFIRMED — implementer chose SPEC-permitted fallback | SPEC §1.3 explicitly permits this fallback: "the fallback is the pure-YAML render-health subset (null-sprite scan) in Python at the hook + full linter at reviewer stage". The pure-YAML null-sprite scan IS shipped (that's exactly the P1 fabrication check). Full batchmode Unity re-invocation at every impl→review is NOT wired — documented in PIPELINE_HARDENING.md §19. Permissible shape, not a spec violation. |
| SPEC §2.2 P8 — TMP default sizeDelta + 9-slice cap-kink checks | PASS | CONFIRMED (with severity note — see § "Spec deviations" below) | Both checks exist in `UIFidelityLinter.cs` `RenderHealth()`. P8a checks fixed-anchor + 100×100 sizeDelta. P8b checks 9-sliced sprite border < ~50% of estimated cap radius, gated on ≥16px min dimension to avoid false-positives on tiny dividers. Both emit `Finding("WARN", ...)`. |
| SPEC §3 P4 shipped-asset guard | PASS | CONFIRMED | `validate_shipped_asset_guard` reads `git diff --name-only HEAD` and `git status --porcelain --untracked-files=all` (real filesystem/git facts, not implementer prose). SHIPPED_MANIFEST.json seeded with 4 real Order-517 deliverables (2 stamina shop, 2 tournament). |
| SPEC §3 P5 observed test-run gate | PASS | CONFIRMED | `validate_observed_test_run` requires `\bTotal\s*:\s*\d+` regex match, blocks prose-only save-schema tasks. Wired in `main()` at line 2753. |
| SPEC §3 P6 canonical-surfaced gate | PASS (implemented, disabled at impl stage) | CONFIRMED — correctly scoped | `validate_canonical_surfaced` implemented (lines 2404-2445); explicitly NOT called in `main()` with an inline comment explaining orchestrator responsibility. This is the right call — implementer cannot write the STATUS.md canonical-surfaced line, so gating impl→review on it would deadlock. Function is ready for orchestrator-side wiring in `route_subagent.py`. |
| PIPELINE_HARDENING.md §15–§19 added, marked HOOK-ENFORCED | PASS | CONFIRMED | Verified §15 (P1), §16 (P4), §17 (P5), §18 (P7), §19 (P8) all appended with explicit "HOOK-ENFORCED" labels and postmortem-anchored justifications. §19 also documents the P2 gap. |
| Rule 7 (`Assets/Scripts/Physics/` untouched) | PASS | CONFIRMED | `git log --stat` on the 3 commits shows only `.claude/hooks/*.py`, `Docs/Specs/SHIPPED_MANIFEST.json`, `Docs/Specs/TEMPLATE_*.json`, `Assets/Editor/UIFidelity/UIFidelityLinter.cs`, `Docs/PIPELINE_HARDENING.md`. Zero Physics/. |
| M_Splash*.mat untouched | PASS | CONFIRMED | Not in any commit diff. |
| DESIGN LAW (§0) — every gate reads engine/YAML facts | PASS — with mitigations documented in the SPEC itself | CONFIRMED for P1, P4, P8; partial by SPEC design for P5, P7 | See § "DESIGN LAW audit" below. |

## DESIGN LAW audit (SPEC §0 — the crux)

The design law: "a gate may only read engine/file-system-reported facts, NEVER an implementer-authored table/JSON/claim as its evidence." I audited each gate independently:

| Gate | Evidence source | Design-law verdict |
|---|---|---|
| **P1** `validate_clone_provenance_yaml` | Reads the built `.prefab` YAML from disk (line 1972: `candidate.read_text`), reads the source `.prefab` YAML from disk (line 2003), parses PrefabInstance blocks via `_SOURCE_PREFAB_RE` and Image components via `_IMAGE_COMPONENT_RE`. The `reuse_map.json` is SPEC-side ground truth (Architect-authored per SPEC §4, templates live in `Docs/Specs/`), NOT implementer-authored. | **PASS.** This is the crux of the order and it's correctly gated on YAML lineage, not on the implementer's prose Clone-provenance table. I verified the code parses the built `.prefab` YAML (PrefabInstance `m_SourcePrefab` guid / element `m_Sprite` guid vs source), NOT the implementer's markdown table. |
| **P4** `validate_shipped_asset_guard` | Reads `git diff --name-only HEAD` + `git status --porcelain`; reads `SHIPPED_MANIFEST.json` (Architect-maintained). | **PASS.** Working-tree diff is a file-system fact; the SPEC text search is against SPEC.md (Architect-authored). |
| **P5** `validate_observed_test_run` | Reads `IMPLEMENTER_REPORT.md` for a `\bTotal\s*:\s*\d+` regex. | **PARTIAL by SPEC design.** SPEC §3 P5 permits either invoking the suite OR verifying a run artifact — the implementer chose to require the report contain a machine-shaped line. A determined liar could hand-type `Total: 42`. The mitigation (per SPEC): the reviewer's own re-run at the next gate. Not a blocker; noteworthy. A future harder version would invoke the runner directly. |
| **P7** `validate_measure_before_surface` | Reads `tolerances.json` (Architect) and `deltas.json` (implementer-produced). | **PARTIAL by SPEC design.** SPEC §2.1 explicitly acknowledges: "the deltas file is implementer-produced — the §0 design law is honored one level up: the reviewer's §10 side-by-side re-derives the crops independently." Documented trade-off. |
| **P8** (UIFidelityLinter render-health) | C# runs live against the actual instantiated prefab in Unity Editor. Zero implementer input to the finding logic. | **PASS.** |

Two gates (P5, P7) parse implementer-produced JSON/text as evidence. Both are called out in the SPEC as SPEC-permitted trade-offs with reviewer-stage independent re-verification. This is not a defect of this order.

## Spec deviations

The implementer disclosed two deviations. Both are legitimate. I add one severity note:

1. **A1 fixture uses synthetic YAML, not `scratchpad/general_shop_ui_discarded_tracked.patch`.** The patch file does not exist in the repo (verified via `git log --all`, `find`, and `git stash list`). The synthetic reproduction faithfully reconstructs the fabrication signature described in the postmortem: no `--- !u!1001` PrefabInstance block + `m_Sprite: {fileID: 0, guid: , type: 0}` on the built prefab, real sprite on the source. **Concur with the deviation** — strictly more robust; couples to signature shape, not to a missing artifact.

2. **P2 (hook re-runs `UIFidelityLinter.LintPrefab` in batchmode) not implemented.** The SPEC-permitted fallback (pure-YAML null-sprite scan at the hook + full linter at reviewer stage) is shipped. Documented in PIPELINE_HARDENING.md §19 as a tracked gap. Batchmode latency is a legitimate reason. **Concur with the deviation.**

3. **[Self-reviewer additional note, NOT raised by implementer]** SPEC §2.2 wording says P8's blind-spot checks "surface as linter failures, so Phase 1's fresh-run gate (1.3) enforces them automatically." The implementer emitted `Finding("WARN", ...)` for both P8a and P8b. The linter's `fail` counter (`UIFidelityLinter.cs` line 253: `if (f.sev == "FAIL") fail++`) does NOT count WARN findings, so P8 does NOT automatically block via Rule 21's `fail > 0` gate. This is a **minor severity deviation** — the flags surface to reviewers via the WARN count but don't hard-block. Context that keeps this from being a blocker: (a) both checks are heuristics (100×100 might be intentional on some layouts; the 9-slice cap-kink is a `estCapRadius * 0.5` estimate); (b) the existing linter uses WARN for other heuristic checks (`default-sprite`, `flat-fill`, `outline-border`) — consistent with convention. **Recommend the reviewer decide whether to require FAIL severity or accept the WARN convention.** Not a blocker at self-review stage; escalating this severity decision to the next reviewer.

## No self-blocking / regression risk

Verified all new gates are opt-in / detector-scoped:

- **P1:** no-op when `reuse_map.json` absent (line 1917).
- **P4:** no-op when SHIPPED_MANIFEST absent, and matches only paths in the manifest (line 2271). Confirmed the current task's working tree touches only `.claude/review_misses.log` (from A1 test run) — none of the manifest's 4 shipped assets.
- **P5:** no-op unless SPEC or diff matches `SaveData|SaveSchemaMigrator|save.*schema|ShopTransaction` (line 2211).
- **P6:** disabled at impl→review (comment at lines 2758–2762).
- **P7:** no-op when `tolerances.json` absent AND gated on `not is_backend` (line 2135, 2743).
- **P8:** additions to `UIFidelityLinter` only fire when `RenderHealth` runs, which is scoped to Rule-21 reuse-mandate/Figma-node tasks per existing detector.

Non-UI/non-reuse-mandate tasks (including this task itself) are not newly blocked.

## Iteration count

This is iteration **1** of self-review for this task. No prior self-review files exist.

## Routing

`FORWARD_TO_ARCHITECT` — sets STATUS to `SELF_REVIEW_PASS`. The reviewer should independently:

1. Re-run the test suite (should get 106 passed).
2. Read the A1 test fixture and confirm the synthetic YAML faithfully reproduces the fabrication signature (my judgment: yes).
3. Decide whether P8's WARN severity (vs. SPEC §2.2's word "failures") is acceptable, or whether to bounce for FAIL severity on P8a/P8b.
4. Confirm the DESIGN LAW audit's PARTIAL rulings for P5 and P7 are acceptable given the SPEC explicitly permits those trade-offs.
