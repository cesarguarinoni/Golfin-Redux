READY_FOR_REDTEAM

# STATUS — `gps_checkin`

**Current:** `READY_FOR_REDTEAM` — iter-1, 2026-09-03 14:43 JST. `golfin-reviewer`
PASS. Cesar's six asks independently re-derived and verified: (1) ApiEnvelope
`DateParseHandling.None` central + every string-ISO carrier's deserialization
path traced to a DateParseHandling.None-fixed site (no unfixed carrier); (2)
UIFidelityLinter re-run in-editor `fail == 0` on all three prefabs; (3) sort
caret sprite `S_Common_Icon_ArrowBottom` (GUID `57bd1559b589c…`) at 22×22 gold,
loc strings clean, all `● ◎ — – ›` render; (4) `_venueName` wires to a GO
literally named `Venue`, order pin(-108)→venue(-250)→sub(-300)→stats(-366),
glyph bands within 1 px of the node's 167/268/310/385; (5) EditMode 2383/2380/
0/3, `ActivityTimestampFidelityTests` both PASS; (6) `e2e_activity_economy.py`
`=== ALL PASS ===` re-run this pass, auto-expire migration confirmed live,
invariant 0 violations. Live Figma re-pull on `14080:34097` (Rule 9) confirms
SPEC's per-element numbers are current. Bbox containment holds. Scene mutation
clean (single additive commit `64d5061fd` on ShellScene).

Handing to `golfin-redteam-reviewer` — the adversarial gate that is the ONLY
agent allowed to advance to `ARCHITECT_REVIEW_PASS`.

Backend + admin remain DEPLOYED AND PROVEN LIVE. Uncommitted parallel-session
work (`PersistentUIManager.cs`, `UiMotion.cs`, `GpsPolishBuilder.cs`,
`GpsNavBarHighlight.cs`, `Docs/CONTROL_SCHEMES_PLAN.md`, `game_polish_a/`,
`design_consistency_audit/`) is explicitly NOT this task's per Cesar's
kickoff.

## Notes surfaced for the red-team (see `ARCHITECT_REVIEW.md` § Notes)

- Frame 06 (`06_resumed_round_after_restart.png`) is missing the venue address
  sub-line that frame 03 has (東京都中央区晴海) — potential rehydration gap
  on resume; canonical (01) is unaffected.
- Frames 03/06 pre-date the caret sprite fix (captured 12:42; sprite commit
  13:29) and still show the tofu square in those older captures. Canonical
  (01) and detail (07) both show the fixed sprite.
- Report's "3 sites carry string timestamps" undercounts: `SaveData` has four
  string-ISO fields all covered by the same `SaveDataHost.RawDates`
  deserialize. Substantively correct, numerically imprecise — no defect.

## Prior states (for the run log)

- `SELF_REVIEW_PASS` — 2026-09-03 13:28 JST. `golfin-self-reviewer` verified
  all four Cesar asks; surfaced three formal gaps (Rule 14/18/21) that were
  then closed post-verdict (canonical screenshot declared; Figma fidelity
  table added; UI fidelity lint section added). Also caught: sort caret was
  tofu (fixed to sprite atom), and the caret was authored 22×14 on a 72×72
  sprite (linter caught it, now 22×22).

## Changed AFTER the reviewer's PASS — red-team please note

`golfin-reviewer` passed to `READY_FOR_REDTEAM` and surfaced, as a non-blocking
note, that frame 06 was missing the venue address the live card shows. That was
NOT a capture artefact — it was a real bug, and it is now fixed.

**Root cause.** `PaintActiveCard` resolved the address via `SpotSubtitleFor`,
which can only answer from `_spots` — the list currently ON SCREEN. Two ways that
fails, both real:

1. The card paints on entry before `/venue/nearby` has answered, and nothing
   repainted it when the answer arrived.
2. Worse, and why the first fix was not enough: opening a round flips the list to
   FOOD & DRINK, which by definition never contains the golf course being played.
   A RESUMED round therefore could never resolve its address from the list at
   all, and had nothing cached because the process had just started.

**Fix.** The card remembers the resolved address for the open round, is repainted
when the nearby list lands, and — when the list genuinely cannot answer — fetches
that ONE venue with `VenueService.ById`. Verified live across a real play-mode
restart: the resumed card now reads `東京都中央区晴海`. `screenshots/06` re-captured.

**Shape audit** (rule 15) — every field the card paints, and where it reads from:

| Field | Source | Transient? |
|---|---|---|
| `_cardVenue` | `row.VenueName` | no |
| `_cardVenueSub` | **`_spots` lookup** | **YES — this bug** |
| `_cardSince` | `Session.CheckInAt` | no |
| `_cardElapsed` | `Session.Elapsed` | no |
| `_cardPts` | `row.Points` | no |
| `_cardGps` | `Session.Quality` | no |
| `_cardFixes` | `Session.FixCount` / `row.GpsCheckCount` | no |

Six of seven read the round or the session. Exactly one read transient list
state, and it was the one that broke.

Also: frames 03 and 06 predated the caret sprite fix. 06 is re-captured above; 03
still shows the old tofu caret in the list BEHIND the card and should be judged on
the card, or re-shot.

