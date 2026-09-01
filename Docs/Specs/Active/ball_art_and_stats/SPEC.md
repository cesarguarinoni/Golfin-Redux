# SPEC/RUNBOOK — `ball_art_and_stats` (18 full images + stats for all 20 balls)

> Self-contained: a fresh session can execute this with no other context. Runner must be a
> **Cowork/Architect session** (needs Cesar's Chrome via claude-in-chrome + device folder grants);
> Claude Code cannot drive Chrome.
>
> **The runner also writes the Claude Code spec for the data half** (Cesar, 2026-08-31). Do not
> expect a Code kickoff to exist — producing it is deliverable D5 below.

## Goal

Balls are the last catalog still running on 2 rows. Twenty ball designs exist as hand-made
1000×1000 art and have shipped in the shot UI for months, but only **Golfin** and **Putt Ace** have
a detail image, a `Balls.csv` row, or stats. This task takes all 20 to parity with the club roster:

1. a **537×900 "full" detail image** for the remaining 18, generated the same way club fulls were;
2. a **stat line** for all 20, tiered by rarity, designed against the real physics coefficients;
3. EN+JA blurbs;
4. a Claude Code spec + kickoff for the CSV / localization / importer half.

## Decisions of record (Cesar, 2026-08-31)

- **Roster = the 20 designs that exist.** Ball-only brands (CIRQ, SORALIS, SHIMMERG, ACE ATTIRE,
  BIRDIE V1, CLOVER PRO, GOLFIN MK2) stay ball-only; the seven club brands with no ball (EAGLEZ,
  FOREFIT, BogeyB, GREEN SWING, FairX, TeePit, VBOOOT) are **not** in scope and are **not** logged
  as a follow-up — Cesar chose "ship the 20 that exist" over "ship 20, note the 7".
- **Stats are tiered by rarity** (§4). Rarity does not exist on the ball today — resolving where it
  lives is part of this task (§4.2), and it is the one open decision the runner must take to Cesar.
- **Runner is Cowork, not Code.** Code's work is specced by the runner at the end, not by the
  architect up front.

## 1. What actually exists (verified against the repo 2026-08-31)

| Thing | State |
|---|---|
| Base ball art | **20 designs**, 1000×1000 RGBA, `Assets/Art/Original UI/Ball Sprites/S_Controls_Ball_<BRAND>.png` |
| …copied into Resources | 18 of them + `Golfin.png` (200×200) + `PuttAce.png` (178×178) in `Assets/Resources/Balls/Thumbnails/` |
| …**not** copied into Resources | `S_Controls_Ball_GOLFINMK2.png` and `S_Controls_Ball_PUTTACE.png` |
| Full detail images | **2 only** — `Assets/Resources/Balls/Full/{Golfin,PuttAce}.png`, 537×900 RGBA |
| `Assets/Data/Balls.csv` | **2 rows** — `ball_golfin`, `ball_putt_ace` |
| Localization | 8 UI keys + `BALL_INFO_GOLFIN` / `BALL_INFO_PUTT_ACE`, EN+JA, in `Assets/Localization/LocalizationText.csv` |
| Stat icons | `Assets/Art/BallsScreen/Icon{Rebound,Roll,Spin,Wind}.png` — **no Power icon** (Power renders name+bar+number only) |

### ⚠️ There are NO per-ball 3D models

`Assets/Art/3D/Balls/` holds one generic mesh plus a **single** Golfin-skinned prefab
(`GolfinBall/Pf_Golfin_Ball.prefab`, `MAT_Golfin_Ball.mat`) and a red variant. Nothing in
`Assets/Scripts` swaps a ball material or mesh by `ballId` — grep for `sharedMaterial` returns
trajectory, water and OB code only. **The in-world ball is the same Golfin ball whatever you have
equipped.** Per-ball 3D skins would be 20 albedo textures plus an equip→material lane that does not
exist yet. Out of scope here (§8); raise it with Cesar as its own task if he wants it.

## 2. Deliverables

- **D1** — 18 × `Assets/Resources/Balls/Full/<Name>.png`, 537×900 RGBA, committed to the working tree.
- **D2** — thumbnail wiring resolved (§3.3) so every one of the 20 resolves from the CSV.
- **D3** — approved stat table for all 20 balls (§4), written into `BALL_IDENTITY.md` in this folder.
- **D4** — EN+JA blurb per ball (§5).
- **D5** — `Docs/Specs/Active/ball_data_wiring/SPEC.md` + a Claude Code kickoff block, covering
  Balls.csv rows, `BALL_INFO_*` keys, the rarity column decision and the importer run (§6).
- Review sheets to Cesar as each set of 6 lands, on a **neutral checkerboard — never magenta**.

## 3. Art

### 3.1 The full image is a W1 scene swap

`Full/Golfin.png` and `Full/PuttAce.png` are the **same photoreal scene**: a golf ball at rest on
close-cut green grass, blurred fairway/bunker/flagstick behind, soft daylight, ball centred slightly
below middle and filling ~55% of the width. Only the ball changes between them. So this is exactly
the club-full workflow (`club_art_batches` §W1), with an easier setup: **every ball already has
reference art**, so there is no PUTT ACE-style bootstrapping.

| | |
|---|---|
| FIRST image (template) | `Assets/Resources/Balls/Full/Golfin.png` — the scene to keep |
| SECOND image (brand reference) | that ball's `S_Controls_Ball_<BRAND>.png` — colour and graphics only |
| Output | 537×900 RGBA, scene kept, **30px rounded corners** (same as club fulls) |

Prompt shape that worked on the club fulls, adapted:

> Take the FIRST image — a photograph of a golf ball resting on a putting green — and replace the
> ball in it with the ball shown in the SECOND image. Everything about the FIRST image stays exactly
> as it is: same background, same grass, same flagstick, same lighting and shadow, same camera
> angle, same ball position and size in frame. Only the ball's livery changes. Copy the SECOND
> image's exact colours, graphics and wordmark onto a real dimpled golf ball, wrapped naturally
> around the sphere with correct perspective and the dimple texture showing through the paint. The
> wordmark must be spelled exactly `<WORDMARK>`, printed once, level and fully readable, not cut off
> at the edge of the ball. Photoreal product render, no fabric or felt texture. No other text or
> watermark anywhere, and do NOT draw any real-world brand logo of any kind — no Nike mark, no tick,
> no check mark, no manufacturer emblem.

### 3.2 Known traps, carried over from the club run

- **Spelling.** Gemini drops letters in invented wordmarks (`PUT ACE` for PUTT ACE, `VBOOT` for
  VBOOOT). **Re-roll, never correct** — corrections on letter count reliably make it worse. Highest
  risk here: `ACE ATTIRE` (interlocked letters), `BIRDIE`, `CLOVER PRO`, `PAR PERFECT`, `VOIGT94`
  on TIFTO, and `KLYRO` repeated around a ring.
- **Wrap-around clipping.** A ball is a sphere, so a long wordmark curves off the edge. The shipped
  `Full/PuttAce.png` already shows this — "PUTT ACE" breaks awkwardly across two lines with the
  letters crowding the silhouette. Ask for the wordmark **sized to sit inside the ball's front
  face**. Treat the shipped Putt Ace full as a known-mediocre baseline, not a target.
- **Never click Send twice** — the button becomes Stop and cancels the generation.
- Attachments are usually still there when the composer looks empty; **scroll the composer up**
  before concluding they dropped.
- A generation that stalls past ~2 minutes usually ends in "I seem to be encountering an error".
  Abandon it and re-send in a fresh chat.

### 3.3 Thumbnails — the naming mismatch

`BallDatabaseCSV` resolves `thumbnailSprite` under `Resources/Balls/Thumbnails/` and `fullSprite`
under `Resources/Balls/Full/`. The 18 unwired balls' art is named `S_Controls_Ball_<BRAND>`, which is
not a clean display name.

**Default: set `thumbnailSprite` to the existing file stem** (`S_Controls_Ball_MIREO`, …) rather than
renaming the art. Smallest diff, and it keeps the hardcoded
`Resources.Load<Sprite>("Balls/Thumbnails/S_Controls_Ball_GOLFIN")` in `BallButtonWidget`,
`CentralBallWidget` and three editor builders valid. Two files must still be **copied** into
`Resources/Balls/Thumbnails/`: `S_Controls_Ball_GOLFINMK2.png` and `S_Controls_Ball_PUTTACE.png`.

Verify on device that a 1000×1000 thumbnail renders correctly in the Balls-screen carousel and the
compare panel before committing to this — the two wired balls use 200×200 and 178×178, so the
carousel has never been shown a 1000×1000 sprite. If it looks wrong, downscale copies to 200×200
named `<Name>.png` instead and say so in the report.

## 4. Stats

### 4.1 The physics, exactly (`StatCoefficients.Default`, `StatCaps.Default`, `StatModifierResolver`)

Five stats, each **−10..+10**, read from the CSV as `power, rebound, windResistance, roll, spin`.

| Stat | Per point | Where it lands | Range at ±10 |
|---|---|---|---|
| Power | 0.01 | velocity multiplier, multiplied with Club Power and Character Strength, capped at 2.6 | ±10% velocity |
| Rebound | 0.02 | bounce restitution, clamped 0.80–1.20 | **saturates the clamp exactly at ±10** |
| Roll | 0.02 | rolling resistance, clamped 0.80–1.20, **inverted** | **saturates the clamp exactly at ±10** |
| Wind res. | 0.01 | wind-delta drag cut, clamped 0–0.30 | +10 → 0.10, i.e. **only ⅓ of the cap** |
| Spin | 0.01 | applied spin magnitude | ±10% spin |

Three things follow, and they are what make a stat line good rather than arbitrary:

- **Rebound and Roll are hard-edged at ±10.** The coefficient was tuned so ±10 exactly fills the
  clamp (`ball_rebound_perceptibility` / Order 417 raised Rebound from 0.01 to 0.02 for precisely
  this). ±10 is the real edge; there is no headroom past it and two balls at +10 feel identical.
- **`+Roll` means the ball rolls FARTHER — on the fairway *and* on the green.** A high-Roll ball is
  long off the tee and hard to stop putting. A control ball wants **negative** Roll. Get this
  backwards in the blurbs and every description is a lie.
- **Wind resistance is the weak stat.** At 0.01/pt a maxed +10 buys 0.10 of a 0.30 cap. A "wind
  ball" built today is worth about a third of what the cap implies.

> **Open decision for Cesar (do not change silently):** raise `BallWindCutPerPoint` from `0.01` to
> `0.02` or `0.03` so +10 reaches 2/3 or all of `WindCutMax`. This is a physics-side change to
> shipped feel and would need a perceptibility check exactly like `ball_rebound_perceptibility` did.
> Recommend putting it to him **before** designing the table, because if wind stays weak then wind
> should be a cheap stat in the budget, and if it gets fixed it should be an expensive one.

### 4.2 Where does ball rarity live? — the one thing that must be settled first

Balls have **no rarity**. `BallDataRuntime` has no field, `Balls.csv` has no column, and
`BallDatabaseCSV.ParseRow` never reads one. Today rarity is written on the *listing*, not the ball:

- `gacha_pools.csv` → `psc1_ball_golfin,…,ball_golfin,Common,60,…`
- `shop_catalog.csv` → `shop_ball_putt_ace,ball,ball_putt_ace,50,35,50,false,true,Rare,…`

Tiering stats by rarity needs rarity on the ball. Two routes, both real:

- **(a) Add a `rarity` column to `Balls.csv`.** `content_rows.data` is jsonb of the CSV row with
  values stored verbatim as strings, so a new column flows through the importer **without a
  migration**. Costs: one field on `BallDataRuntime`, one `f.Get("rarity")` in `ParseRow`, and
  whatever the admin dashboard's content panel needs to render it. Rarity then means the same thing
  for balls as for clubs and characters.
- **(b) Leave rarity on the listing** and treat the tier as design-time only — the budget governs the
  numbers, but nothing at runtime knows a ball's tier.

Recommend **(a)**: the Balls screen has no rarity framing today and it is the obvious next ask, the
gacha reveal already colours by rarity, and (b) means the tier exists only in a spec nobody reads at
runtime. **Put it to Cesar; it changes the Code spec materially.**

### 4.3 Budget

Net sum of the five stats, by tier. Calibrated so the shipped Putt Ace (+10/−6/0/+5/−4 = **+5**,
listed Rare) is already legal and needs no retune:

| Tier | Net sum | Notes |
|---|---|---|
| Common | 0 | pure trade-off, or all-zero for the default |
| Uncommon | +3 | |
| Rare | +5 | Putt Ace sits here |
| Mythic | +7 | |
| Legendary | +9 | |

Supreme is unused for balls unless Cesar wants it. Hard rules on top of the budget:

1. Every stat in **−10..+10**. Never exceed — Rebound and Roll physically cannot pay out past ±10.
2. Every ball except `ball_golfin` carries **at least one negative of −3 or worse**. A ball with no
   downside is a strictly-better ball, and balls are consumable, so that is a pay-to-win lane.
3. **At most one stat at ±10**, and only at Rare or above.
4. Sum of absolute values **≥ 8** for anything above Common — a Common may be flat, but a Rare that
   nets +5 as `+1/+1/+1/+1/+1` has no identity.
5. `ball_golfin` stays `0,0,0,0,0` and `isDefault=true`. It is the ball every player owns, it is
   granted for every "a ball" reward, and it must never be a gacha prize or a shop listing.
6. The stat line must match the art. SHIMMERG's oil-slick and TIFTO's ripple mark are not the same
   ball; neither are FYLOE **Aim** and FYLOE **Soft**.

`BALL_IDENTITY.md` in this folder carries a first-pass table for all 20 as something to react to.
It is **DRAFT — not approved**. Take it to Cesar before generating any blurbs from it.

## 5. Blurbs

One `info` per ball, EN **and** JA. Two-sentence house style, matching the shipped pair: what the
brand is known for, then what the ball actually does — naming the real trade-off, not just the
upside. Say what the numbers say: if Roll is negative, it checks up; it does not "roll true".

Both copies are required and they are separate systems:

- `Balls.csv` `info` column — the raw English fallback.
- `LocalizationText.csv` key `BALL_INFO_<ID minus the ball_ prefix, uppercased>` — EN + JA, e.g.
  `ball_ace_attire` → `BALL_INFO_ACE_ATTIRE`. `BallDetailPanel.LocalizeBody` reads the key and falls
  back to `template.info` only when the key is missing.

## 6. The Code half (D5 — the runner writes this spec, at the end)

Once art and stats are approved, write `Docs/Specs/Active/ball_data_wiring/SPEC.md` + `STATUS.md`,
add the pointer and kickoff block to `Docs/TellCode.md` under SPEC_READY POINTERS, and deliver the
kickoff in chat. It must cover, at minimum:

- 18 new rows in `Assets/Data/Balls.csv`, ids `ball_<snake_case>` (see §7).
- 18 × `BALL_INFO_*` in `Assets/Localization/LocalizationText.csv` with EN **and** JA in the same
  commit.
- The rarity-column outcome from §4.2, including `BallDataRuntime`, `ParseRow` and the admin panel.
- The `BallWindCutPerPoint` outcome from §4.1, if Cesar took it.
- **The importer path, spelled out** (`claude/WORKFLOW_NOTES.md`, "New text strings"): balls and
  texts are existing catalogs, so this is
  `python3 Tools/content/import_content.py --env-file … --catalogs balls,texts` → read the PLAN
  verdicts → `--apply` → publish from the admin → `export_content.py --check` clean. Never
  code-only, never migration-only, never a hand-inserted `content_rows` row. If the plan reports
  CONFLICTS, stop and report — no `--overwrite-dirty` on the implementer's own judgment.
- Acceptance: `--check` clean for `balls` and `texts`; all 20 balls resolve both sprites with no
  `ContentSpriteGuard` veto in the Unity console; the Balls screen carousel shows 20 entries.

## 7. Roster — ids, sprite names, wordmark to render

`fullSprite` is the file stem under `Resources/Balls/Full/`; existing convention is PascalCase with
no prefix (`Golfin`, `PuttAce`).

| # | Art token | id | name | fullSprite | Wordmark on the ball |
|---|---|---|---|---|---|
| 1 | ACEATTIRE | `ball_ace_attire` | Ace Attire | `AceAttire` | ACE ATTIRE (interlocked) |
| 2 | BIRDIEV1 | `ball_birdie_v1` | Birdie V1 | `BirdieV1` | BIRDIE |
| 3 | CIRQ | `ball_cirq` | Cirq | `Cirq` | CIRQ / GOLF |
| 4 | CLOVERPRO | `ball_clover_pro` | Clover Pro | `CloverPro` | *(none — clover mark only)* |
| 5 | FAIRLOFT | `ball_fairloft` | Fairloft | `Fairloft` | FAIRLOFT / JAPAN |
| 6 | FAIRWAY | `ball_fairway_threads` | Fairway THREADS | `FairwayThreads` | Fairway / THREADS |
| 7 | FYLOEAIM | `ball_fyloe_aim` | Fyloe Aim | `FyloeAim` | *(none — crosshair mark)* |
| 8 | FYLOESOFT | `ball_fyloe_soft` | Fyloe Soft | `FyloeSoft` | FYLOE (repeated on the band) |
| 9 | GF | `ball_gf` | G&F | `GF` | G&F |
| 10 | GOLFIN | `ball_golfin` | Golfin | `Golfin` | *(G mark)* — **DONE** |
| 11 | GOLFINIX | `ball_golfinix` | GolfinIX | `GolfinIX` | GOLFIN**IX** |
| 12 | GOLFINMK2 | `ball_golfin_mk2` | Golfin MK2 | `GolfinMK2` | *(bold G mark)* |
| 13 | KLYRO | `ball_klyro` | Klyro | `Klyro` | KLYRO (ring, repeated) |
| 14 | MIREO | `ball_mireo` | MireO | `MireO` | MireO |
| 15 | PARPERFECT | `ball_par_perfect` | Par Perfect | `ParPerfect` | PAR PERFECT |
| 16 | PUTTACE | `ball_putt_ace` | Putt Ace | `PuttAce` | PUTT ACE — **DONE** |
| 17 | ROYAL | `ball_royal_swing` | Royal Swing | `RoyalSwing` | ROYAL SWING |
| 18 | SHIMMERG | `ball_shimmer_g` | Shimmer G | `ShimmerG` | *(G in a white disc)* |
| 19 | SORALIS | `ball_soralis` | Soralis | `Soralis` | SORALIS |
| 20 | TIFTO | `ball_tifto` | Tifto | `Tifto` | VOIGT94 (small) |

`ball_gf` → `BALL_INFO_GF`. Avoid `&` in filenames — `GF`, not `G&F`.

## 8. Out of scope (do NOT do these)

- **Per-ball 3D models / in-world ball skins.** See §1. Separate task if Cesar wants it.
- **Balls for the seven club brands with no ball art.** Cesar ruled the roster is the 20 that exist.
- **Gacha pools and shop listings for the new balls.** Rarity gets *decided* here; wiring balls into
  `gacha_pools.csv` / `shop_catalog.csv` is an economy task with its own pity/weight questions.
- **Retuning Golfin or Putt Ace.** Both ship. Putt Ace's stat line is already budget-legal; its full
  image is mediocre but regenerating it is optional and needs Cesar's say-so.
- **Rarity framing on the Balls screen UI.** Adding the column ≠ drawing a rarity border.
- **Running `git commit`.** Cowork never commits — hand Code the file list and message.

## 9. Acceptance

- [ ] 18 new `Full/<Name>.png`, all exactly 537×900 RGBA with 30px rounded corners.
- [ ] Every wordmark spelled correctly and fully inside the ball's face — zoomed and checked, not
      assumed. List each ball's wordmark verdict in the report.
- [ ] No real-world brand marks anywhere (the FOREFIT/Nike incident — zoom every ball).
- [ ] All 20 stat lines obey §4.3 rules 1–6; the arithmetic is shown per ball.
- [ ] EN + JA blurb per ball, and every blurb's claims match the sign of its stats.
- [ ] `BALL_IDENTITY.md` updated to the approved table, marked APPROVED with the date.
- [ ] `Docs/Specs/Active/ball_data_wiring/` exists with SPEC.md + STATUS.md, `Docs/TellCode.md` has
      the pointer, and the kickoff block was delivered in chat.
- [ ] Review sheets sent on a neutral checkerboard, never magenta.
