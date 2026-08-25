# SPEC/RUNBOOK — `club_art_batches` (11 remaining brands, nano banana pipeline)

> Self-contained: a fresh session can execute this with no other context. Runner must be a
> **Cowork/Architect session** (needs Cesar's Chrome via claude-in-chrome + device folder grants);
> Claude Code cannot drive Chrome. CSV wiring afterwards is a separate task (`C2` in
> claude/EXECUTION_PLAN_TOURN_ECON_CLUBS.md, project docs).

## Goal

Every brand gets 5 head designs (Driver, Wood, Iron, Wedge — ONE wedge shared by P.W/A.W/S.W —
and Putter), each in 3 sprite kinds. 19 brands × 5 = 95 designs. **KLYRO (pilot), MireO, FYLOE, GOLFIN, G&F, ROYAL SWING and TIFTO are complete.**
Art is shared across a club's 6 rarity variants (rarity shows via RarityHelper UI framing).

## Decisions of record (Cesar, 2026-08-19)

- Type-matched templates ALWAYS — small angle differences per club type matter.
- One wedge design per brand. Brand look/stat-bias table: `claude/CLUB_BRAND_IDENTITY_SHEET.md`
  (project) — APPROVED; use its "Look / description tone" column in prompts.
- Never leave Chrome Save dialogs stacked. macOS keystroke-injection (osascript System Events
  key code) is AUTO-BLOCKED by the permission layer — do not attempt. As of 2026-08-20 Chrome's
  "ask where to save" is OFF, so no Save dialogs appear at all (mechanics item 6).

## Sprite targets

| Kind | Size | Naming | Repo folder (GolfinRedux/Assets/Resources/Clubs/) |
|---|---|---|---|
| Portrait | 264×411 RGBA transparent | `S_Menu_<Type>_<BRAND>.png` (BRAND caps, e.g. MIREO) | `Portraits/` |
| Controls | 1156×649 RGBA transparent | `S_Controls_<Type>_<BRAND>.png` | `Controls/` |
| Full | 537×900, 30px rounded corners, scene kept | `<Type>-<Brand>.png` (e.g. `Wood-Klyro.png`) | `Full/` |

Type token in sprite names: Driver, Wood, Iron, Wedge, Putter.

## Coverage matrix — what EXISTS (✓ = portrait+controls pair; F = Full scene)

| Brand | Driver | Wood | Iron | Wedge | Putter |
|---|---|---|---|---|---|
| KLYRO | ✓F | ✓F | ✓F | ✓F | ✓F (COMPLETE) |
| G&F | ✓F | ✓F | ✓F | ✓F | ✓F (COMPLETE) |
| GOLFIN | ✓F | ✓F | ✓F | ✓F | ✓F (COMPLETE) |
| MireO | ✓F | ✓F | ✓F (Iron7) | ✓F | ✓F (COMPLETE) |
| FYLOE | ✓F | ✓F | ✓F | ✓F (WedgeA) | ✓F (COMPLETE) |
| ROYAL SWING | ✓F | ✓F | ✓F | ✓F (WedgeP) | ✓F (COMPLETE) |
| EAGLEZ | ✓ | ✓ | ✓ | ✓ | ✓ (Eaglez) |  <!-- completed 2026-08-20 -->
| FOREFIT | ✓ | – | – | – | – |
| PAR PERFECT | ✓ | – | – | – | – |
| BogeyB | – | – | ✓ | – | – |
| Fairway THREADS (art tag FAIRWAY) | – | – | ✓ | – | – |
| GREEN SWING (GREENSWING) | – | – | ✓ | – | – |
| FairX (FAIRX) | – | – | – | – | ✓ |
| FAIRLOFT | – | – | – | – | ✓ |
| GOLFINIX | ✓ | ✓ | ✓ | ✓ | ✓ (GolfinX) |  <!-- completed 2026-08-20, browser pipeline -->
| TeePit WNDRWLL (TEEPIT) | – | – | – | – | ✓ |
| VBOOOT | – | – | – | – | ✓ |
| TIFTO | ✓F | ✓F | ✓F | ✓F | ✓F (COMPLETE) |
| PUTT ACE | – | – | – | – | – (from scratch) |

Remaining ≈ 45 portrait/controls pairs + 59 Full scenes. Batch brand-by-brand, most-covered
first: GOLFINIX → single-design brands → PUTT ACE last.

## Template registry (FIRST image in every swap)

- **Full scenes** (club leaning on white wall, course behind): Driver → `Full/Driver-G&F.png`;
  Wood → `Full/Wood-Klyro.png`; Iron → `Full/Iron7-Mireo.png`; Wedge → `Full/WedgeA-Fyloe.png`;
  Putter → `Full/Putter-GolfinX.png`.

  ⚠️ **CHANGED 2026-08-24.** Five `Full/` sprites shipped at 2148×3600 — 4× the 537×900 spec
  above — including the WEDGE and PUTTER templates. `Resources/Clubs/Full/` is now
  spec-conformant (all 71 at 537×900); the high-resolution originals were preserved and moved to
  **`Assets/Art/Clubs/Full_Masters~/`** (trailing `~` = Unity ignores the folder, so they no
  longer ship in the build). Use the MASTERS folder as the W1 template source — a 537×900
  template gives the model less to work with. `Full_Masters~/Putter-GolfinX.png` is 8.8MB —
  compress to JPEG q92 before upload (10MB total cap per file_upload). Rationale and the
  pixel-diff proof: `Docs/CONTENT_PIPELINE_PLAN.md` §11.
- **Controls** (large side view, transparent): `Controls/S_Controls_<Type>_GOLFIN.png` for
  Driver/Iron/Putter/Wedge/Wood — GOLFIN has all... verify per type with `ls`; any same-type
  `S_Controls_*` works.
- **Portrait composition refs**: any same-type `S_Menu_*` from another brand.

## The three workflows (prompts of record — Cesar's, verbatim)

All in Cesar's Chrome at gemini.google.com (his Work Pro account), model **Flash**, NEW chat per
image. Upload order matters: template FIRST, target club art SECOND.

**W1 — Full scene** (needs the brand's portrait or a render as second image):
> Give me the first image with the club on the second image: (change ONLY the club including both
> head and grip, but absolutely nothing else in the image)

If the result keeps the template's shaft/grip (happens when the second image shows no grip),
follow up in-chat:
> Almost. Now also change the shaft and grip to match the <BRAND> brand: <look from the identity
> sheet>. Change absolutely nothing else in the image.

Or bake it into the first prompt: "(change ONLY the club including both head, shaft and grip —
all in the <BRAND> brand style of the second image: <colors>. But absolutely nothing else in the
image changes.)"

**W2 — Controls** (second image = the brand's driver/type render or portrait):
> Give me the first image with the club on the second image: (change ONLY the club, keeping the
> first image's exact camera angle, framing, transparent background and composition — a large side
> view of the club head. Absolutely nothing else changes.)

If it renders a literal checkerboard as "transparency", follow up: "Same image exactly, but on a
plain solid white background instead of the checkerboard pattern. Change nothing else."

**W3 — Portrait for a brand×type with NO art** (style transfer; first image = same-type portrait
from another brand, second = any same-brand art):
> Create the <BRAND> <TYPE> for a fictional mobile golf game brand. First image = composition and
> camera-angle reference (match that exact framing and angle, head with part of the shaft,
> portrait orientation). Second image = the <BRAND> design language: <look column from the
> identity sheet, incl. logo/emblem notes>. Photorealistic product render, no background, no
> shadow, no watermark, no text other than <BRAND> branding.

Per-design order when a brand×type has nothing: W3 portrait first → its render feeds W2 and W1.

## Mechanics (hard-won, do not rediscover)

0. ⚠️ UPSIDE-DOWN WORDMARK (hit 3× on GOLFINIX W2/W1). The cavity-badge brand text often renders
   rotated 180°. Zoom the badge on EVERY generated controls/full before downloading. The weak fix
   ("rotate the text") fails; the proven in-chat correction is:
   "ERASE the text from the badge completely, then print \"<BRAND>\" on the badge so it reads
   normally for a viewer of THIS image: first letter on the left, last letter on the right,
   letters upright. Change absolutely nothing else."
   Also: strip stray floating components (Gemini leaves detached accent squiggles) by keeping only
   the largest alpha component after background removal.

1. Folder grants needed: `/Users/cesar/Documents/GolfinRedux` + `/Users/cesar/Downloads`.
2. Upload into Gemini: click the "+ Upload & tools" button, then `find` "hidden input type=file
   under the Upload files menu item" and `file_upload` BOTH files in one call (order = template,
   target). Staged repo files live under `/mnt/user-data/uploads/...` after `device_stage_files`.
3. Generation takes 60–120s. The composer shows a stop square while streaming — do NOT click it
   (a stopped response reads "You stopped this response"; the image usually survives but don't
   risk it). Wait, screenshot, scroll to verify the result.
4. Download: hover the image → download icon top-right → click, then rename via MacOS-MCP Shell.
   ⚠️ SUPERSEDED BY ITEM 6 — "ask where to save" is now OFF, so files land as
   `~/Downloads/Gemini_Generated_Image_*.jpeg` with no Save sheet. The old temp-file dance
   (`~/Downloads/.com.google.Chrome.<rand>`, md5-verify against stale temps) is only needed if
   someone re-enables the setting. Either way, confirm the file actually appeared before moving on —
   the download fires ~3-5s after the click.
5. Raw sizes ~1500×2700 (portrait-ish) / 2736×1536 (controls). Full-scene raws are already the
   537:900 aspect — plain resize, NO background removal (the scene IS the art) + 30px rounded
   corner alpha. Portraits/controls need white-bg removal.
6. Turn OFF Chrome's "Ask where to save each file" (chrome://settings/downloads) BEFORE starting.
   Downloads then land as `~/Downloads/Gemini_Generated_Image_*.jpeg` and rename in one shell call:
   `f=$(ls -t ~/Downloads/Gemini_Generated_Image_* | head -1); mv "$f" ~/Downloads/golfin_club_gen/<name>_raw.jpg`
   No Save sheets, no temp-file forensics, no stale-temp risk. (Cesar turned this off 2026-08-20.)
7. After each generation the Gemini composer sticks in a stale "Stop response" state and SILENTLY
   swallows the next message — Enter and the send button both do nothing. Reload the chat URL to
   clear it, then retype. Never click the stop square itself.
8. The send button MOVES DOWN when attachment thumbnails finish rendering (~8s after file_upload).
   Screenshot and re-locate it before clicking, or the click lands on empty space and the message
   sits unsent. Prefer `find` → click by ref, and verify with a screenshot that the turn posted.
9. W3 style-transfer inherits the reference brand's signature crown/sole graphic (the KLYRO X landed
   on the MireO driver). Put "Do NOT copy the first image's surface graphics, emblems or logo —
   those belong to a different brand" in the prompt, or expect one follow-up round.
10. W2 on PUTTERS ignores the template framing and returns a 3/4 top view on a literal checkerboard.
   One follow-up naming BOTH fixes at once resolves it: "plain solid WHITE background, not the
   checkerboard" + "straight-on front view at eye level, head spanning almost the full frame width,
   shaft rising vertically out of the top of the head".
11. FAILURE MODES — do not wait these out blind:
   - A generation still showing "Creating your image" past ~3 minutes has FAILED. The chat can
     vanish from Recents entirely on reload (happened to the FYLOE iron controls, 2026-08-20).
     Abandon it, start a NEW chat and re-send. Do not keep waiting.
   - Gemini sometimes replies with TEXT ONLY ("I have fixed the text...") and no image. The prior
     image is still in the chat — scroll up and download that instead of re-prompting into a loop.
   - Two slow/failed generations back-to-back means the account is degraded or rate-limited.
     Stop, bank what you have, tell Cesar, and resume later rather than burning the session.
12. ⚠️ REAL TRADEMARK IN THE PUTTER TEMPLATE. `Full/Putter-GolfinX.png` has a real grip-maker's
   name ("GOLF PR...") moulded into the green band at the top of the grip. W1 preserves it whenever
   the model keeps the template's grip. ALWAYS zoom the grip cap of a generated putter full scene
   before accepting it; if the text survived, one follow-up fixes it: "there is leftover text on
   the green band at the top of the grip that reads GOLF PR... - remove it completely or replace it
   with <BRAND> in the same small lettering. Everything else stays exactly as it is."
   The shipped `Putter-GolfinX.png` itself still carries the mark — worth a separate cleanup task.
13. DOWNLOAD GRAB IS NOT SAFE ON ITS OWN. If a download-icon click appears to do nothing and you
   click again, TWO files land; `ls -t Gemini_Generated_Image_* | head -1` then picks up whichever
   is newest and a leftover duplicate stays behind to poison the NEXT grab. It happened on
   2026-08-20: `fyloe_putter_fullscene_raw.jpg` came out byte-identical to the iron portrait.
   After every move, check `ls ~/Downloads/Gemini_Generated_Image_*` is empty and confirm the new
   file's size/md5 differs from the previous one:
   `md5 -q ~/Downloads/golfin_club_gen/<brand>_*.jpg | sort | uniq -d`  (must print nothing)
14. BRAND-COLOUR BLEED FROM THE TEMPLATE. W1/W2 sometimes carries an accent colour off the template
   brand onto the new head — GOLFIN's lime-green sole marking landed on the G&F iron controls as a
   bright green wedge shape (2026-08-20). Name the exclusion in the prompt ("NO GREEN ANYWHERE —
   <BRAND> uses only <palette>") for every sprite of that brand once you've seen it once, and verify
   programmatically before delivery — count saturated pixels of the offending hue:
   `a=np.array(im); mask=(a[...,1]>140)&(a[...,1].astype(int)-a[...,0]>50)&(a[...,1].astype(int)-a[...,2]>50); mask.sum()`
   Should be ~0. Zooming the sole/heel of the sprite is the visual equivalent.
15. ⚠️ TWO-SHAFT / ANATOMY DEFECTS - THE ONE THAT SHIPPED BAD ART TWICE.
   **`qa_sprites.py` in this folder now catches this automatically. RUN IT ON EVERY SPRITE BEFORE
   DELIVERY: `python3 qa_sprites.py <outdir>`.** It counts narrow limbs crossing the frame edge;
   more than one means the club has two shafts. It also checks size, corner alpha and duplicates.
   The defects seen so far:
   - a driver/iron CONTROLS sprite with a shaft at BOTH ends of the head (TIFTO driver + iron) -
     the model invents a second hosel at the toe even though the GOLFIN template has only one
   - TWO parallel shafts out of one hosel (ROYAL SWING iron portrait)
   - a looping / doubled-back hosel (ROYAL SWING + TIFTO putters)
   - the wordmark PRINTED TWICE on one head (TIFTO wedge, ROYAL SWING putter)
   Bad portraits propagate into W2 and W1, so fix the portrait first, then re-derive.
   Put this in every controls prompt: "copy the first image's shaft layout exactly - ONE shaft,
   leaving the head at the HEEL in the upper LEFT only; nothing may come out of the bottom, the
   right side or the toe. Exactly one shaft in the whole picture."
   And in every portrait prompt: "exactly ONE shaft, one hosel, one ferrule ring; print the
   wordmark exactly once."
   A thumbnail-sized contact sheet does NOT show this - Cesar caught both rounds after delivery.
   Check every sprite at FULL RESOLUTION on a magenta background, or just run the script.

15b. (original W3 note)  Style-transfer portraits invent the neck
   and hosel, and Gemini regularly gets it wrong in ways that survive a thumbnail glance:
   - TWO parallel shafts running out of one hosel (ROYAL SWING iron, 2026-08-20)
   - a looping / doubled-back hosel where the shaft folds into itself (ROYAL SWING + TIFTO putters)
   - the wordmark PRINTED TWICE on one head (TIFTO wedge)
   These propagate: a bad portrait feeds W2 and W1, so the controls sprite inherits the same
   broken hosel. Cesar caught them after they were committed. MANDATORY per portrait, before it
   feeds anything else: zoom the hosel/neck region at 4x and count the tubes (must be ONE), and
   count the wordmarks (must be ONE). Put this in the prompt too:
   "CRITICAL ANATOMY: the club must have EXACTLY ONE shaft - a single straight tube rising from a
   single hosel, with exactly one ferrule ring. No duplicated, forked, doubled or looping shafts.
   Print the wordmark exactly once."
   W1 full scenes are NOT affected - they inherit the template's real shaft.
16. ⚠️ TEXT-ONLY REPLIES MEAN YOUR PROMPT NEVER LANDED. If Gemini answers with a prose
   DESCRIPTION of the uploaded images instead of generating, the message was almost certainly sent
   with attachments and an EMPTY prompt. The composer's y-position depends on the window height,
   so a hardcoded click coordinate silently misses the textbox and the `type` goes nowhere.
   THE FIX - always in this order:
     a. `find` the "Enter a prompt for Gemini" textbox and click it BY REF, never by coordinate;
     b. type the prompt FIRST, before attaching anything;
     c. SCREENSHOT and confirm the prompt text is actually visible in the composer;
     d. only then click "+" and `file_upload`, wait for the thumbnails, and send.
   Verify after sending too: a correct turn shows YOUR PROMPT BUBBLE under the thumbnails. If the
   chat shows thumbnails with no prompt bubble, you sent an empty message - start a new chat.
   (Cost ~45 min on 2026-08-20 and was misdiagnosed as a Gemini outage. It was not.)
17. THE "+" MENU TOGGLES. Clicking "Upload & tools" twice opens then CLOSES the menu, and `find`
   then reports no file input. If `find` cannot see the hidden input, click "+" ONCE more and
   `find` again - do not spam clicks, each one flips the state.
18. A BAD REFERENCE PRODUCES A BAD CLUB - CHECK THE TEMPLATE FIRST. The doubled-shaft putters were
   not hallucinations: `Portraits/S_Menu_Putter_KLYRO.png` has a double-bend shaft that reads as
   two parallel tubes, and W3 faithfully copied it. Use `Portraits/S_Menu_Putter_GOLFIN.png` as the
   putter composition reference instead - single clean bend. Before blaming the model for an
   anatomy defect, open the reference at 2x and check the reference itself.
19. A GENERATION THAT LOOKS HUNG MAY HAVE FINISHED. The iron-controls fix showed "Creating your
   image" for 3+ minutes and appeared dead, but the chat later turned up complete in Recents with
   a good image. Before re-spending a generation, check Recents for a new chat title and open it.

## Post-processing (run in the cloud container; PIL+numpy)

```python
# Flood-fill bg removal from borders. thresh=235 for clean white; use 195 when a soft
# shadow splits the background (the KLYRO putter splotch bug). After that, if a gray
# shadow ghost remains, kill gray pixels (channel spread<=30, min>=125) reachable from
# transparent regions (second flood fill). Feather alpha with GaussianBlur(1.0-1.2).
# fit_canvas: crop to bbox, scale into target with 2-4% margin, center on transparent canvas.
# Full scene: resize to 537x900 + ImageDraw.rounded_rectangle mask radius 30.
```
Full working code: this folder's `postprocess.py` (verbatim from the KLYRO pilot).

## Per-brand loop

1. Stage the brand's existing art + the type templates.
2. Generate missing portraits (W3) → controls (W2) → fulls (W1); copy each raw out.
3. Post-process to the three formats; visual-check EVERY sprite (logo spelling! Gemini
   occasionally mirrors text — the KLYRO wood full had a mirrored sole logo accepted as-is, but
   reject obvious misspellings like "KLYPO").
4. SendUserFile the brand's set to Cesar for approval.
5. On approval, device_commit_files into `Resources/Clubs/{Portraits,Controls,Full}/` with the
   exact naming above. Unity generates .meta on next focus. Claude Code commits git, never Cowork.
6. Update the coverage matrix in this file.

## Start prompt for the next session (paste as first message)

> Read Docs/Specs/Active/club_art_batches/SPEC.md in the GolfinRedux repo (or project doc
> claude/CLUB_ART_PIPELINE_RUNBOOK.md) and execute it brand by brand, starting with FOREFIT (GOLFINIX and EAGLEZ are complete).
> Request folder access to /Users/cesar/Documents/GolfinRedux and /Users/cesar/Downloads first.
> Deliver each brand's sprites to me for approval before committing to the repo.

(KLYRO, MireO, FYLOE, GOLFIN, G&F, ROYAL SWING and TIFTO are done. Read mechanics items 6–13 before driving Chrome — they
cost several full runs to learn - especially 15 to 19.)
