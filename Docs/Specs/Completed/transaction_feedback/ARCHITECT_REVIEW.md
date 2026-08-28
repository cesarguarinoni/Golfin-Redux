# Architect Review — `transaction_feedback`

**Verdict: ARCHITECT_REVIEW_PASS** (2026-08-28, Architect via Cowork)

## Verified against the working tree (not the report)

- `PendingSpend.cs` read in full: two-pass cache-then-write, idempotent Dispose, `alsoDisable`
  restored to cached state. Sound. Unity-null checks use the overloaded `!=`, so a destroyed button
  is skipped rather than thrown on.
- `GeneralShopScreenController.HandleBuy`: `Begin` after the latch, `Dispose()` first line of
  `onResult` before any `Bind`/`Rebuild` — correct ordering (a later dispose would paint BUY over
  OWNED). `LevelUpModalController`: `Begin(confirm, label, cancel)` → dispose inside the
  `LevelUpAsync` callback before `OnServerAnswered`. Same shape.
- `ApiClient`: one `LogCompleted` at each of the four exits; path only (no body/headers), `SLOW`
  branch > `SlowRequestMs` (1500). As specced.
- `PointsSpendGate` diff is a read-only getter; `Spend` untouched. Deviation 1 accepted — it prevents
  a permanently disabled button on a swallowed concurrent spend, which would have been worse than
  the original defect.
- `playlife/backend/fly.toml:10` = `auto_stop_machines = "suspend"`. Report quotes `fly status`
  `suspended` after 7 m 20 s and 5.20 s → 1.18 s cold/resume.
- Canonical screenshot `01b_generalshop_buy_pending_crop.png` read: PUTT ACE BUY in disabled tint
  reading `…`, MIKE MILLAR below it bright `BUY`. Matches the frame probe.

## Accepted as-is

- Sites 2–6 have no live picture. The report says so plainly rather than claiming otherwise; the
  wiring is the same helper on the same path and is visible in the diff, plus 9 green unit tests.
  Accepted — an irreversible level-up/tournament entry on Cesar's live account is not a price worth
  paying for a screenshot. Cesar will see these in normal play.
- `insufficient` / `price_changed` not exercised live: the pre-check makes `insufficient`
  unreachable from the client, and `price_changed` needs an admin publish mid-session. Covered by
  the Dispose tests. Accepted.
- Deviations 2–4: all narrowing or defensive, none change specced behaviour.

## Open item — CLOSED 2026-08-28

Warm pair measured by Code (report § Warm purchase): 586 ms then **246 ms**, second tap fired on the
same tick the first answered. 246 < 400 → keep-alive follow-up CLOSED. Original note kept below.

### (original)

- The 1383 / 1080 ms purchases each paid a *resume* (manual probes let the machine suspend between
  them). A back-to-back pair on a hot machine should land near the 207 ms ack. Cesar: buy two
  stacking balls within a few seconds of each other and read the two `[ApiClient] POST
  /api/v1/shop/purchase` lines. If the second is > 400 ms, the §8 keep-alive follow-up opens;
  otherwise it closes.

## Rule 13

Files outside the spec folder = the 14 listed in IMPLEMENTER_REPORT + `Docs/AI_CONTEXT.md`.
`Docs/Reports/content_art.txt`, `Docs/Versioning/last_uploaded_build.txt` were dirty at kickoff
(not this task). Nothing is committed yet — Code commits on Cesar's DONE.
