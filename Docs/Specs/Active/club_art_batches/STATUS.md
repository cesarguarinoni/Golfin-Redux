# STATUS — club_art_batches

`IN_PROGRESS` (2026-08-22, Cowork/Architect runner).

**Coverage: 18 of 19 brands complete — 90 of 95 head designs.**
(Verified against the repo 2026-08-28 by counting `S_Menu_*` per brand. Earlier revisions of this
line drifted upward by one because the count was incremented rather than re-verified — recount from
the filesystem, do not trust the running tally.)

## ⚠️⚠️ PROMPT RULE CHANGE (Cesar, 2026-08-28): STOP LOCKING THE HEAD SHAPE

**What was wrong.** Every W2/W3 prompt said *"same shape, same size, same camera angle, same
position"*. That clause existed to stop the BogeyB failure (head rebuilt face-on and mirrored) and
to keep framing stable for `fitfix.place`. But "same shape" also froze the head SILHOUETTE, so every
brand's iron is the same KLYRO cavity-back, every driver the same G&F head, and so on. Across 19
brands the roster reads as one club in 19 paint jobs.

**Cesar:** *"That makes all the clubs look too similar. Some variation is allowed, as long as the
clubs keep real world designs."*

**The fix - separate POSE (locked) from SHAPE (free).**

LOCKED, always, in every prompt:
- camera angle, pose and orientation
- position on the canvas, overall size in frame
- lighting, and the plain solid white background (W2/W3) or the untouched photograph (W1)
- never mirror, flip, rotate, move, zoom

FREE TO VARY, and it SHOULD vary brand to brand:
- crown profile, cavity vs muscle-back vs hollow-body, sole geometry, toe and heel shape,
  hosel transition, weight-port placement, topline thickness, mallet vs blade massing

The constraint on the variation is realism, not sameness: it must be a believable real-world club of
that type that a manufacturer could actually make, and it must still read instantly as that type.

### The replacement W3 (portrait) opening - use this verbatim
> Repaint the FIRST image in the <BRAND> brand shown in the second image.
> KEEP EXACTLY: the camera angle, the pose and orientation, the position on the canvas, the overall
> size in frame, the lighting, and the plain solid white background. Do NOT mirror or flip the club,
> do NOT rotate it, do NOT move it, do NOT zoom in or out.
> YOU MAY AND SHOULD REDESIGN the head's own silhouette and construction to suit this brand - a
> different crown profile, a different cavity or muscle-back shape, different sole geometry,
> different toe and heel shape, a different hosel transition. It must stay a realistic, believable
> real-world <TYPE> that a real manufacturer could make, and it must still read instantly as a
> <TYPE>. Give this brand its own head design rather than copying the first image's silhouette.
> <brand look string>
> IMPORTANT: do NOT draw any real-world brand logo of any kind. Exactly ONE shaft attached at ONE
> point, one complete unbroken head, the wordmark printed exactly once.

### W2 (controls) keeps its explicit pose sentence
Still name the pose in words ("We are looking at the driver from BEHIND AND SLIGHTLY ABOVE, seeing
the large domed CROWN... the hosel and shaft leave at the TOP-LEFT") - that is what stopped the
BogeyB mirror. Then add the same "YOU MAY AND SHOULD REDESIGN the silhouette" paragraph.

### W1 (fulls) - the head must match the brand's OWN portrait, not the scene template
For fulls the second image is the brand's finished portrait, so say:
*"the club takes the head design shown in the SECOND image - that exact head shape, not the head
shape from the first image"*, while the photograph, camera angle and framing stay locked to the
first image. Otherwise Driver-Full and Driver-Portrait end up as two different clubs.

### Scope - DECIDED BY CESAR 2026-08-28: FORWARD ONLY
TeePit (DONE 2026-08-28), VBOOOT (DONE 2026-08-28) and PUTT ACE use the new rule. The 16 brands committed
before it stay as they are.

The 9 that were pipeline-generated under the old rule - GOLFINIX, EAGLEZ, FOREFIT, PAR PERFECT,
BogeyB, Fairway THREADS, GREEN SWING, FairX, FAIRLOFT - are logged for a possible future retrofit
in the Notion page **club_art_shape_retrofit**:
https://app.notion.com/p/3cab3e9702b7819685b4ce68872becf5
That page records which template silhouette each sprite inherited and what a retrofit would cost
(~85 generations for portraits only, ~220 for everything).

The other 7 - KLYRO, MireO, FYLOE, GOLFIN, G&F, ROYAL SWING, TIFTO - are ORIGINAL hand-made art and
must never be regenerated; several of them are the templates themselves.

## TeePit - COMPLETE, 13 of 13 new sprites committed (2026-08-28)

**FIRST BRAND BUILT UNDER THE NEW SHAPE RULE.** Putter portrait + putter controls were already in
the repo; generated Driver/Wood/Iron/Wedge portraits + controls and all five fulls. 15/15 pass
`qa.py`. Repo names follow the majority convention, not the hand-made-brand variants:
`Full/{Driver,Wood,Iron,Wedge,Putter}-TeePit.png` (Iron, not Iron7; Wedge, not WedgeA).
Raws: `~/Downloads/golfin_club_gen/tp_*_{portrait,controls,full}_raw.jpg`.

### Brand look string (audited from the two shipped putter sprites) - use verbatim
> TeePit is a MATTE GUNMETAL-GREY / CHARCOAL body with gloss black topline and sole edges, and one
> bold BRIGHT GRASS-GREEN band sweeping across the head. "TeePit" in clean white sans-serif letters
> with a capital T and a capital P. Two short white vertical alignment bars flanking the green, and
> a deep black milled grooved insert plate. A black ferrule, a MATTE BLACK shaft and a matte black
> grip. NO violet, NO purple, NO electric blue, NO crossed flags, NO silver or white cavity panel,
> NO crossed-clubs emblem - the body is gunmetal grey and the only colour is the one bright
> grass-green band.

Flattened references: `/mnt/user-data/outputs/tp_up/tp_ref_portrait.jpg` (792x1233) and
`tp_ref_controls.jpg` (1156x649).

### ✅ THE NEW SHAPE RULE WORKS
Driver, wood, iron and wedge each came back with a genuinely different silhouette from the KLYRO /
MireO / GOLFIN template they were built on - different crown profile, different sole vent, different
toe and heel. Within TeePit the iron and the wedge still resemble each other, which is correct: that
is family resemblance inside one brand, not the cross-brand sameness Cesar objected to.

### ⚠️ `deshadow=True` EATS LIGHT-GREY CROWNS - only use it on dark-bodied clubs
`postprocess.controls(..., deshadow=True)` runs `remove_shadow_ghost`, which floods any pixel with
`min(rgb) >= 125` and low saturation that touches transparency. TeePit's driver is dark enough that
it only removed the drop shadow (384k -> 334k opaque). The FAIRWAY WOOD's crown is light gunmetal,
and deshadow bit a large notch out of it (248k -> 195k opaque, visible bite near the hosel).
**Rule: deshadow only when the head is dark. On any light, silver, chrome or gunmetal crown, run
with `deshadow=False` and accept the soft shadow, or the crown loses a chunk.** Sanity check: a
sudden drop in opaque count between deshadow on/off means it ate body, not shadow.

### ⚠️ NEVER CLICK SEND TWICE - THE SEND BUTTON BECOMES STOP
While a response is generating, the "Send message" button turns into a stop button but KEEPS THE
SAME accessibility name and ref. Clicking it again to "make sure it sent" cancels the generation
("You stopped this response"). Cost me two wasted generations (driver full, putter full).
**Click Send exactly once, then poll with screenshots.** If you did stop it, `find` the "Redo"
button and click that - it re-runs the same prompt with the same attachments.

### ⚠️ THE ATTACHMENTS ARE OFTEN THERE WHEN THE COMPOSER LOOKS EMPTY
After typing a long prompt the composer scrolls to the bottom and the thumbnail row scrolls out of
view. Twice I concluded the attachments had been dropped and re-uploaded, and Gemini answered *"You
already uploaded a file named Driver-GandF.jpg"*. **Scroll the composer UP before deciding the
attachments are missing.** Thumbnails can also take ~10s to render as grey placeholders first.

### ⚠️ GEMINI WILL NOT FIX AN UPSIDE-DOWN WORDMARK - FIX IT LOCALLY
On the iron full and the wedge full, Gemini painted "TeePit" rotated 180 degrees on the head. Three
increasingly explicit in-chat corrections ("rotate the lettering by 180 degrees", "the capital T on
the LEFT") all came back still inverted. Do not keep paying for retries.
**Use `/root/fliptext.py` `flip_wordmark(path, box, fill_thresh, glyph_thresh)`.** It masks the
glyphs by luminance, inpaints them away by iterated blur so the panel gradient is preserved, rotates
only the glyph layer 180 degrees, and composites it back. Boxes used:
- `Iron-TeePit.png` `(412, 828, 478, 851)` at the default thresholds (dark panel, median lum 76)
- `Wedge-TeePit.png` `(413, 779, 464, 797)` with `fill_thresh=185, glyph_thresh=195`
  (lighter panel, median lum 132 - the defaults grab the panel itself and leave a grey rectangle)
**Pick the box so it contains ONLY the lettering and flat body around it** - if it clips the green
band, the bright sole edge or the topline, the inpaint smears those into a visible rectangle. Check
the patch's luminance percentiles first and set the thresholds above the panel, below the glyphs.

### W1 invents nothing this time - the "do NOT add anything" list works
Listing the furniture explicitly (*"no hooks, no brackets, no signs, no extra clubs, no golf balls,
no bag"*) kept all five scenes clean. Keep that sentence in every W1 prompt.

### Gemini repainted the REFERENCE instead of the TEMPLATE (wedge controls)
For the wedge controls it ignored the GOLFIN wedge template entirely and repainted the TeePit
*putter* reference, producing a mallet. One in-chat correction fixed it: name the first image
concretely (*"the black and green GOLF WEDGE with the shaft going up to the left"*), say
*"use the SECOND image ONLY as the colour and branding reference, never as the shape"*, and name the
wrong output (*"not a putter, and never a mallet"*).

## VBOOOT - COMPLETE, 13 of 13 new sprites committed (2026-08-28)

Putter portrait + putter controls were already in the repo; generated Driver/Wood/Iron/Wedge
portraits + controls and all five fulls. 13/13 pass `qa.py`. Repo names:
`Full/{Driver,Wood,Iron,Wedge,Putter}-VBOOOT.png`.
Raws: `~/Downloads/golfin_club_gen/vb_*_{portrait,controls,full}_raw.jpg`.

The identity sheet's VBOOOT row was already correct against the shipped art - no ART-WINS rewrite
needed. Only nuance: the shipped putter is a BLADE, not a mallet, and the finish is satin rather
than gloss. Both sprites agree on black + gold, so there is no GREEN SWING-style contradiction.

### Brand look string (audited from the two shipped putter sprites) - use verbatim
> VBOOOT is a SATIN BLACK / near-black body with gloss black edges and BRIGHT METALLIC GOLD accents.
> One gold insert panel or gold sole flash set into the head, and a short gold ladder of stripes.
> The wordmark reads VBOOOOT - V, B, then FOUR letter O's, then T - in bold GOLD block capitals,
> printed exactly once, reading normally left to right, with the small gold tagline
> "EAGLE, BIRDIE, SUCCESS" in tiny gold capitals beside it. Round black weight ports with fine
> concentric rings. A black ferrule, a MATTE BLACK shaft and a matte black grip. NO silver or chrome
> body, NO white cavity panel, NO blue, NO green, NO purple, NO red, NO neon - the body is black and
> the ONLY accent colour is metallic gold.

Flattened references: `/mnt/user-data/outputs/vb_up/vb_ref_portrait.jpg` (792x1233) and
`vb_ref_controls.jpg` (1156x649).

### ⚠️⚠️ GEMINI CANNOT SPELL "VBOOOT" - ASK FOR **FOUR** O's
Gemini drops the third O and writes VBOOT. Four escalating in-chat corrections all failed:
naming the misspelling, spelling it letter by letter, "three identical circular O's", and switching
to wide letter spacing (which gave five widely-spaced glyphs, still two O's).

**The fix that works: write VBOOOOT with FOUR O's in the prompt. It then draws three.**
This is the standing rule for this brand - it is baked into the look string above.

It is not deterministic. Observed over 13 sprites:
- correct VBOOOT first try: iron portrait, driver + wood controls, most fulls
- one O short (VBOOT): needs a re-roll, not a correction
- one O too many (VBOOOOT): iron controls, twice - a fresh chat with the same prompt fixed it
- a malformed glyph (VBOOrT): one targeted in-chat correction fixed the glyph but then reverted the
  count, so **do not chain corrections** - re-roll instead

**Re-roll, do not correct.** Corrections on the letter count reliably make it worse. Use "Try again"
under the image, or start a fresh chat with the same prompt. Two rolls usually lands it.

### Upside-down wordmarks were the norm on the fulls - fix them locally
Four of the five fulls (driver, iron, wedge, putter) and the wedge controls came back with the
wordmark rotated 180 degrees. As with TeePit, Gemini will not fix this on request.
Use `/root/fliptext.py` `flip_wordmark(path, box, fill_thresh, glyph_thresh)`. Boxes and thresholds
used - the thresholds must sit ABOVE the panel and BELOW the gold glyphs, so check the patch's
luminance percentiles first (`np.percentile(lum,[50,70,80,90,95])`):
- `S_Controls_Wedge_VBOOOT.png` `(646,296,894,343)` fill 85 (dilate 7x7, 60 blur passes) / glyph 125
- `Driver-VBOOOT.png` `(424,801,489,836)` fill 100 / glyph 110
- `Iron-VBOOOT.png` `(398,792,480,822)` fill 95 / glyph 105
- `Wedge-VBOOOT.png` `(415,783,494,806)` fill 105 / glyph 118
- `Putter-VBOOOT.png` `(333,804,407,830)` fill 120 / glyph 132
Keep the box off the green/gold band, the bright sole edge, the topline and any background that
intrudes - anything else inside it gets smeared into a visible rectangle.

**`Wood-VBOOOT.png` was left alone**: its wordmark runs along the crown axis rather than inverted,
which is how a real fairway wood is branded. Not a defect.

### Stray second-club fragment at the frame edge (iron controls)
One controls generation painted a sliver of a second club at the extreme right edge, which `qa.py`
caught as "2 SHAFTS crossing frame". Fix in post - keep only the largest connected alpha component:
```python
lbl, n = ndimage.label(a[...,3] > 128)
keep = int(np.argmax(ndimage.sum(m, lbl, range(1, n+1)))) + 1
a[...,3] = np.where(lbl == keep, a[...,3], 0)
```

### ⚠️ THE RENDERER FREEZES AFTER A LONG SESSION - OPEN A FRESH TAB
Deep into the run, `Page.captureScreenshot` started timing out after 30s on every call while `find`
still worked. The tab was unrecoverable. **Fix: `tabs_create_mcp`, then `tabs_close_mcp` the old
tab.** Closing the group's last tab drops the group, so the next `tabs_context_mcp` needs
`createIfEmpty: true` and returns a NEW tab id - re-read it before the next action.

## Committed and verified clean (18 brands)

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

## BogeyB - COMPLETE, 13 of 13 committed (2026-08-21)

Committed: portraits Driver/Wood/Wedge/Putter, controls Driver/Wood/Wedge/Putter. Iron portrait and
iron controls were already shipped. All 8 pass qa.py at thresh=235, no seal needed - the olive-gold
shaft is dark enough that the flood fill never eats it. Plus 3 of 5 full scenes:
`Full/{Driver,Wood,Iron}-BogeyB.png`.

All 5 full scenes done: `Full/{Driver,Wood,Iron,Wedge,Putter}-BogeyB.png`, 537x900 rounded.

### Gemini has a DAILY IMAGE QUOTA
Hit it mid-brand: *"I can't create more images for you today."* Not a bug and nothing to work
around - it resets on Google's clock (next day worked). Bank and commit whatever is finished before
stopping, and write down exactly which items are owed plus which template pairs with which raw, so
the resume is mechanical.

Note: naming the grip explicitly ("matte black grip. NO purple anywhere - the grip must be matte
black, not purple") stopped the Fyloe/GolfinX purple-grip bleed on the FIRST try for both the wedge
and the putter. Worth keeping in every W1 prompt that uses those two templates.

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

## Fairway THREADS - COMPLETE (13 of 13, 2026-08-22)

Committed: 5 portraits (the iron was already shipped), 5 controls, 5 fulls
(`Full/{Driver,Wood,Iron,Wedge,Putter}-Fairway.png`). All pass qa.py and were eyeballed against
their templates for pose before committing.
The putter portrait needed the bbox-width fit (scale bbox width -> 253, centre), same as PAR PERFECT
and BogeyB - maxrow came out 222 against the shipped norm of 224.

### IDENTITY SHEET WAS WRONG FOR THIS BRAND - corrected from the art
The sheet said "polished chrome head with a WHITE PANEL". The shipped iron has NO panel: the entire
head is polished mirror chrome with no colour anywhere. "Fairway" in a black italic serif script
with "THREADS" in small black capitals tucked beneath it, a small circled-G emblem at the toe and
again on the sole, chrome shaft with a black ferrule band. Sheet row rewritten (ART WINS).

Working prompt clause: *"the ENTIRE head is polished mirror chrome with no colour anywhere - no
white panel, no insert, no accent stripe."* Every one of the 13 landed first try.

### No brand-portrait raw for the iron - flatten the shipped sprite instead
There is no `ft_iron_portrait_raw.jpg` (the iron portrait was already in the repo). For the iron
full, flatten `S_Menu_Iron_FAIRWAY.png` onto white and upscale 3x, then use that as the second
image. Do NOT substitute another brand's iron.

```python
im = Image.open('.../S_Menu_Iron_FAIRWAY.png').convert('RGBA')
bg = Image.new('RGB', im.size, (255,255,255)); bg.paste(im, (0,0), im)
bg.resize((im.width*3, im.height*3), Image.LANCZOS).save('ft_iron_portrait_ref.jpg', quality=95)
```

### ❌ CORRECTION: THE GEMINI "+" MENU WAS NEVER WEDGED - I WAS MISSING IT
An earlier version of this file said the upload menu wedges and that the fix is to quit and reopen
Chrome. **That was wrong.** Cesar: *"The + sign works perfectly in Chrome."* The real cause, found
with `javascript_tool`:

- The "Upload & tools" button is only **32x32 px**, and its rect **moves horizontally** as the
  composer resizes (CSS x=632 in one state, x=750 in another).
- A hardcoded click at (652, 370) therefore lands on it sometimes and misses entirely other times.
- A miss looks exactly like a wedge: `aria-expanded` stays false, no overlay, no file input.

**THE FIX: compute the click point from the DOM every single time. Never hardcode it.**
```js
const b=[...document.querySelectorAll('button')].find(x=>x.getAttribute('aria-label')==='Upload & tools');
const r=b.getBoundingClientRect(); const s=1568/window.innerWidth;
JSON.stringify({x:Math.round((r.x+r.width/2)*s), y:Math.round((r.y+r.height/2)*s)})
```
Click that point, then wait ~2s for the overlay to mount and verify before calling `find`:
```js
await new Promise(r=>setTimeout(r,2200)); document.querySelectorAll('input[type=file]').length  // expect 2
```
Then `find` -> `file_upload` on the FIRST ref in the very next call (the menu closes on its own).
Do the same DOM lookup for Send (`aria-label="Send message"`) rather than trusting (1169, 509).

Do NOT restart Chrome, do NOT recreate the tab, and do NOT hand this back to Cesar. With the
DOM-computed coordinate the menu opened first try on every one of the 5 Fairway THREADS fulls.

Also: synthetic `click()` / dispatched MouseEvents do not work (Angular Material ignores untrusted
events), and neither does focusing the button and pressing Return or Space. Use a real click at the
measured coordinate.

### Composer click point
After the images attach, clicking (678, 402) hits a **thumbnail** (you get a filename tooltip)
instead of the text field. Click **(950, 402)**, type "XX", screenshot to confirm the caret is in
the composer, then type the real prompt.

## GREEN SWING - COMPLETE (13 of 13, 2026-08-22)

Committed: 4 portraits + 4 controls (the iron of each was already shipped) and 5 fulls
(`Full/{Driver,Wood,Iron,Wedge,Putter}-GreenSwing.png`). All pass qa.py; every pose was eyeballed
against its template before committing. Controls post-processed at `thresh=235` (a black-bodied
brand - no chrome-shaft split risk; 235 and 250 differed by <2% opaque pixels).

### ⚠️ THE SHIPPED ART CONTRADICTS ITSELF - portrait is SILVER, controls are BLACK
This brand shipped with only an iron, and its two sprites disagree on the body colour:

- `Portraits/S_Menu_Iron_GREENSWING.png` - **satin silver / polished chrome body with a large WHITE
  cavity panel.**
- `Controls/S_Controls_Iron_GREENSWING.png` - **gloss BLACK body.**

This is not a rendering artefact of the controls camera angle: G&F's iron pair was checked as a
control and its portrait and controls are both chrome/white, so a light body does render light in
the controls view.

**Resolution applied (ART WINS, per sprite kind):** generate each sprite kind to match its own
shipped counterpart. Portraits and fulls follow the SILVER portrait; controls follow the BLACK
controls. Each folder then stays internally consistent, which is what the game actually renders.
Same precedent as the mirrored sole wordmark on iron/wedge fulls.
**Flag for Cesar:** if he wants one body colour for the whole brand, the portraits or the controls
need regenerating - say which and it's 4 sprites either way.

### Everything the two shipped sprites DO agree on (put all of this in every prompt)
- A solid bright grass-green rectangular bar sitting in the cavity slot
- A thin bright grass-green outline stripe framing the cavity panel
- "GREEN" in light grey (portrait) / white (controls) capitals, "SWING" in bright grass-green
- A small **crossed-golf-clubs** emblem (dark on silver, white on black)
- **A MATTE BLACK shaft** - the sheet said "chrome shaft" and was wrong. Chrome hosel, black
  ferrule band, matte black grip.

Exclusions that earned their place: `NO lime green` (GOLFIN), `NO yellow-green`, and per template
`NO blue / NO crossed flags` (KLYRO), `NO gold / NO amber / NO sakura` (MireO),
`NO purple / NO flame graphics` (FYLOE), `NO crimson` (G&F).

### The MireO wedge template carries its gloss-black body into a silver brand
The wedge portrait came back with a BLACK head from the `S_Menu_Wedge_MIREO` template even though
the prompt asked for satin silver. One in-chat correction cleared it cleanly and did NOT disturb
the pose:

> Same image, one single change: recolour the black body of the wedge head to SATIN SILVER /
> POLISHED CHROME. The whole head becomes bright satin silver metal instead of black - the topline,
> the toe, the sole and the surround around the white panel. Everything else stays exactly as it is.

### In-chat emblem DE-duplication works (unlike logo removal)
The wedge controls printed the crossed-clubs emblem twice. Naming which copy to delete and what to
fill the space with fixed it in one shot with the pose intact. Note this is the opposite of the
FOREFIT Nike-mark case, where in-chat REMOVAL failed - deleting a duplicate of the brand's own mark
is fine; removing a summoned real-world trademark is not.

## FairX - COMPLETE (13 of 13, 2026-08-28)

Committed: 4 portraits + 4 controls (the putter of each was already shipped) and 5 fulls
(`Full/{Driver,Wood,Iron,Wedge,Putter}-FairX.png`). All pass qa.py; poses eyeballed against
templates before committing. Controls post-processed at `thresh=235`.

### IDENTITY SHEET WAS WRONG ON THE SHAFT - corrected from the art
Both shipped sprites (putter portrait + putter controls) agree, so no contradiction here:
gloss BLACK body, a WHITE rectangular insert panel, "FairX" in a white italic script printed on
the BLACK part (never inside the panel), two thin white pinstripe curves sweeping along the topline
above and below the wordmark, and small brushed-silver weight bars on the sole.
**The shaft is POLISHED CHROME SILVER, not black** - the sheet said "black shaft" and was wrong.
Black grip, black ferrule, chrome hosel.

### ⚠️ FairX's CHROME SHAFT SPLITS ON ALMOST EVERY SPRITE - seal.py is routine here
`remove_white_bg` cuts a slot up the near-white specular highlight of the chrome shaft. Affected the
driver portrait and the driver, wood AND iron controls - 4 of 8 sprites. `seal.seal(path, radius=4)`
cleared every one with no visible change and no loss of opaque area (Driver controls 291,856 ->
292,658). Run qa.py, then seal any FAIL reporting "2 SHAFTS crossing frame", then re-run qa.py.
Do NOT raise the threshold to 250 for this brand - 235 + seal is cleaner.

### The wordmark drifts INTO the white panel - say where it goes
The wood portrait came back with "FairX" set inside the white panel instead of on the black body.
One in-chat correction fixed it (move the script off the panel, shrink the panel back to a plain
empty white rectangle) and the pose survived. Every later prompt carries the clause
*"printed directly on the BLACK part of the head and NOT inside the white panel"* and it did not
recur.

### The GOLFIN wedge controls template leaves "GOLFIN" on the sole
The wedge controls came back still reading GOLFIN (mirrored) on the sole plus an invented round dot
in the cavity. One in-chat correction naming both fixed them together. Worth checking the sole text
on every W2 sprite built from a GOLFIN template.

### W1 can invent scenery furniture - check the WALL, not just the club
The putter full came back with a **metal club hook bolted to the stucco wall** that is in none of the
other fulls. "change ONLY the club... absolutely nothing else in the image" does not reliably stop
this. One in-chat correction removed it cleanly. Add the wall/background to the eyeball check on
every full before committing.

### ✅ BETTER THAN DOM-COMPUTED COORDINATES: click by element `ref`
Correcting the earlier note again. Computing the "+" coordinate from `getBoundingClientRect()` still
missed after Chrome was reopened at a different window size - the click tool applies its own scaling,
so a coordinate computed in page space can land ~12% off. **`find` the element, then
`computer{action:"left_click", ref:"ref_NNN"}`** - no arithmetic, works at any window size, first
try every time. Use it for the "+" (`Upload & tools`), the composer textbox, Send, and the
`Download full size image` button.
Caveat on the download button: after an in-chat correction there are TWO download buttons. Refs are
assigned in the order `find` first saw them, so the LOWER ref number is the OLDER image. Confirm
with:
```js
[...document.querySelectorAll('button')].filter(b=>/download full size/i.test(b.getAttribute('aria-label')||''))
  .map(b=>Math.round(b.getBoundingClientRect().top+window.scrollY))
```
The largest `top` is the newest image; click the higher-numbered ref.

## FAIRLOFT - COMPLETE (13 of 13, 2026-08-28)

Committed: 4 portraits + 4 controls (the putter of each was already shipped) and 5 fulls
(`Full/{Driver,Wood,Iron,Wedge,Putter}-Fairloft.png`). All pass qa.py; controls at `thresh=235`
with **no seal needed** - the shaft is black, so there is no chrome-highlight split on this brand.

### ✅ FIRST BRAND WHERE THE IDENTITY SHEET WAS ALREADY RIGHT
Both shipped sprites agree and both matched the sheet: solid deep teal / petrol-blue, "FAIRLOFT" in
white block capitals with a small "JAPAN" beneath, black shaft, calm and matte, not sky blue.
Detail added from the art (not corrections, just precision):
- The teal carries a **very fine cross-hatch woven texture**, not flat paint.
- The wordmark sits on a **matte BLACK rectangular insert panel**, not directly on the teal.
- **Two short white vertical alignment ticks** flank that panel.
- A **darker near-black teal band** runs along the sole.

Working look string (used verbatim on all 13):
> a solid MATTE DEEP TEAL / PETROL-BLUE body with a very fine cross-hatch woven texture, a darker
> near-black teal band along the sole, and a matte BLACK rectangular insert panel carrying
> "FAIRLOFT" in white block capitals with a small "JAPAN" in white beneath it. Two short white
> vertical alignment ticks flanking the black insert panel. A black ferrule, a MATTE BLACK shaft
> and a matte black grip. NO electric blue, NO sky blue, NO cyan - the blue is a deep muted petrol
> teal, calm and matte, never bright.

### The teal creeps up the shaft on wedges
The wedge portrait came back with a TEAL hosel and upper shaft. One in-chat correction
("recolour them MATTE BLACK so the entire shaft from the ferrule all the way up is matte black,
with only the head itself remaining teal") fixed it with the pose intact. A teal hosel that stops at
the ferrule is correct and needs no fix - only teal running past the ferrule is a defect.

### A GOLFIN-template sole becomes a SECOND wordmark, not a blank
On the iron controls, "GOLFIN" on the sole was replaced by a second mirrored "FAIRLOFT" rather than
deleted, even with `wordmark printed exactly once` in the prompt. Two lessons:
1. The wording that works is explicit about placement:
   *"the FAIRLOFT wordmark printed exactly ONCE, inside the black insert panel only"* plus
   *"the sole must carry NO other lettering - delete any GOLFIN text and do NOT repeat FAIRLOFT
   anywhere else on the club."* With that clause the wedge controls came back clean first try.
2. When it still doubles, one in-chat correction naming WHICH copy to delete fixes it.

### Gemini dropped the connection mid-batch
On the wedge full the sidebar showed **"Couldn't connect / Reload"** and the attached images were
silently dropped while the typed prompt stayed in the composer. Not a wedge, not a quota - just a
network blip. Reload the page and redo the upload; the retry worked immediately. Also worth noting:
after a reload, `file_upload` can fail once with *"Couldn't determine which page this action
targets"* - call `tabs_context_mcp`, then re-`find` and retry.

## Then, in order (3 brands, ~41 generations)

~~EAGLEZ~~ → ~~FOREFIT~~ → ~~PAR PERFECT~~ → ~~BogeyB~~ → ~~Fairway THREADS~~ → ~~GREEN SWING~~ → ~~FairX~~ → ~~FAIRLOFT~~ → **TeePit (next)** →
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
- GREEN SWING = silver/white portraits + GLOSS BLACK controls, bright grass green, BLACK shaft
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
