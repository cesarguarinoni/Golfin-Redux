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

## Then, in order (10 brands, ~92 generations)

EAGLEZ → FOREFIT → PAR PERFECT → BogeyB → Fairway THREADS → GREEN SWING → FairX → FAIRLOFT →
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
