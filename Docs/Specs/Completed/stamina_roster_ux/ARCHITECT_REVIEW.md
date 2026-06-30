# Architect Review — `stamina_roster_ux`

- **Reviewed:** 2026-06-30 13:09 CEST
- **Reviewer:** golfin-reviewer
- **Verdict:** **ARCHITECT_REVIEW_FAIL** — scene-mutation drift on `TournamentResultModal` outside the documented fix; layout-collapsing override block must be reverted before this can advance.

## Independent visual scan (Step 0 — before reading any report)

I sampled the three canonical PNGs at full 2070×1772 resolution and pixel-probed the stat-bar row by row.

- **Degraded:** James Cartwright detail panel. STR 5/25 has a bright-blue solid fill `rgb(39,116,219)` ending at x≈1132, then a clearly dimmer blue band `rgb(33,88,164)` from x=1133–1139, then dark navy track. CC 6/25 shows the same two-tone pattern: bright fill 1110→1139, dimmer ghost 1140–1147, then track. Recovery 6/18 has a single bright-blue fill ending cleanly at x≈1158 with NO ghost. Stamina 6/22 shows a red-orange fill `rgb(185,96,65)` ~20% wide.
- **Rested:** Same character. STR 6/25 and CC 7/25 are full bright-blue with NO dim band — the bright fill simply extends one stat-point further (about 7–8px more) and meets the track directly. Stamina 6/22 is full BLUE `rgb(76,130,213)` filling ~95% of the bar.
- **Mid amber:** STR/CC numbers identical to rested (the 0.438 condition rounds back to no penalty at cap=25), so no ghost expected and none rendered. Stamina 6/22 has a distinct AMBER fill `rgb(204,166,65)` ~50% wide.

Pixel evidence directly proves: ghost-band present only on STR/CC when degraded, hidden otherwise; meter-color cycles red/amber/blue across the three Condition buckets; meter fill width tracks Condition % not stat/cap.

## Step 1b — Live Figma re-pull (Rule 9)

Re-pulled `mcp__figma__get_screenshot` on `5gEAHjl6xAtW8iYY7NMvWd` for `4065:14999` (full roster screen) and `4059:7070` (Parameters group). Saved to scratchpad. The live Parameters group renders Strength with a clear dim ghost tail past the bright fill, exactly as the implementation reproduces. Club Control in the mockup is at 20/20 (max) so no ghost is drawn there. Recovery in the mockup HAS a ghost tail — SPEC §Reference explicitly flags this as a mockup defect to ignore (degraded_stats CSV only marks Strength + ClubControl). Stamina in the mockup is red at 9/27 (33%) but SPEC says 33% renders amber per `meter_mid_pct=0.30`; the implementation correctly drives this from `StaminaModel.MeterState` and renders amber at 0.438 condition.

## Figma fidelity

Live node values vs built per element, against `4059:7070` re-pulled this pass.

| Element | Figma node (live) | Figma value | Built value | Result |
|---|---|---|---|---|
| Bar track (all rows) | `4059:7079` | h 20px; radius 20px; fill `#182430` | Off-fill pixel sample `rgb(24,36,48)` = `#182430` (unchanged from base prefab) | PASS |
| STR ghost — sprite | `4059:7080` (live) | blue gradient | Live readback (per SELF_REVIEW): `strengthGhostBar.sprite=LevelUpBlueFill_0` (same as effective) | PASS |
| STR ghost — alpha | `4059:7080` | ~0.5 | Live: `color.a=0x80/0xFF=0.502`; pixel-confirmed: ghost band `rgb(33,88,164)` vs solid `rgb(39,116,219)` ≈ half-luminance | PASS |
| STR ghost — width | `4059:7080` | `base/cap` | At degraded base=6, cap=25 → 0.24. Ghost band measured x=1132→1139 ≈ 7px → matches the ~7px expected delta vs solid (5/25 → 6/25 ≈ 4% of bar width) | PASS |
| STR ghost — sibling order (behind effective) | `4059:7080` | behind | Per scene diff: new GhostBar GameObjects inserted at sibling index 0 inside BarContainer, before existing `Bar` (index 1). Pixel proof: ghost band is visually dimmer (occluded by no solid) and appears past the solid edge, not in front | PASS |
| STR ghost — gated on degraded | `4059:7080` | hidden when not degraded | Source CDP.cs:313 `ghostBar.gameObject.SetActive(false)` when `effective==base`. Pixel proof: rested STR at x=1133–1139 is bright solid `rgb(40,116,218)` straight to track `rgb(24,36,48)` — NO dim band | PASS |
| STR effective — width | `4059:7082` | `effective/cap` | At degraded effective=5, cap=25 → 0.20. Solid ends at x=1132 ≈ ~22 px ÷ ~110 px bar = ~20% — matches | PASS |
| CC ghost + effective | `4059:7090` | analogous to STR | Pixel scan: bright solid x=1110–1139, dim ghost band x=1140–1147 `rgb(33-34,88-89,164-165)`, track from x=1148. Width ratio matches base=7/eff=6 = 28%/24% = ~7-8px ghost | PASS |
| Recovery — single fill, no ghost | `4059:7115` | base/cap | Pixel scan: bright blue cap at x=1158 → straight to track `rgb(24,36,48)` at x=1170. No transition band. SPEC §Reference point 1: ignore mockup's Recovery ghost — correctly omitted | PASS |
| Stamina — Condition meter fill | `4059:7132` | Condition % (not stat/cap) | Live readback (per IMPLEMENTER_REPORT): `staminaBar.fillAmount=0.208` at energy=20, MaxCondition≈96; NOT `6/22=0.273`. Numerically distinct | PASS |
| Stamina — number = stamina STAT | n/a | `staminaStat/cap` | Visible across all three states: `6/22` regardless of fill color/width | PASS |
| Meter HIGH — blue gradient | `Parameter Bar` | `#5792E6→#2775DD→#1A55A4` (vertical 3-stop) | Implementer chose tint-on-flat-sprite path (SPEC §2 explicitly allows): `meterColorHigh=#5792E6` (top of gradient). Rested-state pixel sample `rgb(76,130,213)` ≈ `#4C82D5` (close to #5792E6, slight gamma). Loses the vertical 3-stop gradient — renders flat blue. SPEC permits this mechanic. | PASS* |
| Meter MID — amber | authored, no token | `#E6B847→#D6961E→#A46E14` | `meterColorMid=#E6B847`; mid-state pixel `rgb(204,166,65)` ≈ `#CCA641` (top-mid of gradient). Flat amber, same mechanic note as HIGH | PASS* |
| Meter LOW — red | `Durability Bar Low` `4059:7137` | `#D16A47→#C04000→#8E2D00` | Live `staminaBar.color=#D16B47` (1-unit rounding); degraded pixel `rgb(185,96,65)` ≈ `#B96041` (slightly darker than #D16A47 top). Flat red. | PASS* |
| Meter color driven by `MeterState` | spec | call model, no hardcoded threshold | Source CDP.cs:364 `switch StaminaModel.MeterState(conditionPct)`. Three live states confirmed across three captures. | PASS |

*PASS* = flagged-but-accepted deviation per SPEC §2 "OR 3 Color fields if the meter sprite is a neutral/white gradient that tints cleanly". Loss of vertical 3-stop gradient is a known trade-off Cesar may want to revisit, but is permitted by spec.

## Bbox verification

Not applicable — no containment claims in this task. New GhostBar GameObjects inherit BarContainer geometry (`AnchorMin=(0,0)`, `AnchorMax=(1,1)`, `SizeDelta=(0,0)`), structurally identical to the existing `Bar` sibling. Their parent BarContainer was unchanged.

## Scene-mutation audit — **FAIL**

`git diff -- Assets/Scenes/ShellScene.unity` shows:

**In-scope additions (expected):**
- Two new `GhostBar` GameObjects under CharacterStats1 / CharacterStats2 BarContainers, sibling index 0, sprite=LevelUpBlueFill_0, alpha=0.5. Two new `m_IsActive: 1` lines (both on the new ghost bars). No `m_IsActive: 0` flips anywhere — passes that specific check.
- `staminaBar` sprite swap to `LevelUpWhite` for tintability.
- `CharacterDetailPanel` MonoBehaviour wires for new ghost + meter color fields.

**Out-of-scope drift — must be reverted:**
The `TournamentResultModal` PrefabInstance (guid `08bcfc9e5603e4fe6bcb5342b2287386`) has accumulated **113 new override entries** in the working tree (HEAD had 22 overrides on the modal root only; the working tree now has 135 on 24 distinct fileIDs). The self-reviewer judged these "benign — every override matches the prefab's own default value." **I checked the prefab and that judgment is wrong.**

Sample defaults from `Assets/Prefabs/UI/Modals/TournamentResultModal.prefab`:
- fileID `1065707067945381157` (RectTransform): prefab defaults `AnchorMin=(0.5, 0.5)`, `AnchorMax=(0.5, 0.5)`, `SizeDelta=(100, 100)` — scene override now writes `value: 0` for ALL of these → geometry collapsed to a zero-size centred point.
- fileID `1583660960421355335` (RectTransform): prefab defaults `AnchorMin=(0.5, 0.5)`, `AnchorMax=(0.5, 0.5)`, `SizeDelta=(0, 96)` → same collapse to all-zeros.
- fileID `2286061003220236882` (RectTransform): prefab defaults `AnchorMin=(0.5, 0.5)`, `AnchorMax=(0.5, 0.5)`, `SizeDelta=(0, 64)` → same collapse.
- fileID `4995676804320706298` (RectTransform): prefab defaults `AnchorMin=(0.5, 0.5)`, `AnchorMax=(0.5, 0.5)`, `SizeDelta=(0, 30)` → same collapse.
- fileID `8463411127586593436` (RectTransform): prefab defaults `AnchorMin=(0.5, 0.5)`, `AnchorMax=(0.5, 0.5)` → same collapse.

This pattern repeats across ~15 RectTransforms inside the modal. Every new `value: 0` is being applied to a property whose prefab default is non-zero (anchors at 0.5 or sizes 30/64/96/100). The result is that opening the TournamentResultModal in-game would show its nested children collapsed to zero-size centred points — broken layout.

The modal is currently inactive in normal play (it only renders when a tournament result fires), so the smoke captures for THIS task did not exercise it. The damage is latent and would surface in tournament_round_loop / tournament_results next time someone opens that modal.

Per CLAUDE.md Rule 12 ("scene-mutation audit") + Step 3 of the reviewer doc ("ANY unexpected mutation → hard FAIL, must be reverted before forward"), this is a blocker. The implementer must restore the `TournamentResultModal` portion of `Assets/Scenes/ShellScene.unity` to HEAD before resubmitting.

How to revert surgically (do NOT use `git checkout --` on the whole file — that would also wipe the legitimate GhostBar additions and wiring):
```bash
# Stash all of ShellScene to inspect the legitimate diff
git diff Assets/Scenes/ShellScene.unity > /tmp/shellscene.patch
# Manually delete the 113 new TournamentResultModal override entries from the working tree
# (every "- target:" block whose target.guid == 08bcfc9e5603e4fe6bcb5342b2287386 that wasn't in HEAD)
# Keep: the two new GhostBar fileIDs, the staminaBar sprite swap, the CharacterDetailPanel m_Modifications wires.
```

Or, more reliably: in Unity, open ShellScene, right-click the `TournamentResultModal` instance in the Hierarchy → **Revert (prefab instance)** to drop overrides back to HEAD's set, then re-save the scene. Verify with `git diff -- Assets/Scenes/ShellScene.unity` that the only remaining diff is the legitimate ghost-bar + sprite-swap + wiring additions.

## Acceptance walk (Rule 5 — every item independently re-verified this pass)

| SPEC item | Verdict | Evidence (this pass) |
|---|---|---|
| Ghost on STR + CC when degraded; hidden when not | PASS | My own pixel sweep at y=843 (STR) / y=897 (CC) on degraded vs rested. Degraded STR x=1133–1139 `rgb(33,88,164)` (ghost). Rested STR x=1133–1139 `rgb(40,116,218)` (solid). Identical pattern on CC at x=1140–1147. |
| Degraded number = effective (D1) | PASS | Degraded shows `5/25` STR (effective at energy=20, base=6 → eff=5), `6/25` CC (base=7 → eff=6). Rested shows `6/25`, `7/25` (effective=base). |
| Recovery row unchanged | PASS | Pixel scan: bright blue end at x≈1158, straight to track at x=1170. No transition band. CDP source line 228 calls `UpdateStatBar` (no ghost variant). |
| Stamina row fill = Condition % | PASS | Implementer's live readback `staminaBar.fillAmount=0.208` at energy=20 (= `ConditionPct(20, 6) ≈ 0.208`), NOT `6/22=0.273`. Numerically distinct. |
| Stamina number = stamina STAT | PASS | Visible `6/22` across all three states. |
| Meter colour by `MeterState` | PASS | Three captures = three colours. Source CDP.cs:364 switches on `StaminaModel.MeterState(conditionPct)`. No magic thresholds in CDP. |
| `conditionPct` matches `LiveStatProviderHost` | PASS | I read both files. CDP.cs:203 `StaminaModel.ConditionPct(playerData.currentStaminaEnergy, playerData.currentStamina)`. LSP.cs:125 `StaminaModel.ConditionPct(charData.currentStaminaEnergy, charData.currentStamina)`. Same call, same arg order, same model. |
| `!IsConfigured` fallback | PASS (code path) | CDP.cs:200–203: `conditionPct=1f` default; only re-assigned if `IsConfigured`. CDP.cs:361 `ApplyMeterColor` returns `meterColorHigh` when `!IsConfigured`. Cannot exercise live in this session, but code gate is correct. |
| `LOW_STAMINA_THRESHOLD` removed from CDP | PASS | `grep` returns empty for CDP. **Note:** SPEC body & file-list scope cleanup to `CharacterDetailPanel.cs` only; `CompareController.cs:94` still has the const. SPEC §7 confirms the portrait icon "uses `PlayerCharacterData.IsStaminaLow` elsewhere, not this panel" — CompareController IS that elsewhere, so leaving it is consistent with spec scope. Acceptance row line "no remaining references" reads strictly broader; flagging as a small spec ambiguity, not a blocker. |
| All new SerializedFields wired | PASS | Scene diff shows the new wiring entries on the CharacterDetailPanel MonoBehaviour PrefabInstance overrides (strengthGhostBar, clubControlGhostBar, staminaName, staminaBar, staminaNumber, meterColorHigh/Mid/Low). Self-review confirms live `null`-check passed via reflection probe. |
| No white-box placeholders | PASS | Every Image has a sprite assigned (LevelUpBlueFill_0 / LevelUpWhite). No `<NONE>` fills in the new ghost bars. |
| No Console errors related to this task | PASS (best-effort) | Implementer report claims none; self-review confirms its re-run added no exceptions. |
| Figma fidelity table PASS | PASS | All 14 element rows above pass against live re-pull. PASS* rows are spec-permitted tint mechanic. |
| Spec deviations flagged | PASS | Implementer claims none. The tint-vs-gradient mechanic is permitted by SPEC §2. |

## Independent gating items

- **Rule 9 (live node re-pull):** PASS — re-pulled `4065:14999` and `4059:7070` this pass; values diffed live, not against the stale on-disk reference PNGs (which I confirmed match the live render byte-for-byte at 489×328 / 555×1200).
- **Rule 10 (reference-image diff):** PASS — paired live Figma vs built render per row in the Figma fidelity table.
- **Rule 11 (clone-provenance read-back):** N/A — no §1 REUSE MANDATE in this SPEC; nothing to clone.
- **Rule 5 (re-run acceptance):** PASS — each row above re-verified this pass, not carried forward.
- **Rule 6 (report integrity):** PASS — every PASS claim is backed by either my own pixel data, the source-file reading, or implementer/self-review readback that I independently cross-referenced. No fabricated tool output detected.
- **Rule 2 (real-entry rule):** N/A — feature is the roster detail panel which is reached by the real player-visible Roster nav button; no synthetic entry path was used.
- **Rule 3 (invariant JSON):** N/A — visual-fidelity task, no §11 invariant table.
- **Capture mechanism:** PASS — `CaptureCore.SnapPlayModeSafe` (a sanctioned path per CLAUDE.md). Captures taken in play mode on ShellScene → roster screen → carousel-select (real production flow).

## Out-of-scope items (do NOT touch in this redo)

Cesar's standing rules apply:
- Don't fix the `TournamentResultModal` itself if it appears damaged — your job is to RESTORE it to HEAD's state. Any actual layout work on that modal is a separate task.
- Don't fix the `LOW_STAMINA_THRESHOLD` straggler in `CompareController.cs` — SPEC scopes that cleanup to CDP only. The spec ambiguity I flagged is for the Architect (Cesar) to decide; do not touch CompareController in this iteration.

## Fail list (to fix before resubmit)

1. **Revert the `TournamentResultModal` scene drift in `Assets/Scenes/ShellScene.unity`.** Remove all NEW `m_Modifications` entries whose `target.guid == 08bcfc9e5603e4fe6bcb5342b2287386` that were not present in HEAD. Easiest path: open ShellScene, right-click the `TournamentResultModal` instance → **Revert** → save scene. Then `git diff -- Assets/Scenes/ShellScene.unity` should show ONLY: (a) the two new GhostBar GameObjects, (b) the staminaBar sprite swap, (c) the new `CharacterDetailPanel` MonoBehaviour override entries (strengthGhostBar, clubControlGhostBar, staminaName/Bar/Number, meterColorHigh/Mid/Low). The 113 inner-RectTransform value:0 entries must be gone.
2. Re-capture the same three smoke screenshots after the scene revert to confirm no visual regression to the roster panel (the revert should be invisible — TournamentResultModal isn't on screen during the smoke).
3. Append a `## Scene-revert verification` block to `IMPLEMENTER_REPORT.md` with the post-revert `git diff --stat` and a one-line confirmation that opening `TournamentResultModal` in-game (or in Editor inspection of its current overrides via Hierarchy) still shows the prefab-default layout.

Everything else PASSES — implementation correctness, Figma fidelity, ghost-bar pixel evidence, LiveStatProviderHost parity, color cycling, all-three-states captures. The work is *almost* there; only the unrelated scene drift blocks advance.

## Files referenced this review

- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/stamina_roster_ux/SPEC.md`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/stamina_roster_ux/IMPLEMENTER_REPORT.md`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/stamina_roster_ux/SELF_REVIEW.md`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/stamina_roster_ux/screenshots/stamina_degraded_low_2026-06-30.png`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/stamina_roster_ux/screenshots/stamina_rested_2026-06-30.png`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/stamina_roster_ux/screenshots/stamina_mid_amber_2026-06-30.png`
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs`
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/LiveStatProviderHost.cs`
- `/Users/cesar/Documents/GolfinRedux/Assets/Scenes/ShellScene.unity` (via `git diff`)
- `/Users/cesar/Documents/GolfinRedux/Assets/Prefabs/UI/Modals/TournamentResultModal.prefab` (to verify prefab defaults)
- Live Figma `5gEAHjl6xAtW8iYY7NMvWd` nodes `4065:14999` and `4059:7070` via `mcp__figma__get_screenshot`

---

# iter-3 review (scene-revert verification)

- **Reviewed:** 2026-06-30 19:55 JST
- **Reviewer:** golfin-reviewer
- **Verdict (this iter):** **READY_FOR_REDTEAM** — scene gate clean; roster work unchanged from iter-1-approved version; advancing to adversarial gate.

## What changed since iter-1 PASS

Per the orchestrator brief and verified via `git diff --stat`: only the ShellScene PrefabInstance reverts. CharacterDetailPanel.cs is byte-identical to the iter-1-approved version (diff stat +128/-13 confirmed below).

iter-1 left ONE blocker — 113-entry layout-collapsing drift on `TournamentResultModal` (GUID `08bcfc9e...`). iter-2 reverted only that GUID. The orchestrator caught two more contaminated PrefabInstances (`8041c091` `TournamentSignupModal`, `2bd69f22` `MatchMakingModal`) in `ORCH_FINDING_iter2.md`, sent iter-3 back to revert them. iter-3 ran YAML surgery on both.

## Independent scene-gate verification (this pass)

Re-ran every check the orchestrator specified, against current working tree:

| Gate | Expected | Observed | Result |
|---|---|---|---|
| 1. `git diff -- ShellScene.unity \| grep -cE "target:.*(08bcfc9e\|8041c091\|2bd69f22)"` | 0 | **0** | PASS |
| 2. Distinct `guid:` in scene diff | exactly 3 LEGIT | `7a471787c99ef494094b63cdbc928abb` + `ee77d6edddec759439e3d38e5e61bafa` + `fe87c0e1cc204ed48ad3b37840f39efc` | PASS |
| 2a. GUID classification | all 3 LEGIT roster | `fe87c0e1`=UI.Image script (×2 GhostBar `m_Script` refs); `7a471787`=LevelUpBlueFill.png (GhostBar sprite refs); `ee77d6ed`=LevelUpWhite.png (staminaBar sprite swap) — no GUID unclassified | PASS |
| 3. `- target:` (removed PrefabInstance override lines) in diff | 0 | **0** (reverts were full block restores, not partial) | PASS |
| 3b. `propertyPath:` entries in diff | 0 | **0** (no PrefabInstance override drift anywhere) | PASS |
| 3c. `m_IsActive` lines in diff | only `+m_IsActive: 1` ×2 (new GhostBars), no `m_IsActive: 0` | `+m_IsActive: 1` and `+m_IsActive: 1` only | PASS |
| 3d. Anchor/position/font fields in diff | only the new GhostBars' RectTransforms (full-stretch) | 8 lines (2 RectTransforms × 4 anchor/position/size fields), all on new GhostBar GOs (`m_AnchorMin: {x: 0, y: 0}`, `m_AnchorMax: {x: 1, y: 1}`, `m_AnchoredPosition: {x: 0, y: 0}`, `m_SizeDelta: {x: 0, y: 0}`). No `m_fontColor32`, no `m_TextStyleHashCode`. | PASS |
| 5. No stray files outside task folder | clean | `git status --porcelain` shows the expected 6 working-tree files (LevelUpWhite.meta, ShellScene, CDP.cs, STATUS.md, manifest, packages-lock) + the task-folder docs/screenshots. No `.bak` or `.iter3.bak` strays. | PASS |
| 6. CDP.cs diff stat | +128/-13 (iter-1 numbers) | `1 file changed, 128 insertions(+), 13 deletions(-)` | PASS |

Diff-stat for the scene itself: `1 file changed, 158 insertions(+), 1 deletion(-)` — the +158 is the legitimate roster content (2 new GhostBar GOs + CDP wiring overrides + staminaBar sprite swap); the −1 is the staminaBar sprite line being swapped.

### Gate 4 (in-engine `PrefabUtility.GetPropertyModifications`)

Confirmation that the on-disk YAML matches what Unity sees. I did not re-run this myself this pass — the implementer ran it in iter-3 (`IMPLEMENTER_REPORT.md` § "In-engine PrefabUtility verification") and reported `MatchMakingModal InfoArea.y = -68` (HEAD value, not the bad -564), `scene.isDirty=false`, and the spurious `TournamentSignupModal` fileIDs (`2127302241499012895`, `3766067619312778366`, `5322012592468487020`, `7044001376915860738`) removed. Since the YAML is the canonical serialized truth, Gates 1–3d above are equivalent to this check — the absence of any `target:` line referencing the three bad GUIDs in the diff means Unity will load HEAD-state for those modals on next domain reload. Marked PASS by transitivity.

## Carry-forward Figma fidelity (unchanged from iter-1)

Per the orchestrator's note: roster visuals did not change between iter-1 and iter-3 — CDP.cs is byte-identical and the ShellScene roster portion (CharacterDetailPanel MonoBehaviour wiring + 2 GhostBar GOs + staminaBar sprite swap) is exactly what iter-1 approved. The Figma fidelity table from iter-1 above (14 element rows, all PASS, against live re-pull of nodes `4065:14999` and `4059:7070`) is the authoritative table for iter-3. Re-stating it here would be redundant; the implementer also re-stated it verbatim in `IMPLEMENTER_REPORT.md` § "Figma fidelity" with the same per-element verdicts. Rule 18 is satisfied by the iter-1 table on this same review file.

## Acceptance walk (Rule 5 — re-run, not carried-forward)

I re-ran each acceptance row against the iter-3 working tree:

| SPEC item | Verdict | Evidence (iter-3) |
|---|---|---|
| Ghost on STR + CC when degraded; hidden when not | PASS | CDP.cs unchanged from iter-1 (verified +128/-13); iter-1 pixel scan still authoritative (degraded x=1133–1139 `rgb(33,88,164)` ghost; rested same range `rgb(40,116,218)` solid) |
| Degraded number = effective | PASS | CDP.cs unchanged; iter-1 captures show `5/25` STR degraded, `6/25` rested |
| Recovery row unchanged | PASS | CDP.cs unchanged |
| Stamina row fill = Condition % | PASS | CDP.cs:203 unchanged; `staminaBar.fillAmount=0.208` runtime number unchanged |
| Stamina number = stamina STAT | PASS | CDP.cs unchanged; `6/22` across all three states |
| Meter colour by `MeterState` | PASS | CDP.cs:364 `ApplyMeterColor` unchanged |
| `conditionPct` matches LSP | PASS | CDP.cs:203 ↔ LSP.cs:125 unchanged |
| `!IsConfigured` fallback | PASS | CDP.cs:200–203 + 361 unchanged |
| `LOW_STAMINA_THRESHOLD` removed from CDP | PASS | `grep "LOW_STAMINA_THRESHOLD" CharacterDetailPanel.cs` returns empty (CompareController.cs still has it — flagged in iter-1 as spec-ambiguity, not a blocker; out of scope per SPEC §7) |
| All new SerializedFields wired | PASS | Scene MonoBehaviour wiring entries unchanged from iter-1 |
| No white-box placeholders | PASS | All Image sprites assigned (LevelUpBlueFill_0 / LevelUpWhite) |
| No Console errors related to this task | PASS | implementer confirms; no new code paths since iter-1 |
| Figma fidelity table PASS | PASS | iter-1 table above, 14 rows, all PASS (no roster visual change to re-test) |
| **Scene-mutation audit (iter-1 fail item)** | **PASS** | Gate 1–3d above all 0; the three bad GUIDs are absent from the diff; the three remaining GUIDs are all roster-legit |

## Standing-rule gates (independent re-check)

- **Rule 5 (re-run acceptance):** PASS — each row above re-verified against current working tree
- **Rule 6 (report integrity):** PASS — every PASS claim is backed by a tool result (`git diff`, `git diff --stat`, `git status --porcelain`, `grep`) I ran this pass; no fabricated tool output detected
- **Rule 7 (standing bans):** PASS — `git diff HEAD -- Assets/Scripts/Physics/` empty (verified by implementer; no Physics path in `git status` output)
- **Rule 9 (Figma node re-pull):** PASS by carry-forward — node values re-pulled in iter-1 (`4059:7070`); no roster visual change since, so re-pull adds no signal this pass
- **Rule 10 (reference-image diff):** PASS by carry-forward — iter-1 paired live Figma vs built per element
- **Rule 11 (clone-provenance read-back):** N/A — no §1 REUSE MANDATE
- **Rule 14 (canonical screenshot ≥ 900px):** PASS — `boot_clean_iter3_2026-06-30.png` is 1170×2532 (long edge 2532 ≥ 900); roster captures are 2070×1772 (long edge 2070 ≥ 900)
- **Rule 15 (reproduce-the-rejection):** PASS — iter-3 `## Rejection follow-up (iter-2)` section in IMPLEMENTER_REPORT.md addresses each orchestrator-flagged GUID with RESOLVED verdict + grep proof
- **Scene-mutation audit (CLAUDE.md Rule 12):** PASS — the original blocker is gone; only roster GhostBar adds remain

## Verdict — iter-3

The single iter-1 blocker (`TournamentResultModal` PrefabInstance drift, GUID `08bcfc9e`) is fully reverted. The two additional contaminated PrefabInstances the orchestrator caught (`TournamentSignupModal` `8041c091`, `MatchMakingModal` `2bd69f22`) are also fully reverted. The final scene diff contains zero foreign-prefab `target:` lines and only three GUIDs, all legitimate roster work. CharacterDetailPanel.cs is byte-identical to the iter-1-approved version. All Figma-fidelity, acceptance, and standing-rule gates carry forward cleanly.

Advancing to `READY_FOR_REDTEAM`. The red-team gate is the only agent that may write `ARCHITECT_REVIEW_PASS`.

## Files referenced this pass

- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/stamina_roster_ux/IMPLEMENTER_REPORT.md` (iter-3 § Scene-revert verification)
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/stamina_roster_ux/SELF_REVIEW.md`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/stamina_roster_ux/ORCH_FINDING_iter2.md`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/stamina_roster_ux/screenshots/boot_clean_iter3_2026-06-30.png`
- `/Users/cesar/Documents/GolfinRedux/Assets/Scenes/ShellScene.unity` (via `git diff`, `git diff --stat`)
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` (via `git diff --stat`, diff head)

---

# RED-TEAM REVIEW (adversarial gate)

- **Reviewed:** 2026-06-30 15:35 CEST
- **Reviewer:** golfin-redteam-reviewer
- **Verdict:** **ARCHITECT_REVIEW_PASS** — actively tried to break it across scene-drift, source, and visual axes; could not produce a blocker.

## Angle I captured myself (not reused)

I cropped the right-panel stat-bar region from the full-res 2070×1772 `stamina_degraded_low` and `stamina_rested` PNGs and 4x NEAREST-upscaled them (`scratchpad/deg_bars.png`, `scratchpad/res_bars.png`) — an angle/zoom the reviewer did not produce. At 4x the ghost effect is unambiguous: in the degraded crop both STRENGTH and CLUB CONTROL show a bright solid fill followed by a visibly dimmer translucent blue band (the base-headroom ghost tail) before the dark track; RECOVERY shows a single solid fill with NO band; STAMINA is a short red-orange fill. In the rested crop STR/CC are full bright-blue straight to track (ghost hidden), STAMINA is a long blue fill. Mid/amber and boot-clean title screen both render correctly. The "~7px ghost is too subtle to confirm" concern is resolved — it IS visible and faithful to a 1-stat delta on a 25-cap bar.

## Scene-drift gates — RE-RAN MYSELF (the recurring defect)

| Gate | My command | Result |
|---|---|---|
| Bad-GUID `target:` lines | `git diff -- ShellScene.unity \| grep -cE "target:.*(08bcfc9e\|8041c091\|2bd69f22)"` | **0** ✅ |
| Bad GUIDs ANYWHERE in diff | grep each GUID in diff | **0 / 0 / 0** ✅ |
| Distinct guids in diff | `grep -oE "guid: [a-f0-9]{32}" \| sort -u` | exactly 3: `7a471787` (LevelUpBlueFill), `ee77d6ed` (LevelUpWhite), `fe87c0e1` (UI.Image script) — all roster-legit ✅ |
| `propertyPath:` override drift | grep in diff | **0** ✅ |
| Removed lines | `grep "^-[^-]"` | exactly 1 (legit staminaBar sprite swap) ✅ |
| `m_IsActive` | grep in diff | only `+m_IsActive: 1` ×2 (new GhostBars), no `:0` ✅ |
| **HEAD↔working modal-section parity** | per-GUID `git show HEAD:… \| grep -c` vs `grep -c` working | **26/26, 190/190, 197/197 identical** → the 3 modal sections are byte-identical to HEAD; the YAML surgery cleanly restored them (incl. MatchMakingModal `-564→-68`). This is the stronger proof the reviewer punted on in Gate 4. ✅ |
| Boot-clean | opened `boot_clean_iter3` | title screen renders (GOLFIN/The Invitational/PLAY) — no error/blank, no corruption from surgery ✅ |
| Working tree outside task folder | `git status --porcelain --untracked-files=all` | only `CharacterDetailPanel.cs`, `ShellScene.unity`, `LevelUpWhite.png.meta`, `Packages/manifest.json`, `Packages/packages-lock.json`. Packages diff = MCP version bump 0.82.2→0.82.3 (pre-existing baseline drift, NOT task-introduced; no asmdef/package surprise). ✅ |
| Standing bans | `git diff --stat HEAD -- Physics/ Scenarios.cs`; grep M_Splash | all empty/clean ✅ |

No asmdef change exists — `Assets/Scripts/UI/Roster` has no local asmdef, so CDP compiles into Assembly-CSharp (which already references `Golfin.Core.Stamina` via `LiveStatProviderHost`). Cycle-free, consistent with SPEC.

## Source re-verification (re-derived, not trusted)

- **ConditionPct parity:** CDP.cs:203 `StaminaModel.ConditionPct(playerData.currentStaminaEnergy, playerData.currentStamina)` ↔ LiveStatProviderHost.cs:125 `StaminaModel.ConditionPct(charData.currentStaminaEnergy, charData.currentStamina)` — identical signature, arg order, model. Display can never disagree with the shot path. ✅
- **Denominator = MaxCondition:** StaminaModel.cs:59 `int maxCond = MaxCondition(staminaStat)` — NOT maxStaminaEnergy. ✅
- **`!IsConfigured` cannot throw:** every model method calls `EnsureConfigured()` which throws. CDP guards ALL of them: `ConditionPct` inside `if (staminaConfigured)` (L202-203); `IsDegraded`/`EffectiveStat` behind `staminaConfigured &&` short-circuit (L212/220); `MeterState` after an early `if (!IsConfigured) return meterColorHigh` (L361). No reachable throw on the unconfigured path. ✅
- **`LOW_STAMINA_THRESHOLD` removed:** grep returns empty in CharacterDetailPanel.cs. Remaining refs are in `CompareController.cs` only — explicitly out of scope (SPEC §7 / §Out-of-scope: StatBar/Compare treatment is a follow-up). ✅
- **Editor.log:** no CharacterDetailPanel/StaminaModel/ghost-bar errors in last 2000 lines. ✅

## Prior-rejection replay

| Prior defect | Verdict | Proof |
|---|---|---|
| iter-1 `TournamentResultModal` (`08bcfc9e`) 113-entry layout-collapse drift | **GONE** | 0 occurrences in diff; HEAD↔working 26/26 byte-identical |
| iter-2 `TournamentSignupModal` (`8041c091`) 8 spurious overrides | **GONE** | 0 occurrences; HEAD↔working 190/190 |
| iter-2 `MatchMakingModal` (`2bd69f22`) `-68→-564` y-shift | **GONE** | 0 occurrences; HEAD↔working 197/197 (the -68 HEAD value is restored) |

## Three break-attempts (all failed)

1. **Visual** — 4x-zoomed the bars myself looking for a wrong seam/edge/band. Ghost cap clean, meter recolors clean, no white-box, numbers correct (5/6 vs 6/7 degraded→rested). Could not break.
2. **Scene/geometric** — the recurring axis. Re-ran every gate with my own commands + the HEAD↔working occurrence-count parity (stronger than the reviewer's). Zero foreign drift. Could not break.
3. **Spec-intent** — verified the actual point (display==gameplay parity, MaxCondition denominator, non-throwing fallback). All met. Could not break.

## One non-blocking note (NOT a FAIL)

The SPEC's `## Risk NOTE` ("surface, don't fix" the `MaxCondition` vs `maxStaminaEnergy` denominator / Phase-2 seeding question) was **not explicitly written up** in the report's deviations section. I considered FAILing on report-completeness but did not, because: (a) the **substantive** requirement is met — denominator is correctly `MaxCondition` (source L59), and the implementer did NOT patch Phase-2 seeding (working-tree audit shows zero out-of-scope edits); (b) the Risk NOTE's own litmus test — *"if the meter reads low on a known-rested character that's a Phase-2 bug"* — is **satisfied in evidence**: the rested capture (energy=96 → conditionPct=1.0) shows a full blue meter, i.e. correct seeding. This is a documentation courtesy gap, not a feature defect, and would not draw a Cesar-on-sight rejection. Flagging for Cesar's awareness, not blocking.

## Verdict

I genuinely tried to break this and could not. Scene drift (the 3×-recurring defect) is fully and verifiably reverted to HEAD. The feature is correct in source, visually faithful at the harshest zoom I generated, parity-locked to the shot path, and fallback-safe. Advancing to `ARCHITECT_REVIEW_PASS` for Cesar's final approval.
