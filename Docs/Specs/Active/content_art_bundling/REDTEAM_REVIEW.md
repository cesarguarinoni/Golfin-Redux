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
