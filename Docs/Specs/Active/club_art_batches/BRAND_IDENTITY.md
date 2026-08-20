# C1 — Club Brand Identity Sheet (for Cesar's approval)

**Purpose:** one signature strength per brand, expressed inside each rarity's stat caps (never
breaking them) and echoed in every description (EN+JA). The C2 generation script reads this table;
the art batches use the "look" column in prompts. Stats map only to WIRED mechanics
(`StatCoefficients`): Power→velocity, Accuracy→aim cone, LieResistance→terrain penalty,
Loft→launch shape, Durability→wear/repair economy; putters: Control/Accuracy/Weight.

Bias key: ++ strong lean / + lean / − trade-off, applied as a shift WITHIN the rarity band
(a Common EAGLEZ still loses to a Rare anything — rarity budget rules, brand shapes the split).

| Brand | Archetype | Stat bias | Look / description tone | Existing art (of 5 designs) |
|---|---|---|---|---|
| G&F | Trusted all-rounder | balanced | classic silver/black, heritage, "dependable" | D, W |
| GOLFIN | House flagship | balanced, small floor bonus spread | black/green house colors, confident | D, I, P, Wedge |
| KLYRO | Precision tech | Accuracy++ Power− | matte black + blue accents, crossed leaves, technical | ALL 5 ✅ |
| MireO | Spin & finesse | Loft/spin++ Accuracy+ Power− | dark + orange stars, artisan Japanese | I, W, Wedge |
| FYLOE | Recovery specialist | LieResistance++ Power− | rugged, earthy green, "escape anything" | D, W, Wedge |
| ROYAL SWING | Premium refined | Accuracy+ Power+ Durability− | gold/cream luxury, prestige | W, Wedge |
| EAGLEZ | Raw distance | Power++ Accuracy−− | aggressive red/black, speed lines, loud | D |
| FOREFIT | Forgiving workhorse | Durability++ LieRes+ Power− | chunky, safety orange, gym-brand energy | D |
| PAR PERFECT | Metronome consistency | Accuracy++ Loft− | clean white/navy, minimalist, clinical | D |
| BogeyB | Budget scrapper | Durability+ all else floor | scuffed charcoal/yellow, self-deprecating fun | I |
| Fairway THREADS | Style crossover | balanced, Accuracy slight+ | fashion-label stitching, fabric textures | I |
| GREEN SWING | Smooth turf | LieRes+ Loft+ | eco green/bamboo, organic curves | I |
| FairX | Modern aggressor | Power+ Accuracy− Durability+ | angular gunmetal/cyan, esports vibe | P |
| FAIRLOFT | High soft landing | Loft++ Power− | sky blue/white, airy | P |
| GOLFINIX | Touch & feel | putter Control++ / clubs Accuracy+ | iridescent black, boutique GOLFIN offshoot | P |
| PUTT ACE | Green specialist | putter Weight/Control++ / clubs Accuracy+ Power− | felt green/white, snooker-hall cool | none |
| TeePit WNDRWLL | Launch tech | Power+ Loft++ | violet/white gradient, spacey (WNDRWLL collab) | P |
| TIFTO | Indestructible | Durability++ Power− | tool-brand steel/red, industrial | W, Wedge(ctrl) |
| VBOOOT | Glass cannon | Power++ Loft+ Accuracy−− Durability− | neon chaos, meme energy | P |

Coverage math: 95 head designs total (19 brands × 5: D, W, I, Wedge shared across P/A/S, Putter).
~33 have portrait+controls pairs; 5 have Full scenes + KLYRO's 4 new = 9 Full. Remaining to
generate: ~62 portrait/controls pairs + ~86 Full scenes, batched brand-by-brand with type-matched
templates.

Description templates (per rarity variant, EN+JA): brand voice sentence + type role + rarity tier
flavor, e.g. EAGLEZ Supreme Driver: "EAGLEZ's flagship launcher — outrageous carry for players who
aim with their ego. Handle with care." / 「EAGLEZの最上位ランチャー。飛距離は暴力、精度は自己責任。」

**Approve / adjust the archetype column and C2 proceeds:** stat table generation per (type ×
rarity × brand bias) inside RarityStatCaps bands, monotonic carry ladders with even gaps
(Driver > Wood > Iron > PW > AW > SW), the AW/PW 136yd overlap fixed, one-brand sample sheet
first.
