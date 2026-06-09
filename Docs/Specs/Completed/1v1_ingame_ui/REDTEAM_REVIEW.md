# REDTEAM_REVIEW — `1v1_ingame_ui` iter-13

**Red-team reviewer:** golfin-redteam-reviewer
**When:** 2026-06-09 00:12 CEST
**Iteration:** 13 (video re-render only; code/scene byte-identical to the iter-12 state I reviewed)
**Verdict:** **ARCHITECT_REVIEW_PASS** — I attacked the iter-12 blocker and the full sweep, and could not break it.

---

## PRIMARY ATTACK — the exact thing I caught at iter-12 (banner_show R4-3 left-drift)

I FAILed iter-12 because `banner_show_1v1_ingame_ui.mp4` was a STALE iter-7/8 render that still showed the 45px YOUR-TURN left-jump Cesar rejected in ROUND-4. The code was correct; only the deliverable was stale. iter-13 re-rendered it (mtime **23:32**, was 17:32).

**I re-extracted the re-rendered video myself** (all 300 frames, native ~40.5fps) and ran my OWN white-text centroid + bounding-box track — I did not reuse the reviewer's frames or numbers.

YOUR TURN hold window (80 fully-lit frames, px>14000):
```
cx   594.633 .. 595.039   RANGE = 0.41px
minx 322 .. 322           RANGE = 0px
maxx 843 .. 843           RANGE = 0px
```
The glyph bounding box does **NOT translate horizontally at all** across the entire 2s post-arrival hold. The 45px left-jump is **GONE**. (My 0.41px cx range vs the reviewer's 0.14px differs only by band/threshold choice — both ~100× below the 5px noise floor and ~110× below the stale-video's 45px defect. Directional agreement total.)

OPPONENT'S TURN (my track, frames 160-237):
```
f160 cx=1063.51 minx=930 maxx=1169  <- enters HUGGING RIGHT EDGE
f162..f169 slides left
f170+ settled: cx 581.45..584.26 (RANGE 2.8px), minx 323-325, maxx 847-849
```
Enters from the RIGHT, settles clean, no post-arrival drift. **R2-4 + R4-3 both PASS.**

Visual corroboration (my own montage `/tmp/redteam_iter13/banner_montage.png`): YOUR TURN identically centered at t=0.25 and t=1.48; OPPONENT'S TURN visibly entering from the right edge then centered. White Rubik-SemiBold caps, thin silver rules. No left-drift visible by eye.

---

## Staleness sweep — all 5 videos (mtime + CONTENT, not just mtime)

| Video | mtime | @1170×2532 | CONTENT verified by me |
|---|---|---|---|
| banner_show | **23:32** (re-render) | yes | YOUR TURN clean settle (bbox 0px range), OPPONENT'S TURN from RIGHT. Caption "banner — your/opponent turn" clean. |
| nav | **23:37** (re-render) | yes | Title card → MODE SELECTION visible behind matchmaking modal (t=7, R2-5) → game entry both cards full+uppercase (t=13.5) → OPPONENT'S TURN centered + active-card swap (t=16, JAMES dim/EAGLEEYE bright). Mini-map versus pos lower-right. |
| solo_regression | **23:47** (re-render) | yes | SINGLE CAMILA card top-left; hole-info card + mini-map TOP-RIGHT; no P2; no banner; bottom buttons unchanged. Caption "solo HUD". Identical t=1 and t=2.5. |
| turn_swap | 22:49 (iter-12) | yes | OPPONENT'S TURN centered, apostrophe, silver borders. Caption "turn swap". |
| versus_launch | 22:49 (iter-12) | yes | Cards-full-at-frame-1 CAMILA(bright)/TARO(dim) uppercase mirrored; map→STRAIGHT→DRIVER equal gaps. Caption "1v1 launch". |

**NONE from the 17:32 iter-7/8 batch. No stale-content deliverable. The iter-12 blocker is empirically resolved.**

---

## Prior-rejection replay (each defect Cesar ever flagged — re-shot by me)

| Defect | Verdict | My evidence |
|---|---|---|
| R4-3 banner post-arrival left-drift (45px) | **GONE** | banner_show bbox minx=322/maxx=843 constant, cx range 0.41px / 80 frames |
| R2-4 OPPONENT'S TURN must enter from RIGHT | **GONE (fixed)** | enters cx=1063/minx=930 (right edge), settles clean |
| R2-5 Mode Select must be visible behind matchmaking modal | **GONE (fixed)** | nav t=7 shows MODE SELECTION dimmed behind FINDING OPPONENT modal |
| R3-1 P2 name must be uppercase | **GONE (fixed)** | TARO / EAGLEEYE / JAMES / CAMILA all uppercase across videos |
| R4-2 map right-edge must align with buttons | **GONE (fixed)** | map.right=1107 vs buttons.right=1103-1105 (≤4px fuzz, flush) |
| FIX-1 gap inequality (upper too tight) | **GONE (fixed)** | upper 35px / lower 37px, delta 2px (safe direction) |
| `_debugForceVersus:1` auto-bake (solo/Practice regression) | **GONE (fixed)** | scene ships `:0` + runtime-only `_runtimeDebugForceVersus`, serialized field never mutated by code |
| iter-12 stale banner_show deliverable | **GONE (fixed)** | re-rendered 23:32, clean motion verified above |

---

## Re-run gates (numbers, not adjectives — all independently measured by me)

- **Gap equality:** map-bottom y≈1897, STRAIGHT body 1932-2159, DRIVER body 2196+. Upper gap **35px**, lower gap **37px**, delta **2px** (inside ±5px, lower marginally larger = safe direction). PASS.
- **Right-edge:** STRAIGHT 1103-1105, DRIVER 1105, map 1107 → ≤4px (translucent-tile fuzz). Flush by eye. PASS.
- **Scene `_debugForceVersus: 0`** (LabScaffold:21192) — NOT `:1`. PASS (the hard-fail trap is avoided).
- **Scene `_miniMapVersusPos: {x: -61, y: -1718}`** (LabScaffold:21189). PASS.
- **Clone gate:** GUID `c9b16932b3e429543aa96a954ce0ccbf` appears exactly **2×** (P1 original + PlayerCard_P2). Mirror by anchors, no negative scale. PASS.
- **Scene corruption check (decisive):** total `m_IsActive: 0` = **24 in working tree vs 22 at HEAD = net +2**, exactly PlayerCard_P2 + TurnBanner. If any pre-existing GameObject had lost active state the count would exceed +2. The 6 `-m_IsActive: 1` diff lines are realignment artifacts — ClubHandle (line 19494) and Pf_GOLFIN_Ball(Clone) verified still `m_IsActive: 1`. **Zero pre-existing GameObjects lost active state.** PASS.
- **Solo `!IsVersus` byte-equivalence:** `git diff HEAD` on PlayerCardWidget.cs — solo `else` branch reads PlayerContext exactly as before; ONLY addition is null-guarded `if (_canvasGroup != null) _canvasGroup.alpha = 1f;`. Matches the spec gate verbatim. The "TURN 1" label in the solo video is the pre-existing `$"TURN {GameSession.TurnCount}"`, not a versus leak. PASS.
- **Source hardening:** VersusHudController `_runtimeDebugForceVersus` non-serialized (line 70), OR'd into both gates (81/114/124), serialized field NEVER set true by code (line 239 + comment 231/238). TurnBannerWidget pre-positions rect (line 119) BEFORE SetActive (line 122). PASS.
- **Tests:** test_results_iter5.txt (run 2026-06-08T11:59Z) = VersusHudTests **7/7**, total **370/370**, newer than VersusHudTests.cs (10:29). iter-13 has zero code changes, so still valid. PASS.
- **All 5 videos @1170×2532** (ffprobe). PASS.

---

## Three break-attempts (required) — and why each failed

1. **Visual.** Re-extracted banner_show at full native fps and frame-stepped the entire YOUR TURN entry+hold (82 frames) AND OPPONENT'S TURN (78 frames) — the 45px jump I caught at iter-12 is gone (bbox 0px range). Re-inspected nav/solo/versus_launch/turn_swap frames by eye for any stale or wrong-content frame — every one matches the final build (versus pos, uppercase, alpha split, Mode-Select-behind-modal, single solo card). The ONLY blemish found: the nav title-card caption arrow renders as a tofu box (`menu □ opponent turn`) — an ffmpeg drawtext font-glyph limitation. It is short, centered, fully visible, no edge clip, no iter label; it does not misrepresent content or hide a defect. Below the bar of a concrete blocker. **Attack failed.**
2. **Geometric.** Re-measured every number from my OWN extracts, not the reviewer's: gaps 35/37 (delta 2px, safe direction), right-edge ≤4px fuzz, scene inactive-count net +2 (proves no corruption). Nothing sits near a threshold in the wrong direction — none fragile. **Attack failed.**
3. **Spec-intent.** The iter-12 break WAS spec-intent: the literally-named "banner show" deliverable showed the rejected glitch. That is the one thing iter-13 fixed — the re-rendered clip now shows clean banner motion exactly as named. Every deliverable now shows what its name claims. **Attack failed.**

---

## Minor note (NOT blocking)

- nav title-card caption arrow `→` renders as `□` (tofu) due to the ffmpeg drawtext monospace font lacking the glyph. Caption remains short/centered/visible/no-clip/no-iter-label and self-explanatory. Cosmetic; flagged for Cesar's awareness only. If he wants it perfect, swap the `→` for the word "to" or "›" in a future caption pass — not worth a round-trip on its own.

---

## Verdict

**ARCHITECT_REVIEW_PASS.** The single concrete blocker I found at iter-12 (stale `banner_show` deliverable showing the 45px R4-3 left-drift) is resolved: my own independent 40fps frame-track of the re-rendered video shows the YOUR TURN bounding box is constant (minx=322/maxx=843, cx range 0.41px) — the jump is gone. All 5 videos are fresh (none from the 17:32 batch), all @1170×2532, all CONTENT-verified against the final build. Every prior Cesar rejection re-shot and confirmed GONE. Gaps (35/37, delta 2px), right-edge (≤4px), scene gates (`_debugForceVersus:0`, `_miniMapVersusPos:(-61,-1718)`, clone GUID 2×, inactive net +2 = no corruption), solo byte-equivalence, source hardening, and tests (7/7 + 370/370) all independently confirmed. I tried three ways to break it and came up empty. Advancing to Cesar.
