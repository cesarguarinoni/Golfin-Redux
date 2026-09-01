# STATUS — ball_art_and_stats

`ART_DONE / DATA_HANDED_TO_CODE` (2026-08-31, Cowork/Architect runner). D1–D5 delivered; the
data half is `Docs/Specs/Active/ball_data_wiring/` (SPEC_READY). This folder closes when that one
does — the 18 fulls are uncommitted in the working tree until Code's first commit (see
`ball_data_wiring/SPEC.md` §9).

## Decisions of record (Cesar, 2026-08-31 — decision round before the stat table)

1. **Ball rarity lives in `Balls.csv` as a `rarity` column** (SPEC §4.2 route (a)). Cesar added:
   *"But of course, Balls should also be managed from the web admin like clubs and characters."*
   → `ball_data_wiring` §5 covers the admin (balls already ride as a tab in the Items panel; a
   dedicated Balls panel is flagged as a question, not built).
2. **`BallWindCutPerPoint` 0.01 → 0.02** (SPEC §4.1). +10 wind buys 0.20 of the 0.30 cap.
   Perceptibility numbers are an acceptance item in `ball_data_wiring` §4.2 (no bar invented).
3. **Stat table approved on the runner's recommendation** — the draft stands, with Klyro, Fyloe Aim
   and Soralis trimmed on their other positives to pay for the doubled wind stat; net budget
   unchanged. Final table + arithmetic in `BALL_IDENTITY.md` (APPROVED 2026-08-31).
4. **Dedicated Balls admin panel** (Cesar, after the kickoff was drafted: *"I want the panel"*) —
   `ball_data_wiring` §5.1; the balls tab leaves the Items panel.
5. **`Physics/stats.csv` retired** (Cesar chose retire over wire/leave) — `ball_data_wiring` §4.2
   deletes the file and the never-called `LoadStatCoefficients()`.

## Deliverables

| | State |
|---|---|
| D1 — 18 × `Full/<Name>.png` 537×900 RGBA 30px corners | **DONE**, in the working tree (uncommitted). 20 generations for 18 keepers. |
| D2 — thumbnail wiring | `thumbnailSprite` = existing `S_Controls_Ball_<TOKEN>` stem; `S_Controls_Ball_GOLFINMK2.png` + `S_Controls_Ball_PUTTACE.png` copied into `Resources/Balls/Thumbnails/`. On-device 1000×1000 check → `ball_data_wiring` §7. |
| D3 — stat table | **APPROVED** in `BALL_IDENTITY.md`; rules 1–6 script-checked (`reference/build_docs.py`). |
| D4 — EN+JA blurbs | In `BALL_IDENTITY.md` and as ready-to-import rows in `ball_data_wiring/reference/`. Every claim checked against the sign of its stats. |
| D5 — Code spec + kickoff | `Docs/Specs/Active/ball_data_wiring/SPEC.md` + `STATUS.md`; pointer + kickoff in `Docs/TellCode.md`; kickoff delivered in chat. |
| Review sheets | 3 × 6 on a neutral grey checkerboard, sent as each set landed. |

## Wordmark / brand-mark verdicts (SPEC §9 — zoomed on the 1600×2682 raw, not the chat panel)

| Ball | Verdict |
|---|---|
| Cirq | `CIRQ` over `GOLF` + spiral — correct, inside the face |
| Klyro | `KLYRO` ×4 around the ring, chevron centred — correct (the far repeats mirror exactly as the base sprite does) |
| Soralis | `SORALIS` on the lime band, crescent mark — correct; the wordmark runs toward the right limb exactly as the base sprite does |
| Royal Swing | `ROYAL` (orange) `SWING` (white) — correct, inside the face |
| MireO | `MireO` script on the Greek-key medallion, two sparkles — correct |
| G&F | `G&F` serif + comma mark, double pinstripes — correct |
| Fairloft | `FAIRLOFT` / `JAPAN` lozenge, X lines — correct |
| Fairway THREADS | `Fairway` script / `THREADS` badge on the white+red stripe — correct |
| GolfinIX | `GOLFIN` white + `IX` orange on black badge — correct |
| Par Perfect | `PAR` / `PERFECT`, pink+navy flank stripes — correct and fully inside the face (better than the base sprite, which clips) |
| Birdie V1 | `BIRDIE` + crossed-flags mark — correct (carries the base sprite's scuffed finish, faithfully) |
| Tifto | `VOIGT94` + ripple — correct on the keeper (roll 2) |
| Ace Attire | `ATTIRE` / `ACE` sharing the big A — the interlocked monogram came back exactly as drawn |
| Fyloe Soft | `FYLOE` repeated on the magenta band, centre repeat fully readable — correct |
| Fyloe Aim | crosshair + F, no lettering — correct |
| Clover Pro | white clover + green disc — correct. **The disc carries a swinging-golfer silhouette**, which is what the base sprite actually has (the draft sheet said "swirl"; ART WINS — sheet corrected). It is the shipped hand-made mark, so it ships. |
| Golfin MK2 | one large bold gradient G, visibly heavier than the Golfin full's — correct |
| Shimmer G | oil-slick + white disc + grey G — correct on the keeper (roll 2) |

No real-world brand marks anywhere (zoomed all 18). No scenery furniture invented; the flag, bunker
and treeline are the template's in all 18.

## Running log

- 2026-08-31 — Session start. Read SPEC, BALL_IDENTITY (draft), `club_art_batches/STATUS.md`.
  Verified on disk: 20 base sprites, Full/ has Golfin + PuttAce only, Thumbnails/ missing
  GOLFINMK2 + PUTTACE — matches SPEC §1. Decision round with Cesar (above).
- Art batch, 20 generations → 18 keepers. Order: Cirq, Klyro, Soralis, Royal Swing, MireO, G&F ·
  Fairloft, Fairway THREADS, GolfinIX, Par Perfect, Birdie V1, Tifto ·
  Ace Attire, Fyloe Soft, Fyloe Aim, Clover Pro, Golfin MK2, Shimmer G.
  Raws banked in `~/Downloads/golfin_ball_gen/<token>_full_raw.jpg` (+ `tifto_full_raw.jpg` and
  `shimmerg_full_raw.jpg`, the rejected first rolls).
- Post-processed with `club_art_batches/postprocess.py::full_scene` (resize 537×900 + 30px mask),
  corner-white check 0 px on all 18; `qa.py`/`pafix.py` were not needed (no cut-outs, no logos,
  no wordmark flips on a sphere).
- Stat table finalised, blurbs written EN+JA, `Balls.csv` + `texts_rows.csv` generated and
  rule-checked by `reference/build_docs.py`.
- `ball_data_wiring` spec written; TellCode pointer + kickoff; AI_CONTEXT updated.
- Cesar's two follow-up decisions (Balls panel, retire stats.csv) folded into the spec BEFORE kickoff
  (§4.2, §5.1); pointer, kickoff and AI_CONTEXT re-issued.

## Lessons from this run (add to the next runbook)

- **The Gemini tab was a BACKGROUND tab all session** (`document.hidden === true`) because Cesar
  was working in another window. Chrome throttles timers in background tabs, so: the first `type`
  after a `navigate` was swallowed every time, the "+" menu took >3 s to mount, and `find` reported
  0 file inputs while the menu was still animating. **Fix that worked 18/18:** after every action
  that needs the page to render, take a `zoom` of a small region — the capture forces a frame.
  Sequence per ball: `navigate` → wait 5 → screenshot → click composer → type `XX` → zoom → `cmd+a`
  → type the prompt → JS length check → click "+" by coordinate → wait 2 → zoom → wait 2 → JS
  `input[type=file]` count → `find` → `file_upload` → wait 5 → zoom → wait 6 → scroll composer up →
  screenshot (confirm both thumbnails, FIRST = template) → click Send by coordinate → zooms while
  waiting → `find` Download → click by ref → zoom the toast.
- **Click "+" and Send by COORDINATE computed from the DOM, not by ref.** Ref clicks on "+" toggled
  nothing three times running; the coordinate from
  `getBoundingClientRect()` × (screenshotWidth / innerWidth) landed every time. The screenshot
  width changed mid-session (1558 → 1568) when Cesar resized the window — recompute, don't cache.
- **Keep a `browser_batch` under ~45 s of waits** — a 60 s batch timed out and the Send click in
  it never happened (the composer still held the prompt; nothing was lost, but check before
  re-clicking, never click Send twice).
- **W1 ball swap failure mode = OVERSIZED BALL** (2 of 20: Tifto, Shimmer G — the ball filled ~80%
  of the width and sat higher). Not a spelling issue, not fixable in post. Re-roll with the size
  clause added after the first sentence: *"The ball must stay exactly the size it is in the FIRST
  image - a little over half the width of the frame, sitting in the lower half of the picture
  with grass visible on both sides - do NOT enlarge it, do NOT zoom in, do NOT move it up."* Fixed
  both first retry. Worth putting in every ball prompt from the start.
- Spelling was a non-issue on balls (0 misspellings in 20 rolls) — flat wordmarks on a sphere are
  easier than on a club sole. The monogram description for ACE ATTIRE ("one large A, TTIRE on the
  upper line, CE on the lower line") reproduced first try.
- Gemini reproduced marks the prompt got WRONG from the reference image (Clover Pro's golfer
  silhouette, Birdie V1's scuffs) — the second image wins over the text, which is what we want.
- Downloads land in `~/Downloads` as `Gemini_Generated_Image_*.jpeg`; the pre/post `ls` diff was
  reliable every time. `device_commit_files` in batches of 3–4 files was fine (0 timeouts).
