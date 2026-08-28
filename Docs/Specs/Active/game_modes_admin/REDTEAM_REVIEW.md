# Red-Team Review — `game_modes_admin` (iter-3)

**Gate:** `golfin-redteam-reviewer` (adversarial) · **Date:** 2026-08-28 20:20 JST
**Verdict:** `ARCHITECT_REVIEW_ESCALATE` — no concrete defect found and every SPEC §6
acceptance item re-verified this pass, BUT the single most dangerous surface (the
live-on-save Rewards panel that sets every player's payout) has zero automated
coverage, and this gate had no mechanism to exercise its guards against the live
system — only source-read + a proof that disk == deployed. Whether an untested,
no-draft-net, all-player-payout panel ships is a ship-with-known-tradeoff decision
the implementer explicitly routed to Cesar, and I agree it is his to make.

My recommendation to Cesar is **SHIP with a fast-follow** (a small vitest suite over
the pure validators), not block — the delivered code is correct and deployed verbatim
and meets every acceptance row. But I will not write the terminal "a hostile reviewer
broke nothing, ship it" PASS on a live-on-save payout path I could verify only by
inspection, so I escalate the tradeoff rather than preempt his call or misroute
out-of-scope test-infra work back to the implementer.

---

## 1. The rollback fix — one read, not re-derived (per kickoff)

Confirmed not regressed from primary source, my own grep + read this pass:
`Tools/admin-dashboard/lib/contentMutations.ts`:
`MIRRORED_CATALOGS = ["characters","modes"]` (297); `mirrorForCatalog` (298) is the
sole dispatcher with exactly two callers — `publishCatalog:396` and
`rollbackCatalog:537`. `rollbackCatalog` body: `fetchVersionSnapshot(toVersion)` (40)
→ `mirrorForCatalog(catalog, snapshot)` (45) **before** → `fail(502)` on mirror error
(47) → `content_rollback` rpc (55). Mirror-before-rpc + abort posture intact. Live prod
corroborates: mirror rows `updated_at 10:41:01.697` precede catalog v6
`10:41:01.81695` by ~119 ms — read live via service key this pass. Not re-derived
further.

## 2. Primary attack — the declared gap (Rewards panel guards nothing exercises)

**Deployed == disk, proven:** `git diff --stat 7337bdf67..HEAD --
lib/rewardsMutations.ts app/api/rewards/` → **empty**. The bytes I read ARE the bytes
serving prod. No runtime/feature-flag config gates a Next.js route handler, so source
behavior is deployed behavior.

**Every probe from the kickoff, traced against the deployed source (`route.ts` `field()`
+ `rewardsMutations.ts` `checkNumber`/`updateRewardAction`):**

| Probe | Path | Result | Guard |
|---|---|---|---|
| `{"pts": -5}` | `field`→ -5 (number) → `checkNumber` `<0` | **400 refused**, versus_win stays 20 | ✓ |
| `{"pts": 1.5}` | `field`→ 1.5 → `checkNumber` `!isInteger` | **400 refused** | ✓ |
| `{"pts": "20"}` (string) | `field` `typeof !== number` → `"bad"` | **400 refused** (never reaches mutation) | ✓ |
| `PATCH /api/rewards/no_such_action` | `fetchRewardAction`→ null → `fail(404)` | **404, no row created** (upsert-free `.update().eq()`) | ✓ |
| `{"maxPerEvent": -1}` / `{"dailyCap": -1}` | `checkNumber` `<0` | **400 refused** | ✓ |
| `{"pts": null}` | `field`→ null → `checkNumber` legal | succeeds → sets NULL (client-amount) | intended |

All guards are correct in the deployed source. **What I could NOT do:** issue these
against `admin.golfin.world`. The route is `checkAdmin()` behind Cloudflare Access;
curl 302s (kickoff acknowledges this), the Access service token is unimplemented, and
this gate has no browser/claude-in-chrome tool available. So "nothing exercises these
guards" is answered by inspection + deploy-diff, **not by execution** — which is
exactly the implementer's declared gap, and I could not close it empirically.

**Sharp edge found (documented, not a live bug):** the route normalizes a *missing*
body key to `null` (`field(undefined) → null`), so a hand-crafted partial
`PATCH {"pts":25}` on versus_win would null `max_per_event` and `daily_cap` —
removing the 200/day cap. This is full-replace, not merge, and the code comments it as
deliberate. The actual panel (`rewards-panel.tsx:186-196`) seeds all three fields from
the current row and always sends the full trio (`JSON.stringify(parsed)` with
pts/maxPerEvent/dailyCap), so no cap-wipe is reachable through the UI. A direct
partial-body caller is the same threat surface the implementer already reasoned about
("the route is reachable without the panel"). Noted, not a blocker.

## 3. Concurrency / atomicity (#3) — examined, invariant holds

Both `publishCatalog` and `rollbackCatalog` do mirror-write **then** rpc as two round
trips. Mirror-BEHIND (catalog value newer than mirror value) IS reachable under two
concurrent `modes` publishes — interleave `mirror_B, mirror_A, publish_A, publish_B`
leaves mirror = A's value, catalog = B's. But in every reachable interleaving the
server prices from `golfin_mode_fees` and **echoes that fee via `fee_changed` before
any debit** (`routers/points.py:480-499`), so the player is never charged an unshown
amount:
- mirror-ahead → player shown a *higher* fee, pays it (bounded overcharge-shown),
- mirror-behind → player shown a *lower* fee, pays less (player benefit).

The skew is transient and self-heals on the next consistent publish. Two operators
editing `modes` simultaneously is also operationally rare (single admin). The residual
"mirror-ahead is the safer direction" the code documents is accurate; mirror-behind,
the case it doesn't name, is harmless. Not a blocker.

## 4. Reason-parse / `MODE_ENTRY_FEE_PREFIX` (#4) — watertight

`routers/points.py:480-501` + `_get_mode_fee` (166). Adversarial cases, all traced:
- `mode_entry_fee::practice` → suffix `:practice` → `.eq("mode_id",":practice")` no
  match → `unknown_mode`, **no debit**.
- colon-in-id `mode_entry_fee:practice:x` → lookup miss → `unknown_mode`, no debit.
- whitespace `mode_entry_fee:  practice  ` → `.strip()` → prices `practice` correctly;
  ledger logs the raw reason (cosmetic only) — never prices A while a debit is logged
  as B.
- exactly 200 chars → `len(mode_id) > MAX_MODE_ID_LEN (80)` fires **before** the DB
  lookup → `unknown_mode`, no debit. Truncation (`reason[:200]`) is after the gate, so
  a suffix can't be silently clipped into a different mode.
- unicode / case (`PRACTICE`) → `.eq` is exact & case-sensitive → miss → `unknown_mode`.
- the only "evasion" (drop the colon) → falls through to the **intentional** legacy
  bare-`mode_entry_fee` debit (SPEC §4, unchanged). A hostile client could underpay
  regardless — outside the spec's honest-client threat model ("a client never wrongly
  *spends* RP"), unchanged from the pre-existing client-asserted model.

`MAX_MODE_ID_LEN = 80` now agrees with the dashboard's `ROW_ID_MAX = 80` (the iter-1
implementer bound fix). No mispricing, no unguarded debit, no injection. Nothing here.

## 5. Report integrity (Rule 6) — re-ran the new numbers myself, no fabrication

| Claim | My re-run this pass | Verdict |
|---|---|---|
| backend 118 passed | `pytest tests/ -q` → **118 passed in 0.37s** | matches |
| `modes` at v6 | PostgREST `content_catalogs` → `published_version=6` | matches |
| cursor `modes=6` | `grep modes content_version.txt` → `modes=6` | matches |
| content 26 tests | `unittest discover Tools/content/tests` → **Ran 26 · OK** | matches |
| versus_win 20 / mirror 10 | live read: `versus_win pts=20 max 20 cap 200`; mirror `practice=10` etc. | matches kickoff |
| mirror-before-rpc | mirror `10:41:01.697` vs catalog `10:41:01.81695` (live) | matches |

No fabricated numbers.

## 6. Confirmed cheaply (per kickoff)

- Scope/bans **verified myself**: `git diff --stat 256f21587..HEAD -- Assets/Scenes/
  Assets/Scripts/Physics/ .../Scenarios.cs Assets/Materials/M_Splash*` → **empty**. The
  modes commit touches only `Assets/Resources/Data`, `ContentRuntime`, `Economy(Runtime)`,
  `UI/ModeSelect`, `Tests`, `Tools/admin-dashboard`, `Tools/content`, `Docs`.
- Rewards code diff since deployed stamp `7337bdf67` → **empty** (disk == deployed).
- API v59 / dashboard stamp `7337bdf67` accepted from prior gates; the empty
  dashboard diff since that stamp proves no code landed.
- Gates 14/16/17/18/19/21 legitimately do not engage — no Unity UI/prefab/mesh/Figma
  node/screenshots. Re-confirmed: the deliverable is a server-priced spend + two
  Next.js panels, no player-facing visual.
- Pre-existing `texts` drift (`GACHA_PRIZES_TITLE`, `SHOP_HISTORY_COMING_SOON`,
  `a10f46318`) is genuine, is why FULL `--check` exits 1, and is a different thing from
  the fixed cursor staleness. Out of scope.

## 7. Three break-attempts and why each failed

1. **Visual/geometric — n/a** (non-visual task); instead I attacked the reason-parse
   for a mispricing/unguarded-debit and the concurrency window for an unshown
   overcharge. Both held: every non-matching reason yields `unknown_mode` with no
   debit; every mirror/catalog skew is echoed via `fee_changed` before any charge.
2. **Guard-bypass on the payout panel** — traced all six kickoff probes plus the
   partial-body cap-wipe through the deployed source; each is correctly refused, and
   the UI can't reach the cap-wipe. Could not break the *code*.
3. **Report fabrication** — re-ran backend/content suites and re-read prod; every new
   number matches. Nothing invented.

The reason this is not a clean PASS is not any of these breaking — it is that the one
surface most worth breaking (live-on-save payouts) is one I could only *read*, not
*run*, and its zero-coverage/no-draft-net posture is a ship decision the implementer
handed to Cesar.

## Decision for Cesar

The delivered work has **no defect I could find** and meets every SPEC §6 item. The
open question is policy, not correctness: **do you accept shipping the Rewards panel —
live on save, no draft/publish net, sets every player's payout — with zero automated
coverage?** It is pre-existing (every dashboard mutation is untested) and out of this
SPEC's scope, and the guards are correct + deployed verbatim. My recommendation: **ship
it**, and file a fast-follow ~1-file vitest over the pure validators (`checkNumber`,
the route's `field`, `mirrorForCatalog`'s row mapping) — small, high-value, and the
right home for the coverage this panel's danger profile warrants. Your call to ratify;
I did not want to make it silently inside a PASS.

## Files touched by this review

| File | Reason |
|---|---|
| `Docs/Specs/Active/game_modes_admin/REDTEAM_REVIEW.md` | This adversarial verdict (replaces iter-1) |
| `Docs/Specs/Active/game_modes_admin/STATUS.md` | Set to `ARCHITECT_REVIEW_ESCALATE` |
