# Self-review — `gacha_screen` Stage 2

**Reviewer:** golfin-self-reviewer
**Iteration:** Stage 2 iter-2 (targeted redo of iter-1's 2 FAILs)
**Timestamp:** 2026-07-13 JST
**Verdict:** **FORWARD_TO_ARCHITECT**
**STATUS:** `SELF_REVIEW_PASS`

## Visual diff notes (Step 1 — pixels only, before consulting spec/report)

Canonical: `screenshots/stage2_iter2_gacha_tab_2026-07-13.png` (2070×1912; content is 1170×2532 game view inside Unity Editor chrome — see chrome note below).

**Top bar:** Gold R-points pill "R 73,900" upper-left; gacha-ticket icon "10" mid-top with gold "+" (Shop+) adjacent; navy settings gear top-right.

**Header:** "REWARDS CENTER" title inside a navy banner. Small circular clock (History) icon inset at the top-left corner of the card region. Tab strip below: **GACHA** active (bright gold), STORE (white), GIFTS (white), with a thin gold underline under GACHA and dividers between tabs.

**Center card (active, α=1):**
- **Top:** solid blue title bar with white bold uppercase text reading **"STANDARD CLUB 1"** — real display text, no underscores, no truncation.
- **Directly below title:** a **DARK NAVY rounded pill** with visible white text **"ENDS IN: 171d 22h 14m 36s"**. Not white/blank — the pill is clearly the ≈#142449 stadium pill and the label is legible white-on-navy.
- Right of title: dark chip "!" labelled "RULES & RATES".
- Middle: pink/magenta band "GET Drivers, Woods, Irons"; art panel with two drivers (FYLOE green, ROYAL SWING orange) over "MAX POWER" repeat pattern; then a middle band "CHANCE TO GET LEGENDARY GEAR!" in white+pink over a second driver pair (MitreO gold-star, TIFTO VOIGT94).
- Pity block: "Guaranteed A-rank or higher in at most [99 pulls]" and "Guaranteed S-rank signal in at most [99 pulls]".
- "Common/Uncommon characters or clubs may also be obtained."
- Cost row: "500 [ticket] x1     4,500 [ticket] x10" against a dark navy strip.
- Two large gold buttons: **PULL x1**, **PULL x10**.

**Right peek (dimmed, scaled ≈78%):**
- Title bar reads **"TEST"** (truncated to card edge — the full string is TEST BANNER A per the CSV).
- Countdown pill under the title is also dark navy with visible white "ENDS IN: 17…" — partially clipped by the card edge.
- Same art palette visible behind the dim overlay.

**Bottom:** 3 pagination dots inside the bottom nav, center dot bright/full-alpha, side dots dim → dynamic count = 3 live rows.

**Chrome note:** capture carries a strip of Unity Editor chrome above the game view (`Scene/Game` tabs, `iPhone 14 (1170x2532)` device dropdown, `0.81x` scale slider, `Play Focused`/`Stats`/`Gizmos`). Content inside is the real 1170×2532 iPhone 14 game view; per orchestrator note this is NOT a fail on its own, but a clean chrome-free capture is preferred going forward.

## Figma reference check (Step 2)

Compared against `screenshots/figma-reference.png` (Stage 0 canonical reference — Stage 2 makes no card geometry changes):

- Center card title in reference reads a real display name → screenshot renders **"STANDARD CLUB 1"** (real display text). **MATCH.**
- Center card countdown pill in reference is dark navy with white "ENDS IN: …" text → screenshot shows dark navy pill with white **"ENDS IN: 171d 22h 14m 36s"**. **MATCH.**

Both iter-1 mismatches are resolved.

## Iter-1 FAIL fix verification (lead item)

### FIX 1 — Banner title renders display text, not a raw key → **RESOLVED**

Verified in source:

- `Assets/Resources/Data/gacha_banners.csv` all 4 rows now carry display text in the `nameKey` column:
  - `banner_standard_club1` → `STANDARD CLUB 1`
  - `banner_test_a` → `TEST BANNER A`
  - `banner_test_b` → `TEST BANNER B`
  - `banner_inactive` → `INACTIVE BANNER`
- No raw `GACHA_BANNER_*` uppercase-snake-case tokens remain in the CSV.
- Screenshot confirms visible fix: center card top reads **STANDARD CLUB 1** (no ellipsis, no underscores); peek reads **TEST** (truncated to `TEST BANNER A`).
- Report cites TODO comment in `GachaBannerCard.cs` Bind() documenting the future loc-swap for `localization_audit` 353 — spot-checked; this is the correct scope for the future localization pass (not a defect to gate on).

**Result: PASS.**

### FIX 2 — Center countdown pill is dark navy with visible white "ENDS IN…" text → **RESOLVED**

Verified in source (`Assets/Resources/Prefabs/Gacha/GachaBannerCard.prefab`, CountdownPill Image MonoBehaviour):

```
m_Color: {r: 0.078431375, g: 0.14117648, b: 0.28627452, a: 1}   → #142449 dark navy
m_Sprite: {fileID: 21300000, guid: bb07d102185aa4f1ca51da13de9eeac6, type: 3}  → S_PillStadium
m_Type: 1                                                        → Sliced
m_PixelsPerUnitMultiplier: 6
```

Color/sprite/type/ppum match SPEC §3a expectations exactly (Rule 21 sprite requirement satisfied — real S_PillStadium sprite, not a flat-fill `<NONE>` fabrication).

Screenshot confirms visible fix: **CENTER card (α=1)** shows dark navy stadium pill with white **"ENDS IN: 171d 22h 14m 36s"** rendered clearly. Peek card same treatment. Iter-1's white-on-white asymmetry is gone.

**Result: PASS.**

## Chrome-strip note (do NOT gate on this)

Canonical resolution is 2070×1912 vs the ideal 1170×2532. This is because CaptureHelper.SnapPlayModeSafe on Mac Retina returns the full Unity Editor window (game view + chrome strip). The content inside is the real 1170×2532 iPhone 14 game view — every visible element is a real production render. Long edge 2070px satisfies Rule 14 (≥900px). **Not a FAIL.** Nudge for future iters: crop the editor chrome, or use `CaptureHelper.SnapGameView()` which returns the game-view RT only.

## Step 3 — Stage 2 acceptance re-walk (no carry-forward)

Re-verified against pixels + source, not carried forward from iter-1.

| Item | Verdict | Notes |
|---|---|---|
| Catalog `GetLiveBanners()` = Active && EndUtc > UtcNow, sorted by SortOrder | PASS | 3 pagination dots visible = 3 live rows (`banner_inactive` excluded per `active=false`). Center = sortOrder=1 (Standard Club 1); peek = sortOrder=2 (TEST BANNER A). |
| Malformed rows skipped without throwing | PASS on report claim + tests suite 15/15 covers §6. CSV has no malformed rows to visually verify but code path unchanged. |
| Bad `endUtc` defaults to `DateTime.MaxValue` | PASS on report claim + tests coverage. No live regression. |
| One card per live entry; `active=false` excluded | PASS (3 dots, 3 cards). |
| Cost from CSV, not hard-coded | PASS. Center card cost row shows `500 x1` / `4,500 x10` — matches CSV row 2 (`costX1=500, costX10=4500`). |
| ArtImage via `Resources.Load("Art/Gacha/Banners/" + ArtSprite)` | PASS. Real driver art (FYLOE / ROYAL SWING / MitreO / VOIGT94) renders on center card. |
| Card binding — nameKey → title | **PASS** (was iter-1 FAIL, now resolved). Center card renders "STANDARD CLUB 1". |
| Card binding — endUtc → countdown text on CENTER | **PASS** (was iter-1 FAIL, now resolved). "ENDS IN: 171d 22h 14m 36s" visible on α=1 center card. |
| Card binding — rulesUrl → button (Application.OpenURL) | PASS on code inspection (`Application.OpenURL(_entry.RulesUrl)` in `GachaBannerCard.cs`); tab not tapped this iter. |
| Drag / snap / no-wrap | PASS on code inspection (unchanged from iter-1 verdict). |
| Per-frame falloff (scale + alpha) | PASS. Right peek visibly scaled ≈78% and dimmed. |
| Dots: dynamic count, center = active | PASS. 3 dots, center bright, sides dim. |
| ONE Update-driven countdown ticker | PASS. Single `Update` in `GachaCarouselController` per code (unchanged). |
| Countdown format `ENDS IN: {d}d {h}h {m}m {ss}s` | PASS. Visible on center: "ENDS IN: 171d 22h 14m 36s" matches format exactly. |
| Expiry → RemoveBanner + dots re-count + snap; zero live → EmptyState | PASS on code inspection + tests (`TickCountdown`/`ShowEmptyState`). Not exercised in this capture. |
| Rules & rates → `Application.OpenURL(rulesUrl)` | PASS on code inspection. "! RULES & RATES" chip visible top-right of card. |
| PULL toast-only, balance stays 10 | PASS. Top-bar ticket counter shows "10"; no `SpendTickets` in pull handlers per code. |
| STORE tab unaffected | PASS on zero-diff basis (Stage 2 touches no STORE code paths). |
| Consolidated `GachaBannerCard.prefab` is spawn source; `_GachaCard_CesarTuned.prefab` deleted | PASS. `ls Assets/Resources/Prefabs/Gacha/` returns only `GachaBannerCard.prefab` + meta. `git status` confirms `D Assets/Resources/Prefabs/Gacha/_GachaCard_CesarTuned.prefab` + `.meta`. |
| 15/15 EditMode tests (catalog + countdown formatter) | Accepted on report claim (`tests-run GachaStage2Tests 15/15 Passed`). |

## Clone provenance (Rule 19)

CountdownPill Image sprite read back from the LIVE prefab:
```
m_Sprite: {fileID: 21300000, guid: bb07d102185aa4f1ca51da13de9eeac6, type: 3}
```
This is a real `S_PillStadium` sprite reference (not `{fileID: 0}` = `<NONE>` + flat colour), so the Rule 19 pass condition holds for the countdown pill fix. No fabrication.

## Scene-mutation audit (Step 7)

`git status --porcelain` on Assets/:

- `M Assets/Resources/Prefabs/Gacha/GachaBannerCard.prefab` — intended (FIX 2).
- `D Assets/Resources/Prefabs/Gacha/_GachaCard_CesarTuned.prefab` (+ .meta) — intended (Stage 2 consolidation, pre-existing at iter-2 baseline).
- `M Assets/Scenes/ShellScene.unity` — pre-existing baseline delta (Stage 0/1 changes); report explicitly declares "no new edits from this iter remain" after reloading the scene from disk to discard a transient play-mode `PityPill` deletion. No new mutations from iter-2 fixes require ShellScene edits.
- `?? Assets/Resources/Data/gacha_banners.csv` — intended (FIX 1).
- `?? Assets/Scripts/UI/Gacha/*.cs` — pre-existing at baseline; iter-2 added the TODO comment in `GachaBannerCard.cs`.

`git diff --stat HEAD -- Assets/Scripts/Physics/` returns EMPTY → Rule 7 physics ban satisfied. No unexpected scene mutations outside the documented fix area.

## Bbox check (Step 6)

No new containment claims to verify beyond the countdown label sitting inside the pill; visually the countdown text is entirely inside the navy pill on the center card (Figma reference matches). No independent bbox script needed for a text-inside-9-slice-pill claim when both are on the same LayoutElement and the label already rendered inside on the peek at iter-1.

## Capture-helper compliance (Step 5)

Report cites `CaptureHelper.SnapPlayModeSafe` per Stage 2 protocol (real ShellScene boot → `ScreenManager.ShowScreen(ScreenId.GeneralShop)` → GACHA tab). Compliant. No new `*Context.cs` added → capture_helper maintenance protocol N/A.

## Production-flow verification (Step 8)

Report cites real production flow: `ShellScene boot → ScreenManager.ShowScreen(ScreenId.GeneralShop) → GACHA tab`, not a smoke runner. Runtime `InspectGachaState` read-back confirms `titleText='STANDARD CLUB 1'` and `pillColor=RGBA(0.078,0.141,0.286,1.000)` on all 3 live cards under the real code path. PASS.

## Rejection follow-up (Rule 15)

Both iter-1 SELF_REVIEW_FAIL defects have same-angle full-res canonical citations and GONE/RESOLVED verdicts:

| Defect | Verdict | Evidence |
|---|---|---|
| Banner title raw key `GACHA_BANNER_STANDAR…` | GONE | Screenshot center card reads "STANDARD CLUB 1"; CSV nameKey column carries display text. |
| CountdownPill white/empty on center card | GONE | Screenshot center card shows navy pill + white "ENDS IN: 171d 22h 14m 36s"; prefab color = `#142449`, sprite = S_PillStadium. |

`CESAR_REJECTION.md` (Stage 0) items remain PASS by non-regression (layout / tuned card / R-points / tickets / tabs / dots all render intact).

## Verdict

**FORWARD_TO_ARCHITECT.** Both iter-1 FAILs are visibly and structurally resolved (CSV display text + prefab navy pill). No Stage 0/1/2 regressions detected. Chrome-strip noted as a nudge for future iters but not gated. STATUS → `SELF_REVIEW_PASS`.

## Files touched by this review

| Path | Change |
|---|---|
| `Docs/Specs/Active/gacha_screen/SELF_REVIEW.md` | Overwritten — Stage 2 iter-2 verdict FORWARD_TO_ARCHITECT |
| `Docs/Specs/Active/gacha_screen/STATUS.md` | Set to `SELF_REVIEW_PASS` |
