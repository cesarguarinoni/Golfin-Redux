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
