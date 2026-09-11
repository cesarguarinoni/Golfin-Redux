# REDTEAM_REVIEW — `weekly_rotation_admin`

**Iteration:** 2
**Reviewer:** `golfin-redteam-reviewer` (adversarial gate)
**Date:** 2026-09-11 13:30 JST
**Verdict:** **ARCHITECT_REVIEW_PASS**
**STATUS after this review:** `ARCHITECT_REVIEW_PASS`

Prior rejections: **none** — no `CESAR_REJECTION.md`; this is the first pass to Cesar.

Scope: admin dashboard (Next.js/Cloudflare) + content pipeline + one applied SQL migration +
one new catalog CSV + a placeholder PNG. No Unity C#, no Figma node, no world→screen feature,
no mesh, no clone mandate. The Unity/Figma/UI-lint/bbox/mesh/clone gates do not apply and I did
not fail for their absence. I attacked the surfaces the kickoff named and **re-derived every
material claim from primary sources** rather than trusting the report or the two prior gates.

---

## The angle I captured (for a no-render task: my own re-derived evidence, not re-read)

Everything below I ran myself this pass. Scripts in the session scratchpad; commands quoted.

### Adversarial vitest against the pure generator (`lib/rotation.ts`) — I wrote 7 probes

I authored a throwaway `lib/__tests__/redteam_probe.test.ts`, ran it, extracted the findings,
then **deleted it** (working tree confirmed clean; only pipeline artifacts uncommitted). Results:

| Probe | What I tried to break | Result |
|---|---|---|
| P1 | Feed the eligible clubs/balls/chars in **reversed order** → does the seeded shuffle give a different unpinned lineup? | **YES it does** (hash `d53833d7` vs `ccdef177`). The Fisher–Yates shuffle is input-order-sensitive. **Mitigated:** the panel loads every catalog via `fetchRows` which `.order("row_id")` (contentData.ts:301), so prod always feeds a stable order → cross-session determinism holds. The shipped plan is fully pinned, so it is immune regardless. Latent fragility, **not a defect**. |
| P2 | Over-fill: can total picks EXCEED the quota total via fill? | **No.** 9/9 clubs at seeds 1/42/999/31337; `fillPlan` caps `left` at `quota.total − pins`, so fill never overshoots. |
| P3 | Pin a Legendary on a `Common:1` quota | 1 club (the pinned Legendary), quota rarity displaced. This is the documented "pins count by total first" behaviour (Deviation 7). Not a bug. |
| P4 | `characterQuota="Any:1;Legendary:1"` with 4 chars → double-fill the same ref? | **No** — 2 distinct refs (`char_shae` + `char_mike`); the `taken` set prevents any bucket re-picking. |
| P5 | Duplicate `pinnedClubs` (`x;x`) → two `shop_<rot>_x` rows with the same rowId? | The **generator does emit a colliding rowId and does NOT error**. Mitigated twice: validator **R1 blocks the publish** (`R1: pinnedClubs lists x twice`, contentValidate.ts:2135) and `upsertDraftRow` collapses the duplicate id. Never reaches prod. Not a blocker. |
| P6 | Re-seed (seed 1 → seed 77) with `existing` → are stale rows left live (two lineups)? | **No** — 9 rows deactivated, 13 live shop rows, and **no rowId is both live and stale-off**. `materializeLineup`'s stale-row deactivation works, and the panel **does pass `existing`** (lineup-workbench.tsx:202-207). |
| P7 | 6 prior rotations, `excludeWeeks=4` → is exclusion by rotation COUNT? | Yes — `recentRotations` slices the `n` most recent by `startUtc`; the pinned ref of recent rotations was excluded. |

### Prod state — re-derived via `Tools/content/rest.py` PostgrestClient (read-only, no writes)

```
catalog versions:  gacha_banners v12 · gacha_pools v5 · gacha_rates v6 · rotations v4 · shop_catalog v9
rotations rows:    total=54 active=52  (wk_2026_36/37 inactive)  materialized: wk_2026_36/37/38
shop wk_2026_38:   total=13 active=13  window 2026-09-14T00:00:00Z → 2026-09-21T00:00:00Z
                   categories {ball,character,club}  quantity="" on every row (one-ball, Dev 1)
banners weekly:    banner_wk_2026_38 active thr=50/Legendary · banner_wk_2026_36/37 inactive thr=50/Legendary (R3-consistent)
pity (Cratilo):    weekly counter=3 total=3  ·  banner_wk_2026_36 counter=0 total=2  ·  banner_wk_2026_37 counter=0 total=1
```

Every number matches the report and the kickoff. **The pity E2E is proven independently:**
the `weekly` counter=3 is 2 pulls on banner_36 + 1 on banner_37 under the shared key, while each
per-banner row holds its own cap count (2 and 1). The `weekly`-keyed row cannot exist unless the
migration is live — so the migration is definitively applied and correct on prod.

### Server refusals — re-derived with the synthetic uuid `00000000-0000-4000-8000-0000000f0f0f`

```
gacha_pull  banner_wk_2026_38 (pre-window):  {status:not_available, reason:window}
            banner_wk_2026_37 (archived):    {status:not_available, reason:inactive}
            banner_wk_2026_36 (archived):    {status:not_available, reason:inactive}
shop_buy    shop_wk_2026_38_ball_ace_attire (pre-window): {status:not_listed, reason:window}
            shop_wk_2026_36_club_driver_klyro_common (archived): {status:not_listed, reason:inactive}
            shop_ticket_standard_50 (Cesar off-sale):    {status:not_listed, reason:inactive}
```

Matches the report exactly (incl. the flagged copy nit — an inactive banner refuses `inactive`,
not `banner`). Deviation 5 (stale ticket draft aligned) is proven: the server refuses to sell it.

### Test suites + export — re-run myself

- `npx vitest run` rotation suites → **54 passed** (rotation.test.ts 34 + rotationValidate.test.ts 20).
- `python3 -m unittest discover Tools/content/tests` → **Ran 53 tests … OK**.
- `export_content.py --check` → **clean — no file would change, no catalog has drifted**.

### The shipped 52-week plan actually resolves — the one thing NO prior gate checked

I cross-referenced every pinned refId in all 54 `rotations.csv` rows against the LIVE
clubs (799) / balls (20) / characters (12) catalogs:

```
UNRESOLVED pins: 0     DUPLICATE pins (R1 would block): 0
default-ball pins: 0   starter-character pins: 0
Supreme character pins: wk_2027_01 → char_freda, wk_2027_18 → char_freda  (exactly the two §3.1a flags as a Cesar DECISION)
```

Every future week is generatable — no typo'd pin will block a publish months from now. The only
Supreme pins are the two the spec itself flagged for Cesar's decision (Freda). Clean.

---

## Migration (`2026_09_11_gacha_pity_group.sql`) — diffed against its base, hunk by hunk

`diff` of the `golfin_gacha_pull` body (base `2026_09_02_default_ball_guard.sql`, comments/blanks
stripped) shows the **ONLY** substantive changes are the pity-key hunks:
`v_pity_key` declaration; replay reads counter under key / total_pulls under banner; step-6 same;
step-11 primary upsert keyed by `v_pity_key` (DO UPDATE increments `total_pulls` from the TABLE,
not the stale read) + a second per-banner row `if v_pity_key <> v_banner`; and `'key'` in both
responses. **Counter, prize, cost, pool logic are byte-identical.** `v_bdata` is loaded (line 207)
before the replay key is computed (line 214) — no null-key bug. Both repo mirrors
(`admin-dashboard/migrations/` and `playlife/backend/migrations/`) are md5-identical for both the
pity and the seed migration.

---

## Validator R1–R4 (`lib/contentValidate.ts`) — read + reasoned

- **R1 overlap:** sort-by-start + compare-to-`prev.end`. I checked the classic nested-interval
  hole: whenever ANY two active rotations overlap, at least one adjacent pair also overlaps
  (a nested `[a,b]⊂[c,d]` always trips `a<d`), so it always raises ≥1 error → publish blocked.
  Sound. Also dedups pins (R1 line 2135), rejects commas, bad windows, bad grammar.
- **R2:** a tagged shop/banner row with a blank end OR an end after the rotation's end → error
  (line 422 `e === null || e > re`). A tagged row **cannot outlive its week**.
- **R3:** collects only `isActive && active` banners; two in one group with differing
  `pityThreshold`/`pityMinRarity` → error (blank≡0). Inactive ignored. Cannot publish a mismatch.
- **R4:** warn (never block) when a tagged ref was listed within `excludeWeeks` rotations.
- **`SHOP_REFERENCED_CATALOGS`** = `Array.from(new Set(Object.values(SHOP_CATEGORY_TO_CATALOG)))`
  — complete for every category value **by construction** (includes `ticket_types`, the fix).
- **Publish order** puts `rotations` last, but `publishCatalog` loads other catalogs as **drafts**
  (contentMutations.ts:483) and adds `rotations` for shop/banners (line 441), so R2 resolves the
  tag against the draft rotation regardless of order — no first-publish ordering bug (live E2E
  published all 5).

---

## Report integrity (Rule 6) — every asserted number re-derives, nothing fabricated

| Claim | Re-derived |
|---|---|
| 72 new DICT keys in `05f0f7da1` | `git show … grep -c` → **72** |
| Deploy stamp `f8063af6b` in built worker | `grep` route.js → **`"f8063af6b"`** |
| Migration mirrors byte-identical | md5 pity `4af2065a…` == ; seed `661eed04…` == |
| Seed byte-identity md5 `11a9211b…` | `tr -d '\r' < reference/rotations_seed.csv \| md5` → **`11a9211bc77e365d4d8dbe34fb42cea6`** |
| Reference is CRLF, 52 rows, wk_2026_38→wk_2027_36 (Dev 8) | 53 CR chars, 52 data rows, first/last confirmed |
| Shipped CSV = 52 plan + 2 archived (Dev 12) | 54 rows, 52 active; all 52 reference ids present |
| Banner withheld (not broken) when art unbundled + blank artUrl (Dev 11) | `GachaBannerArt.Resolve` ladder ends at step 0 → **null = WITHHELD** |

---

## The three break-attempts, and why each FAILED

1. **Break the generator behaviourally** (7 vitest probes): tried overshoot, `Any`+rarity
   double-fill, duplicate-pin collision, re-seed two-live-lineups, exclusion-by-count,
   input-order sensitivity, pins-beyond-quota. **Failed to break:** the only two real findings
   (P1 order-sensitivity, P5 dup-pin collision) are both mitigated before prod (row_id ordering;
   R1 + upsert) and neither affects the fully-pinned shipped plan.
2. **Bypass the validator to publish something unsafe**: tried two overlapping active rotations
   (nested-interval sort hole — proved it still fires an error), a tagged shop row outliving its
   week (R2 blocks blank/out-of-bounds end), and two grouped banners with different pity (R3
   blocks when both active). **No bypass found.**
3. **Find a non-pity change or a logic error in the migration**: full body diff shows ONLY the
   5 pity-key hunks; `total_pulls` for the cap is read/written under `v_banner` (a second row);
   the grouped counter is under the key; replay loads `v_bdata` before the key. **Verified live**
   on prod by the counter split (weekly=3 vs per-banner 2/1). **No defect.**

Spec-intent check: the SPEC goal — a weekly rotation of clubs/balls/characters in shop + a
rate-up gacha banner, authored as one unit, generated deterministically, published behind one
button, pity surviving the week — is met and live for `wk_2026_38`.

---

## Decisions surfaced to Cesar (already in the report; not review-gate blockers)

These are product/ops decisions, correctly surfaced by the implementer — they reach Cesar at his
final-approval step (which `ARCHITECT_REVIEW_PASS` advances to), so I do not FAIL/ESCALATE for them:

- **Deviation 1 — a ball listing delivers ONE ball, not ten.** The SPEC §3.3 wrote "quantity 10",
  but `golfin_shop_purchase` reads `quantity` for tickets only and rule G3-Q refuses any other
  value on a non-ticket row (a `quantity:10` ball row would be REFUSED at publish). So generated
  ball rows carry blank quantity at the ladder price (30/60/120…). This makes a ball ~10× more
  expensive per-ball than the spec's intent implied. Editable before publish; documented in
  `ECONOMY_MASTER §3` flagged for Architect wording. **Ten-ball delivery is a server change, not a
  content edit** — Cesar's call.
- **Deviation 11 — `banner_wk_2026_38` needs `artUrl` set before Monday 00:00 UTC.** Installed
  builds 2873/2874 don't bundle `GachaBanner_Weekly.png`, so on Monday the banner is **withheld**
  on those builds (safe — never a broken card) until Cesar uploads art via the admin. Operational
  deadline, not a defect.
- Ticket listing back on sale?, retire the two shop_stocking placeholders that collide with the
  week's picks?, the named `rotation_*` audit action?, ECONOMY_MASTER wording — all flagged.

---

## Verdict

I attacked the generator (7 probes), the validator (3 bypass attempts), the migration (full
body diff + live proof), the publish-order ordering, the shipped plan's 52-week pin resolution,
and re-derived all prod state, the pity E2E, the RPC refusals, both test suites, `--check`, the
deploy stamp, the DICT count, the migration mirrors and the seed hash — from primary sources, not
from the report or the two prior gates. **I could not find a blocker.** Every acceptance criterion
holds against evidence I generated this pass. The disclosed deviations are sound tradeoffs; the
two that are product decisions are surfaced for Cesar's final approval.

**ARCHITECT_REVIEW_PASS.**

---

## Files summary

| Path | Change |
|---|---|
| [`/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/weekly_rotation_admin/REDTEAM_REVIEW.md`](Docs/Specs/Active/weekly_rotation_admin/REDTEAM_REVIEW.md) | NEW — adversarial red-team verdict PASS. |
| [`/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/weekly_rotation_admin/STATUS.md`](Docs/Specs/Active/weekly_rotation_admin/STATUS.md) | UPDATED — `READY_FOR_REDTEAM` → `ARCHITECT_REVIEW_PASS`. |

---

# Iteration 3 — red-team (2026-09-11 14:2x JST)

STATUS was `READY_FOR_REDTEAM` after golfin-reviewer's iter-3 PASS. Two post-iter-2 defects were
fixed in code (archive trap `70464d323`; R3 stand-in exemption `b0a70282a`); my brief was to break
the fixes and hunt a third of the same shape. HEAD = `a0c845ce4`. No Unity/Figma/mesh gates apply
(admin-dashboard + content-pipeline task). **Verdict: `ARCHITECT_REVIEW_FAIL` — one concrete
blocker (the R3 exemption is incomplete; the same masked-check in a second tool was not exempted).**

## Method (Rule 5 — I re-generated, did not carry forward)

Read both fix diffs; read the whole 2194-line `contentValidate.ts` and enumerated **all 15**
`otherCatalogs` cross-row sites myself; read the client art ladder (`GachaBannerArt.cs`,
`GachaBannerModel.cs`); read `export_content.py` R3 bodies + `own_name`; read
`ContentArtFetcher.cs`, `CIBuild.cs`, `ContentArtValidator.cs`; simulated the R3 boolean against
the live CSV; wrote & ran 5 adversarial vitests (deleted after); re-derived all prod state over
PostgREST; re-ran both suites and `export --check`; curled the artUrl and the deployed stamp.

## Fix 1 — archive trap (`contentValidate.ts` active-row guards) → SOUND

Full shape audit, re-derived (every `otherCatalogs` site, incl. the fine ones):

| Site (line) | Cross-row | Automated deactivation path? | Guarded on `row.isActive` | Verdict |
|---|---|---|---|---|
| shop rule 6 refId/ref-active/default-ball (554) | yes | ARCHIVE (shop row) | **now yes** | fixed ✓ |
| shop rule 8 price band (805) | read, **warn** | — | no | fine (warn never blocks) |
| shop R2 checkRotationTag (400/818) | yes | ARCHIVE (shop row→rotation) | **now yes** | fixed ✓ |
| shop R4 (820) | warn | — | now yes | fine |
| shop G1/G1-T/G3-Q/G2 | yes | ARCHIVE | yes (pre-existing) | fine |
| level_up_costs coverage/ceiling (892) | catalog-level | — | n/a | fine (not the shape) |
| gacha_rates↔pools `checkRatesAgainstPool` (1621/1712/1808) | yes | ARCHIVE (rates+pool) | **active-only by construction** (poolIds built from active rows) | fine ✓ |
| gacha_pools rule 5 kind/ref/active/default (1721) | yes | ARCHIVE (pool) | yes (psc1 scar) | fine |
| gacha_pools rule 6 rarity-match (1762) | compare-if-found | — | no | fine (sane-row consistency) |
| gacha_pools rule 8 min_build (1806) | yes | ARCHIVE | yes | fine |
| gacha_banners rule 10 pool/rates/ticket (1846) | yes | ARCHIVE (banner→pool) | **now yes** | **THE TRAP — fixed ✓** |
| gacha_banners rule 13 rolled (1929) | yes | ARCHIVE | **now yes** (empty set) | fixed ✓ |
| gacha_banners rule 18 featured-in-pool (2005) | warn | ARCHIVE | **now yes** | fixed ✓ |
| gacha_banners R2 (2018) | yes | ARCHIVE | **now yes** | fixed ✓ |
| gacha_banners R3 pity group (2020) | yes | ARCHIVE | yes (`row.isActive && active`) | fine |
| ticket_types rule 20 (2086) | yes | — (fires on `!row.isActive` but counts only **active** banners) | yes | fine |
| rotations R1 base pool (2153) | yes | ARCHIVE (rotation) | **now yes** | fixed ✓ |
| rotations R1 overlap/gap (398) | yes | ARCHIVE | active-only | fine |
| **missions→areas/loadouts (1030-1090); mission_loadouts→clubs (1299)** | yes | **NONE automated** (see below) | **no** | same shape, correctly deferred |

- **Q1 "can an inactive row hide garbage a reactivation revives unchecked?" → NO.** Every guard is
  `if (row.isActive)`. Probe A/A′: an inactive banner pointing at a rateless `pool_gone` yields no
  rule-10 error; flip it active and the rule-10 error returns. Garbage is caught the moment it can
  reach a player. (The only skip-all-validation path is the `content_publish` RPC, which bypasses
  the validator for **every** row regardless of these guards — pre-existing, not introduced here.)
- **Q2 "any unguarded cross-row site a deactivation path can trigger today?" → NO.** The only
  automated deactivation flow is `archiveRows` (`rotation.ts` L1065-79), which touches exactly
  `gacha_rates/gacha_pools/gacha_banners/shop_catalog` (by poolId/rotationId) + the rotation row —
  never clubs, missions, areas, loadouts or components. Every catalog it touches is now
  active-guarded. The report's "missions↔components / loadouts↔clubs = same shape, no deactivation
  path" is **accurate in the sense that matters**: no automated flow retires those referents. A
  *manual* drawer toggle of a club or `mission_start_area` can trip the pre-existing (unchanged by
  this task) missions rules — probe confirms an inactive mission on a deactivated area still errors
  — but that is escapable (reactivate / re-point, per the rule's own message) and is correctly
  flagged for the Architect, not a regression this task owns.
- **Q3 "does any guard skip a rule an ACTIVE row needs?" → NO.** Probe B: an active banner with an
  empty poolId still errors "poolId is empty". Each guard preserves pre-fix behaviour for active
  rows verbatim.

5 adversarial vitests all passed (probe deleted). Fix 1 is sound.

## Fix 2 — R3 stand-in exemption in `export_content.py` → SOUND (but see the blocker)

- Client masking requires `ownSprite == true`, i.e. `artSprite == ConventionName(bannerId)`.
  `own_name` in `export_content.py` is byte-identical to `GachaBannerArt.ConventionName`, so
  `GachaBanner_Weekly` is the own-name of **only** `banner_weekly`. For every real weekly banner
  (`banner_wk_2026_38`→`GachaBanner_Wk202638`) `ownSprite` is false, so `Resolve` skips step 2 and
  the **URL wins at step 3** — no masking. The exemption removes a **false-positive** refusal, it
  does not hide a real one.
- **Data-widening (Q3): cannot cause harm.** A hand-made banner with a fake `rotationId` +
  `GachaBanner_Weekly` is exempted from `--check`, but on the client its `ownSprite` is still false
  → URL wins → not masked. The only id that masks (`banner_weekly`) passes R3 *with or without* the
  exemption (sprite == own-name), so the exemption opens no new masking path. `conflicting_art`
  skip is correct too (weekly banners share the stand-in with different URLs *by design*).
- `ContentArtFetcher` does **not** consume the R3 report; "Fetch URL Art" resets the sprite to the
  row's own name, so the exemption stops applying there — nothing broken. `--check` is clean.

## THE BLOCKER — the R3 exemption is incomplete: `ContentArtValidator.cs` runs the identical masked-check, un-exempted

The R3 "placeholder masks URL art" check exists in **two** static tools (the client `Resolve` is a
third, and it handles the stand-in correctly at runtime):

1. `Tools/content/export_content.py` `masked_art_report` — **exempted** in `b0a70282a`. ✓
2. `Assets/Editor/ContentArtValidator.cs` `ValidateCatalog` (L~318-338) — **the same check, NOT
   exempted.** Its `gacha_banners` column carries `ownName: GachaBannerArt.ConventionName`, and it
   emits a `Verdict = "masked"` Miss whenever `artUrl` is set and `artSprite != own-name`.

`banner_wk_2026_38` (live, v13: `artUrl` set, `artSprite=GachaBanner_Weekly`, own-name
`GachaBanner_Wk202638`) trips it. Simulated the exact boolean against the live CSV:

```
MASKED-FAIL: banner_wk_2026_38  artSprite='GachaBanner_Weekly' own='GachaBanner_Wk202638' rotationId='wk_2026_38'
```

`ContentArtValidator.RunAndReport()` runs in the build lane (`CIBuild.cs:479`) and via
`GOLFIN/Content/Validate Catalog Art`. On the next **game** build it will rewrite the committed
`Docs/Reports/content_art.txt` — today "gacha_banners (4 rows, 0 with missing art) … every sprite
column resolves" — to report `banner_wk_2026_38 … masked`, whose own legend reads **"masked —
FAIL. The row renders the WRONG picture."** That verdict is factually false (the client renders the
uploaded art; `ownSprite` is false so the stand-in is demoted to step 4), and it directly
contradicts `export --check`'s "no row's art is masked by a placeholder" on the same row — breaking
the codebase's own stated "three tools must agree on one string" invariant. It does **not** filter
by `is_active`, so every weekly banner ever run that carries an `artUrl` accumulates a permanent
false "masked" row in that committed report (I6 keeps them in the CSV forever).

Why this is a blocker and not a note:
- It is the **third defect of the exact shape** the two fixes address (a pre-weekly-rotation art
  gate flagging the legitimate shared stand-in) — the one the kickoff asked me to hunt for, in the
  exact domain it pointed at ("a build step the exemption breaks").
- The Rule-15 / PIPELINE_HARDENING §22 shape audit was done for the **archive-trap** defect but
  **not** for the **R3** defect. Grepping the R3 masked-check operation class (not sampling) returns
  **two** static sites; the fix patched one. Enumerating both is exactly what §22 requires.
- It writes a committed **"FAIL"** artifact to the repo on the next build — the "Cesar catches it on
  sight" class this gate exists to stop.

Mitigating (so the implementer can scope it): `ContentArtValidator` is deliberately
report-only — `RunAndReport` never throws/fails, `CIBuild.cs:479` wraps it in try/catch as "a
report, not a gate", and nothing consumes `MaskedRowCount`. So it does **not** block the build, the
weekly-rotation flow, or TestFlight today. It is a false-FAIL in a committed report + a tool-
consistency violation, not a functional outage. But it is squarely in scope (the same R3 concept
the fix under review addresses) and mechanically fixable, so it routes to the implementer, not to
Cesar.

### Fix instruction

Mirror `b0a70282a` into `ContentArtValidator.ValidateCatalog`: before recording the `"masked"`
Miss, skip a rotation stand-in — `spec.Name == "gacha_banners" && !string.IsNullOrEmpty(Field(fields, index, "rotationId")) && spriteName.Trim() == "GachaBanner_Weekly"`.
Prefer one shared definition of the stand-in name (beside `GachaBannerArt.ConventionName`) that
`export_content.py`, `lib/rotation.ts` (`WEEKLY_BANNER_ART`) and `ContentArtValidator` all reference,
honouring "three tools, one string". Add an EditMode test asserting a rotation-tagged
`GachaBanner_Weekly` banner with an `artUrl` is **not** flagged `masked`, while an un-tagged one on
the stand-in still is (parity with `test_export_check.py::TestWeeklyStandinIsNotMaskedArt`).
Re-run the game-lane `Validate Catalog Art` and confirm `content_art.txt` reports 0 masked.

## SPEC §7 re-verification at HEAD (independent, read-only)

| Check | Expected | Got | |
|---|---|---|---|
| content_catalogs published_version | rates 6 / pools 5 / banners 13 / shop 10 / rotations 4 | rates 6 / pools 5 / banners 13 / shop 10 / rotations 4 | ✓ |
| banner_wk_2026_38 | active v13, artSprite GachaBanner_Weekly, pityGroup weekly, rotationId wk_2026_38, artUrl set | exact | ✓ |
| artUrl serves | 200 image/jpeg 244928 | HTTP/2 200, image/jpeg, 244928 | ✓ |
| shop_char_mike / shop_ball_putt_ace | inactive v10 | is_active=false v10 both | ✓ |
| shop_wk_2026_38 | 13 active | total 13, active 13 | ✓ |
| rotations wk_2026_36/37 / 38 | inactive / active | 36 F · 37 F · 38 T | ✓ |
| pity 'weekly' for f2636482 | counter 3 | counter 3, total 3 | ✓ |
| export_content.py --check | clean | clean (no masked) | ✓ |
| dashboard vitest | 382 | 382 passed | ✓ |
| content suite | 56 | 56 OK | ✓ |
| deployed stamp / Access | 70464d323 / 302 | /api/version → 302 cloudflareaccess (stamp per Chrome) | ✓ |

All prod/repo state matches the kickoff exactly. The two named fixes and every §7 item are
verified good; the sole blocker is the un-mirrored R3 exemption in `ContentArtValidator.cs`.

## Verdict

**`ARCHITECT_REVIEW_FAIL`** — one concrete, reproducible blocker: the R3 stand-in exemption is
incomplete. `Assets/Editor/ContentArtValidator.cs` runs the identical R3 masked-check with no
stand-in exemption and will report `banner_wk_2026_38` (and every future weekly banner carrying an
`artUrl`) as `masked — FAIL` in the committed `Docs/Reports/content_art.txt` on the next game build,
contradicting `export --check`. Fix per the instruction above (small, mechanical). Fix 1, Fix 2's
export half, and all SPEC §7 state are verified sound this pass.

## Files summary (iter-3)

| Path | Change |
|---|---|
| [`REDTEAM_REVIEW.md`](Docs/Specs/Active/weekly_rotation_admin/REDTEAM_REVIEW.md) | Appended iter-3 adversarial record — FAIL (one blocker). |
| [`STATUS.md`](Docs/Specs/Active/weekly_rotation_admin/STATUS.md) | `READY_FOR_REDTEAM` → `ARCHITECT_REVIEW_FAIL`. |

---

# Iteration 4 — red-team (2026-09-11 14:43 JST)

Iter-4 addresses the single blocker I named at iter-3: the third R3 site,
`Assets/Editor/ContentArtValidator.cs`, ran the identical masked-art check un-exempted. Fix in
`6eb69d0f5` (`0febb2e1a` = docs). Editor tooling + one test only — no CSV, no publish, no deploy
(live stamp still `70464d323`). I re-generated every number below myself; nothing carried forward.

## The angle I attacked (no-render task → primary source, not the report)

The kickoff pointed me at five knives. I ran each against HEAD, read-only.

**(a) Is the C# predicate EXACTLY the exporter's?** Field-by-field, both trimmed:

| clause | `export_content.py::is_rotation_standin` | `ContentArtValidator.IsRotationStandIn` | equal? |
|---|---|---|---|
| catalog | `catalog_name == "gacha_banners"` | `spec.Name != "gacha_banners" → false` | ✓ |
| rotationId non-blank | `bool((row.get("rotationId") or "").strip())` | `!IsNullOrEmpty(Field(…, "rotationId"))` — and `Field` returns `fields[i].Trim()` (L397) | ✓ (both trim; the whitespace-only edge collapses identically) |
| sprite == stand-in | `(row.get("artSprite") or "").strip() == "GachaBanner_Weekly"` | `(spriteName ?? "").Trim() == WeeklyBannerStandIn` | ✓ |
| constant | `WEEKLY_BANNER_STANDIN = "GachaBanner_Weekly"` | `WeeklyBannerStandIn = "GachaBanner_Weekly"` == `rotation.ts WEEKLY_BANNER_ART` (L53) | ✓ three tools, one string |

Byte-equivalent. (Latent, not a blocker: the C# passes the *current column's* `spriteName` rather
than always reading `artSprite`; harmless while `gacha_banners` has exactly one art column, and it
would fail *safe* — toward flagging — if a second were ever added.)

**(b) Applied at EVERY masked site in the file?** `grep -n "masked" ContentArtValidator.cs`: exactly
one `Verdict = "masked"` emission (L357), and it now carries `&& !IsRotationStandIn(spec, index,
fields, spriteName)` (L348). The C# validator has **no** conflict check (the Python R3 has a second
half, `conflicting_art_report`, that the C# tool never implemented), so there is no second C# site
to exempt. `grep -c "error CS"` on the offline Roslyn build of `Assembly-CSharp-Editor` (the rsp the
kickoff supplied) = **0**.

**(c) Independent boolean simulation on the LIVE CSV** (my own replica of the exact C# branch,
`convention_name` + the exemption, run on `Assets/Resources/Data/gacha_banners.csv`):
```
total banner rows: 7
MASKED before exemption: ['banner_wk_2026_38']
MASKED after  exemption: []
```
`ConventionName("banner_wk_2026_38")` = `GachaBanner_Wk202638` ≠ `GachaBanner_Weekly`, so the row
genuinely trips the check without the exemption and is genuinely clean with it. The stand-in sprite
`Assets/Resources/Art/Gacha/Banners/GachaBanner_Weekly.png` exists, so the exempted row also
resolves (it does not merely trade a `masked` verdict for a `withheld` one).

**(d) Does anything in the build lane consume the "masked" verdict as a DECISION?** No.
`MaskedRowCount` (L191) feeds only `Summary()` report text (L251) and one `Debug.LogWarning` in
`RunAndReport`. `CIBuild.cs:479` wraps `RunAndReport()` in try/catch with **no failure return** ("a
report, not a gate"); `TESTFLIGHT_RUNBOOK.md` documents it is skipped entirely on the standalone
lane and report-only on the game lane. The exemption changes what `Docs/Reports/content_art.txt`
*says*, never whether a build proceeds. (This is why the iter-3 blocker was a false-FAIL-in-a-
committed-artifact, not an outage — restated here from my own re-read, not carried forward.)

**(e) Can a hand-made banner widen it (fake `rotationId`)?** No — proven by the two narrowness tests
in `test_export_check.py`: a tagged banner on **another** shared sprite
(`GachaBanner_StandardClub1`) is still `masked`, and an **un-tagged** banner on the stand-in is
still `masked`. The only exempt shape is (rotation tag ∧ sprite == the stand-in) — which is, by
construction, a real weekly banner, and for which the runtime `GachaBannerArt.SpriteIsOwn` returns
false so the URL art wins and nothing is actually masked. The exemption cannot hide a real
masking defect.

## Fourth-site hunt (kickoff grep, incl. release lane under Docs/Scripts + Tools/)

`grep -rn "masked|ConventionName|SpriteIsOwn" Assets Tools Docs/Scripts` (code files, excl. Specs):
the only **own-name gate** sites are `export_content.py` (`masked_art_report` + `conflicting_art_report`,
both exempted since `b0a70282a`) and `ContentArtValidator.cs` (one site, now exempted). Everything
else is out of scope by inspection:
- `GachaBannerArt.SpriteIsOwn` / `GachaTicketArt.SpriteIsOwn` — runtime *resolvers*; a `false` there
  is the desired stand-in demotion, not an error, so they must **not** be exempted (correct as-is).
- `ContentArtFetcher.cs:212` uses `ConventionName` only to *write* the sprite name; no masked verdict.
- `Docs/Scripts/make_*_pill.py` "masked" = image alpha-masking of gradient pills — unrelated.
- test files — not sites of the shape.

No fourth site.

## Prior-rejection replay (Step 1)

No `CESAR_REJECTION.md` — this task iterated through red-team FAILs, not a Cesar bounce. The one
prior defect to replay is my own iter-3 blocker:

| iter-3 blocker | verdict | proof |
|---|---|---|
| R3 exemption incomplete — `ContentArtValidator.cs` runs the identical masked-check un-exempted; `banner_wk_2026_38` (+ every future weekly banner w/ `artUrl`) would be stamped `masked — FAIL` in committed `content_art.txt`, contradicting `export --check` | **GONE** | `&& !IsRotationStandIn(...)` at L348 (predicate byte-equal to exporter); offline compile 0 errors; live-CSV sim before=1/after=0; `export --check` prints "no row's art is masked by a placeholder"; test #57 greps the exact exemption call + pins the string across all three tools so a regression re-fails |

## Re-run of the ENTIRE SPEC §7 list at HEAD (Rule 5 — re-derived, not re-read)

| Check | Expected | Got (my run) | |
|---|---|---|---|
| offline C# compile (Assembly-CSharp-Editor) | 0 `error CS` | 0 | ✓ |
| C# masked branch on live CSV | 1 before / 0 after | `[banner_wk_2026_38]` / `[]` | ✓ |
| content suite (`python3 -m unittest discover Tools/content/tests`) | 57 | Ran 57, OK | ✓ |
| dashboard `npx vitest run` | 382 | 382 passed (16 files) | ✓ |
| dashboard `npx tsc --noEmit` | clean | exit 0, 0 errors | ✓ |
| `export_content.py --check` (env-file) | clean | "no catalog has drifted, and no row's art is masked" | ✓ |
| published versions | rates 6 / pools 5 / banners 13 / shop 10 / rotations 4 | all match, all `unchanged` (drift check) | ✓ |
| `banner_wk_2026_38` | active v13, artSprite GachaBanner_Weekly, rotationId wk_2026_38, artUrl set | exact (live CSV; drift-clean ⇒ == published) | ✓ |
| artUrl serves | 200 image/jpeg 244928 | HTTP/2 200, image/jpeg, 244928 | ✓ |
| shop_char_mike / shop_ball_putt_ace | inactive | is_active=false both | ✓ |
| shop_wk_2026_38 rows | 13 active | total 13 / active 13 | ✓ |
| rotations wk_2026_36/37/38 | F / F / T | 36 false · 37 false · 38 true | ✓ |
| pity 'weekly' for f2636482… | counter 3 | `golfin_gacha_pity` banner_id="weekly" counter 3 / total_pulls 3 | ✓ |
| deployed stamp / Access | 70464d323 / 302 | no dashboard change this iter → stamp unchanged by construction; Access 302 (unchanged) | ✓ |

Report integrity (Rule 6): every number the implementer/reviewer cited (0 CS errors, before=1/after=0,
57, 382, artUrl 244928) re-derives against primary sources. No fabrication.

## The three break-attempts, and why each FAILED

1. **Behavioral/predicate** — hoped the C# predicate diverged from the exporter (e.g. `IsNullOrEmpty`
   vs `strip()` on `rotationId`, or comparing the wrong column). **Failed:** `Field` trims, so the
   rotationId clause is identical; the sole art column is `artSprite`, so the column passed is always
   the right one; live-CSV sim matches the exporter exactly.
2. **Completeness/scope** — hoped for a second un-exempted masked site in the C# file, or a fourth
   site elsewhere, or a build step that consumes the verdict as a decision. **Failed:** one masked
   site (exempted), no C# conflict check, no fourth own-name gate anywhere, and CIBuild treats it as
   a report with no failure path.
3. **Exploit/over-broad** — hoped a hand-made banner could fake a `rotationId` to mask real art.
   **Failed:** the exemption requires the sprite to be *exactly* the stand-in, in which case the
   runtime resolver never lets the sprite win; the two narrowness tests lock both escape hatches.

## Verdict

**`ARCHITECT_REVIEW_PASS`.** I attacked the fix five ways the kickoff named plus my own three, on
primary evidence I generated this pass, and could not break it. The iter-3 blocker is GONE: the R3
stand-in exemption is now mirrored into `ContentArtValidator.cs` with a predicate byte-equivalent to
the exporter's, at the file's only masked site, verified by an offline compile (0 errors), a live-CSV
simulation (masked before=1 / after=0), a clean `export --check`, and a regression test that pins the
string across all three tools and greps the exact exemption call. It is editor tooling only — no
Unity/Figma/mesh/invariant/clone gate applies. Every SPEC §7 item re-ran green. Advancing to Cesar.

## Files summary (iter-4)

| Path | Change |
|---|---|
| [`REDTEAM_REVIEW.md`](Docs/Specs/Active/weekly_rotation_admin/REDTEAM_REVIEW.md) | Appended iter-4 adversarial record — PASS (blocker GONE; no new blocker found). |
| [`STATUS.md`](Docs/Specs/Active/weekly_rotation_admin/STATUS.md) | `READY_FOR_REDTEAM` → `ARCHITECT_REVIEW_PASS`. |
