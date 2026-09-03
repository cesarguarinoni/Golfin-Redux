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

## CESAR REJECTED after the reviewer's PASS — two defects, both fixed

Cesar: *"Live Round text is spilling out of the red pill."* He was right, and
chasing it turned up a second, worse one.

### 1. LIVE ROUND pill (what he reported)

`● LIVE ROUND` measures **153.4px** at SemiBold 22; the pill was **150** wide, so
the final D ran into the rounded edge with zero right padding. The JA string is
122.8px and fits, which is why only English showed it.

**The node has the same bug, worse.** In `reference/rounds_active_14077-100447.png`
Figma's own render WRAPS the string to "● LIVE" / "ROUND" and the second line
collides with the venue name beneath. Node 14077:100704 says 150x40, and that
geometry cannot hold its own text. The pill is therefore **180** wide here — a
deliberate, documented deviation honouring the design's intent (the full words)
over its measurements. Verified in the rendered frame: glyphs 152..302 inside a
pill at 138..317, **left pad 14 / right pad 15**.

### 2. The list disagreed with the chip (found while verifying #1)

The screen showed a **GOLF COURSES** chip over a **FOOD & DRINK** list. Cause:

```csharp
string category = Session.HasActive ? "food" : Categories[_category];
```

The PlayerPrefs mirror paints a round on frame one, so the entry fetch asks for
FOOD. When `/activity/active` then says the round is gone — checked out on
another device, or expired — `ApplyState` hid the card but **nothing re-fetched
the list**, leaving food under a golf chip until the player manually switched
category.

This is the SAME SHAPE as the address bug the reviewer surfaced: state derived
from the round is not re-derived when the round changes. Reproduced deliberately
(check in, close the round server-side underneath the running app, leave and
re-enter) and fixed.

**My first fix was wrong and the test caught it.** I gated the re-fetch on
`!_fetchInFlight` to avoid a loop — but the entry fetch is ALWAYS in flight at
exactly that moment, so the guard silently dropped the correction every time. The
re-fetch is now self-correcting: `FetchSpots` re-checks the round state when its
answer LANDS and refetches if it changed while in flight. It converges because
each run reads the current value.

Proven end to end: `HasActive False`, `_listBuiltForActive False`, list flipped
from 5 food rows to 50 golf with TEST Office first.

Screenshots `01`, `03` re-captured; `08_live_pill_detail.png` added.

An earlier attempt at this test was inconclusive for an environmental reason worth
recording: `/activity/active` timed out because the Fly app had scaled to zero
(`/health` took 17s, then 0.04s once warm), and `Session.Refresh` deliberately
keeps the mirror on a failed fetch. The code was fine; the tunnel was cold.

## ARCHITECT_REVIEW_FAIL cleared — acceptance 12 delivered

Red-team blocked on item 12 (motion parity) having zero runtime evidence. Correct
call. Now delivered:

- **Motion invariants:** `gps_rounds_motion_invariants.json` — 12 transitions,
  `fail=0`, Rounds measured both directions (0.257 s / 0.264 s vs 0.250 s).
- **A13:** `gps_rounds_motion_perf.json` — 12 pushes; Rounds 5.28 / 5.21 MB and
  27.3 / 21.2 ms, inside the family envelope.
- **Video: WAIVED by Cesar.** Recording the Rounds screen hard-locks the Mac
  (twice, manual reset both times). See `KNOWN_ISSUE_recorder_lockup.md` — the
  scenario is proven safe with the encoder off, so this is a tooling
  incompatibility, not a defect in the screen. Ship on the objective gates.

Also fixed, from the red-team's secondary finding: `FetchCardSubtitle` set its
once-per-round guard BEFORE the request, so a single failed `/venue/{id}` — a
cold start on a scale-to-zero backend is enough — blanked the resumed round's
address for the life of the round with no retry. A failure now releases the guard.

