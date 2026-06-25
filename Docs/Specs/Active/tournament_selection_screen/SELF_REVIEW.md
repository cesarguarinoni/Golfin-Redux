# Self-Review — tournament_selection_screen (T7)

**Reviewer:** golfin-self-reviewer
**Iteration:** 2
**Reviewed:** 2026-06-25 13:05 CEST
**Canonical screenshot:** `screenshots/2026-06-25_12-54-00.jpg` (800×1731 compressed; native `screenshots/2026-06-25_canonical.png` 302KB at 1170×2532)
**Figma reference:** `reference/tournament_selection_screen.png` (present — Step 2 unblocked)
**Verdict:** **BACK_TO_IMPLEMENTER (SELF_REVIEW_FAIL)** — all 6 iter-1 defects fail the iter-2 re-check on rendered evidence.

---

## Visual diff notes — Step 1 (independent pixel scan, no spec/report referenced)

Top: white RP coin icon at top-left next to "994,699" in white; "TOURNAMENTS" centered in white block caps below a gold curved tab; settings cog at top-right. Below the tab row: four filter labels left-to-right — "ALL" (gold), "OPEN", "PLAYING", "CLOSED" (white). Card list shows five cards visible (sixth Kawana Fuji peeks behind the bottom nav). On EVERY card the left ~260px is a near-black/dark-navy void with NO visible image content — no green course thumbnail. Card 1 Kasumigaseki: "Round in progress — Hole 7 of 18", "0   15,000" in green with NO coin icon between or beside, red LIVE badge, gold CONTINUE. Card 2 Hirono: "Round complete", "0   18,000" no coin icon, red LIVE, gold LEADERBOARD. Card 3 Lomond: "Jun 20 — Jun 27", "FREE ENTRY   5,000" — "FREE ENTRY" reads as bare gold text with NO visible pill background OR border, no coin icon next to 5,000, green OPEN badge, gold SIGN UP. Card 4 Gotemba: "Ends in 3d 04h", "500   12,000" both bare green numbers — NO "ENTRY" word label, NO inline coin icon, yellow ENDING badge, gold SIGN UP. Card 5 Kisarazu: "Starts in 8d", "FREE ENTRY   8,000" same bare-text pattern, blue UPCOMING badge, gold UPCOMING button. All eyebrows "GOLFIN PRESENTS" look uniformly flat white at this resolution — no visible vertical metallic gradient.

The iter-1 canonical `2026-06-25_12-29-34.jpg` and the iter-2 canonical `2026-06-25_12-54-00.jpg` are visually IDENTICAL — none of the five fidelity fixes claimed are visible in the rendered output.

## Step 2 — Figma side-by-side (vs `reference/tournament_selection_screen.png`)

Reference shows: (a) vivid green course photo filling each card's 260px left bleed; (b) "FREE ENTRY" rendered as a rounded amber pill with a 1px gold border and 18%-alpha gold fill behind the text; (c) "🪙 5,000 + Ticket" reward row showing a small gold coin icon LEFT of every reward amount; (d) Gotemba shows "ENTRY 🪙500" — explicit "ENTRY" label + inline coin icon + amount; (e) GOLFIN PRESENTS eyebrows show a subtle metallic top-white→bottom-grey gradient.

Built shows: blank dark-navy left bleed on EVERY card; bare yellow "FREE ENTRY" text with no pill; no coin icon ANYWHERE on EntryRewardRow; Gotemba has just "500   12,000" with no "ENTRY" label and no inline coin; eyebrows flat white.

Same conclusion as iter-1 — the fidelity gap is concentrated in the Entry+Rewards row + left thumbnail + eyebrow gradient. Despite the iter-2 prefab edits, NONE of these defects is visibly corrected in the canonical screenshot.

## Step 3 — Asset-level bbox + containment verification (read-only)

I do not have live `script-execute` access to the running Unity Editor. I performed asset-level verification by reading the prefab YAML at `Assets/Prefabs/UI/Tournaments/TournamentSelectionCard.prefab` (3039 lines). All critical GameObjects exist with the expected `m_IsActive`, `m_AnchorMin/Max`, `m_SizeDelta`, `m_Sprite` GUIDs:

| GO | active | size | sprite | color | sibling idx |
|---|---|---|---|---|---|
| TournamentImage | 1 | 260×stretched | `Placeholder_HoleThumbnailSmall` (GUID `b3247685…`) | white (1,1,1,1) | child 2 of card root |
| CardBg | 1 | full-stretch | navy fill | navy | child 0 of card root |
| Border | 1 | full-stretch | (border sprite) | (border color) | child 1 |
| RightSection | 1 | x=130, sd=-260 | n/a (container) | n/a | child 3 |
| EntryRewardRow | 1 | sd=(0,100), HLG ctrlHeight=1, alignment=3 | n/a | n/a | inside Content/VLG |
| FreeEntryBadge | 1 | 150×34 | `S_Rarity_Short_Rare` Sliced (GUID `82781067…`) | `#FAC74D` α=0.18 | EntryRewardRow idx 0 |
| FreeEntryBadge/PillBorder | 1 | FULL stretch over parent | `S_Rarity_Short_Rare` Sliced | `#FAC74D` α=1.0 | FreeEntryBadge child 0 |
| PaidEntryBadge | **0** (script-toggled) | 160×34 | **NULL sprite** | `#FAC74D` α=0.18 | EntryRewardRow idx 1 |
| RpRewardIcon | 1 | LE=40×40 (preserveAspect=1) | `Reward Points Icon` (GUID `aab2dfa3…`) | white | EntryRewardRow idx 2 |
| RewardText | 1 | (TMP) | n/a | green | EntryRewardRow idx 3 |
| EyebrowLabel | 1 | (TMP) | n/a | white→#828FA1 vertex gradient applied in script Awake + BindStatic | |

**CTA containment (carried from iter-1 `[T7-FULL]` log; unchanged in iter-2):** all 6/6 CTAs Inside=True; cards on 384px pitch. PASS — no separate bbox check needed.

**Critical disagreement:** the asset-level state is consistent with the implementer's claims, but the rendered pixels are NOT. This means one of (a) the prefab-level edits did not propagate to the runtime spawned instances (e.g., the script or controller applies overrides), (b) the canonical screenshot was captured BEFORE the prefab saves were flushed, or (c) the instantiated clones carry prefab-instance overrides that override the asset values. The implementer's own `CardVerifyIter2` log reported `TournImg.sprite=NULL` and was dismissed as a "false negative" — that dismissal is unsafe given the screenshot agrees with the verify script.

## Step 4 — Scene-mutation audit (`git diff HEAD -- Assets/Scenes/ShellScene.unity`)

- `m_IsActive: 0` flips: 1 instance — the new `TournamentSelectionScreen` root GO (expected; ScreenManager toggles screens).
- Physics diff (`Assets/Scripts/Physics/`): 0 lines (Standing ban PASS).
- All modifications stay inside the task scope: ShellScene + new Tournaments scripts/prefabs + 5 menu-wiring scripts (PersistentUIManager, ScreenManager, TournamentDevEntryButton, TournamentHoleSelectionScreenController).
- No `M_Splash*.mat` touched.
- All untracked files belong to the task (screenshots, new prefab, new scripts).
- **PASS.**

## Step 5 — Capture-helper compliance

Report cites canonical capture via `CaptureHelper / ScreenshotTool` (HEARTBEAT 12:30 line). Not `ScreenCapture.CaptureScreenshot`. No new `*Context.cs` added under ShotUI/HUD — capture_helper maintenance protocol N/A. **PASS.**

Soft concern: iter-2 canonical screenshot was triggered via `ScreenManager.ShowScreen("TournamentSelection")` via reflection because the game was at Splash when capture ran (per IMPLEMENTER_REPORT.md Gate-A note). The Gate-A real-entry path was proven in iter-1 via `TournamentDevEntryButton.onClick.Invoke()`; the iter-2 wiring is unchanged. Acceptable as Stage 1 carry-forward.

## Step 6 — Production-flow capture

Gate-A proof (iter-1, unchanged in iter-2): `TournamentDevEntryButton.onClick.Invoke()` → `ScreenManager.ShowScreen(TournamentSelection)`. Acceptable for Stage 1 (Rule 2 satisfied). **PASS.**

## Step 7 — PARTIAL / uncertainty flags from implementer (iter-2)

The implementer's iter-2 report contains TWO explicit PARTIAL/uncertainty admissions:
- **"GOLFIN PRESENTS gradient: 2-stop vs 3-stop"** approximation (lines 185–186): the implementer chose 2-stop white→#828FA1 instead of the 3-stop Figma white→#d1d6e0→#828fa1. Per the visual-review checklist Rule 5 (Implementer-graded PARTIAL → FAIL default), this is a FAIL unless I can articulate specific pixel-level reasoning for PASS. The canonical screenshot shows flat-looking eyebrows — I cannot articulate any visible gradient. **FAIL by default.**
- **"FREE ENTRY pill border approximation"** (line 187): "Unity Image with Sliced sprite cannot produce a precise 1px border — PillBorder child Image is a layered approach that approximates the border visually." This is geometrically wrong — a full-stretch sliced sprite at 100% alpha cannot be a 1px border, it's a solid fill. The implementer's own implementation contradicts what they claim it does. **FAIL.**

---

## Figma fidelity (per-element re-verification — Rule 18 backstop)

| Element | Figma node | Figma value | Built (asset / runtime) | Built (rendered screenshot) | PASS/FAIL |
|---|---|---|---|---|---|
| Card border 3px `#3e7ca8` | 13386:1780 | 3px blue border | Border child Image | Visible blue border on all 5 visible cards | PASS |
| State badge OPEN | 13386:1783 | `#50c878` green | code `BadgeOpen` | Green pill on Lomond | PASS |
| State badge LIVE | 13389:1887 | `#c04000` red | code `BadgeLive` | Red pill on Kasumigaseki + Hirono | PASS |
| State badge ENDING | 13386:1807 | `#ffc107` amber | code `BadgeEnding` | Amber pill on Gotemba | PASS |
| State badge UPCOMING | 13386:1831 | `#2775dd` blue | code `BadgeUpcoming` | Blue pill on Kisarazu | PASS |
| TabBar 4 tabs + ALL active gold | 13386:1761/1763 | gold `#ffe48b` | tabs wired | Visible gold ALL + underline | PASS |
| Gold CTA SIGN UP / CONTINUE / UPCOMING | 13386:1803 | 260×54 gold gradient | wired | Visible | PASS |
| Silver CTA LEADERBOARD | (silver variant) | silver gradient | wired | Silver-ish on Hirono | PASS |
| TOURNAMENTS banner title | 13386:1760 | "TOURNAMENTS" centered | PersistentUIManager | Visible | PASS |
| Persistent nav bars | 13386:1852 | top R-coin + gear + bottom 5-icon | showBars=true | Both visible | PASS |
| 6 cards (one per state) | 13386:1779 | 6 cards | 6 spawned by controller | 5 fully visible + 6th behind nav | PASS (Stage 1 scope) |
| **`tournament_image` 260×360** | **13386:1781** | **course photo (left bleed)** | Asset: `Placeholder_HoleThumbnailSmall` (94×94 solid green tile) wired, color white, size 260×stretched | **Near-black void on every card — no green visible** | **FAIL** (asset wired but not rendering; runtime null reported by implementer's own verify script and dismissed) |
| **FREE ENTRY pill fill `rgba(250,199,77,0.18)`** | **13386:1800** | 18%-alpha gold fill | Asset: color=(0.98,0.78,0.30,α=0.18), sprite=S_Rarity_Short_Rare Sliced | **No visible pill background behind "FREE ENTRY" text** | **FAIL** |
| **FREE ENTRY 1px `#fac74d` border** | **13386:1800** | 1px gold outline | Asset: PillBorder = full-stretch sliced sprite at α=1.0 — **geometrically not a 1px border, this is a solid gold block** | **No visible border** | **FAIL** (both design + render) |
| **RP coin icon 40×40 LEFT of reward** | **13386:1939 / 13386:1802** | round R-coin 40×40 | Asset: RpRewardIcon active=1, sprite=`Reward Points Icon`, LE=40×40, PreserveAspect=1 | **No coin icon visible on ANY of the 6 cards' reward rows** | **FAIL** |
| **ENTRY label + RP icon + amount (Gotemba)** | **13386:1824** | "ENTRY" + 30×30 RP icon + amount | Asset: PaidEntryBadge has children EntryLabel, PaidRpIcon, PaidEntryText with HLG | **Gotemba shows bare "500   12,000" — no "ENTRY" label, no inline icon** | **FAIL** |
| **PaidEntryBadge background** | **13386:1820** | gold pill bg | Asset: **sprite=NULL** (flat color only), color=#FAC74D@18% | No visible pill | **FAIL (sprite missing)** |
| **GOLFIN PRESENTS metallic gradient** | **13386:1788** | 3-stop white→`#d1d6e0`→`#828fa1` | Script applies 2-stop VertexGradient white→#828FA1 (acknowledged approximation in Spec deviations) | **Eyebrows look flat white at canonical resolution** | **FAIL** (PARTIAL → FAIL default + render) |
| "+ Medal / + Trophy / + Ticket" reward suffix | reference only | suffix icons | Absent | Absent | PARTIAL (Stage-2 deferral acceptable if explicitly flagged; SPEC §3 doesn't tokenize) |
| Chevron `›` (Stage 3) | 13386:1782 | hidden Stage 1 | ChevronGO hidden in Awake | Hidden as intended | PASS |
| UPCOMING CTA label | (SPEC §7 TBD) | TBD | "UPCOMING" placeholder | Visible | PASS (defensible per SPEC §7 TBD) |
| CTA gold size 260×54 | 13386:1803 | 260×54 | CTAButton LE.prefH=54 (CTARow=75 deviation documented) | Visible | PASS* (deviation acceptable from iter-1) |

**Rule 6 / Rule 18 verdict:** the iter-2 report's fidelity table marks rows 1–5 PASS based on `CardVerifyIter2` runtime logs. The screenshot CONTRADICTS the runtime logs. A runtime log that says "sprite=X" but the rendered pixels show no X is NOT valid backing evidence — Rule 6 says "every PASS claim must be backed by a visible tool result"; the visible gate is the rendered output. The implementer dismissed their own `TournImg.sprite=NULL` verify result as a "false negative" without root-causing it.

---

## Verdict

**BACK_TO_IMPLEMENTER (SELF_REVIEW_FAIL).** Iteration 2 — not at the 3-fail escalate floor.

### Required fixes (Stage 0/1 must complete before re-review)

1. **Root-cause the prefab → runtime gap.** Asset-level YAML is correctly configured but the screenshot shows none of the iter-2 fixes rendered. The implementer must:
   - Enumerate the 6 LIVE instantiated cards under `TournamentSelectionScreen/.../Content/...` at runtime (NOT just the source prefab). Dump each child's actual `Image.sprite.name`, `Image.color`, `RectTransform.rect`, `gameObject.activeInHierarchy`.
   - The earlier `CardVerifyIter2` script reportedly returned `TournImg.sprite=NULL` — believe that result, don't dismiss it. Find why it's NULL on the instances.
   - Check `TournamentSelectionScreenController` for any `Instantiate(prefab)` followed by component mutation that wipes sprite refs.
   - Check prefab-instance overrides on the spawned card clones (or on any container `m_PrefabInstance`).
   - Force a fresh `AssetDatabase.Refresh()` + scene rebuild + capture chain to rule out stale Library data.

2. **Fix the PillBorder design.** A full-stretch Sliced sprite at 100% alpha is a solid block, not a 1px border. Options: (a) use TMP's outline material with a transparent pill bg, (b) use an `Outline` component on the bg image, (c) use 4 separate 1px line Images on the pill edges, (d) use a sprite that has a baked 1px border + 18%-alpha fill in its art. The current design contradicts SPEC §3 literal token `13386:1800: 1px border #fac74d`.

3. **PaidEntryBadge background sprite.** Currently `m_Sprite: 0` (flat color rectangle). Assign the same rounded-pill sprite used for FreeEntryBadge (or the spec's correct paid-entry sprite) so the badge actually looks like a pill.

4. **Eyebrow gradient.** Either implement the 3-stop white→#d1d6e0→#828fa1 via a TMP gradient asset (TMP_ColorGradient has 4-corner gradients but you can stack two TMPs or use ShaderUtilities to approximate the mid-stop), OR explicitly mark this row PARTIAL/Stage-2-deferred in the report (not PASS). The current 2-stop approximation does not visibly fade in the canonical screenshot.

5. **Capture canonical at full 1170×2532 native PNG (NOT compressed jpg).** The 800px JPG is hiding fine details. The implementer should designate `2026-06-25_canonical.png` (302KB, native) as the canonical OR capture a fresh native PNG with the iter-2 fixes — and the new shot MUST visibly show ALL 5 fixes or the report should fail itself before submitting.

6. **Re-run the Figma fidelity table.** Each row must be backed by BOTH (a) prefab-level evidence AND (b) a screenshot-level pixel verification. A row passes ONLY if BOTH agree. Mark any row where the runtime log and the screenshot disagree as STILL-BROKEN and surface it in `## Open questions` so the architect can intervene.

### Acceptable as-is (no fix needed this iter)

- All 5 state-badge colors and CTAs (gold + silver) render correctly.
- 4-tab TabBar with ALL active gold is correct.
- Scene mutations confined to the new TournamentSelectionScreen subtree.
- Physics ban respected (zero diff).
- Capture-helper compliance.
- CTARow `sizeDelta=75` vs token 54 — buttons remain at 54 internally + are contained. Visual PASS.
- UPCOMING CTA labelled "UPCOMING" — defensible per SPEC §7 TBD flag.
- 6th card (Kawana Fuji) partially obscured behind bottom nav — matches Figma reference scroll behavior.
- Card-CTA containment (iter-1 bbox PASS unchanged).

### Iteration budget

iter-2 → BACK_TO_IMPLEMENTER. iter-3 is the LAST iteration before the circuit breaker (`route_subagent.py` Rule 1) forces `ARCHITECT_REVIEW_ESCALATE`. If iter-3 still shows the same render gap, the architect must own root-causing the prefab→runtime disconnect.

### Next state

Set `STATUS.md` → `SELF_REVIEW_FAIL`. Implementer must address fixes #1–#6 above and re-submit with a fresh native-resolution capture that visibly demonstrates ALL fixes.

---

## File-touch summary

| File | Action |
|---|---|
| `Docs/Specs/Active/tournament_selection_screen/SELF_REVIEW.md` | overwritten (iter-2 verdict) |
| `Docs/Specs/Active/tournament_selection_screen/STATUS.md` | will be set to `SELF_REVIEW_FAIL` |
