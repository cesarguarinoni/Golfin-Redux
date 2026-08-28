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

## 1b. The migration's own verification, read back from prod

Cesar applied `2026_08_28_golfin_mode_fees.sql` and returned the grants line
(`anon_or_authenticated_grants = 0`); the five seeded rows were re-derived
directly over PostgREST rather than confirmed from the artifact asserting them
(`feedback_derive_dont_confirm_evidence`):

```
driving_range 0 / locked    missions 0 / locked    practice 10
tournaments 0               versus_1v1 0
```

The RLS half was NOT derivable that way — PostgREST cannot reach `pg_class`, and
`service_role` bypasses RLS so a successful select proves nothing about it. Read
back from prod on request, alongside the rest of the RLS-on/no-policies family so
the new table is checked against the shape it is meant to match:

| table | rls_enabled | policy_count |
|---|---|---|
| `content_drafts` | true | 0 |
| `content_rows` | true | 0 |
| `game_point_actions` | true | 0 |
| `golfin_fake_players` | true | 0 |
| **`golfin_mode_fees`** | **true** | **0** |

Zero policies with RLS on is deny-all for `anon` and `authenticated`; combined
with the revoked grants, nothing but `service_role` can read or write the fee
mirror. (`reference_supabase_rls_lint_false_positive` — the Supabase linter warns
on `create table` read in isolation; `pg_class` is the answer.)

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

## 4b. One defect found by writing this brief

PIPELINE_HARDENING rule 15's corollary — *do not dispatch the next gate while
holding an unexamined suspicion* — earned its keep before the gate even ran.

`MAX_MODE_ID_LEN` (routers/points.py) was **60**. `ROW_ID_MAX`
(lib/contentValidate.ts) is **80**. So an operator could create a `modes` row
with a 61–80 character id, publish it, have it mirrored into `golfin_mode_fees`,
and have the client render it as a real card — and every tap would be refused
`unknown_mode`, with nothing anywhere naming the length as the cause. A mode
nobody can pay for and no way to find out why.

Unlikely to be reached (the longest shipped id is `driving_range`), which is
precisely why it would have been miserable to diagnose the once it was.

Fixed: bound raised to 80. The regression test asserts `>= 80`, not `== 80` — the
property is "not tighter than the surface that mints the ids", not "80 is
correct". **Tripwire-checked:** reverting the constant to 60 fails it (1 failed /
16 passed); restoring passes 17. Backend suite now **118 passed**.

I swept the other client↔server bounds in this task for the same shape and they
agree: `entryFee >= 0` (validator) vs the table's `check (entry_fee >= 0)`;
`locked` accepted as true/false/1/0/blank by the validator, mapped to true on
`"true"`/`"1"` by the mirror, and read the same way by `ContentFields.GetBool`.
`order` has no server counterpart to disagree with.

---

## Rejection follow-up — red-team iter-1 (`ARCHITECT_REVIEW_FAIL`)

The red-team gate found a real blocker that three parties before it (me, the
self-reviewer, the reviewer) all missed. Per-defect verdict:

| Defect | Verdict | Evidence |
|---|---|---|
| **BLOCKER — a `modes` rollback strands the fee mirror.** `mirrorModeFees` was reachable only from `publishCatalog`; `rollbackCatalog` produced a new client-visible version and left `golfin_mode_fees` at the last publish. | **RESOLVED** | Reproduced on PROD and re-verified: publish practice 12 (v5, served=12 mirror=12) → **rollback to v4** (v6, served=**10** mirror=**10**). Before the fix the mirror would have stayed 12. Audit row records `{"mirrored": true, "restoredFrom": 4}`. A live spend then confirmed the *consequence*: paying 12 is now refused `fee_changed: 10`, paying 10 debits. |
| Sibling `golfin_characters` shares the same rollback gap | **RESOLVED** | Covered by the same fix — `mirrorForCatalog` dispatches both; `MIRRORED_CATALOGS = ["characters", "modes"]`. |
| Secondary — `setCatalogEnabled` (kill switch) does not touch the mirror | **ACCEPTED AND DOCUMENTED, not changed** | All three options are written out in the `setCatalogEnabled` doc comment: deleting the mirror makes `/spend` answer `unknown_mode` for every mode and locks everyone out of everything; skipping validation while disabled turns a kill switch into an authorisation bypass and hands back the client-asserted price. Leaving it means the mismatch surfaces as `fee_changed` — re-priced and shown before anything is charged. Only option 3 is safe in both directions. Also in `ADMIN_DASHBOARD_OPS.md`. |
| Withhold-rule consumers, `HandleSpendDenied` shared `_data`, mirror-fails-publish, report fabrication | **NO CHANGE NEEDED** | The red-team checked all four and they held; recorded here so the next gate does not re-derive them. |

### I fixed the SHAPE, not the instance (PIPELINE_HARDENING rule 15)

Two `if (catalog === …)` call sites *were* the bug — a third would not have been
the fix. `mirrorForCatalog()` is now the only thing that writes a mirror, with
`MIRRORED_CATALOGS` as the named list, and both `publishCatalog` and
`rollbackCatalog` route through it. `rollbackCatalog` mirrors from the
**rolled-to snapshot** (new `fetchVersionSnapshot`) **before** the rpc and aborts
the rollback if the mirror write fails — identical ordering, abort and residual
window to publish.

### One honesty note about how the reproduction was driven

The publish and rollback were issued through the deployed
`/api/content/modes/{publish,rollback}` routes using the live admin page's own
session, not by clicking the buttons: the publish drawer's confirm checkbox is a
controlled React input and would not take a synthetic click. That is the same
server code path the buttons call (`route.ts` → `publishCatalog` /
`rollbackCatalog`) with the same auth — what was skipped is the button, not the
logic under test. The mirror, the served catalog, the audit row and the live
spend were all then read back from prod independently.

**Deploys after the fix:** dashboard Cloudflare version
`5dd60935-66ef-46f2-b92c-e1521fb79580`, stamped **`7337bdf67`** (confirmed on the
live sidebar). API unchanged at v59 — the fix is dashboard-side only.

**Live state restored:** every mode back to its baseline (practice 10/5,
missions + driving_range locked, the rest 0). `modes` is now at **v6** — three
more publishes than the report's original v4, because a publish never rewinds a
version. That is the counter working, not drift.

---

## Rejection follow-up — reviewer iter-2 (`ARCHITECT_REVIEW_FAIL`)

| Defect | Verdict | Evidence |
|---|---|---|
| **`--check --catalogs modes` exits 1** — `content_version.txt` reads `modes=4`, prod is at v6 after iter-2's live rollback verification. | **RESOLVED** | Re-exported; manifest now `modes=6`; `--check --catalogs modes` **exit 0**. `modes.csv` md5 `c36e4288…` **unchanged** — only the cursor moved, which is why it was invisible. |
| Self-review §3 claimed that command exited 0 | **CONFIRMED WRONG** | Re-derived myself before accepting the reviewer's word: disk `modes=4`, prod `6`, `--check` exit 1. The self-review reported a result it did not get. Recorded here rather than quietly fixed — the reviewer catching a false PASS from the gate before it is the two-gate design working. |

### This was the SECOND instance of one shape, so I audited the shape (rule 15)

Iter-1 hit the same thing and I caught it (`8aa71b878`, cursor 1 → 4). Iter-2 hit
it and I did not. Two instances ⇒ stop fixing instances.

**The shape, as a mechanically checkable question:** *does every catalog's cursor
in `content_version.txt` equal its `published_version` on prod?* Enumerated all
nine rather than sampling:

| catalog | disk | prod | |
|---|---|---|---|
| bags | 1 | 1 | OK |
| balls | 5 | 5 | OK |
| characters | 5 | 5 | OK |
| clubs | 1 | 1 | OK |
| items | 1 | 1 | OK |
| level_up_costs | 3 | 3 | OK |
| **modes** | **6** | **6** | **OK (was 4 vs 6)** |
| shop_catalog | 4 | 4 | OK |
| texts | 14 | 14 | OK |

Only `modes` was stale, and it is now fixed — but the useful output is the
*question*, not the row. The root cause is structural: **a rollback publishes
FORWARD**, so undoing a change leaves the version higher than before you started
even though the content is byte-identical. Anyone verifying against prod and then
"restoring" reasonably believes they have left no trace, and the cursor is the
trace.

Written into `Tools/content/README.md` as a standing rule where the next person
running a live verification will actually read it, rather than left as a lesson
in a spec folder that moves to `Completed/`.

---

## A gap I am declaring rather than leaving to be found

**The Rewards panel's validation has no automated coverage, because the dashboard
has no test infrastructure at all.** `Tools/admin-dashboard/package.json` has no
`test` script and no jest/vitest/playwright dependency; there is no `__tests__`
or `tests` directory. So `updateRewardAction`'s guards — `pts`/`maxPerEvent`/
`dailyCap` must be a non-negative integer or null, the row must already exist
(no create), no delete — are enforced only by code nothing exercises.

That matters more here than it would elsewhere in the dashboard: this panel edits
`game_point_actions`, it is LIVE ON SAVE with no draft or publish step to catch a
mistake, and it decides what every player is paid.

**It is not a regression and not specific to this task** — every dashboard
mutation (`adjustRp`, notices, banners, the content publish path) has always been
in the same position, and standing up a test framework is scope the SPEC did not
ask for and I have not silently taken. But "no gate objected" is not the same as
"it is covered", so it is written down here rather than left implicit.

What DOES cover the equivalent server-side logic: `/points/spend`'s mode-fee
validation has 17 backend tests, and the Unity spend verdicts have 9. The
asymmetry is real — the Python service is tested, the TypeScript admin is not.

Suggested follow-up (NOT done here): either a small vitest suite over the pure
validators (`checkNumber`, `validateCatalog`, `mirrorForCatalog`'s row mapping),
or a documented decision that the dashboard is verified by use rather than by
tests. Cesar's call, not mine to make inside this task.

---

## 5. One thing NOT fixed, and it is not mine

The full `export_content.py --check` exits 1 on a **pre-existing** `texts`
drift: `GACHA_PRIZES_TITLE` and `SHOP_HISTORY_COMING_SOON` are in
`Assets/Localization/LocalizationText.csv` (committed in `a10f46318`, the gacha
task) but not in the `texts` catalog, which sits at 506 rows vs the CSV's 508.
The repo is AHEAD of the catalog, so the fix is a re-seed of those two keys, not
an export. Scoped `--check --catalogs modes` is clean. Left alone deliberately —
it predates this task and belongs to whoever owns `texts`.
