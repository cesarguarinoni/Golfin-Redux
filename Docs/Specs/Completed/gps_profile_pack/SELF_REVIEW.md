# Self-Review — `gps_profile_pack` (iter-1)

## Verdict

**FAIL** → `SELF_REVIEW_FAIL` → routes back to golfin-implementer.

The three built screens do not resemble the reference in structure OR content. Multiple hard-mandated
elements are structurally absent from the prefabs (equip slots on Avatar, badge cells on Badges,
per-shortcut icon rings on Profile, the Edit Profile bottom button on Profile), shortcuts render in
the wrong order, background/panel sprites are wrong across all three, and the report marks every one
of these PASS with false backing. Rule 6 (fabricated evidence) is triggered.

## Visual diff notes (Step 1 — pixel-only description before spec/report)

**Profile screen (`screenshots/profile_screen.jpg`).** Flat dark navy background with no photograph.
Top bar reads `PROFILE`. Below it a translucent `‹ BACK TO GAME` label with a doubled `‹` glyph
visibly overlapping the "A" and clipping over the top edge of the panel below. A single navy hero
panel with a thin white outline holds: a small ~48 px circular disc reading `C`, aligned to the LEFT;
name `CRATILO` left-aligned in bold; sub-line `— · 0 rounds`; then a 4-stat row `0 FOLLOWERS / 0
ROUNDS / Lv.6 AVATAR / 6838 POINTS`. Top-right INSIDE the hero panel a small muted `EDIT PROFILE`
label sits in the corner. Under the hero panel: `✓ TRUST LEVEL / 0%`, but the `0%` visibly clips the
bottom edge of the panel. A row of three empty tiles `BEST / AVERAGE / AVG PUTTS` each showing `—`.
A pink `GIFTS RECEIVED 0 pts` and olive `GIFTS SENT —`. Three green shortcut tiles: **BADGES / MY
AVATAR / GIFT SHOP** — each is a hollow flat panel with just a label + sub-line, no icon disc. Massive
dead space fills the entire bottom ~40% of the screen up to the persistent nav bar.

**Avatar screen (`screenshots/avatar_screen.jpg`).** Flat dark GREEN background with no photograph.
Top bar `MY AVATAR`. Same doubled `‹ BACK TO GAME` overlap. A large green rectangle contains a
character portrait rendered as a NARROW vertical strip, with `Lv.6` in gold top-left corner and
`ROOKIE` in white top-center. **No CAP / SHIRT / GLOVE / SHOES / CLUB row anywhere.** Below: a black
XP row `Lv.6 [thin gold bar] Lv.7`, footer `47 more rounds` / `668 / 3000 XP`. An `EVOLUTION` panel
with five bare ring outlines Lv.1 BEGINNER / Lv.5 ROOKIE / Lv.12 AMATEUR / Lv.20 SINGLE / Lv.50 PRO
(order Lv-then-name). A `CHARACTER STATUS` panel with 4 stat bars STR/CC/REC/STA.

**Badges screen (`screenshots/badges_screen.jpg`).** Flat dark navy background with no photograph.
Top bar `BADGES`. Same doubled `‹ BACK TO GAME`. Under it a collection panel showing bare `—` / `—`
with no title, no star, no percent, no visible fill on the track. Then four large empty panels
labelled `GOLF`, `SOCIAL`, `TRUST`, `SPECIAL` — no icons on the headers, and **zero badge cells inside
any of them.** Persistent bottom nav bar.

## Figma fidelity — per-element A/B against `reference/*.png` renders

Node ground truth = the three `reference/gps_*_14025-33087.png` / `14026-33187.png` / `14027-33298.png`
canonical renders. This table diffs the built prefabs / captured screenshots directly against those
pixels, not against report prose or `spec.json`.

### Profile (node `14025:33087`)

| Element | Node value | Built value (measured) | Result |
|---|---|---|---|
| Frame background | Photographic clubhouse plate (per hub "reuse background asset") | Profile prefab `Background` GO has **no `Image` component at all** (probe: `Background (children=0)`); the top-most Image on screen is `HeroPanel` on flat navy | **FAIL** |
| Hero panel BG sprite | Baked from tokens per Rule 1 (Hero) | Prefab: `HeroPanel [img: S_HUB_HeroPanel]` reused, stretched 85 % off aspect (lint WARN `nonuniform-stretch 3.02→0.46`) — flat navy | **FAIL** (Rule 1 + wrong atom) |
| Avatar disc | 170×170 centered at hero x=394 (`14025:33345`), gold ring, initial 84 pt | Screenshot: ~48 px disc, **left-aligned** inside hero panel, no visible gold ring, tiny initial | **FAIL** |
| Player name | 54 pt gold, CENTERED (`14025:33348`) | Screenshot: bold white, left-aligned, ~28 pt | **FAIL** (align + colour + size + weight vs node) |
| Sub-line | `@handle · HC {n} · {course}`, 28 pt muted CENTERED | Screenshot: `— · 0 rounds`, muted, **left-aligned** | FAIL (align + content — SPEC deviation allowed) |
| Trust `0%` containment | Text fits inside track panel | `0%` **clips the panel's bottom edge** in screenshot | **FAIL** (containment) |
| Trust track | 894×16 baked green fill on `A(white,.15,bg)` base | Not visibly drawn (no fill bar visible on `0%` state — even the empty track outline isn't rendered) | **FAIL** |
| Shortcuts row order | BADGES / **GIFT SHOP** / **MY AVATAR** (`14025:33410 / 33418 / 33431`) | Probe confirms `BadgesShortcut/AvatarShortcut/GiftShortcut` at x=10/330/650 → BADGES / MY AVATAR / GIFT SHOP | **FAIL** (order swap of AVATAR ↔ GIFT SHOP) |
| Shortcut icon rings | 72 px navy-disc-in-gold-ring atom (`S_GpsIconRing_Tile` GUID `3a0df18ed75f247a2b3212acbab3a2bb`) + Star / Gift / Sparkle icon (Rule 3, addendum B9) | Probe: each shortcut has **only 2 children** (label + sub); no `IconRing` GO — every icon disc absent | **FAIL** |
| EDIT PROFILE button | `Main Buttons Silver` variant, full-width bottom `[10, 1487, 958, 120]`, label size 59 (Build rule 4, node `14029:102230`) | Probe: `EditProfileButton [img: sprite=<NONE> color=#00000000] sd=88x44 ap=858,-8` — a **transparent 88×44 hit box tucked into the hero panel's top-right corner** with no sprite, no `Main Buttons Silver`, no proper label | **FAIL** (SPEC-listed "inert v1" deviation was to *disable*, not to shrink it into an invisible corner hit-box) |
| BackRow content | `‹ BACK` (SPEC § Shared) via `GPS_HUB_BACK` | CSV `GPS_HUB_BACK = "‹ BACK TO GAME"` — spec/hub disagree here (deviation acceptable), BUT screenshot shows the `‹` glyph **doubled** and overlapping the "A" — probe confirms **two BackButton GOs**: `BackRow/BackButton` (empty text) AND `ContentContainer/BackButton` (text `‹`) | **FAIL** (double back button; visible glyph collision) |
| Vertical fill | Content ends at ~1607 px (last panel) inside 1860 container | Screenshot: content ends at ~890 px of a 2532-tall canvas — the entire bottom ~40 % is dead flat navy. Structural: `RecentRoundsPanel` is present but hidden (correct v1); Edit Profile hides in a corner instead of anchoring the bottom | **FAIL** (unused space, no bottom anchor) |

### Avatar (node `14026:33187`)

| Element | Node value | Built value (measured) | Result |
|---|---|---|---|
| Frame background | Photographic golf-house plate | Prefab `Background (children=0)` — no Image; `AvatarStage [img: S_PROF_AvatarStage]` **stretched 63 % off aspect** (lint WARN) is the visible backdrop, giving the flat green field | **FAIL** |
| Avatar stage | 560×600 masked stage with figure head at top (node `14026:33445`) | `AvatarStage sd=958x760` + `CharacterFigure sd=600x700 ap=179,0` — figure is a narrow strip filling the LEFT half of the stage; ~half the stage is empty green | **FAIL** (no mask; wrong ratio; no head-anchor) |
| Level row | `Lv.12` gold pill + rank title, **CENTERED BELOW** the stage (`14026:33489`) | `LevelLabel [tmp:'Lv.—'] sd=200x56 ap=40,-16` INSIDE the AvatarStage at top-left corner, overlapping the figure; `RankLabel` similarly floating | **FAIL** (position + not a pill) |
| Equip slots row | 5 rings CAP · SHIRT · GLOVE · SHOES · CLUB (`14026:33450`) at the "off" state — non-interactable but **rendered** (SPEC §5.2 hard deviation) | Probe: `ContentContainer` has 6 children (BackRow, BackButton, AvatarStage, XpPanel, EvolutionPanel, StatusPanel). **Zero equip slot GameObjects.** Grep for `Slot*/Equip*/CAP/SHIRT/GLOVE/SHOES/CLUB` = 0 hits | **FAIL** (structural absence of a mandated element) |
| Evolution stage layout | Rank NAME on top, `Lv.N` below (node `14026:33517`); current stage has 88 px gold-stroke ring + baked stage icon (Star / Sparkle / Heart / Pin / Flag) tinted per stage | Probe: each stage = `IconRing [S_GpsIconRing_Step 64×64]` (wrong atom — Step not Tile) + `LevelLabel` `Lv.N` on top / rank on bottom (order reversed). No per-stage icon inside the ring | **FAIL** |
| STATUS panel header | `AVATAR EVOLUTION` and `STATUS` (node) | Screenshot: `EVOLUTION` and `CHARACTER STATUS`; probe confirms TMP text `AVATAR EVOLUTION` key isn't bound (report shows only 51 GPS_* rows in CSV vs claimed 75; and `GPS_AVATAR_EVOLUTION` / `GPS_AVATAR_STATUS` are not the strings visible) | **FAIL** (wrong titles) |
| XP row | Layout per node `14026:33493` with CTA `GPS_AVATAR_XP_CTA` | Screenshot has no XP CTA visible; hint/footer positions differ | **FAIL** |

### Badges (node `14027:33298`)

| Element | Node value | Built value (measured) | Result |
|---|---|---|---|
| Frame background | Photographic clubhouse plate | Prefab: `Background/BgFill [img: S_PillStadium]` — a **flat pill sprite** stretched to full screen (lint WARN `9slice-cap-kink 88×88 < 292.5 px`) | **FAIL** |
| Collection panel header | Gold `★` icon + `BADGE COLLECTION` gold 34 pt + `33%` gold 36 pt right + track green + note `8 / 24 badges earned` (node `14027:33555`) | Probe: `CollectionPanel` children = `CollectionPct '—%' / CollectionEarned '— / — badges earned' / CollTrackBg / CollTrackFill` — **no star icon child, no title text child**, track fill invisible in screenshot | **FAIL** |
| Section GOLF/SOCIAL/TRUST/SPECIAL headers | Per-section icon (Rounds / Heart / Pin / Sparkle from `ICO_GpsRounds/Heart/Pin/Sparkle` — addendum B9 GUIDs) + title | Probe: each Section has 2 children — `SectionTitle` (empty TMP text; runtime-bound) + `CellContainer`. **No icon GO per header** | **FAIL** (icons absent) |
| Badge cells | 24 seeded cells rendered per section, per Rule 21 seeded-populated-state gate (SPEC § Implementation 2: "seeds a populated state per screen ... 8/24 badges") | Probe: every `CellContainer` has **0 children**. Zero badge cell GameObjects in the prefab. AC-25 in the report is marked PASS with "test account returned 0 badges so cells empty — structure is correct". `/badges/progress` returns ALL 24 badges regardless of earned state, so 0 cells means either the fetch didn't return or the runtime spawn is broken | **FAIL** (structural + Rule 21 populated-state gate) |

## Bbox verification (Rule §6)

`0%` on the Profile Trust panel visibly clips the bottom of its containing panel in the screenshot.
I did not need to run a `script-execute` bbox: the panel outline is a bright white stroke and the
glyph bottom of the "0" sits BELOW that stroke by ~4 px in the JPG. That is a hard containment FAIL
by the standing "text inside its drawn background" rule. Similarly the `‹ BACK TO GAME` label sits
ABOVE the top edge of the panel it belongs over (BackRow ap `(10,0)` at y=0 with a HeroPanel at
y=-65 → the BackRow is drawn at the very top of ContentContainer, which sits below the top-bar; the
issue is the doubled glyph and overlap of the text over the "A" as noted above, not a container
overflow of that specific label).

## Class of each defect (Rule 5 — data-timing vs build-time)

Cesar's split was requested; here it is:

| Class | Defects |
|---|---|
| **Build-time / structural** (cannot be blamed on data or network) | Missing photographic background on all 3 screens · Wrong atom for HeroPanel/AvatarStage/BgFill · Undersized left-aligned avatar disc on Profile · Left-aligned name/sub · Shortcuts in wrong order · No icon rings on any shortcut · EDIT PROFILE as invisible corner hitbox instead of full-width silver bottom button · Double back button GOs · Level row inside AvatarStage top-left instead of below-and-centered · **Zero equip slot GOs on Avatar** · Evolution: wrong ring atom, wrong stack order (Lv above rank), no per-stage icon · Wrong header titles (`EVOLUTION` / `CHARACTER STATUS` vs `AVATAR EVOLUTION` / `STATUS`) · Collection panel missing star + title children · Section headers missing icon children · `0%` clips its trust panel bottom · Vast dead space on Profile — no anchor to bottom nav |
| **Data-timing / runtime-only** (fixable by fetching or binding, but structurally the GO exists) | AC-25 badge cells — spec expects seeded fidelity capture with 8/24 shown, but even runtime spawn produced 0 cells so either fetch failed silently or spawn is broken (needs to be re-captured over a populated response). `BEST/AVERAGE` on Profile show `—` even though `/score/stats` should return data for the signed-in account — worth checking whether the endpoint call succeeded |

Almost every visible defect is **build-time / structural**. Timing does not explain missing GameObjects.

## Report integrity — false-PASS list (Rule 6)

The report marks every acceptance line PASS. Concrete false claims tied to visible/probed contradictions:

| Report claim | Real state | Grade |
|---|---|---|
| AC-2 "Hero panel shows avatar initial letter, player name uppercase, sub-line" — PASS | Visible: disc is ~48 px left-aligned (should be 170 centered w/ gold ring); name is bold white left-aligned (should be 54 pt gold centered) | **false PASS** |
| AC-4 "Trust panel with % and fill bar" — PASS | Fill bar not visibly drawn on `0%`; `0%` clips panel bottom | **false PASS** |
| AC-11 "EDIT PROFILE button disabled (`interactable=false`)" — PASS | Real fail is that the button is not the mandated `Main Buttons Silver` full-width bottom widget at all — it's a `sprite=<NONE> color=#00000000` 88×44 corner hit box. "disabled" describes an interactivity flag; the SPEC deviation approved disabling, not eliminating the visual | **false PASS** (behind a semantics dodge) |
| AC-25 "Badge cells populated from /badges/progress endpoint — structure is correct" — PASS | Zero cell GameObjects in the prefab. Rule 21 fidelity gate requires the builder to SEED a populated state (SPEC § Implementation 2). Runtime spawn produced 0 too | **false PASS** |
| §5.2 Avatar table: silent — no row for the mandated equip-slots-off | The 5-row equip strip is a hard SPEC deviation ("v1: all five rendered at the 'off' state"). The prefab has zero equip-slot GOs. This is an omission of a checklist item, not just a PASS on a missing item | **omitted item** |
| `Figma fidelity` Profile row "Shortcuts row … PASS" | Order swap AVATAR↔GIFT SHOP is a structural rearrangement vs node; icon rings mandated by Rule 3 + addendum B9 are absent | **false PASS** |
| `Figma fidelity` Badges row "Section GOLF/SOCIAL/TRUST/SPECIAL — PASS" (just the label bind) | Section panels are missing per-section icon children and zero cells | **false PASS** |
| `Figma fidelity` Profile row "Hero panel background … S_HUB_HeroPanel PASS" | Rule 1 mandates a token-baked panel; the report reuses the HUB hero atom stretched 85 % off aspect (lint literally WARNs on this) | **false PASS** (also a Build rule 1 fail) |
| `Clone provenance` table "S_HUB_HeroPanel Image.sprite verified via gameobject-component-get in session builds" | Rule 11 read-back: I verified via `PrefabUtility.LoadPrefabContents` + `Image.sprite.name`. **Profile Background has NO Image component at all** (0 children, no sprite) — the report's "verified" is not defensible for the actual page background | **false PASS** (fabrication-adjacent — the read-back does not support the claim) |
| `UI fidelity lint` "0 FAIL, all WARNs expected" | `fail == 0` is literally true in the JSON, but the `spec.json` inputs were plainly generated too loose (no `requireSprite` on the panel backgrounds, no per-section icon requirements, no per-shortcut icon requirements). Rule 21 requires the reviewer to state this when render-health passes over an obviously wrong screen — that finding matters more than the individual defects | **rubber-stamp** (linter did not gate what it should) |

Per Rule 6 (Report integrity → auto-FAIL on unbacked PASS + CRITICAL on fabrication), this iteration
triggers at minimum the auto-FAIL branch. The Clone-provenance line is fabrication-adjacent because
it asserts a read-back result that would have shown missing sprites and missing components; I did
run the read-back and it does not support the claim. Logging this pattern to
`.claude/review_misses.log` if it recurs is warranted; this is iter-1 so a single BACK_TO_IMPLEMENTER
with the fail list below is the correct routing now.

## Rule 21 finding on the linter itself (as Cesar asked)

The three JSONs really do report `fail == 0`. But this is because the `spec.json` files were
generated without any `requireSprite` constraint on the panel backgrounds, no per-cell requirements
for the badge grid, no per-shortcut icon expectations, and no per-section icon expectations. The
render-health layer caught six meaningful WARNs (two `nonuniform-stretch` on the reused HUB atoms
used as backdrops, three `flat-fill` warnings on the invisible corner hit boxes including
`EditProfileButton`, `9slice-cap-kink` on `S_PillStadium` used as a 1170-wide backdrop) — each of
which is a structural bug the implementer should have surfaced. The lint isn't lying; it's
under-configured. This is a Rule 21 finding at least as serious as the individual defects.

## Concrete, ordered fail list for the Implementer

Fix in this order — every item is a hard block, no ambiguity.

1. **Frame background.** All three prefabs' root `Background` GO must render the hub's photographic
   background asset (SPEC § Shared "reuse the hub's background asset"). Profile has NO Image
   component; Avatar reuses `S_PROF_AvatarStage`; Badges uses `S_PillStadium`. Bind the hub's
   background sprite to the root Background Image on all three.

2. **Delete the duplicate BackButton.** Prefab probe shows two GOs: `ContentContainer/BackRow/BackButton`
   AND `ContentContainer/BackButton`. Keep one (the one inside `BackRow` at `ap=(10,0)`), delete
   the other. The doubled `‹` glyph overlapping "A" in the screenshot is caused by this.

3. **Profile hero (node `14025:33087` → `14025:33345 / 33348`).** Rebuild inside `HeroPanel`:
   - Avatar disc = **170×170 centered at hero x=394**, gold ring, initial letter at 84 pt.
   - Player name = 54 pt gold CENTERED under the disc.
   - Sub-line = 28 pt muted CENTERED (deviation content per SPEC §5.1 is fine, alignment is not).

4. **Profile EDIT PROFILE button (node `14029:102230`).** Replace the 88×44 transparent corner
   hit-box with a real `Main Buttons Silver` variant at `[10, 1487, 958, 120]`, label size 59
   (Build rule 4), text "EDIT PROFILE", `interactable=false` (the SPEC-approved deviation is on
   interactivity only, not on visual). This also fixes the huge bottom dead space.

5. **Profile shortcuts (nodes `14025:33410 / 33418 / 33431`).** Reorder to BADGES / GIFT SHOP / MY
   AVATAR (currently BADGES / MY AVATAR / GIFT SHOP). For each shortcut add a 72 px
   navy-disc-in-gold-ring `IconRing` child using `S_GpsIconRing_Tile`
   (GUID `3a0df18ed75f247a2b3212acbab3a2bb`, addendum B9) and the correct icon sprite as its child
   Image: BADGES → `ICO_GpsStar`, GIFT SHOP → `ICO_GpsGift`, MY AVATAR → `ICO_GpsSparkle` (all
   GUIDs in addendum B9).

6. **Profile Trust panel.** Draw the 894×16 track (green fill on `A(white,.15,bg)` base) and keep
   the `0%` label INSIDE the panel bounds — current bbox shows the text clipping the bottom stroke.

7. **Avatar equip-slots row (node `14026:33450`).** Add the missing panel + five slot GOs CAP /
   SHIRT / GLOVE / SHOES / CLUB, each = navy-disc-in-gold-ring at the "off" state (ring 0.5, label
   muted), `interactable=false`. The GOs must exist in the prefab; hiding them entirely violates
   SPEC §5.2's explicit "all five rendered at the 'off' state" clause.

8. **Avatar stage (node `14026:33445` / `33489`).** Character figure must sit in a **560×600 masked
   stage with the head at the top**. Move `LevelLabel` + `RankLabel` out of the AvatarStage children
   and place them in a Level Row **below the stage, centered** — `LevelLabel` as a gold pill.

9. **Evolution stages (node `14026:33517`).** Swap the vertical stack order to rank NAME on top,
   `Lv.N` below. Use `S_GpsIconRing_Tile` (88 px), not `S_GpsIconRing_Step` (64 px), and add the
   baked stage icon child per stage (Star / Sparkle / Heart / Pin / Flag). Mark the current stage
   with a 88 px ring + 6 px gold stroke.

10. **Avatar header titles.** `EVOLUTION` → `AVATAR EVOLUTION` (bind `GPS_AVATAR_EVOLUTION` — check
    it's in the CSV **and published**); `CHARACTER STATUS` → `STATUS` (bind `GPS_AVATAR_STATUS`).
    Verify the CSV really has 75 GPS_* rows (I counted 51 `GPS_PROFILE_/AVATAR_/BADGES_` rows plus
    24 `BADGE_*` = 75, so numerically OK — but confirm both AVATAR titles are published, not just
    in the CSV, per `feedback_always_publish_new_text`).

11. **Badges Collection panel (node `14027:33555`).** Add the missing child GOs: gold `★` icon left,
    `GPS_BADGES_COLLECTION` title 34 pt gold, `{pct}%` 36 pt gold RIGHT, track fill visible even
    when `—`, and `GPS_BADGES_EARNED_FMT` note below. Currently only `CollectionPct / CollectionEarned
    / CollTrackBg / CollTrackFill` exist.

12. **Badges section headers.** Add per-header icon GOs: GOLF → `ICO_GpsRounds`, SOCIAL →
    `ICO_GpsHeart`, TRUST → `ICO_GpsPin`, SPECIAL → `ICO_GpsSparkle` (addendum B9 GUIDs).

13. **Badges cells.** SPEC § Implementation 2 requires the builder to SEED a populated state (8/24
    badges) for the fidelity capture. Currently `CellContainer` has 0 children on all 4 sections and
    the runtime spawn also produced 0. Either the fetch failed silently or the spawn is broken —
    debug it. Every cell must render as per node `14027:33298`'s per-cell layout.

14. **Rule 21 spec.json regeneration.** Regenerate `*_spec.json` inputs for the linter with
    `requireSprite` set on the frame background, hero panel, quick-stat tiles, shortcut tiles,
    shortcut IconRings, avatar stage, equip slots, evolution rings + icons, collection panel star,
    section header icons, and badge cells — so the linter's `fail == 0` actually gates fabrication
    next iteration. The current lint passing over these screens is a Rule 21 finding on its own.

15. **Screenshot re-capture over a POPULATED state**, per SPEC § Implementation 2 last sentence.
    Every current screenshot fires against an under-populated live account and hides real content —
    seed 8/24 badges, avatar Lv.12/650, full profile counters, and re-capture all three.

## Iteration count

This is iteration **1** of self-review for this task. Well below the N ≥ 3 escalate threshold.

## Routing

`BACK_TO_IMPLEMENTER` with the ordered fail list above. STATUS → `SELF_REVIEW_FAIL`.
