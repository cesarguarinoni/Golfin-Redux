# SPEC — `content_cursor_per_catalog`

SPEC_KIND: backend

> Declared for `.claude/hooks/enforce_implementer_done.py` (§7). This task builds no Unity
> surface — no scene, no prefab, no Game View — so the screenshot, figma-reference,
> Figma-fidelity and UI-lint gates do not apply and are skipped. A field, not a prose phrase,
> for the reason §7 gives.

> Follow-up to `content_catalog` (Phase 0). **Must land BEFORE the Unity overlay spec**
> (`content_overlay_texts`, Phase 1) — after a build ships with a scalar cursor this becomes a
> migration instead of an edit.
>
> Filed as its own spec, not an amendment to `content_catalog`: that task is finished, and
> `WORKFLOW_NOTES.md` records that mid-flight amendments do not get implemented.

## Status

`SPEC_READY`. Values per `_TEMPLATE/SPEC.md`.

## Goal

Replace the single scalar `since` cursor on `GET /api/v1/content` with a **per-catalog** cursor.
The scalar cursor cannot express the state of seven independently-versioned catalogs; every
possible scalar choice is either lossy or degenerate, and Phase 0 shipped the safe-but-degenerate
one deliberately.

No Figma, no UI surface. **No Unity behaviour change** — the client that consumes this does not
exist yet.

## Background — why the scalar cursor cannot work

Phase 0's `content_catalog` §B1 specified `version = max(published_version)`. The Implementer
changed it to `min` and flagged it (D-2). **The change was correct and the spec was wrong**, but
`min` is a floor, not a fix. Both fail, differently:

Live catalog versions at the time of writing (measured on prod):
`bags 1 · clubs 1 · items 1 · shop_catalog 1 · balls 5 · characters 5 · texts 9`.

- **`max` (what the spec said) — lossy AND wasteful.** A client stores 9. Every catalog whose
  `published_version` is below 9 then trips the `since > published_version` branch and is sent
  **in full on every boot** (the Implementer measured 610,327 bytes). Remove that branch and it
  becomes silently lossy instead: clubs publishes v2, the client asks `since=9`, `2 > 9` is false,
  and the change is **never delivered**.
- **`min` (what shipped) — safe, but degrades to a full replay.** The client stores 1 and asks
  `since=1` forever, because the cursor is pinned by whichever catalog changes least. Every row
  that has ever moved past v1 is re-sent on every boot, permanently. **The Implementer's "164–172
  bytes per catalog" measurement is real but does not disprove this** — it is small today only
  because the v2–v9 publishes were validation tests that changed no rows, so the
  `IS DISTINCT FROM` guard left every row at v1. The first real text edit starts the ratchet, and
  it never unwinds. This defeats `CONTENT_PIPELINE_PLAN.md` §2 I2 outright.

The plumbing for the correct answer already exists: Stage C's `content_version.txt` is already
written as one `<catalog>=<version>` line per catalog. Only the endpoint and the client cursor
are scalar.

## Implementation

### 1. Request — `since` accepts per-catalog pairs

`GET /api/v1/content?build=<int>&since=clubs:1,texts:9,characters:5`

- Parse `since` as comma-separated `<catalog>:<version>`. A catalog absent from the string is
  `0` → full. Whitespace tolerated; an unparseable pair is treated as `0` for that catalog and
  **logged, not 400** — degrading to "send everything" is always safe, and a client that cannot
  be parsed is exactly the client that needs a full payload.
- **Keep the bare-integer form working**: `since=5` applies 5 to every requested catalog. Nothing
  consumes it today, but a staging build or a curl in a runbook might, and accepting both costs
  three lines.

### 2. Response — the top-level cursor is removed

- **Delete the top-level `version` field.** It has no correct value; leaving it invites a future
  client to store it. Nothing consumes it yet, so deleting is free now and breaking later.
- Keep `latest_version` = `max(published_version)`, documented in the docstring as
  **informational only — never a cursor** (ops/debugging: "which publish is prod on").
- Each catalog keeps its own `version`, `full` and `changed`. That per-catalog `version` is the
  only thing a client may ever store.

### 3. `since > published_version → full` becomes correct

Scope that branch per catalog, where it means what it was meant to mean: a client built against
a staging catalog that is genuinely ahead of this server. It stops firing spuriously the moment
the cursor is per-catalog, so this is a consequence of §1, not extra work.

### 4. `Endpoints.cs`

Replace the Phase 0 property:

```csharp
/// <summary>GET → <c>{data:{fetched_at, enabled, latest_version, catalogs:{…}}}</c> — the
/// admin-managed content delta. No auth, same posture as <see cref="Banners"/>. No trailing
/// slash. <paramref name="since"/> is per-catalog ("clubs:1,texts:9"); each catalog's cursor
/// comes from its own line in Resources/Data/content_version.txt. There is deliberately no
/// single top-level version to store — see content_cursor_per_catalog/SPEC.md.</summary>
public static string Content(string since, int build) =>
    BaseUrl + "/content?since=" + UnityWebRequest.EscapeURL(since ?? "") + "&build=" + build;
```

Still called by nothing. Do not write `ContentService`.

### 5. Fix the live texts drift — and make it impossible to miss again

**The `texts` catalog holds 501 rows; `Assets/Localization/LocalizationText.csv` holds 502.**
Measured on prod against the working tree the day after Phase 0 landed. The A3 round-trip was
genuinely clean when it ran; the CSV then gained a row from in-flight work (`auth_recovery_flow`
and `tournament_restrictions` both had uncommitted `LocalizationText.csv` hunks — see
`TellCode.md`). This is precisely the drift the catalog exists to prevent, arriving within a day.

- Re-import `texts` from the CSV into drafts and publish, so the catalog matches the file.
  Re-verify the count on prod and paste it in the report.
- **Add a CSV-vs-catalog drift check** to `Tools/content/export_content.py --check`: compare row
  counts and id sets per catalog and exit non-zero on a mismatch, naming the missing/extra ids.
  `--check` already exits 0 for exporter idempotence; this makes it also answer "is the catalog
  actually in sync with the repo". That is the check worth wiring into the release pipeline.

### 6. `saleRpCost` — fix the DATA, restore the RULE

Phase 0 relaxed `saleRpCost < rpCost` to a warning because `shop_club_pwedge_royal` ships
`600/600`. **Relaxing was the right call in the moment** — the shipped catalog must be
publishable, and a spec rule that makes real data invalid is the spec's bug, not the data's.

But that row also carries `offer=false` and `popular=false`, so it is not on sale at all; `600/600`
is "no sale" written as an equal price. Blank `saleRpCost` on that row instead, and restore
`saleRpCost < rpCost` as a **blocking** rule for rows that actually set it. An always-warn rule on
a field that is meant to mean "on sale" is a rule that will be ignored.

**DECISION (Architect, 2026-08-25).** Asked; Cesar had no context on the row and delegated the
call. Since `offer=false` and a zero discount is indistinguishable from no discount, the row is
treated as a placeholder: **blank `saleRpCost` on `shop_club_pwedge_royal` and restore
`saleRpCost < rpCost` as blocking.** No gameplay effect either way — it is not on sale in either
form. Reversible in one CSV cell if that turns out to be wrong.

### 7. Widen the STATUS hook's backend detection (tooling, 2 lines)

`.claude/hooks/enforce_implementer_done.py:502` `BACKEND_TASK_RE` matches
``no\s+`?assets/`?\s+changes`` — and `content_catalog/SPEC.md` says "No `Assets/` **edits**".
One word, and it forced four inapplicable gates (screenshot, figma-reference.png, Figma fidelity
table, UI lint) onto a task whose spec opens "No Figma. This task has no UI surface."

**The Implementer left those four failing rather than fabricating evidence. That was correct and
must stay correct** — a hook that can only be satisfied by inventing a screenshot trains people
to invent screenshots.

- Minimal fix: `(?:changes|edits|modifications)` in that alternation.
- **Durable fix, do this too:** stop matching prose. Honour an explicit
  `SPEC_KIND: backend` line near the top of SPEC.md, and add that line to both
  `content_catalog/SPEC.md` and this one. Prose drifts; a declared field does not. Keep the prose
  patterns as a fallback so existing specs still pass.

### 8. Deploy the dashboard — ✅ DONE 2026-08-25 (Architect). Nothing to implement here.

**CONFIRMED LIVE 2026-08-25**, in a browser with a real Cloudflare Access + admin session
(Cesar authenticated; the Architect drove the checks):

| URL | Result |
|---|---|
| `admin.golfin.world/api/audit` | **200**, real `admin_audit_log` rows |
| `admin.golfin.world/api/content` | **404 — "This page could not be found."** |

So it is not an auth problem: the deployed Worker simply does not carry the content routes.
`Tools/admin-dashboard/.open-next/` (what `cf-deploy.sh` emits) is dated **2026-08-19**; the six
handlers are from **2026-08-25**. They are committed and were exercised locally — only the deploy
is missing, and Phase 0's "needs a browser click" item could not have passed as framed.

**Silver lining — the mirror upsert is proven in production data.** The audit log carries the
Implementer's harness runs against the live database, including
`content.publish:characters` with `"mirroredToGolfinCharacters": true` and a field-level diff
(`char_mike` rarity Common → Rare → Common, v3→v4→v5). §A4 of `content_catalog` works end to end;
the local dev server writes to the same prod Supabase, which is why the evidence survived.

**RESOLVED.** The Architect ran `npm run deploy` on 2026-08-25 after clearing all four §4 traps
(no `next dev` running, `NODE_ENV` unset in the shell, devDependencies intact, env file present).
Result: `Uploaded golfin-admin` → `Deployed golfin-admin triggers` → `admin.golfin.world (custom
domain)`, **Version ID `5f6548cd-c93b-4a19-a86f-ef93e93cdc72`**. The §4.3 secret guard ran and
passed — the log carries `bundle carries no service_role key` and `restored
.env.development.local`.

Post-deploy verification:

| Check | Result |
|---|---|
| `https://admin.golfin.world/` (§2 gate) | **302** → `late-cake-f2a4.cloudflareaccess.com` ✓ (a 200 here would mean Access is off) |
| `/api/content` unauthenticated | **302** — behind the Access gate ✓ |
| `/api/content` with a real admin session | **200**, all 7 catalogs, `"mock": false` ✓ |

The live payload also shows the §5 drift in plain sight: `texts` `publishedCount: 501` against 502
rows in `LocalizationText.csv`. Every catalog reports `dirtyCount: 0`, so drafts and published are
in step.

**Phase 0 is fully closed.** The Implementer starts this spec at §1.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] `since=clubs:1,texts:9` returns a clubs delta and a texts delta computed from DIFFERENT cursors (show both)
- [ ] A catalog omitted from `since` comes back `full: true`
- [ ] Bare `since=5` still works and applies to every requested catalog
- [ ] An unparseable pair yields `full` for that catalog and a server log line — not a 400
- [ ] Top-level `version` is GONE from the response; `latest_version` remains and is documented as not-a-cursor
- [ ] Publish `texts`, then re-fetch with the OLD texts cursor and an unchanged clubs cursor: texts returns the changed rows, clubs returns `changed: []` (this is the exact case `max` lost and `min` replayed)
- [ ] `texts` catalog row count on prod == `LocalizationText.csv` data row count (paste both)
- [ ] `export_content.py --check` exits non-zero on a deliberately introduced drift and names the offending ids; exits 0 when clean
- [ ] `shop_club_pwedge_royal.saleRpCost` blanked per §6 and `saleRpCost < rpCost` restored as blocking
- [x] ~~Dashboard deployed + signed-in 200 on `/api/content`~~ — DONE 2026-08-25 by the Architect, see §8
- [ ] `enforce_implementer_done.py` accepts `content_catalog/SPEC.md` as a backend task; the four Figma/screenshot gates no longer fire; NO fabricated evidence was added
- [ ] `/health`, `/notices`, `/banners`, `/tournaments/golfin` all still 200 after deploy
- [ ] Full unfiltered EditMode sweep green (`Endpoints.cs` is in Assembly-CSharp)
- [ ] Spec deviations flagged at the bottom of the report with justification

## Files this task touches

- `playlife/backend/routers/content.py` — cursor parsing, response shape
- `GolfinRedux/Tools/content/export_content.py` — `--check` drift detection
- `GolfinRedux/Assets/Scripts/Net/Endpoints.cs` — `Content()` signature
- `GolfinRedux/Assets/Resources/Data/shop_catalog.csv` — §6, pending Cesar's answer
- `GolfinRedux/.claude/hooks/enforce_implementer_done.py` — §7
- `GolfinRedux/Docs/Specs/Active/content_catalog/SPEC.md` — add `SPEC_KIND: backend`
- `GolfinRedux/Docs/AI_CONTEXT.md`

## Out of scope

- `ContentService`, `RemoteContentSource`, any `*DatabaseCSV.cs` edit, `LocalizationManager` —
  all Phase 1.
- Admin panels (`content_admin_panels`).
- Player inventory, Addressables, art URLs.
- Re-litigating `min` vs `max`. Both are wrong; per-catalog is the answer.
