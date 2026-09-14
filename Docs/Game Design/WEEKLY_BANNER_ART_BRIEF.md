# Weekly gacha banner art — brief

Written 2026-09-14 for the weekly rotation shipped by `weekly_rotation_admin`
(`Docs/Specs/Completed/weekly_rotation_admin/`). Every fact below is read from the plan
(`Assets/Resources/Data/rotations.csv`) and the catalogs, not from memory; re-run the
generator in the Rotations panel (PREVIEW) if a week's pins change.

## 1. What is missing

The game has **three** banners with their own art (`GachaBanner_StandardClub1`, `GachaBanner_TestA`,
`GachaBanner_TestB`) and **52 planned weekly banners with none**. Every weekly banner points at the
shared stand-in `GachaBanner_Weekly` — a blue re-tint of STANDARD CLUB 1 that still bakes that
banner's copy ("GET Drivers, Woods, Irons / CHANCE TO GET LEGENDARY GEAR!"). It was made on
2026-09-11 so a weekly banner is *visible* rather than withheld; it is not the week's picture.

- **Live right now:** `banner_wk_2026_38` — DRIVER WEEK · BOGEYB, 2026-09-14 → 09-21 — is on the
  stand-in (uploaded as its `artUrl`). Players see a STANDARD CLUB 1 lookalike under the title
  "DRIVER WEEK · BOGEYB".
- **Next Monday:** `banner_wk_2026_39` — WOOD WEEK · G&F — materialized as drafts, also on the stand-in.
- **Then** one banner per Monday through `wk_2027_36` (2027-09-06).

Twelve of the 52 are **seasonal** weeks that need a bespoke picture; the other forty are **brand**
weeks (one brand's club set, two hero clubs) and can share one layout family per brand — twenty
brands cover all forty, and the two hero renders swap.

## 2. The one-image spec

| | |
|---|---|
| Format | One portrait image per week, **882 × 1448 px** (the card's art slot — `gacha_admin_catalogs` §5.2 measured the bundled banner; the editor previews at card aspect and flags drift amber). sRGB. |
| **No text** | Hard rule from `gacha_admin_catalogs` §5.2 (decision 7): *"one image, no text in it"*. The card draws the title (EN **or** JA from the row), the countdown, RULES & RATES, the pity / guarantee lines and the prices over and around the art. STANDARD CLUB 1 predates the rule and bakes English copy — the weeklies must not, or the Japanese UI shows English slogans under a Japanese title. No numerals either ("×3", "50") — those are text. |
| Safe areas | The top edge sits directly under the title bar (nothing drawn over it). The **bottom ~15 %** is under the dark pity/guarantee strip — keep it quiet, no hero there. Centre 70 % is the stage. |
| Hero | The two **featured clubs** of the week (the rate-up prizes — ×3 weight, a featured Legendary is 1.5 % per pull against 0.5 % for a base Legendary). Their full renders exist at `Assets/Resources/Clubs/Full/{Type}-{Brand}.png` (listed per week below with the on-disk spelling) — same renders STANDARD CLUB 1 composites. Not the balls or the character: those are the STORE half of the week, not gacha prizes. |
| File to hand over | Master PNG at 882 × 1448, plus (or instead) a **JPEG ≤ 500 KB** — the admin upload cap (`CATALOG_ART_SPEC.maxBytes`, JPEG/PNG only, no WebP). q90 JPEG of the stand-in was 245 KB; a PNG of this size does not fit the cap. I can derive the JPEG from the PNG. |
| Naming | Hand-over name is free. When bundled at the next build (`GOLFIN/Content/Fetch URL Art`) it becomes `Assets/Resources/Art/Gacha/Banners/GachaBanner_{Pascal(bannerId − "banner_")}` — `banner_wk_2026_38` → **`GachaBanner_Wk202638`** (ASSET_NAMING_CONVENTION §5). |

## 3. How a picture reaches players

1. The week is materialized in the Rotations panel (drafts) — `wk_2026_38` and `wk_2026_39` already are; any future week can be, ahead of time.
2. The image is uploaded on the banner row (Gacha Banners → `banner_wk_…` → Upload art) — it becomes the row's `artUrl` in the `catalog-art` bucket. Since `d536b5b90` a re-MATERIALIZE keeps it.
3. PUBLISH ROTATION (or the ordinary gacha_banners publish) ships the URL; installed builds download it and re-bind the card (ladder step 3), showing the stand-in while it downloads.
4. The next TestFlight build bundles it under the convention name (step 2 of the ladder) — no more download on first launch.

A week's art is wanted **before its Monday 00:00 UTC (09:00 JST)**; uploaded mid-week it appears on the next launch of every installed build.

## 4. Priority

1. `wk_2026_38` — live now, ends Mon 09-21 (worth doing even mid-week: it is the first week players see).
2. `wk_2026_39` — Mon 09-21.
3. The rest of the 8-week calendar (`wk_2026_40` … `wk_2026_45`), which contains the first two seasonals (Sports Day 10-12, Halloween 10-26).
4. The remaining seasonals (each bespoke), then the brand template family.

## 5. The 52 banners

`Bundled name` is what the file becomes at build time. `Kind` — **brand** weeks share a layout family; **seasonal** weeks are bespoke (notes in §6).

| # | Week (Mon → Mon) | Banner id · bundled name | Title EN / JA | Kind | Hero clubs (featured, ×3) ← full render | Brand(s) pinned |
|---|---|---|---|---|---|---|
| 1 | 2026-09-14 → 2026-09-21 | `banner_wk_2026_38` · `GachaBanner_Wk202638` | DRIVER WEEK · BOGEYB / ドライバーウィーク · BOGEYB | brand | Driver BogeyB (Legendary) ← `Clubs/Full/Driver-BogeyB.png` + Wood BogeyB (Mythic) ← `Clubs/Full/Wood-BogeyB.png` | BogeyB |
| 2 | 2026-09-21 → 2026-09-28 | `banner_wk_2026_39` · `GachaBanner_Wk202639` | WOOD WEEK · G&F / ウッドウィーク · G&F | brand | Wood G&F (Legendary) ← `Clubs/Full/Wood-GandF.png` + Iron 5 G&F (Mythic) ← `Clubs/Full/Iron-GandF.png` | G&F |
| 3 | 2026-09-28 → 2026-10-05 | `banner_wk_2026_40` · `GachaBanner_Wk202640` | IRON WEEK · PAR PERFECT / アイアンウィーク · PAR PERFECT | brand | Iron 8 PAR PERFECT (Legendary) ← `Clubs/Full/Iron-ParPerfect.png` + A.Wedge PAR PERFECT (Mythic) ← `Clubs/Full/Wedge-ParPerfect.png` | PAR PERFECT |
| 4 | 2026-10-05 → 2026-10-12 | `banner_wk_2026_41` · `GachaBanner_Wk202641` | A.WEDGE WEEK · EAGLEZ / Aウェッジウィーク · EAGLEZ | brand | A.Wedge EAGLEZ (Legendary) ← `Clubs/Full/Wedge-Eaglez.png` + P.Wedge EAGLEZ (Mythic) ← `Clubs/Full/Wedge-Eaglez.png` | EAGLEZ |
| 5 | 2026-10-12 → 2026-10-19 | `banner_wk_2026_42` · `GachaBanner_Wk202642` | SPORTS DAY WEEK / スポーツの日ウィーク | seasonal | P.Wedge GOLFIN (Legendary) ← `Clubs/Full/Wedge-Golfin.png` + S.Wedge GOLFIN (Legendary) ← `Clubs/Full/Wedge-Golfin.png` | GOLFIN |
| 6 | 2026-10-19 → 2026-10-26 | `banner_wk_2026_43` · `GachaBanner_Wk202643` | S.WEDGE WEEK · PUTT ACE / Sウェッジウィーク · PUTT ACE | brand | S.Wedge PUTT ACE (Legendary) ← `Clubs/Full/Wedge-PuttAce.png` + Putter PUTT ACE (Mythic) ← `Clubs/Full/Putter-PuttAce.png` | PUTT ACE |
| 7 | 2026-10-26 → 2026-11-02 | `banner_wk_2026_44` · `GachaBanner_Wk202644` | HALLOWEEN WEEK / ハロウィンウィーク | seasonal | Putter FAIRLOFT (Legendary) ← `Clubs/Full/Putter-Fairloft.png` + Driver FAIRLOFT (Legendary) ← `Clubs/Full/Driver-Fairloft.png` | FAIRLOFT |
| 8 | 2026-11-02 → 2026-11-09 | `banner_wk_2026_45` · `GachaBanner_Wk202645` | DRIVER WEEK · GOLFINIX / ドライバーウィーク · GOLFINIX | brand | Driver GOLFINIX (Legendary) ← `Clubs/Full/Driver-GolfinX.png` + Wood GOLFINIX (Mythic) ← `Clubs/Full/Wood-GolfinX.png` | GOLFINIX |
| 9 | 2026-11-09 → 2026-11-16 | `banner_wk_2026_46` · `GachaBanner_Wk202646` | WOOD WEEK · ROYAL SWING / ウッドウィーク · ROYAL SWING | brand | Wood ROYAL SWING (Legendary) ← `Clubs/Full/Wood-RoyalSwing.png` + Iron 4 ROYAL SWING (Mythic) ← `Clubs/Full/Iron-RoyalSwing.png` | ROYAL SWING |
| 10 | 2026-11-16 → 2026-11-23 | `banner_wk_2026_47` · `GachaBanner_Wk202647` | IRON WEEK · FAIRWAY THREADS / アイアンウィーク · FAIRWAY THREADS | brand | Iron 7 Fairway THREADS (Legendary) ← `Clubs/Full/Iron-Fairway.png` + A.Wedge Fairway THREADS (Mythic) ← `Clubs/Full/Wedge-Fairway.png` | Fairway THREADS |
| 11 | 2026-11-23 → 2026-11-30 | `banner_wk_2026_48` · `GachaBanner_Wk202648` | A.WEDGE WEEK · GOLFINX / Aウェッジウィーク · GOLFINX | brand | A.Wedge G&F (Legendary) ← `Clubs/Full/Wedge-GandF.png` + P.Wedge GOLFINIX (Mythic) ← `Clubs/Full/Wedge-GolfinX.png` | FAIRLOFT, FOREFIT, G&F, GOLFINIX, GREEN SWING, MireO, PUTT ACE, TIFTO |
| 12 | 2026-11-30 → 2026-12-07 | `banner_wk_2026_49` · `GachaBanner_Wk202649` | P.WEDGE WEEK · TEEPIT WNDRWLL / Pウェッジウィーク · TEEPIT WNDRWLL | brand | P.Wedge TeePit WNDRWLL (Legendary) ← `Clubs/Full/Wedge-TeePit.png` + S.Wedge TeePit WNDRWLL (Mythic) ← `Clubs/Full/Wedge-TeePit.png` | TeePit WNDRWLL |
| 13 | 2026-12-07 → 2026-12-14 | `banner_wk_2026_50` · `GachaBanner_Wk202650` | S.WEDGE WEEK · FAIRX / Sウェッジウィーク · FAIRX | brand | S.Wedge FairX (Legendary) ← `Clubs/Full/Wedge-FairX.png` + Putter FairX (Mythic) ← `Clubs/Full/Putter-FairX.png` | FairX |
| 14 | 2026-12-14 → 2026-12-21 | `banner_wk_2026_51` · `GachaBanner_Wk202651` | PUTTER WEEK · GREEN SWING / パターウィーク · GREEN SWING | brand | Putter GREEN SWING (Legendary) ← `Clubs/Full/Putter-GreenSwing.png` + Driver GREEN SWING (Mythic) ← `Clubs/Full/Driver-GreenSwing.png` | GREEN SWING |
| 15 | 2026-12-21 → 2026-12-28 | `banner_wk_2026_52` · `GachaBanner_Wk202652` | HOLIDAY WEEK / ホリデーウィーク | seasonal | Driver TIFTO (Legendary) ← `Clubs/Full/Driver-Tifto.png` + Wood TIFTO (Legendary) ← `Clubs/Full/Wood-Tifto.png` | TIFTO |
| 16 | 2026-12-28 → 2027-01-04 | `banner_wk_2026_53` · `GachaBanner_Wk202653` | YEAR-END WEEK / 年末ウィーク | seasonal | Wood FOREFIT (Legendary) ← `Clubs/Full/Wood-Forefit.png` + Iron 4 FOREFIT (Legendary) ← `Clubs/Full/Iron-Forefit.png` | FOREFIT |
| 17 | 2027-01-04 → 2027-01-11 | `banner_wk_2027_01` · `GachaBanner_Wk202701` | NEW YEAR HATSUURI / 新春初売りウィーク | seasonal | Iron 9 KLYRO (Legendary) ← `Clubs/Full/Iron9-Klyro.png` + A.Wedge KLYRO (Legendary) ← `Clubs/Full/Wedge-Klyro.png` | KLYRO |
| 18 | 2027-01-11 → 2027-01-18 | `banner_wk_2027_02` · `GachaBanner_Wk202702` | A.WEDGE WEEK · VBOOOT / Aウェッジウィーク · VBOOOT | brand | A.Wedge VBOOOT (Legendary) ← `Clubs/Full/Wedge-VBOOOT.png` + P.Wedge VBOOOT (Mythic) ← `Clubs/Full/Wedge-VBOOOT.png` | VBOOOT |
| 19 | 2027-01-18 → 2027-01-25 | `banner_wk_2027_03` · `GachaBanner_Wk202703` | P.WEDGE WEEK · FYLOE / Pウェッジウィーク · FYLOE | brand | P.Wedge FYLOE (Legendary) ← `Clubs/Full/WedgeA-Fyloe.png` + S.Wedge FYLOE (Mythic) ← `Clubs/Full/WedgeA-Fyloe.png` | FYLOE |
| 20 | 2027-01-25 → 2027-02-01 | `banner_wk_2027_04` · `GachaBanner_Wk202704` | S.WEDGE WEEK · MIREO / Sウェッジウィーク · MIREO | brand | S.Wedge MireO (Legendary) ← `Clubs/Full/Wedge-Mireo.png` + Putter MireO (Mythic) ← `Clubs/Full/Putter-Mireo.png` | MireO |
| 21 | 2027-02-01 → 2027-02-08 | `banner_wk_2027_05` · `GachaBanner_Wk202705` | PUTTER WEEK · BOGEYB / パターウィーク · BOGEYB | brand | Putter BogeyB (Legendary) ← `Clubs/Full/Putter-BogeyB.png` + Driver BogeyB (Mythic) ← `Clubs/Full/Driver-BogeyB.png` | BogeyB, FairX, G&F, GOLFIN, TeePit WNDRWLL |
| 22 | 2027-02-08 → 2027-02-15 | `banner_wk_2027_06` · `GachaBanner_Wk202706` | VALENTINE WEEK / バレンタインウィーク | seasonal | Driver G&F (Legendary) ← `Clubs/Full/Driver-G&F.png` + Wood GOLFIN (Legendary) ← `Clubs/Full/Wood-Golfin.png` | EAGLEZ, FairX, G&F, GOLFIN, TeePit WNDRWLL, VBOOOT |
| 23 | 2027-02-15 → 2027-02-22 | `banner_wk_2027_07` · `GachaBanner_Wk202707` | WOOD WEEK · PAR PERFECT / ウッドウィーク · PAR PERFECT | brand | Wood PAR PERFECT (Legendary) ← `Clubs/Full/Wood-ParPerfect.png` + Iron 8 PAR PERFECT (Mythic) ← `Clubs/Full/Iron-ParPerfect.png` | FAIRLOFT, GOLFINIX, GREEN SWING, PAR PERFECT, VBOOOT |
| 24 | 2027-02-22 → 2027-03-01 | `banner_wk_2027_08` · `GachaBanner_Wk202708` | IRON WEEK · EAGLEZ / アイアンウィーク · EAGLEZ | brand | Iron 5 EAGLEZ (Legendary) ← `Clubs/Full/Iron-Eaglez.png` + A.Wedge EAGLEZ (Mythic) ← `Clubs/Full/Wedge-Eaglez.png` | BogeyB, EAGLEZ, GOLFIN, PUTT ACE, TIFTO |
| 25 | 2027-03-01 → 2027-03-08 | `banner_wk_2027_09` · `GachaBanner_Wk202709` | A.WEDGE WEEK · GOLFIN / Aウェッジウィーク · GOLFIN | brand | A.Wedge GOLFIN (Legendary) ← `Clubs/Full/Wedge-Golfin.png` + P.Wedge GOLFIN (Mythic) ← `Clubs/Full/Wedge-Golfin.png` | FairX, GOLFIN, GOLFINIX, GREEN SWING, TIFTO, TeePit WNDRWLL |
| 26 | 2027-03-08 → 2027-03-15 | `banner_wk_2027_10` · `GachaBanner_Wk202710` | P.WEDGE WEEK · PUTT ACE / Pウェッジウィーク · PUTT ACE | brand | P.Wedge PUTT ACE (Legendary) ← `Clubs/Full/Wedge-PuttAce.png` + S.Wedge PUTT ACE (Mythic) ← `Clubs/Full/Wedge-PuttAce.png` | G&F, GREEN SWING, PAR PERFECT, PUTT ACE, ROYAL SWING |
| 27 | 2027-03-15 → 2027-03-22 | `banner_wk_2027_11` · `GachaBanner_Wk202711` | S.WEDGE WEEK · FAIRLOFT / Sウェッジウィーク · FAIRLOFT | brand | S.Wedge FAIRLOFT (Legendary) ← `Clubs/Full/Wedge-Fairloft.png` + Putter FAIRLOFT (Mythic) ← `Clubs/Full/Putter-Fairloft.png` | FAIRLOFT, FOREFIT, FYLOE, PAR PERFECT, TIFTO |
| 28 | 2027-03-22 → 2027-03-29 | `banner_wk_2027_12` · `GachaBanner_Wk202712` | SAKURA WEEK / 桜ウィーク | seasonal | Putter GOLFINIX (Legendary) ← `Clubs/Full/Putter-GolfinX.png` + Driver MireO (Legendary) ← `Clubs/Full/Driver-Mireo.png` | FYLOE, GOLFINIX, MireO, PAR PERFECT, TeePit WNDRWLL |
| 29 | 2027-03-29 → 2027-04-05 | `banner_wk_2027_13` · `GachaBanner_Wk202713` | DRIVER WEEK · ROYAL SWING / ドライバーウィーク · ROYAL SWING | brand | Driver ROYAL SWING (Legendary) ← `Clubs/Full/Driver-RoyalSwing.png` + Wood ROYAL SWING (Mythic) ← `Clubs/Full/Wood-RoyalSwing.png` | FYLOE, FairX, PUTT ACE, ROYAL SWING, TeePit WNDRWLL |
| 30 | 2027-04-05 → 2027-04-12 | `banner_wk_2027_14` · `GachaBanner_Wk202714` | SPRING MAJOR WEEK / 春のメジャーウィーク | seasonal | Wood Fairway THREADS (Legendary) ← `Clubs/Full/Wood-Fairway.png` + Iron 4 GOLFINIX (Legendary) ← `Clubs/Full/Iron-GolfinX.png` | FAIRLOFT, FairX, Fairway THREADS, GOLFINIX, VBOOOT |
| 31 | 2027-04-12 → 2027-04-19 | `banner_wk_2027_15` · `GachaBanner_Wk202715` | IRON WEEK · GOLFINX / アイアンウィーク · GOLFINX | brand | Iron 4 VBOOOT (Legendary) ← `Clubs/Full/Iron-VBOOOT.png` + A.Wedge G&F (Mythic) ← `Clubs/Full/Wedge-GandF.png` | FairX, G&F, GOLFIN, MireO, PUTT ACE, TIFTO, VBOOOT |
| 32 | 2027-04-19 → 2027-04-26 | `banner_wk_2027_16` · `GachaBanner_Wk202716` | A.WEDGE WEEK · TEEPIT WNDRWLL / Aウェッジウィーク · TEEPIT WNDRWLL | brand | A.Wedge TeePit WNDRWLL (Legendary) ← `Clubs/Full/Wedge-TeePit.png` + P.Wedge TeePit WNDRWLL (Mythic) ← `Clubs/Full/Wedge-TeePit.png` | FAIRLOFT, FairX, GREEN SWING, MireO, TeePit WNDRWLL |
| 33 | 2027-04-26 → 2027-05-03 | `banner_wk_2027_17` · `GachaBanner_Wk202717` | P.WEDGE WEEK · FAIRX / Pウェッジウィーク · FAIRX | brand | P.Wedge FairX (Legendary) ← `Clubs/Full/Wedge-FairX.png` + S.Wedge FairX (Mythic) ← `Clubs/Full/Wedge-FairX.png` | FAIRLOFT, FOREFIT, FairX, GREEN SWING, KLYRO, ROYAL SWING |
| 34 | 2027-05-03 → 2027-05-10 | `banner_wk_2027_18` · `GachaBanner_Wk202718` | GOLDEN WEEK SPECIAL / ゴールデンウィーク特別 | seasonal | S.Wedge GREEN SWING (Legendary) ← `Clubs/Full/Wedge-GreenSwing.png` + Putter Fairway THREADS (Legendary) ← `Clubs/Full/Putter-Fairway.png` | EAGLEZ, Fairway THREADS, GOLFIN, GREEN SWING, PAR PERFECT, ROYAL SWING, VBOOOT |
| 35 | 2027-05-10 → 2027-05-17 | `banner_wk_2027_19` · `GachaBanner_Wk202719` | PUTTER WEEK · TIFTO / パターウィーク · TIFTO | brand | Putter TIFTO (Legendary) ← `Clubs/Full/Putter-Tifto.png` + Driver TIFTO (Mythic) ← `Clubs/Full/Driver-Tifto.png` | EAGLEZ, GOLFIN, KLYRO, TIFTO |
| 36 | 2027-05-17 → 2027-05-24 | `banner_wk_2027_20` · `GachaBanner_Wk202720` | DRIVER WEEK · FOREFIT / ドライバーウィーク · FOREFIT | brand | Driver FOREFIT (Legendary) ← `Clubs/Full/Driver-Forefit.png` + Wood FOREFIT (Mythic) ← `Clubs/Full/Wood-Forefit.png` | FOREFIT, GOLFIN, KLYRO, TIFTO |
| 37 | 2027-05-24 → 2027-05-31 | `banner_wk_2027_21` · `GachaBanner_Wk202721` | WOOD WEEK · KLYRO / ウッドウィーク · KLYRO | brand | Wood KLYRO (Legendary) ← `Clubs/Full/Wood-Klyro.png` + Iron 9 KLYRO (Mythic) ← `Clubs/Full/Iron9-Klyro.png` | FAIRLOFT, GOLFINIX, KLYRO, MireO, VBOOOT |
| 38 | 2027-05-31 → 2027-06-07 | `banner_wk_2027_22` · `GachaBanner_Wk202722` | IRON WEEK · VBOOOT / アイアンウィーク · VBOOOT | brand | Iron 5 G&F (Legendary) ← `Clubs/Full/Iron-GandF.png` + A.Wedge VBOOOT (Mythic) ← `Clubs/Full/Wedge-VBOOOT.png` | FOREFIT, FairX, G&F, GREEN SWING, PUTT ACE, VBOOOT |
| 39 | 2027-06-07 → 2027-06-14 | `banner_wk_2027_23` · `GachaBanner_Wk202723` | A.WEDGE WEEK · FYLOE / Aウェッジウィーク · FYLOE | brand | A.Wedge FYLOE (Legendary) ← `Clubs/Full/WedgeA-Fyloe.png` + P.Wedge FYLOE (Mythic) ← `Clubs/Full/WedgeA-Fyloe.png` | BogeyB, FOREFIT, FYLOE, GOLFINIX, KLYRO |
| 40 | 2027-06-14 → 2027-06-21 | `banner_wk_2027_24` · `GachaBanner_Wk202724` | P.WEDGE WEEK · MIREO / Pウェッジウィーク · MIREO | brand | P.Wedge MireO (Legendary) ← `Clubs/Full/Wedge-Mireo.png` + S.Wedge MireO (Mythic) ← `Clubs/Full/Wedge-Mireo.png` | G&F, GOLFINIX, MireO, ROYAL SWING, TIFTO, VBOOOT |
| 41 | 2027-06-21 → 2027-06-28 | `banner_wk_2027_25` · `GachaBanner_Wk202725` | S.WEDGE WEEK · BOGEYB / Sウェッジウィーク · BOGEYB | brand | S.Wedge BogeyB (Legendary) ← `Clubs/Full/Wedge-BogeyB.png` + Putter BogeyB (Mythic) ← `Clubs/Full/Putter-BogeyB.png` | BogeyB, KLYRO, PAR PERFECT, ROYAL SWING, VBOOOT |
| 42 | 2027-06-28 → 2027-07-05 | `banner_wk_2027_26` · `GachaBanner_Wk202726` | PUTTER WEEK · G&F / パターウィーク · G&F | brand | Putter G&F (Legendary) ← `Clubs/Full/Putter-GandF.png` + Driver G&F (Mythic) ← `Clubs/Full/Driver-G&F.png` | EAGLEZ, G&F, MireO, TeePit WNDRWLL |
| 43 | 2027-07-05 → 2027-07-12 | `banner_wk_2027_27` · `GachaBanner_Wk202727` | DRIVER WEEK · PAR PERFECT / ドライバーウィーク · PAR PERFECT | brand | Driver PAR PERFECT (Legendary) ← `Clubs/Full/Driver-ParPerfect.png` + Wood PAR PERFECT (Mythic) ← `Clubs/Full/Wood-ParPerfect.png` | FairX, Fairway THREADS, GREEN SWING, PAR PERFECT, PUTT ACE, TeePit WNDRWLL, VBOOOT |
| 44 | 2027-07-12 → 2027-07-19 | `banner_wk_2027_28` · `GachaBanner_Wk202728` | SUMMER LINKS WEEK / 真夏のリンクスウィーク | seasonal | Wood EAGLEZ (Legendary) ← `Clubs/Full/Wood-Eaglez.png` + Iron 7 MireO (Legendary) ← `Clubs/Full/Iron7-Mireo.png` | EAGLEZ, FAIRLOFT, FYLOE, MireO, PUTT ACE |
| 45 | 2027-07-19 → 2027-07-26 | `banner_wk_2027_29` · `GachaBanner_Wk202729` | IRON WEEK · GOLFIN / アイアンウィーク · GOLFIN | brand | Iron 4 GOLFIN (Legendary) ← `Clubs/Full/Iron-Golfin.png` + A.Wedge GOLFIN (Mythic) ← `Clubs/Full/Wedge-Golfin.png` | FAIRLOFT, FOREFIT, GOLFIN, GOLFINIX, TIFTO |
| 46 | 2027-07-26 → 2027-08-02 | `banner_wk_2027_30` · `GachaBanner_Wk202730` | A.WEDGE WEEK · PUTT ACE / Aウェッジウィーク · PUTT ACE | brand | A.Wedge PUTT ACE (Legendary) ← `Clubs/Full/Wedge-PuttAce.png` + P.Wedge PUTT ACE (Mythic) ← `Clubs/Full/Wedge-PuttAce.png` | BogeyB, FAIRLOFT, G&F, KLYRO, PAR PERFECT, PUTT ACE, ROYAL SWING, TIFTO |
| 47 | 2027-08-02 → 2027-08-09 | `banner_wk_2027_31` · `GachaBanner_Wk202731` | P.WEDGE WEEK · FAIRLOFT / Pウェッジウィーク · FAIRLOFT | brand | P.Wedge FAIRLOFT (Legendary) ← `Clubs/Full/Wedge-Fairloft.png` + S.Wedge FAIRLOFT (Mythic) ← `Clubs/Full/Wedge-Fairloft.png` | BogeyB, FAIRLOFT, FOREFIT, G&F, Klyro, PAR PERFECT |
| 48 | 2027-08-09 → 2027-08-16 | `banner_wk_2027_32` · `GachaBanner_Wk202732` | OBON WEEK / お盆ウィーク | seasonal | S.Wedge GOLFINIX (Legendary) ← `Clubs/Full/Wedge-GolfinX.png` + Putter MireO (Legendary) ← `Clubs/Full/Putter-Mireo.png` | Fairway THREADS, GOLFINIX, MireO, PAR PERFECT, TeePit WNDRWLL |
| 49 | 2027-08-16 → 2027-08-23 | `banner_wk_2027_33` · `GachaBanner_Wk202733` | PUTTER WEEK · ROYAL SWING / パターウィーク · ROYAL SWING | brand | Putter ROYAL SWING (Legendary) ← `Clubs/Full/Putter-RoyalSwing.png` + Driver ROYAL SWING (Mythic) ← `Clubs/Full/Driver-RoyalSwing.png` | EAGLEZ, FYLOE, Fairway THREADS, GOLFIN, ROYAL SWING, TeePit WNDRWLL |
| 50 | 2027-08-23 → 2027-08-30 | `banner_wk_2027_34` · `GachaBanner_Wk202734` | DRIVER WEEK · FAIRWAY THREADS / ドライバーウィーク · FAIRWAY THREADS | brand | Driver Fairway THREADS (Legendary) ← `Clubs/Full/Driver-Fairway.png` + Wood Fairway THREADS (Mythic) ← `Clubs/Full/Wood-Fairway.png` | FairX, Fairway THREADS, GOLFIN, PUTT ACE |
| 51 | 2027-08-30 → 2027-09-06 | `banner_wk_2027_35` · `GachaBanner_Wk202735` | WOOD WEEK · GOLFINX / ウッドウィーク · GOLFINX | brand | Wood BogeyB (Legendary) ← `Clubs/Full/Wood-BogeyB.png` + Iron 5 TIFTO (Mythic) ← `Clubs/Full/Iron-Tifto.png` | BogeyB, FYLOE, FairX, GOLFIN, MireO, PAR PERFECT, PUTT ACE, ROYAL SWING, TIFTO |
| 52 | 2027-09-06 → 2027-09-13 | `banner_wk_2027_36` · `GachaBanner_Wk202736` | SEASON FINALE / シーズンファイナル | seasonal | Iron 5 TeePit WNDRWLL (Legendary) ← `Clubs/Full/Iron-TeePit.png` + A.Wedge FairX (Legendary) ← `Clubs/Full/Wedge-FairX.png` | FairX, GREEN SWING, ROYAL SWING, TeePit WNDRWLL |

## 6. Seasonal weeks — bespoke notes

The store character named here is what the same week lists in the STORE (a rotation is store +
gacha as one unit) — a mood cue for the picture, **not** a subject for it: the gacha pool has no
characters. Two of these are the plan's Supreme weeks (Freda Faarlund) — the Architect still owes
a decision on those (report § Manual verification).

- **wk_2026_42 · SPORTS DAY WEEK / スポーツの日ウィーク** (2026-10-12) — hero: P.Wedge GOLFIN (Legendary) + S.Wedge GOLFIN (Legendary). Japanese Sports Day (2nd Monday of October). GOLFIN house brand, two Legendary wedges. Athletic / stadium energy; the store side of the week carries Camila Perez (Legendary).
- **wk_2026_44 · HALLOWEEN WEEK / ハロウィンウィーク** (2026-10-26) — hero: Putter FAIRLOFT (Legendary) + Driver FAIRLOFT (Legendary). Halloween. FAIRLOFT Legendary putter + Legendary driver. Night course, lanterns, orange/purple — keep the clubs the hero, not the props.
- **wk_2026_52 · HOLIDAY WEEK / ホリデーウィーク** (2026-12-21) — hero: Driver TIFTO (Legendary) + Wood TIFTO (Legendary). Christmas week. TIFTO. Winter dusk, warm lights; store side: Shae O'Connell (Legendary).
- **wk_2026_53 · YEAR-END WEEK / 年末ウィーク** (2026-12-28) — hero: Wood FOREFIT (Legendary) + Iron 4 FOREFIT (Legendary). Year-end (Dec 28 → Jan 4). FOREFIT. Countdown / last light of the year; store side: Ean McCormick (Legendary).
- **wk_2027_01 · NEW YEAR HATSUURI / 新春初売りウィーク** (2027-01-04) — hero: Iron 9 KLYRO (Legendary) + A.Wedge KLYRO (Legendary). 初売り (New Year first sale). KLYRO. Japanese New Year motifs (rising sun, pine, red/gold); this is a Freda Faarlund (Supreme) store week — the biggest week of the plan.
- **wk_2027_06 · VALENTINE WEEK / バレンタインウィーク** (2027-02-08) — hero: Driver G&F (Legendary) + Wood GOLFIN (Legendary). Valentine week — mixed brands (the pins are a cross-brand set). Reds/pinks; store side: Shae O'Connell.
- **wk_2027_12 · SAKURA WEEK / 桜ウィーク** (2027-03-22) — hero: Putter GOLFINIX (Legendary) + Driver MireO (Legendary). Sakura week — cross-brand pins. Cherry blossom course at golden hour; store side: Roshana Smith (Legendary).
- **wk_2027_14 · SPRING MAJOR WEEK / 春のメジャーウィーク** (2027-04-05) — hero: Wood Fairway THREADS (Legendary) + Iron 4 GOLFINIX (Legendary). Spring Major — cross-brand pins. Tournament / trophy mood, green jacket-adjacent without borrowing a real event; store side: Shae O'Connell.
- **wk_2027_18 · GOLDEN WEEK SPECIAL / ゴールデンウィーク特別** (2027-05-03) — hero: S.Wedge GREEN SWING (Legendary) + Putter Fairway THREADS (Legendary). Golden Week special — cross-brand pins. Gold, celebratory; the second Freda Faarlund (Supreme) store week.
- **wk_2027_28 · SUMMER LINKS WEEK / 真夏のリンクスウィーク** (2027-07-12) — hero: Wood EAGLEZ (Legendary) + Iron 7 MireO (Legendary). Summer links — cross-brand pins. Seaside links, hard light, dunes; store side: Shae O'Connell.
- **wk_2027_32 · OBON WEEK / お盆ウィーク** (2027-08-09) — hero: S.Wedge GOLFINIX (Legendary) + Putter MireO (Legendary). Obon week — cross-brand pins. Summer festival evening, lanterns; store side: Roshana Smith.
- **wk_2027_36 · SEASON FINALE / シーズンファイナル** (2027-09-06) — hero: Iron 5 TeePit WNDRWLL (Legendary) + A.Wedge FairX (Legendary). Season finale — cross-brand pins. Closing-ceremony scale: fireworks over the 18th; store side: Shae O'Connell.

## 7. Brand weeks — the template family

Forty weeks, twenty brands, one layout: the brand's mood (its colourway is already on the club
renders), the two hero renders large in the centre 70 %, secondary clubs of the same set small or
absent, nothing in the bottom 15 %. Producing the family once per brand (20 backgrounds) and
swapping the two hero renders per week covers the forty. Brands by number of weeks: FairX 14,
GOLFIN 13, PAR PERFECT / PUTT ACE / FAIRLOFT / TIFTO / TeePit WNDRWLL 12, G&F / GOLFINIX /
ROYAL SWING / GREEN SWING / MireO / VBOOOT 11, FOREFIT 9, BogeyB / EAGLEZ / KLYRO / FYLOE 8,
Fairway THREADS 7 (counts include the seasonal weeks' cross-brand pins).

