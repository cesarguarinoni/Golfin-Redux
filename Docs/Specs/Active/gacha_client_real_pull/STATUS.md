DONE_PENDING_CESAR_APPROVAL

Built and PROVEN ON PROD 2026-08-31 (Claude Code, direct implementation — the same route
gacha_admin_catalogs and gacha_server_pull took, not the subagent pipeline).

Full unfiltered EditMode sweep: 2129 passed, 0 failed, 3 skipped (the three pre-existing
HoleCompleteDriver skips). 30 new tests.

THE GACHA IS REAL. `POST /api/v1/gacha/pull` is what rolls a banner now; the mock pool, the
client-side ticket spend and the mock history are deleted, not disabled. Every §7 step was run
live against prod from the Editor as cesar.guarinoni@gmail.com, and the server's own ops panel
agrees with the client on every number: 6 pulls, 2 300 tickets spent, 1 160 dupe RP paid.

WHAT CESAR SHOULD LOOK AT FIRST — two visuals and one judgement call:
  screenshots/01_banner_card_numeric_costs.png   the card: title, art, COST x50 / COST x450,
                                                 and the two guarantee lines bound to the row
  screenshots/06_prizes_x10_mixed_kinds.png      a mixed x10 — every kind on the CLUB card, all
                                                 ten measured at 181x374 scale 1.00. Rebuilt after
                                                 Cesar rejected the scaled-down shop card.

REJECTED ONCE AND REBUILT. §4.3 said to put a non-club prize on the Rewards-Center shop card and
scale it to fit. It does not fit — 978x274 into 183x410 is 0.19 — and Cesar rejected it on sight:
"They should be the same size and shape as club." Every kind now draws on the CLUB card via a new
`BagClubCard.InitializePrize`, which is the pattern `GachaHistoryRowBall` has used to put BALL data
on a BagClubCard since gacha_history Stage 1. Three more bugs fell out of that rebuild (a clone of
an inactive scene object is born inactive, so two slots rendered EMPTY; a slot returning to a club
left a stale card behind; and the ball name printed twice while the item detail wrapped to three
lines). All fixed and re-verified live.

TWO EARLIER BUGS ALSO FIXED (both found by running it, neither by a test):
  • the 5b re-apply was subscribed LAZILY, so the boot refresh fired before anything listened
    and a mid-session publish never landed — the disk cache held gacha_banners v4 while the
    store still served v3. Now [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)].
  • the prize-card entrance animation ended on a hard localScale = 1, silently undoing the
    scale-to-fit. The fit now lives on a slot-sized WRAPPER the animation cannot reach.

DELIBERATELY NOT RUN: §7 step 5's "publish a brand-new banner, then deactivate it". Creating a
banner on prod leaves a row in the published catalog forever (I6 — nothing is ever deleted), and
the mechanism it tests is already proven twice over: the mid-session install is PROVEN LIVE (v4
landed and the card re-priced to 60 without a build) and the append path has its own EditMode
test. Say the word and I will run it.

PROD FOOTPRINT, stated plainly and NOT reverted — these are the first real gacha rows, which is
what this task exists to produce:
  cesar.guarinoni@gmail.com  2 500 tickets granted, 2 360 spent, 140 left
                             478 RP → ~1 100 (dupe payouts), 8 clubs granted
  6 pulls + their prizes in golfin_gacha_pulls / _prizes, pity 49/50
  texts v18 → v19 (nine GACHA_* keys), gacha_banners v3 → v4 → v5 (costX1 60, then back to 50)

AWAITING CESAR: approval, and a verdict on the scaled-to-fit non-club prize card.
