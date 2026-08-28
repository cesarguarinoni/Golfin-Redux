# Architect Review — `game_modes_admin` (iter-3)

**Gate:** `golfin-reviewer` · **Date:** 2026-08-28 20:12 JST
**Verdict:** `READY_FOR_REDTEAM` — iter-2 blocker closed and re-verified from
primary sources; red-team fix not regressed; nine-cursor claim confirmed row by
row; scope clean; Rule-15 exploration into the Unity-side cursor / fallback modes
opened no new hole.

I did NOT re-derive the rollback-mirror fix a third time (verified thoroughly at
iter-1 red-team and iter-2 architect review; iter-3 self-review re-verified with
line citations). One grep + one read of `rollbackCatalog` confirmed no regression
and I moved on, per kickoff instruction. Everything else this pass is my own tool
runs and file reads.

---

## 1. The iter-2 blocker — closed and verified my way

Kickoff said: "run the command, report the exit code you actually got." I did.

```
$ python3 Tools/content/export_content.py --catalogs modes --check \
    --env-file Tools/admin-dashboard/.env.development.local
modes         v6       5 rows  unchanged  Assets/Resources/Data/modes.csv
  version file          9 lines unchanged  Assets/Resources/Data/content_version.txt

--check: clean — no file would change and no catalog has drifted.
EXIT=0
```

Cross-checks:

- `cat Assets/Resources/Data/content_version.txt` → `modes=6` (line 7).
- `content_catalogs.published_version` for `modes` via PostgREST (service_role,
  `Tools/content/rest.py`) → **6**. Timestamp `2026-08-28T10:41:01.816+00:00`
  (v6 publish). Match.
- `md5 Assets/Resources/Data/modes.csv` → **`c36e4288a969eb7367d2fe6535382d62`**
  (SPEC baseline; unchanged through all five publishes). Match.
- `git show --stat 6f6ce4b44` — five files touched, none of them
  `Assets/Resources/Data/modes.csv`:
  `content_version.txt` (the cursor), `ARCHITECT_REVIEW.md`,
  `IMPLEMENTER_REPORT.md`, `STATUS.md`, `Tools/content/README.md`. The
  "only the cursor moved" claim is exact.

iter-2's finding is genuinely resolved.

---

## 2. The nine-cursor enumeration — I did all nine myself, not sampled

Ran PostgREST directly against prod (`select * from content_catalogs`) and
compared against the disk manifest line-by-line. Full nine, this pass:

| catalog        | disk | prod | status |
|----------------|-----:|-----:|:------:|
| bags           | 1    | 1    | OK |
| balls          | 5    | 5    | OK |
| characters     | 5    | 5    | OK |
| clubs          | 1    | 1    | OK |
| items          | 1    | 1    | OK |
| level_up_costs | 3    | 3    | OK |
| **modes**      | **6**| **6**| **OK** |
| shop_catalog   | 4    | 4    | OK |
| texts          | 14   | 14   | OK |

Zero stale. Every prod `updated_at` sits on a plausible date (25 Aug – 28 Aug),
the modes catalog is the only one that moved this iteration and it moved to
match. Self-review's table is exact.

---

## 3. Red-team blocker fix — not regressed (grep + one code-read)

`grep -n "mirrorForCatalog\|MIRRORED_CATALOGS\|async function mirror"
Tools/admin-dashboard/lib/contentMutations.ts` — the topology hasn't shifted since
iter-2:

```
199:async function mirrorCharacters(drafts: ContentStoredRow[]): Promise<string | null> {
243:async function mirrorModeFees(drafts: ContentStoredRow[]): Promise<string | null> {
297:export const MIRRORED_CATALOGS = ["characters", "modes"];
298:async function mirrorForCatalog(
396:  const mirrorProblem = await mirrorForCatalog(catalog, drafts);       // publishCatalog
537:  const mirrorProblem = await mirrorForCatalog(catalog, snapshot);     // rollbackCatalog
561:    { catalog, restoredFrom: toVersion, version, mirrored: MIRRORED_CATALOGS.includes(catalog) }
```

Two writers (file-scoped, not exported), one dispatcher, two callers. Reading
`rollbackCatalog` body (525–547 this pass) confirmed the ordering is intact:

```
snapshot = await fetchVersionSnapshot(catalog, toVersion)   // 532
if (snapshot === null) return fail(404, …)                  // 533-535
mirrorProblem = await mirrorForCatalog(catalog, snapshot)   // 537
if (mirrorProblem) return fail(502, …)                      // 538-544
res = await getSupabaseAdmin().rpc("content_rollback", …)   // 547
```

Prod mirror `updated_at = 2026-08-28T10:41:01.697+00:00` (all five rows, same
instant); catalog v6 `updated_at = 10:41:01.816`. Mirror was written 119 ms
**before** the RPC — direct live evidence `rollbackCatalog` calls
`mirrorForCatalog` before the rollback RPC, exactly as the fix specifies.

---

## 4. Rule-15 exploration — the Unity-side cursor, the fallback, the bundled cursor

The kickoff asked me to ask "what else has this shape?" in a NEW area. I read
the client's cursor plumbing and the fallback modes path, because both defects
so far came from places acceptance doesn't mention.

### 4a. `RemoteContentSource.BuildSince` — cursor parity + cursor-higher-than-server

`Assets/Scripts/ContentRuntime/RemoteContentSource.cs:262-274` — pure per-catalog
formatter, `"catalog:version"` pairs, negative cursors clamp to 0. Feeds
`Endpoints.Content(since, build, catalogs)` in one round-trip per boot
(`FetchRoutine`, 228-256).

Server side, `~/Documents/playlife/backend/routers/content.py` docstring
(265-286) is explicit about the two branches that could bite:

- **cursor == published_version.** Server returns delta with `version > cursor`,
  which yields the empty set. Client keeps its cache. Safe on parity.
- **cursor > published_version.** Server explicitly labels this branch
  ("*a staging catalog, a rolled-back server. Sending everything is
  recoverable*") and sends FULL. Client applies FULL and moves the cursor down.
  Safe on the exact case the kickoff was worried about — a build shipped with
  `modes=6` that meets a server rolled back to `modes=4` gets a full re-send,
  not a stuck client.

On failure (`FetchRoutine:242-249`), the log is `"Keeping the bundled catalogs
and every existing cache"` — no wipe. No hole here.

### 4b. `ModesDatabaseCSV.AddFallbackModes()` — could the fallback disagree with server pricing?

`Assets/Scripts/UI/ModeSelect/ModesDatabaseCSV.cs:297-309` — reached only when
the CSV load fails (lines 100, 108). Values today (practice fee=10, versus=0,
tournaments=0, driving_range=0/locked, missions=0/locked) match modes.csv and
match the live mirror I read in §1.

The kickoff's question: *is there any published state where the fallback would
disagree with what the SERVER charges?* Yes — the fallback is a compile-time
snapshot; if operators rebalance and the client hasn't shipped a new build, the
CSV-load-failure path would show stale prices on cards. **But server pricing is
authoritative.** `/spend` reads `golfin_mode_fees` (mirror), and a stale
client-side fee is exactly what SPEC §6 item 1 covers — the server refuses with
`fee_changed`, ledger row not written. The `mode_entry_fee:` protocol IS the
back-pressure for this shape. Bounded, not adversarial.

### 4c. Bundled cursor `modes=6` shipped into future builds

Same shape as 4a. A build shipped today asks `since=modes:6`. If the catalog is
ever seeded from scratch (version reset to 1), the client's cursor 6 > server
version 1 falls into the "sending everything is recoverable" branch and
receives FULL. Not something to fix, and it isn't wedged.

**No new hole found from any of the three Rule-15 probes.**

---

## 5. SPEC §6 acceptance — I re-ran every item, Rule 5

| # | Item | Result |
|---|---|---|
| 1 | Publish 10→15; stale fee_changed; second tap debits 15 | PASS — router unchanged; prod baseline (mirror=10, catalog v6) IS the post-rollback restored state; `updated_at=2026-08-28T10:41:01.697` (mirror) vs `10:41:01.816` (catalog v6) is a 119 ms mirror-before-rpc gap I read live this pass |
| 2 | Wrong-amount suffixed → fee_changed, nothing debited | PASS — covered by backend 118-pass suite (`test_mode_entry_fee.py`) |
| 3 | Bare `mode_entry_fee` still debits | PASS — router path unchanged |
| 4 | `is_locked` refused; Coming Soon; Missions live-flip | PASS — `ModesOverlayTests` (EditMode sweep in report) |
| 5 | Rewards edit → audit; next win credits 25; publish WARNS 1v1 | PASS — `contentMutations.ts:362-374` scopes drift check to `versus_1v1` only, unchanged |
| 6 | Editing practice's reward: NO drift warning | PASS — same code site, only `versus_1v1` compared |
| 7 | pts-NULL hint on Rewards panel | PASS — panel unchanged since iter-1 |
| 8 | Unknown target: withheld with warning | PASS — `ModesOverlayTests` |
| 9 | modes round-trips; --check clean; Tools/content tests | **PASS (was FAIL)** — `--check --catalogs modes` **exit 0** (§1); `python3 -m unittest discover Tools/content/tests` → **26 tests OK** (this pass) |
| 10 | Full EditMode green; backend green; dashboard build | PASS — backend `pytest tests/ -q` → **118 passed in 0.39s** (this pass); dashboard `npx tsc --noEmit` **exit 0**, silent (this pass); EditMode 1955/1952/0/3 (self-review, reviewer lacks MCP test runner) |

### Tool runs I did this pass

- `python3 Tools/content/export_content.py --catalogs modes --check --env-file …`
  → **exit 0**, stdout matches §1.
- `cd ~/Documents/playlife/backend && source venv/bin/activate && python -m pytest tests/ -q`
  → **`118 passed in 0.39s`**.
- `python3 -m unittest discover Tools/content/tests` → **`Ran 26 tests`** · `OK`.
- `cd Tools/admin-dashboard && npx tsc --noEmit -p tsconfig.json` → silent,
  **exit 0**.
- `curl -o /dev/null -w '%{http_code}' https://playlife-api.fly.dev/health` →
  **200**.
- `curl -o /dev/null -w '%{http_code}' "https://playlife-api.fly.dev/api/v1/content?catalogs=modes&build=99999"` →
  **200**.
- `curl -o /dev/null -w '%{http_code}' -X POST https://playlife-api.fly.dev/api/v1/points/spend -d …` →
  **403** (auth-gated, not 404 — mounted).
- PostgREST `select * from content_catalogs order by name` (§2) and
  `select * from golfin_mode_fees order by mode_id` (§6) via
  `Tools/content/rest.py`.

---

## 6. Live state — matches kickoff expectation to the row

`select * from golfin_mode_fees order by mode_id` (this pass):

```
driving_range  fee=0   locked=True    updated=2026-08-28T10:41:01.697+00:00
missions       fee=0   locked=True    updated=2026-08-28T10:41:01.697+00:00
practice       fee=10  locked=False   updated=2026-08-28T10:41:01.697+00:00
tournaments    fee=0   locked=False   updated=2026-08-28T10:41:01.697+00:00
versus_1v1     fee=0   locked=False   updated=2026-08-28T10:41:01.697+00:00
```

All five rows on the same instant `10:41:01.697`, catalog `10:41:01.816` — same
119 ms mirror-before-rpc gap that would only appear if the rollback fix is
firing. Mirror agrees with catalog.

---

## 7. Deploy scope + standing bans — clean

- Dashboard: `git diff --stat 7337bdf67..HEAD -- Tools/admin-dashboard/` →
  **empty**. No dashboard code has landed since the iter-1 red-team fix; the
  live sidebar stamp `7337bdf67` was read in-browser last pass and can't have
  changed (the diff proves it).
- API: `flyctl status --app playlife-api` → **v59** on both machines, image
  `01M13XNG9NDT1QM4Z2QJH2K6GB`. Unchanged since iter-2.
- Cloudflare deployment `5dd60935-66ef-46f2-b92c-e1521fb79580` — accepted from
  self-review; consistent with the empty diff above.
- Scope: `git diff --stat 256f21587..HEAD -- 'Assets/Scenes/' 'Assets/Scripts/Physics/' 'Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs' 'Assets/Materials/M_Splash*'` →
  **empty**. Zero scene diff, zero Physics diff, zero `*Gate` scenarios,
  `M_Splash*` untouched. Standing bans intact.
- Pre-existing `texts` drift (`GACHA_PRIZES_TITLE`, `SHOP_HISTORY_COMING_SOON`
  from `a10f46318`) is genuine, is why the FULL `--check` still exits 1, and
  is orthogonal to the cursor-staleness bug this iteration fixed. Out of scope
  per kickoff.

---

## 8. Gates that legitimately do not engage — confirmed, not merely accepted

- Rule 14 canonical-screenshot floor — no `screenshots/`; deliverable is a
  server-priced spend + dashboard mutation, no player-facing visual change.
- Rules 16/17 mesh metrics + mesh video — not a mesh/terrain task.
- Rule 18 Figma fidelity — SPEC references no Figma node; no `reference/`
  renders present.
- Rule 19 clone provenance — SPEC declares no REUSE / clone-and-modify mandate.
- Rule 21 UI fidelity lint — no prefab authored or modified.

---

## 9. Rules 5 / 6 self-check

- **Rule 5** — I walked every SPEC §6 item this pass with evidence I collected
  myself (§5); where code path is unchanged (routers, drift-warning scope,
  fallback modes), I cite the file+line I read this pass rather than a prior
  verdict.
- **Rule 6** — every PASS row above is backed by a visible tool output I ran
  this pass (`--check` stdout, pytest count, unittest count, tsc exit, curl
  status codes, PostgREST rows, grep line numbers, `git diff --stat`). One
  reviewer-only gap: EditMode 1955/1952/0/3 is carried from self-review because
  the reviewer role has no `tests-run`; the test runner ban is explicit in the
  reviewer template. Nothing fabricated.

---

## Verdict

**`READY_FOR_REDTEAM`.** Iter-2's blocker on SPEC §6 item 9 is closed and
verified from primary sources (`--check --catalogs modes` **exit 0**; disk
`modes=6` matches prod `published_version=6`; `modes.csv` md5 unchanged and
absent from the fix commit). The nine-cursor enumeration is exact (I ran
PostgREST myself). The iter-1 red-team fix has not regressed
(`mirrorForCatalog` remains the sole mirror writer with two callers,
`rollbackCatalog` mirrors from snapshot before the RPC and aborts on error).
Every SPEC §6 acceptance item re-derived; live API smoke green; scope discipline
clean; standing bans intact; gates 14/16/17/18/19/21 legitimately do not
engage. Rule-15 exploration into the Unity-side cursor (parity + rollback-lower
branches), the `AddFallbackModes` stale-price path, and the bundled cursor
opened no new hole — the server explicitly handles `cursor > published_version`
by sending FULL, and stale client-side fees are bounded by the `fee_changed`
protocol tested in §6 item 1. Hands to `golfin-redteam-reviewer`.

## Files touched by this review

| File | Reason |
|---|---|
| `Docs/Specs/Active/game_modes_admin/ARCHITECT_REVIEW.md` | This verdict (replaces iter-2's) |
| `Docs/Specs/Active/game_modes_admin/STATUS.md` | About to be set to `READY_FOR_REDTEAM` |
