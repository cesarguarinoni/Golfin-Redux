# STATUS — club_art_batches

`IN_PROGRESS` (2026-08-20, Cowork/Architect runner).

## Committed and verified clean (9 brands)

**EAGLEZ — DONE, 13 new sprites committed (2026-08-20).** Driver portrait + driver controls were
already in the repo; generated Wood/Iron/Wedge/Putter portraits + controls and all five fulls
(`Full/{Driver,Wood,Iron,Wedge,Putter}-Eaglez.png`). 15/15 pass `qa_sprites.py`.
Raws: `~/Downloads/golfin_club_gen/egz_*_{portrait,controls,full}_raw.jpg` (13, md5-deduped).
- Controls needed `remove_white_bg(thresh=200)`, not the default 235 — EAGLEZ's chrome crown sits on
  a soft grey studio gradient that the default threshold leaves as haze. **Do NOT use
  `remove_shadow_ghost` on chrome-bodied brands: it eats the chrome and the shaft.**
- `qa.py` in the cloud container now carries the same corner-crossing patch as `qa_sprites.py`.

## Previously committed (8 brands)

KLYRO (pilot), **MireO**, **FYLOE**, **GOLFIN**, **G&F**, **ROYAL SWING**, **TIFTO**,
**GOLFINIX (complete 2026-08-20)**. All pass `qa_sprites.py` (12/12 for GOLFINIX).

## GOLFINIX — DONE, all 12 committed (2026-08-20)

Browser pipeline, W3→W2→W1, proven prompts verbatim + brand references. Committed over the 8
rejected API files: `Portraits/S_Menu_{Driver,Wood,Iron,Wedge}_GOLFINIX.png`,
`Controls/S_Controls_{Driver,Wood,Iron,Wedge}_GOLFINIX.png`, and NEW fulls
`Full/{Driver,Wood,Iron,Wedge}-GolfinX.png` (naming matches existing `Putter-GolfinX.png`).
Raws banked: `~/Downloads/golfin_club_gen/gxb_*_{portrait,controls,full}_raw.jpg` (12, md5-deduped).
Review sheet delivered in chat (checkerboard bg). IoU vs GOLFIN controls templates 0.85–0.99.
Stray-pixel components (floating orange squiggles from Gemini) auto-removed by keeping only the
largest alpha component — do this for every future controls sprite.
- ⚠️ RECURRING DEFECT: W2/W1 print the cavity-badge wordmark UPSIDE-DOWN (~3 of 8 gens).
  Weak fix ("rotate the text") failed once. PROVEN fix, use verbatim:
  "ERASE the text from the badge completely, then print \"GOLFINIX\" on the badge so it reads
  normally for a viewer of THIS image: G first on the left, X last on the right, letters upright.
  Change absolutely nothing else."
- `qa_sprites.py` patched: a single shaft crossing exactly AT a frame corner used to count as
  2 shafts + corner-alpha fail (Iron/Wood controls false-positived); corner-pair crossings now
  merge and an occupied top-left corner is exempt when the shaft runs through it. Real two-shaft
  defects (both ends of head) still fail.
- Cleanup for Cesar: `_to_delete/spurious_nested_GolfinRedux/` at repo root — an accidental
  nested commit path, safe to delete. Also `~/Downloads/gemini_key.txt` still exists —
  delete it and revoke the key at aistudio.google.com/apikey (API experiment is dead).

## ⚠️ MECHANIC: NEVER JUDGE A GENERATION FROM THE CHAT VIEWPORT (2026-08-20)

I wrongly declared FOREFIT "blocked" after four generations that all looked like extreme close-up
crown crops in the Gemini chat panel. They were not. The chat panel renders a tall portrait image
taller than the viewport, so scrolling to it shows only a HORIZONTAL SLICE of the head - which
reads exactly like a zoomed hero crop. Downloading attempt 4 showed a correctly framed
1664x2590 portrait (aspect 0.6425 vs the 264/411 target 0.6423), whole head plus shaft, wordmark
matching the shipped FOREFIT driver.

**Rule: judge framing ONLY from the downloaded file, never from the in-chat render.** Cheap check:
`Image.open(raw).size` - portrait raws should be ~0.642 aspect, controls ~1.781, fulls ~0.597.
Anything at those ratios is correctly framed no matter what the chat panel looked like.
Four generations were thrown away to this misread; do not repeat it.

## FOREFIT - COMPLETE, 13 of 13 committed (2026-08-21)

Committed: portraits Wood/Iron/Wedge/Putter, controls Wood/Iron/Wedge/Putter (driver portrait and
driver controls were already shipped), and all 5 full scenes
`Full/{Driver,Wood,Iron,Wedge,Putter}-Forefit.png`. All 8 sprites pass qa_sprites.py; all 5 fulls
are 537x900 with the 30px rounded mask.

### ⚠️ FULL SCENES: the template's GRIP colour bleeds through
W1 replaces the club but keeps the scene template's grip. `WedgeA-Fyloe` gives a bright purple grip
and `Putter-GolfinX` a purple/black one, so FOREFIT's wedge and putter came back purple-gripped
while driver/wood/iron (GandF/Klyro/Mireo templates) came back black. Shipped EAGLEZ fulls have the
whole shaft+grip recoloured to brand, so purple is a real defect, not the house style.
Fix is one in-chat correction on the same chat, which works reliably:
> Same image, one single change: recolour the grip from purple to matte black with a thin mint-white
> accent line. Everything else stays exactly as it is - same head, same FOREFIT wordmark, same
> chrome shaft, same background, same lighting, same framing.
**Check the grip colour on every full scene from the Fyloe wedge and GolfinX putter templates.**

### NOT a defect: mirrored wordmark on Iron and Wedge fulls
The iron and wedge scene templates show the club face-on, so the sole wordmark renders mirrored
(reads "TIFEROF"). Shipped `Iron-Eaglez.png` and `Wedge-Eaglez.png` do exactly the same. Leave it -
consistency with the shipped set wins.

### Download-grab gotcha
Gemini saves as `.jpeg`. A pre/post `ls *.png *.jpg` misses it and you can pick up a stale leftover
download instead of the new one. Diff the FULL listing: `ls -1 > /tmp/dl_pre.txt` before the click,
`ls -1 | diff /tmp/dl_pre.txt - | grep '^> '` after. One wrong file was banked this way and caught
only by opening it.

### ⚠️ TRADEMARK: the word "swoosh" pulls a real NIKE MARK
The identity sheet's FOREFIT Look column says "mint-white outlined swoosh". Using that word in a
prompt produced an actual Nike swoosh on the wood controls sole. An in-chat "remove the Nike logo"
correction did NOT clear it - it took a fresh generation with the word removed. Say **"mint-white
curved outline stripes"** instead, and add: "do NOT draw any real-world brand logo of any kind - no
Nike mark, no tick, no check mark, no manufacturer emblem." Identity sheet updated to match.
**Zoom every FOREFIT sole before shipping.** Check other brands whose Look column names a real-world
graphic idiom for the same failure.

### ⚠️ POST-PROCESSING, NOT GENERATION: the chrome-shaft "split shaft"
FOREFIT controls kept failing QA with 2-3 narrow top-edge crossings. I regenerated three times
before checking the raw - the raw shaft was ONE solid chrome tube every time. The specular highlight
running down a chrome shaft is near-white; where it touches the frame edge the flood fill drives a
3-4px slot up the middle, splitting the shaft in the alpha only.
**Always open the RAW before regenerating an anatomy defect.** Fixes, in order:
- `remove_white_bg(thresh=250)` instead of 235 for chrome-shaft brands (but 250 also lets background
  noise through on some raws - if strays explode, go back to 235 and rely on the seal below).
- `/root/seal.py` (cloud container): binary-closes the alpha with a 4px kernel to re-join the sliver,
  then keeps only the largest component. Run it on every controls sprite after the cut.

### ⚠️ PORTRAIT SCALE: fit by HEAD WIDTH, not bbox
`portrait()` fits the whole head+shaft bbox into 264x411, so a raw with a long shaft shrinks the head
- FOREFIT's wood and putter came out 156px wide where shipped art runs 224-255. `/root/fitfix.py`
scales by measured head width instead. Per-type targets from the shipped median:
**Driver 253, Iron 253, Wedge 252, Putter 250, Wood 232.**

### Known cosmetic issue, not fixed
`S_Controls_Iron_FOREFIT.png` - the FOREFIT wordmark reads upside-down. Spelling and count are
correct and the shaft/head are clean. Left as-is per Cesar's "note it and move on"; worth one
correction pass if he wants it.

## PAR PERFECT - COMPLETE, 13 of 13 committed (2026-08-21)

Committed: portraits Wood/Iron/Wedge/Putter, controls Wood/Iron/Wedge/Putter. Driver portrait and
driver controls were already shipped. All 8 pass qa.py. Plus all 5 full scenes,
`Full/{Driver,Wood,Iron,Wedge,Putter}-ParPerfect.png`, 537x900 with the 30px rounded mask.

### ⚠️ "strictly monochrome" in a W1 prompt DESATURATES THE WHOLE PHOTOGRAPH
The iron full came back with the grass, sky and wall all greyscale. Scope the colour rule to the
club: *"Keep the photograph itself exactly as it is and in FULL COLOUR - same green grass, same blue
sky, same wall, same daylight. Only the CLUB changes... the club itself carries no colour, only
black, chrome, silver and grey."* Works first time.
Also: the in-chat "put the colour back" correction restored colour but **swapped the smooth white
stucco wall for a rough sandstone block wall** - the background is not preserved across that repair.
Regenerate fresh instead of repairing a desaturated full.

### ⚠️ IDENTITY SHEET WAS WRONG FOR THIS BRAND - corrected from the art
The sheet said "clean white crown with a gloss black sole". The shipped driver is the opposite:
**matte black body with a large polished mirror-chrome crown panel**, "PAR" in wide squared italic
capitals above "PERFECT" in smaller capitals, engraved tone-on-tone in dark grey, fine dark groove
lines, chrome shaft with a black ferrule, strictly monochrome. Sheet row rewritten (ART WINS).

### Per-file threshold, not per-brand
PAR PERFECT's chrome shafts fade to pure white at the top, so `remove_white_bg` splits them. But a
single threshold does not work for the whole brand:
**wood 235+seal, iron 250+seal, wedge 250+seal, putter 235+seal.**
At 250 the wood raw let ~460k px of background noise through (98 phantom "shafts" in QA); at 235 the
iron and wedge shafts split into 3 strands. Try 250 first, check the opaque pixel count against the
other sprites of the same brand (~245k for controls here), and fall back to 235+seal if it explodes.

### Putter portraits: fit by BBOX width, not head width
`fitfix.place()` scales so the widest alpha row = target, then centres the *bounding box*. When the
shaft leans well off to one side (as the PAR PERFECT putter raw does) the bbox is much wider than
the head, so the head gets clipped at both frame edges. Shipped `S_Menu_Putter_GOLFIN.png` measures
maxrow 224, x-extent 5..258 - so for putters scale **bbox width -> 253** and centre. Gives maxrow
~221, matching the shipped norm.

### In-chat wordmark rotation fixes DO work
Both the wedge (rotated 90 degrees) and the iron (upside down) cleared on the first in-chat
correction: "the wordmark is rotated sideways / upside down. Redraw it so it reads horizontally
left-to-right for the viewer... Change absolutely nothing else." Cheaper than a regeneration.

### W2 "transparent background" summons a literal checkerboard
Write **"on a plain solid white background"** in the first W2 prompt instead of "transparent
background" - it avoids the checkerboard render and the extra correction round-trip entirely.

## BogeyB - 11 of 13 committed (2026-08-21), WEDGE + PUTTER FULLS STILL OWED

Committed: portraits Driver/Wood/Wedge/Putter, controls Driver/Wood/Wedge/Putter. Iron portrait and
iron controls were already shipped. All 8 pass qa.py at thresh=235, no seal needed - the olive-gold
shaft is dark enough that the flood fill never eats it. Plus 3 of 5 full scenes:
`Full/{Driver,Wood,Iron}-BogeyB.png`.

### ⛔ STOPPED ON GEMINI'S DAILY IMAGE QUOTA (2026-08-21)
"I can't create more images for you today." **Still owed: `Wedge-BogeyB.png` and
`Putter-BogeyB.png`.** Both raws are ready to go - resume with W1:
- Wedge: upload `mireo_up/WedgeA-Fyloe.jpg` first, `bb_wedge_portrait_raw.jpg` second
- Putter: upload `mireo_up/Putter-GolfinX.jpg` first, `bb_putter_portrait_raw.jpg` second

Both of those templates carry a PURPLE grip that bleeds through - the prompt already says
"an olive-gold shaft with a black ferrule band and a matte black grip. NO purple anywhere - the grip
must be matte black, not purple." Check the grip on the result anyway.

Identity-sheet row was already accurate for this brand; the only refinement is that the star cluster
is ONE LARGE star with two smaller ones, and the ferrule is a black band on an olive-gold shaft.

### ⚠️ W2 SILENTLY REBUILDS THE CLUB FACE-ON AND MIRRORED
BogeyB's driver and wedge controls both came back at the WRONG camera angle: the head redrawn
face-on, left-right flipped, shaft entering from the opposite corner, and the wedge turned into a
copy of the shipped iron with the grooves gone. Wood and putter from the same batch were fine, so
this is intermittent - and both bad ones passed qa.py, because QA checks anatomy, not pose.
**"keeping the first image's exact camera angle" is NOT enough.** Say the pose in words:
> We are looking at the driver from BEHIND AND SLIGHTLY ABOVE, seeing the large domed CROWN - not
> the face. The hosel and shaft leave the head at the TOP-LEFT and run up out of the top-left corner
> of the frame. The head body sweeps to the RIGHT. Do NOT mirror or flip the club. Do NOT change the
> camera angle. Do NOT redraw it face-on.

Framing it as "Repaint the FIRST image in the <BRAND> brand. Keep the first image's geometry
EXACTLY... Only the paint and graphics change" also holds the pose much better than "give me the
first image with the club on the second image".
**Always eyeball a new controls sprite against its template before committing.** Cesar caught these
two after they were already in the repo.

### Rotated wordmark on the driver controls: leave it
The driver controls print "BogeyB" rotated along the sole. One in-chat correction did NOT fix it,
and shipped `S_Controls_Iron_BOGEYB.png` reads the same way - it is the house style for this brand,
not a defect. Don't spend generations on it.

## Then, in order (10 brands, ~92 generations)

~~EAGLEZ~~ → ~~FOREFIT~~ → ~~PAR PERFECT~~ → BogeyB (fulls outstanding) → Fairway THREADS → GREEN SWING → FairX → FAIRLOFT →
TeePit → VBOOOT (13 each), then **PUTT ACE last** (15 — it has no reference art at all).

## Identity sheet: ALL 19 ROWS NOW CORRECTED

Per Cesar's ART-WINS rule, every brand's Look column was audited against the shipped sprites on
2026-08-20. **16 of 19 rows were wrong** and have been rewritten in
`claude/CLUB_BRAND_IDENTITY_SHEET.md`. Highlights for the brands still to generate:
- GOLFINIX = white + deep indigo patterned insert + orange heel accent + navy shaft (NOT "iridescent black")
- EAGLEZ = chrome crown + burgundy body + gold sunburst face (NOT "red/black speed lines")
- FOREFIT = gunmetal + mint-white swoosh (NOT "safety orange")
- PAR PERFECT = white/black minimalist (NOT navy)
- BogeyB = chrome + white + gold chevrons (NOT "scuffed charcoal/yellow")
- Fairway THREADS = plain chrome + script wordmark (NOT "fabric textures")
- GREEN SWING = chrome/white + bright grass green
- FairX = gloss black + white insert (NOT "gunmetal/cyan")
- FAIRLOFT = deep teal/petrol mallet (NOT "sky blue")
- TeePit = gunmetal + bright green band (NOT violet — the FYLOE clash is resolved)
- VBOOOT = gloss black + gold insert (NOT "neon chaos")

## QA — run the script, it now catches both shipped defects

    python3 Docs/Specs/Active/club_art_batches/qa_sprites.py Portraits Controls Full

Checks: **two shafts** (narrow limbs crossing the frame), **detached fragments** (severed head —
caught the GOLFINIX wood), exact size, corner alpha, duplicates. PIL+numpy only, no scipy.
Known non-pipeline failures: 6 stray files with full-scene names sitting in `Portraits/` at
168×261 (`Driver-G&F`, `Iron7-Mireo`, `Iron9-Klyro`, `Putter-GolfinX`, `WedgeA-Fyloe`,
`WedgeP-RoyalSwing`) — misplaced, ask Cesar before moving. Plus 5 legacy fulls at 2148×3600.

A thumbnail contact sheet is NOT sufficient review — both defect rounds were invisible at that size.

## ⚠️ API EXPERIMENT: TRIED AND REJECTED (2026-08-20) — DO NOT RETRY

A Gemini-API port was attempted with Cesar's approval and REJECTED by Cesar after review. Verdict:
text-described composition does not hold geometry. Even template-anchored API calls produced
misspelled wordmarks (GOLFING / OLFLNIX), foreign-brand bleed (GOLFIN's green + golf-ball icon),
wrong camera angles, missing shafts and interior alpha holes. Eight bad GOLFINIX sprites reached
the repo before being caught (4 portraits + 4 controls) — REGENERATE THESE IN THE BROWSER AND
OVERWRITE. The pipeline of record is the BROWSER workflow: proven prompts verbatim (+ their known
corrections), the brand's own reference images always attached. Do not iterate on the prompts.

## Old pace note (superseded — browser is the only pipeline now)

Driving Gemini through the browser is costing **15–20 tool actions per sprite** because the UI is
unstable: the "+" menu toggles unpredictably, the composer's y-position moves with content length,
attachments silently drop, and the send button often registers as hover-only. ~92 generations
remain. Mechanics 15–19 in SPEC.md capture the workarounds; the reliable sequence is:

1. `find` the textbox AND the "Upload & tools" button, act on them **by ref, never by coordinate**
2. type the prompt FIRST, screenshot to confirm it is in the composer
3. click "+" by ref, then `find` the hidden file input **in a separate call** (retry the click if
   find fails — the menu toggles)
4. `file_upload`, wait ~16s, then `find` the "Send message" button and click **by ref**
5. confirm the sent turn shows YOUR PROMPT BUBBLE under the thumbnails before waiting on the image

If this is going to be finished in bulk, the Gemini API (or nano-banana via a script) would be far
cheaper than UI automation. Worth raising with Cesar before grinding out the remaining 10 brands.

## Rules carried forward

- Cesar approved running the remaining brands unattended, review sheets only, no approval gates.
- Raws in `~/Downloads/golfin_club_gen/` (mireo_*, fyloe_*, golfin_*, gf_*, rs_*, tif_*, gx_*).
  `~/Downloads/grab.sh pre` before EVERY download click (mechanic 13).
- Use `S_Menu_Putter_GOLFIN.png` — NOT KLYRO — as the putter W3 composition reference (mechanic 18).
- device_commit_files times out past ~4 full-scene files; commit in batches of 3–4.
- Work in your OWN Chrome tab; Cesar uses the same Gemini account.
- OPEN ISSUE: shipped `Full/Putter-GolfinX.png` still carries a real grip-maker's name.
- Claude Code commits git, never Cowork.
