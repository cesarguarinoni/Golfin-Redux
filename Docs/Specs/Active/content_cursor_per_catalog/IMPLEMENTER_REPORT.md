# Implementer Report — `content_cursor_per_catalog`

**Iteration shape:** `content-api:scalar-cursor`
**SPEC_KIND:** backend — no Unity surface, no scene, no prefab, no Figma node, no Game View.
The screenshot / figma-reference / Figma-fidelity / UI-lint gates do not apply and are skipped
by `enforce_implementer_done.py` (that skip is itself part of this task — §7).

## Implementation summary

`GET /api/v1/content` now takes a **per-catalog** cursor (`since=clubs:1,texts:9`) and the
top-level `version` field is **gone**. Each catalog is evaluated against its own cursor, so the
`since > published_version → full` branch finally means what it was meant to mean instead of
firing on every catalog that merely publishes less often than the busiest one. `latest_version`
survives, documented as informational-only. The bare-integer form still works and applies to
every catalog. An unparseable fragment degrades that catalog to `full` and logs a warning — never
a 400.

Alongside it: the exporter's `--check` gained a CSV-vs-catalog id-set drift gate (the case it
provably could not see before), `saleRpCost < rpCost` is a blocking rule again with the offending
row's data fixed rather than the rule bent, and the STATUS hook learned to recognise a backend
task from a declared field instead of a matched prose phrase.

## Files modified or created

| Path | Change |
|---|---|
| `playlife/backend/routers/content.py` | modified — `parse_since()` (per-catalog / bare-int / junk-tolerant), per-catalog `full` branch, top-level `version` deleted, `logger.warning` on junk, module docstring rewritten with the measured rationale |
| [Tools/content/export_content.py](Tools/content/export_content.py) | modified — `drift_report()` + `_sample()`; `--check` now fails on id-set drift as well as on a stale file, with separate messages; a plain export also exits 1 on unresolved drift |
| [Tools/admin-dashboard/lib/contentValidate.ts](Tools/admin-dashboard/lib/contentValidate.ts) | modified — `saleRpCost >= rpCost` is a blocking **error** again (§6); the equal case gets its own message pointing at BLANK as the way to say "no sale" |
| [Assets/Resources/Data/shop_catalog.csv](Assets/Resources/Data/shop_catalog.csv) | modified — `shop_club_pwedge_royal.saleRpCost` `600` → blank (§6) |
| [Assets/Resources/Data/content_version.txt](Assets/Resources/Data/content_version.txt) | modified — exporter output after the two publishes: `shop_catalog=2`, `texts=11` |
| [Assets/Scripts/Net/Endpoints.cs](Assets/Scripts/Net/Endpoints.cs) | modified — `Content(string since, int build)` with `UnityWebRequest.EscapeURL`; added `using UnityEngine.Networking;`; docstring rewritten (per-catalog cursor, no top-level version, `latest_version` is not a cursor) |
| [.claude/hooks/enforce_implementer_done.py](.claude/hooks/enforce_implementer_done.py) | modified — `BACKEND_TASK_RE` widened to `(?:changes\|edits\|modifications)`; new `SPEC_KIND_RE`; `spec_is_backend_task` honours the field first; Rules 18 + 21 scoped to `not is_backend` |
| [.claude/hooks/test_enforce_implementer_done.py](.claude/hooks/test_enforce_implementer_done.py) | modified — 8 new `TestBackendExemption` cases covering the synonyms, the field, markdown decoration, the prose-backtick negative, the Rule-18/21 scoping, and the two real spec files |
| [Docs/Specs/Active/content_catalog/SPEC.md](Docs/Specs/Active/content_catalog/SPEC.md) | modified — `SPEC_KIND: backend` declared at the top |
| [Docs/Specs/Active/content_cursor_per_catalog/SPEC.md](Docs/Specs/Active/content_cursor_per_catalog/SPEC.md) | modified — `SPEC_KIND: backend` declared at the top |
| [Docs/AI_CONTEXT.md](Docs/AI_CONTEXT.md) | modified — session status |
| [.../content_cursor_per_catalog/acceptance_probe.txt](Docs/Specs/Active/content_cursor_per_catalog/acceptance_probe.txt) | created — the full endpoint transcript this report cites |
| `Docs/TellCode.md`, `Docs/Versioning/last_uploaded_build.txt` | **NOT MINE.** Already `M` in the kickoff baseline (`HEARTBEAT.log`, iter-1) before any work started. Untouched. |

## Screenshot

**N/A — backend task.** There is no Game View to capture: this task changes one FastAPI router,
two Python/TypeScript tools, one CSV cell, one C# URL builder and one hook. Fabricating a frame to
satisfy a gate is the failure mode §7 exists to remove, so no screenshot was invented. The
equivalent evidence is `acceptance_probe.txt` — a complete, reproducible endpoint transcript.

## Verification setup

A local `uvicorn main:app` on **the real `routers/content.py`**, in a venv built from
`backend/requirements.txt`, pointed at **the real prod Supabase** (`--env-file
Tools/admin-dashboard/.env.development.local`). No fake, no reimplementation of the handler — the
production module, imported by the production `main.py`. Server stopped after the run.

Live versions during the run: `bags 1 · balls 5 · characters 5 · clubs 1 · items 1 ·
shop_catalog 2 · texts 11`.

## Acceptance checklist

| Item | Verdict | Evidence |
|---|---|---|
| `since=clubs:1,texts:9` returns clubs and texts deltas from DIFFERENT cursors | PASS | `acceptance_probe.txt` ACC-1. A: `texts:9,clubs:1,shop_catalog:1` → texts `changed=[HOME_MAINTENANCE_TITLE]`, shop `changed=[shop_club_pwedge_royal]`, clubs `changed=[]`. B moves ONLY the texts cursor → texts empties, shop still returns its row. C moves ONLY the shop cursor → shop empties, texts still returns its row. Each catalog responds to its own cursor and nothing else. |
| A catalog omitted from `since` comes back `full: true` | PASS | ACC-2: `since=texts:11&catalogs=texts,clubs` → `clubs full=True changed=799`, `texts full=False changed=0`. |
| Bare `since=5` still works and applies to every catalog | PASS | ACC-3: `since=1` → all seven evaluated at cursor 1 (2,177 B); `since=11` → six of seven trip `cursor > published` and come back full (610,333 B). Both 200. `parse_since` unit matrix: `"5"`, `" 5 "`, `"-3"`→0 all pass. |
| An unparseable pair yields `full` for that catalog + a server log line, not a 400 | PASS | ACC-4, all HTTP **200**. `texts:banana,clubs:1` → texts `full=True`, and **clubs keeps its delta** (`full=False`) — the degradation is surgical, not global. Server emitted `content: unparseable since fragment(s) ['texts:banana'] … those catalogs are FULL` (pasted at the foot of `acceptance_probe.txt`). Also covered: `totally-garbage`, `clubs:1,,:9,texts`. |
| Top-level `version` GONE; `latest_version` remains, documented as not-a-cursor | PASS | ACC-5: `top-level keys = ['catalogs','enabled','fetched_at','latest_version']`, `'version' in d = False`, `latest_version = 11`. Documented as informational-only in the `content.py` module docstring, the handler docstring, the return-value block comment, and `Endpoints.Content`'s XML doc. |
| Publish `texts`, re-fetch with the OLD texts cursor + unchanged clubs cursor: texts returns changed rows, clubs returns `changed: []` | PASS | The exact case `max` lost and `min` replayed. `texts` published twice (v9→v10 change, v10→v11 revert; one row moved each time — the `IS DISTINCT FROM` guard held). ACC-1A: `since=texts:9,clubs:1` → texts `changed=['HOME_MAINTENANCE_TITLE']`, clubs `changed=[]`. |
| `texts` catalog row count on prod == `LocalizationText.csv` data row count | PASS | **The spec's premise was wrong — see § Spec deviations D-1.** Both are **501**, and the id sets are identical (0 missing, 0 extra). `LocalizationText.csv` is 503 physical lines = 1 header + 1 mid-file `#` comment + **501** key rows. The "502" was that comment counted as data. Exporter agrees: `texts v11 501 rows unchanged`. |
| `--check` exits non-zero on deliberate drift naming the ids; exits 0 when clean | PASS | Clean: `--check: clean — no file would change and no catalog has drifted.` **EXIT 0**. Drift injected (`DRIFT_PROBE_KEY` appended to the CSV — the exact 502-row shape §5 describes): `texts: DRIFT — 502 row(s) … vs 501 in the catalog. / 1 id(s) in the CSV but NOT in the catalog …: DRIFT_PROBE_KEY` → **EXIT 1**, *and zero files would have changed*, which is precisely why the old `--check` could not see it. Reverse direction (row removed from the CSV) also caught and named. CSV restored; re-check EXIT 0. |
| `shop_club_pwedge_royal.saleRpCost` blanked and `saleRpCost < rpCost` restored as blocking | PASS | CSV blanked + published to prod (`shop_catalog` v1→v2, exactly one row moved). Regression matrix on the real `validateCatalog` against the real live drafts: blank → clean; `600` (the Phase 0 warn case) → **ERROR**; `700` → **ERROR**; `-5` → **ERROR**; `450` → clean; `0` → clean. `tsc --noEmit` exit 0. Gameplay proven inert **in Unity through the production loader**: `GeneralShopCatalog.Entries` → `shop_club_pwedge_royal rp=600 sale=0 HasSale=False Effective=600`, identical to the 600/600 it replaced (`HasSale` was already false there), other 4 rows untouched. |
| ~~Dashboard deployed + signed-in 200 on `/api/content`~~ | PASS | Done by the Architect 2026-08-25, not by this iteration. §8, Version ID `5f6548cd-c93b-4a19-a86f-ef93e93cdc72`. Not re-verified here — out of scope per the kickoff. |
| Hook accepts `content_catalog/SPEC.md` as backend; the four gates no longer fire; NO fabricated evidence added | PASS | `spec_is_backend_task(content_catalog/SPEC.md) = True` (was False — it said "No `Assets/` **edits**"). Same for this spec. Rules 18/21 now gated on `not is_backend`; the node detector still fires on `content_catalog` because `FIGMA_NODE_ID_RE` matches the **date** `2026-08`, which is why `is_backend` had to be the gate rather than the detector. 147 non-backend specs scanned: **zero** new false exemptions (the one hit, `figma_node_spec_generator`, is the spec the original regex was written for). 8 new tests, all pass. **No screenshot, no figma-reference.png, no fidelity table and no lint JSON were invented.** End-to-end proof: the hook was dry-run on this task's own `READY_FOR_ARCHITECT_REVIEW` transition and returned **EXIT 0** — it raised no screenshot, figma-reference, fidelity or lint objection at any point, only ordinary format issues (verdict cells, baseline block), which were fixed in the report rather than papered over with invented artifacts. |
| `/health`, `/notices`, `/banners`, `/tournaments/golfin` all still 200 | PASS | All four **200 on prod** and **200 on the local server running this change**, side by side. `/api/v1/content` 200 on both. |
| Full unfiltered EditMode sweep green | PASS | Green for this change; 17 failures that predate it. `tests-run EditMode`, unfiltered: **1550 total, 1530 passed, 17 failed, 3 skipped**. All 17 are `Golfin.Save.Tests` asserting `CurrentSchemaVersion == 9` against a committed `10`, plus one `Golfin.Tournaments` STR 6-vs-7. `CurrentSchemaVersion = 10` landed in commit `bade0e2f4` (a roster commit), several commits before this session's HEAD `caed8ed7f`. My working tree touches **no file** in `Golfin.Save`, `Golfin.Tournaments` or their tests. The iter-1 baseline DIRTY block lists exactly two modified files outside this task's folder, ` M Docs/TellCode.md` and ` M Docs/Versioning/last_uploaded_build.txt`, neither of which is a Save or Tournaments source; so these failures were already failing at baseline HEAD `caed8ed7f`, and their cause `CurrentSchemaVersion = 10` is committed in `bade0e2f4`. Zero failures in `Golfin.Net` (where `Endpoints.cs` lives). |
| Spec deviations flagged with justification | PASS | Four, below. |

## Spec deviations

**D-1 — §5's premise was wrong: there is no texts drift, and there was none to fix.**
The spec says the catalog holds 501 rows against 502 in `LocalizationText.csv`. Measured against
prod and the working tree: **501 = 501, identical id sets, zero value differences, and
`export --check` clean before I changed anything.** `LocalizationText.csv` is 503 physical lines —
1 header, 1 mid-file `#` comment, 501 key rows. The "502" is 503 − 1 header, counting the comment
as a row. `catalogs.py`'s own docstring already records this trap ("the 502nd parsed line is a `#`
comment sitting in the MIDDLE of the file"), and the dashboard's `publishedCount: 501` was right.
The in-flight `LocalizationText.csv` hunks the spec blames are no longer in the tree.

So the §5 *re-import and publish* was a no-op and I did not dress one up as a fix. What §5
actually asked for that had teeth — **the drift check** — is built, and it is built to catch the
id-set case rather than the count case, because two files can hold 501 rows each and disagree
about which 501. The `texts` publishes on the record (v9→v11) are the **acceptance-6 round trip**,
not a drift repair, and they are labelled as such in the audit log.

**D-2 — `Endpoints.cs` is in assembly `Golfin.Net`, not `Assembly-CSharp`.**
The acceptance list says "(`Endpoints.cs` is in Assembly-CSharp)". It is not — reflection over the
loaded domain reports `assembly : Golfin.Net`. Immaterial to the outcome (the sweep is unfiltered
either way), but the parenthetical should be corrected in any follow-up spec so nobody scopes a
future test run to the wrong assembly.

**D-3 — the `texts` acceptance publish used a deliberately dead row, and was reverted.**
`HOME_MAINTENANCE_TITLE` — the key the CSV's own mid-file comment flags as no longer read by
`HomeScreenController` (Home notices come from `/api/v1/notices`), and the same row Phase 0 used
for its A3 round trip. Changed → published v10 → probed → restored → published v11. The value on
prod is byte-identical to the repo CSV, and `export --check` is clean. Net effect: `texts` version
9 → 11, no content change. Versions only ever move forward by design, so this is not reversible
and did not need to be.

**D-4 — deploying `playlife-api` to fly.io is NOT done, and is Cesar's call.**
Prod still serves the Phase 0 scalar shape (`top-level keys` include `version`, and
`version == latest_version == 11`). The change is verified but undeployed. Deploying a shared
production API is an outward-facing action I do not take unasked, so it is surfaced rather than
performed. `flyctl v0.4.84` is installed and authenticated as `cesar.guarinoni@wonderwall-g.com`,
so it is one command away. Two things make it low-risk when Cesar says go: **nothing consumes this
endpoint** (no `ContentService` exists — Phase 1), and the bare-int form keeps any runbook curl
working. Item 12's "all still 200 **after deploy**" is therefore satisfied against the local
server carrying the change, and prod is unchanged.

## What the numbers say about why the scalar cursor had to go

Same live data, same endpoint, three cursors (`acceptance_probe.txt` ACC-3):

| cursor | bytes per boot | what happens |
|---|---|---|
| `max` (11) | **610,333** | six of seven catalogs trip `cursor > published` and download in full, every boot |
| `min` (1) | **2,177** | safe, but replays every row that ever moved past v1 — **already up from Phase 0's 1,407 B measured one day earlier**, which is the ratchet starting |
| per-catalog | **454** | steady state, `changed: []` everywhere |

The min ratchet is not a projection: 1,407 → 2,177 bytes in a single day of ordinary publishing,
and nothing ever removes a row from that replay. That is the number that settles it.
