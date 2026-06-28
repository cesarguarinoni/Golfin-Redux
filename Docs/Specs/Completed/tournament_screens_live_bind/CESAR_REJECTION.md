# CESAR_REJECTION — tournament_screens_live_bind

Rejected after `ARCHITECT_REVIEW_PASS` (2026-06-27). Backend wiring is correct and stays — these are
content + fidelity fixes against the two canonical Figma references, which are now in `reference/`:

- Selection: `reference/ref_selection_hi.png` — Figma node `13386-1758`
  (https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/Golfin-Game-Redux?node-id=13386-1758)
- Leaderboard: `reference/ref_leaderboard_hi.png` — Figma node `13414-5598`
  (https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/Golfin-Game-Redux?node-id=13414-5598)

**The references are the source of truth.** A/B every card and header against them. Do NOT carry forward
the prior iteration's strings/spacing as "good enough."

---

## Defect 1 — Tournament names are wrong (raw localization keys leaking)

The cards render `TOURN.KASUMIGASEKI`, `TOURN.HIRONO`, etc. Root cause: `tournaments.csv` `nameKey` column
holds localization keys (`tourn.kasumigaseki`, `tourn.hirono`, …) but there are **no matching entries in
`Assets/Localization/LocalizationText.csv`**, and `TournamentSelectionScreenController.cs:154` does
`string name = def.NameKey;` (the raw key, uppercased by TMP/format).

Fix:
1. Add localization entries (EN + JP) for every `tourn.*` key, with the EN display name **exactly** matching
   the reference:
   - `tourn.kasumigaseki` → **Kasumigaseki Open**
   - `tourn.hirono` → **Hirono Invitational**
   - `tourn.lomond` → **Lomond Championship**
   - `tourn.gotemba` → **Gotemba Masters**
   - `tourn.kisarazu` → **Kisarazu Cup**
   - `tourn.kawana` → **Kawana Fuji Open**
2. Resolve the name through `LocalizationManager.Get(def.NameKey)` in the controller, not the raw key.
3. The venue sub-line should also match the reference (e.g. "Kasumigaseki Country Club · 18 Holes",
   "Lomond Country Club · 18 Holes", "Taiheyo Club Gotemba · 18 Holes", "Kisarazu Higashi CC · 18 Holes").
   Use a localized venue key; do not hardcode.

## Defect 2 — Dates + countdown missing; LIVE status line malformed

The cards show only a bare countdown (or `Round in progress — Hole 0 of 18`). Cesar's note: *"Tournament
dates do not have the exact dates + countdown on upcoming and Live tournaments (Live has a Rounds in
progress indicator that was not in the references)."*

Match the reference per-state status line **exactly** (separator is a middot `·`, not an em-dash; the LIVE
hole number must be the real in-progress hole, never `Hole 0`):

| State    | Reference status line                                   |
|----------|---------------------------------------------------------|
| LIVE (playing)  | `Round in progress · Hole {N} of 18`             |
| LIVE (finished, resolving) | `Round finished · Hole 18 of 18`      |
| OPEN     | `{MMM DD} – {MMM DD} · Ends in {Nd NNh}`  e.g. `Jun 24 – Jun 27 · Ends in 3d 04h` |
| ENDING   | `{MMM DD} – {MMM DD} · Ends in {NNh NNm}` e.g. `Jun 21 – Jun 25 · Ends in 06h 40m` |
| UPCOMING | `{MMM DD} – {MMM DD} · Starts in {Nd}`    e.g. `Jul 02 – Jul 05 · Starts in 8d` |

Key change: the **date range prefix** (`Jun 24 – Jun 27 ·`) is currently absent on OPEN/ENDING/UPCOMING —
add it. Derive both the range and the countdown from `def.StartUtc`/`def.EndUtc`. For LIVE cards, keep the
round-progress line (it IS in the reference) but fix it to the reference format + a real hole number.
Re: Cesar's parenthetical — the reference DOES carry a round-progress line on LIVE cards, so what was
"not in the references" is the malformed `Hole 0` / em-dash version, not the indicator itself.

## Defect 3 — Sponsors are all "GOLFIN"; vary them

`TournamentSelectionCard.cs:156` hardcodes `_eyebrowLabel.text = "GOLFIN PRESENTS";` — it ignores the
`sponsorKey` column that already exists in `tournaments.csv` (PUMA / GOLFIN / GOLFIN / TAIHEIYO / GOLFIN /
GOLFIN).

Fix:
1. Bind the eyebrow to the tournament's sponsor: render `"{SPONSOR} PRESENTS"` from `def.SponsorKey`
   (localized/display-cased), not the hardcoded string.
2. Vary the `sponsorKey` column so the list is a mix — **keep GOLFIN on ~2 of the 6**, give the rest
   distinct made-up/real golf sponsors (e.g. PUMA, TAIHEIYO, plus a couple of NIKE / TITLEIST / CALLAWAY /
   SRIXON / MIZUNO). Display format stays "{SPONSOR} PRESENTS".

## Defect 4 — Selection pills + panel sit too high, overlapping the top bar

The `ALL / OPEN / PLAYING / CLOSED` filter row and the cards panel are pushed up under the R-currency top
bar. In `reference/ref_selection_hi.png` there is clear breathing room: top bar → `TOURNAMENTS` title →
filter tab row → first card, none of it colliding with the currency/settings bar.

Fix the RectTransform offsets so the filter row + panel start below the top bar, matching the reference
vertical rhythm. Also confirm the tab labels read **ALL / OPEN / PLAYING / CLOSED** (the prior build
showed a clipped `ALL … OSED`). Use the **golfin-ui-fidelity** skill (measure → validate → persist) — do
not guess-nudge.

## Defect 5 — Leaderboard sits too low; panel↔sticky gap too short

In the build the leaderboard list panel nearly touches the bottom nav bar, and the gap between the bottom
of the scroll panel and the sticky "your rank" row is too tight. Compare `reference/ref_leaderboard_hi.png`:
the panel ends higher with a visible gap above the sticky row, and the sticky row clears the bottom nav.

Fix: raise the panel / reduce its bottom extent, and increase the panel→sticky gap to match the reference.
Measure the actual gaps (GetWorldCorners) before/after per the golfin-ui-fidelity skill; cite the numbers.

## Defect 6 — Leaderboard sponsor + tournament name don't match the selection card

The leaderboard header shows a hardcoded `KASUMIGASEKI OPEN` + a `SPONSOR NAME` placeholder regardless of
which card was tapped (red-team flagged this as out-of-scope; Cesar is pulling it back in). The header must
reflect the **selected** tournament so the name + sponsor are identical to that tournament's selection card.

Fix: bind the leaderboard header's tournament-name label and sponsor pill from the
`SelectedTournamentId` → `def.NameKey` (localized) and `def.SponsorKey`. Verify by tapping two different
cards and confirming the leaderboard header changes to match each.

---

## Acceptance for the redo
- Names, venues, sponsors, dates+countdown, and per-state status lines on all 6 cards match
  `reference/ref_selection_hi.png` (A/B screenshot in the report).
- Sponsors are varied (GOLFIN on ~2; others distinct), bound from CSV.
- Selection filter row + panel clear the top bar; tabs read ALL/OPEN/PLAYING/CLOSED.
- Leaderboard panel clears the bottom nav with the reference's panel↔sticky gap (cite measured px).
- Leaderboard header name + sponsor match the tapped tournament's card (show two different tournaments).
- Backend wiring, MapCardState tests, and real-entry path remain intact (don't regress the prior PASS).
- Fresh bot video at 1170×2532 over the real flow.
