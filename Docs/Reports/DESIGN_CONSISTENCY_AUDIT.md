# DESIGN_CONSISTENCY_AUDIT — findings and fix list

**Task:** `design_consistency_audit` (Notion 2112). **Audit only — this task changed no production
code, prefab, scene or CSV.** Every fix below becomes its own Quick spec that Cesar approves
individually; the Architect writes those from § 5.

**Coverage: 17 surfaces dumped in EN and JA** via real navigation where a player path exists, `ShowScreen` re-seat where none does (recorded per dump as `reachedVia`).

**Evidence under every row:** `Docs/Reports/DesignAudit/*.json` — 61 live dumps (17 screens × EN+JA,
4 Inventory tab states × EN+JA, 13 modals EN, 6 Tier-2 auth screens EN), all via real navigation.
**These are COMMITTED.** They were originally written only to `Docs/Diagnostics/_capture/`, which is
gitignored — so every citation in earlier revisions of this report pointed at a file no other machine
could open, and the audit's evidence base did not ship with the audit. `Docs/Diagnostics/_capture/`
remains the tool's working output; `Docs/Reports/DesignAudit/` is the tracked copy the numbers are
computed from. Also `Docs/Diagnostics/_capture/*_lint.json` (74 prefabs + 5 live roots),
`Docs/Design/DESIGN_TOKENS.md` (the "expected" column), and
`Docs/Specs/Active/design_consistency_audit/reference/` (29 node renders).

---

## 0 · Before the findings: the SPEC's own node table is wrong in 7 places

Rule 9 re-pull of every row (`reference/NODE_RESOLUTION.md`). **Checkable question: does the id in
the screen column resolve to a 1170×2532 frame?**

| Row | SPEC id | What it actually is | Correct id |
|---|---|---|---|
| ModeSelectionScreen | `13027:10222` | carousel side-arrow **asset**, 30×60 | `13026:1924` |
| HoleSelectionScreen | `12885:87551` | **canvas** 3846×8343 (2 variants + loose art) | `12961:1694` |
| HoleSelection/HoleCard | `12961:1694` | the **screen frame** | `12961:1728…1733` |
| MissionSelectionScreen | `4002:6036` | not the screen state | `4065:7960` / `4065:7961` |
| TournamentLeaderboardScreen | `13414:4041` | **LOCKED card**, 978×164 | `13414:5598` |
| GachaHistoryScreen | `13622:21105` | a **`Rankings Card`** component | `4079:18306` |
| Inventory · Bags / Items | `2563:18880` / `4063:393` | **page ids** | `12754:40669` / `4065:13487` |

Left unresolved, A5 would have generated node specs for card subtrees and A6 would have diffed live
screens against a locked card. **This is a finding about the audit's inputs, not a defect in the
game** — but it is the reason every id below was confirmed from the render call's own
`original_width/height` before use.

---

## 1 · Summary

**All counts below come from ONE corpus: the 17 distinct screens dumped in EN, Inventory's
four tab states collapsed to one screen.** Prefab-only dumps and modals are counted separately where
they appear. An earlier revision mixed EN and JA dumps and produced two different numbers for the
same shape in §1 and §3 — see § 7 deviation 4.

| Dimension | Sites | Severity | Shape |
|---|---|---|---|
| **F** — JA text bound to a Latin font asset | **660 CJK labels** (7 on NotoSansJP) | **S3** — see § 3.1, the render is CORRECT | new |
| **F** — Unity's default font never replaced | **36 in-screen** + 5 in two prefabs = **41** | S1 | (i) |
| **S** — off-scale rendered sizes | **209 ÷1.4 · 139 ÷1.2 · 46 59/66 · 274 unexplained** | S1/S2 | (ii)+(iii) |
| **O** — `Outline` component used as a border | **20** (one card family) | S2 | (iv) |
| **R** — 9-slice collapse (oval pills / rims) | **12 prefab FAIL + 30 live FAIL** | S1 | (vi-adjacent) |
| **S** — `Image.Type.Filled` on bar sprites | **225** | S2 | (vii) |
| **C** — visible flat fill where art is expected | **291 panel-sized** of 701 visible | S2/S3 | (viii) |
| **D** — `Shadow` component faking a drop shadow | **0** | — | (v) **REFUTED** |
| **R** — `unlocalized-text` | 412 WARN (prefab lint) | S3 | — |
| **C** — MISSIONS card shows tournament copy | 1 string | S1 | § 3.10 |

## 2 · Token sheet deltas

`Docs/Design/DESIGN_TOKENS.md` is complete for what the file actually uses. Two gaps matter:

- **The EN scale in use is nine steps** — 20 · 30 · 33 · 33 · 39 · 45 · 48 · 51 · 66. `Title_1`,
  `Large Title`, `Body`, `Caption_1` are referenced by **no in-scope frame**. A rendered size
  matching none of the nine cannot be excused as "another step".
- **Only ONE `JP/*` variable exists in any in-scope frame** (`JP/Footnote (jp)`, 39/54/−0.24).
  Every other JA size therefore has **no node-side token to be measured against**, which is why
  § 3.1 reports the JA *font binding* rather than JA size defects.
- `Gold` and `Silver` were EMPTY from `get_variable_defs` and are now resolved from SVG stops:
  Gold `#FCF195 → #D6AB42 @0.6 → #BB7F1D`, Silver `white → #D1D5DB @0.4 → #818EA1`. This
  **confirms** `UI_ELEMENT_PALETTE.md`'s gold claim against the file rather than inheriting it.

---

## 3 · Shape tables (§22 — the site enumeration, including the sites that are FINE)

### 3.1 · Japanese text is bound to a Latin font asset — **S3, and the render is CORRECT**

**This finding was downgraded from S1 by the red-team gate, and the downgrade is the important
part.** It was originally derived entirely from `font.name` in the dumps. Nobody had looked at a
Japanese screen. The red-team switched the build to Japanese, navigated to MissionSelection and
captured `screenshots/REDTEAM_ja_MissionSelection.png`: **every CJK label renders correctly through
TMP's fallback chain** — ミッション, ロモンドカントリークラブ, デイリーミッション, the rule list,
プレイ on the button — no tofu, no boxes, no clipping, correct weight and size.

So **no player sees a defect today**. The finding is that the *binding* is inconsistent and
fragile, not that the screen is broken.

| | count |
|---|---|
| Labels containing CJK, bound to a **Latin** font asset | **660** |
| Labels containing CJK, bound to **NotoSansJP** | **7** |
| NotoSansJP bindings that hold no CJK at all | 6 |

*(This number has been wrong twice. The first revision said "860 of 873", which subtracted
NotoSansJP **bindings** from **CJK labels** — two different populations. The second said "866 / 7",
which counted across ALL 21 JA dumps and so multiplied every Inventory site by four, violating this
report's own corpus rule. Under the stated rule — 17 distinct screens, Inventory tabs collapsed —
it is **660 / 7**. Both errors were caught by the red-team, not by me; the numbers are now
generated by `Docs/Scripts/audit_numbers.py` rather than typed.)*

**Why it still matters, at S3.** The correct pattern exists — the Stamina screens bind NotoSansJP
deliberately — so the game is inconsistent with itself. Fallback works until it doesn't: a glyph
outside the fallback face, a font-asset swap, or a platform with different fallback resolution turns
a working screen into tofu with no code change. But it is **not** a player-visible defect today, and
**Q1 must not be scheduled as one.**

### 3.2 · (i) The default font was never replaced  **S1 — CONFIRMED, 41 sites**

**Question:** *is this TMP's font asset `LiberationSans SDF`?*

| Source | live labels | Architect's count | verdict |
|---|---|---|---|
| InventoryScreen | 27 | 27 | ✅ exact |
| RosterScreen | 8 | 8 | ✅ exact |
| SettingsOverlay | 1 | 1 | ✅ exact |
| CharacterThumbnailCard.prefab | 3 | 3 | ✅ exact |
| StatBar.prefab | **2** | 4 | ❌ **the baseline double-counts** |
| HomeScreen / GeneralShop / ModeSelection / PersistentUI | **0** | — | ✅ clean |
| **TOTAL** | **41** | 46 stated / 43 summed | — |

**Why the baseline said 46.** A YAML GUID grep counts every TMP **twice** — each label writes the
font GUID to both `m_fontAsset` and `m_sharedMaterial`:

```
70:  m_fontAsset:      guid: 8f586378…      ← StatBar label 1
71:  m_sharedMaterial: guid: 8f586378…      ← the SAME label
341: m_fontAsset:      guid: 8f586378…      ← StatBar label 2
342: m_sharedMaterial: guid: 8f586378…      ← the SAME label
```

StatBar: 2 labels / 4 hits. CharacterThumbnailCard: 3 labels / 6 hits. The same doubling means the
headline **"LiberationSans ×77 across scenes+prefabs" is ~38 labels, not 77.** The stated 46 never
matched its own breakdown (27+8+1+3+4 = 43) either.

**All 41 are stat readouts** — "50/100", "9/25", "228 yd", "STRENGTH" — one coherent population at
33 px rendered (16–18 px inside the card prefabs). Several sit on **inactive** objects (Roster's
Compare panel, Settings' UserProfile submenu) and one is an **empty** label that is invisible today
and will render in Liberation the moment it gets text.

### 3.3 · (iv) `UnityEngine.UI.Outline` used as a border  **S2 — CONFIRMED, 20 instances / 1 family**

| Screen | instances | object |
|---|---|---|
| HomeScreen | 15 | `ModeCarouselSection/…/ModeHomeCard(Clone)` |
| ModeSelectionScreen | 5 | `CardsContainer/…/ModeCard` |
| every other screen | **0** | clean |

Twenty instances, **one prefab family** (ModeCard / ModeHomeCard). Trap C5: `Outline` blurs, it is
not a crisp Npx border. Plus 3 `outline-border` WARNs at prefab level.

### 3.4 · (v) `UnityEngine.UI.Shadow` faking a drop shadow  **REFUTED — 0 sites**

Zero `Shadow` components on any in-scope screen or prefab. The expected shape does not exist; the
shadows in this UI are baked into sprite margins as the palette prescribes. Published as a negative
so the next audit does not re-derive it.

### 3.5 · (vii) `Image.Type.Filled` on bar sprites  **S2 — CONFIRMED, 225 images**

| Object | count |
|---|---|
| `Bar` | 182 |
| `BarContainer` | 33 |
| `BarPending` | 8 |
| `GhostBar` | 2 |

`Image.Type.Filled` discards 9-slicing, so a rounded bar renders with wedge caps
(`reference_ui_bar_fill_width_not_fillamount`: drive **width**, not `fillAmount`). Concentrated in Inventory (84) and Roster (26) — the StatBar / durability-bar family.

### 3.6 · (viii) Visible flat fill where art is expected  **S2/S3 — 701 visible, 291 panel-sized**

The raw null-sprite count is misleading: most are near-transparent raycast/layout helpers, *not*
defects. Two thresholds separate the population, and **both are stated here so the numbers
reproduce from the dumps without reading the script** (red-team item 4):

| Term | Exact rule, applied to every `images[]` entry in the 17 EN dumps | Count |
|---|---|---|
| **visible flat fill** | `sprite == "<NONE>"` **AND** `alpha(color) > 0.2` — alpha is byte 7–8 of the 8-digit hex ÷ 255 | **701** |
| **panel-sized** | the above **AND** `width >= 200` **AND** `height >= 60` | **291** |

Reproduce with `python3 Docs/Scripts/audit_numbers.py` (the `visfill` / `panel` fields); the rule is
the `alpha(i['color']) > 0.2` and `i['width'] >= 200 and i['height'] >= 60` lines of `compute()`.
**Sensitivity of the 0.2 floor, measured rather than asserted.** Eight fills do sit between α 0.02
and α 0.2, so the cut is not free and the honest range is worth stating:

| floor | visible | panel-sized |
|---|---|---|
| α > 0.02 | 709 | 295 |
| α > 0.10 | 708 | 295 |
| α > 0.15 | 704 | 291 |
| **α > 0.20 (used)** | **701** | **291** |
| α > 0.30 | 690 | 291 |

So the **panel-sized** figure — the one the fix list acts on — is stable at **291** anywhere from
α 0.15 to α 0.30, and moves by 4 only if the floor drops to 0.10. The **visible** figure is the
softer of the two (690–709 across that range) and should be read as "about 700", not as an exact
population. Three `InventoryScreen/VerticalDivider` fills sit exactly at α 0.20 and are therefore
EXCLUDED by the strict `>`; they are dividers, not missing art, so excluding them is correct — but
it is a judgement, and a node check on them is listed in the fix rows rather than assumed.

The reviewable population is therefore the **291** panel-sized fills — and some of those are
legitimate modal scrims (`#000000D9` on `BagsClubModal/Background`). Each of the **291** needs a
node check before it becomes a fix row.

### 3.7 · 9-slice collapse (oval pills and rims)  **S1 — 12 prefab FAILs, 30 live FAILs**

Every FAIL in the entire 74-prefab sweep is this one rule. Two sub-shapes:

| Sub-shape | sites | detail |
|---|---|---|
| Entry-fee badge pills | 8 | 44 px borders on 34–38 px height — `TournamentSelectionCard`, `GeneralShopCard` |
| Main-button rims | 4 | 122–130 px borders on 120 px height — `HoleCompleteModal`, `HoleCompleteWidget`, `TournamentCloseButton` |

Live, `GeneralShopScreen` reports **30 FAILs** (15 collapse-x + 15 collapse-y) because the cards
spawn at runtime. This is the same oval-pill defect Cesar caught by eye on `stamina_boost_shop`,
now recurring in five more prefabs.

### 3.8 · (ii)+(iii) Off-scale sizes — **THREE competing divisors, not one**  **S1**

**Question:** *for a non-autosized label whose rendered px is off-scale, which conversion explains
it?* Tolerance ±0.4 px. Auto-sized labels excluded — their `fontSize` is a result, not an authored
value.

| Convention | labels | share | the values it explains |
|---|---|---|---|
| On one of the nine scale steps | **1389** | 67.5 % | — |
| **÷1.4** | **209** | 10.2 % | 21.6 · 27.86 · 28 · 28.05 · 32 · 32.14 · 47.1 |
| **÷1.2** | **139** | 6.8 % | 16.7 · 17 · 25 · 27.5 · 37.4 · 40 · 42.5 · 55 |
| **59/66** SemiBold | **46** | 2.2 % | 18 · 26.7 · 26.99 · 34.6 · 35 |
| **Unexplained by any** | **274** | 13.3 % | — |
| total non-autosized | 2057 | | |

**The ÷1.4 population was found by node comparison, not by pattern-matching, and it corrected this
audit's own first answer.** An earlier pass tested only ÷1.2 and mapped 27.86 → 33 (27.86 × 1.2 =
33.4, "close enough"). Pulling `get_design_context` on the ModeCard node proved that wrong:

| ModeCard element | node `13026:2366` | live rendered | Δ |
|---|---|---|---|
| `MULTIPLAYER` title | **45** | 32.14 | −28.6 % |
| `ENTRY FEE` | **39** | 27.86 | −28.6 % |
| `REWARDS` | **39** | 27.86 | −28.6 % |
| `PLAY` | **66** | 66 | **0 %** ✅ |

45 ÷ 1.4 = 32.14 and 39 ÷ 1.4 = 27.857 — **exact**. These are not ÷1.2 leftovers; they are a
different divisor, and the card mixes them with a correct 66 on the very same button. Had the
÷1.2 reading stood, Q7 would have "fixed" 27.86 → 33 when the node says **39**.

**Cesar's shape-(iii) decision, reframed.** The question was "which convention does the game keep?"
The answer is that **no single convention is in force**: 67 % of labels are already on-scale and the
remaining third is split across three mutually incompatible conversions plus 274 labels no
conversion explains. Recommendation: the **nine scale steps are the ruler**, every off-scale value
is a defect regardless of which divisor produced it, and each population is fixed against **the
node**, never against another divisor.

**A colour difference that is NOT a defect.** The node's `MULTIPLAYER` title is `#EEDC9A`
(Mission Font gold); the live one sampled `#D1D5DB`. That is the *collapsed* card against the
node's *expanded* one — in the live build the expanded card's title IS gold. State, not fidelity;
recorded here so a later reader does not re-raise it.

### 3.10 · A crop-sheet find the dumps alone would have missed  **S1, XS to fix**

The MISSIONS card's reward line reads **"Varies by tournament"** — tournament copy on a missions
card. Confirmed in the dump on `…/RewardsRow/RewardSlot2/CoinValueGroup/Reward2Amount` and its
`…Exp` twin (collapsed AND expanded), both with **`locKey` empty**, so it is also unlocalized. The
node says `x200 (average)`. Found by putting the live capture beside the node render — no font,
size or colour check would have surfaced it.

### 3.9 · (ix) Card radius r50 vs r32  **PARTIAL — node side captured for one card family**

`get_design_context` on `13026:2366` gives the ModeCard's node geometry directly: **radius 50 px,
border 3 px WHITE, fill `#133453 → #091B33`, drop-shadow `0 10 10 rgba(0,0,0,0.4)`**, inner Pop-Up
border `#0A1D35`, PLAY button radius 20 px with a 2 px `#FFE48B` rim. That matches the palette's
"r50 big cards" rule.

What is NOT done: the live radius is carried by the card's SPRITE, and the dumper records sprite
name/GUID/border but not a measured corner radius. Confirming r50-vs-r32 per card class needs
either a sprite-border read or a corner crop per card family — see § 6.

---

## 4 · Lint results

| Surface | prefabs/roots | FAIL | WARN |
|---|---|---|---|
| Prefabs (`Assets/Prefabs/UI/**` minus `Gps/`) | 74 | 12 (5 prefabs) | 919 |
| Live ShellScene roots | 5 | 30 (GeneralShopScreen only) | 982 |

WARN distribution (prefabs): `unlocalized-text` 412, `flat-fill` 270, `nonuniform-stretch` 153,
`tmp-default-sizedelta` 41, `9slice-cap-kink` 40, `outline-border` 3.

---

## 5 · Fix list — candidate Quick specs

| # | Group | Closes | Files | Op | Est | Blast radius |
|---|---|---|---|---|---|---|
| **Q1** | Bind a JA font asset *(DEFERRED — see § 3.1)* | § 3.1 (660) | `LocalizedText.cs` + the font asset | Swap `font` to `NotoSansJP-VariableFont_wght SDF` on language change. **NOT urgent — JA renders correctly through fallback today.** Consistency/robustness, not a visible defect | **M** | every screen, both locales; a JA visual pass mandatory before merge |
| **Q2** | Replace the default font on stat readouts | § 3.2 (41) | ShellScene `InventoryScreen` (27), `RosterScreen` (8), `SettingsScreen` (1); `CharacterThumbnailCard` (3), `StatBar` (2) | Set font asset to `Rubik-VariableFont_wght SDF` via `SerializedObject` | S | StatBar + card prefabs are instanced across Roster/Inventory |
| **Q3** | ModeCard border: `Outline` → real rim | § 3.3 (20) | `ModeCard.prefab`, `ModeHomeCard.prefab` | Replace `Outline` with a two-layer rim or 9-sliced rim sprite | S | Home carousel + ModeSelection |
| **Q4** | Fix the oval pills and rims | § 3.7 (12+30) | `TournamentSelectionCard`, `GeneralShopCard`, `HoleCompleteModal`, `HoleCompleteWidget`, `TournamentCloseButton` | Raise `pixelsPerUnitMultiplier` or re-bake the sprite at the used height | S | Shop + tournament cards share the badge atom |
| **Q5** | Bars: width, not `fillAmount` | § 3.5 (225) | `StatBar.prefab` + the durability/progress bar family | `Image.Type.Filled` → drive `sizeDelta.x` | **M** | Roster, Inventory, tournament progress |
| **Q6** | Triage the panel-sized flat fills | § 3.6 (291) | per site | Node check each; scrims are correct, missing art is not | M | mixed |
| **Q9** | MISSIONS card shows tournament copy | § 3.10 | `ModeCard` missions binding | `Reward2Amount` reads "Varies by tournament" on the MISSIONS card, in both collapsed and expanded containers, and has **no locKey** | **XS** | ModeSelection + Home carousel |
| **Q7** | Retire the ÷1.4 sizes | § 3.8 (209) | ModeCard / ModeHomeCard family first (27.86 / 32.14) | Re-set to the NODE value (39 / 45) — **not** to the ÷1.2 target | S | Home carousel + ModeSelection |
| **Q7b** | Retire the ÷1.2 sizes | § 3.8 (139) | screens carrying 25 / 40 / 42.5 / 55 | Re-set to the node value per site | **M** | wide |
| **Q8** | Triage the unexplained sizes | § 3.8 (274) | per site | Compare rendered px to the node; neither convention explains these | **M** | wide |

---

## 6 · What this audit could NOT measure, and why

1. **Shapes (ii), (iii), (ix)** — need the node-spec layer (A5) and crop sheets (A6). The ÷1.2 and
   59/66 questions specifically need rendered cap-height crops of both SemiBold populations
   side by side; the dumps carry the rendered px but the visual A/B is not built.
2. **JA size conformance** — impossible as specified: only one `JP/*` token exists in any in-scope
   frame, so there is no node-side ruler for JA sizes. § 3.1 reports the font binding instead.
3. **Auto-sized labels** — 35 of HomeScreen's 131 labels are auto-sized, so their `fontSize` is a
   *result*, not an authored value. It moves when anything nearby changes: the tripwire showed
   49.05 → 51 from a font swap alone. Those sites are judged on `autoSizeMin/Max`, never on the
   momentary value, and no "wrong size" row is raised from one.
4. **Modals and Tier-2 are dumped in EN ONLY, with no crop sheets.** 13 in-scope modal
   controllers were opened through their own `Show()` and dumped (`MODAL_*.json`, all
   `locale:"en"`); 6 of the 7 Tier-2 screens were re-seated and dumped in EN. **No JA modal pass
   was run**, so the § 3.1 JA font finding is not confirmed on modal surfaces — it is asserted only
   for the 17 screens. **No modal crop sheet exists**, so no modal has been diffed against its node
   render. Five GPS modals were swept in by the first modal pass because they share the
   `ModalController` base class, and were removed (A13); the pass now filters on namespace.

5. **Nine of the 17 surfaces were reached by `ShowScreen`, not a tap**, because no player path to
   them exists from a fresh session (a tournament needs an entered tournament; GachaPrizes needs a
   completed pull). Each such dump records `reachedVia:"harness ShowScreen (no player path)"`. Their
   findings are real, but their *layout* is a re-seated state, not one a player produced.
6. **`TournamentSignupModal`'s reference render is 1020 px**, 4 px under A0's floor. Figma will not
   render above 1:1 (verified: requesting 2040 returns 1020), so the floor is unreachable for that
   node without fabricating resolution.

---

## 7 · Deviations (A14)

1. **A10 measured by diff, not by `git status`** (Cesar's call, "Option 1"). The tree carried
   **150 dirty paths at kickoff, 121 outside this task's permitted surface** — `bot_scheme_parity`,
   ~60 character-art test imports, `CLAUDE.md`, `tasks/lessons.md` — none of them this task's. The
   full list is in `HEARTBEAT.log`'s iter-1 baseline; A10 is therefore "no production path appears
   in THIS task's diff".
2. **EditMode tests live in a new asmdef inside `Assets/Editor/UIFidelity/Tests/`** rather than
   `GolfinRedux.Tests.EditMode`, which lacks a `Unity.TextMeshPro` reference. Adding one would have
   touched a shared file outside A10's permitted paths; a local asmdef keeps the whole change
   inside the allowed surface.
3. **The node table was corrected, not followed** — § 0.
4. **The corpus was rebuilt after the red-team found contradictory counts.** The EN and JA passes
   originally wrote to the SAME filename (`<Screen>.json`), so each overwrote the other and the
   corpus held a mix of locales. Locale-INVARIANT properties then differed by locale — 
   `Image.Type.Filled` read 134 in one folder and 1007 in another — which is impossible and is how
   the contradiction surfaced. Fixed structurally: `DesignAuditDumper` now writes
   `<Screen>__<locale>.json`, so the two corpora cannot clobber each other. All four passes were
   re-run from scratch. EN and JA now agree exactly on `Image.Type.Filled` across all 21 dump files (561 = 561).
   That parity check deliberately spans every dump — including the Inventory tab states, modals and
   prefabs that the § 1 corpus rule excludes — because its job is to prove the two locales no longer
   clobber each other, not to count a shape. The shape count itself, on the 17-screen corpus, is
   **225** (§ 3.5). There is a single legitimate difference (RosterScreen 104 vs 103 non-autosized labels — one label flips to
   auto-sizing under longer Japanese text). **Every count in this report now comes from one
   corpus**, stated at the top of § 1.
5. **Re-running the modal pass silently moved the size buckets — and that is now guarded.** The
   modal pass dumps modals AND the six Tier-2 auth screens. Modals carry a `MODAL_` prefix and were
   excluded by name; the Tier-2 screens carry no prefix, so when the pass was re-run to restore the
   missing modal evidence they walked straight into the corpus, taking it from 17 screens to 23 and
   moving **÷1.2 from 139 to 194 and unexplained from 274 to 279** — while every other number
   (`Filled` 225, flat fills 701/291, LiberationSans 36) stayed put, which is exactly what makes this
   kind of drift hard to see. **Regenerating evidence must never be able to change a finding.**
   `audit_numbers.py` now excludes the six Tier-2 screens by name, with the reason written at the
   exclusion. The buckets above are the 17-screen figures.

6. **The evidence was not shipping with the audit.** All 61 dumps were written only to
   `Docs/Diagnostics/_capture/`, which `.gitignore` excludes — so every JSON this report cites was
   per-machine, and no reader, including the Architect who turns these groups into Quick specs,
   could open one. They are now copied to the tracked `Docs/Reports/DesignAudit/` (9.4 MB raw,
   ~0.3 MB packed) and the numbers are computed from that copy.

7. **The headline finding's severity was wrong and was corrected by the gate, not by the author.**
   § 3.1 was S1 on the strength of `font.name` alone; the render was never looked at. It is S3.
