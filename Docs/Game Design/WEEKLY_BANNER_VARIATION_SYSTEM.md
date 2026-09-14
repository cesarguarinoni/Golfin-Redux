# Weekly banner art — variation system

Written 2026-09-14. Companion to `Docs/Game Design/WEEKLY_BANNER_ART_BRIEF.md`.
Supersedes the single-layout recipe: the brief's §7 plan of one shared layout per brand
would have given players the same picture with different clubs 40 weeks running.

Every one of the 52 weeks gets its own **composition × background** pair. Across the 40
brand weeks no pair repeats, and no two consecutive weeks share a composition, a background
family or a club palette. The 12 seasonals get a bespoke background from §6 of the brief.

---

## 1. The invariant contract

These hold for every banner regardless of variant. They are the acceptance criteria.

| | |
|---|---|
| Size | 882 × 1448 px, sRGB. Master PNG plus JPEG q95 (lands ~215–300 KB, under the 500 KB admin cap). |
| No text | No letters, words, numerals or brand wordmarks **anywhere**, including on the club heads. The card draws the title, countdown and rates over the art, in EN **or** JA. |
| Dead zone — top 18% | Dark and empty. The title, countdown pill, RULES & RATES **and the tagline ribbon** are all drawn over this band. Measured off the prefab 2026-09-14 — the earlier "top 8%" was wrong and clipped the club crowns. |
| Dead zone — 72.7% to 86% | The tagline hook band sits here. No club heads; shafts may cross it. |
| Dead zone — bottom 91% | Pity and guarantee lines, drawn straight on the art with no plate, so this band must fall away to near-black. |
| Stage | All club heads sit between **18% and 72%**. That is the whole usable band — compose to it. |
| Heroes | The week's **two featured clubs** — the `pinnedFeatured` pair, the only ones at ×3 weight. Liveries matching the `Clubs/Full/` renders. They dominate the frame and are the only clubs in sharp focus. |
| Support | **Three** more clubs from the same week's `pinnedClubs` set, behind the heroes, at most half their size, dimmer and softer. Standard on every banner (approved on wk_2026_38, 2026-09-14) — not optional. They say 'the whole set is in' without competing with the rate-up pair. Never in the bottom 15%, never sharp, never equal in size to a hero. Drop to two only if three genuinely will not fit the composition. |
| Proportions | Head no more than one sixth of the image height, with at least 4× that in visible shaft. Shafts exit the frame rather than ending in a grip. |

## 2. Compositions

**C1 · Crossed X** — The two club HEADS side by side in the upper third, shafts running down and crossing in a narrow X in the lower middle, both exiting past the bottom edge.
  *Support tier:* Behind and below the two heroes, a small fan of further club heads from the same set, half their size, sunk into the glow and softly out of focus.

**C2 · Parallel rise** — The two clubs upright and near-parallel, fanning very slightly apart, one head set about half a head-height higher than the other, both shafts running straight down and out of the bottom edge.
  *Support tier:* Two or three more shafts rising further back between and behind the pair, their heads only just breaking into view, heavily defocused.

**C3 · Diagonal sweep** — The pair laid along a strong diagonal: both heads together in the upper RIGHT of the frame, shafts sweeping down to the lower left and exiting the bottom-left corner. Leave the upper-left corner open and dark.
  *Support tier:* Supporting clubs echoing the same diagonal further up and right, progressively smaller and softer as they recede.

**C4 · Hero and support** — The first club large, sharp and slightly left of centre in the foreground; the second club set behind it, smaller, further back and softly out of focus. Shallow depth of field. Both shafts exit the bottom edge.
  *Support tier:* Supporting clubs dissolved deep into the background bokeh behind the second club, read as shapes rather than clubs.

**C5 · Low-angle tower** — Camera low, looking steeply UP at the clubs so they tower over the viewer: heads high and foreshortened near the top, shafts converging in strong perspective toward the bottom of the frame and out of it.
  *Support tier:* Supporting heads ranked behind the pair and lower in the frame, foreshortened away into the distance.

**C6 · Face-on pair** — Both club FACES turned square to the camera, heads side by side and flat on, shafts receding directly away from the viewer and downward with heavy foreshortening.
  *Support tier:* Supporting heads in a shallow row behind, each partly occluded by the two heroes in front, soft.

**C7 · Tilted V** — The pair in a shallow V, the whole arrangement rotated about 20 degrees off vertical so the frame feels kinetic and off-balance; the two shafts exit the bottom-left and bottom-right corners.
  *Support tier:* Supporting clubs filling the open wedge between the two heroes, smaller, tilted to match, and out of focus.

**C8 · Turned pair** — The two heads near the top centre, one rotated toward the viewer showing its crown and face, the other turned away showing its sole and back; shafts crossing low and exiting the bottom edge.
  *Support tier:* Supporting heads clustered behind at varied angles, defocused, none of them overlapping a hero head.

## 3. Background families

**B1 · Radial burst** — Light rays and glowing bokeh streaming outward from behind the club heads. The default; use it sparingly now that there are eight others.

**B2 · Stadium night** — A night tournament: distant floodlight flares with starburst points, dark out-of-focus grandstand, drifting haze.

**B3 · Course hour** — A real course thrown far out of focus, reduced to soft bands of fairway, bunker and sky. The time of day rotates.

**B4 · Material field** — A flat graphic field of a premium material catching a raking light, like a product backdrop. The material rotates.

**B5 · Element spray** — A frozen arc of water, sand, turf, dust or ice thrown up behind the heads, caught mid-flight. The element rotates.

**B6 · Aurora wash** — Smooth ribbons of aurora-like gradient light folding through deep space. Clean, atmospheric, no hard edges.

**B7 · Emblem plate** — One large soft glowing shape floating behind the clubs like a crest, edge-lit, the rest of the frame falling away dark. The shape rotates.

**B8 · Speed streaks** — Long horizontal motion streaks and light trails whipping past, suggesting clubhead speed.

**B9 · Particle fall** — A slow drift of embers, petals, snow, confetti or dust falling through the frame. The particle rotates.

**B10 · Seasonal (bespoke)** — per-week, from §6 of the brief. Listed inline in the table below.

Backgrounds B3, B4, B5, B7 and B9 each carry a rotating detail (time of day, material, element,
shape, particle) so that even a repeat family looks different — the table names the one to use.

## 4. Palettes, read off the club renders

Confirmed by eye from `Assets/Resources/Clubs/Full/`. The background light takes the brand's
accent; the club body colour tells you what must stay untouched.

| Brand | Colourway |
|---|---|
| BogeyB | silver-white body, gold trim |
| EAGLEZ | champagne gold face, deep maroon |
| FAIRLOFT | petrol teal body, white markings |
| FOREFIT | brushed gunmetal, no strong accent |
| FYLOE | violet body, green band |
| FairX | black body, white outline (monochrome) |
| Fairway THREADS | polished chrome, mint green dot |
| G&F | white body, red trim |
| GOLFIN | black body, lime green accents |
| GOLFINIX | white body, violet insert, orange dot |
| GREEN SWING | silver body, green stripe |
| KLYRO | black body, cyan graphics |
| MireO | black body, amber honeycomb |
| PAR PERFECT | chrome blade, black cavity (monochrome) |
| PUTT ACE | deep forest green body, white markings |
| ROYAL SWING | silver body, orange accents |
| TIFTO | black-gunmetal body, teal dot |
| TeePit WNDRWLL | gunmetal body, green graphic |
| VBOOOT | black body, amber-gold accents |

## 5. The 52-week rotation

| # | Banner id | Title | Kind | Brand palette | Composition | Background |
|---|---|---|---|---|---|---|
| 1 | `banner_wk_2026_38` | DRIVER WEEK · BOGEYB | brand | silver-white body, gold trim | C1 Crossed X | B1 · Radial burst |
| 2 | `banner_wk_2026_39` | WOOD WEEK · G&F | brand | white body, red trim | C2 Parallel rise | B2 · Stadium night |
| 3 | `banner_wk_2026_40` | IRON WEEK · PAR PERFECT | brand | chrome blade, black cavity (monochrome) | C3 Diagonal sweep | B3 · Course hour |
| 4 | `banner_wk_2026_41` | A.WEDGE WEEK · EAGLEZ | brand | champagne gold face, deep maroon | C4 Hero and support | B4 · Material field |
| 5 | `banner_wk_2026_42` | SPORTS DAY WEEK | seasonal | black body, lime green accents | C5 Low-angle tower | B10 · seasonal |
| 6 | `banner_wk_2026_43` | S.WEDGE WEEK · PUTT ACE | brand | deep forest green body, white markings | C6 Face-on pair | B6 · Aurora wash |
| 7 | `banner_wk_2026_44` | HALLOWEEN WEEK | seasonal | petrol teal body, white markings | C7 Tilted V | B10 · seasonal |
| 8 | `banner_wk_2026_45` | DRIVER WEEK · GOLFINIX | brand | white body, violet insert, orange dot | C8 Turned pair | B8 · Speed streaks |
| 9 | `banner_wk_2026_46` | WOOD WEEK · ROYAL SWING | brand | silver body, orange accents | C1 Crossed X | B9 · Particle fall |
| 10 | `banner_wk_2026_47` | IRON WEEK · FAIRWAY THREADS | brand | polished chrome, mint green dot | C2 Parallel rise | B1 · Radial burst |
| 11 | `banner_wk_2026_48` | A.WEDGE WEEK · GOLFINX | brand | white body, red trim | C3 Diagonal sweep | B2 · Stadium night |
| 12 | `banner_wk_2026_49` | P.WEDGE WEEK · TEEPIT WNDRWLL | brand | gunmetal body, green graphic | C4 Hero and support | B3 · Course hour |
| 13 | `banner_wk_2026_50` | S.WEDGE WEEK · FAIRX | brand | black body, white outline (monochrome) | C5 Low-angle tower | B4 · Material field |
| 14 | `banner_wk_2026_51` | PUTTER WEEK · GREEN SWING | brand | silver body, green stripe | C6 Face-on pair | B5 · Element spray |
| 15 | `banner_wk_2026_52` | HOLIDAY WEEK | seasonal | black-gunmetal body, teal dot | C7 Tilted V | B10 · seasonal |
| 16 | `banner_wk_2026_53` | YEAR-END WEEK | seasonal | brushed gunmetal, no strong accent | C8 Turned pair | B10 · seasonal |
| 17 | `banner_wk_2027_01` | NEW YEAR HATSUURI | seasonal | black body, cyan graphics | C1 Crossed X | B10 · seasonal |
| 18 | `banner_wk_2027_02` | A.WEDGE WEEK · VBOOOT | brand | black body, amber-gold accents | C2 Parallel rise | B9 · Particle fall |
| 19 | `banner_wk_2027_03` | P.WEDGE WEEK · FYLOE | brand | violet body, green band | C3 Diagonal sweep | B1 · Radial burst |
| 20 | `banner_wk_2027_04` | S.WEDGE WEEK · MIREO | brand | black body, amber honeycomb | C4 Hero and support | B2 · Stadium night |
| 21 | `banner_wk_2027_05` | PUTTER WEEK · BOGEYB | brand | silver-white body, gold trim | C5 Low-angle tower | B3 · Course hour |
| 22 | `banner_wk_2027_06` | VALENTINE WEEK | seasonal | white body, red trim | C6 Face-on pair | B10 · seasonal |
| 23 | `banner_wk_2027_07` | WOOD WEEK · PAR PERFECT | brand | chrome blade, black cavity (monochrome) | C7 Tilted V | B5 · Element spray |
| 24 | `banner_wk_2027_08` | IRON WEEK · EAGLEZ | brand | champagne gold face, deep maroon | C8 Turned pair | B6 · Aurora wash |
| 25 | `banner_wk_2027_09` | A.WEDGE WEEK · GOLFIN | brand | black body, lime green accents | C1 Crossed X | B7 · Emblem plate |
| 26 | `banner_wk_2027_10` | P.WEDGE WEEK · PUTT ACE | brand | deep forest green body, white markings | C2 Parallel rise | B8 · Speed streaks |
| 27 | `banner_wk_2027_11` | S.WEDGE WEEK · FAIRLOFT | brand | petrol teal body, white markings | C3 Diagonal sweep | B9 · Particle fall |
| 28 | `banner_wk_2027_12` | SAKURA WEEK | seasonal | white body, violet insert, orange dot | C4 Hero and support | B10 · seasonal |
| 29 | `banner_wk_2027_13` | DRIVER WEEK · ROYAL SWING | brand | silver body, orange accents | C5 Low-angle tower | B2 · Stadium night |
| 30 | `banner_wk_2027_14` | SPRING MAJOR WEEK | seasonal | polished chrome, mint green dot | C6 Face-on pair | B10 · seasonal |
| 31 | `banner_wk_2027_15` | IRON WEEK · GOLFINX | brand | black body, amber-gold accents | C7 Tilted V | B4 · Material field |
| 32 | `banner_wk_2027_16` | A.WEDGE WEEK · TEEPIT WNDRWLL | brand | gunmetal body, green graphic | C8 Turned pair | B5 · Element spray |
| 33 | `banner_wk_2027_17` | P.WEDGE WEEK · FAIRX | brand | black body, white outline (monochrome) | C1 Crossed X | B6 · Aurora wash |
| 34 | `banner_wk_2027_18` | GOLDEN WEEK SPECIAL | seasonal | silver body, green stripe | C2 Parallel rise | B10 · seasonal |
| 35 | `banner_wk_2027_19` | PUTTER WEEK · TIFTO | brand | black-gunmetal body, teal dot | C3 Diagonal sweep | B8 · Speed streaks |
| 36 | `banner_wk_2027_20` | DRIVER WEEK · FOREFIT | brand | brushed gunmetal, no strong accent | C4 Hero and support | B9 · Particle fall |
| 37 | `banner_wk_2027_21` | WOOD WEEK · KLYRO | brand | black body, cyan graphics | C5 Low-angle tower | B1 · Radial burst |
| 38 | `banner_wk_2027_22` | IRON WEEK · VBOOOT | brand | white body, red trim | C6 Face-on pair | B2 · Stadium night |
| 39 | `banner_wk_2027_23` | A.WEDGE WEEK · FYLOE | brand | violet body, green band | C7 Tilted V | B3 · Course hour |
| 40 | `banner_wk_2027_24` | P.WEDGE WEEK · MIREO | brand | black body, amber honeycomb | C8 Turned pair | B4 · Material field |
| 41 | `banner_wk_2027_25` | S.WEDGE WEEK · BOGEYB | brand | silver-white body, gold trim | C1 Crossed X | B5 · Element spray |
| 42 | `banner_wk_2027_26` | PUTTER WEEK · G&F | brand | white body, red trim | C2 Parallel rise | B6 · Aurora wash |
| 43 | `banner_wk_2027_27` | DRIVER WEEK · PAR PERFECT | brand | chrome blade, black cavity (monochrome) | C3 Diagonal sweep | B7 · Emblem plate |
| 44 | `banner_wk_2027_28` | SUMMER LINKS WEEK | seasonal | champagne gold face, deep maroon | C4 Hero and support | B10 · seasonal |
| 45 | `banner_wk_2027_29` | IRON WEEK · GOLFIN | brand | black body, lime green accents | C5 Low-angle tower | B9 · Particle fall |
| 46 | `banner_wk_2027_30` | A.WEDGE WEEK · PUTT ACE | brand | deep forest green body, white markings | C6 Face-on pair | B1 · Radial burst |
| 47 | `banner_wk_2027_31` | P.WEDGE WEEK · FAIRLOFT | brand | petrol teal body, white markings | C7 Tilted V | B2 · Stadium night |
| 48 | `banner_wk_2027_32` | OBON WEEK | seasonal | white body, violet insert, orange dot | C8 Turned pair | B10 · seasonal |
| 49 | `banner_wk_2027_33` | PUTTER WEEK · ROYAL SWING | brand | silver body, orange accents | C1 Crossed X | B4 · Material field |
| 50 | `banner_wk_2027_34` | DRIVER WEEK · FAIRWAY THREADS | brand | polished chrome, mint green dot | C2 Parallel rise | B5 · Element spray |
| 51 | `banner_wk_2027_35` | WOOD WEEK · GOLFINX | brand | silver-white body, gold trim | C3 Diagonal sweep | B6 · Aurora wash |
| 52 | `banner_wk_2027_36` | SEASON FINALE | seasonal | gunmetal body, green graphic | C4 Hero and support | B10 · seasonal |

### Background detail per week

Paste this sentence straight into the prompt's background slot.

| Banner id | Background text |
|---|---|
| `banner_wk_2026_38` | a radial burst of light rays and glowing bokeh sparkles streaming outward from directly behind the club heads |
| `banner_wk_2026_39` | a night tournament atmosphere: distant floodlight flares with starburst points, a dark out-of-focus grandstand, drifting haze |
| `banner_wk_2026_40` | a real golf course thrown far out of focus at blue hour dusk, reduced to soft bands of fairway, bunker and sky |
| `banner_wk_2026_41` | a flat graphic field of polished stone catching a raking light, like a premium product backdrop |
| `banner_wk_2026_42` | a stadium-scale sports festival: bold graphic track-and-field colour blocks, banners and pennants far out of focus, bright athletic daylight |
| `banner_wk_2026_43` | smooth ribbons of aurora-like gradient light folding through deep space, clean and atmospheric, no hard edges |
| `banner_wk_2026_44` | a night course under a huge low moon, carved-pumpkin lanterns glowing far out of focus, orange and violet mist low to the ground |
| `banner_wk_2026_45` | long horizontal motion streaks and light trails whipping past the clubs, suggesting clubhead speed |
| `banner_wk_2026_46` | a slow drift of confetti falling through the frame, catching the light as they pass |
| `banner_wk_2026_47` | a radial burst of light rays and glowing bokeh sparkles streaming outward from directly behind the club heads |
| `banner_wk_2026_48` | a night tournament atmosphere: distant floodlight flares with starburst points, a dark out-of-focus grandstand, drifting haze |
| `banner_wk_2026_49` | a real golf course thrown far out of focus at golden hour, reduced to soft bands of fairway, bunker and sky |
| `banner_wk_2026_50` | a flat graphic field of tiled hexagonal plates catching a raking light, like a premium product backdrop |
| `banner_wk_2026_51` | a frozen arc of dust and embers thrown up behind the heads, caught mid-flight with sharp droplets and grains |
| `banner_wk_2026_52` | a winter dusk course under falling snow, warm string lights strung out of focus, deep blue shadows and gold highlights |
| `banner_wk_2026_53` | the last light of the year: a low cold sun on the horizon, long shadows, a sky graduating from ember orange to deep indigo |
| `banner_wk_2027_01` | Japanese New Year: a rising sun disc, pine and plum silhouettes, red and gold, a crisp graphic hinomaru feel |
| `banner_wk_2027_02` | a slow drift of snow falling through the frame, catching the light as they pass |
| `banner_wk_2027_03` | a radial burst of light rays and glowing bokeh sparkles streaming outward from directly behind the club heads |
| `banner_wk_2027_04` | a night tournament atmosphere: distant floodlight flares with starburst points, a dark out-of-focus grandstand, drifting haze |
| `banner_wk_2027_05` | a real golf course thrown far out of focus at dawn mist, reduced to soft bands of fairway, bunker and sky |
| `banner_wk_2027_06` | Valentine week: deep rose and blush gradients, soft heart-shaped bokeh far out of focus, warm and romantic |
| `banner_wk_2027_07` | a frozen arc of torn grass and turf thrown up behind the heads, caught mid-flight with sharp droplets and grains |
| `banner_wk_2027_08` | smooth ribbons of aurora-like gradient light folding through deep space, clean and atmospheric, no hard edges |
| `banner_wk_2027_09` | one large soft glowing diamond floating directly behind the clubs like a crest, edge-lit, with the rest of the frame falling away dark |
| `banner_wk_2027_10` | long horizontal motion streaks and light trails whipping past the clubs, suggesting clubhead speed |
| `banner_wk_2027_11` | a slow drift of petals falling through the frame, catching the light as they pass |
| `banner_wk_2027_12` | a cherry blossom course at golden hour, sakura petals drifting, pink and warm white against soft green |
| `banner_wk_2027_13` | a night tournament atmosphere: distant floodlight flares with starburst points, a dark out-of-focus grandstand, drifting haze |
| `banner_wk_2027_14` | a spring major: a championship trophy plinth silhouette far out of focus, banked azaleas, rich green and cream, tournament grandeur |
| `banner_wk_2027_15` | a flat graphic field of brushed metal catching a raking light, like a premium product backdrop |
| `banner_wk_2027_16` | a frozen arc of bunker sand thrown up behind the heads, caught mid-flight with sharp droplets and grains |
| `banner_wk_2027_17` | smooth ribbons of aurora-like gradient light folding through deep space, clean and atmospheric, no hard edges |
| `banner_wk_2027_18` | Golden Week: a celebratory field of gold light, streamers and falling gold particles, luxurious and loud |
| `banner_wk_2027_19` | long horizontal motion streaks and light trails whipping past the clubs, suggesting clubhead speed |
| `banner_wk_2027_20` | a slow drift of embers falling through the frame, catching the light as they pass |
| `banner_wk_2027_21` | a radial burst of light rays and glowing bokeh sparkles streaming outward from directly behind the club heads |
| `banner_wk_2027_22` | a night tournament atmosphere: distant floodlight flares with starburst points, a dark out-of-focus grandstand, drifting haze |
| `banner_wk_2027_23` | a real golf course thrown far out of focus at overcast silver light, reduced to soft bands of fairway, bunker and sky |
| `banner_wk_2027_24` | a flat graphic field of matte technical fabric catching a raking light, like a premium product backdrop |
| `banner_wk_2027_25` | a frozen arc of water thrown up behind the heads, caught mid-flight with sharp droplets and grains |
| `banner_wk_2027_26` | smooth ribbons of aurora-like gradient light folding through deep space, clean and atmospheric, no hard edges |
| `banner_wk_2027_27` | one large soft glowing shield crest floating directly behind the clubs like a crest, edge-lit, with the rest of the frame falling away dark |
| `banner_wk_2027_28` | a seaside links under hard summer sun: marram dunes, white sand, deep blue sea and sky, high contrast |
| `banner_wk_2027_29` | a slow drift of dust motes falling through the frame, catching the light as they pass |
| `banner_wk_2027_30` | a radial burst of light rays and glowing bokeh sparkles streaming outward from directly behind the club heads |
| `banner_wk_2027_31` | a night tournament atmosphere: distant floodlight flares with starburst points, a dark out-of-focus grandstand, drifting haze |
| `banner_wk_2027_32` | Obon: a summer festival evening, paper lanterns glowing warm out of focus, indigo night, drifting sparks |
| `banner_wk_2027_33` | a flat graphic field of polished stone catching a raking light, like a premium product backdrop |
| `banner_wk_2027_34` | a frozen arc of crushed ice thrown up behind the heads, caught mid-flight with sharp droplets and grains |
| `banner_wk_2027_35` | smooth ribbons of aurora-like gradient light folding through deep space, clean and atmospheric, no hard edges |
| `banner_wk_2027_36` | a season finale: fireworks bursting over the 18th green at night, reflected light on water, closing-ceremony scale |

---

## 6. The prompt

One fresh Gemini chat per banner. Attach the week's two hero renders from `Clubs/Full/`
as reference images, then send:

```
Create an image: a vertical portrait mobile game gacha banner, aspect ratio 2:3.
Photorealistic product-render golf clubs. No cartoon illustration style, no people,
no buildings, and absolutely no text, letters, words or numbers anywhere in the
picture. The frame is dark at the very top edge and at the very bottom edge.

BACKGROUND: {BACKGROUND TEXT FROM THE TABLE}, lit in {BRAND ACCENT COLOUR}.

CLUBS: the two club heads from the reference photos are the heroes: {CLUB A} and
{CLUB B}. Match those two liveries exactly — {BODY COLOUR} bodies with {ACCENT} as
trim only, never an {ACCENT} body.

LAYOUT: {COMPOSITION TEXT FROM THE TABLE}

SUPPORTING CLUBS: behind the two heroes, up to three more clubs of the same
set — {SUPPORT TEXT FOR THIS COMPOSITION}. They must be clearly smaller than
the two heroes, clearly out of focus, and must not reach the bottom of the
frame. The two hero clubs stay the largest and the only sharp ones.

PROPORTIONS: each head is SMALL, no more than one sixth of the image height, and
the visible shaft is at least four times as long as the head is tall, so they read
as real full-length golf clubs rather than stubby toys.
```

Then: download full size, centre-crop the width to 0.609 aspect, resize to 882 × 1448,
export JPEG q95.

## 6b. Prompt corrections learned on weeks 42-45 (2026-09-14)

These supersede the wording in §6. Apply them to every remaining week.

1. **Never write "banner", and never write "aspect ratio 2:3".** Both pull the model to
   landscape — wk_2026_42 came back landscape four times running on the §6 wording. Open with:
   *"Create an image in a TALL VERTICAL format shaped like a mobile phone screen held upright,
   882 pixels wide by 1448 pixels tall, clearly taller than it is wide, never landscape."*
   Repeat "tall vertical phone-screen shape" in the closing line. With that wording weeks 43,
   44 and 45 came back portrait first time.
2. **Do not attach reference renders.** Gemini's upload control opens a native file picker the
   browser automation cannot drive. Describing the livery in words works as well or better —
   and it removes the §7.2 prompt-truncation trap entirely, since nothing is typed after an
   upload. Read the colourway off the head crops rather than the full render: wedges are
   chrome-bodied even for brands listed as "black body".
3. **Ban digits explicitly.** "No brand names" is not enough — wedges come back with a loft
   number (`60°`) stamped on the sole. Say *"no letters, words, numbers, degree marks or brand
   names anywhere, including on the club heads and soles."* Lettering still needed a strip turn
   on wk_2026_44 and wk_2026_45, so §7.4 stands.
4. **State the shaft direction.** Say the heads sit near the TOP and the shafts run DOWNWARD
   and out through the bottom edge. Left unsaid, the model hangs the clubs the other way up —
   wk_2026_42 shipped that way.
5. **A bright seasonal brief fights the invariant contract.** "Bright athletic daylight" cannot
   also be dark at top and bottom, and every attempt put colour where the pity lines go.
   wk_2026_42's background was rewritten to an EVENING festival under floodlights, keeping the
   colour blocks and pennants. Check the remaining seasonals in §5 for the same conflict before
   prompting: the summer, spring-major and Golden Week briefs all read bright.
6. **Watch for a letterbox seam.** Gemini sometimes composites a hard dark bar across the top
   instead of lighting the scene dark. Detect it with a row-luminance diff over the top 40% —
   a jump above ~30 is a bar, normal variation is under 15. Fix in post by stretching the
   content just below the seam upward over the band and applying a smooth darkening ramp,
   rather than spending another turn.
7. **Downloading: hover the image first.** Clicking "Download full size image" without hovering
   over the image silently does nothing — this is what burned calls in the earlier session.
   Hover, click, then confirm a NEW file appeared in `~/Downloads` before processing; never
   take "newest file" on trust, or you will reprocess the previous week and overwrite it.

## 7. Notes from the first three banners

1. **Proportions are what goes wrong first.** Left alone the model draws a huge head on a
   stubby shaft — the first pass had the head at 42% of the club's length against a real ~10%.
   Small head plus shafts leaving the frame is the fix; the length is implied off-frame.
2. **Do not lead with the proportions paragraph.** Putting it first displaced the style
   anchoring and returned bright cartoon art with a blue sky, palm trees and a character in
   frame. Style and negatives first, layout second, proportions last — that is why the prompt
   above is ordered the way it is.
3. **Shrinking the heads invites gold-plating.** State the body colour and that the accent is
   trim only.
4. **Lettering creeps back onto the heads.** Every banner so far needed one turn to strip a
   wordmark. Always check the heads at full resolution before accepting.
5. **Consecutive weeks drift to the same warm gold** unless told otherwise. That is what the
   palette column is for.
6. **Which clubs to show.** The two heroes are the row's `pinnedFeatured` pair in
   `Assets/Resources/Data/rotations.csv` — read them there, not from the title. The
   support tier comes from the same row's `pinnedClubs` (nine per week); pick up to
   three of the remaining seven, favouring distinct club types (iron, wedge, putter)
   over duplicates of the hero types, so the set reads as a set.
7. Budget ~3 turns per banner: generate, correct layout or colour, strip lettering.
8. `banner_wk_2026_53` and `banner_wk_2027_01` are the only adjacent pair sharing a background
   slot (both seasonal). Their briefs are far apart — cold last-light of the year against a
   red-and-gold New Year sun — so they will not read alike, but check them side by side.
