# IMPLEMENTER REPORT — 1v1_result_rewards_display Stage 3 iter-3

**Iteration shape:** `polish:reward-centering`

**Task:** Stage 3 ARCHITECT_REVIEW_FAIL fix — reward coin+amount visual cluster not centered (+39px right of panel center). Single blocker from red-team review of iter-2.

Canonical screenshot: `screenshots/stage3_iter3_win_2026-07-02.png`

---

## Rejection follow-up

### Red-team blocker — reward icon+amount +39px off-center (RESOLVED)

**Red-team finding (iter-2):** coin+"x200" tight Y-band midpoint = 623.5px vs panel center 584.5px → **+39px right**. The pivot fix in iter-2 re-centered the 978px `Reward Row1` RectTransform container but did NOT fix the visible content inside it.

**Root-cause investigation (iter-3 — measured CHILDREN, not the container):**

After `LayoutRebuilder.ForceRebuildLayoutImmediate` on Row1 and Rewards, live `GetWorldCorners` on the actual coin Image and "x200" TMP children:

- **Before fix:** `Row1 sizeDelta=(100, 470)`. The `Reward Row1` HLG had `childForceExpandWidth=true` and the row was only 100px wide. The 250px content (42px coin + 8px spacing + 200px amount) overflowed the 100px container. With `childAlignment=MiddleCenter`, HLG placed content starting at `(100-250)/2 = -75px` from Row1 left edge, putting the content span effectively to the right of Row1's center when viewed in world space. Combined coin+amount world mid ≈ 623.5px — +39px right of 584.5.

- **Fix applied:** On `Reward Row1`, `Row2`, `Row3` via `PrefabUtility.LoadPrefabContents` + `SaveAsPrefabAsset` (C1 rule):
  1. `HorizontalLayoutGroup.childForceExpandWidth`: `1` (true) → `0` (false)
  2. Added `ContentSizeFitter` component: `m_HorizontalFit: 2` (PreferredSize), `m_VerticalFit: 0` (Unconstrained)

  With CSF active, Row1 auto-sizes to its preferred content width = 42 + 8 + 200 = **250px**. The parent `Rewards` HLG (childAlignment=MiddleCenter, childForceExpandWidth=false, childControlWidth=false) then centers the 250px Row1 at world 585px. Row1's own HLG (MiddleCenter) places 250px content in 250px container → content midpoint = container midpoint = **585.0px**.

**After measurements — via `GetWorldCorners` on actual coin Image + "x200" TMP, after `ForceRebuildLayoutImmediate`:**

| State | Combined coin+amount midX | Rewards center | Offset vs 584.5 |
|---|---|---|---|
| **WIN** | **585.0px** | 585.0px | **0.0px — CENTERED** |
| **TIE** | **585.0px** | 585.0px | **0.0px — CENTERED** |

Console log evidence (WIN): `[WIN] COMBINED coin+amount midX=585.0 vs Rewards midX=585.0 offset=0.0px` — `MeasureRewardLayoutV3` script.
Console log evidence (TIE): `[TIE] COMBINED coin+amount midX=585.0 vs Rewards midX=585.0 offset=0.0px` — `CaptureTIEState` script.
Row1 sizeDelta after CSF (both states): `(250.00, 470.00)` (was `(100, 470)`).

**Multi-slot correctness (forward-safety):** With 2 slots at 250px each + 100px HLG spacing = 600px total, centered in 978px Rewards container → symmetric. With 3 slots: 3×250 + 2×100 = 950px centered → symmetric. No hardcoded positions.

WIN capture: `screenshots/stage3_iter3_win_2026-07-02.png` (1170×2532)
TIE capture: `screenshots/stage3_iter3_tie_2026-07-02.png` (1170×2532)

**Verdict: RESOLVED.** Coin+amount visual cluster midpoint = 584.5±0.5px (within 1px) in both WIN and TIE states.

---

### Stale comment cleanup — DONE

`VersusResultScreenController.cs` line 373 comment cleaned:
- OLD: `// Stage 3: DRAW — both columns show "DRAW" in neutral color`
- NEW: `// Stage 3: TIE — both columns show "TIE" in neutral color`

(Red-team noted this as a cosmetic nit. Cleaned in iter-3.)

---

## Figma fidelity

Figma file `5gEAHjl6xAtW8iYY7NMvWd`. Reference images in `reference/figma-win-13274-877.png` and `reference/figma-lose-13275-2628.png`. Nodes `13274:877` (WIN) and `13275:2628` (LOSE) pulled this pass.

**TIE state has no Figma node** — it is a CESAR_REJECTION spec-defined addition (SPEC §5 D2 resolved 2026-07-02).

| Element | Figma node | Figma value | Built value | PASS/FAIL |
|---|---|---|---|---|
| RESULTS header | 13274:877 | White bold "RESULTS", centered | White SemiBold "RESULTS", centered | PASS |
| WINNER label (left col, win state) | 13274:877 | "WINNER" green #50C878 | "WINNER" Color(0x50,0xC8,0x78) | PASS |
| LOSER label (right col, win state) | 13274:877 | "LOSER" orange-red | "LOSER" Color(0xC0,0x40,0x00) | PASS |
| TIE label (both cols, draw/tie state) | N/A — CESAR spec D2 | "TIE" neutral #CCCCCC both cols | "TIE" #CCCCCC both cols | PASS |
| Portrait cards | 13274:877 | CharacterThumbnailCard, rarity+lv badge+name | Reused `CharacterThumbnailCard` instances | PASS |
| Vs. text between cards | 13274:877 | "Vs." centered between portraits | "Vs." centered | PASS |
| Username line | 13274:877 | "USERNAME" under each card | "You" / opponent DisplayName | PASS |
| RANK line — winner green / loser orange | 13274:877 | Green winner rank / orange loser rank | Green #50C878 winner / orange #C04000 loser | PASS |
| RANK line — tie state neutral | N/A — CESAR spec D2 | Both neutral grey | Both #CCCCCC neutral (no green/orange) | PASS |
| HOLE label | 13274:877 | "HOLE" gold centered | "HOLE" gold centered | PASS |
| Course/hole line | 13274:877 | "Lomond Country Club  - Hole 5" | Populated from holeNumber at runtime | PASS |
| Reward row — 1 slot WIN (bright) | 13274:877 | Coin icon bright, centered | Coin×200 bright α=1.0, midX=585.0px | PASS |
| Reward row — 1 slot LOSE/TIE (greyed) | 13275:2628 | Greyed/dimmed reward row | α=0.5 CanvasGroup + child tint dim | PASS |
| Reward row centering | 13274:877 | Single slot centered under separators | Coin+amount tight-Y midX=585.0px vs panel center 584.5, offset=0.5px | PASS |
| NEW MATCH button | 13274:877 | Gold CTA button | Gold CTA button | PASS |
| TopBar (RP balance + gear) | 13274:877 | Visible | Visible (PersistentUIManager.ShowBars) | PASS |
| Bottom nav | 13274:877 | Visible | Visible | PASS |

---

## Acceptance checklist — §4c Stage 3

| # | Item | Result | Evidence |
|---|---|---|---|
| 1 | `ShowResult` uses 3-way outcome switch (win/lose/draw) | PASS | `isDraw = outcome == GameSession.MatchOutcome.Draw; localWon = outcome == GameSession.MatchOutcome.P1Win` — from iter-2 diff, unchanged |
| 2 | DRAW/TIE state: both columns "TIE" neutral grey; ranks neutral; reward row greyed | PASS | `stage3_iter3_tie_2026-07-02.png` — TIE/TIE grey labels, both RANK neutral, coin×200 dimmed |
| 3 | WIN/LOSE states unchanged — regression check | PASS | `stage3_iter3_win_2026-07-02.png` — WINNER green / LOSER orange; coin×200 bright at 585.0px |
| 4 | Reward coin+amount cluster midX == panel center (584.5 ±4px) in WIN | PASS | Measured 585.0px via GetWorldCorners on coin Image + amount TMP; offset 0.0px |
| 5 | Reward coin+amount cluster midX == panel center (584.5 ±4px) in TIE | PASS | Measured 585.0px via GetWorldCorners on coin Image + amount TMP; offset 0.0px |
| 6 | Entrance transition: 0.9→1.0 scale pop-in, 0.15–0.25s ease-out, ends at 1.0 even if interrupted | PASS | `VersusResultModalController.PopInScaleRoutine()` unchanged from iter-1; ease-out cubic + StopCoroutine+Vector3.one interrupt guard |
| 7 | Delta captures: TIE still + WIN regression still; sanctioned CaptureHelper | PASS | Both 1170×2532 via `CaptureHelper.SnapGameViewWithLabel` |
| 8 | Compiles clean; no new console errors | PASS | `assets-refresh` after C# edit; no compile errors |
| 9 | Scene/prefab diff scoped; no Physics/; no Scenarios.cs; no M_Splash*.mat | PASS | `git diff HEAD -- Assets/Scripts/Physics/` = empty; prefab diff = CSF add + childForceExpandWidth change on Row1/2/3 only |

---

## Physics diff (Rule 7)

`git diff HEAD -- Assets/Scripts/Physics/` — no output. PASS.

---

## Files modified or created

| File | Change | Reason |
|---|---|---|
| `Assets/Prefabs/UI/Matchmaking/VersusResultScreen.prefab` | M | Fix: `childForceExpandWidth=false` + `ContentSizeFitter(horizontalFit=PreferredSize)` on Reward Row1/2/3 |
| `Assets/Scripts/UI/Matchmaking/VersusResultScreenController.cs` | M | Stale "DRAW" comment at line 373 → "TIE" (cosmetic nit from red-team) |
| `Assets/Scripts/UI/Matchmaking/VersusResultModalController.cs` | M (unchanged iter-3) | Stage 3 pop-in coroutine from iter-1 — no changes this iter |
| `Packages/manifest.json` | M (pre-existing) | Pre-existing — in baseline dirty block `=== iter-3 kickoff baseline ===` (HEAD SHA `5b72d37fc`) before any iter-3 edits |
| `Packages/packages-lock.json` | M (pre-existing) | Pre-existing — same baseline |

Pre-existing claim for Packages/: HEARTBEAT.log `=== iter-3 kickoff baseline ===` block (HEAD `5b72d37fc`, recorded before iter-3 edits began) lists both `M Packages/manifest.json` and `M Packages/packages-lock.json` in DIRTY block.

---

## Unity authoring traps self-cert (Rule 12)

- **C1 dirty-on-write:** prefab edit used `PrefabUtility.LoadPrefabContents` + `SaveAsPrefabAsset`. PASS.
- **C2 modal-root-stays-active:** VersusResultModalController root stays active; child `modalPanel` toggled by ModalController base. PASS.
- **C3 layout-group vs fixed-size:** fix REMOVES the fixed `sizeDelta.x=100` constraint via CSF auto-sizing; no remaining frozen fixed-height rows. PASS.
- **C4 childForceExpandWidth:** explicitly set to `false` on Reward Row1/2/3 as part of the fix. PASS.
- **C5 Outline:** no Outline component added. PASS.
- **C6 flat layout vs nested groups:** reward row layout unchanged in structure; only Row size policy changed. PASS.
- **C7 edit-mode repaint:** captures taken via `CaptureHelper.SnapGameViewWithLabel` from script-execute (plays in Editor). PASS.
- **C8 real entry:** modal shown via `ShowResult()` on real `VersusResultModalController` instance found in ShellScene hierarchy. PASS.
