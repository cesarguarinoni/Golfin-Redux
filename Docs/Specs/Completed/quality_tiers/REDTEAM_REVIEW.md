# REDTEAM_REVIEW — `quality_tiers` iter-1

**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Timestamp:** 2026-08-27 09:58 JST
**Prior state:** `READY_FOR_REDTEAM` (golfin-reviewer PASS).
**Verdict:** **ARCHITECT_REVIEW_PASS** — I attacked all seven vectors and could not break it. Every reviewer claim re-generated from primary source, not carried forward.

---

## Evidence I generated myself (not re-used)

- **EditMode tests re-run by me** via `tests-run` (fallback client): **1809 total / 1806 passed / 0 failed / 3 skipped**. The 3 skips are the pre-existing `HoleCompleteDriverTests` Stage-C1 no-ops. Matches the claimed 1809/1806/0/3 exactly.
- **QualitySettings.asset read by me:** level order + GUID mapping confirmed — `Low`(0)→`a519…`(Mobile_Low), `Mid`(1)→`ce12…`(Mobile_Mid), `High`(2)→`5e6c…`(Mobile_High), `PC`(3)→`4b83…`. `maximumLODLevel` 1/0/0/0. `lodBias` 1/1/1/2. `anisotropicTextures` 0/1/1/2. `excludedTargetPlatforms` `[Standalone]` on all three mobile, `[Android,iPhone]` on PC. `m_PerPlatformDefaultQuality` iPhone=1 Android=1 Standalone=3.
- **Three RP assets read by me:** Low `renderScale 0.6 / HDR 0 / cascade 1 / dist 15 / shadowmap 512 / soft 0`; Mid `0.7 / 0 / 1 / 40 / 1024 / 0`; High `0.8 / 1 / 2 / 60 / 1024 / 0`. All three cite the same renderer `65bc7dbf…` (Mobile_Renderer). Byte-matches the tier table.
- **`Vegetation.shader` diff (`git show 1dcb4a3d4`):** exactly **7** `#pragma shader_feature _WIND` → `#pragma multi_compile _ _WIND` pairs and **nothing else**. (SPEC undercounted at 5; 7 is the correct real count — both variants now ship, this is the mechanism, not scope creep.)
- **Screenshots viewed by me at native 1170×2532:** EN submenu reads `Auto (High)` / `High` / `Medium` / `Low` best-first with a display+gear Quality icon; JP reads `自動 (高)` / `高` / `中` / `低` in the same order with clean Rubik-family JP glyphs (no NotoSans fallback squares); `low_selected` shows Low highlighted cyan, Auto dropped to navy, and the dev HUD reading **29.9 fps / 33.4 ms** — the 30 fps Low cap is live and applied through the real `SetOverride` path.
- **Fairness treeline composite viewed by me:** all three tier bands place every tree silhouette, the far-tree cut, and the `457 yds` flag at the same X; differences are only sharpness/shadow/wind. Consistent with the reviewer's re-derived `4.9864/255` whole-frame diff.

## Seven attack vectors — why each failed

1. **`TreeWindDriver.SetEnabled(true)` (the dangerous line).** Read line-by-line: `_authoredKeyword[m]` is populated on first touch (`m.IsKeywordEnabled`), and re-enable is gated `if (enabled && _authoredKeyword[m]) EnableKeyword; else DisableKeyword` — it **restores cached authored state, never blanket-enables**. Ordering is safe: `Init()` clears the cache at `SubsystemRegistration`, which runs *before* `QualityTierService.Boot` (`AfterSceneLoad`) and before the first hole load, so the first material touch reads the fresh-from-asset authored keyword. `ApplyTierHoleEffects` re-runs `SetEnabled` on **every** hole load (`PhysicsLabController:2283`), so a hole with new tree prototypes gets its new materials cached+frozen correctly. The one residual edge — a domain reload *mid-play* re-caching an already-disabled keyword as "authored" — is editor-only (no disk persistence, no mid-session reload in a player build) and is mitigated by `TreeWindDriverEditorGuard`. Not a shipping defect.
2. **Enum↔quality-index coupling.** There IS a guard beyond the comment: `QualityTierServiceTests.ProjectHasTheFourExpectedQualityLevels_InTierOrder` asserts `QualitySettings.names[0..3] == Low/Mid/High/PC`; plus `FairnessRule_TerrainAndLodBiasAreIdenticalOnEveryTier` and `MaximumLodLevel_IsOneOnLow_ZeroOnMidAndHigh`. A Quality-window reorder trips a test. Verified the on-disk order matches. Failed to break.
3. **Shell-camera post-processing retry.** Double-subscribe is guarded by `_awaitingShellScene`; `OnSceneLoadedRetry` unsubscribes + resets the flag when ShellScene loads; `Boot()` re-arms both flags on domain reload (statics do not survive it, so the stale delegate is gone too). No leak, no double-fire. Failed to break.
4. **`PhysicsLabController` subscriptions + putter sync.** `ApplyTierHoleEffects` is a **static** method; `Awake` does `-=` then `+=` (idempotent) and `OnDestroy` does a single `-=`. Cannot double-fire. The putter-selector fix from `56eddcb92` is entirely inside new `SyncClubSelectorToPutter` / `RestoreClubSelectorAfterPutter` methods and touches none of the tier code (confirmed by reading the commit diff). Failed to break.
5. **Report honesty.** §12.6 is headed “ONE warm run per tier”, explicitly “Not the protocol … not a publishable number”. The **`-O0` caveat is present and detailed** (build 2325 archived `-configuration Debug`, 987 files unoptimised; `mainMs` flagged as inflated and not to be quoted; engine-native counters called out as unaffected). §12.7 “STILL EMPTY — the cooled protocol” is all dashes. **No cooled High number is claimed anywhere.** The on-disk `*_O3_perfbot.txt` re-measures show `thermal=Serious` (still warm) and are NOT presented as cooled results. Report is straight.
6. **Acceptance list re-run (not carried forward).** Tests, QualitySettings, 3 RP assets, shared renderer, shader diff, localization (`SETTINGS_GRAPHICS/AUTO/LOW/MID/HIGH` EN+JP), telemetry (`TelemetryHooks:104-105` `tier` + `tier_source`), submenu→`SetOverride` wiring — all re-derived from source this pass. All pass.
7. **Screenshots.** Best-first order in both languages, icon renders, 30 fps Low cap visibly applied. The dev FPS HUD overlapping the “High” row is pre-existing editor overlay noise, not a shipped element.

## Three break-attempts (Step 3)

- **Visual:** hunted for a seam/clip/missing sprite/wrong glyph at native res in all three settings shots + the fairness composite. Found none; only the editor-only dev HUD overlaps a row (not in build). FAILED to break.
- **Geometric/threshold:** every RP + QualitySettings value is an **exact** spec match (0.6/0.7/0.8, cascades 1/1/2, dist 15/40/60, LOD 1/0/0), not a near-threshold fragile value; fairness diff 4.99/255 is far inside the “same silhouettes” rule. Nothing within 20 % of a failing boundary. FAILED to break.
- **Spec-intent:** the fairness *rule* (not just the checklist) is encoded as executable tests (lodBias identical, terrain/placement/cull excluded from `ApplyTierHoleEffects`), and the subtlest correctness point (authored-keyword restore, not blanket-enable) is written correctly. Letter and intent both met. FAILED to break.

## Prior rejections

No `CESAR_REJECTION.md` in the folder — iter-1, no prior Cesar rejection to reproduce. (The `green_slope_height_bake` scar that motivated this gate is a different task.)

## Non-blocking (disclosed, not blockers — consistent with reviewer §9 and Cesar's pre-approvals)

- Device-half (cooled 3-run tables, endurance, on-device telemetry observation) correctly declared NOT DONE and explicitly out of this task's code-side acceptance; Cesar owns those + the aim-arrow play.
- `ButtonPressFeedback` absent on the 5 new Buttons — Cesar said “Leave the buttons”; the whole Settings accordion family lacks it. Settled.
- Report §6 still doesn’t narrate the submenu best-first reorder — report-accuracy nit, decision itself Cesar-approved.

**Verdict: ARCHITECT_REVIEW_PASS.** I could not find a concrete blocker on any vector after re-generating the evidence myself.
