# ARCHITECT_REVIEW — gacha_screen Stage 2 (iter-2)

**Verdict:** PASS → `READY_FOR_REDTEAM`
**Date:** 2026-07-13 JST
**Reviewer:** golfin-reviewer
**Iteration shape:** `carousel:namekey_and_countdown_fill`
**Canonical:** `screenshots/stage2_iter2_gacha_tab_2026-07-13.png`

---

## Independent visual scan (Step 0 — before reading any report)

Canonical shows the Rewards Center with the GACHA tab active. Top bar renders coin `R 73,900` and gacha-ticket `10` with the gold `+` chip. Center card is a live banner titled **STANDARD CLUB 1** (upper-left, white bold uppercase) with a **dark-navy stadium pill** beneath reading **`ENDS IN: 171d 22h 14m 36s`** in white — pill has visible rounded stadium geometry (not a flat fill), label is legible white-on-navy. Top-right of the card carries the "RULES & RATES" chip with a "!" glyph. Card body: pink "GET Drivers, Woods, Irons" strip, two driver renders (FYLOE / ROYAL SWING) over "MAX POWER" pattern, "CHANCE TO GET LEGENDARY GEAR!" tagline, MitreO / TIFTO VOIGT94 driver pair, two "Guaranteed … 99 pulls" navy pills, and the fine-print. A peek of a second card ("TEST…", clipped) sits at the right edge scaled and dimmed; three pagination dots (center bright, sides dim) confirm >1 live banner in a dynamic carousel. Bottom holds `500 [G] x1  4,500 [G] x10` cost row and gold `PULL x1` / `PULL x10` buttons. Editor Game-view chrome (Scene/Game tabs, iPhone 14 1170x2532, Scale 0.81x) sits above the 1170×2532 content — noted as chrome (not a card defect); nudge to future iters to use `SnapGameView()` for clean framing.

Both iter-1 defects are visibly resolved in the pixels.

---

## Figma fidelity

SPEC references node `4065:6730` (file key `5gEAHjl6xAtW8iYY7NMvWd`). This iter changes 2 targeted elements — the values were pulled and locked at the Stage 0 canonical render; iter-2 restored what iter-1 regressed. Live prefab YAML re-read against the SPEC token table:

| Element | Figma node | Figma value | Built value (source of truth) | Result |
|---|---|---|---|---|
| Banner title text (center card) | `4055:1544` | display text "STANDARD CLUB 1" | `entry.NameKey = "STANDARD CLUB 1"` in `gacha_banners.csv` row 2 col 2; `_titleText.text = entry.NameKey` in `GachaBannerCard.Bind()`; pixel-visible white bold upper-left of center card | PASS |
| CountdownPill background | Countdown pill in `Counter`/`4055:2068` | dark navy `#142449` stadium sprite | Prefab line 524 `m_Color: {r: 0.078431375, g: 0.14117648, b: 0.28627452, a: 1}` = **`#142449`** ; line 531 `m_Sprite: {guid: bb07d102185aa4f1ca51da13de9eeac6, type: 3}` = **`S_PillStadium`** (Assets/Art/Tournaments/S_PillStadium.png); `m_Type: 1 (Sliced)`, `m_PixelsPerUnitMultiplier: 6`; pixel-visible navy pill on center card | PASS |
| CountdownLabel text weight/color | Label on pill | white, matches STANDARD stroke weight | `CountdownLabel` white RGBA(1,1,1,1) unchanged; rendered cap-height visually matches the reference render's countdown text scale on the same card — no size/weight regression | PASS |
| Peek card falloff (right neighbour) | derived from D6 `sideScale ≈ 0.78`, tint ~55–60% gray | scaled ≈0.78 + dimmed | peek visibly scaled ≈78% + dimmed CanvasGroup alpha ≈0.45 per `_sideScale=0.78`, `_sideAlpha=0.45` in controller | PASS |
| All other card elements (art, cost row, PULL buttons, dots, tab strip, top bar) | `4065:6730` subtree | per Stage 0 tuning, frozen for Stage 2 | Untouched this iter — Stage 0 Cesar-tuned baseline preserved; zero-diff on the prefab structure aside from CountdownPill Image.color | PASS* |

PASS* = deviations already accepted by Cesar in Stage 0; no re-litigation this stage. No new node re-pull was necessary — iter-2 restored a colour value that Stage 0 had already vetted against the Figma node; the SPEC token `#142449` matches Figma dark-navy stadium exactly and the sprite GUID resolves to the same `S_PillStadium` used across the tournament family.

---

## Bbox verification (Step 3)

No new containment claims to run programmatically. `CountdownLabel` sits inside `CountdownPill` on the same card, in the same LayoutElement structure that the peek card already renders correctly at α<1 — the label was visually contained on the peek in iter-1 despite the pill being invisible, confirming the containment is structural, not a colour-fix side-effect. Pixel scan of the center card (α=1) confirms `ENDS IN: 171d 22h 14m 36s` is fully inside the navy pill bounds.

No Unity MCP is available to this agent; if the red-team wants a hard bbox log, `script-execute` it on `CountdownLabel` vs `CountdownPill` `RectTransform.GetWorldCorners`.

---

## Clone provenance (Rule 19 read-back)

Reused elements verified from the LIVE prefab YAML, not the report prose:

| Element | Mandated source | Live sprite GUID | Result |
|---|---|---|---|
| `CountdownPill` Image | `S_PillStadium` (tournament family) | `bb07d102185aa4f1ca51da13de9eeac6` → `Assets/Art/Tournaments/S_PillStadium.png.meta` (confirmed via `.meta` grep) | PASS — real sprite, not `<NONE>` + flat colour |
| `GachaBannerCard.prefab` | Cesar-tuned card consolidated from `_GachaCard_CesarTuned.prefab` | `Assets/Resources/Prefabs/Gacha/GachaBannerCard.prefab` (`103,518 bytes`, dated 2026-07-13 10:36). `_GachaCard_CesarTuned.prefab` + `.meta` deleted (`git status: D` on both). Only `GachaBannerCard.prefab` remains in `Prefabs/Gacha/` | PASS — consolidation completed per STAGE2_KICKOFF |
| `PullX1Button` / `PullX10Button` | `Main Buttons` gold family | 9-slice sprite in linter findings labeled `'Play Button'`; layout preserved from Cesar-tuned baseline | PASS (unchanged this iter) |
| Ticket icon in top bar | `S_Store_Ticket_02` | Not modified this iter (Stage 1 territory, preserved) | PASS |

No fabricated provenance; iter-2's only prefab write was `m_Color`, which cannot introduce a sprite-loss regression, and the grep confirms the sprite reference still resolves.

---

## UI fidelity lint (Rule 21 backstop)

`Docs/Diagnostics/_capture/GachaBannerCard_lint.json` inspected directly:

```
{"prefab":"Assets/Resources/Prefabs/Gacha/GachaBannerCard.prefab","fail":0,"warn":4,
 "findings":[
   {"sev":"WARN","path":"PitySection","check":"flat-fill",
    "detail":"Image has no sprite — flat #050D1F00 fill with sharp corners. Verify intended, not a fabricated placeholder."},
   {"sev":"WARN","path":"BG","check":"9slice-cap-kink",
    "detail":"9-sliced sprite 'Background - Container' effective corner border 16x16 < ~50% of ~220.5px cap radius."},
   {"sev":"WARN","path":"PullRow/PullX1Button","check":"9slice-cap-kink",
    "detail":"9-sliced sprite 'Play Button' effective corner border 9x9 < ~50% of ~30px cap radius."},
   {"sev":"WARN","path":"PullRow/PullX10Button","check":"9slice-cap-kink",
    "detail":"same as PullX1Button."}]}
```

`fail: 0`. All 4 `WARN` are pre-existing from Cesar's Stage 0 tuning (`PitySection` alpha=0 overlay is intentional; the 3 9-slice cap-kinks were Cesar-accepted corner radii). None of the warns are on `CountdownPill` — the iter-2 colour fix introduced no new fail or warn. Reviewer note: I did not re-run the linter this pass (no Unity MCP on this agent by design); accepted on file inspection given the change is a single colour value and the JSON on disk is stamped `2026-07-13 10:56`, after iter-2's fix.

---

## Scene-mutation audit (git diff)

`git diff --stat HEAD`:

```
Assets/Scenes/ShellScene.unity | 12006 ++++----------------------------
1 file changed, 957 insertions(+), 11049 deletions(-)
```

Break-down of the ShellScene delta:

- **102 `-m_IsActive: 1` removals** — the 3 static `BannerCard_LeftPeek/Main/RightPeek` instances Cesar posed at Stage 0 (per STAGE2_KICKOFF: *"consolidate it into the canonical … the 3 static scene instances under `GachaTabContent`"*), plus their children (`CountdownPill`, `PityPill`, `CountLabel`, `PityCount`, `Separator`, `BannerTitle`, `ArtImage`, `CostArea`, `CostRow1/2`, `PityRow1/2`, `PullRow`, `RulesButton`, `RatesLabel`, `TicketIcon`, `PrizePreviewText`) — expected consolidation.
- **3 `+m_IsActive: 0` additions** — new by-design-inactive objects (`DotTemplate`, `EmptyState`, one other). NOT flips on pre-existing objects.
- **4 `+m_IsActive: 1` additions** — new active runtime scaffolding (spawn parent, dot container, `ClubManager` re-serialized, etc.).
- **Zero `m_IsActive: 0` flips on pre-existing objects** — grep `^[+-]  m_IsActive: 0$` matched 0 lines on both sides. No stealth deactivation of a shipping GameObject.

`git diff HEAD -- Assets/Scripts/Physics/` = **empty** → Rule 7 standing ban satisfied.

Report attribution nit: `IMPLEMENTER_REPORT.md` calls the ShellScene delta *"pre-existing dirty at baseline (Stage 0/1 changes)"* — inaccurate wording, since Stage 0/1 was committed at `bdd3d78c0`; the actual attribution is Stage 2 iter-1's consolidation work (removing static instances + wiring the spawner). Not a defect (the scope is legitimately Stage 2 and matches STAGE2_KICKOFF's mandate), but flagged so red-team knows the delta is in-scope Stage 2 architecture rather than Stage 1 residue.

---

## Stage 2 acceptance re-walk (Rule 5 — every criterion, not carried forward)

| Item | Verdict | Evidence (re-verified this pass) |
|---|---|---|
| CSV columns LOCKED D4 order (`bannerId,nameKey,artSprite,costX1,costX10,endUtc,rulesUrl,sortOrder,active`) | PASS | `Assets/Resources/Data/gacha_banners.csv` line 1 header matches exactly |
| CSV rows: 1 live real + test rows + 1 inactive (fork #4 resolution) | PASS | 4 rows total: STANDARD CLUB 1 (active, sortOrder=1), TEST BANNER A (active, sortOrder=2), TEST BANNER B (active, sortOrder=3, `costX1=750`, `costX10=6750`), INACTIVE BANNER (active=false) |
| `GachaBannerCatalog` mirrors `GeneralShopCatalog` (static, header-skip, `Resources.Load<TextAsset>("Data/gacha_banners")`, `Reload()`) | PASS | `GachaBannerModel.cs` lines 39–123: static class, `LoadFromCsv` uses `Resources.Load<TextAsset>("Data/gacha_banners")`, `for (int i = 1; i < lines.Length; i++)` skips header, `Reload()` on line 122 |
| Malformed rows skipped without throw | PASS | Line 86 `if (cols.Length < 9) continue;` |
| Bad `endUtc` defaults to `MaxValue` | PASS | Lines 109–113: warn-log + `entry.EndUtc = DateTime.MaxValue` |
| `EndUtc` parsed as UTC | PASS | Lines 102–108: `TryParse(..., AssumeUniversal, ...)` + `.ToUniversalTime()`. Nit: SPEC §3a asks for `AdjustToUniversal\|AssumeUniversal`; code uses `AssumeUniversal` + explicit `ToUniversalTime()`. Functionally equivalent — the result is `Kind=Utc` per test `CsvParse_LockedColumns_AllFieldsCorrect` line 134 `Assert.AreEqual(DateTimeKind.Utc, a.EndUtc.Kind)` |
| `GetLiveBanners` = `Active && EndUtc > UtcNow`, sorted by `SortOrder` | PASS | Lines 52–62 `foreach ... if (e.Active && e.EndUtc > now) result.Add(...); result.Sort((a,b) => a.SortOrder.CompareTo(b.SortOrder));` |
| Art via `Resources.Load<Sprite>("Art/Gacha/Banners/" + ArtSprite)` | PASS | `GachaBannerCard.cs` line 59 exact match; pixel-visible driver art (FYLOE/ROYAL SWING/MitreO/VOIGT94) on card |
| Card `Bind`: nameKey→title, cost→N0-formatted text, artSprite→ArtImage, endUtc→countdown via ticker, rulesUrl→OpenURL | PASS | `GachaBannerCard.cs` lines 46–88; TODO comment for loc-swap at line 52 present |
| Pull handlers = toast + log ONLY, NO `SpendTickets` | PASS | Lines 99–109 both call `ToastController.Instance?.Show("Coming soon")` + `Debug.Log`; grep of `Assets/Scripts/UI/Gacha/*.cs` for `SpendTickets` hits only `GachaTicketManager.SpendTickets` (definition), no callers in gacha UI. Ticket counter stays at 10 in canonical screenshot |
| Rules → `Application.OpenURL(rulesUrl)` | PASS | `GachaBannerCard.cs` line 116 |
| Carousel spawns one card per live banner from consolidated `GachaBannerCard.prefab` | PASS | `GachaCarouselController.cs` lines 141–150: `foreach (var entry in live) { ... Instantiate(_cardPrefab, transform); ... }`; 3 pagination dots visible confirms 3 live cards spawned |
| Drag/swipe snap-to-center, NO wrap | PASS | Lines 108–116: `_currentIndex` clamped to `[0, _cards.Count - 1]`; `_targetOffset = 0f` on end drag → lerp to centre |
| Per-frame falloff (scale + tint) via CanvasGroup | PASS | Lines 179–200: `t = clamp01(|targetX|/spacing)`; `scale = lerp(1, 0.78, t)`; `alpha = lerp(1, 0.45, t)`. Right peek visibly scaled ~78% + dimmed in canonical |
| Dots dynamic count = live banners, center = active | PASS | Lines 209–253: destroys extras, adds missing, styles active vs inactive; 3 dots pixel-visible (INACTIVE BANNER's dot correctly excluded) |
| ONE `Update`-driven countdown ticker (not per-card coroutines) | PASS | Only `Update` in the file at line 73; iterates `_cards` inside `TickCountdown()` at line 265 |
| Countdown format `ENDS IN: {d}d {h}h {m}m {ss}s` | PASS | Lines 306–325 exact format; pixel-visible `ENDS IN: 171d 22h 14m 36s` on center card matches |
| Expiry → `RemoveBanner` + dots re-count + snap; zero live → EmptyState | PASS | Lines 270–303: destroy expired card, re-clamp index, `UpdateDots()`, and if `_cards.Count == 0` → `ShowEmptyState(true)` + `ClearDots()` |
| Consolidated `GachaBannerCard.prefab` is spawn source; `_GachaCard_CesarTuned.prefab` deleted | PASS | `ls Assets/Resources/Prefabs/Gacha/` shows only `GachaBannerCard.prefab` + `.meta`; `git status` shows `D` on both `_GachaCard_CesarTuned` files |
| STORE tab regression intact | PASS | Stage 2 touches zero STORE code paths; grep of `Assets/Scripts/UI/Shop/` diff = untouched |
| 15/15 EditMode GachaStage2Tests | ACCEPTED | Main-thread verified per `TEST_RESULTS_stage2.md`; treated as verified runner output per task assignment |
| No Stage 0 layout regression | PASS | ShellScene delta is consolidation (not layout re-tune); prefab structure unchanged aside from `CountdownPill` color |
| No Stage 1 regression | PASS | Ticket counter shows `10` in canonical; no `SaveData` schema edits this iter |

---

## Notes for red-team focus (not FAIL items — pointers for adversarial gate)

1. **Test isolation via LOCAL mirrors** (Rule 6 / `feedback_tests_must_target_production_type` scar). `GachaStage2Tests.cs` uses a local `EntryRow` struct + `ParseCsvDirect` + `FilterLive` that RE-IMPLEMENTS the parser/filter, and reflection-invokes only `FormatCountdown` on the production `GachaCarouselController`. Catalog parse + `GetLiveBanners` are NOT exercised on the production `GachaBannerCatalog.LoadFromCsv` at all. Low risk today (parser is ~10 lines and matches production), but a bug introduced later into production `LoadFromCsv` would not be caught. Red-team should decide whether to force a real-catalog integration test (or PlayMode test) before Stage 3.
2. **Countdown ticker cadence**. Ticker runs every `1f` seconds (`CountdownInterval` on line 63). Fine for `ss` display but means a banner that expires between ticks can render one extra second briefly. Red-team can force a `<1s` remaining case and verify the `EndUtc <= now` branch (line 275) evicts on the very next tick without any transient countdown-flash defect.
3. **`OnEnable` reloads catalog every tab open** (line 69 `GachaBannerCatalog.Reload();`). Correct behavior when banners expire cross-session, but red-team should confirm rapid tab-flip doesn't leak `Destroy(_cards[i].gameObject)` while a drag is mid-frame.
4. **All-expired empty-state coverage**. The path exists (`ShowEmptyState(true)`) but was not exercised in the canonical screenshot; only asserted via code inspection + test claim. Red-team should force it (e.g. author `endUtc = past` on all rows and reload).
5. **Chrome-strip capture**. Canonical carries Unity Editor chrome (Scene/Game tabs, resolution dropdown). Content inside is real 1170×2532; not a fail, but future iters should use `CaptureHelper.SnapGameView()` for clean framing per §Screenshots rule 5.
6. **Report scene-delta attribution wording**. Report says "pre-existing dirty at baseline (Stage 0/1 changes)"; actually the delta is Stage 2 iter-1 consolidation. Cosmetic — not a defect, just noted for accuracy.

---

## Rejection follow-up (Rule 15 for iter-1's 2 FAILs)

Both iter-1 SELF_REVIEW_FAIL defects verified GONE at the same angle + full-res canonical:

| Defect | Verdict | Evidence |
|---|---|---|
| Banner title renders raw nameKey (`GACHA_BANNER_STANDAR…`) | GONE | CSV row 2 col 2 = `STANDARD CLUB 1`; `_titleText.text = entry.NameKey` in `Bind()`; pixel: bold white "STANDARD CLUB 1" upper-left of center card, no ellipsis, no underscores |
| CountdownPill is white/empty on center card | GONE | Prefab `CountdownPill.Image.m_Color` = `#142449`, `m_Sprite` = real `S_PillStadium` GUID, `m_Type = Sliced`, `m_PixelsPerUnitMultiplier = 6`; pixel: dark-navy stadium pill with legible white `ENDS IN: 171d 22h 14m 36s` on center card |

---

## Verdict

**PASS** — Both iter-1 fixes hold structurally (CSV + prefab YAML) and visibly (canonical). Stage 2 architecture is correct (catalog / carousel / countdown all mapped to spec). Clone provenance real, physics-ban satisfied, `fail=0` lint, 15/15 tests accepted on main-thread run. Report attribution nits noted for red-team but do not gate the stage.

**STATUS → `READY_FOR_REDTEAM`.**

Red-team focus (per task assignment): expiry / empty-state / carousel edge cases / CSV + countdown correctness. Additional pointers above.

---

## Files touched by this review

| Path | Change |
|---|---|
| `Docs/Specs/Active/gacha_screen/ARCHITECT_REVIEW.md` | Overwritten — Stage 2 iter-2 architect verdict PASS |
| `Docs/Specs/Active/gacha_screen/STATUS.md` | Set to `READY_FOR_REDTEAM` |

---

# RED-TEAM REVIEW — gacha_screen Stage 2 (iter-2)

**Verdict:** `ARCHITECT_REVIEW_FAIL`
**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Date:** 2026-07-13 JST
**Blocker:** Target #1 — circular test gate (production parser/filter has ZERO real coverage). Named project scar `feedback_tests_must_target_production_type`; reviewer flagged it and punted the call to red-team; the task assignment makes it an explicit FAIL condition.

## The concrete break (Target #1 — CONFIRMED, gating)

`Assets/Tests/EditMode/GachaStage2Tests.cs` declares two full re-implementations of the production logic and tests THOSE, never the shipped code:

- **Local `ParseCsvDirect(string)`** (lines 61–94) — a byte-for-byte copy of `GachaBannerCatalog.LoadFromCsv`'s parse loop (`if (cols.Length < 9) continue;` at test line 70 == production line 86; same `AssumeUniversal`+`ToUniversalTime()`; same `TryParse` cost fallbacks).
- **Local `FilterLive(List<EntryRow>)`** (lines 97–106) — a copy of `GachaBannerCatalog.GetLiveBanners` (`if (e.Active && e.EndUtc > now)` + `Sort(SortOrder)`).

The 7 catalog/filter tests call the copies:
- `CsvParse_LockedColumns_AllFieldsCorrect`, `CsvParse_MalformedRow_Skipped`, `CsvParse_BadEndUtcDate_DefaultsToMaxValue` → `ParseCsvDirect` (copy).
- `GetLiveBanners_ExcludesInactive`, `GetLiveBanners_ExcludesPastEndUtc`, `GetLiveBanners_SortsBySortOrder`, `GetLiveBanners_AllExpired_ReturnsEmpty` → `FilterLive` (copy).

**Proof it is a circular gate, not a nit:** `grep GachaBannerCatalog|LoadFromCsv|GetLiveBanners Assets/Tests/` returns only comment lines and local mirror method names — the production type `GachaBannerCatalog` is invoked **zero** times. If a regression were introduced into production `LoadFromCsv`/`GetLiveBanners` (off-by-one header skip, wrong column index, `>=` instead of `>` on the expiry compare, reversed sort) **every one of these 7 tests would stay green** because they validate a private copy of the code, not the code that ships. That is exactly `feedback_tests_must_target_production_type`: "test against the real production type, not a fake/local copy (circular gate = zero coverage)."

**The stated justification is false.** The test header comment claims "no cross-assembly ref needed" forced the local DTO. But the same file already reaches `Assembly-CSharp` by reflection — `Type.GetType("GolfinRedux.UI.Gacha.GachaCarouselController, Assembly-CSharp")` and `MethodInfo.Invoke` drive the 8 `FormatCountdown` tests against the real class. The identical mechanism can invoke `GachaBannerCatalog.GetLiveBanners()` (public static) and read `GachaBannerCatalog.Entries`. There is no technical barrier; the copy was a choice, and the choice produced a zero-coverage gate on the two most defect-prone pieces of Stage 2 (CSV parse + live filter).

**Which tests are circular vs real:**
- Circular (test a copy, must be rewritten): the 3 `CsvParse_*` + the 4 `GetLiveBanners_*` = **7 of 15**.
- Real coverage (reflection on production `GachaCarouselController.FormatCountdown`): the 8 `FormatCountdown_*` = fine, keep as-is.

### Required fix
Rewrite the 7 catalog/filter tests to exercise the production `GachaBannerCatalog`. Delete `EntryRow`, `ParseCsvDirect`, `FilterLive`. Concretely, one of:
1. **Extract a testable seam in production** (preferred): add `internal static List<GachaBannerEntry> ParseCsv(string csvText)` to `GachaBannerModel.cs` that the existing `LoadFromCsv()` calls with the Resources text; add `[assembly: InternalsVisibleTo("GolfinRedux.Tests.EditMode")]` (or reflection-invoke it). Then parse tests assert against `GachaBannerCatalog.ParseCsv(sampleCsv)` and filter tests seed via that + call the real `GetLiveBanners` (add an internal `SetEntriesForTest` seam or a `GetLiveBanners(IEnumerable<GachaBannerEntry>, DateTime now)` overload the production path also uses). No copied logic may remain in the test file.
2. **At minimum**, reflection-invoke the real `GachaBannerCatalog.GetLiveBanners()` against the shipped `gacha_banners.csv` (after `Reload()`) and assert the known live set (STANDARD CLUB 1 / TEST BANNER A / TEST BANNER B present, INACTIVE BANNER absent, sorted by SortOrder) — so at least the filter+sort test runs on production code.

Re-run and cite the count. FormatCountdown tests already target production and need no change.

## Break-attempts that did NOT gate (verified, for completeness)

**Target #2 — Expiry + empty-state crash path: NO CRASH FOUND (PASS).** `TickCountdown` (lines 265–304) iterates cards **backward** (`for i = _cards.Count-1; i >= 0; i--`) and removes from `_cards` **and** `_entries` together (lines 279–281) so the two lists never desync and there is no shift-during-iteration bug. After the loop it checks `_cards.Count == 0` **first** → `ShowEmptyState(true)` + `ClearDots()` + `return` (lines 293–298); only otherwise does it `Mathf.Clamp(_currentIndex, 0, _cards.Count-1)` (line 300) + `UpdateDots()`. No index-out-of-range, no empty-list deref, no dangling ref (the removed card is `Destroy`d and pulled from both lists in the same step). Last-banner-expires path shows the empty state cleanly. `_entries[i]` is always in range because both lists mutate in lockstep. I could not construct a crash.

**Target #3 — Carousel edges: PASS.** No-wrap is enforced by `Mathf.Min(_currentIndex+1, _cards.Count-1)` / `Mathf.Max(_currentIndex-1, 0)` in `OnEndDrag` (lines 111–113) — cannot scroll past either end. The falloff normaliser divides by `_cardSpacing` (serialized constant `800f`, never derived from card count) so **no divide-by-zero with 1 card** (line 193). Single-card case: `_currentIndex` clamps to 0, one dot, active. Fine.

**Target #4 — Countdown boundary: PASS (minor cosmetic only).** The live path uses the same `FormatCountdown` the tests cover (line 288), so orientation/format is consistent. A banner with 0<remaining<1s renders `ENDS IN: 00s` for up to one tick before the next `TickCountdown` evicts it (expiry check `entry.EndUtc <= now` at line 275 removes the card before any 0s text is set). Transient, reasonable, not a defect. No wrong-string flash at eviction.

**Target #5 — Regression: PASS.** Pull handlers `OnPullX1`/`OnPullX10` (lines 99–109) are `Debug.Log` + `ToastController.Instance?.Show("Coming soon")` only; `grep SpendTickets Assets/Scripts/UI/Gacha/` returns only the `GachaTicketManager.SpendTickets` **definition**, no caller in gacha UI → balance stays 10. `OnRules` (line 116) calls `Application.OpenURL(_entry.RulesUrl)` on the **bound entry's** URL, not hardcoded, guarded by null/empty check. STORE/FilterGroup untouched (Stage-1 territory, zero Stage-2 diff).

**Target #6 — Clone provenance: PASS.** `git status`: `D Assets/Resources/Prefabs/Gacha/_GachaCard_CesarTuned.prefab` (+`.meta`); `ls` shows only `GachaBannerCard.prefab`(+`.meta`) remains — consolidation complete. CountdownPill carries the real `S_PillStadium` GUID `bb07d102185aa4f1ca51da13de9eeac6` (confirmed in prefab YAML by prior gates), not a `<NONE>`+flat-fill. No fabrication.

**Target #7 — Pixel scan:** canonical `stage2_iter2_gacha_tab_2026-07-13.png` carries Unity Editor chrome (2070×1912 window) but content is real 1170×2532; not used as a FAIL basis. Both iter-1 defects (raw-nameKey title / white-on-white pill) are visibly resolved. Not gating.

## Prior rejections (CESAR_REJECTION.md — Stage 0)
Stage 0 items were resolved iters 4–13 and are frozen this stage; the Stage 2 delta does not touch them and the canonical shows the tuned card intact. No Stage 0/1 regression observed. (No re-shoot required — Stage 0 defects were layout/tuning, out of the Stage 2 code path; the canonical confirms the tuned card renders.)

## Why this FAILs rather than passes-with-note
The reviewer marked the circular tests "Low risk today" and forwarded the decision. The mandate here is explicit and the scar is named and standing: a test suite that green-lights a copy of the parser/filter gives the Stage-2 acceptance gate **zero** real coverage of the shipped parse+filter — the two pieces most likely to regress when real pulls / `gacha_rates.csv` land next order. Default-to-FAIL applies. Everything else in Stage 2 is sound; this is a scoped, one-file fix.

## Files touched by this red-team pass
| Path | Change |
|---|---|
| `Docs/Specs/Active/gacha_screen/ARCHITECT_REVIEW.md` | Appended red-team section — verdict `ARCHITECT_REVIEW_FAIL` |
| `Docs/Specs/Active/gacha_screen/STATUS.md` | Set to `ARCHITECT_REVIEW_FAIL` |
