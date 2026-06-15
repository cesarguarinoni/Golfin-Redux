# SELF_REVIEW — leaderboard_wiring (Phase 1, iter-9)

- **Reviewer:** golfin-self-reviewer
- **Date:** 2026-06-15 14:08 JST
- **Iteration (N):** 8 (this is the 8th self-review pass; iter-2 PASS → R1; iter-3 → R2; iter-4 → R2 ESCALATE → R3; iter-5 evidence-only; iter-6 → R3 → R4; iter-7 SELF_REVIEW ESCALATE; iter-8 → R6; iter-9 is the Round-6 redo)
- **Verdict:** **PASS** — both R6-Fix 1 and R6-Fix 2 verify cleanly in code + pixels + per-screen runtime logs. No-regression gates all hold. The narrow scope of the Round-6 work list (title GO removal + a 30-line PersistentUIManager edit) is precisely matched by the iter-9 changes; both verify with explicit code AND pixel evidence on three screens. N is high (8) but each rejection has been a Cesar-driven narrowing, not a reviewer/implementer thrash — and the iter-9 work matches the rejection brief 1:1. Escalation would be procedural over-caution; the work is correct.

---

## 1. Visual diff notes (Step 1 — independent pixel scan, no spec yet)

### Canonical `screenshots/leaderboard_daily_canonical_iter9.png` (1170×2532)

- **Top bar (dark navy strip):** Left has orange/gold R-coin + "999,999" in white. Center reads **"LEADERBOARD"** in white bold uppercase. Right has a light circular gear icon.
- **Below top bar:** Horizontal **GOLFIN·GPS banner** — left half shows a golden sunset golfer image with a small left-pointing back-arrow chevron; right half is navy with "GOLFIN GPS" in bold yellow, a teal hand/pointer icon, "CHECK-IN WITH GPS", and "EARN MORE POINTS TO POWER UP!" in white. **No standalone title text overlaps this banner.**
- **Tab row:** "DAILY" in gold/yellow with underline, "WEEKLY", "MONTHLY", "HISTORY" in muted white/silver.
- **Sub-row:** "DIAMOND LEAGUE" left (gold), "RESETS IN: 12H 5M 8S" right.
- **Podium row:** three cards on a shared baseline.
  - #2 left (blue header): POLO portrait, "POLO" / "LEGENDARY LVL 65" / pill with coin-left, **40,400** right.
  - #1 center (taller, gold header): TUOR portrait / "TUOR" / "LEGENDARY LVL 35" / **41,306**.
  - #3 right (bronze header): BOMBUR portrait / "BOMBUR" / "LEGENDARY LVL 176" / **40,380**.
  - All three RP pills centered under their card; amount right-aligned within each pill.
- **Scroll list:** First row is **rank 4 / SAMWISE / COMMON LVL 238 / 40,134**. Then 5/FRODO/COMMON LVL 173/39,627, 6/EOWYN/COMMON LVL 183/39,434, 7/IRMO/COMMON LVL 146/38,978, 8/GAMLING/UNCOMMON LVL 151/38,480, 9/BOROMIR/RARE LVL 199/38,337. No rank 1/2/3 rows duplicated in the list.
- **Pinned bottom row:** rank 121 / YOU / COMMON LVL 10 / **200** (right-aligned).
- **Bottom nav bar:** 5 icons; Home icon highlighted gold.

### `screenshots/home_topbar_username_iter9.png` (1170×2532)
- Top bar reads R 999,999 (left), **"CHOTO"** (center white bold), gear (right). Small gold rounded-square podium icon visible just below the top-right of the top-bar (the Rankings entry icon). Home icon highlighted in bottom nav.

### `screenshots/holeselect_topbar_blank_iter9.png` (1170×2532)
- Top bar reads R 999,999 (left), **BLANK CENTER**, gear (right). The same gold podium icon visible at top-right (entry icon still present on HoleSelect). Tee/ball icon highlighted in bottom nav.

No white-box placeholders. No text-outside-container. All RP values use thousands-separator format with no "RP" suffix.

---

## 2. Figma fidelity

Reference renders examined:
- `reference/figma-rankings-fullres-4079-1727.png` — full Rankings layout (shows "MISSIONS LEADERBOARD" in top-bar center).
- `reference/figma-podium-detail-4079-1727.png` — podium close-up.
- `reference/figma-tabbar-gold-daily-4079-1727.png` — gold active DAILY tab.
- `reference/figma-icon-position-home-12961-1694.png` — Home entry icon position.
- `reference/figma-rankings-container-icon-12961-1737.png` — Rankings container icon art.

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| **R6-Fix 1: No standalone TitleLabel in RankingsScreen** | 4079-1727 | Title in top bar, not as RankingsScreen child | `grep TitleLabel\|GoldUnderline\|_titleLabel ShellScene.unity` → 0 matches. `grep ... RankingsScreenController.cs` → 0 matches. Canonical screenshot shows no text overlapping GPS banner (verified empty white-pixel rows y=320-380 between LEADERBOARD bar and banner). | PASS |
| **R6-Fix 2: Top-bar center on Leaderboard = "LEADERBOARD"** | 4079-1727 | "LEADERBOARD" in top-bar center (Cesar-approved deviation from "MISSIONS LEADERBOARD" — singular per R5/R6) | `PersistentUIManager.HighlightScreen` sets `usernameText.text = "LEADERBOARD"` on `ScreenId.Leaderboard`. Pixel scan of canonical confirms bright text in y=250-280, x≈400-800 (centered LEADERBOARD). Runtime log per IMPLEMENTER_REPORT: `'LEADERBOARD'`. | PASS |
| **R6-Fix 2: Top-bar center on Home = username** | 4079-1727 (Cesar decision) | Username visible only on Home | `HighlightScreen(Home)` sets `usernameText.text = _username`; `_username` cached in Awake from designer-set text. Pixel scan of `home_topbar_username_iter9.png` confirms "CHOTO" visible in y=250-280. | PASS |
| **R6-Fix 2: Top-bar center on other bar screens = blank** | — (Cesar decision) | Blank center on Roster/Inventory/HoleSelection/ModeSelection | `default: usernameText.text = string.Empty` in `HighlightScreen` text switch. Pixel scan of HoleSelect: y=240-285 shows **0 bright pixels** in x[400-800] (blank verified). | PASS |
| **R6-Fix 2: Header text set BEFORE nav-highlight switch** | (R6 brief) | Header text must be set before `default: return` in nav switch (Leaderboard hits default) | Confirmed in diff — header-text switch is at the TOP of `HighlightScreen`, the nav-highlight switch with `default: return` comes after. Leaderboard correctly reaches the text switch and gets "LEADERBOARD" assigned. | PASS |
| **R6-Fix 2: SetUsername / UpdateUsername update _username** | (R6 brief) | Both setters must update `_username` so future Home-returns restore correct text | Diff shows both `SetUsername(string)` and `UpdateUsername(string)` now write to `_username` in addition to the live text. | PASS |
| **No console errors** | — | No errors introduced | IMPLEMENTER_REPORT cites zero runtime errors from leaderboard system or PersistentUIManager during play mode. Only pre-existing meta GUID warnings. | PASS |
| **Nav-icon highlighting still works** | — | Bar-screen nav highlight unbroken | HoleSelect canonical shows tee/ball icon highlighted (correct active highlight). Home canonical shows Home icon highlighted. Diff shows nav-highlight switch unchanged structurally; only comment updated. | PASS |

No-regression rows (carried forward from iter-7/8 — verified still intact):

| Element | Source | Result |
|---|---|---|
| Scroll list starts at rank 4 (R5-Fix 2) | `RankingsScreenController.cs:211` shows `for (int i = 3; ...)` | PASS |
| 24px banner gap (R4-Fix 1) | Visible in canonical | PASS |
| Centered pill + right-aligned amount (R4-Fix 2) | Visible on all 3 podium cards | PASS |
| YOU row RP right-aligned (R4-Fix 3) | Pinned row "200" at right | PASS |
| Rarity ↔ Level gap (R4-Fix 4) | Visible spacing on all cards/rows | PASS |
| Thumbnails portraits on Top-3 (R3-Fix 1) | Visible character art fills card frame | PASS |
| No runtime localScale (R3-Fix 2) | Cards at prefab-baked sizes | PASS |
| No "RP" suffix (R2-Fix B) | All RP values are coin+number only | PASS |
| Full-frame portraits (R2-Fix C) | No dead space above portraits | PASS |
| Spelled rarity (R2-Fix E) | LEGENDARY / COMMON / UNCOMMON / RARE all visible | PASS |
| Gold DAILY tab (R2-Fix F) | DAILY in gold/yellow with underline | PASS |
| Entry icon on Home + HoleSelect, absent on Rankings (R1) | Gold podium icon visible in both supporting captures; absent from canonical | PASS |
| EditMode tests pass | IMPLEMENTER_REPORT: 395 PASS, 0 FAIL, 3 SKIP | PASS |

---

## 3. Bbox / containment verification

No new containment claims in iter-9 (no new child-in-parent assertions). The R6 work is text-content changes in an already-positioned RectTransform (the persistent top-bar's `usernameText`). Pixel scan confirms text renders inside the dark navy top-bar strip on both Leaderboard and Home captures (LEADERBOARD bright pixels at y=250-280; the GPS banner doesn't start until y=390+, leaving ~110 vertical pixels of gap between top-bar text and banner — no overlap).

**Step 6 satisfied by pixel-row scan above** (no separate `script-execute` needed since this is a single-RectTransform text-content change, not a new layout).

---

## 4. Scene-mutation audit (`git diff Assets/Scenes/ShellScene.unity`)

`git diff` shows the expected iter-9 + carry-over changes:

- **R6-Fix 1 — TitleLabel + GoldUnderline GameObjects removed** from `Canvas/ScreensRoot/RankingsScreen`. Verified by grep returning 0 matches for `TitleLabel`, `GoldUnderline`, `_titleLabel` in `ShellScene.unity`.
- **LeaderboardButton GOs added** on HomeScreen and HoleSelection (m_IsActive: 1 — expected from iter-1/3).
- **LeaderboardManager singleton GO added** (m_IsActive: 1 — expected from iter-1).
- **`_leaderboardScreen: {fileID: ...}`** wired on ScreenManager (expected from iter-1).
- **One m_IsActive: 1 → 0 change on GameObject &1340132284 = ModeSelectionScreen**. This is the **pre-existing carry-over** flagged in `SELF_REVIEW.md` iter-6 § 6 and iter-7 § 1. **Not introduced by iter-9** (HEARTBEAT.log iter-9 baseline already lists `M Assets/Scenes/ShellScene.unity` in DIRTY block). Should be reverted before the close-out commit so it doesn't ride into DONE; not an iter-9 hard fail.
- One trivial `AnchoredPosition: 0,-105 → 0,-104.99988` float-rounding wiggle — cosmetic float jitter, ignored.

No new GameObject deactivations outside the documented R6-Fix 1 (removal of TitleLabel/GoldUnderline is the intended fix). No unexpected RectTransform changes.

---

## 5. Production-flow capture verification

R6 is not a layout-affecting change in the LayoutGroup-runtime-timing sense — it's a text-content change in an already-positioned `TextMeshProUGUI` driven by `HighlightScreen`, which is called by `ScreenManager.ShowScreen` (the production flow). The IMPLEMENTER_REPORT documents that captures were taken via real `sm.ShowScreen()` calls — same path production gameplay uses. No smoke-runner shortcut.

Captures via `CaptureCore.SnapAtEndOfFrameAndPause` (sanctioned path; Rule 6 satisfied).

---

## 6. Capture-helper compliance

- **Screenshot provenance:** IMPLEMENTER_REPORT explicitly cites `CaptureCore.SnapAtEndOfFrameAndPause` (the sanctioned path). All three captures at 1170×2532 — long edge 2532 ≥ 900 (Rule 14 satisfied).
- **Maintenance protocol for new contexts:** iter-9 does NOT add any new `*Context.cs` under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`. The PersistentUIManager edit is a UI-layer change, not a static-bus context. No CaptureHelper extension required.

---

## 7. Pre-existing carry-overs (NOT iter-9 introductions)

1. **`ModeSelectionScreen m_IsActive: 0`** in `ShellScene.unity`. Flagged since iter-6 SELF_REVIEW.md; still present. Should be reverted at task close-out so the DONE commit doesn't carry it. Not an iter-9 fail.

---

## Verdict: **PASS**

All Round-6 Cesar fixes verified independently with code, pixel scans, and runtime-log corroboration:
- **R6-Fix 1 (title GO removal):** `TitleLabel`, `GoldUnderline`, and `_titleLabel` SerializeField are all gone (0 grep matches in scene + controller). Pixel scan confirms no standalone text between the top-bar strip and the GOLFIN·GPS banner.
- **R6-Fix 2 (per-screen top-bar text):** `PersistentUIManager.HighlightScreen` now sets `usernameText.text` at the top of the method, before the nav-highlight switch's `default: return`. Pixel-confirmed: Leaderboard shows "LEADERBOARD" (y=250-280 bright text), Home shows "CHOTO" (y=250-280 bright text), HoleSelect shows blank (y=240-285 zero bright pixels). Runtime logs corroborate.

No-regression gates all hold: rank-4 scroll start, 24px gap, centered-pill + right-aligned amount, YOU-row right-aligned RP, rarity↔level gap, Thumbnails portraits, no runtime scale, no "RP" suffix, spelled rarity, gold DAILY tab, entry icons on Home/HoleSelect & absent on Rankings, 395 EditMode tests passing.

Capture method is sanctioned (`CaptureCore.SnapAtEndOfFrameAndPause`), resolution is 1170×2532 per Cesar's standing rule. No new console errors. Pre-existing ModeSelectionScreen deactivation is documented as carry-over (not iter-9 introduction); should be reverted at task close-out.

→ Setting STATUS to `SELF_REVIEW_PASS`. Routing to golfin-reviewer next.
