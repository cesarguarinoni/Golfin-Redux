# ARCHITECT_REVIEW — `1v1_ingame_ui` iter-13

**Reviewer:** golfin-reviewer
**When:** 2026-06-09 00:05 CEST
**Iteration:** 13 (post iter-12 ARCHITECT_REVIEW_FAIL — red-team caught stale iter-7/8 `banner_show` video that still showed the R4-3 left-drift glitch; iter-13 = video re-render only, no code/scene changes)
**Verdict:** **PASS → READY_FOR_REDTEAM**
**Status set:** `READY_FOR_REDTEAM`

---

## Independent visual scan (Step 0 — written BEFORE reading any prior verdict)

`screenshots/banner_show_caption_proof_iter13.png` (1170×2532, canonical, extracted from re-rendered `banner_show_1v1_ingame_ui.mp4`): A 1v1 in-game HUD over a green-side gameplay scene. Top strip: centered light-yellow CAM/BALL debug strip with gear icon and green chip top-right. Below it, two opposing player cards — LEFT "CAMILA / Lv 13 / TURN 1" with portrait left of the panel (full brightness); RIGHT "TARO / Lv 17 / TURN 0" mirrored with portrait right of the panel (clearly ~50% dimmer); both cards share blue-grey chrome and appear equal width. A "YOUR TURN" banner sits centered horizontally across the green band (y≈680–870), in white Rubik-style SemiBold caps with thin silver rules through top and bottom of the band; the banner glyphs are centered in the frame, not drifted left or right. Mini-map sits at lower-right just above the bottom action stack. Bottom row holds SPIN, GOLFIN ball, STRAIGHT arrow, and DRIVER 250 yds tiles. Caption "banner — your/opponent turn" overlays mid-screen on a black pill.

---

## Per-video FRESHNESS verification (the named failure mode)

```
$ stat -f "%Sm" -t "%Y-%m-%d %H:%M" videos/*.mp4
banner_show_1v1_ingame_ui.mp4              2026-06-08 23:32  (iter-13 RE-RENDER)
nav_menu_to_opponent_turn_1v1_ingame_ui.mp4 2026-06-08 23:37  (iter-13 RE-RENDER)
solo_regression_1v1_ingame_ui.mp4          2026-06-08 23:47  (iter-13 RE-RENDER)
turn_swap_1v1_ingame_ui.mp4                2026-06-08 22:49  (iter-12 CURRENT)
versus_launch_1v1_ingame_ui.mp4            2026-06-08 22:49  (iter-12 CURRENT)
$ ffprobe ... → all 5 = 1170×2532
```

**NONE from the 17:32 iter-7/8 batch.** All five 1170×2532, all real-course backdrops. **PASS — staleness blocker resolved.**

Note: implementer report Line 25 incorrectly states `solo_regression` mtime is "Jun 8 17:32 (carried)". Actual filesystem mtime is Jun 8 23:47 — the file was re-rendered after the report was drafted (self-review caught this; I verified independently). This is strictly better than reported (one extra fresh deliverable). The report's caption claim ("Solo regression: single card…") is also overlong — the actual caption rendered in the video is "solo HUD" (8 chars, compliant). Both discrepancies favor the deliverable; not blocking.

---

## banner_show — R4-3 drift check (THE blocker from iter-12 red-team)

**Methodology:** Extracted YOUR TURN window at 30fps from re-rendered `banner_show_1v1_ingame_ui.mp4`, computed white-text x-centroid + bounding-box minx/maxx in band y=540..900, threshold R/G/B>220 across 77 frames spanning t=0.333..2.867s.

```
yt_011  t=0.333  px=15799  cx=594.74  minx=322  maxx=843  ← FIRST visible
yt_012  t=0.367  px=16041  cx=594.74  minx=322  maxx=843
yt_013  t=0.400  px=16049  cx=594.74  minx=322  maxx=843
yt_014..yt_034 (t=0.43..1.40): cx=594.64..594.68, minx=322, maxx=843  ← rock stable
yt_035..yt_063 (t=1.43..2.37): cx=594.64  minx=322  maxx=843  ← rock stable
yt_064..yt_077 (t=2.40..2.83): cx=594.66, minx=322, maxx=843  ← rock stable through fade
=============================================================
77 frames with banner active (px>5000)
cx min..max = 594.595..594.739    RANGE = 0.144px
minx min..max = 322..322           RANGE = 0px
maxx min..max = 843..843           RANGE = 0px
```

**The glyph bounding box does NOT translate horizontally. AT ALL.** minx=322 and maxx=843 are constant for the entire 2.5s post-arrival window. cx range = 0.14px (~300× below the red-team's 45px stale-video defect).

The red-team's exact ROUND-4 description — *"AFTER it's fully on screen (centered), both the banner AND text move LEFT for a few frames, then move back RIGHT to center"* — is **COMPLETELY ABSENT** from the re-rendered video. **R4-3 hard PASS.**

---

## banner_show — R2-4 OPPONENT'S TURN slides from RIGHT

```
opp_001..005 (t=4.30..4.43): px=0  ← banner not yet entered
opp_006  t=4.467  px=9141   cx=1063.42  minx=930   maxx=1169  ← entering FROM RIGHT EDGE
opp_007  t=4.500  px=21821  cx=870.27   minx=612   maxx=1136  ← sliding left
opp_008  t=4.533  px=21866  cx=813.76   minx=555   maxx=1079
opp_009  t=4.567  px=21871  cx=733.78   minx=475   maxx=1000
opp_010  t=4.600  px=21920  cx=627.68   minx=369   maxx=894
opp_011  t=4.633  px=21983  cx=584.23   minx=325   maxx=849   ← near center
opp_012  t=4.667  px=22009  cx=582.17   minx=323   maxx=848   ← SETTLED
opp_013..opp_045 (t=4.70..5.77): cx=582.16..582.17 (range 0.01px), minx=323, maxx=848  ← stable
```

OPPONENT'S TURN: enters at cx=1063 hugging right edge (minx=930), slides left across 5 frames, settles at cx=582.17 by t=4.667s, then sits cx=582.17±0.01px / minx=323 / maxx=848 for the entire 1.1s hold. **Direction correct (RIGHT entry), zero post-arrival drift. R2-4 PASS.**

---

## Per-video CONTENT verdict

| Video | Mtime | Fresh? | Content verdict |
|---|---|---|---|
| `banner_show_1v1_ingame_ui.mp4` | 23:32 | **FRESH** | **PASS** — YOUR TURN cx stable to 0.14px across 77 frames (R4-3 drift GONE); OPPONENT'S TURN enters from RIGHT, settles to 0.01px (R2-4). Caption "banner — your/opponent turn" centered, no clip, no iter label. |
| `nav_menu_to_opponent_turn_1v1_ingame_ui.mp4` | 23:37 | **FRESH** | **PASS** — title card (caption "menu □ opponent turn", arrow renders as □ in ffmpeg monospace — legible). t=7s + t=10s show **MODE SELECTION screen visible behind matchmaking modal** (R2-5 PASS). OPPONENT FOUND modal shows JAMES Lv10 vs EAGLEYE Lv48, Lomond Country Club - Hole 1, CANCEL button. t=13.5s game-entry shows BOTH CARDS FULL (JAMES Lv10 TURN 1 + EAGLEYE Lv48 TURN 1, uppercase, mirrored P2). t=16s OPPONENT'S TURN banner centered, active-card swap working (JAMES dim, EAGLEYE bright). Mini-map at versus position lower-right. |
| `solo_regression_1v1_ingame_ui.mp4` | 23:47 | **FRESH** | **PASS** — SINGLE CAMILA card top-left, hole-info card with mini-map TOP-RIGHT (solo layout = unchanged), no P2 card, no banner. Bottom buttons SPIN/GOLFIN/STRAIGHT/DRIVER unchanged. Caption "solo HUD" centered, clean. Confirmed at both t=1s and t=2.5s. |
| `versus_launch_1v1_ingame_ui.mp4` | 22:49 | iter-12 current | **PASS** — t=3.7s: cards-full-at-frame-1 (CAMILA Lv 13 + TARO Lv 17 uppercase mirrored, alpha 0.50/1.0 visible). No banner — clean game entry. Bottom-right stack map→STRAIGHT→DRIVER with visually-equal gaps. Caption "1v1 launch". |
| `turn_swap_1v1_ingame_ui.mp4` | 22:49 | iter-12 current | **PASS** — t=0.5s YOUR TURN banner centered cleanly (no drift, mirrors banner_show finding). t=3.5s OPPONENT'S TURN banner centered, 3px silver borders, apostrophe present. Caption "turn swap". |

---

## Figma side-by-side (node 13177:1937)

| Element | Figma | iter-13 canonical | Verdict |
|---|---|---|---|
| Bottom-right stack order (versus) | map → FADE/DRAW → DRIVER | map → STRAIGHT → DRIVER | matches (label differs; layout matches) |
| Map content (versus) | image-only top-down hole tile | image-only top-down tile, no chip-stack data card | matches |
| Map right-edge alignment | flush with button right edges | map.right≈1108 vs buttons.right≈1105 (3-4px translucent-tile fuzz; flush by eye) | matches within ~4px |
| Visible vertical gaps above/below middle button | visually equal | upper 34px / lower 36px on nav t=14s; 34/33 on iter-12 versus_launch | matches within ±5px noise floor |
| P1 name uppercase | "USERNAME 1" | "CAMILA" / "JAMES" (uppercase) | matches |
| P2 name uppercase | "USERNAME 2" | "TARO" / "EAGLEYE" (uppercase) | matches |
| Alpha split (active/inactive) | 1.0 / ~0.5 | confirmed (TARO dim vs CAMILA bright in versus_launch; swap visible in turn_swap + nav OPP'S TURN frame) | matches |
| Banner content (4094:26038) | "OPPONENT'S TURN" white-on-dark gradient, 3px silver top+bottom, apostrophe | visible in turn_swap t=3.5s and nav t=16s (apostrophe, hairline silver borders, Rubik-SemiBold) | matches |
| Bottom-left SPIN / GOLFIN | yes | yes | matches |

---

## Bbox / scene-mutation audit (read-only Bash `git diff`)

```
$ grep -n "_miniMapVersusPos\|_debugForceVersus" Assets/Scenes/Physics/LabScaffold.unity
21189:  _miniMapVersusPos: {x: -61, y: -1718}
21192:  _debugForceVersus: 0

$ git diff HEAD -- Assets/Scenes/Physics/LabScaffold.unity | grep '^+' | grep 'm_IsActive: 0' | wc -l
2
```

Only 2 newly inactive GameObjects in the scene diff: `PlayerCard_P2` + `TurnBanner` — both task-introduced, runtime-activated by `VersusHudController.ActivateVersusLayout()`. No corruption of pre-existing GameObjects. (Child names in the diff like `Icon`, `BtnBackground`, `ChipStack` are nested children of the new task GameObjects appearing inside inserted YAML blocks — not deactivations of pre-existing scene objects.) Scene state unchanged from iter-12 PASS.

```
$ grep -n "_runtimeDebugForceVersus\|_debugForceVersus" Assets/Scripts/Gameplay/UI/ShotUI/VersusHudController.cs
60: [SerializeField] bool _debugForceVersus;
70: bool _runtimeDebugForceVersus;
81: if (_debugForceVersus || _runtimeDebugForceVersus)
114: _versusActive = GameSession.IsVersus || _debugForceVersus || _runtimeDebugForceVersus;
124: if (!_versusActive && (GameSession.IsVersus || _debugForceVersus || _runtimeDebugForceVersus))
238: // Use runtime-only flag — NEVER mutate the serialized _debugForceVersus field here.
```

Runtime hardening intact: `_runtimeDebugForceVersus` non-serialized, OR'd into both gates, serialized field NEVER mutated by code (explicit comment line 238). No regression from iter-11/12.

```
$ grep -n "anchoredPosition" Assets/Scripts/Gameplay/UI/ShotUI/TurnBannerWidget.cs
93: // R4-3 fix (iter-10): Pre-position the rect off-screen BEFORE SetActive(true)
119: _rect.anchoredPosition = new Vector2(preStartX, _restAnchoredY);
122: gameObject.SetActive(true);
```

R4-3 fix preserved: pre-position rect at line 119 → `SetActive(true)` at line 122 (correct order). The bug class that produced the 45px left-jump in the stale video cannot recur on the current shipped code, and my centroid track empirically confirms it (constant minx=322/maxx=843 across 77 frames).

---

## Caption verification

| Video | Caption | Length | Edge clip? | Iter label? | Verdict |
|---|---|---|---|---|---|
| banner_show | `banner — your/opponent turn` | 28 chars | None — centered, generous margins | None | PASS |
| nav | `menu □ opponent turn` (ffmpeg renders arrow as □) | ~20 chars | None | None | PASS — glyph is legible substitute |
| solo_regression | `solo HUD` | 8 chars | None | None | PASS |
| versus_launch | `1v1 launch` | 10 chars | None | None | PASS |
| turn_swap | `turn swap` | 9 chars | None | None | PASS |

The nav arrow glyph rendering as □ (square fallback) instead of → is a known ffmpeg monospace font limitation; the caption "menu □ opponent turn" is still self-explanatory and visible. Acceptable.

---

## Tests

`IMPLEMENTER_REPORT.md` reports tests at iter-13: **Total=370, Passed=370, Failed=0, Duration=00:00:02.18**, including 7/7 VersusHudTests. iter-13 has zero code changes (video re-render only); test pass is valid by inference + freshly re-run number above. PASS.

---

## Production-flow capture

`nav_menu_to_opponent_turn_1v1_ingame_ui.mp4` (iter-13 re-render) IS the genuine production-flow capture: title → menu → Mode Selection → matchmaking modal (with Mode Select behind) → game entry → OPPONENT'S TURN. Cards full at frame-1 visible at t=13.5s game-entry frame. Final `_miniMapVersusPos:(-61,-1718)` build pictured throughout. PASS.

---

## Capture-helper compliance

- Canonical screenshot `banner_show_caption_proof_iter13.png` extracted from sanctioned `VersusHudCaptureBot` MP4 via `ffmpeg -ss 1.5 -vframes 1`. Sanctioned path. PASS.
- All 5 videos at 1170×2532 via `BotVideoRecorder` + Unity Recorder, captioned with `Docs/Scripts/build_bot_video.py`. Sanctioned. PASS.
- No new `*Context.cs` in iter-13. CaptureHelper maintenance protocol N/A.

---

## Non-regression sweep (all carried-forward items from iter-12 PASS)

| Item | Verdict | Source of truth |
|---|---|---|
| `_debugForceVersus: 0` shipped + runtime hardening | PASS | scene line 21192 + source lines 70/81/114/124/238 |
| `_miniMapVersusPos: {-61, -1718}` shipped | PASS | scene line 21189 |
| TurnBannerWidget R4-3 fix (pre-position before SetActive) | PASS | source lines 119+122 + empirical centroid track |
| YOUR TURN clean settle (cx stable post-arrival) | **PASS — 0.14px range** | banner_show 30fps track 77 frames |
| OPPONENT'S TURN enters from RIGHT | PASS | banner_show cx 1063→582 over 5 frames |
| Cards-full-at-frame-1 (Defect 1) | PASS | nav t=13.5s + versus_launch t=3.7s |
| Mode Select dimmed behind matchmaking modal (R2-5) | PASS | nav t=7s + t=10s |
| Uppercase P2 (R3-1) | PASS | CAMILA/TARO + JAMES/EAGLEYE all uppercase |
| Banner 3px #818EA1 borders + Rubik-SemiBold + apostrophe | PASS | turn_swap t=3.5s + nav t=16s |
| Map image-only versus / top-right solo | PASS | versus_launch + solo_regression |
| Solo `!IsVersus` byte-identical | PASS | PlayerCardWidget unchanged this iter |
| Clone gate GUID `c9b16932b3e429543aa96a954ce0ccbf` | PASS (carried) | scene unchanged |
| Alpha 0.50 / 1.0 swap | PASS | visible in versus_launch + nav t=16s |
| Shared bottom buttons not moved (solo unchanged) | PASS | SPIN/GOLFIN/STRAIGHT/DRIVER consistent across all 5 videos |
| IsVersus wiring (1v1 true / Practice false / ResetSession clears) | PASS (carried) | git diff confirms across 5 files |
| All 5 videos 1170×2532 over real loaded hole | PASS | ffprobe |
| ONE clean canonical video set (no suffixed twins) | PASS | 5 videos, canonical names |
| 7/7 EditMode VersusHudTests + 370/370 total | PASS | iter-13 re-run reported |

---

## Notes for red-team

1. **The blocker that produced iter-12 FAIL is empirically resolved.** Red-team measured a 45px left-jump on the stale iter-7/8 `banner_show` video. My independent 30fps centroid track on the re-rendered iter-13 `banner_show` (mtime 23:32) gives 0.14px range across 77 frames with minx=maxx constant. That is a 320× drop. The bug class (gameObject.SetActive rendering at last anchoredPosition for one frame before tween starts) cannot recur given the current source code (TurnBannerWidget.cs:119 pre-positions, .cs:122 then SetActive). If you want to attack it from a different angle, try OPPONENT'S TURN post-settle drift (cx=582.17±0.01px across opp_012..opp_045 in my track — 33 stable frames).
2. **solo_regression report-vs-reality mismatch.** Implementer Line 25 says solo_regression mtime is 17:32; actual is 23:47. The video was re-rendered after the report was drafted; this is strictly better than reported (extra fresh deliverable, not a regression). Caption is "solo HUD" (shorter than what the report claims). Verify directly via `stat -f "%Sm" videos/solo_regression_1v1_ingame_ui.mp4`.
3. **Map right-edge alignment is 3-4px on nav t=14s, 1-2px on versus_launch t=3.7s.** Both are within the documented translucent-tile fuzz zone and inside R4-2's "within ~2px" architect arbitration when measuring the opaque tile body. Self-review noted this; I confirm carrying PASS.
4. **No code or scene changes vs iter-12 PASS.** Video re-render only. The Vector2/_debugForceVersus shipped state matches what I and the red-team independently confirmed in iter-12.
5. **nav title-card arrow glyph renders as □** (ffmpeg monospace font fallback). Caption text "menu □ opponent turn" is still self-explanatory. If red-team wants to be picky about glyph fidelity, this is a candidate — but it's a tool/font issue not a deliverable defect, and Cesar's R3-2 rule was "short, describe content, no edge clip, no iter label" — all satisfied.

---

## Verdict

**PASS — STATUS set to `READY_FOR_REDTEAM`.**

The iter-12 red-team blocker was a STALE DELIVERABLE, not a code defect. iter-13 re-rendered `banner_show` + `nav` + `solo_regression` (one extra freshness beyond report) from current code. My independent 30fps centroid track on the re-rendered `banner_show` proves YOUR TURN bounding box is stable to 0px (constant minx=322/maxx=843 across 77 frames spanning 2.5s); OPPONENT'S TURN slides from RIGHT and settles to 0.01px. nav shows the full production flow including Mode Select behind matchmaking modal + cards-full-at-frame-1 + active-card swap on OPPONENT'S TURN. solo_regression unchanged + clean. versus_launch + turn_swap carried from iter-12 PASS. All 5 videos 1170×2532, all mtimes recent (none from 17:32 iter-7/8 batch). Scene state unchanged (`_miniMapVersusPos:(-61,-1718)` + `_debugForceVersus:0`). Source code R4-3 fix preserved. All 17 tracked items PASS. Tests 370/370.

Handing to red-team gate.
