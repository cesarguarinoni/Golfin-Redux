# CESAR_REJECTION — `leaderboard_wiring` (Phase 1)

> **ROUND 6 is the ACTIVE work list (below). Rounds 5/4/3/2/1 are preserved further down as history.**

---

## ROUND 6 — after iter-8 (title placement decided)

- **Rejected by:** Cesar, after iter-8.
- **Date:** 2026-06-15
- **Verified done (do NOT regress):** R5-Fix 2 (scroll list starts at rank 4 — the top 3 show only in the podium). All R4/R3/R2 fixes, gold DAILY tab.
- **Context:** iter-8 added a standalone "LEADERBOARD" title INSIDE the GPS banner — it overlapped the "GOLFIN·GPS" logo. Cesar's decision: the title goes in the **persistent top bar center**, replacing the username there; and the **username should only appear on the Home Screen**.

### R6-Fix 1 — Remove the iter-8 standalone title
Remove the `TitleLabel` + `GoldUnderline` GameObjects that iter-8 added as children of `Canvas/ScreensRoot/RankingsScreen` in `ShellScene.unity`, and remove the now-unused `_titleLabel` SerializeField wiring from `RankingsScreenController` (and the field if it's only used for that). The title is no longer a RankingsScreen child.

### R6-Fix 2 — Drive the persistent top-bar center text per screen
In `Assets/Scripts/UI/PersistentUIManager.cs`, make `usernameText` (the center text that currently always shows "CHOTO") screen-aware:
- Cache the real username at startup (e.g. `_username = usernameText.text` in Awake/Start) and have `SetUsername`/`UpdateUsername` update `_username` too.
- Set the header text on every screen change. `HighlightScreen(screenId)` (called from `ScreenManager.cs:151` for every showBars screen) is the hook — set `usernameText.text` at the TOP of that method, BEFORE the existing nav-highlight switch (note the switch has `default: return` which Leaderboard currently hits, so the header text must be set before it):
  - `ScreenId.Home` → `_username` (the username, e.g. "CHOTO")
  - `ScreenId.Leaderboard` → **"LEADERBOARD"**
  - every other showBars screen (Roster, Inventory, HoleSelection, ModeSelection) → **""** (blank — username shows ONLY on Home)
- Net result: username appears only on Home; "LEADERBOARD" appears on the leaderboard; other screens show a blank center.
- Optional: if it's clean, give the "LEADERBOARD" text the gold underline/trim from Figma `4079-1727`; if it crowds the top bar, plain white bold is fine — surface it for Cesar to confirm.

This touches the SHARED PersistentUIManager — verify Home still shows the username, the leaderboard shows "LEADERBOARD", and Roster/Inventory/HoleSelection show a blank center with no errors. Do not break the nav-icon highlighting.

### Round-6 re-submit checklist
- Captures at iPhone 14 1170×2532 via `CaptureHelper.SnapAtEndOfFrameAndPause`. Show: (a) the Leaderboard with "LEADERBOARD" in the top-bar center (no overlap, no standalone banner title), list starting at rank 4; (b) the Home screen showing the username in the top-bar center; (c) one other bar screen (e.g. HoleSelection) showing the blank center.
- Per-tab captures: name each for the tab it actually shows; verify pixels before PASS (labeling missed 3×).
- Do NOT regress R5-Fix 2, R4/R3/R2 fixes, gold DAILY tab, Round-1 entry icons, or iter-2 PASSes.
- Fresh `=== iter-N kickoff baseline … ===` block in `HEARTBEAT.log`; compile-check after the C# edits; update the report + Figma fidelity table.

---

## ROUND 5 — after iter-7 (Cesar reviewed live; title decided)

- **Rejected by:** Cesar, after iter-7.
- **Date:** 2026-06-15
- **Verified done (do NOT regress):** all R4 fixes (24px gap, centered pill + right amount, YOU-row RP right, rarity↔level gap), R3/R2 fixes, gold DAILY tab.
- **Scope:** Two fixes — add the title, and exclude the top 3 from the scroll list.

### R5-Fix 1 — Add the "LEADERBOARD" title
Cesar decided the title text is **"LEADERBOARD"** (singular). The screen currently has no title node at all. Add a title (TextMeshProUGUI, white bold uppercase with the gold underline/trim) styled and positioned per **Figma node `4079-1727`** — in that reference "MISSIONS LEADERBOARD" sits centered in the dark curved header band, above the tab strip. Add it as a child of the RankingsScreen (do NOT touch the shared persistent top bar / username). Because the GOLFIN-GPS banner occupies the sub-header in our build (it isn't in the Figma node), place the title centered in the header so it does not overlap the persistent coin/username/gear or the banner — match Figma position as closely as the live layout allows, and **surface the result so Cesar can confirm/nudge the exact placement.** Use the localization-key pattern if trivial; otherwise the literal "LEADERBOARD" is acceptable for Phase 1.

### R5-Fix 2 — Top 3 must NOT appear in the scrolling list
The top 3 already appear in the podium area; they should not be duplicated as the first three scroll rows. In `RankingsScreenController.cs` `RebuildList()`, the list loop at line ~210 (`for (int i = 0; i < ranking.Count; i++)`) currently instantiates a row for every entry INCLUDING ranks 1/2/3. Change it to start at **`i = 3`** (skip the three podium entries) so the scroll list begins at rank 4. Keep the podium binding (`ranking[0/1/2]` at lines 189-191) and the pinned YOU row unchanged. Guard for `ranking.Count <= 3` (empty list is fine). The pinned YOU row still shows the player's true rank even if they're in the top 3.

### Round-5 re-submit checklist
- Captures at iPhone 14 1170×2532 via `CaptureHelper.SnapAtEndOfFrameAndPause`. Canonical (DAILY) must show the "LEADERBOARD" title AND a scroll list that starts at **rank 4** (no rank 1/2/3 rows).
- Per-tab captures: name each file for the tab whose gold label it actually shows, and verify the pixels of each saved file before marking PASS (the labeling has missed 3×; do not repeat).
- Do NOT regress R4/R3/R2 fixes, gold DAILY tab, Round-1 entry icons, or iter-2 PASSes (tabs/ranking/banner-off/EarnPoints/14 EditMode tests). Note the list now starts at rank 4 — the ranking/tie logic itself is unchanged, only which rows render.
- Fresh `=== iter-N kickoff baseline … ===` block in `HEARTBEAT.log`; compile-check after the C# edit; update the report + Figma fidelity table (add the title row citing `4079-1727`).

---

## ROUND 4 — after iter-6 (Cesar reviewed live)

- **Rejected by:** Cesar, after iter-6.
- **Date:** 2026-06-15
- **Verified done (do NOT regress):** R3-Fix 1 (Thumbnails portraits), R3-Fix 2 (no runtime scale), R2-Fix B/C/E, gold DAILY tab.
- **Still OPEN (architect, not in this round):** the Rankings screen has no "LEADERBOARD" title node (SPEC §1). Cesar has not yet decided add-vs-waive — leave as-is this round.
- **Scope:** Four layout fixes (gap + RP alignment + label spacing). Mostly prefab/scene; no data/provider/time/tab logic.

### R4-Fix 1 — 24px gap between the banner and the top nav bar (shift everything down)
Currently the banner sits flush under the persistent top nav bar with no gap. Add a **24px** gap between the top nav bar and the banner (matches Figma: Top UI ends y=313, Content starts y=337 = 24px). Adding the gap must push **everything below it down** — banner, tab bar, league/reset row, podium, and the scroll list all move down by 24px. Implement by shifting the Rankings screen content root down 24px (top anchor/offset), not by resizing individual children.

### R4-Fix 2 — Top-3 RP: re-center the PILL, right-align only the AMOUNT inside it
Iter-6 over-corrected: setting the RewardPoints container to MiddleRight moved the **whole pill** to the right so it's no longer centered under the card. Cesar wanted only the **number** moved right, inside a centered pill. Fix BOTH:
- (a) The RP **pill** (`RewardPoints/Background`) must be **centered** under the card again (revert the container alignment that moved it).
- (b) The RP **amount** (`RewardPoints/Background/NameLabel`, the digits) must be **right-aligned within the pill** — coin on the left, number to the right, like the list rows read.
Keep the `N0` no-"RP"-suffix format.

### R4-Fix 3 — Pinned "YOU" row RP must be right-aligned too
The pinned `RankingsCardUser` row still shows its RP amount on the LEFT. Right-align it to match the scroll-list rows and the corrected podium (coin left, number right). Node: `RankingsCardUser` → `RewardPoints/Background/NameLabel`.

### R4-Fix 4 — Add a space between the Rarity name and the Level
On the cards the rarity and level run together with no gap (e.g. "LEGENDARYLVL 176", "RARELVL 80"). Add spacing between the **RarityLabel** and **LevelLabel** so they read "LEGENDARY  LVL 176". Apply on the Top-3 cards (`Info/RarityLabel` + `Info/LevelLabel`) AND the scroll-list rows (the rarity+level pair under `Name+Level`). Use layout spacing / padding (or a separator) — not a trailing space hack baked into the text.

### Round-4 re-submit checklist
- Captures at iPhone 14 1170×2532 via `CaptureHelper.SnapAtEndOfFrameAndPause`. **Capture each tab AFTER selecting it, with a layout rebuild + 1-frame yield, and verify each file's active tab matches its filename before finishing** (the off-by-one mislabeling has recurred twice — do not mark per-tab PASS without checking the pixels of each saved file; cite the capture method explicitly in the report).
- Canonical (DAILY default) must show: 24px banner gap, centered pill with right-aligned amount on all three podium cards, right-aligned RP on the YOU row, and a visible gap between rarity and level.
- Do NOT regress R3-Fix 1/2, R2-Fix B/C/E, gold DAILY tab, Round-1 entry icons, or iter-2 PASSes.
- Fresh `=== iter-N kickoff baseline … ===` block in `HEARTBEAT.log`; compile-check after any C# edit; update the report + Figma fidelity table.
- Note: do NOT re-add a runtime podium-card scale (Cesar sizes those in the prefab). Do NOT touch the missing-title question this round.

---

## ROUND 3 — after iter-4 (Cesar reviewed live; gold-tab CONFIRMED working)

- **Rejected by:** Cesar, after iter-4.
- **Date:** 2026-06-15
- **Confirmed working (do NOT touch):** R2-Fix F gold active-tab selection — Cesar verified it works. The earlier canonical-labeling/escalation concern is therefore MOOT; just produce a clean DAILY-default canonical as part of the normal re-capture. R2-Fix A/B (no "RP" suffix), C, D, E all stand.
- **Scope:** Three top-3 podium tweaks. Two are code, one is prefab layout. Do NOT touch data/provider/time/tab logic or the entry icons (approved).

### R3-Fix 1 — Top-3 portraits must load from `Resources/Portraits/Thumbnails`, not Rankings
Only the **top 3** use the Thumbnails images. Currently `Top3CardWidget.BindCharacterArt` loads `Resources.Load<Sprite>($"Portraits/InGame/{portraitSpriteName}")` then falls back to `template.portraitSprite` (which resolves to a Rankings-folder sprite — that's what's rendering). Change the Top-3 portrait source to **`Resources.Load<Sprite>($"Portraits/Thumbnails/{portraitSpriteName}")`** (`Top3CardWidget.cs:77`). Leave the scroll-list rows (`RankingsCardWidget`) on their current source — only the top 3 change. Verify the Thumbnails sprites actually resolve for the podium characters (fall back gracefully if a name is missing).

### R3-Fix 2 — Remove the runtime card-size override; Cesar sizes the cards in the prefab
Delete the runtime scale lines in `RankingsScreenController.cs:194-196`:
```
if (_top1Card != null) _top1Card.localScale = Vector3.one;
if (_top2Card != null) _top2Card.localScale = Vector3.one * 0.88f;
if (_top3Card != null) _top3Card.localScale = Vector3.one * 0.82f;
```
Keep the `_top1Card/_top2Card/_top3Card` fields (still used for `BindPodiumCard` at 189-191) — remove ONLY the `localScale` assignments. **Do not introduce any other runtime size/scale manipulation of the podium cards.** Cesar will set #1/#2/#3 sizes (and the bottom-baseline alignment) directly in the prefab. Expect the capture to show the cards at their prefab size — that's correct; do not re-add a shrink.

### R3-Fix 3 — Top-3 RP amount goes on the RIGHT side of the pill (match the other cards)
The scroll-list rows show the RP (coin + number) on the **right** side of the row. The top-3 pill currently has it positioned differently (left). Make the Top-3 card's `RewardPoints/Background/NameLabel` (and the coin) **right-aligned like the list-row cards** — inspect the `RankingsCard` row's `RewardPoints` layout and replicate it on the Top1/Top2/Top3 card prefabs so all RP amounts read consistently. This is a prefab layout change on the podium cards (no data change). Keep the no-"RP"-suffix format (`N0`).

### Round-3 re-submit checklist
- Captures at iPhone 14 1170×2532 via `CaptureHelper.SnapAtEndOfFrameAndPause`; produce a clean **DAILY-default** canonical (DAILY gold) plus one per other tab, correctly named.
- Confirm top-3 portraits are the Thumbnails art, no runtime scale override remains, and the top-3 RP sits on the right of the pill matching the rows.
- Do NOT regress R2-Fix B/C/E or any iter-2 / Round-1 PASS.
- Fresh `=== iter-N kickoff baseline … ===` block in `HEARTBEAT.log`; compile-check after every C# edit; update the `## Figma fidelity` / report to reflect the new portrait source + RP position.

---

## ROUND 2 (HISTORY) — items A–E stand; F confirmed working

> **ROUND 2 was the prior active list. Items A/B/C/D/E remain in effect; F (gold tab) is confirmed working by Cesar.**

---

## ROUND 2 — after iter-3 (Cesar reviewed the live screenshots)

- **Rejected by:** Cesar, after iter-3 `READY_FOR_SELF_REVIEW`
- **Date:** 2026-06-15
- **Round-1 status:** Home-screen entry icon placement = **APPROVED**. HoleSelect entry icon placement = **APPROVED**. The four Rankings-screen items below are NOT yet right.
- **Routing:** `CESAR_REJECTED` → back to golfin-implementer.
- **Scope:** Rankings screen visuals ONLY. Do NOT touch data/provider/time/tab logic. Do NOT move the entry icons (those are approved).

New reference crops added to `reference/`:
- `figma-podium-detail-4079-1727.png` — close-up of the Top-3 podium cards (the target look).
- `figma-tabbar-gold-daily-4079-1727.png` — the tab bar with the active **DAILY** tab in gold.

### R2-Fix A — Left-align RP on EVERY row, not just the Top-3
Iter-3 only left-aligned the podium RP. The **scroll-list rows** and the **pinned `YOU` row** still have their old RP alignment. Apply the same left-aligned (coin fixed left, number immediately right of it) treatment to **all** RP displays: Top-3 pills, every `RankingsCards` list row, and the `RankingsCardUser` pinned row.

### R2-Fix B — Remove the literal "RP" suffix from the RP quantity
Currently reads `R 40.1K RP`. The Figma shows just the coin + number (`R 999999`) — **no "RP" word**. Drop the trailing " RP" everywhere RP score is shown (podium, list, pinned). Keep the coin sprite + the number. Match the **top-nav RP counter's numeric format** (it uses thousands separators, e.g. `999,999`); just no "RP" text.

### R2-Fix C — Top-3 portraits: fill the card frame (the raise was the wrong fix)
Iter-3 nudged the small square portrait up, which left **empty space UNDER it**. That is not the design. In Figma (`figma-podium-detail-4079-1727.png`) the portrait is a **large framed image that fills the card's upper region edge-to-edge** (character shown chest-up, inside the rarity-colored frame) — there is no empty gap above OR below it; the image occupies the whole portrait area, then name/rarity/level/RP sit beneath.
**How:** stop treating the portrait as a small floating thumbnail. Size the `Portrait` Image to **fill its frame container** (stretch anchors / match the frame rect) so the sprite fills the card's image area like Figma. Use the same portrait sprite the list rows use; if the only available sprite is the small square in-game icon, fill the frame with it (preserve aspect, anchored to fill the top region) so there is no dead space. Compare directly against the reference crop.

### R2-Fix D — Bottom-anchor the Top-3 cards (so all three share a baseline)
The #1/#2/#3 sizes from iter-3 are good, but the shrunk #2/#3 currently shrink around their **center**, so their bottoms float. In Figma all three cards sit on the **same bottom baseline** — #1 is taller and rises higher in the center; #2 and #3 are shorter but their **bottom edges line up** with #1's. Set the podium card pivots/anchors to **bottom** (or reposition after scaling) so the three bottoms align. This is the classic podium silhouette.

### R2-Fix E — Spell out the rarity, not a single letter
Iter-3 shows the single-letter rarity (`R`). Figma spells it: **RARE / LEGENDARY / SUPREME / COMMON / UNCOMMON / MYTHIC** in the rarity color. Use the full rarity label (the project rarity helper has a full-name getter — use it; do not hardcode). Apply on the Top-3 cards AND the list rows (`RartityLabel`).

### R2-Fix F — Active tab label must be GOLD (match the existing game filter bars)
The selected tab (DAILY by default) should render in **gold**, inactive tabs in silver — exactly like the existing filter bars (HoleSelection course/tee pills, ClubFilterBar, ModeSelect). Use the project idiom **`Golfin…TextGradients.ApplyGold(tmp)`** for the active tab label and **`TextGradients.ApplySilver(tmp)`** for the inactive ones (`Assets/Scripts/Utilities/TextGradients.cs`; `#EEDC9A` gold). Drive it from the tab-switch handler so it updates on tab change. Reference: `figma-tabbar-gold-daily-4079-1727.png` (DAILY in gold).

### Round-2 re-submit checklist
- Captures at iPhone 14 1170×2532 via `CaptureHelper.SnapAtEndOfFrameAndPause`.
- New canonical podium showing: full-frame portraits (no dead space), bottom-aligned #1/#2/#3, gold DAILY tab, spelled-out rarities, left-aligned RP with no "RP" word.
- One capture per other tab (Weekly/Monthly/History) confirming the gold-active-tab logic moves with the selection.
- Do NOT regress Round-1 approved items (entry-icon placement) or any iter-2 PASS (tabs/ranking/banner-off/EarnPoints/tests).
- Fresh `=== iter-N kickoff baseline … ===` block in `HEARTBEAT.log`; compile-check after every C# edit; update the `## Figma fidelity` table with these six items citing nodes `4079-1727` (podium/tab) with PASS verdicts against the new reference crops.

---

## ROUND 1 — after iter-2 (HISTORY; items below are resolved or superseded)

- **Rejected by:** Cesar, after `ARCHITECT_REVIEW_PASS` (iter-2)
- **Date:** 2026-06-15
- **Verdict:** Almost perfect — 4 fixes required before DONE.
- **Routing:** `CESAR_REJECTED` → back to golfin-implementer.

Iter-2 nailed the data layer, the four tabs, the distinct ranked board, the banner-off case, and EarnPoints. These are **layout/placement** corrections only — do **not** touch the data/provider/time code. New Figma reference renders are in `reference/` (listed per item).

---

## Fix 1 — Move the leaderboard entry icon OUT of the persistent top nav bar; make it a per-screen element BELOW the bar, right-aligned, on Home + Hole Selection ONLY

**Current (wrong):** the icon was added to `PersistentUIManager`'s shared TopBar. Because Leaderboard is in `showBars`, that persistent bar — and therefore the leaderboard icon — also renders **while you are already on the Rankings screen** (a "go to leaderboard" button on the leaderboard itself). It also sits *inside* the top nav bar rather than below it.

**Wanted (Figma node `12961-1694`, ref `reference/figma-icon-position-home-12961-1694.png`):** a standalone **Rankings Container** button that sits **under the top nav bar, right-aligned**, as a child of each screen's own content — present on **Home Screen** and **Hole Selection Screen only**, NOT on the Rankings screen and NOT in the shared persistent bar.

**Exact geometry** — node `12961:1737` "Rankings Container", a sibling of the content under the top UI:
- Size: **75 × 75 px**
- Position in the 1170-wide screen: `x = 1047, y = 262` → right edge at 1122 = **~48px inset from the right edge**, vertically **just below the Top UI bar** (Top UI spans y 0–313) and above the screen content.
- Icon art: gold rounded-square tile with the 2·1·3 podium — `reference/figma-rankings-container-icon-12961-1737.png`. The existing `ICO_Leaderboard.png` is the bare podium; the Figma version is that podium **on a gold rounded-square tile**. Match the tile if cheap; at minimum match the **position**.

**How:**
- Remove the `leaderboardButton` from `PersistentUIManager`'s TopBar (revert that wiring). Keep `RankingsScreenController.OpenFrom(returnScreen)` — just call it from the new per-screen buttons.
- Add a `LeaderboardButton` (Button + `Golfin.UI.Polish.ButtonPressFeedback`) to the **HomeScreen** and **HoleSelectionScreen** prefabs/hierarchy at the geometry above (anchor top-right: `anchorMin = anchorMax = (1,1)`, `anchoredPosition ≈ (-86, -86)` from the top-right of the screen-content rect so the 75×75 tile lands at x≈1047/y≈262 — confirm against the live rect and the reference).
- `onClick` → record invoking screen → `ScreenManager.Instance.ShowScreen(ScreenId.Leaderboard)` (via `RankingsScreenController.OpenFrom`).
- Verify the icon does **NOT** appear on the Rankings screen anymore. Capture Home **and** HoleSelect showing the icon below-the-bar top-right, plus a Rankings capture proving the icon is absent there.

---

## Fix 2 — Raise the Top-3 portrait images (kill the empty space above the heads)

**Current (wrong):** in each podium card the character art sits low — there is a large empty band of background above each character's head (see `screenshots/leaderboard_canonical_final_f80455.png` podium row).

**Wanted (Figma node `4079-1727`, ref `reference/figma-rankings-podium-4079-1727.png`):** the portrait fills the card with the head near the top — minimal empty space above.

**How:** raise the `Portrait` image within each Top3 card (adjust the portrait RectTransform offset / anchored Y upward, and/or its rect height) so the character art is pushed up to match Figma. Do this on the #1/#2/#3 podium portrait slots. No data change. Re-capture the podium.

---

## Fix 3 — RP numbers spill outside the pill; left-align them like the top-nav RP counter

**Current (wrong):** the gold RP pills on the podium cards (`R 40K RP`, `R 40.5K RP`, …) have the value **center-aligned**, crowding/spilling at the pill edges.

**Wanted:** align the RP value **left**, immediately after the coin icon, exactly like the **top nav bar RP counter** (`R 999,999` — coin, then left-aligned number). The number must stay inside the pill.

**How:** set the podium RP `TextMeshProUGUI` alignment to Left (match the top-bar RP label's alignment + horizontal layout). Mirror whatever the top-nav counter does (coin sprite fixed left, text left-aligned with a small gap). If overflow is still possible with large values, ensure the text uses the same abbreviation/format as the top bar. Apply to all three podium pills (and check the pinned-row / list RP if they exhibit the same spill). Re-capture.

---

## Fix 4 — Shrink the #2 and #3 podium displays (simple scale, no GO rebuild)

**Current (wrong):** #1, #2, #3 cards render at essentially the same size.

**Wanted (Figma node `4079-1727`):** #1 (center) is the largest; **#2 and #3 are visibly smaller**, #3 a touch smaller still — the classic podium hierarchy.

**How — use the carousel selected-card idiom, do NOT rebuild the GameObjects.** The roster/inventory carousels scale cards with a plain `transform.localScale = Vector3.one * factor` (e.g. `Assets/Scripts/UI/Inventory/BagThumbnailCard.cs:74` → `selected ? Vector3.one * 1.08f : Vector3.one`). Apply the same: leave the #1 card at `Vector3.one`, set the #2 and #3 podium card root transforms to a shrunk `localScale` (start around **#2 ≈ 0.88, #3 ≈ 0.82** and tune to the Figma proportions). Pure transform scale on the existing podium card GOs — no hierarchy changes. Re-capture and compare side-by-side with `reference/figma-rankings-podium-4079-1727.png`.

---

## Re-submit requirements

- Re-capture at iPhone 14 **1170×2532** via `CaptureHelper.SnapAtEndOfFrameAndPause` (not `screenshot-game-view`).
- New required captures: **Home** screen (icon below-bar, top-right), **HoleSelect** screen (icon below-bar, top-right), **Rankings** screen proving the icon is now absent there, and a fresh **canonical** podium showing raised portraits + left-aligned RP pills + shrunk #2/#3.
- Update the `## Figma fidelity` table with rows citing nodes `12961-1694` / `12961-1737` (icon position) and `4079-1727` (podium portraits + #2/#3 scale + RP pill alignment), each with a real PASS verdict against the new references.
- Start the iteration with a fresh `=== iter-N kickoff baseline … ===` block in `HEARTBEAT.log`.
- Do not regress any iter-2 PASS (tabs, ranking, banner-off, EarnPoints, tests).
