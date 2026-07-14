# QUEUED — gacha_history: ball reward card should show BALL STATS

**Filed:** 2026-07-14 (Cesar, during gacha_history Stage 0 review — "File it for later").
**Parent task:** `gacha_history` (Gacha pillar, screen 2 of 3).

## The ask
The history row's **ball** reward card currently renders the ball art only. It should show **ball stats**
the same way the **club** card shows club stats (the stat rows + values block on the card).

- Club row card: shows distance (e.g. `150 yd`) + the stat bars (Power / Accuracy / Lie Res / Loft /
  Durability) with numeric values — reused from `BagClubCard`.
- Ball row card (`GachaHistoryRowBall`): currently art + name + amount badge only — **no stats block**.

## What's needed
Give the ball card its own stats block, mirroring the club card's treatment but with the **ball's** stat
set (whatever the ball data model exposes). Reuse the ball card atom from the Inventory/Bag ball card if
one exists (Rule 19 — clone, don't fabricate) rather than hand-building a stats panel.

## Why deferred
Out of scope for gacha_history Stage 0 (static posing). Cesar explicitly deferred it. Pick this up either
in a later gacha_history stage or as a follow-up order once the ball stat model is confirmed.
