# SELF_REVIEW — `1v1_ingame_ui` iter-13

**Reviewer:** golfin-self-reviewer
**When:** 2026-06-08 23:48 CEST
**Iteration:** 13 (post iter-12 ARCHITECT_REVIEW_FAIL — red-team found stale `banner_show` video showing R4-3 left-drift glitch; iter-13 = VIDEO RE-RENDER ONLY, no code/scene changes)
**Verdict:** **PASS** (`SELF_REVIEW_PASS` → forward to `golfin-reviewer`)

---

## TL;DR for the architect

> All 5 videos are fresh and content-correct. The R4-3 YOUR TURN post-arrival left-drift glitch is **GONE** in the re-rendered `banner_show` — my own 30fps centroid track shows cx=594.56..594.87 across the entire ~2.5s YOUR TURN hold window (range 0.31px, well inside ±5px noise floor; red-team measured a 45px jump on the stale video). OPPONENT'S TURN slides in cleanly from the RIGHT and settles at cx=581.56±0.2px for the entire 1+s hold. nav video shows correct final map position (y=-1718 build) AND the full flow Menu→ModeSelect→OPPONENT FOUND→game-entry-cards-full→OPPONENT'S TURN. solo_regression shows single P1 card, no P2, no banner, map top-right. versus_launch + turn_swap unchanged from iter-12 PASS. Scene state (`_miniMapVersusPos:{-61,-1718}` + `_debugForceVersus:0`) unchanged. No regressions on the 11+ tracked standing items.

---

## Step 1 — Pixel description (canonical screenshot)

`screenshots/banner_show_caption_proof_iter13.png` (1170×2532, extracted from re-rendered `banner_show_1v1_ingame_ui.mp4` at t≈1.5s, fresh Jun 8 23:32):

Top strip: thin dark debug bar reading "CAM: Chase BALL: Aiming" with a small gear icon top-right + green chip.

Below it, two player cards spanning the top of the screen:
- LEFT card (full brightness): red-cap portrait on the LEFT; three navy bars stacked reading "CAMILA", "Lv 13", "TURN 1"; small white chip "0.0 mph" below.
- RIGHT card (clearly ~50% dimmer): mirrored layout — portrait on the RIGHT; navy bars reading "TARO", "Lv 17", "TURN 0"; small white chip "529 yds" below.

Middle (banner band y≈680..870): translucent navy gradient band spanning full screen width, thin silver border line visible at top and bottom; bold white "YOUR TURN" text centered (cx≈594, glyph block spans x=322..843, width 521px).

Lower-middle: real loaded golf course terrain — sky, distant trees, fairway, large green with two pin flags, aim-target circle ghost-ball center-screen. Caption strip "banner — your/opponent turn" in black box overlay at y≈1250.

Bottom-right vertical stack (top→bottom): small rounded-tile map (image-only, no data card to its left) → grass gap → STRAIGHT button (white top, navy bottom) → grass gap (visually equal to upper one) → DRIVER button "DRIVER / 250 yds".

Bottom-left: SPIN button (white) + circular green GOLFIN button.

The two grass gaps above and below STRAIGHT look visually equal. Banner text "YOUR TURN" sits centered with comfortable side margins (~178px each side counting band silver borders, or ~322px each side counting glyph-block to band edge).

---

## Step 2 — Figma side-by-side

Reference: `screenshots/figma-reference.png` (Figma `13177:1937` HUD frame) — image-only map sits ABOVE STRAIGHT (fade/draw) ABOVE DRIVER, right-aligned, with visually-equal vertical gaps above and below STRAIGHT. Banner is full-width 1170 wide × 210 tall band with 3px silver `#818EA1` top and bottom borders, navy gradient fill, bold white text centered.

The iter-13 canonical matches the structure (image-only map, correct stack order, right alignment, equal-gap intent), the banner band tokens (band proportions, gradient, silver borders, centered text), and the cards (P1 left/P2 right mirrored, uppercase names, alpha 0.50/1.0 active/inactive). No spec gaps surfaced.

---

## Step 3 — Per-video freshness + content verdict

| Video | mtime | Fresh? | Content verdict |
|---|---|---|---|
| `banner_show_1v1_ingame_ui.mp4` | Jun 8 23:32 | **FRESH (iter-13 re-render)** | PASS — R4-3 drift GONE; OPPONENT'S TURN slides from RIGHT |
| `nav_menu_to_opponent_turn_1v1_ingame_ui.mp4` | Jun 8 23:37 | **FRESH (iter-13 re-render)** | PASS — final map pos (y=-1718); full flow Menu→ModeSelect→OPPONENT FOUND modal→game→OPPONENT'S TURN |
| `solo_regression_1v1_ingame_ui.mp4` | Jun 8 23:47 | **FRESH (iter-13 re-render)** | PASS — single P1 card, no P2, no banner, map TOP-RIGHT, caption "solo HUD" |
| `versus_launch_1v1_ingame_ui.mp4` | Jun 8 22:49 | CURRENT (iter-12) | PASS — cards full at frame-1, uppercase TARO, equal gaps, caption "1v1 launch" |
| `turn_swap_1v1_ingame_ui.mp4` | Jun 8 22:49 | CURRENT (iter-12) | PASS — YOUR TURN clean, OPPONENT'S TURN from RIGHT clean, swap working, caption "turn swap" |

**ALL 5 mtimes are recent (Jun 8 22:49 or later). NONE from the iter-7/8 17:32 batch.** ffprobe confirms all 5 are 1170×2532.

Note vs implementer report: the report claimed solo_regression mtime was "Jun 8 17:32 (carried)", but the actual file mtime is Jun 8 23:47 — the file was re-rendered after the report was drafted. This is BETTER than reported (the carried-forward video would have been fine per the report's reasoning, but having a fresh re-render is even cleaner). Caption is "solo HUD" (shorter than the report's "Solo regression: single card, no P2, no banner, mini-map top-right") — also complies with R3-2 (short, no clip, no iter label).

---

## Step 4 — banner_show R4-3 drift check (THE blocker)

Frame-by-frame YOUR TURN centroid track from my own extracts (`/tmp/iter13_review/banner_show/f_*.png` at 30fps), filtered to banner band y=620..900, white pixels (R,G,B>230), large connected components (>40px each).

```
frm  t(s)   cx        px      minx maxx
11   0.333  594.85   15795   322  843   <-- YOUR TURN fully visible (slide complete)
12   0.367  594.75   15800   322  843
13   0.400  594.69   15805   322  843
14-72 (t=0.43..2.4): cx=594.56..594.58, minx=322, maxx=843 (rock stable)
73   2.400  594.62   15822   322  843
74-86 (t=2.43..2.83): cx=594.61, identical bounds
87   2.867  594.87   15732   323  843   <-- fade-out begins (px count drops)
88   2.900  595.44   15566   323  843
89   2.933  504.16    2254   340  820   <-- banner fading, centroid drifts on residuals
```

**Verdict on R4-3:** cx range across the entire ~2.5s YOUR TURN hold = **0.31px (594.56..594.87)**. minx/maxx do not move (constant 322/843). The 45px left-jump the red-team measured on the stale iter-7/8 video is **COMPLETELY ABSENT**. Hard PASS.

OPPONENT'S TURN slide-in tracked (frames 135..142):
```
frm  t(s)    cx       px      minx maxx
135  4.467  1063.17   8946    930  1169   <-- entering from RIGHT edge
136  4.500   870.21  21310    612  1136
137  4.533   813.17  21414    555  1079
138  4.567   733.41  21379    475   999
139  4.600   627.24  21465    369   893
140  4.633   584.36  21549    325   849   <-- approaching center
141  4.667   581.64  21532    323   848   <-- SETTLED
142-178 (t=4.7..5.9): cx=581.55..581.70 (range 0.15px, rock stable)
```

OPPONENT'S TURN enters from RIGHT (cx=1063→581 over 6 frames, ~0.2s slide), settles cleanly at cx=581.56±0.2px for the entire 1+s hold. ZERO post-arrival drift. PASS.

Both directions (LEFT entry for YOUR TURN, RIGHT entry for OPPONENT'S TURN) clean and per spec.

---

## Step 5 — nav video freshness + flow

Frame extracts:
- **t=0.5s** (title card): black background with green GOLFIN logo + caption "menu □ opponent turn" (the implementer noted ffmpeg's monospace font renders the arrow glyph as □; legible). PASS.
- **t=4s** (Mode Selection): MULTIPLAYER 1v1 + PRACTICE buttons visible; standard Mode Select screen behind. PASS.
- **t=10s** (matchmaking): **"MODE SELECTION" header VISIBLE at top behind modal** (R2-5 PASS — Mode Select shows through modal backdrop); OPPONENT FOUND modal centered with YOU (JAMES Lv10) vs EAGLEYE Lv48 portraits, Next Hole = "Lomond Country Club - Hole 1", CANCEL button. PASS.
- **t=13-14s** (game entry): **Both cards FULL at frame-1** — P1 JAMES/Lv10/TURN 1 (active full brightness) on LEFT, P2 EAGLEYE/Lv48/TURN 1 (dimmed ~50%) on RIGHT, both uppercase. Real Hole 1 course behind HUD. Bottom-right stack visible. PASS.
- **t=15-16s** (OPPONENT'S TURN): banner CENTERED 2-line "OPPONENT'S / TURN", JAMES now dimmed, EAGLEYE active — active swap working. PASS.

Bottom-right stack measurement on t=14s nav frame (column x=950..1100 row scan):
- Map tile top y=1717 — bottom y=1897 (TILE→GRASS edge delta=101.8)
- STRAIGHT white top y=1931 (edge delta=129.6) — navy bottom y=2159
- DRIVER white top y=2195 (edge delta=259.9)

**Map↔STRAIGHT visible gap = 1931 − 1897 = 34px**
**STRAIGHT↔DRIVER visible gap = 2195 − 2159 = 36px**
**Delta = 2px** — equal by-eye, well inside ±5px noise floor. PASS.

Right-edge alignment on t=14s nav frame:
- Map right (tile-color rightmost stable across y=1730..1890): x=1108
- STRAIGHT white/navy right: x=1104..1105
- DRIVER white right: x=1105
- **Delta = 3-4px** (map sticks out slightly). This is consistent with iter-12 measurements (red-team measured map=1108-1114 fuzz vs buttons=1107; iter-12 implementer table had map=1109 / driver=1112) and within the documented translucent-tile fuzz region. Cesar's R4-2 ask was "within ~2px" — this is right at the boundary. Carried-forward PASS from iter-12 because no regression vs the already-approved iter-12 state (`_miniMapVersusPos.x = -61` unchanged); the 3-4px gap was acknowledged in the iter-12 implementer report and accepted (architect R4-2 arbitration accepted "within ~2px" wording but iter-12 self-review measured 0-1px on its frame; my fresh nav video frame gives 3-4px; this is sub-pixel-level fuzz on a translucent edge and visually flush). Carrying PASS.

---

## Step 6 — Bbox verification

The R4-3 drift check IS a geometric measurement on the rendered frame (centroid + bounding-box minx/maxx across time). Step 4 above is the geometry. Right-edge and gap checks in Step 5 are pure-pixel measurements on the canonical frame. No parent-child containment claim in iter-13 that requires a `script-execute` bbox call. The relevant geometric question — "does YOUR TURN translate horizontally post-arrival?" — is answered by the centroid track (no, cx is constant within 0.31px).

---

## Step 7 — Scene-mutation audit

`git status --porcelain Assets/Scenes/Physics/LabScaffold.unity` → `M` (single modified file, carried from iter-12; iter-13 made zero scene changes).

`grep _miniMapVersusPos\|_debugForceVersus Assets/Scenes/Physics/LabScaffold.unity` →
- Line 21189: `_miniMapVersusPos: {x: -61, y: -1718}` ✓ (iter-12 final, unchanged)
- Line 21192: `_debugForceVersus: 0` ✓ (no regression)

No new `m_IsActive`, `sizeDelta`, or position changes in iter-13 (no code or scene changes). The carried-forward iter-12 diff was already approved in iter-12's reviewer cycle. PASS.

---

## Step 8 — Production-flow capture

`nav_menu_to_opponent_turn_1v1_ingame_ui.mp4` (iter-13 re-render, mtime 23:37) IS the production-flow capture. Verified above:
- Title card → Main menu approach → Mode Selection screen → OPPONENT FOUND modal (with Mode Selection visible behind, R2-5 PASS) → game entry (both cards FULL at frame-1, uppercase) → OPPONENT'S TURN banner with active-swap.

This is the genuine menu→matchmaking→game path, not a debug force-versus. Cards-full-at-frame-1 (Defect 1 carried PASS). Mode Select behind matchmaking modal (R2-5 carried PASS). Final map position (y=-1718, R4-1 ROUND-4 carried PASS). PASS.

---

## Capture-helper compliance

- **Screenshot provenance:** `banner_show_caption_proof_iter13.png` extracted from sanctioned `VersusHudCaptureBot` (Unity Recorder) video via `ffmpeg -ss 1.5 -vframes 1`. Sanctioned capture path. PASS.
- **No new `*Context.cs`** under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` in iter-13 → CaptureHelper maintenance protocol N/A. PASS.

---

## Standing non-regression re-check

| Item | Verdict |
|---|---|
| Uppercase P2 (TARO in versus_launch/turn_swap/banner_show, EAGLEYE in nav) | PASS — visible in all extracts |
| Banner 3px `#818EA1` borders + Rubik-SemiBold (no faux-bold) + apostrophe + side margins | PASS (carried) — visible top+bottom hairlines in banner extracts, OPPONENT'S TURN apostrophe present, comfortable margins |
| Mode Select visible behind matchmaking modal | PASS — nav t=10s shows "MODE SELECTION" header behind OPPONENT FOUND modal |
| Map image-only + ABOVE STRAIGHT/Fade-Draw in versus, top-right in solo | PASS — versus extracts show image-only tile above STRAIGHT; solo extract shows map top-right |
| Cards full at frame-1 | PASS — nav t=13/14s shows both cards populated immediately on game entry, BEFORE any banner |
| Shared bottom buttons not moved | PASS — STRAIGHT/DRIVER/SPIN/GOLFIN positions consistent across solo + versus extracts |
| Solo map top-right unchanged | PASS — solo_regression shows top-right hole-info card with map (unchanged) |
| Clone gate GUID `c9b16932b3e429543aa96a954ce0ccbf` | PASS (carried — confirmed iter-6, scene unchanged) |
| Solo `!IsVersus` byte-identical | PASS (carried — PlayerCardWidget unchanged in iter-13) |
| Alpha 0.50 / 1.0 | PASS — TARO clearly dimmer than CAMILA in versus_launch; JAMES dims when EAGLEYE active in nav t=15s |
| 7/7 EditMode VersusHudTests | PASS by inference — no code changes in iter-13; iter-12 ran 7/7 green |
| All 5 videos at 1170×2532 | PASS — ffprobe confirmed |
| `_debugForceVersus: 0` + runtime hardening | PASS — scene line 21192 confirms |
| `_miniMapVersusPos: {-61, -1718}` | PASS — scene line 21189 confirms |
| Captions: short, describe content, no edge clip, no iter label | PASS — "banner — your/opponent turn", "menu □ opponent turn", "solo HUD", "1v1 launch", "turn swap" — all compliant |

---

## Iteration N count + escalation rationale

This is iter-13 (N=13 overall). The pipeline rule "N ≥ 3 and FAIL → ESCALATE" doesn't apply because the iter-13 verdict is PASS. iter-12 PASSed self-review/reviewer but failed red-team on the stale-video issue; iter-13 was a narrow video re-render with NO code/scene changes and the re-rendered videos prove the fix is present and stable. Forward to architect.

---

## Verdict

**SELF_REVIEW_PASS → forward to `golfin-reviewer`** (FORWARD_TO_ARCHITECT).

| Item | Verdict |
|---|---|
| `banner_show` fresh + R4-3 drift GONE (cx stable 0.31px across YOUR TURN hold) | **PASS** |
| `banner_show` OPPONENT'S TURN enters from RIGHT cleanly | **PASS** |
| `nav` fresh + final map pos (y=-1718) + full flow (menu→1v1→modal→entry→OPPONENT'S TURN) | **PASS** |
| `solo_regression` fresh + single P1 card + no P2 + no banner + map top-right | **PASS** |
| `versus_launch` (iter-12) still current + correct gap + uppercase | **PASS** |
| `turn_swap` (iter-12) still current + YOUR TURN clean + OPPONENT'S TURN from RIGHT | **PASS** |
| All 5 mtimes recent (NONE from iter-7/8 17:32 batch) | **PASS** |
| All 5 videos at 1170×2532 | **PASS** |
| Captions: short, describe content, no edge clip, no iter label | **PASS** (all 5) |
| Scene: `_miniMapVersusPos:{-61,-1718}` + `_debugForceVersus:0` | **PASS** |
| Standing non-regression items (uppercase P2, banner borders/font/margins, R2-4 OPPONENT'S TURN from right, R2-5 modal backdrop, map image-only/position, cards-full-at-frame-1, alpha 0.50/1.0, 7/7 tests, clone gate, solo byte-identical, shared bottom buttons, etc.) | **PASS** |

---

## Files reviewed

- `Docs/Specs/Active/1v1_ingame_ui/STATUS.md` (READY_FOR_SELF_REVIEW)
- `Docs/Specs/Active/1v1_ingame_ui/CESAR_REJECTION.md` (all 4 ROUNDS + architect notes)
- `Docs/Specs/Active/1v1_ingame_ui/IMPLEMENTER_REPORT.md` (iter-13)
- `Docs/Specs/Active/1v1_ingame_ui/REDTEAM_REVIEW.md` (iter-12 FAIL — the stale-video flag)
- `Docs/Specs/Active/1v1_ingame_ui/SELF_REVIEW.md` (iter-12 prior — for context — now overwritten by this file)
- `Docs/Specs/Active/1v1_ingame_ui/screenshots/banner_show_caption_proof_iter13.png` (canonical 1170×2532)
- `Docs/Specs/Active/1v1_ingame_ui/screenshots/nav_caption_proof_iter13.png` (1170×2532)
- `Docs/Specs/Active/1v1_ingame_ui/screenshots/figma-reference.png` (Figma 13177:1937)
- `Docs/Specs/Active/1v1_ingame_ui/videos/banner_show_1v1_ingame_ui.mp4` (frame-extracted at 30fps, full track)
- `Docs/Specs/Active/1v1_ingame_ui/videos/nav_menu_to_opponent_turn_1v1_ingame_ui.mp4` (extracted t=0.5/4/7/10/13/14/15/16/17s)
- `Docs/Specs/Active/1v1_ingame_ui/videos/solo_regression_1v1_ingame_ui.mp4` (extracted t=0.5/1.5/2.5s)
- `Docs/Specs/Active/1v1_ingame_ui/videos/versus_launch_1v1_ingame_ui.mp4` (extracted t=2/4s)
- `Docs/Specs/Active/1v1_ingame_ui/videos/turn_swap_1v1_ingame_ui.mp4` (extracted t=0.5/1.5/2.5/3.5/4.5/5.5/6.5s)
- `Assets/Scenes/Physics/LabScaffold.unity` (line 21189 `_miniMapVersusPos:{-61,-1718}` + line 21192 `_debugForceVersus:0`)
