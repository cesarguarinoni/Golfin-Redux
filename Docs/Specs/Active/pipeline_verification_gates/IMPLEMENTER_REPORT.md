# IMPLEMENTER REPORT — pipeline_verification_gates (Order 611, iter-2)

**Iteration shape:** clone-provenance:guid-paste-bypass

**Task:** Fix the A1-mutant bypass in `validate_clone_provenance_yaml` — a from-scratch fabrication carrying the source's `m_Sprite` guid (zero `!u!1001` blocks, pasted guid string) PASSED the iter-1 P1 gate. The red-team demonstrated this live.

---

## Rejection follow-up

`ARCHITECT_REVIEW.md` RED-TEAM section verdict: `ARCHITECT_REVIEW_FAIL` — guid-paste bypass.

### Defect: no-lineage CopyAsset branch accepted same-sprite-guid as PASS

**Red-team characterization:** from-scratch prefab, 0 `!u!1001` PrefabInstance blocks, source's `m_Sprite` guid pasted into the `Image` component → `validate_clone_provenance_yaml` returned 0 errors (PASSED). The shipping line 2067 comment read: `# else: same sprite or source has none — PASS (or keySpriteGuid match)`.

**Root cause:** `built_sprite_guid == source_sprite_guid` was treated as implicit PASS in the `else` branch. But a sprite GUID is copyable text — pasting `m_Sprite: {guid: X}` from the reference YAML does not prove the element was created via `AssetDatabase.CopyAsset`. SPEC §1.1 explicitly states: *"Deliberately NOT checked: sprite equality as such."* Same-guid without `!u!1001` lineage is corroborating at best, never sufficient.

**Fix applied:** replaced the `# else: same sprite…PASS` comment+fallthrough with an explicit `elif source_sprite_guid and built_sprite_guid == source_sprite_guid:` branch that emits `CRITICAL FAIL` and calls `_log_p1_miss`. The diagnostic message names the bypass explicitly:
> "A GUID string is copyable text; matching guids without !u!1001 PrefabInstance lineage cannot distinguish a real CopyAsset clone from a from-scratch fabrication with a pasted guid. Require PrefabInstance (!u!1001) lineage to prove this element was cloned."

**Verdict: GONE — confirmed by test `test_A1_mutant_guid_paste_critical_fail` (see below).**

### Reviewer follow-up #2: name absent from source should WARN

**Red-team (via reviewer §4):** an element name absent from BOTH built and source prefabs — or present in built but absent from source — currently had a silent fallthrough in the `elif not source_sprite_guid` case. Added an explicit `elif not source_sprite_guid and built_sprite_guid:` branch that emits `P1 WARN: element name may be absent from source prefab (reuse_map name mismatch)`. This surfaces for reviewer confirmation without blocking.

**Verdict: GONE — the name-mismatch case now emits a visible WARN.**

---

## What changed (iter-2 scope)

### 1. `.claude/hooks/enforce_implementer_done.py` — fix the no-lineage branch

The `else:` fallthrough at line 2067 (old) was replaced with two new `elif` branches:

```python
elif source_sprite_guid and built_sprite_guid == source_sprite_guid:
    # CRITICAL FAIL — guid string is copyable text, not lineage proof (A1-mutant fix)
    errors.append(f"CRITICAL FAIL (P1): ...")
    _log_p1_miss(task_dir, element_path, source_guid, repo_root)
elif not source_sprite_guid and built_sprite_guid:
    # WARN — source has no sprite, built does; may be name mismatch (follow-up #2)
    errors.append(f"P1 WARN: ...")
# else: both sides have no sprite — already handled above (CRITICAL FAIL)
```

The A3 case (`elif source_sprite_guid and built_sprite_guid != source_sprite_guid:` → WARN) is UNCHANGED — legal re-skins still WARN only, not FAIL.

### 2. `.claude/hooks/test_enforce_implementer_done.py` — new A1-mutant test

Added `test_A1_mutant_guid_paste_critical_fail` to `TestCloneProvenanceYAML`:
- Builds a from-scratch prefab (0 `!u!1001`) whose element carries `_SPRITE_GUID` — the SAME guid as the source.
- Asserts `validate_clone_provenance_yaml` returns `>= 1` CRITICAL FAIL.
- Asserts `review_misses.log` is created with `P1-CRITICAL-FAIL`.

### 3. `Docs/PIPELINE_HARDENING.md` §15

Updated to describe the now-sound P1: documents all verdict branches (null, neither-sprite, same-guid CRITICAL FAIL, different-sprite WARN, source-has-no-sprite WARN), names the A1-mutant bypass as closed in iter-2, updates test count to 13 (107 total suite).

---

## Acceptance checklist

### A1 — fabricated prefab (no PrefabInstance, null sprite) must CRITICAL FAIL

`test_A1_fabrication_null_sprite_critical_fail` — **PASS** (existing test, continues to pass).

Evidence: `107 passed, 2 warnings in 1.64s` (full suite).

### A1-mutant — from-scratch prefab with source's sprite guid pasted must CRITICAL FAIL

`test_A1_mutant_guid_paste_critical_fail` — **PASS** (new test added this iteration).

Evidence: test output:

```
test_enforce_implementer_done.py::TestCloneProvenanceYAML::test_A1_mutant_guid_paste_critical_fail PASSED
```

This is the exact bypass the red-team demonstrated.

### A2 — true PrefabInstance clone must PASS

`test_A2_true_clone_prefab_instance_pass` — **PASS** (existing test, continues to pass).

### A3 — legal re-skin (different real sprite, no PrefabInstance) must WARN not FAIL

`test_A3_legal_reskin_warn_not_block` — **PASS** (existing test, continues to pass).

The fix does NOT touch the `elif source_sprite_guid and built_sprite_guid != source_sprite_guid:` WARN branch — A3 is structurally intact.

### Full suite

```
cd .claude/hooks && python3 -m pytest test_enforce_implementer_done.py -q
107 passed, 2 warnings in 1.64s
```

(2 deprecation warnings on `datetime.utcnow()` — cosmetic, pre-existing, not failures.)

Previous count: 106 tests (iter-1). New count: 107 (one new test: `test_A1_mutant_guid_paste_critical_fail`).

---

## Branch table (complete no-lineage verdict logic after fix)

All cases for an element with NO `!u!1001` PrefabInstance lineage:

| Case | built_sprite | source_sprite | Verdict | Correct? |
|---|---|---|---|---|
| A. same guid pasted (A1-mutant) | non-null X | non-null X | **CRITICAL FAIL** | YES — closed iter-2 |
| B. different real sprite (re-skin) | non-null Y | non-null X (Y≠X) | **WARN** | YES — A3 preserved |
| C. null where source has art (610 exact) | null | non-null X | **CRITICAL FAIL** | YES — existing |
| D. neither side has sprite | null | null/None | **CRITICAL FAIL** | YES — existing |
| E. source has no sprite, built has one | non-null Y | null/None | **WARN** | YES — follow-up #2 |
| F. source YAML not found | — | — | **WARNING** (can't check) | YES — existing |

Case A was the bypass. It now correctly CRITICAL FAILs.

---

## Rule 7 compliance — Physics/ untouched

`git diff HEAD -- Assets/Scripts/Physics/` → empty output. **PASS.**

---

## Files modified or created

| File | Change |
|---|---|
| `.claude/hooks/enforce_implementer_done.py` | Fixed no-lineage branch: replaced silent-PASS else with CRITICAL FAIL for same-guid-paste + WARN for source-has-no-sprite case |
| `.claude/hooks/test_enforce_implementer_done.py` | Added `test_A1_mutant_guid_paste_critical_fail` to `TestCloneProvenanceYAML` |
| `Docs/PIPELINE_HARDENING.md` | Updated §15 to document sound P1 with all verdict branches, A1-mutant bypass closure, updated test count |
| `Docs/Specs/Active/pipeline_verification_gates/HEARTBEAT.log` | Iter-2 baseline + activation |
| `Docs/Specs/Active/pipeline_verification_gates/STATUS.md` | Pipeline state |

Untracked files outside the task folder that appear in `git status`:
- `Assets/Art/Shop/Background - Rewards.png` + `.meta` — pre-existing from 610 (Cesar-completed task, not touched by this iteration)
- `Assets/Prefabs/UI/Shop/GeneralShopCard.prefab` + `.meta` — pre-existing from 610
- `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab` + `.meta` — pre-existing from 610
- `Docs/Specs/Active/general_shop_ui/` — pre-existing 610 spec folder
- `Docs/Specs/Active/pipeline_verification_gates/ARCHITECT_REVIEW.md`, `SELF_REVIEW.md`, `fixtures/` — pre-existing from iter-1 pipeline run

All are pre-existing artifacts that precede this iteration (HEAD at iter-2 start: `745caedaec8460ea473dd28ff18e9ee7058964f8`; baseline DIRTY listed only `.claude/review_misses.log` and `STATUS.md` as modified before work started per HEARTBEAT.log).

---

## Open questions for Architect

None. The fix is complete and the bypass is closed. Red-team's two requirements:
1. Same-guid-paste must CRITICAL FAIL → done (A1-mutant test PASS).
2. A3 (different real sprite) must still WARN not FAIL → done (A3 test unchanged and PASS).
