# gacha_history Stage 1 — Cesar defects (live, during self-review)

Recorded 2026-07-15 while the self-reviewer was running. These are MUST-FIX in the
next implementer iteration, IN ADDITION to whatever SELF_REVIEW.md lists.

1. **Missing separator between rows (Cesar, 2026-07-15).** The runtime row-spawning
   dropped the inter-row `Divider` lines. Stage 0 had a separator between each static
   row (SPEC §2 nodes `4079:18059`, `4079:18080` — REUSE `Divider.prefab`). The Stage 1
   `GachaHistoryScreenController` spawns rows dynamically but does NOT insert a Divider
   between them. Fix: the controller must place a `Divider.prefab` clone between spawned
   rows (not after the last one), matching the Stage-0 treatment and the node. Cite the
   reused Divider GUID (Rule 19).

Orchestrator-spotted (already in the self-reviewer's brief, repeated here so nothing is lost):
2. Club vs ball rows format date/time + pull-count DIFFERENTLY
   (`GachaHistoryRow.cs` "PULLED yyyy/MM/dd / hh:mm:ss tt / PULLS: N" vs
   `GachaHistoryRowBall.cs` "yyyy-MM-dd / HH:mm UTC / xN PULL"). Unify to the node/spec.
3. Ball-card stat block looks cramped (labels overflow into the ball name). Must match the
   club card's stat-block geometry (STAGE1_SPEC §3).
4. Ball metadata line 2 should show the QUANTITY (STAGE1_SPEC §3c), not skip to the date.

5. **Ball card must structurally MIRROR the club card (Cesar, 2026-07-15).** The club card
   is: a framed image region on top with the club centered, and a DISTINCT BLUE PANEL BELOW
   holding the stat bars. The ball card currently uses ONE bigger ball image filling the card
   with the stats crammed/overlaid and NO panel. Restructure `GachaHistoryRowBall` so it has
   the same two-region layout as `BagClubCard`:
     - TOP: framed image region, ball centered, sized to MATCH the club card's image area
       (not a full-card blow-up). Same rarity-frame footprint / proportions as the club.
     - BOTTOM: the blue stats panel (club card's `Parameters` block geometry, STAGE1_SPEC §3b)
       holding the 5 ball segmented-stat rows.
   The ball image should NOT be bigger than the club image — the two cards must read as the
   same card family. This supersedes the "cramped ball stats" note (item 3) — the crampedness
   is a symptom of the missing panel + oversized image.

6. **Ball card must REUSE the club's exact layer stack (Cesar, 2026-07-15 — supersedes the iter-4 flat-fill approach).**
   Investigation of `BagClubCard.prefab` found the club card is TWO stacked existing sprites, not one composite:
     - **Base card = `Assets/Art/ItemsScreen/BackgroundClub.png`** (GUID `b7789a2078893f746b5c0837bd0151c8`, 181×374) —
       a NAVY rounded-rect card with border that bounds the whole card. (Club `Background` AND `Mask` both use it.)
     - **Rarity frame over the TOP image region only = `Assets/Resources/Rarities/{rarity}.png`** (club `CardTop`;
       COMMON = silver `Common.png` GUID `5d6956d471735654bae7517da045cde6`).
     - **Stats area is TRANSPARENT** — the navy base card shows through under the stat bars (club `StatsPanel` has no Image).
   The gacha BALL card was wrongly using the silver `Common.png` (5d6956) as its ENTIRE background (`Background`,
   `Mask`, AND `CardTop` all = 5d6956), so it has NO navy bounded base — that is the whole "not the same family" defect.
   **FIX (Cesar's choice — reuse the club's layer stack exactly):**
     a. Ball card `Background` + `Mask` → swap sprite to `BackgroundClub.png` (`b7789a2078893f746b5c0837bd0151c8`),
        the navy base card — SAME as the club.
     b. `CardTop` → keep the silver rarity frame (`Common.png` `5d6956...`) over just the TOP image region (the ball
        image sits in it), mirroring the club's silver COMMON top.
     c. **REMOVE the iter-4 flat navy `StatsPanel` Image fill** — set `StatsPanel` back to TRANSPARENT (no Image, or
        alpha 0) so the navy `BackgroundClub.png` base shows through under the 5 ball stat rows, exactly like the club.
     d. Keep the 5 ball segmented stat rows in the transparent lower region; ball image not larger than the club's.
   Result: ball and club share the SAME base card sprite → genuinely the same card family. Pure reuse, no new asset.
   Cite the `BackgroundClub.png` GUID on the live ball `Background`/`Mask` (Rule 19).

7. **Separator vertical gap is asymmetric (Cesar, 2026-07-15).** The gap between a separator and the
   data ABOVE it (top gap) is correct, but the gap between the separator and the data BELOW it (bottom
   gap) is TOO BIG. Make the bottom gap equal to the top gap so each separator sits with equal spacing
   above and below. Likely the row has bottom padding, or the Content VLG spacing + the separator's own
   layout produce uneven space below vs above — measure both gaps (GetWorldCorners) and equalize.
   Verify numerically: top-gap px == bottom-gap px around each separator.

--- Cesar manual review of iter-5 (2026-07-15) — 4 defects, ball card + separators ---

8. **Separator gaps went the WRONG way.** iter-5 equalized every separator gap to ~24px — but that
   equals the OLD BOTTOM gap (the too-big one). Cesar wanted both gaps at the OLD TOP gap value (the
   smaller, correct one he'd approved). Result now: all gaps are uniformly too big, and inconsistent
   between club and ball rows. FIX: find the original TOP-gap value (the one approved before the iter-5
   separator change — check git/prefab history) and set BOTH the above- and below-separator gaps to that
   SMALLER value, uniformly across EVERY row (club and ball). Measure and report top==bottom==<small value>.

9. **Ball image shorter than club + crammed on top; blue (stats) area too big.** Measured geometry:
     - CLUB: `CardTop` (image region) = **181×206**; `StatsPanel` = **157×130.8**; `Portrait` = **134.7×205** (fills CardTop).
     - BALL: `CardTop` = **181×140** (TOO SHORT); `StatsPanel` = size(0,**234**) bottom-stretch (TOO TALL); `Portrait` = **120×120** (too small, crammed at top).
   FIX: match the club's split — ball `CardTop` ≈ **206px** tall, ball `StatsPanel` ≈ **131px** tall (shrink from 234),
   and enlarge the ball `Portrait` to fill the taller CardTop like the club's does (centered, not crammed).
   The image-vs-stats proportion must match the club card.

10. **Ball stats crammed to the left + one icon missing.**
    a. One ball `StatIcon` has `sprite=NONE` (missing). Assign the correct ball-stat icon — cross-reference
       `BallDetailPanel` (the shipped ball stat rows: power, rebound, windResistance, roll, spin) to find which
       of the 5 has no icon and assign the matching sprite it uses.
    b. The 5 ball stat rows are crammed to the LEFT. Make each row span the StatsPanel width like the club's
       stat rows (icon left · bar stretches to fill · value right-aligned). Match the club's stat-row HLayout/widths.

11. **Ball card has NO outline.** CLUB `Rim` uses `Assets/Art/ItemsScreen/Rim.png` (181×374, full-card outline).
    BALL `Rim` points at a DIFFERENT sprite `Assets/Art/Rarities/Rim.png` — so no matching outline renders.
    FIX: set the ball `Rim` sprite to `Assets/Art/ItemsScreen/Rim.png` (SAME as the club), same 181×374 size/anchor,
    so the ball card gets the club's outline. Cite the GUID on the live object (Rule 19).

--- Cesar decisions on iter-6 open items (2026-07-15) ---

10a RESOLVED. Ball Power-row icon → REUSE the club's power icon, which is
    `Assets/Art/RosterScreen/IconStrenght.png` (GUID 1f43a434856f0864db10af5f5bdb34ea) — the exact sprite the club's
    `StatsPanel/StatRow_Power/Image` uses (NOT the non-existent ItemsScreen/IconStrenght path the
    implementer guessed). Assign it to the ball's StatRow_Power StatIcon (currently NULL). Cite the GUID (Rule 19).

9 dead-space: LEAVE AS-IS (Cesar). The empty navy below the ball's 5 stat rows is accepted — the ball
    simply has fewer/shorter stats than the club. Do NOT spread the rows or resize the panel further.

--- Cesar rejection after ARCHITECT_REVIEW_PASS (2026-07-15) ---

12. **Separator gaps STILL asymmetric on CLUB rows — the visible card-to-divider gap, not the box model.**
    Orchestrator measured the RENDERED gaps at runtime (GetWorldCorners, canvas px), card-edge to divider:
      - CLUB rows: gapAbove ≈ **42px**, gapBelow ≈ **6px**  → the club card sits ~18px too HIGH in its row.
      - BALL rows: gapAbove ≈ **24px**, gapBelow ≈ **24px** → symmetric (correct).
    The implementer's prior "24/24 symmetric" was the ROW-CONTAINER box (row RT edge → divider = 0 + 24 padding),
    NOT the visible card. Cesar sees the CARD-to-divider gap, which on club rows is 42/6 — the asymmetry he has
    flagged twice.
    **FIX:** make the CLUB row (`GachaHistoryRow.prefab`) center its `Col1_ClubCard` vertically within the row
    exactly like the BALL row already does (the ball row measures 24/24). Diagnose the difference between the two
    row prefabs (likely the row HLG `childAlignment`, or the card's `anchoredPosition`/`LayoutElement`, or a
    top-align vs middle-align) and match the club row to the ball row's centered layout.
    **Acceptance (measure at runtime, report the numbers):** every divider must have gapAbove ≈ gapBelow for BOTH
    club-club, club-ball, ball-club, and ball-ball adjacencies (all ≈24/24, or whatever equal value results from
    centering). No 42/6.
    Do NOT change the ball row (it's already correct). Do NOT touch the shared `BagClubCard` prefab.

--- Cesar rejection (2026-07-15, after separator fix) — ball card image/stats sizing STILL wrong ---

13. **Ball image area, image size, and stat-bar width do NOT match the club — because prior "fixes" set
    LayoutElement values that the layout IGNORES.** Orchestrator measured RENDERED sizes at runtime
    (GetWorldCorners) AND read both prefabs' RectTransform+LayoutElement setup. The LE.preferredHeight/Width
    the implementer kept setting are IGNORED (the `Background` VLG does not control child size here), so the
    RectTransform `sizeDelta`+anchors are what actually render. Same box-model-vs-render trap as the separators.

    **Measured club vs ball (rendered / prefab):**
    | Element | CLUB (target — replicate) | BALL (current, WRONG) |
    |---|---|---|
    | CardTop | anchor TL (0,1)-(0,1), sizeDelta **(181,206)** → renders 206 tall | center (0.5,0.5), sizeDelta **(181,140)** → renders 140 (LE pref=206 IGNORED) |
    | StatsPanel | anchor CENTER (0.5,0.5)-(0.5,0.5), sizeDelta **(157,130.8)**, LE flexW=1, VLG UpperCenter → renders 157×131 | stretch (0,0)-(1,0), sizeDelta (0,234), LE flex=(-1,-1) → renders **0 wide** ×234 |
    | Portrait | (0.5,1)-(0.5,1), sizeDelta **(134.7,205)** → fills the 206 region | (0.5,0.5), sizeDelta **(157,170)** → shorter, doesn't fill |
    | Stat Bar | sizeDelta.x **(87)** in StatRow HLG (ctrlW=false) | sizeDelta.x **(60)** → too narrow, and moot while StatsPanel=0-wide |

    **FIX — replicate the CLUB card's RectTransform+LayoutElement setup on the ball card (match the MECHANISM, not just numbers):**
    a. Ball `CardTop`: set actual `sizeDelta.y` to **206** (match club image-area height). The image area and blue area must equal the club's.
    b. Ball `StatsPanel`: replace the stretch anchor with the club's: anchor CENTER (0.5,0.5)-(0.5,0.5), `sizeDelta` **(157,130.8)**, LE flexibleWidth=1 — so it RENDERS ~157 wide (not 0). This is what lets the bars span.
    c. Ball `Portrait`: enlarge to FILL the 206-tall CardTop like the club's Portrait fills its region (club 135×205). Keep the round-ball aspect but make it clearly bigger than the current 170 — Cesar: "Ball image is still smaller."
    d. Ball stat `Bar`s: set width to span the StatsPanel like the club (~**87px**), so "stats take all horizontal space." (BallSegmentedBar rebuilds segments to the container width — once StatsPanel is 157 wide and the bar is ~87, the segments fill.)

    **ACCEPTANCE — VERIFY AT RUNTIME with GetWorldCorners and REPORT the rendered numbers (NOT LayoutElement values):**
    ball CardTop rendered height ≈ 206; ball StatsPanel rendered width ≈ 157 (NOT 0); ball Portrait clearly larger/fills the region; ball stat Bar rendered width ≈ 87. Compare side-by-side to the club card — image area, blue area, image size, and bar span must MATCH.
    Do NOT trust LayoutElement.preferredHeight/Width — they are being ignored. Do NOT touch the club row or BagClubCard.

14. **Ball image was cut in half by the card top (position, not size).** Cesar (2026-07-16): "The size
    seems right, the ball itself is too high and cutting in half by the top of the card." After iter-9 sized
    the ball Portrait to the club's 134.7×205 rect, its `anchoredPosition.y` was left at **+8** (center 8px
    ABOVE the CardTop top edge) instead of the club's **-102**, so the mask clipped the ball's top half.
    FIX (orchestrator, main thread): `GachaHistoryRowBall.prefab` Portrait `anchoredPosition.y` 8 → **-102**
    (match the club). Runtime-verified (GetWorldCorners): ball visible-top 34.5px BELOW CardTop top, 36.5px
    above CardTop bottom → fully contained, centered, not clipped. Cesar confirmed "The ball is fine now."
