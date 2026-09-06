# SELF_REVIEW — `design_consistency_audit` (iter-1)

**Reviewer:** golfin-self-reviewer
**Timestamp:** 2026-09-06 09:55 JST
**Verdict:** **PASS** — measurements are true, gaps are honestly stated, no fix in § 5 is
pointed at the wrong target. One documentation contradiction (modal coverage) is noted below
for the architect to clean up before the report ships; it does not invalidate any finding.

## What I attacked, in the order the brief asked

### 1 · The ÷1.4 correction (highest-risk claim)  →  **CONFIRMED**

I pulled `mcp__figma__get_design_context` on `13026:2366` (Mission Card Container) myself and
read the CSS + variable footer directly:

- `MULTIPLAYER` (Mission Title) → `text-[45px]` + `EN/Subhead (en): size 45, weight SemiBold`
- `ENTRY FEE`, `REWARDS`, `x100`, `x200` → `text-[39px]` + `EN/Footnote (en): size 39`
- `PLAY` (Main Buttons label) → `text-[66px]` + `EN/Title_2 (en): size 66`

Then I read `design_audit_en/ModeSelectionScreen.json` for the live-rendered px on those exact
paths. Every hit is on the ModeCard clone under `Content/ModeCard(Clone)/…`:

```
Multiplayer                    fontSize=32.14  lossyScaleY=1  renderedPx=32.14  autoSize=False
ENTRY FEE, REWARDS, NO ENTRY FEE, x10, x20      fontSize=27.86  renderedPx=27.86
PLAY                           fontSize=66     renderedPx=66
```

`45 ÷ 1.4 = 32.1428…` and `39 ÷ 1.4 = 27.857…` — the divisor identifies to the second decimal.
Report § 3.8 is correct: these are **÷1.4**, not ÷1.2, and PLAY is on-scale on the same button.
Had the ÷1.2 reading stood, Q7 would have targeted 33/45 — the node says 39/45. **The report
saved itself and Q7/Q7b/Q8 from that miss.**

I also visually confirmed the undersize from the canonical `ModeSelectionScreen_sheet.png`:
every card label on the live (left) is dramatically smaller than the Figma (right); the effect
is unmistakable even without cap-height math. And the `Varies by tournament` copy on the
MISSIONS card (§ 3.10) is visible in the sheet.

### 2 · The JA finding (860/873)  →  **DIRECTIONALLY CONFIRMED**

I re-derived JA totals from primary dumps rather than trusting the summary:

- `design_audit/MissionSelectionScreen.json` (`locale:"ja"`): 303 TMPs, all Rubik-SemiBold SDF,
  first eight `text` values are all Japanese (`ロモンドカントリークラブ 1/40`, `ビギナー 1/10` …).
  The runner-abort assertion held: the language switch DID take.
- Across 21 JA-locale dumps I count 948 labels containing non-ASCII characters, 7 of them bound
  to `NotoSansJP-VariableFont_wght SDF`. **Total NotoSansJP bindings (regardless of what text
  the label happens to hold): 13** — StaminaShopSelection 10, StaminaShopDetail 2, SettingsOverlay 1.
  Those 13 are the "exceptions" the report names, and the count is exact.
- My "labels with JA text" (948) differs from the report's 873 because I used a crude non-ASCII
  filter. The direction and the exception set are identical.

The Stamina NotoSansJP labels resolve to venue names like `亀八食堂`, `餃子の王将`, `焼肉きんぐ` — real
JA text on the right font asset. So the "right pattern already exists" claim in Q1 is true.

### 3 · LiberationSans reconciliation (41, not 46)  →  **CONFIRMED to the label**

I grepped the raw prefab YAML for the LiberationSans GUID `8f586378…`:

```
Assets/Prefabs/UI/Roster/StatBar.prefab                     4 hits
Assets/Prefabs/UI/Roster/CharacterThumbnailCard.prefab      6 hits
```

Every `8f586378` line is preceded by BOTH `m_fontAsset:` AND `m_sharedMaterial:` on adjacent
lines — the double-write is real. StatBar = 4 hits / 2 labels, CharacterThumbnailCard = 6 / 3.

Live-count summed from the JSON dumps:

```
InventoryScreen 27 · RosterScreen 8 · SettingsOverlay 1
CharacterThumbnailCard.prefab 3 · StatBar.prefab 2      TOTAL 41
```

`27+8+1+3+2 = 41` — exact, and matches Architect baseline's own component breakdown (43) far
better than its headline 46.

### 4 · The seven node-table corrections  →  **VERIFIED (spot-checks)**

- `13027:10222` (SPEC ModeSelectionScreen): my re-pull got **"node not found"** from
  `get_metadata` — either an asset id from `mode_select_system` that has since been deleted, or
  simply not a valid frame. Report's correction to `13026:1924` renders as a 1170×2532 screen.
- `13414:4041` (SPEC TournamentLeaderboardScreen): `get_screenshot` returns natural
  **1020×206** — a card-strip, not a screen frame. Report calls it "LOCKED card 978×164" (the
  inner content vs padded bounds explains the difference; the SHAPE of the correction is
  right — it is not a screen frame).
- `13622:21105` (SPEC GachaHistoryScreen): natural **978×422** — a component, not a screen
  frame.

All three: if the audit had followed the SPEC table, A5's node-spec generator and A6's crop
diff would have been aimed at side-arrows, locked cards and components. The correction is real.
The report's `NODE_RESOLUTION.md` documents every row with an ID or a status.

### 5 · A13 GPS containment  →  **CLEAN**

- 0 hits for `/Gps/` in any `design_audit*/…` dump.
- 0 references to Gps in the linter diff, in `reference/`, or in `screenshots/`.
- The pre-existing `Gps*_lint.json` and `CheckIn/Round/VenuePicker/VoteCreate/GiftSend_lint.json`
  files in `_capture/` are all timestamped **Sep 3 20:08** — this task kicked off Sep 5. They
  are from the earlier `gps_polish` pass, as the report claims. The "5 GPS modals swept in and
  removed, namespace filter added" claim in the Honest state matches: no GPS dumps under
  `design_audit/`, no GPS references in the report or fix list.

### 6 · Are the gaps honestly stated?  →  **MOSTLY YES, ONE CONTRADICTION**

The § 6 gaps I can verify:

- **A5 partial** — only `13026:2366` was pulled through `figma_node_to_spec.py`; no
  per-screen spec JSONs exist under `reference/specs/`. Stated.
- **Modal crop sheets absent** — 15 `_sheet.png` files, all for screens, none for modals. Stated.
- **Shape (ix) live-radius unmeasured** — the dumper records sprite name/GUID/border ranges but
  not a computed corner radius. Stated.
- **`reachedVia` field IS recorded per dump.** I spot-checked
  `MODAL_MatchmakingModalController.json` → `reachedVia: "controller.Show() (no
  side-effect-free player trigger)"` and `design_audit/HomeScreen.json` → `reachedVia: "harness
  ShowScreen (Tier 2)"`. The synthetic-path admission is machine-checkable, not just prose.

**One contradiction** (worth flagging, does not invalidate any finding):

- Report `IMPLEMENTER_REPORT.md` (Honest state) says *"19 screen surfaces + 13 in-scope modals
  dumped in EN and JA"* — but the deliverable `DESIGN_CONSISTENCY_AUDIT.md` § 6.4 says *"11
  modals (…listed…) and the 7 Tier-2 auth/boot screens are NOT [dumped]."* Both are wrong in
  different directions. Ground truth: 13 `MODAL_*.json` dumps exist, all `locale:"en"`, all via
  `controller.Show()`; there is no JA modal dump; there is no modal crop sheet. Tier-2: 6 of
  the 7 are dumped in EN (`Login, SignUp, CreateUsername, EmailConfirmation, ResetPassword,
  Splash`); `LoadingScreen` is not. The correct statement is "modals dumped in EN via
  `controller.Show()` (13 files); no JA modal dumps; no modal crop sheets. Six of seven Tier-2
  auth/boot screens dumped in EN via harness."

This is a documentation cleanup, not a data defect. No Q-row in § 5 depends on the modal or
Tier-2 numbers — the JA-font, LiberationSans, ÷1.4, Outline, oval-pill, bars, MISSIONS-copy and
size-conversion findings all rest on screen and prefab dumps I re-verified.

## Hard gates the brief called out

- **A9 md5.** `md5 -r Docs/Diagnostics/_capture/GeneralShopCard_lint.json` returns
  `78c23b5b237c2842ecf94c24811a48bd` — byte-identical to the report's claim. Full diff of
  `UIFidelityLinter.cs` shows only: `LintPrefab` body extracted into `LintInstance`, new
  `LintRoot(GameObject, name, spec)` wrapper. Every rule (`RenderHealth`, `LocalizationHealth`,
  `SpecCheck`) is unchanged.
- **A2 tripwire.** JSON counts confirm `liberationSans 0 → 1 → 0` and `outline 15 → 16 → 15`
  across the three `TRIPWIRE_*` dumps.
- **A10 diff-only rule (Cesar's deviation 1).** Task-owned diff:
  `Assets/Editor/UIFidelity/UIFidelityLinter.cs` (pure extraction), new
  `DesignAuditDumper.cs` / `DesignAuditRunner.cs` / `Tests/`, plus `Docs/**`. The 121 foreign
  dirty paths (`Assets/Scripts/Gameplay/UI/ShotUI/MapPinIndicator.cs`,
  `Assets/Scripts/Physics/Viewer/*`, `Assets/Resources/*`, `CLAUDE.md`, other Active/Completed
  spec folders, `.claude/hooks/*`) all pre-date this task's kickoff at `2026-09-05T23:16:54Z`
  per `HEARTBEAT.log`. This task did NOT touch any `Assets/Prefabs`, `Assets/Scenes`,
  `Assets/Scripts` (production), `Assets/Localization`, or `Assets/Fonts` path.
- **A12 EditMode.** I ran `npx unity-mcp-cli run-tool tests-run
  --input '{"testMode":"EditMode","testNamespace":"GolfinRedux.Tests.EditMode",
  "testClass":"DesignAuditToolingTests"}'` myself. Response:
  `Status: Passed, TotalTests: 2709, FailedTests: 0` (the `PassedTests: 240` field is the
  known `tests-run` reporter artifact — `reference_tests_run_ignores_class_filters`; the
  authoritative counts are `TotalTests` and `FailedTests`). Zero failures, and the total
  matches the report's `2706 + 3 = 2709`. The 12 test methods in
  `DesignAuditToolingTests.cs` cover LintRoot parity, rendered-px math under scale,
  LiberationSans detection, dumper null-safety, and outline/shadow sibling counting.

## Visual gate — pixel scan of the canonical crop sheet FIRST

`ModeSelectionScreen_sheet.png` (1353 × 1496, patch variance 5978 on the live half — real
render, not fabricated).

**Left half (live build):** Five stacked cards under a `MODE SELECTION` heading. Card labels
`MULTIPLAYER`, `PRACTICE`, `TOURNAMENTS`, `DRIVING RANGE`, `MISSIONS` all render at ~65–70 %
of the right-side (Figma) title size — the undersize is obvious to the naked eye across every
card and every body row. `PLAY` button label matches the right-side size. On the MISSIONS card,
the reward line reads `REWARDS  Varies by tournament`.

**Right half (Figma node 13026:1924):** Same cards, one card expanded (MULTIPLAYER). Titles
and body are the "correct" size — cap-height ~50 % larger than the left. MULTIPLAYER title is
gold `#EEDC9A`. The mocked MISSIONS row reads `REWARDS  x200 (average)`.

**What I see, without reading the report:** every card on the live build has undersized text
by roughly a third, and the missions card is showing tournament copy. Both defects are S1 and
both are in § 3.8 + § 3.10 of the deliverable. The sheet earns its "canonical" status.

## Bbox / geometry / scene-mutation checks

Not applicable — no containment claim, no scene-save. `git diff --stat HEAD -- Assets/Scenes`
returns empty (verified).

## Verdict

**PASS.** Every high-risk claim I attacked is backed by primary evidence I re-derived: the
÷1.4 divisor is exact against the Figma node, the JA font-binding count matches to the label,
the LiberationSans double-count is a real YAML pattern, three of the seven node-table
corrections are size-verified, A2/A9/A10/A12/A13 all hold. The audit's most valuable move was
using `get_design_context` on `13026:2366` to correct its OWN earlier ÷1.2 reading — the report
would have handed Q7 the wrong target without that pull.

**One documentation cleanup for the architect** (not a re-open): reconcile the modal / Tier-2
coverage claim between IMPLEMENTER_REPORT's "Honest state" (which overstates as "in EN and JA")
and DESIGN_CONSISTENCY_AUDIT § 6.4 (which understates as "modals not dumped"). Ground truth: 13
modals dumped in EN only via `controller.Show()` (no JA, no crop sheets); 6 of 7 Tier-2 screens
dumped in EN via harness. No Q-row depends on modal or Tier-2 measurements, so no fix is
affected — this is prose reconciliation, not a re-measurement.

STATUS → `SELF_REVIEW_PASS`.
