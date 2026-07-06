# PIPELINE_HARDENING.md — enforced subagent-pipeline rules

**Created:** 2026-06-19, triggered by the `map_view_aiming` (Order 352) iter-15 escalation — the pipeline marked `ARCHITECT_REVIEW_PASS` twice on a feature that could not be opened in the real game and rendered upside-down. These rules convert previously-advisory lessons into **orchestrator-enforced hard stops**. Applies to ALL FULL-PIPELINE tasks.

> Implementer applies these to `route_subagent.py`, `.claude/agents/*`, and `CLAUDE.md § Multi-Agent Workflow`. They are not optional and not task-specific.

## 1. Iteration circuit-breaker (was advisory "spiral rule")
- `route_subagent` counts iterations per task. **3 failures of the same shape → forced `ESCALATE` to the Architect.** No iter-4 of the same fix shape may run.
- "Same shape" = the failure touches the same subsystem/symptom (e.g. capture-flip, ring placement). The orchestrator tags each iteration with a shape label; 3 matching tags trip the breaker.
- On trip: stop, write `ARCHITECT_ESCALATION.md`, surface to Cesar's chat. (iter-15 should have tripped at ~iter 6.)

## 2. Real-entry rule for player-facing features
- Any feature with a player entry point MUST be exercised through the **real UI widget's `onClick`/handler**. Driving a **synthetic/test-only button** that the player never sees = **automatic FAIL**.
- If the real entry isn't wired, the bot cannot reach the feature → the gate fails by construction. (This is what hid the iter-15 entry-point bug.)

## 3. Verify the math, not the pixels (visual-fidelity gate)
- For features producing world→screen visuals (markers, projections, camera framing), the **pass/fail gate is a capture-time invariant dump** (JSON of projected coords + world refs) with **deterministic assertions**, NOT a human-style judgement of a video.
- The recorded video is an **artifact for the human**, not the gate.
- Each task's SPEC defines its invariant table (see `map_view_aiming` SPEC §11 as the template: orientation, marker collinearity, projected-pos == `WorldToScreenPoint`, write-back round-trip, banned-API/architecture absence).

## 4. Capture the flip-free way — do NOT build flip-catchers
- **Flips are not a platform fact; they are self-inflicted.** Plain Unity Recorder on a normal/tagged camera does not flip (Cesar's `HoleFlyoverRecorder` flyovers prove it). Flips appear only when we add indirection: **RT→RawImage** (RenderTexture sampling origin differs across graphics APIs), a **`uvRect` flip** to "fix" it, GameView-overlay composite capture, etc.
- **Capture via the proven path:** Recorder `TaggedCamera` input pointed at the target camera — the mechanism already working in `HoleFlyoverRecorder`. **Banned:** RT→RawImage capture indirection, `uvRect` flips, and any post-process re-flip (`yflip_repair.py`-style). A pipeline that re-flips its own output is failing, not passing.
- **Verify orientation by the math, not the pixels** — the world→screen invariant (e.g. `ball.screenY > flag.screenY`) catches an upside-down render with zero frame-pixel analysis. Do not stand up a flip-detector as a routine gate; a flipped frame = a capture-path regression to fix at the source.
- If frames are ever sampled at all: decode **consecutive** frames. `ffmpeg -ss <time>` single-frame keyframe sampling stays **banned** (keyframe-snaps past intermittent flips — the iter 6–15 blind spot).

## 5. Reviewer scope
- Reviewer + red-team agents **re-run the ENTIRE SPEC §acceptance list every pass** — not only the symptom the previous reviewer named. iter-15's recurring miss was scoped re-checks that fixed the last-named thing while the feature stayed broken as a whole.

## 6. Report integrity (hard)
- **Any claim in a review/implementer report not backed by a visible tool result or the invariant JSON = automatic FAIL**, logged to `.claude/review_misses.log`.
- **Fabricating an approval, quote, or test result is a critical FAIL** and must be logged with the iteration number. (iter-9 contained a fabricated approval quote — this rule exists because of it.)

## 7. Standing bans (already in lessons; now gate-enforced)
- No edits to `Assets/Scripts/Physics/` for capture/test scaffolding (no bespoke `*Gate` scenarios there).
- No banned capture APIs (`ScreenCapture.CaptureScreenshot` as canonical proof).
- No scene-baking a new subsystem into `LabScaffold.unity` as the only home; it must live in the real gameplay flow.
- Capture must be **normal play**, not scripted discrete states presented as fluid play.

## 8. Clone-provenance gate for reuse-table tasks (was advisory clone-and-modify rule)
- **Created:** 2026-06-25, triggered by the `tournament_selection_screen` (T7) iter-1 failure — the implementer **rebuilt the screen scaffold + CTA buttons from scratch** despite a HARD clone-and-modify rule, and all three gates (implementer self-check, reviewer scene-mutation audit, orchestrator token review) checked **fidelity** but never **provenance**, so an ~80%-right-looking frame on a 100%-wrong (non-reused) foundation passed for 3 iterations.
- **Applies to:** any task whose SPEC contains a `§1 reuse / clone-from table`.
- **The gate:** for **every row** in that table, the implementer report MUST cite the concrete **source it cloned from** — a prefab **GUID**, or a named **scene-object** (path + fileID). E.g. *"CTA = instance of `GoldPrimaryButton.prefab` guid `…`"*, *"chassis = duplicate of `RankingsScreen.prefab` guid `…`"*. A reuse row with **no provenance, or provenance pointing at a net-new bespoke object = automatic FAIL.**
- **Evidence the reviewer runs** (per net-new prefab/screen that should be a clone): `grep -cE "PrefabInstance|^--- !u!1001" <asset>` must be **> 0** (a clone carries nested prefab-instance blocks; a hand-built one has 0); and the named source GUID must appear in the new asset's YAML.
- **If the named reuse source doesn't exist or isn't cleanly extractable, STOP and flag the Architect** — do **not** hand-roll a substitute and proceed. (The T7 spec told Code to reuse a "shared gold button" that had no prefab; the correct move was to flag, not rebuild.)
- This is the UI analogue of the Rule 2 real-entry gate and the Rule 3 invariant gate: provenance is checkable mechanically, so it is a hard stop, not a judgement call.

## 9. Figma node re-pull gate for Figma-referencing tasks
- **Created:** 2026-06-29, triggered by the `tournament_signup_modal` (T6) postmortem (`Docs/Reports/POSTMORTEM_tournament_signup_modal.md` §3.A3) — the modal was built and reviewed against the SPEC's prose token table + a static reference PNG; nobody ran `get_design_context` on the node, so spec mis-specs (two separators in the node vs one wanted, font divisor ÷1.4 vs ÷1.3, pill style) went unreconciled and Cesar had to dictate the px values by hand.
- **Applies to:** any task whose SPEC references a Figma NODE (a `figma.com/design` URL or a `<n>:<n>`/`<n>-<n>` node id alongside "figma").
- **The gate:** at **step 0**, the implementer AND **each reviewer** MUST run `get_design_context` (or `get_metadata` → `get_design_context`) on that node and diff the **live px / font / gap / sprite** values against the **NODE**, not against the SPEC's token table. The SPEC token table is a *reconcile-against-node convenience and never the source of truth*; where node and table disagree, the node wins (or the discrepancy is surfaced to the Architect).
- **Evidence:** the implementer report's `## Figma fidelity` section (Rule 18) and the reviewer's `ARCHITECT_REVIEW.md` must each show the node was pulled this pass — the node id + at least one value cited as read *from the node* (e.g. "node `13480:2530` gap = 48px → built 48px"). **No node-pull evidence in the report = FAIL.**
- One `get_design_context` call at the start would have given exact values for the entire modal; this is the single highest-leverage miss in the postmortem.

## 10. Reference-image diff gate (built render vs node render)
- **Created:** 2026-06-29, triggered by the `tournament_signup_modal` postmortem (§4) — no automated step ever compared the built modal to the reference and failed on dissimilarity, so the first true A/B-against-reference happened only when Cesar looked, every single time.
- **Applies to:** any Figma-node UI task (same detector as §9 / Rule 18).
- **The gate:** the reviewer produces a **side-by-side**: the built render (real-flow capture at device res) next to the `reference/` node render, and **fails on dissimilarity**. Per **mandated element** (every row of the §1 reuse table / Rule 18 fidelity table) the reviewer **pastes both crops** (built crop + node crop) into `ARCHITECT_REVIEW.md` rather than asserting "looks like Figma" / "matches". A blanket "matches" with no paired crops = FAIL of that row.
- This is the visual analogue of §9: §9 checks the numbers came from the node; §10 checks the pixels match the node.

## 11. Clone-provenance read-back (extends §8)
- **Created:** 2026-06-29, triggered by the `tournament_signup_modal` postmortem (§3.A1/A2, §5.2) — the implementer hand-built the modal from spriteless flat-colour `Image`s and the report marked the clones PASS; §8 caught *missing provenance citations* but nothing verified the cited sprite actually landed on the live object.
- **Extends §8** (do not re-implement the §8 clone-provenance citation gate / `enforce_implementer_done.py` "Rule 19" — this is the *verification* half).
- **The gate:** for **every mandated-clone element**, the reviewer **reads back the live `Image.sprite` GUID** on the instantiated object (`script-execute`, or `AssetDatabase.GetAssetPath(img.sprite)` → GUID) and confirms it is the real source sprite. **A flat-colour fill (`Image.sprite == <NONE>`) where a sprite is required = FAIL.**
- **Fabricated clone provenance** — a report claiming a clone (sprite/prefab) that does not exist on the live object — is a **critical FAIL per §6**, logged to `.claude/review_misses.log` **with the iteration number** (same weight as a fabricated approval quote).

## 12. Unity authoring traps — implementer checklist (C1–C8)
- **Created:** 2026-06-29, triggered by the `tournament_signup_modal` postmortem (§3.C) — eight Unity-specific traps were each diagnosed one at a time across separate correction cycles instead of being known up front.
- **Applies to:** all implementer work that scripts scene/prefab/UI edits. Each is a checklist item the implementer self-certifies in `IMPLEMENTER_REPORT.md`; a violation found at review = FAIL.
  - **C1. Dirty-on-write.** A scripted `image.sprite = x` (or any field set) does **not** serialize unless the object is dirtied. Use `SerializedObject.ApplyModifiedProperties` / `EditorUtility.SetDirty` / `PrefabUtility.RecordPrefabInstancePropertyModifications` / `LoadPrefabContents`+`SaveAsPrefabAsset`. (Symptom otherwise: edits show live but "revert" on reload/play.)
  - **C2. Modal-root-stays-active invariant.** `ModalController` shows/hides by toggling the child `modalPanel` via `SetActive`; the **root must stay active** or `Show()` can't make content active-in-hierarchy (breaks rendering AND bot/automation that searches for active buttons). Never set a modal root inactive for "clean boot".
  - **C3. Layout-group vs fixed-size.** Dropping a fixed-size, absolutely-positioned cloned element (e.g. a card pill) into a `VerticalLayoutGroup`/`HorizontalLayoutGroup` with `childControl*=true` stretches it. Pin a `LayoutElement` (min/preferred) or use a non-controlling parent.
  - **C4. `childForceExpandWidth/Height=true` silently widens gaps** regardless of `spacing`. For a literal Figma gap, force-expand must be **off**.
  - **C5. Unity `Outline` component ≠ a crisp Npx border.** It renders as offset duplicate copies (heavier/softer). For a 3px panel border, prefer a sprite that carries the border; do not stack an `Outline` on top of a bordered sprite.
  - **C6. Flat layout vs nested groups.** Per-gap Figma values (e.g. 24px only around a separator) are impossible in one flat layout group with uniform `spacing`; mirror the node's nested group structure.
  - **C7. Edit-mode Game View does not repaint** on edit-time changes — you **cannot** verify a UI change by screenshot in edit mode. Verify in **play mode** (`feedback_check_play_mode`).
  - **C8. The app boots through a title/PLAY screen** that manual `ScreenManager.ShowScreen` can't bypass — automated verification must drive the real entry (tap the PLAY button / `BotDriver.NavigateToHome`).
- **C9–C14 added 2026-07-06** from the Order-610 card rebuild (`POSTMORTEM_general_shop_ui_fabricated_provenance.md` Part 2 §P2.1 — each cost a correction iteration):
  - **C9. TMP default `sizeDelta` = 100×100.** A `TextMeshProUGUI` created without an explicit `sizeDelta` defaults to 100×100; with `MidlineLeft`/centre vertical alignment the glyph renders ~50px below the anchor (value labels sit one row under their bars). Always set an explicit small `sizeDelta` (e.g. 80×24) on value labels.
  - **C10. 9-slice cap-kink.** 9-slicing a proportional-width fill whose sprite border is smaller than its rounded-cap radius kinks the leading cap into a point (e.g. `LevelUpBlueFill`: border (8,3,8,3) vs ~10px caps). Use a true stadium sprite (`S_PillStadium`, border 88 = half) + tuned `pixelsPerUnitMultiplier`. Verify by **zooming the fill's leading edge**, never the whole bar.
  - **C11. Runtime layout groups don't bake in edit mode.** A component that builds children via `HorizontalLayoutGroup`/etc. at runtime (e.g. `BallSegmentedBar`) saved with `SaveAsPrefabAsset` in edit mode bakes empty/zero-size children. Static prefabs need the children built explicitly (fixed-position images).
  - **C12. Child-clear during `foreach` skips elements.** `foreach (Transform ch in t) DestroyImmediate(ch.gameObject)` mutates the collection mid-iteration and leaves half the children (doubled the RP coin). Collect into a list first, then destroy.
  - **C13. Edit-mode UI capture path.** `CaptureHelper.SnapGameView` does NOT composite ScreenSpace-Overlay UI in edit mode (see C7); `screenshot-isolated` needs 3D renderers; reparenting a card out of its nested `Canvas` breaks its rendering. Reliable recipe: temp **WorldSpace capture canvas + dedicated ortho camera → RenderTexture → ReadPixels**, far from scene geometry, torn down after (scene stays non-dirty).
  - **C14. Type-specific stat displays.** Balls ≠ clubs: clubs = continuous fill bar; balls = `BallSegmentedBar` (20-segment bidirectional, −10..+10, stats Power/Rebound/WindCut/Roll/Spin). Before building any item-type stat UI, **open the real inventory/detail surface for that type** (`BagClubCard.prefab`, `BallDetailPanel.cs`) — never assume one display fits all. Atom rows in `UI_ELEMENT_PALETTE.md`.

## 13. Fast single-modal render harness
- **Created:** 2026-06-29, triggered by the `tournament_signup_modal` postmortem (§3.E, §5.6) — every visual check cost a multi-minute round-trip (enter play ~11s → tap PLAY → navigate → open modal → force-activate → screenshot), and edit-mode shortcuts didn't work (C7), so UI-fidelity iteration was punishingly slow.
- **Requirement:** a lightweight single-screen capture harness — **boot → open one modal/screen → 1170×2532 screenshot**, without driving the full gameplay loop — analogous to the loop bot but scoped to one surface. UI-fidelity tasks use it for round-trips instead of a full loop record.
- Until it exists, the implementer must still verify in play mode (C7) via the real entry (C8); this rule tracks the missing tooling so fidelity iteration stops being the bottleneck.

## 14. Orchestrator scene-mutation guardrail
- **Created:** 2026-06-29, triggered by the `tournament_signup_modal` postmortem (§3.D1) — a render-isolation/probe script deactivated `ScreensRoot`, a buggy revert missed it, and the orchestrator **saved** the broken scene, booting the whole app to an empty menu.
- **The guardrail (orchestrator-side, like CLAUDE.md Rule 12 for commits):** never `scene-save` after a render-isolation / probe / force-activate mutation without first **diffing GameObject active-state against HEAD** (`git show HEAD:<scene>` → compare `m_IsActive` of touched roots, or re-assert the boot-critical containers — `ScreensRoot`, `PersistentUI`, the active screen — are in their committed state). If any boot-critical container's active-state diverges from HEAD unintentionally, restore it before saving.
- Probe mutations (canvas overrides, force-activations, deactivating screens to isolate a render) are **transient** — they must be reverted, not persisted. Prefer not saving at all after a probe; if a save is required, run the active-state diff first.

## 15. Reuse-FIDELITY verifier — P1 (Order-611, iter-6 reframe 2026-07-06)

> **iter-6 REFRAME (Cesar, decisive):** iters 1/3/4/5 + 3 independent red-teams proved clone *provenance* is UNPROVABLE for a CopyAsset workflow — a CopyAsset clone's artifact is byte-identical to a faithful from-scratch rebuild, and skeleton comparison is defeatable at any bounded depth (leaf → shallow-composite → N). So P1 was reframed from "prove it was cloned" to "verify it is FAITHFUL." **The gate now:** (a) HARD CRITICAL FAIL only on the unfakeable fabrication signature — a reuse-mandated element with a NULL/blank `Image.sprite` where the source has one (pure-Python; the actual 610/tournament/stamina scar = white box / flat fill); (b) PrefabInstance `!u!1001` lineage → proven clone PASS; (c) a real sprite present (same or re-skin) → sprite-fidelity MET → ACCEPT — the best-effort live structural comparison emits a WARN on gross mismatch (reviewer + reference-diff Rule 18 confirm), but MATCH / bare-leaf / unreachable-editor all ACCEPT; (d) sprite-less element → WARN; (e) A3 re-skin → WARN. A faithful from-scratch rebuild passing is CORRECT by design (it carries the real atoms). Visual-correctness scars (oval pill, 9-slice collapse) are P8 render-health's job via P2, not P1. Optional hard-provenance escape hatch: make the element a PrefabInstance clone. Full rationale: `Docs/Specs/Active/pipeline_verification_gates/RESOLUTION.md`. Verified E2E (live editor): composite clone → PASS, leaf clone → PASS, null-sprite → CRITICAL FAIL, structure-mismatch on real-sprite → WARN, editor-unreachable → ACCEPT. Suite 117 green.
>
> **History (iter-4/5, superseded by the reframe above):**
> **iter-4/5 (main-thread, Cesar-directed):** iter-3's live-editor scripts were DEAD — they used `class StructureCompare`/`class LintRerun` + `Debug.Log`, but `script-execute` requires `class Script` + `public static string Main()` and returns only the method's RETURN VALUE. Both seams always returned `None` → P1 blocked legit clones, P2 trusted the cited JSON. 113 green tests all monkeypatched the seam, so none drove the real RPC (parallel reviewer + red-team caught it end-to-end). iter-4 fixes the class/return, rewrites the compare to a **recursive skeleton signature EXCLUDING the root name** (a clone is renamed; verified: a *modified* clone matches modulo root name, a from-scratch forgery does not), makes P2 fail-CLOSED, and adds a **non-mocked `TestLiveEditorIntegration`** that drives the real editor (skips in CI).
> **iter-5 leaf guard:** the red-team then found a residual **leaf guid-paste bypass** — a bare leaf Image (no children) carrying the source's *pasted real sprite* produced a trivially-replicable skeleton → structural MATCH → PASS. (An earlier claim that "P2's null-sprite lint backstops leaves" was FALSE: the linter emits 0 FAIL on a leaf with a real pasted sprite.) A leaf's provenance is unverifiable from the artifact (a CopyAsset leaf is byte-identical to a hand-made one). Fix: `_do_live_editor_structure_check` returns `INSUFFICIENT` for a no-child element → P1 CRITICAL-FAILs it, directing the reuse_map to cite a **composite ancestor** (whose skeleton covers the leaf transitively) or make the element a PrefabInstance clone. **Proven E2E via live editor:** real composite clone → PASS, composite forgery → CRITICAL FAIL, bare-leaf citation → CRITICAL FAIL, editor-unreachable → BLOCK, A3 re-skin → WARN. Suite 117 green.

- **Created:** Order-611, triggered by `POSTMORTEM_general_shop_ui_fabricated_provenance.md` §P1 — the third instance of the fabricated-provenance scar. The existing §8 / Rule 19 gate only checked that `## Clone provenance` had table rows shaped like GUIDs; it never verified the built prefab's YAML lineage against the cited source. Cesar had to catch the fabrication by eye.
- **Design law §0 (binding on every gate):** a gate may ONLY read engine/file-system-reported facts (YAML lineage, a fresh linter invocation, an observed test-run count). Any gate that parses an implementer-authored table/JSON/claim as its evidence is a DEFECT.
- **The gate (HOOK-ENFORCED — `enforce_implementer_done.py` `validate_clone_provenance_yaml`):** for each element in `reuse_map.json` (the SPEC-side ground truth written by the implementer at start-of-task, not the prose table): verify the built prefab's lineage.
  - **PrefabInstance path (`--- !u!1001` block with `m_SourcePrefab.guid` matching the source) = PASS** — proven Unity prefab variant clone.
  - **Null/blank `Image.sprite` where source has one = CRITICAL FAIL** (fabrication signature — case C), logged to `.claude/review_misses.log`.
  - **Neither side has a sprite + no PrefabInstance lineage = CRITICAL FAIL** — cannot distinguish clone from scratch.
  - **No PrefabInstance lineage + same sprite GUID → live-editor structural comparison via MCP HTTP endpoint** (iter-3, replaces the blanket CRITICAL FAIL from iter-2):
    - Editor reports `STRUCTURE_MATCH` → PASS (real `AssetDatabase.CopyAsset` clone — same component types, same child count).
    - Editor reports `STRUCTURE_MISMATCH` → CRITICAL FAIL (from-scratch fabrication with a pasted GUID), logged to `.claude/review_misses.log`.
    - Editor unreachable (returns `None`) → BLOCK (fail-closed: never silently pass; ensure Unity editor is running with the MCP plugin).
  - **Different real sprite + no PrefabInstance lineage = WARN** (legal re-skin A3 — not blocking; reviewer confirms).
  - Source has no sprite + built has a real sprite + no lineage = WARN (reviewer confirms mapping is intentional).
- **Live-editor MCP seam:** `_call_live_editor(csharp_code)` and `_do_live_editor_structure_check(built_prefab_path, source_guid, element_path, repo_root)` — module-level functions. Tests monkeypatch them directly (`eid._do_live_editor_structure_check = lambda *a, **kw: "MATCH"`). Endpoint: `http://localhost:21573` (from `.mcp.json`). Handshake: POST `initialize` → read `Mcp-Session-Id` → POST `notifications/initialized` → POST `tools/call` `script-execute`. Response is SSE; parsed for `result.structuredContent.result.value`.
- **Deliverable:** implementers on reuse-mandate tasks must author `Docs/Specs/Active/<task>/reuse_map.json` at start-of-task. CopyAsset clones (no `!u!1001` blocks) are valid and PASS when the live editor confirms structural identity.
- **Iter-2 scar (corrected in iter-3):** iter-2 made the no-lineage + same-sprite-GUID branch a blanket CRITICAL FAIL requiring PrefabInstance lineage for ALL clones. This broke legitimate CopyAsset clones (same component structure, same sprite, no `!u!1001` blocks — what `AssetDatabase.CopyAsset` produces). The test suite missed this because A2's fixture was a PrefabInstance clone, not a CopyAsset clone. Iter-3 replaces the blanket FAIL with the live-editor structural comparison and adds the A2b/A2c fixtures.
- **Tests:** `TestCloneProvenanceYAML` in `test_enforce_implementer_done.py` — A1 (null-sprite), A1-mutant (MISMATCH from live editor → CRITICAL FAIL), A2 (PrefabInstance → PASS), A2b (CopyAsset + MATCH from live editor → PASS), A2c (editor unreachable → BLOCK), A3 (re-skin), A4–A5g — 15 tests total.

## 16. Shipped-asset guard — P4 (Order-611, 2026-07-06)
- **Created:** Order-611 — the `general_shop_ui` task (610) silently +68-line edited `StaminaShopSelectionScreen.prefab` (Order-517 shipped deliverable) with no SPEC mention. Rule 13 (files-modified-coverage) passed it because disclosure ≠ authorization.
- **The gate (HOOK-ENFORCED — `enforce_implementer_done.py` `validate_shipped_asset_guard`):** if the working-tree diff touches any asset listed in `Docs/Specs/SHIPPED_MANIFEST.json` AND that asset is not explicitly named in the current task's `SPEC.md`, **block the STATUS transition**.
- **Authorization:** add the asset path to `SPEC.md` (e.g. `## Files touched\n- \`Assets/Prefabs/…/Foo.prefab\` — explicit edit target for this task`). Then the gate passes.
- **Deliverable:** `Docs/Specs/SHIPPED_MANIFEST.json` (seeded with Order-517 shop+tournament deliverables). Add new shipped deliverables as tasks complete.

## 17. Observed test-run gate — P5 (Order-611, 2026-07-06)
- **Created:** Order-611 — 488 lines of unverified save/economy code reached the gate on prose alone ("tests pass" in the report without a machine-authored count).
- **The gate (HOOK-ENFORCED — `enforce_implementer_done.py` `validate_observed_test_run`):** for tasks whose SPEC.md or working-tree diff mentions `SaveData` / `SaveSchemaMigrator` / save-schema paths, require a `Total: N` / `Passed: N` line in `IMPLEMENTER_REPORT.md` — the machine-authored output of `mcp__ai-game-developer__tests-run` or the TestRunnerApi. Prose claiming tests pass is not accepted.

## 18. Measure-before-surface gate — P7 (Order-611, 2026-07-06)
- **Created:** Order-611 — the `general_shop_ui` Part 2 saga: Cesar ran QA for 20+ iterations because the implementer eyeballed layout metrics instead of measuring them first.
- **The gate (HOOK-ENFORCED — `enforce_implementer_done.py` `validate_measure_before_surface`):** for tasks that include `tolerances.json`, require `reference/<name>_ref_vs_built.png` + `reference/<name>_deltas.json` (per-element measured deltas vs tolerance) to exist before the STATUS transition. If any delta exceeds its tolerance, block.
- **Deliverable:** implementers on Figma-node card/panel tasks should author `tolerances.json` (see `Docs/Specs/TEMPLATE_tolerances.json`) and produce the overlay + deltas via `Docs/Scripts/figma_diff.py` or equivalent measurement before surfacing.

## 19. UIFidelityLinter blind-spot checks — P8 (Order-611, 2026-07-06)
- **Created:** Order-611 — two classes of visual defect were invisible to the linter until Cesar caught them by eye: (a) `TextMeshProUGUI` created with the Unity default `sizeDelta` (100×100), which silently clips/mis-centres text; (b) 9-sliced sprites whose border is smaller than the element's estimated rounded-cap radius, causing a visible kink or flat tangent.
- **The gate (ENGINE-SIDE HOOK — `Assets/Editor/UIFidelity/UIFidelityLinter.cs` `RenderHealth`):**
  - **C9 / P8a (TMP default-sizeDelta):** any `TextMeshProUGUI` on a fixed-anchor slot whose `sizeDelta` is the Unity default (100×100) → WARN `tmp-default-sizedelta`. Fix: set explicit `sizeDelta` or switch to a stretched anchor.
  - **C10 / P8b (9-slice cap-kink):** any 9-sliced `Image` whose effective corner border (in rendered px, accounting for `pixelsPerUnitMultiplier`) is less than 50% of the estimated cap radius (min-side/4) → WARN `9slice-cap-kink`. Fix: use a true stadium sprite with border ≥ half-height, or increase `pixelsPerUnitMultiplier`.
- Both surface as linter WARN findings. Because P2 re-runs the linter fresh via the live-editor MCP endpoint at the hook, any W/FAIL in the fresh run blocks the transition — so P8 blind-spots are structurally enforced by P2.
- **P2 implementation (iter-4, Order-611):** `validate_ui_lint` calls `_rerun_ui_lint_via_editor` after the cached JSON shows `fail == 0`. The live re-run calls `UIFidelityLinter.LintPrefab(prefabPath, null)` via the same `_call_live_editor` MCP seam as P1. If the fresh run returns `fail > 0`, the transition is blocked. **FAIL-CLOSED (iter-4 correction): if the fresh run cannot run (editor unreachable), the transition is BLOCKED — the cited JSON is NOT accepted as evidence (SPEC §1.3 / §0).** iter-3 shipped this fail-OPEN ("quality gate, not security gate"); both parallel reviewers flagged it as the exact self-authored-artifact trust the postmortem forbids. Tests: `TestValidateUILintLiveRerun` — cached-pass + unreachable-editor (**BLOCK**, `test_p2_cached_pass_editor_unreachable_blocks`), + the pass/fail/no-rerun cases.
