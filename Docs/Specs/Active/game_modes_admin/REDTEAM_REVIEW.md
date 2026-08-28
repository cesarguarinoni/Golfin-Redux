# Red-Team Review — `game_modes_admin`

**Gate:** `golfin-redteam-reviewer` (adversarial) · **Date:** 2026-08-28 19:32 JST
**Verdict:** `ARCHITECT_REVIEW_FAIL` — one concrete blocker (rollback re-opens the fee
mirror drift the whole task exists to close).

The server contract itself is correct and I proved it against production. The failure is
a first-class operator path — **catalog rollback** — that neither the implementer, the
self-reviewer, nor the reviewer examined, and that strands the charged price when an
operator undoes a fee publish.

---

## 1. What I derived myself (not carried forward)

### 1a. The live server contract — DERIVED against production, zero blast radius

Both prior gates DECLINED to re-run acceptance item 1 ("would mutate prod"). I derived the
entire `/points/spend` mode-fee contract without touching any of the 5 real modes, using a
throwaway `golfin_mode_fees` row (`_redteam_probe`) and the dev player token. Script:
`scratchpad/probe.py`. Live run, uid `f2636482…`, API `playlife-api.fly.dev`:

| Assertion | Result |
|---|---|
| probe row `entry_fee=15, is_locked=false` inserted | `golfin_mode_fees._redteam_probe = 15` |
| baseline ledger rows | **45** |
| `amount=10` (wrong) → `{"status":"fee_changed","fee":15}`, HTTP 200 | PASS · ledger still **45** (no debit) |
| `amount=15` (match) → `{"status":"ok","spent":15}`, HTTP 200 | PASS · ledger **46** (+1) |
| the new ledger row | `amount −15, type spend, description **mode_entry_fee:_redteam_probe**` |
| flip `is_locked=true`, `amount=15` → `{"status":"mode_locked"}` | PASS · ledger still **46** (no debit) |
| DELETE probe → `golfin_mode_fees` holds exactly the 5 real modes | PASS |

Final mirror state, re-read after cleanup:
`practice (10,false)`, `versus_1v1 (0,false)`, `tournaments (0,false)`,
`driving_range (0,true)`, `missions (0,true)` — **exactly the spec's five, nothing left
behind.** The dev account spent 15 RP (activity_pts → 878), as the brief authorised.

This confirms SPEC §6 items **1, 2, 4** (the `ok`/`fee_changed`/`mode_locked` branches, the
per-mode-legible ledger description, nothing-debited-on-refusal) directly against prod.

### 1b. Report integrity (Rule 6) — spot-checked, no fabrication

- `modes.csv` md5 on disk = `c36e4288a969eb7367d2fe6535382d62` → matches report's `c36e4288…`.
- `256f21587` is the real GolfinRedux modes commit; `f5749d4` is the playlife feat commit
  (mirror migration + points router + `test_mode_entry_fee.py`); `89508c5` the bound fix.
- The report's live-E2E ledger rows are independently visible in prod `points_transactions`
  for this user: `mode_entry_fee:practice −15` (08:33), bare `mode_entry_fee −10` (08:34),
  `mode_entry_fee_refund −1` (08:34, the "door not keyword filter" case), `versus_win +25`
  (08:34). Numbers corroborated, not fabricated.

### 1c. Legacy bare reason still debits — re-confirmed

`MODE_ENTRY_FEE_PREFIX = "mode_entry_fee:"` (colon load-bearing). Bare `mode_entry_fee`
fails `startswith` → falls through to `spend_pts`. Prod ledger shows bare `mode_entry_fee`
rows still landing. SPEC §6 item 3 holds.

---

## 2. BLOCKER — a `modes` rollback strands the fee mirror (server catalog vs charged price disagree)

**This is the drift the task's own thesis says must never happen** (SPEC line 18-19; the
`mirrorModeFees` doc comment cites the `golfin_characters` incident — "a stale FEE means every
player is charged the old price — or, worse, refused at the new one while the card still shows
the old"). The mirror closes that on **publish**. It is wide open on **rollback**.

### The proof (code paths, all read this pass)

- `Tools/admin-dashboard/lib/contentMutations.ts` — `mirrorModeFees()` (line 242) is called
  from **exactly one place**: `publishCatalog` (line 345, `if (catalog === "modes")`).
- `rollbackCatalog` (line 426) calls `content_rollback` RPC (line 460) + `writeAudit`, and
  **never calls `mirrorModeFees`**. Its own comment: *"Rollback publishes FORWARD, so it
  creates a version too."* So it produces a **new, client-visible catalog version** while
  leaving `golfin_mode_fees` at whatever the LAST publish wrote.
- `~/Documents/playlife/backend/migrations/2026_08_24_content_catalog.sql` — `content_rollback`
  (line 152) operates only on `content_rows`/`content_drafts`/`content_versions`. It has **zero**
  reference to `golfin_mode_fees` (grep clean). The mirror migration
  `2026_08_28_golfin_mode_fees.sql` mentions "rollback" **nowhere**.
- Rollback is a **UI-exposed operator control for every catalog including `modes`**:
  `app/(panels)/_content/publish-drawer.tsx:150 rollbackCatalog(catalog, version)`
  ("the rollback control"), backed by `app/api/content/[catalog]/rollback/route.ts`.

### The failure, concretely

Operator fat-fingers `practice.entryFee = 150`, publishes (mirror → 150), then hits the
**rollback control** to undo it back to the 10-version:

- `content_rollback` republishes the fee-10 rows → clients at next launch download a card
  reading **10**.
- `golfin_mode_fees.practice` **stays 150** (nothing rewrote it).
- Every player taps ENTER at 10 → server prices from the mirror → `fee_changed:150` → card
  flips to **150** → practice, the free-tier entry mode, is now effectively 150 for everyone
  and anyone with < 150 RP **cannot enter it at all** — despite the operator having "rolled it
  back." My §1a probe is exactly this mechanism in miniature: the server charged/refused at the
  mirror's number (15) irrespective of anything else, and **nothing rewrites that number on
  rollback.**

"Undo a bad fee publish" is the single most likely reason to roll `modes` back — the whole
task is about editing fees — so this is not an exotic path. The `fee_changed` UX bounds it
(the player is re-shown 150 before the debit, so there is no *unshown* overcharge and the
literal "never wrongly spends RP" invariant survives), but the operator's rollback is silently
defeated and a free mode becomes unenterable. That is a Cesar-reject-on-sight drift on the
exact axis this task was written to protect.

### Fix

In `publishCatalog` the mirror write for `modes` is placed before `content_publish` and aborts
on failure. Do the same on the rollback path: after (or as part of) `content_rollback` for
`catalog === "modes"`, re-run `mirrorModeFees` from the **rolled-to** draft set so the mirror
follows the catalog, aborting the rollback if the mirror write fails — identical posture to
publish. (Note: the sibling `golfin_characters` mirror shares this exact rollback gap; the fix
should cover both, or Cesar should explicitly accept it as a documented tradeoff. It must not
ship unnamed.)

### Secondary, same shape (name it, lower severity)

`setCatalogEnabled(modes, false)` (the per-catalog kill switch, §7.4) reverts clients to the
bundled CSV fee but does **not** touch the mirror, so a client on the bundled price is still
priced at the last-published mirror fee. Same mirror-ahead direction, same `fee_changed`
bound. Kill-switch is arguably out of this task's scope (different task's machinery), but it is
the same latent disagreement and belongs in the same decision.

---

## 3. Attacks that FAILED to break it (checked, held up)

- **Withhold-rule failure direction.** `ModeSelectScreenController.RebuildCards` and
  `ModeCarouselController.RebuildCards` both iterate `GetAllModes()` by `foreach` and select the
  default/expanded mode by **`id`** (`mode.id == _initialExpandedModeId` / `_defaultModeId`),
  never by position; carousel guards `_dataCount == 0`. A withheld mode's absence degrades
  gracefully (no `[0]`/`count==5` assumption to blow up). If `practice` were ever withheld,
  `startIndex` stays −1 and centering falls back — no crash. Not a blocker.
- **`ModeCardController.HandleSpendDenied` mutating shared `_data.entryFee`.** It updates the
  shared `ModeData` (so the sibling card's *data* is correct) but only re-renders the tapped
  card. Worst case is a cosmetic stale number on a non-focused card until it is tapped/rebound —
  self-healing, and the server enforces the fee regardless (proven in §1a). Not player-harmful.
- **Mirror-fails-publish.** Confirmed `mirrorModeFees` returns before `content_publish` on error
  (line 345-354). Holds — this is the path that IS covered; §2 is the path that is not.
- **Report numbers / fabrication.** §1b — every spot-checked number matches disk or prod.

## 4. Gates that legitimately do not engage — confirmed, not merely accepted

No `screenshots/`, `videos/`, `reference/`, no Figma node, no `.prefab`, no mesh, no scene
diff (`git show --stat 256f21587` touches zero `.unity`/`Physics/`/`.mat`/`Scenarios.cs`). So
Rules 14/16/17/18/19/21 never fire. I verified the deliverable is genuinely non-visual: the two
new admin panels are Next.js (Cloudflare), not Unity prefabs, and the Unity change is data
loading + a spend verdict with no new on-screen element. An admin-panel screenshot would be a
reasonable *nice-to-have* (Cesar may want to eyeball the Modes/Rewards panels), but its absence
is not a rule violation and not the reason for this FAIL.

## 5. Standing bans — clean

Zero `Assets/Scripts/Physics/` edits, no `*Gate` scenarios, no `LabScaffold.unity` touch, no
`M_Splash*.mat`, no scene diff. Pre-existing `texts` drift (`GACHA_PRIZES_TITLE`,
`SHOP_HISTORY_COMING_SOON`, `a10f46318`) is genuine and out of scope. Fly v59
`01M13XNG9NDT1QM4Z2QJH2K6GB` and dashboard `256f21587` (zero dashboard commits since) match
the brief.

---

## Verdict

**`ARCHITECT_REVIEW_FAIL`.** The server contract is correct and I proved it against production
end to end. But `rollbackCatalog` (a UI-exposed operator control) republishes an older `modes`
fee to clients without re-writing `golfin_mode_fees`, re-opening the served-catalog-vs-charged-
price drift that is this task's entire reason to exist — on the most likely rollback scenario
(undoing a bad fee publish). Route back to the implementer to mirror on rollback (and decide the
kill-switch/characters siblings), or to Cesar if he rules rollback-mirroring out of scope; it
must not pass unnamed.

## Files touched by this review

| File | Reason |
|---|---|
| `Docs/Specs/Active/game_modes_admin/REDTEAM_REVIEW.md` | This adversarial verdict (new) |
| `Docs/Specs/Active/game_modes_admin/STATUS.md` | Set to `ARCHITECT_REVIEW_FAIL` |
