# CESAR_REJECTION — `1v1_ingame_ui` (iter-3 ARCHITECT_REVIEW_PASS → rejected)

**Rejected:** 2026-06-08, by Cesar in chat, after the red-team gate passed iter-3.
**Routing:** STATUS → `CESAR_REJECTED` → back to golfin-implementer (iter-4).

The pipeline (self-review + reviewer + red-team) missed 4 visual/behavioral defects and rubber-stamped two video deliverables that were captured over an **empty scene** despite the SPEC + kickoff both mandating a real loaded hole. Fix list below. Each is a hard gate for iter-4 — do not advance until ALL six are resolved AND re-verified on a real course.

---

## Defect 1 — Cards populate AFTER the banner; match must start with both cards FULL

**Observed:** On 1v1 launch the "YOUR TURN" banner plays first, and only then do the two player cards fill in their data (name / level / turn). (Same lazy-seed smell seen in the solo clip: card read "PLAYER / Lv 1" at t=1s, then "CAMILA" at t=2s.)

**Required:** When the match starts, BOTH cards are already fully populated (P1 + P2 name/level/portrait/rarity/turn) — full data visible from frame 1, BEFORE any banner. The banner is an overlay on top of an already-complete HUD, never the trigger for data binding.

**Likely cause / fix:** Data binding must happen at match init (populate `MatchContext.Players[0]` and `[1]` + call `Refresh()` on both cards) at `Start`/scene-load, independent of and prior to the banner coroutine. Make sure `PlayerContextPopulator` (slot 0) and `MatchmakingModalController` (slot 1) have both fired and `MatchContext.Raise()` has been called before the game HUD is shown. The banner's `Show()` must NOT be what causes the cards to refresh. Verify in the navigation video (Defect 6): both cards are full the instant the game view appears.

---

## Defect 2 — Banner is missing the top + bottom silver outline

**Observed:** The banner band has the translucent gradient but NO top/bottom border. The 3px silver outline is absent.

**Figma ground truth** — node `4094:26038` (I pulled it directly; sub-nodes `25990` / `25986` / `25987` / `25988`):
- Container `25990`: **top border 3px solid `#818EA1`**, width 1170, flex-column.
- "Bottom" `25986`: **bottom border 3px solid `#818EA1`**, height **210**, width **1170**.
- "Text" `25987`: fill = vertical gradient **`rgba(19,52,83,0.5)` (top) → `rgba(9,27,51,0.5)` (bottom)**, full 1170×210, content centered, horizontal padding 318 / vertical 9.
- Reference URL: https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/Golfin-Game-Redux?node-id=4094-26038

**Required:** Add BOTH a 3px `#818EA1` top border and a 3px `#818EA1` bottom border to the banner band, spanning the full 1170 width, crisp (not blurred by the gradient). Implement with two thin Image strips (or a 9-sliced border sprite) — whatever renders sharp 3px lines. Verify against the Figma screenshot at full res.

---

## Defect 3 — Banner font does not match Figma; do NOT faux-bold

**Observed:** The banner text font does not match the Figma design.

**Figma ground truth** (node `4094:25988`): font = **Rubik Medium**, **128px**, white `#FFFFFF`, letter-spacing **−2.56px**, centered, `word-break: break-word`.

**Required:** Assign a REAL Rubik TMP font asset that matches the Figma weight. Cesar's explicit instruction: **do NOT manually bold it** — i.e. do not enable TMP's faux-bold style flag / `<b>` to fake weight. If matching the design requires the Rubik **Bold** weight, assign the actual Rubik-Bold TMP Font Asset (generated from the real Rubik-Bold TTF); do not faux-bold a Medium/Regular asset. The Figma layer reports Rubik **Medium** — pull `4094:26038` yourself, confirm the weight, find the matching Rubik font asset already in the project (grep `Assets` for `Rubik*SDF`/`Rubik*.asset`), and assign it. A/B the rendered banner against the Figma screenshot until the glyph shapes/weight match.

---

## Defect 4 — "OPPONENT'S TURN" overflows the banner

**Observed:** The opponent's-turn text is too big and runs outside the banner band.

**Required:** Enable TMP **auto-size (best fit)** on the banner label — max size **128px** (so short strings like "YOUR TURN" / "FAIRWAY" stay at design size), min size low enough that the longest string ("OPPONENT'S TURN") fits inside the text area (1170 − 2×318 = **534px** wide). The text must never spill past the band horizontally or vertically. Verify with the longest expected string actually set.

---

## Defect 5 — Videos were recorded over an EMPTY scene

**Observed:** The four deliverable videos were captured with no course behind the HUD (empty/flat LabScaffold). The SPEC's Smoke-evidence section AND the kickoff both required capture **over a real loaded hole, not flat ground** (standing rule `feedback_capture_resolution_iphone14`).

**Required:** Re-record ALL videos with a real course loaded behind the HUD — additively load a `Hole_NN_Geo` scene (e.g. `Hole_16_Geo`, matching the mini-map) so the fairway/green/sky are visible behind the cards and banner, exactly as in production. Full iPhone-14 **1170×2532**, never the 540p downscale. The HUD must read clearly over the real course.

---

## Defect 6 — Add a full main-menu → opponent's-turn navigation video

**Required (NEW deliverable, in addition to the per-element clips):** One continuous capture that walks the REAL production flow:
**Main menu → select 1v1 mode → matchmaking ("OPPONENT FOUND") → into the game (both cards FULL, P1 active) → trigger the opponent's turn (active swap to P2 + "OPPONENT'S TURN" banner).**

- This is the end-to-end proof that the real route (not a debug force) populates both cards and drives the active/inactive opacity + banner.
- Phase-1 note: the opponent's-turn *trigger* itself may still come from the Phase-1 debug control (turn-flow is Phase 2), but everything UP TO entering the game must be the genuine menu→matchmaking→game navigation. Both cards must already be full when the game view appears (ties to Defect 1).
- Full 1170×2532, over the real loaded hole, captioned via `Docs/Scripts/build_bot_video.py`. Name it something like `nav_menu_to_opponent_turn_1v1_ingame_ui.mp4`.

---

## Do NOT regress (these passed and are correct)

- Step-0 clone gate: P2 = clone of P1 (GUID `c9b16932b3e429543aa96a954ce0ccbf`, `PlayerCard_P2`).
- Solo-unchanged: `PlayerCardWidget` `!IsVersus` branch byte-identical to HEAD.
- P2 mirror by anchors (no negative scale), chip text reads L→R.
- Inactive 0.50 / active 1.0 opacity swap via `MatchContext.SetActive`.
- IsVersus wiring on both 1v1 routes + Practice false + ResetSession; MatchContext contract; 7/7 EditMode tests.
- `_debugForceVersus` shipped false.

## Pipeline-miss note (for reviewers)

The silver borders (Defect 2) were an **explicit SPEC token** ("Borders: top + bottom 3px solid #818EA1") and the red-team claimed "Figma 4094:26052 match" — yet the borders were absent. The two re-shot videos (Defect 5) were accepted despite an empty background that the SPEC + kickoff explicitly forbade. iter-4 review must (a) diff the banner borders pixel-for-pixel against Figma `4094:26038`, (b) confirm a real course is visible behind the HUD in every video, (c) confirm both cards are full at game-entry, before the banner.

---

# CESAR_REJECTION — ROUND 2 (iter-5 ARCHITECT_REVIEW_PASS → rejected again)

**Rejected:** 2026-06-08 (round 2), by Cesar in chat, after iter-5 passed the red-team gate.
**Routing:** STATUS → `CESAR_REJECTED` → back to golfin-implementer (iter-6).

Five more fixes. Figma ground truth for the map (fixes 1+2) pulled directly by the architect — see `reference/figma_1v1_hud_13177-1937.png` (full HUD) and `reference/figma_bottomright_map_zoom.png` (3× zoom of the bottom-right stack). Figma frame: `13177:1937`, file `5gEAHjl6xAtW8iYY7NMvWd`.

## R2-1 — Map must sit ABOVE the Fade/Draw button (currently below it)

**Figma bottom-right vertical stack (top → bottom):** **[Map image] → [FADE/DRAW button] → [DRIVER button]**, flush to the right edge. The relocated versus map currently renders UNDER the Fade/Draw button. Move it ABOVE the Fade/Draw button (top of that right-edge stack). Use the `golfin-ui-fidelity` skill (measure→validate→persist) — measure the Fade/Draw button's RectTransform and place the map directly above it with the Figma gap.

## R2-2 — Map must be ONLY the map image (drop the data card to its left)

In the current build the relocated map widget is a composite: the top-down hole-map image PLUS a hole-info data card to its LEFT (LOMOND / HOLE 16 - REGULAR / PAR 5). Figma shows ONLY a small rounded-rect tile containing the green top-down hole map — no text/data panel. For the **versus** layout, show ONLY the map image; hide/exclude the hole-info data card. (Solo HUD unchanged — this only affects the versus-relocated map.)

## R2-3 — "OPPONENT'S TURN" banner text still too big (nearly touches the screen edges)

At iter-5 auto-size settled to ~92px and the text spanned x≈7..1159 — only ~7px margin each side. Cesar: the fonts almost touch the sides of the screen. Give the banner text comfortable horizontal margins so the longest string ("OPPONENT'S TURN") never comes within a clear gap of the screen edges (Figma's band uses 318px horizontal padding → 534px text area; at minimum enforce a generous side margin / lower the auto-size max so the text sits well inside the band, not edge-to-edge).

## R2-4 — Replace the banner drop-down with a horizontal SWIPE; fix the mid-anim glitches

Current drop-down animation malfunctions on "YOUR TURN": it stutters (stops for 1–2 frames) and the **font size changes partway through** (the TMP auto-size recalculating mid-slide). Two changes:
- **New motion:** swipe IN from the **LEFT** for **"YOUR TURN"**, swipe IN from the **RIGHT** for **"OPPONENT'S TURN"** (then hold, then swipe out). No more top drop-down.
- **Fix the font-size jump:** set the text AND let auto-size fully resolve the final font size BEFORE the swipe begins, so the glyph size is stable for the entire animation (no resize mid-swipe). No stutter — smooth single-tween swipe.
- Re-shoot the banner_show video showing BOTH "YOUR TURN" (from left) and "OPPONENT'S TURN" (from right) animating cleanly, full size over a real hole.

## R2-5 — Matchmaking modal must show the launching screen behind it (not an empty background)

When the matchmaking modal opens, it currently draws over an EMPTY/opaque background. Expected: the screen it was launched FROM (e.g. Mode Select) stays visible (dimmed by the modal's semi-transparent backdrop) behind the modal — standard modal behavior. Diagnose in `Scripts/UI/Matchmaking/MatchmakingModalController.cs` (`OnShow` hides home panels; `ModalController` backdrop) and the launch routes (`ModeSelectScreenController` / `ModeCarouselController` where `Open()` is called): find why the underlying screen isn't rendered behind the backdrop (launching screen deactivated? opaque backdrop image? canvas/sortingOrder hiding it?) and fix so the launch screen shows through. Verify in the re-shot nav video.

## Do NOT regress (round-1 fixes + originals — all confirmed correct)
- Banner top+bottom 3px `#818EA1` borders; Rubik-SemiBold SDF (no faux-bold); "OPPONENT'S TURN" apostrophe; real-course backgrounds in all videos; cards full at frame-1; OPPONENT FOUND modal visible in nav.
- Clone gate (P2 = GUID `c9b16932b3e429543aa96a954ce0ccbf`); solo `!IsVersus` byte-identical; P2 mirror by anchors; alpha 0.50/1.0; IsVersus wiring; MatchContext contract; 7/7 tests; `_debugForceVersus:0`.

## Pipeline-miss note (round 2)
The reviewers claimed "Figma 13177:1937 match" yet missed the map being BELOW (not above) the Fade/Draw button and the map carrying an extra data card (R2-1, R2-2 — both visible in the Figma side-by-side). The matchmaking-modal empty-backdrop (R2-5) and the banner animation stutter/resize (R2-4) were not caught despite being in the nav + banner videos. R2-3 (font margin) and R2-4 (swipe direction) are partly refined design directives. iter-6 review must Figma-diff the bottom-right map stack (above fade/draw, image-only) and frame-step the banner animation for stutter/resize + correct swipe direction, and confirm the launch screen shows behind the matchmaking modal.

---

# CESAR DIRECTED POLISH — ROUND 3 (after iter-7 ARCHITECT_REVIEW_PASS)

**2026-06-08:** iter-7 passed both gates. Cesar approved PENDING a small polish (chose "Uppercase + re-caption"). NOT a quality rejection — the HUD + all 11 tracked items passed; the red-team itself surfaced these two. Routing back via CESAR_REJECTED only because that's the loop-back mechanism. No review-miss logged.

## R3-1 — Uppercase the opponent name (spec compliance)
SPEC §2 mandates opponent `DisplayName` = `ToUpperInvariant`. P1 renders uppercase (CAMILA) but the P2 card shows the gamertag verbatim (e.g. "SwingMst") — `MatchmakingModalController.cs:~453-457` only uppercases in the fallback branch. Fix: uppercase the opponent DisplayName on the MAIN path too, so P2 matches P1 (e.g. "SWINGMST"). Confirm "SWINGMST" (or whatever opponent) fits the chip width at the existing font (it's short; verify no overflow).

## R3-2 — Fix the video captions (presentation)
The iter-7 re-shot clips have bottom captions (a) clipped off both screen edges and (b) mislabeled "Iter-6" though content is iter-7. Per the standing caption rule (`feedback_caption_videos_unobtrusively` + `reference_video_caption_tool` — use `Docs/Scripts/build_bot_video.py` textfile idiom): re-caption within safe margins (wrap/position so nothing clips at the edges) and drop the wrong iter label — caption should describe what the clip shows, not the iteration number. Frame-extract to verify the caption is fully visible before declaring done.

## Re-shoot
Re-record/re-caption the affected videos (any that show the P2 opponent name: versus_launch, turn_swap, nav; plus banner_show if its caption was clipped) over a real hole at 1170×2532, overwrite the canonical `*_1v1_ingame_ui.mp4` names, keep ONE clean set (no suffixed twins).

## Do NOT regress
Everything from rounds 1+2 (all 11 items) — especially the R2-4 swipe directions, R2-5 modal backdrop, map position/content, banner borders/font/margins, cards-full-at-frame-1, clone gate, solo byte-equivalence, alpha 0.50/1.0, tests 7/7, `_debugForceVersus:0`.

---

# CESAR DIRECTED POLISH — ROUND 4 (after iter-8 ARCHITECT_REVIEW_PASS)

**2026-06-08:** iter-8 passed both gates. Cesar requested one spacing nudge. NOT a quality rejection; no review-miss logged (no prior spec target for this gap — it's a new spec detail).

## R4-1 — Map↔Fade/Draw gap must equal the Driver↔Fade/Draw gap (24px in Figma)
In the bottom-right vertical stack [Map] → [FADE/DRAW] → [DRIVER], the vertical gap between the MAP and the FADE/DRAW button must equal the gap between the DRIVER (club-selector) button and the FADE/DRAW button, which is **24px in Figma** (`13177:1937`).

- iter-7 red-team measured the current map↔fade/draw gap at ~28px (map bottom y≈1922, fade/draw top y≈1950). Target = 24px.
- Use the `golfin-ui-fidelity` skill: MEASURE the live Driver↔Fade/Draw vertical gap in LabScaffold first (confirm it's 24px; if the live value differs from Figma's 24, match the live driver-gap so the two gaps are visually identical — the intent is "same gap above and below the fade/draw button"), then set the map's versus anchoredPosition (`VersusHudController._miniMapVersusPos`, currently ≈(-48,-1744)) so the map↔fade/draw gap equals it. Nudge the map DOWN ~4px (more-negative y) to go 28→24, but measure to be exact.
- Re-shoot the affected videos (versus_launch + nav at minimum — any that show the bottom-right stack) so the corrected spacing is visible; re-caption cleanly (no edge clip, no iter label); keep ONE clean canonical set.

## Do NOT regress
All round-1/2/3 items (the 11 + uppercase name + clean captions). Map must stay ABOVE fade/draw, image-only, flush right. Solo map unchanged.

### R4-1 CLARIFICATION (Cesar decision, 2026-06-08)
Self-review iter-9 measured the LIVE Driver↔Fade/Draw gap at ~33-40px (NOT 24px — the build's bottom-button row differs from Figma). That row is the SHARED shot-control UI (solo + versus), so changing it would touch the solo HUD. Cesar's decision: **"Match map to live ~36px"** — do NOT touch the shared driver/fade-draw buttons (solo stays unchanged); instead INCREASE the map↔fade/draw gap so it equals the LIVE driver↔fade/draw gap (visually identical gaps above and below the fade/draw button). Target = the measured live driver-gap value (~36px), NOT 24px. The map currently sits at a 24px gap (`_miniMapVersusPos.y = -1728`) — move the map UP so its gap matches the driver gap.

---

# CESAR ADDITIONS — ROUND 4 (mid-iter-10, Cesar watched iter-9 videos)

Cesar interrupted the iter-10 dispatch to add 2 fixes (didn't want to wait a full iteration). The interrupted iter-10 implementer had only applied the R4-1 gap nudge (`_miniMapVersusPos.y` -1728→-1716, ~36px) + a HEARTBEAT/STATUS marker — no code touched, scene consistent, nothing broken. iter-10 now bundles THREE fixes:

## R4-1 (carried) — map↔fade/draw gap = live driver↔fade/draw gap (~36px)
Cesar's decision: match the map gap to the LIVE driver gap (NOT 24px; do not touch the shared bottom buttons / solo). `_miniMapVersusPos.y` was nudged to -1716 — VERIFY both gaps are equal (measure from VISIBLE button edges) and adjust if off.

## R4-2 (NEW) — map RIGHT border must ALIGN with the buttons' right border below it
The map is not just missing the 36px gap — its RIGHT edge does not line up with the right edge of the FADE/DRAW + DRIVER buttons beneath it. Measure the bottom-button column's right edge X and set the map's X (`_miniMapVersusPos.x`, currently -48) and/or confirm its width so the map's right border is flush with the buttons' right border. Right edges must align.

## R4-3 (NEW) — "YOUR TURN" banner post-arrival left-drift glitch
OPPONENT'S TURN animates perfectly. But YOUR TURN: AFTER it's fully on screen (centered), both the banner AND text move LEFT for a few frames, then move back RIGHT to center. Reproduce by frame-stepping the iter-9 YOUR TURN banner. Root-cause it (leading hypothesis: the active-swap/show path re-invokes `Show()` while the banner is still centered → `BannerRoutine` snaps the rect to startX=-canvasWidth = off-screen LEFT → slides back to center, reading as "centered → jumps left → slides back right". Other candidates: a one-frame center flash before the slide, or an auto-size/offsetMin-offsetMax re-layout after `enableAutoSizing` re-enables). Fix so YOUR TURN settles cleanly at center with ZERO post-arrival horizontal movement — identical clean behavior to OPPONENT'S TURN. Frame-step both before/after to prove it.

## Do NOT regress
Everything prior (uppercase P2 name, clean captions, banner borders/font/margins, OPPONENT'S-TURN-from-right swipe, Mode Select behind matchmaking modal, map above fade/draw + image-only, cards full at frame-1, clone gate, solo `!IsVersus` byte-identical, solo map top-right unchanged, alpha 0.50/1.0, 7/7 tests, `_debugForceVersus:0`).

---

# ARCHITECT ARBITRATION — iter-10 ESCALATE (2026-06-08)

Self-review iter-10 escalated: R4-3 PASS; R4-1 + R4-2 FAIL on pixel measurement; `_debugForceVersus:1` regression. Architect resolution (no need to re-ask Cesar — his intent "equal gaps above/below fade-draw" is unambiguous; the exact px is a measurement, not a decision):

- **R4-1:** the live STRAIGHT(fade/draw)↔DRIVER VISIBLE gap measures **28px** (iter-10 self-review, consensus across x=1000/1060/1090), NOT the ~36px Cesar eyeballed. Target = the REAL measured driver gap. Set the map↔fade/draw gap EQUAL to the measured driver gap (measure both the SAME way on the final rendered frame; they must match within ~2px). Map gap is currently 36px → move the map DOWN ~8px so it equals 28px (or whatever the definitive driver-gap measurement is).
- **R4-2:** map right edge x≈1111 vs buttons x≈1108 → map protrudes 3px. Nudge `_miniMapVersusPos.x` (or sizeDelta.x) so map.right == buttons.right within ~2px.
- **`_debugForceVersus` REGRESSION (must-fix):** LabScaffold.unity:21192 ships `_debugForceVersus: 1` — the capture workflow forced versus and the scene was SAVED in that state. Reset to `0` AND harden the capture flow so it never re-persists the runtime mutation (reset the field + re-save after capture, OR drive the capture's versus override at runtime only without touching the serialized field, OR don't save the scene while the flag is forced). This is the same scene-mutation-during-capture hazard class as the iter-12 LabScaffold corruption lesson — fix it properly, not just flip the bit.

---

# ARCHITECT NOTE — iter-11 self-review (gap overcorrected + caption regression)

iter-11 status: FIX2 right-edge PASS, FIX3 debug-flag PASS (properly hardened via non-serialized `_runtimeDebugForceVersus`), R4-3 banner PASS. TWO items remain — both surgical:

1. **GAP overcorrected.** The driver↔fade/draw VISIBLE gap is ~33px (consensus iter-9: 33-40, iter-11: 33; iter-10's 28 was an outlier). iter-10 at `_miniMapVersusPos.y=-1716` gave a 36px map gap (close); iter-11 moved to -1728 → 22px (now ~11px TOO SMALL). Set y back to ≈ **-1718** (UP ~10px) so the map's visible gap ≈ 33px. **This is ±5px-noise territory — the implementer must validate BY EYE on the rendered frame that the gap above fade/draw LOOKS equal to the gap below it, not just pixel-count.** Do NOT touch x (-61, right-edge already aligned).

2. **CAPTION regression on 2 videos** (versus_launch, turn_swap): both re-captioned with clipped text + an "iter-11" label — violates R3-2. STANDING CAPTION RULE going forward: captions must be SHORT (≤~30 chars), describe the clip ("1v1 launch", "turn swap", "banner — your/opponent turn", "solo HUD", "menu → opponent turn"), contain NO iteration number, be wrapped if needed, and be frame-verified to have NO edge clipping. The 3 unchanged videos (banner_show, solo_regression, nav) keep their clean captions.
