# REDTEAM_REVIEW — `content_art_bundling`

**Iteration:** 1
**Date:** 2026-08-28 12:16 JST
**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Verdict:** **ARCHITECT_REVIEW_FAIL** — one concrete blocker, proven by observation.

Commit under review `541864b38`, baseline `632d42417`. No `CESAR_REJECTION.md` (first-time task,
nothing to replay). Rules 16/17/18/19/21 correctly N/A (no Figma node / mesh / reuse mandate /
world→screen); not re-litigated.

---

## BLOCKER — the collision guard is case-blind on a case-insensitive filesystem (SPEC §4 violated)

SPEC §4 is absolute: *"Collision is a REFUSAL, never an OVERWRITE… an artist's hand-made asset must
never be replaced by a downloaded one."* The tool's sole collision gate is
`ContentArtFetcher.ExistingAsset` (line 646), which compares filenames with
**`StringComparison.Ordinal`** (case-SENSITIVE). The dev/CI/build platform is macOS APFS, which is
case-INSENSITIVE. So a derived name that differs from an existing asset only by case is NOT detected
as a collision, and the subsequent `File.WriteAllBytes` (line 623 — no `File.Exists` guard) resolves
to the existing file's inode and **overwrites the artist's asset's bytes**.

### OBSERVED (not argued)

1. Filesystem is case-insensitive — wrote `Foo.txt` then `foo.txt` in scratch; result was ONE file
   `Foo.txt` containing `foo.txt`'s bytes. `diskutil` confirms `/` is APFS.
2. Invoked the real private `ContentArtFetcher.ExistingAsset` via reflection against the live
   `Assets/Resources/Portraits/Thumbnails` folder (contains `James.png`):
   ```
   [PROBE] ExistingAsset(...,"James") = Assets/Resources/Portraits/Thumbnails/James.png
   [PROBE] ExistingAsset(...,"james") = NULL   <-- case-only variant; collision NOT detected
   ```
   `NULL` means the guard waves the row through → the tool downloads and writes `james.png` → APFS
   overwrites `James.png`. The `.meta` (GUID) stays, so the artist asset silently becomes the
   admin upload. Data loss, no error.

### Why both prior gates missed it

Both verified collision with a SAME-CASE example only (`char_JAMES` → derives `James`, matching the
existing `James.png` exactly). That is the flattering angle — it exercises the one path where Ordinal
and the filesystem agree. Neither tested a case-variant. This is exactly the attack the coordinator
named ("differs only by case; macOS is case-insensitive").

### Reachability is real, not exotic

The derive DELIBERATELY normalizes case: `BrandPascal` lowercases interior letters (its own doc
comment: `"MireO" → "Mireo"`). So any hand-made asset with interior caps diverges from the mechanical
derive. The working tree already carries hand-dropped club art like `Clubs/Full/Driver-FairX.png`
(capital X); the `fullUrl` derive `{ClubArtType}-{BrandPascal}` would produce `Driver-Fairx` for a
`fairx`/`FairX` brand — a case-only divergence from `Driver-FairX.png`. A clubs row with that
brand + a `fullUrl` set + an empty `portraitFull` column would silently overwrite it. Trigger
conditions are narrow (empty name + set URL + a case-mismatched existing asset), but the tool is
designed to run repeatedly against a growing `Resources/` folder on macOS, and the guarantee it
breaks is the tool's core safety property.

### Fix

- `ExistingAsset`: compare with `StringComparison.OrdinalIgnoreCase` so a case-variant is DETECTED
  and REFUSED (the folder scan already enumerates every file; only the comparer is wrong).
- Make the same-run dedup key (`o.Folder + "/" + o.DerivedName` + the `produced` dict, line 542/636)
  case-insensitive for consistency, and belt-and-braces the write path with a case-insensitive
  existence check before `File.WriteAllBytes`.
- Add a test: existing `James.png` + a row deriving `james` (and a club `FairX`-vs-`Fairx` case)
  must be REFUSED with the existing bytes byte-identical afterward.

---

## The other attacks — why each FAILED to break it

### 1. Rule-2 loader fix `?? "" → ?? row's own URL` — SOUND across every enumerated sub-case
Traced `CatalogArtCache.Cached(url, bundledUrl)` (returns null iff `url==bundledUrl` or url empty).
- **Empty-URL rows:** `IsNullOrEmpty(url)` short-circuits to null both before and after — no change.
- **Character/item/ball:** the merged-overlay callsite passes `bundled.portraitUrl` (a parsed value
  = `""`, never null, when the build had no URL). The fix's `?? portraitUrl` engages ONLY when the
  param is null, which happens only at the bundled-parse (line 111) and appended (line 162) callsites
  — genuinely-no-counterpart paths where step 3's unconditional `Cached(url)` still renders URL-only
  rows. A newly-added overlay URL still fires rule 1 because `bundled.portraitUrl == ""`, not the new
  URL.
- **Clubs:** `ClubCsvRow.portraitUrl` defaults to `""` (line 67) and `f.Get` returns `""`, so
  `row.bundled?.portraitUrl` is null ONLY when `row.bundled` is null (no overlay). The `?? row.portraitUrl`
  never engages the "shipped-without-URL, overlay-added-one" sub-case, so re-upload still wins.
- **Clubs Placeholder rung:** HALF-2 (no bundled art + URL) → step 2 `LoadRealSprite` null → step 3
  cached URL → rung 4 Placeholder skipped. Correct ordering preserved.
No regression found.

### 2. CSV splice (`ParseCsvSpans`/`SetField`) — safe on the actual data
Inspected all four CSVs: no BOM, LF-only, no multi-line quoted fields (every physical line has
balanced quotes), and the sprite-NAME columns precede the URL columns — so a URL-bearing row can
never put the name column past the row's field count. Derived names with `, " \r \n` are refused
(line 502). Header-lacks-column → slot skipped. Escaped-quote spans are raw-index accurate.
`SetField` out-of-range returns the line unchanged. (Latent, not live: a short row whose name column
is beyond its fields would silently skip the splice while the asset is written — impossible on these
CSVs because URL columns sit after name columns.)

### 3. Validator report seam — sound in all four orderings
`WriteReport` preserves everything from `ContentArtFetcher.LogMarker` to EOF on rewrite; the fetcher
appends to full existing content. fetch→validate, validate→fetch, fetch×2, validate×2 all retain the
fetch log; no mutual erasure.

### 4. Allowlist — not bypassable
`IsArtAllowed` → `TournamentArtPolicy.IsAllowedUnder`: exact scheme+host Ordinal match, userinfo
rejected, default-port required, path-under-bucket, `..`/`%2e` rejected. Single call site (line 547),
no re-implementation. The `evil.example.com/…/catalog-art/…` substring attack fails on host.

### 5. Stale-sprite hazard — told straight, fail-safe holds
`CatalogArtCache.ResetForTest` does not clear `TournamentArtService._sprites`. Confirmed the three
`ContentArtLadderHandoverTests` all assert texture width `== 8` (or the bundled asset path), so a
stale non-8×8 sprite turns them RED, never falsely GREEN; a stale sprite that IS 8×8 is the correct
answer anyway. In production a URL is content-hashed → stable bytes per session, so the dict can't go
stale. No false green anywhere. The reviewer's HALF-2 identity gate is `AssetDatabase.GetAssetPath`
(empty for any runtime sprite), unaffected by which runtime sprite the dict returns.

### 6. Reverted-test claim — verified
`Assets/Tests/EditMode/ContentArtFetchTests.cs` is byte-clean vs the committed tip (git status empty).

---

## Independently re-run acceptance evidence

- **Full unfiltered EditMode sweep (RAN, mine):** `1894 total / 1891 passed / 0 failed / 3 skipped`
  (00:01:24). The 3 skips are the pre-existing `HoleCompleteDriverTests` Stage-C1 skips. Green on the
  first call.
- **Collision guard (OBSERVED, mine):** the case-blind gap above — the one acceptance item (§7 #4)
  that fails.
- Deviations 1–3 (clubs per-folder, balls omit `-rarity`, OLD-build rung 3): independently judged
  SOUND — verified Balls.csv header has no `rarity` column; both cached-URL rungs render identically.

## Cleanup
Probe was read-only reflection. Task-owned CSVs + `content_art.txt` byte-clean, no fixture assets in
`Resources/`, HEAD `541864b38` unchanged, scratch removed.

---

**ARCHITECT_REVIEW_FAIL.** One blocker: the collision guard must be case-insensitive on the target
filesystem or it will silently overwrite an artist's asset — a direct violation of the SPEC §4
"never overwrite" invariant, proven by observation. Fix the comparer + add the case-variant test,
then re-submit.

---

# REDTEAM_REVIEW — iter-5 (§22 shape-audit completeness gate)

**Iteration:** 5
**Date:** 2026-08-28 13:47 JST
**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Commit under review:** `777487f12` · baseline `632d42417` · delta `93d00e923 → 777487f12`
**Verdict:** **ARCHITECT_REVIEW_FAIL** — one blocker: the §22 side-effect enumeration was NOT
complete. The undo-orchestration that makes the S1 fix work (verification loop + `FinalizeCsvs`)
is itself an unguarded site of the same shape, and was omitted from the eight-site table.

Self-review + reviewer gates deliberately skipped this iter (STATUS.md, Cesar's decision). Per the
brief my primary question is not "find another instance" but "WAS THE EIGHT-SITE ENUMERATION
COMPLETE?" It was not.

---

## BLOCKER — the S1 undo can be bypassed by a throw; that site is missing from the audit table

### What the audit claimed
`IMPLEMENTER_REPORT § iter-5`: *"All EIGHT side effects in the file were checked … Same shape as
iter-3a, one level out."* Table rows S1–S8, each answered A (validated before effect?) and B (undone
on downstream failure?). S1 (`File.WriteAllBytes` the art) was marked a DEFECT and fixed by moving
`allPending.Add(pending)` to the TOP of `ProcessCatalog` (line 647).

### The site they missed
The S1 fix only works **if `FinalizeCsvs` actually runs** — that is where a Fetched-but-not-verified
row's asset is deleted and its splice reverted. But the phase that runs it is **unguarded**:

```
555  try { StartAssetEditing(); foreach(spec) { try { ProcessCatalog } catch {…} } }   // per-catalog catch
567  finally { StopAssetEditing(); Refresh(); }
575  foreach (var o in report.Fetched.ToList()) ApplyAndVerifyImport(o, report);   // ← NO try/catch
580  FinalizeCsvs(allPending, report);                                             // ← NO try/catch
```

Lines 575–583 sit **outside** every try/catch. `FetchMenu()` (529) also wraps `Run()` in nothing.
So a thrown exception anywhere in the verification loop (or before `FinalizeCsvs` completes)
propagates uncaught out of `Run()` and **`FinalizeCsvs` never runs** — the S1 cleanup is skipped.

The audit's own stated protective mechanism — *"Run catches per-catalog and carries on"* — is the
`try/catch` at line 560, which wraps **only** `ProcessCatalog`. It does **not** extend to the
post-batch phase. So question **B** ("if anything downstream fails, is the effect undone?") was
answered only for the **refusal-verdict** failure mode. It was never answered for the
**thrown-exception** failure mode of the undo phase itself. That is the shape "stated too narrowly"
the brief asked me to check for: `fails` was read as "returns Refused", not "throws".

### Why this is the SAME shape as S1 (traced against the live code)
Row `char_x`, valid URL, empty name:
1. `ProcessCatalog` registers `pending` in `allPending` (647), `TryFetchOne` does
   `File.WriteAllBytes` of `X.png` (921), `Verdict=Fetched`, `WrittenPath` set. Returns normally.
2. `finally`: `StopAssetEditing` + `Refresh` → `X.png` + `.meta` now on disk.
3. Line 575: `ApplyAndVerifyImport(char_x)` **throws** (e.g. `SaveAndReimport` on a pathological
   asset, or `Directory.GetFiles` in `FindSibling` if the folder was removed concurrently — this is
   a shared Editor).
4. Exception exits `Run()` uncaught. `FinalizeCsvs` **never runs**.
5. Residue: `X.png` + `.meta` orphaned on disk, `Verdict` still `Fetched`, no cleanup — the exact
   *"assets on disk … named by no CSV"* state S1 was fixed to prevent, reached by a different
   trigger. **The S1 fix is incomplete against its own shape.**

### Severity — stated honestly
- **Reachability: LOW.** Normal admin input routes verification failures through `Refuse` (handled).
  A genuine C# throw out of `ApplyAndVerifyImport` is uncommon: a corrupt download that passes the
  content-type + 500 KB checks makes Unity's importer produce a broken texture and `Refuse` on
  re-read, it does not throw. The realistic trigger is an environmental fault (folder removed
  mid-run by concurrent Editor work, `SaveAndReimport` internal error).
- **Consequence: MILD.** Because the CSV write is deferred to `FinalizeCsvs`, a verification-phase
  throw leaves NO CSV corruption and emits NO false "bundled" report — the residue is only stray
  untracked `.png/.meta` in `Resources/`, which the tool's mandatory git-diff review (SPEC §2) and
  the next run's collision-refusal both surface.

### Why it is nonetheless a FAIL at THIS gate (not a "note and pass")
1. **The report asserts exhaustiveness** ("All EIGHT side effects … checked", shape swept "the whole
   file"). That claim is falsified — a same-shape site is absent from the table. §22's gate is
   precisely "was the enumeration complete", and the brief is explicit: *a site they missed is a
   FAIL, and the highest-value thing this pass.*
2. **By the implementer's own bar it is a defect.** S1's residue is the identical class ("stray asset
   git will show"), and the implementer chose to FIX S1, not wave it through. Consistency demands the
   same treatment for the phase that performs S1's undo.
3. **Two gates were skipped this iter** (STATUS.md) — hold the bar higher, not lower.

### Fix (cheap; closes the shape properly)
- Guarantee the S1 orphan-cleanup runs under **all** failure modes: put a per-outcome `try/catch`
  around `ApplyAndVerifyImport` (575) that `Refuse`s the throwing outcome so its `WrittenPath` is
  cleaned by `FinalizeCsvs`, AND structure the post-batch phase so a throw cannot skip
  `FinalizeCsvs` (e.g. run the fetched-asset cleanup pass from a `finally`, or move the cleanup into
  the same `try/finally` that already brackets `StopAssetEditing`).
- Extend the `VerificationFaultForTest` seam (or add a sibling) to optionally **throw** rather than
  record-a-problem, and add a regression test: force a throw in the verification phase for a fetched
  outcome, run `Run()`, assert (a) no orphan `.png` remains on disk and (b) no CSV name was written.
  This is the same reasoning that created the seam in iter-4 — the throw path is unreachable today
  without editing source.
- Add the site to the §22 audit table with its verdict, so the next pass can check completeness
  rather than rediscover it.

---

## Independent side-effect re-derivation vs the report's 8-site table

`grep`'d the operation classes myself (`File.*`, `Directory.*`, `AssetDatabase.(Delete|Import|
Start/Stop/Refresh)`, importer mutations, `produced[]`, `*.Add`, static mutable fields) — not from
the report's table. The eight EFFECTS they list are the eight that exist and each A/B verdict on the
effects themselves holds. What the table omits is the **orchestration site** that performs the undo:

| Site | In report table? | My verdict |
|---|---|---|
| S1 `File.WriteAllBytes` (art) 921 | yes | fix present but INCOMPLETE (see blocker) |
| S2 CSV via `WriteTextAtomic` 789 | yes | atomic swap sound; `.tmp`-on-crash noted below |
| S3 `AssetDatabase.ImportAsset` 792 | yes | idempotent — agree |
| S4 `AssetDatabase.DeleteAsset` 472/779/806 | yes | best-effort + reported — agree (see note) |
| S5 importer + `SaveAndReimport` 1134 | yes | verified-after; but a THROW here is the blocker |
| S6 report append `WriteTextAtomic` 1282 | yes | after Finalize; catch just warns — agree |
| S7 `produced[key]` 933 | yes | no consumer after phase A — agree |
| S8 `report.*.Add` | yes | in-memory — agree |
| **Run() post-batch phase 575–580 (the undo orchestration)** | **NO** | **unguarded → bypasses S1 cleanup — BLOCKER** |

### Two lower-severity, already-acceptable notes (not blockers, listed for the fix pass)
- **Stale/leftover `.tmp`.** `WriteTextAtomic` (452) writes `path + ".tmp"` then `File.Replace`. A
  crash between the two leaves e.g. `Assets/Data/Characters.csv.tmp` inside `Assets/` (imports as a
  stray DefaultAsset). Strictly better than the old truncate-in-place, and caught by the git review;
  a `finally`-delete or stale-`.tmp` sweep would close it. Not blocking.
- **`DeleteAssetOrReport` failed delete** reverts the CSV name but leaves the asset on disk +
  reports an Error. Consistent state (no CSV names a missing sprite), best-effort, matches the tool's
  reviewable-diff model. Not blocking.

---

## Everything else I re-derived — held

- **Size metric (S§6 fix) — OBSERVED sound.** Re-derived independently: ASTC_6x6 at 170×343 =
  ⌈170/6⌉×⌈343/6⌉ = 29×58 = 1682 blocks × 16 B = **26,912 B**, exactly `TextureUtil.
  GetStorageMemorySizeLong`; old `Profiler.GetRuntimeMemorySizeLong` = 54,784 ≈ 2×. Fallback flag:
  `StorageBytes` sets `StorageFallbackUsed=true` (514) and `ToText` prints the OVER-REPORT warning
  (337–339) — both paths present. Reflection name/signature correct; live run observed 26,912.
- **S2/S6 atomicity — READ sound.** `.tmp`+`File.Replace`/`File.Move` (first-write) is the
  `TournamentArtService` idiom; CSV write failure is caught (794) and rolls back the art.
- **S4 return-checked — READ sound.** `DeleteAssetOrReport` reports a failed delete (472–476).
- **S1 restructure did not break the normal path — READ sound.** `pending.Lines` IS the local
  `lines` (same reference, 646/719), so splices are visible to `FinalizeCsvs`; `dirty`/early-return
  removed correctly — a zero-edit catalog has empty `pending.Edits`, `anySurvived=false`, `continue`
  (no write); an unreadable CSV returns before registering (627–631). Multi-catalog third-throws
  handled by the per-catalog catch (560) — but see blocker for the verification-phase throw.
- **Prior blockers GONE.** iter-1/iter-2 case-collision: `ExistingAsset` is `OrdinalIgnoreCase`
  (977), regression tests `Collision_IsDetectedRegardlessOfCase` + `…ReachableFromTheRealNamingRules`
  present. No `CESAR_REJECTION.md`.

## Acceptance re-verification (SPEC §7) — this pass
- **Full unfiltered EditMode sweep — OBSERVED, mine:** `1904 / 1901 passed / 0 failed / 3 skipped`
  (00:01:27), green on first call; the 3 skips are the pre-existing `HoleCompleteDriverTests`
  Stage-C1 skips. Matches the report.
- Items 1–12 — READ (implementer evidence + code re-trace); not re-OBSERVED end-to-end because a
  concrete structural blocker halts the pass. Item 11 (admin badge) READ per the brief's stated
  limit (no browser tooling). Item 4 (collision) re-derived GONE above.

## Cleanup
Read-only pass: `git`, `sed/grep`, one `tests-run` (non-mutating). No `.tmp`/fixtures in
`Resources`/`Data`; task CSVs + `content_art.txt` + `ContentArtFetcher.cs` + `ContentArtFetchTests.cs`
byte-clean vs the tracked tip. Concurrent `club_art_batches` art + `game_modes_admin`/`TellCode.md`
drift left untouched. Editor idle (not play mode).

---

**ARCHITECT_REVIEW_FAIL.** The §22 shape audit was not complete: the undo-orchestration
(`ApplyAndVerifyImport` loop + `FinalizeCsvs`, lines 575–580) runs outside any try/catch, so a thrown
exception in that phase bypasses the S1 orphan-cleanup and re-opens the exact assets-on-disk /
named-by-no-CSV state S1 was fixed to prevent — the same shape, a trigger the eight-site table
omitted, leaving the iteration's flagship fix incomplete against its own shape. Guard the phase so
cleanup always runs, add a forced-throw regression test via the `VerificationFaultForTest` seam, and
add the site to the audit table — then re-submit.
