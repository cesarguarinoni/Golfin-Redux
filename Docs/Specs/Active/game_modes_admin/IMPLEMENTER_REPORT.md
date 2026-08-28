# IMPLEMENTER_REPORT — `game_modes_admin`

**Iteration shape:** `content_pipeline:modes-catalog-and-server-priced-entry-fee`
**Date:** 2026-08-28 · **Author:** Claude Code (architect thread, not the subagent chain)

Built in the SPEC's §5 order, and deployment was treated as a step rather than an
epilogue: the API was deployed and smoked before the admin work started, and the
dashboard was deployed before the live E2E, because the E2E's first acceptance
item requires publishing **in the live admin**.

---

## 1. Deployment proofs

| Surface | Proof |
|---|---|
| API (`playlife-api`) | `flyctl status` → image `playlife-api:deployment-01M13PM5NTDK20FB5E7HKRKFD5`, **v58**, both machines good (was v57 / `…01M13MS0R4MDNNNGK94RNFAX04`) |
| Dashboard (`golfin-admin`) | `wrangler deployments list` → **`429883ff-99ce-495a-b755-f4d5805a2f57`** at 100 %, created 2026-08-28T08:31:15Z |
| Dashboard version stamp | `cf-deploy.sh` stamped **`256f21587`** (clean tree, no `-DIRTY`); the live sidebar footer at `admin.golfin.world` reads `256f21587` — read in-browser, per `reference_admin_version_stamp_is_readable_in_browser`, because Access 302s the curl |

Both were verified from the running system, not from an exit code
(`reference_flyctl_401_false_deploy_failure`).

API smoke: `/health` 200; `/points/spend`, `/progress/level-up`, `/shop/purchase`
all **403-not-404** (mounted, auth-gated); a garbage route 404s;
`/api/v1/content?catalogs=modes` 200 and already serving the new catalog.

---

## 2. The live E2E (SPEC §6 item 1, PIPELINE_HARDENING §21) — **RAN**

Driven through the deployed admin UI in a real browser and a real player token.
Pre-state: balance 894, 41 ledger rows, mirror `practice = 10`.

| Step | Result |
|---|---|
| Publish `practice.entryFee 10 → 15` in the live admin | `modes` v1 → **v2**; drawer showed the exact diff before confirming |
| Mirror written in the SAME request | `golfin_mode_fees.practice → entry_fee 15`, `updated_at` = publish time |
| Delta endpoint | `modes v2`, `practice entryFee 15` |
| Stale client taps ENTER at the old fee (10) | HTTP **200** `{"status":"fee_changed","mode_id":"practice","fee":15}` · ledger **+0**, balance **+0** |
| Second tap at the server's fee (15) | HTTP **200** `{"status":"ok","spent":15}` · ledger **+1**, balance 894 → **879** |
| The ledger row | `amount −15, type spend, description **`mode_entry_fee:practice`**` — per-mode legible |

### The other server branches, also live

| Case | Result |
|---|---|
| `mode_entry_fee:missions` (locked) at its published fee | 200 `mode_locked` · ledger +0, balance +0 |
| `mode_entry_fee:battle_royale` | 200 `unknown_mode` · ledger +0 |
| `mode_entry_fee:` (empty suffix) | 200 `unknown_mode` · ledger +0 — a refusal, not a 500 |
| `mode_entry_fee_refund` (merely starts similarly) | 200 `ok`, debited — a door, not a keyword filter |
| **bare `mode_entry_fee`** (every installed build) | 200 `ok`, debited — **the legacy door is still open, as specified** |

### Rewards panel + the drift warning, also live

* `versus_win.pts 20 → 25` saved on the Rewards panel → audit row
  `points_action_update` by `cesar.guarinoni@gmail.com` on `game_point_actions`,
  `before {"pts":20,…}` / `after {"pts":25,…}`.
* A live `POST /points/earn-game {action:"versus_win"}` then awarded **25**
  (balance 868 → 893) — **no relaunch, no publish**, which is the panel's whole
  claim and the reason its banner says so.
* Publishing `modes` with `practice.rewards 5 → 7` produced
  **`1 warning(s)`**, and the warning was about **versus_1v1 only**:
  *"The 1v1 card advertises 20 RP but versus_win pays 25 (Rewards panel)."*
  Practice's own reward edit drew **no** warning — decoupled card copy, exactly
  as the decision of record requires.

**Live state restored** afterwards: `practice` fee 10 / rewards 5, `versus_win`
pts 20, mirror back to 10. `modes` sits at **v4** — a publish never rewinds its
version (that is the rollback rule, not drift). Post-restore
`export_content.py --catalogs modes --check` is **clean** and `modes.csv` is
**byte-identical** through all four publishes.

---

## 3. Acceptance checklist

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | Publish 10 → 15; stale client → `fee_changed`; second tap debits 15 | **PASS** | §2 table above; ledger row `mode_entry_fee:practice` −15 |
| 2 | Wrong-amount suffixed spend → `fee_changed`, nothing debited | **PASS** | ledger count unchanged (41 → 41) across the refusal |
| 3 | Bare `mode_entry_fee` still debits | **PASS** | live: 200 `ok`, ledger +1, balance −10 |
| 4 | `is_locked` refused server-side; Coming Soon next launch; Missions can go live with no build | **PASS** | live `mode_locked`; `ModesOverlayTests.FlippingLockedOff_MakesAComingSoonModePlayableWithNoBuild` |
| 5 | Rewards edit → audit before/after; next win credits 25; modes publish WARNS the 1v1 card | **PASS** | §2 — all three observed live |
| 6 | Editing practice's reward publishes with NO drift warning | **PASS** | §2 — the only warning named `versus_1v1` |
| 7 | `pts`-NULL actions show the explanatory hint | **PASS** | `client amount` badge on the three NULL rows + the standing hint under the table, EN + JA |
| 8 | An unknown `target` is withheld with a warning, never a dead card | **PASS** | `ModesOverlayTests` — appended, patched-existing, and empty-target cases, each asserting the `WITHHELD` warning |
| 9 | `modes` round-trips: seed → export byte-identical → `--check` clean; `Tools/content` green | **PASS** | md5 `c36e4288…` unchanged before AND after four publishes; `--check` exit 0; 26 tests OK |
| 10 | Full EditMode sweep; backend suite; dashboard build; EN + JA | **PASS** | 1955 / 1952 passed / 0 failed / 3 pre-existing skips · backend 117 passed · `npm run build` green · every new string has an `en` and a `ja` |

**Tripwire-verified**, per `reference_tests_run_ignores_class_filters`: a
deliberate `Assert.Fail` added to each new suite made the sweep report
**1957 total / 2 failed**, both named — so the 20 new tests really run. Removed,
re-run green.

---

## 4. Files changed

See the two commits: `256f21587` (GolfinRedux) and `f5749d4` (playlife).

---

## 5. One thing NOT fixed, and it is not mine

The full `export_content.py --check` exits 1 on a **pre-existing** `texts`
drift: `GACHA_PRIZES_TITLE` and `SHOP_HISTORY_COMING_SOON` are in
`Assets/Localization/LocalizationText.csv` (committed in `a10f46318`, the gacha
task) but not in the `texts` catalog, which sits at 506 rows vs the CSV's 508.
The repo is AHEAD of the catalog, so the fix is a re-seed of those two keys, not
an export. Scoped `--check --catalogs modes` is clean. Left alone deliberately —
it predates this task and belongs to whoever owns `texts`.
