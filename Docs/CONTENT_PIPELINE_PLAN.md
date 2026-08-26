# Admin-Managed Content — implementation plan

**2026-08-24 · Architect.** Making clubs, items, characters, player inventory and game
text editable from `admin.golfin.world` without ever breaking an installed build.

Everything in §1 was read from the live repo (`GolfinRedux`, `playlife`) on 2026-08-24, not assumed.

---

## 1. What is true today

| Content | File | Size | Loader | Consumed by |
|---|---|---|---|---|
| Clubs (799) | `Assets/Resources/Data/Clubs.csv` | 275 KB | `ClubDatabaseCSV` (TextAsset → `ClubCsvParser`) | `ClubManager`, shop, gacha, bags |
| Characters (12) | `Assets/Data/Characters.csv` | 3.4 KB | `CharacterDatabaseCSV` | `CharacterManager` |
| Items (3) | `Assets/Data/Items.csv` | 1.2 KB | `ItemDatabaseCSV` | `ItemManager` |
| Bags (10) / Balls (2) | `Assets/Data/Bags.csv`, `Balls.csv` | <1 KB | `BagDatabaseCSV`, `BallDatabaseCSV` | `BagManager`, `BallManager` |
| Shop | `Assets/Resources/Data/shop_catalog.csv` | small | `ShopModel` / `GeneralShopModel` | Shop screens |
| Level costs | `Assets/Data/LevelUpCosts.csv` | 2 KB | level-up path | `CharacterManager` |
| Texts (500 keys) | `Assets/Localization/LocalizationText.csv` | 45 KB | editor importer → `LocalizationTextTable.asset` → `LocalizationManager` (static dict) | every screen |
| Player inventory | `Assets/Scripts/Save/SaveData.cs` → `LocalJsonPersister` | — | `SaveDataHost` (schema v10, debounced 250 ms) | everything |

**Already remote, and the pattern to copy.** `RemoteNoticeSource` / `RemoteBannerSource` /
`RemoteTournamentSource` all do the same four things, and they are the right four:
mirror the **raw** response body to `persistentDataPath` before mapping · read that cache
**synchronously at Awake** · fetch **off the boot critical path** · return **null on any
failure** so the caller keeps what it has. No new networking primitives are needed —
`Golfin.Net` (`ApiClient`, `ApiEnvelope`, `Endpoints`) already carries this.

**Already server-authoritative:** RP balance (`points_transactions`), leaderboard
accumulators, tournament entries + holes. Those must **not** be duplicated into any
inventory sync — see §6.

---

## 2. Architecture in one page

Six invariants. Everything below is an application of these; if a phase contradicts one,
the phase is wrong.

**I1 — The bundled CSV is the floor, never replaced.** A build always ships the full
catalog, exported from the admin at build time. Remote content is an **overlay applied on
top of the bundled rows by `id`**, never a substitute. Airplane mode on a fresh install
behaves exactly as it does today.

**I2 — The wire format is a delta, not a file.** Client sends the content version it was
built with; server returns only rows changed since. The common case is
`{"version": N, "changed": []}` — a few hundred bytes. A 275 KB club catalog is only ever
transferred in full to a client whose version the server cannot place.

**I3 — The admin is upstream of the CSV, not downstream.** Publishing in the admin writes
Supabase; a build-time export script rewrites the repo CSVs from Supabase. Without this the
delta grows forever and the bundled floor rots. This is a hard requirement of Phase 0, not
a nicety.

**I4 — Additive-only schema, and rows carry `min_build`.** The client parses by column
*name*, ignores unknown columns, defaults missing ones. Columns are never renamed, removed
or reordered. Any row whose `min_build` exceeds the running build is filtered out
**server-side**, so a January build never receives a club whose art it does not have. New
content therefore appears only where it can render; balance changes to existing rows apply
everywhere.

**I5 — Catalog changes take effect on NEXT LAUNCH. Only texts swap live.** Re-parsing the
club DB while a bag is equipped and a round is in flight is a re-entrancy problem with no
upside. Fetch → validate → write cache → apply at next `Awake`. Texts are a flat dictionary
with an existing `OnLanguageChanged` refresh, so they are the one safe live swap.

**I6 — Nothing is ever deleted, only deactivated.** A removed club is `is_active=false`:
it disappears from shop/gacha and stays fully renderable in the bag of every player who
owns one. The admin physically cannot delete a row a player holds.

Together I1+I4+I5+I6 are the answer to *"updates can never break installed games."*
§7 adds the rails that make it enforced rather than merely intended.

---

## 3. Phase 0 — content tables, publish flow, export script *(no client change)*

Ship this alone and nothing in the game changes; that is the point. It is the whole
foundation and it is independently verifiable.

**Schema** (`playlife/backend/migrations/2026_XX_content_catalog.sql`):

```
content_catalogs(name pk, published_version int, is_enabled bool)
content_rows(catalog, row_id, data jsonb, min_build int, is_active bool,
             version int, updated_at, pk(catalog,row_id))         -- index (catalog, version)
content_drafts(...same shape...)                                   -- staging, never served
content_versions(catalog, version, snapshot jsonb, published_by, published_at)
```

`data` is the CSV row as `{column: value}`. Keeping it JSONB instead of 799-column-typed
tables means a new club column is an admin change, not a migration — which is what I4
needs.

The delta in I2 is one indexed query: `where catalog=$1 and version > $since`. Deactivation
is an ordinary row update with `is_active=false`, so there is no tombstone table and I6
costs nothing.

**Seed.** Generate the initial `content_rows` INSERTs directly from the current CSVs
(`Tools/content/seed_from_csv.py` → SQL, pasted into the Supabase editor per the SQL rule
in `WORKFLOW_NOTES.md`). Day-one parity is then exact by construction, and the first export
in the other direction must produce byte-identical CSVs — that round-trip is Phase 0's
acceptance test.

**Draft → publish.** Admin edits land in `content_drafts`. **Publish** validates (§7), bumps
`published_version`, copies drafts into `content_rows` stamped with the new version, and
snapshots into `content_versions`. Keystrokes must never bump a version — one publish, one
version, one `admin_audit_log` entry with a before/after diff, exactly like the Tournaments
panel.

**Export.** `Tools/content/export_content.py` pulls the published catalogs and rewrites
`Assets/Data/*.csv`, `Assets/Resources/Data/Clubs.csv`, `Assets/Localization/LocalizationText.csv`,
plus a new `Assets/Resources/Data/content_version.txt`. The existing
`LocalizationTextImporter` editor hook already regenerates `LocalizationTextTable.asset`
from the CSV, so texts need no extra step. Run it before every release build; the version
file is what the client sends as `since`.

**Admin panels:** Clubs, Characters, Items (with Bags/Balls), Texts — registered in
`lib/registry.ts`, every route through `checkAdmin()`, every mutation through `lib/audit.ts`.
All new UI strings need EN **and** JA `DICT` entries (`ADMIN_DASHBOARD_OPS.md` §3.4).
The Clubs panel needs server-side pagination and filtering — 799 rows is not a `<table>`.

*Deliverable: an admin where content can be edited and published, and a script that puts
those edits into the next build. The game is untouched and cannot regress.*

---

## 4. Phase 1 — the delivery mechanism, proved on Texts

Smallest blast radius, live-swappable, and it exercises every part of the pipeline.

**Endpoint** — `GET /api/v1/content?since=<int>&build=<int>&catalogs=texts`
No auth, no trailing slash, same posture and same reason as `/banners` and `/notices`: it
warms at boot before any token work. Declare it above any `/content/{id}` route if one is
ever added — the `/tournaments/golfin` lesson.

```json
{"data": {"fetched_at": "...", "enabled": true, "version": 42,
          "catalogs": {"texts": {"version": 42,
                                 "changed": [{"id":"BTN_START","is_active":true,
                                              "data":{"English":"PLAY","Japanese":"プレイ"}}]}}}}
```

Server filters `min_build <= build` and `since` before serialising. `enabled:false` is the
kill switch (§7).

**Client** — new `Golfin.Content` asmdef, dependency-light, mirroring `Golfin.Notices`:

- `RemoteContentSource` — raw-body disk cache at `persistentDataPath/content_<catalog>.json`,
  atomic `.tmp` + `File.Replace`, null on any failure. Straight copy of `RemoteNoticeSource`.
- `ContentService` — `[DefaultExecutionOrder(-200)]`, before `SaveDataHost` (-100) and the
  DB loaders. Reads caches synchronously at `Awake`, exposes
  `TryGetOverlay(catalog, id, out row)`, then fetches off the critical path.
- `LocalizationManager.ApplyOverlay(IReadOnlyDictionary<string, LocalizedTextRow>)` — merges
  into `_textMap` after `Initialize`, fires `OnLanguageChanged`. Keys not in the overlay are
  untouched; unknown keys are added and harmlessly unused. ~15 lines, no call-site changes.

**Acceptance:** edit a string in the admin, publish, relaunch → new copy. Airplane mode →
bundled copy, no error. Corrupt the cache file by hand → bundled copy, one warning.
Set `enabled:false` → bundled copy everywhere within one launch.

---

## 5. Phase 2 — Characters, Items, Bags, Balls · Phase 3 — Clubs

Same mechanism, more consumers. Split because Phase 2's tables are tiny and its consumers
are simple, while Phase 3 is where the delta path and pagination actually earn their keep.

Per catalog: teach `<X>DatabaseCSV.LoadCSV()` to ask `ContentService` for an overlay row by
`id` after parsing the bundled row, and to append overlay rows whose `id` is new. Parsing
stays in the existing pure parsers (`ClubCsvParser` is already EditMode-testable — extend
its tests, do not fork it).

**Clamp on apply, always.** Lowering a club's `maxDurability` must clamp every owned
`PersistedClub.currentDurability`; lowering `maxLevel` must clamp `currentLevel`; a stat cap
change must re-clamp allocated SP against `RarityStatCaps`. Do this once in the overlay-apply
step, not at each read site. Un-clamped is the most likely way this feature corrupts a save.

**Tournaments are already safe** — `PersistedCharacterSnapshot` freezes stats at sign-up, so
a mid-event balance change cannot alter a running entry. Do not "fix" that.

**Art.** As written, `min_build` (I4) is the only thing standing between a new club row and
a grid of `Placeholder` sprites on an old build, which means a genuinely new club needs a
store release. **§10 removes that constraint** by giving rows an optional art URL served
through the existing `TournamentArtService` — read it before speccing Phase 3. `min_build`
survives either way as the escape hatch for rows that need new *code* (a new stat, a new
club type), and once a row is published its `min_build` is immutable: making it visible to
older builds is a new version, not an edit.

**Sizing.** Full clubs catalog ≈ 275 KB raw, ~40 KB gzipped (UnityWebRequest decompresses
transparently). Steady state is a `changed:[]` response of a few hundred bytes. Rule: if a
delta exceeds ~30 % of the full catalog, the server sends the full catalog instead.

---

## 6. Phase 4 — player inventory server-side

The expensive, risky one. It is last on purpose, and it is four steps, each shippable.

**What moves.** The blob is *SaveData minus what the server already owns minus device-local
preference*:

- **Moves:** `ownedClubs`, `ownedCharacters` (level, SP, allocation), `itemQuantities`,
  `ballQuantities`, `ticketBalances`, `unlockedHoles`, `starterCharacterId`,
  `selectedCharacterId`.
- **Stays server-owned, never duplicated:** RP balance, `lifetimeRpEarned` and the
  daily/weekly/monthly accumulators, `tournamentEntries` (already synced by
  `tournaments_server_side` — the local copy is a cache).
- **Stays device-local:** language, audio, UI state, `playedHoles`.

**Naming — do NOT reuse `user_inventory`.** That table already exists (created by
`20260409000000_dual_currency_gifts_badges_followers.sql`, written by `routers/gifts.py`) and is
the PARTNER APP's gift inventory. The game's inventory is a separate concern on a separate row;
conflating them would put gift items in a golf bag.

**Shape — one JSONB column, not a row per owned thing.** `profiles.golfin_inventory jsonb`
plus `golfin_inventory_rev int`. Row-per-owned-club is the expensive shape (300 k rows at
10 k players); a single blob is ~3 KB/user — ~30 MB at 10 k players, which is nothing.

**Store only deltas from the catalog default.** A club at level 1 with full durability is
just its id; write fields only where they differ. This is the "keep save data to a minimum"
requirement, and it also means catalog rebalances propagate to untouched instances for free.

**Write policy.** Write-behind on `SaveDataHost.OnSaved`, coalesced to at most one PUT per
30 s plus one on pause/quit. Never per mutation.

**Merge is additive and never subtracts.** On a `rev` mismatch: re-fetch, union owned ids,
take the max of levels/quantities, keep the higher durability. A bad sync can then lose a
purchase's *timing* but never a player's property. Subtraction only ever happens through an
explicit server-side spend, which already exists for RP.

Steps:

- **4a — push only.** Add the column, `GET/PUT /api/v1/user/golfin-inventory` (auth
  required, server stamps `user_id` from the bearer token, same posture as
  `/user/golfin-character`). Client pushes, never reads. This is a *backup* and carries
  essentially zero risk. Admin gets a read-only inventory tab in the Users drawer
  immediately — which already answers most support questions.
- **4b — read on restore only.** Pull the blob when there is no local save (fresh install /
  new device). Delivers cross-device restore, still cannot corrupt an existing save.
- **4c — read-merge every boot.** Full two-way sync with the additive merge above. This is
  the step that makes admin *writes* to inventory take effect, and the one that needs the
  most testing.
- **4d — server-authoritative spends** (later, separate): move club/character purchases to a
  server endpoint alongside `spend_pts`. Anti-cheat, not admin manageability. Out of scope
  here.

**Get grants early without 4c.** Admin-issued grants ("give this player 3 repair kits") do
*not* need full sync. A `golfin_pending_grants` table the client drains at boot and acks —
additive-only, idempotent by grant id, impossible to subtract — delivers the operational
value of admin inventory management after 4a, and is small enough to ship alongside Phase 2.
**Recommended: do the grants queue before 4c**, and let 4c be driven by whether cross-device
sync is actually wanted for the beta.

---

## 6.5 Phase 4 decisions of record (Architect, 2026-08-26)

Phase 4 shipped (`content_player_inventory`, prod v52). Three questions the Implementer raised, answered.

**1. A refundable spend IS acceptable through the beta — but MEASURE it, don't just accept it.**
✅ **INSTRUMENTED 2026-08-26** (`content_cleanup_quick` item 5): `inventory_merge_raise`, one row
per raised stack, at BOTH merge sites. A brand-new key is deliberately not counted — that is a
restore, not a refund.
The additive merge can restore a consumed item on a rev mismatch (device B with a stale rev pushes
`max(4,5)=5`). RP stays debited, so it is a free consumable, not RP duplication. For testers that is
the correct trade and the one the merge was chosen for. The non-obvious cost is not player harm but
**data harm**: beta consumption figures are what `ECONOMY_MASTER.md` §1 says will tune the economy,
and a silent refund path skews exactly those numbers. So: **log every merge that raises a quantity**,
with player and item. It turns an unknown into a count, costs almost nothing, and it is what tells us
before launch whether §6 step 4d (server-authoritative spends) is urgent or theoretical. If the count
is ~0 through the beta, 4d stays a launch-gate; if it is not, it moves up.

**2. Bag layout is PREFERENCE. The Implementer's call stands, and the reasoning is stronger than
"judgment".** Local-wins only ever decides the case where BOTH a local layout and a blob layout
exist — i.e. two actively-used devices — and there the device you are holding should keep the bag you
built on it. The case that actually matters for testers, restore-after-reinstall, has no local layout
to win, so the blob's slots arrive and are used. Preference costs nothing in the case we care about
and avoids silently equipping a club the player deliberately benched. Keep it.

**3. No grants panel — add a REVOKE action to the existing drawer.** ✅ **DONE 2026-08-26**
(`content_cleanup_quick` item 4). A separate panel is not
warranted at dozens of grants per tester. The real gap is narrower and worth closing: grants are
additive-only and there is no subtraction, so a fat-fingered grant is **permanent** once drained,
fixable only by SQL. Revoking an *unapplied* grant is the cheap half of that and closes most of it.
Cross-player visibility can wait.

---

## 7. Rails (build these in Phase 0, not after the first incident)

1. **Publish validation, blocking.** Required columns present; types parse; `rarity` in the
   six tiers; stats within `RarityStatCaps`; referential integrity (`shop_catalog.refId`,
   bag default club ids, `LevelUpCosts` covers `maxLevel`); no immutable-field edits
   (`id`, `min_build`); every text key present in **both** EN and JA.
2. **Diff preview before publish.** Row counts changed/added/deactivated and a field-level
   diff, shown and confirmed. This is the single highest-value guard against a fat-finger.
3. **Rollback = republish a snapshot.** `content_versions` makes this one click. It is the
   answer to "an update broke installed games", and it needs to be tested once, on purpose,
   before beta.
4. **Kill switches, two of them.** ⚠️ **AMENDED 2026-08-26 — the original wording of this item
   WAS the bug.** It described one switch doing two jobs: a per-catalog column driving a global
   client behaviour. `routers/content.py` implemented it faithfully (`enabled` ANDed across the
   requested catalogs) and `ContentService` implemented the client half faithfully (requests all
   catalogs, drops every cache on `enabled:false`). Each half matched this document; together they
   meant **disabling one catalog reverted all seven to bundled on every client**. Review never
   caught it because the code matched the spec — the spec was wrong. Corrected by
   `content_kill_switch_and_order`; found by flipping the switch against prod, which nobody had done.

   - **Per-catalog kill** — `content_catalogs.is_enabled=false` kills ONE catalog. It vanishes from
     `catalogs` and is named in the top-level `disabled` list; that catalog reverts to bundled and
     no other is touched. ⚠️ **A SERVED catalog carries no `enabled` field of its own** — it was
     dropped by `content_cleanup_quick` item 1, because a disabled catalog is absent and so the
     field could only ever be `true`. `disabled[]` is the whole of the per-catalog kill on the wire.
   - **Global kill** — `content_settings.content_enabled=false`. Top-level `enabled` goes false and
     clients ignore all remote content until it is flipped back.
   - **Top-level `enabled` is NEVER a function of which catalogs the client requested.** Derive it
     from the whole registry, so the same server state gives every client the same answer. The
     invariance of `disabled` across requested subsets is the property that regresses first.
   - `_global_enabled()` **fails OPEN** — a missing table, missing row or any exception reads as
     enabled. Failing closed would turn a transient PostgREST blip into a global cache wipe,
     recovered only on the launch after that.
   - One flag each, no deploy. ✅ **The global flag now has a dashboard control**
     (`content_cleanup_quick` item 2, 2026-08-26): `POST /api/content/enabled`, surfaced as a
     second card in every catalog's Kill switch tab, tagged `ALL CATALOGS` and confirmed on the way
     off. It is shown NEXT TO the per-catalog switch, never merged with it, each naming its own
     blast radius — the two being indistinguishable is the shape of the original bug.
   - **A kill is not instant:** 60 s response cache plus apply-at-next-launch (I5). Budget up to
     60 s to reach a client, landing at its next launch; re-enabling costs another launch.
5. **Deactivate, never delete** (I6), enforced in the API layer, not just the UI.
6. **Staging target.** `Endpoints.RootUrl` is already settable; point dev builds at a staging
   catalog so a publish is rehearsed before it reaches phones.
7. **No role tiers exist** (`ADMIN_DASHBOARD_OPS.md` §3.5) — every admin who can adjust RP
   will be able to rewrite the club roster. Attribution in `admin_audit_log` is the only
   control. Worth deciding before more than three people have access.

---

## 8. Sequencing

| Phase | Delivers | Risk | Depends on |
|---|---|---|---|
| 0 | Content tables, draft/publish, export script, Clubs+Characters+Items+Texts panels | none — game untouched | — |
| 1 | `/content` endpoint + `Golfin.Content` + live text overlay | low | 0 |
| 2 | Characters / Items / Bags / Balls overlay | low–medium (clamping) | 1 |
| 2.5 | `golfin_pending_grants` + admin grant action | low | 1, 4a |
| 3 | Clubs overlay (delta path, `min_build`, pagination) | medium | 1, 2 |
| 4a | Inventory push + read-only admin view | low | — (parallel with 1–3) |
| 4b | Restore on fresh install | medium | 4a |
| 4c | Two-way sync — admin inventory writes take effect | high | 4a, 4b |

0 → 1 is the critical path; 4a can run in parallel with all of it since it touches nothing
the client reads.

---

## 9. Decisions needed before Phase 0 is specced

1. ~~**Is `shop_catalog` in scope?**~~ **SETTLED 2026-08-24 — yes.** Cesar: RP offers spanning
   characters, balls, items and clubs, no IAP. Specced in §11; it moves to Phase 2.
2. **`LevelUpCosts` too?** 240 rows, drives the largest RP sink in the economy. Cheap to
   include, and it is the tuning knob most likely to be wanted mid-beta.
3. **Cross-device inventory sync for the beta — yes or no?** Determines whether 4c is on the
   critical path or a post-beta item. 4a + 2.5 covers admin manageability without it.
4. **Content publish cadence vs. build cadence.** If the export script does not run every
   release, the bundled floor drifts and the delta grows. Recommended: wire it into the
   Fastlane pipeline (`fastlane_testflight_pipeline`) so it cannot be forgotten.


---

## 10. Asset delivery — do we need Addressables?

Added 2026-08-24 at Cesar's question. **Addressables is not currently in the project** —
`Packages/manifest.json` has no `com.unity.addressables`. Everything loads through
`Resources.Load` plus additive scene loading.

Three different problems get conflated under "Addressables". They have three different
answers.

### 10.1 Row data (stats, prices, text) — no, and it would be worse

Addressables ships *assets*. Stats and prices are *rows*. Routing them through Addressables
would mean a catalog build per publish, a bundle download instead of a 200-byte delta, no
row-level merge against the bundled floor, and no way to express I6 (deactivate, not delete).
The JSON delta in §2 is the correct tool. Nothing in Phases 0–4 wants Addressables.

### 10.2 2D art for clubs / characters / items — no, because you already have the better path

`TournamentArtService` (`Assets/Scripts/TournamentsRuntime/`) already downloads allowlisted
art by URL, caches it on disk with a 50 MB LRU bound, enforces a 1 MB per-image download
ceiling, and — importantly — is **already parameterised**: `Instance` is built from a ctor
taking `(tag, cacheDirName, isAllowed)`. A second instance for club/character art is close
to free.

For individual UI sprites this beats Addressables on every axis that matters here: no
package, no catalog build coupled to the publish flow, no Addressables/Resources dual-path
(an asset referenced from both gets included in the build **twice** — a classic and
hard-to-spot regression), and it composes with the content pipeline directly: the row just
carries a URL.

**This upgrades §5.** Give club/character rows optional `portraitUrl` / `fullUrl` /
`controlUrl`. Resolution order becomes: remote URL (if present and cached) → bundled sprite
by name → `Placeholder`. A brand-new club published from the admin then renders on an
already-installed build with **no store release**, which is the thing `min_build` was
otherwise buying at the cost of the feature. Upload art in the admin alongside the row, same
as banners do today.

Sizing, measured 2026-08-24: `Assets/Resources/Clubs` is 122 MB of source PNG across 233
files (84 portraits / 71 full / 78 controls) — and that covers only about a third of the
distinct art the 799-row roster references. Actual dimensions are modest (Full 537×900,
Controls 1156×649), so ASTC in-build cost is roughly 50 MB today, trending toward ~150 MB at
full roster. Significant, growing with every new brand, and entirely avoidable by §10.2.
Get a real build report before acting on that estimate — it is computed from import settings,
not measured.

### 10.3 Hole / course 3D content — yes, eventually. The trigger is the SECOND course.

This is the real Addressables case, and it is nearer than "for the future" suggests.

`Assets/Resources` is **572 MB** of source assets, of which **388 MB is `HoleData`** — 18
holes at roughly 22 MB each of `green.json`, `zones.json` and `.bytes`. Everything under
`Resources/` ships in every build and has its catalog parsed at startup. It cannot be
stripped, cannot be demand-downloaded, and cannot be shipped per-course. The data is
*already* demand-loaded by path —
`Resources.Load<TextAsset>($"HoleData/{courseSlug}/{holeId}/zones")` in
`MapViewController`, `GreenTopology` — so the access pattern is right; it is only the
*packaging* that is wrong.

Add the geometry (`Assets/Golf` 3.4 GB, `Assets/Scenes` 2.1 GB of source) and the
conclusion is blunt: **a second golf course does not fit in a store build.** That is the
trigger, not a date. When course #2 is real, Addressables is the answer — remote catalog,
one group per course, `LoadSceneAsync` for hole geo — and it simultaneously delivers
"download the course you are about to play", which is a product feature rather than a
plumbing change.

Hand-rolling it instead is worse than it looks: `StreamingAssets` on Android lives inside
the APK jar and is only readable through `UnityWebRequest`, so the "just move the files"
version acquires a platform-specific async path anyway. That friction is most of what
Addressables exists to absorb.

**Do now (cheap), so that later is cheap:**

1. **Do not add the package yet.** Introducing it mid-beta, alongside the content pipeline,
   buys nothing and risks the dual-path duplication above.
2. **Stop adding new hole data under `Assets/Resources/`.** Every MB added there now is a MB
   to migrate later.
3. **Put one indirection in front of course data** — `ICourseDataProvider.LoadTextAsset(courseSlug, holeId, name)`
   with today's `Resources.Load` as the only implementation. Then the Addressables switch is
   one new implementation rather than a hunt through call sites. This is a small, safe
   refactor that can ride along with any Phase.
4. **Revisit when course #2 is greenlit**, and treat it as its own spec.

### 10.4 Summary

| Content | Mechanism | Package needed | Phase |
|---|---|---|---|
| Stats, prices, texts | JSON delta overlay (§2) | none | 0–3 |
| Club / character / item 2D art | URL + `TournamentArtService` clone | none | 3 (upgrades §5) |
| Player inventory | JSONB blob (§6) | none | 4 |
| Hole data + course geometry | **Addressables**, remote catalog, per-course groups | `com.unity.addressables` | deferred — gated on course #2 |


---

## 10.5 Existing server mirrors of client CSVs (found 2026-08-24)

Four prod tables are already hand-maintained mirrors of client CSVs, predating this plan:
`golfin_characters` (← `Characters.csv`, read by `tournaments_golfin.py` to enforce
`char_rarity_min/max` at entry), `golfin_bot_fields`, `golfin_bot_brackets`
(← the two bot CSVs) and `golfin_fake_players` (← `fake_players.csv`, read by
`routers/leaderboards.py`).

They matter here for two reasons.

**One of them is already stale, and it is a live behaviour bug.** `golfin_characters` was seeded
2026-08-18 and records `char_olivia` as `Uncommon`; `Characters.csv` says `Common` — the starter
rarity asymmetry was resolved 2026-08-21 (§3 above). The other 11 rows agree. On a
**rarity-restricted** tournament only, Olivia is therefore wrongly rejected from a Common-only
event and wrongly accepted into an Uncommon-minimum one. Fix:
`playlife/backend/migrations/2026_08_24_golfin_characters_rarity_fix.sql`.

**And a hand-maintained mirror is exactly what an admin panel turns from a rare chore into a
routine hazard.** Today a rarity change means someone remembers to edit two places. Once rarity
is a field in a dashboard form, nobody will. So Phase 0 makes publishing the `characters` catalog
upsert `golfin_characters` in the same request, failing the publish if the mirror write fails
(`content_catalog/SPEC.md` §A4). The other three are out of scope for now and recorded as a
follow-up — the long-term answer is that they become catalogs too and the mirrors disappear.

---

## 11. Shop — admin-controlled RP offers

Added 2026-08-24 at Cesar's instruction: **RP offers only. No IAP, no real money, no store SKUs.**
That is consistent with `ECONOMY_MASTER.md` §2.3 — nothing is sold for money until real cosmetic
content exists — so the shop panel can ship without touching the paid-track question at all.

### 11.1 What exists today

`Assets/Resources/Data/shop_catalog.csv`, 5 rows, read by `ShopModel` / `GeneralShopModel`:

```
entryId,category,refId,rpCost,saleRpCost,sortOrder,popular,offer,rarity
shop_club_iron9_klyro,club,club_iron9_klyro,200,150,10,true,true,
```

The schema is already generic — `category` + `refId` points at any catalog — but only `club`
rows exist. Characters, balls and items are not sellable today, and the RP ladders proposed in
`ECONOMY_MASTER.md` §3 (characters 200–6,000; clubs 100–3,000) have nowhere to live.

### 11.2 Schema additions

`shop_catalog` becomes a first-class content catalog on the §3 machinery. Existing columns keep
their names and meaning (I4 is additive-only); new columns:

| Column | Purpose |
|---|---|
| `category` | widen the accepted set to `club \| character \| ball \| item \| bag` |
| `startAt` / `endAt` | scheduled visibility, UTC, `endAt` exclusive — same semantics as `home_notices`. ⚠️ **Specced here but NOT built in Phase 0** (which seeded only the existing CSV shape), so the Shop panel's LIVE/SCHEDULED/ENDED badge had to fall back to `is_active`. Added by `content_panels_gaps` §3 — an Architect gap, caught by the panels Implementer. |
| `saleStartAt` / `saleEndAt` | the SALE window, independent of listing window; outside it `saleRpCost` is ignored |
| `stockLimit` | per-player purchase cap; empty = unlimited. Clubs are unique so 1 is implied |
| `minPlayerLevel` | gate, empty = none |
| `sectionKey` | which shop tab/shelf the row lands on — a localisation key, not literal copy |
| `min_build` | as everywhere else (I4) |

`rpCost` stays the only price field. There is deliberately no currency column: adding one is how
"no IAP yet" quietly becomes IAP.

### 11.3 Admin panel

One **Shop** panel, registered in `lib/registry.ts` beside the others. Per row: category picker →
`refId` **typeahead against the live catalog for that category**, which is what makes a broken
reference impossible rather than merely validated; RP price; sale price + window; listing window;
sort order; `popular` / `offer` flags; stock limit; level gate.

Two things the panel must show that a raw table cannot:

- **A resolved preview** — name, rarity, art thumbnail of the referenced entity, so the operator
  sees the club rather than `club_iron9_klyro`.
- **Live/Scheduled/Ended state** derived from the windows, rendered as an untranslated badge
  exactly like the Tournaments panel (`ADMIN_DASHBOARD_OPS.md` §3.4).

### 11.4 Validation (blocking, on publish — extends §7.1)

1. `refId` resolves in the catalog named by `category`, **and that row is `is_active`**. Listing a
   deactivated club is the most likely way a shop edit produces a broken card.
2. `saleRpCost < rpCost` when present; both ≥ 0.
3. `saleStartAt/saleEndAt` inside `startAt/endAt`; every window well-ordered.
4. `rpCost` inside the rarity band from `ECONOMY_MASTER.md` §3 → **warn, do not block.** Prices are
   the thing most likely to be deliberately tuned; a hard block here would be inventing a rule
   Cesar did not set.
5. `entryId` unique; `sortOrder` unique within a `sectionKey`.
6. `min_build` ≥ the build that shipped the referenced row's art.

### 11.5 Purchase path — unchanged for now, and that is the risk to name

Purchases still debit RP client-side through `PointsSpendGate` and grant locally. Moving the
listing to the server does **not** make prices authoritative: a modified client can still grant
itself a club at any price. Server-authoritative purchase is §6 step 4d and stays out of scope
here — but the moment the shop is admin-driven it becomes easy to assume prices are enforced.
They are not. Write it on the panel.

### 11.6 Sequencing

Phase 2, alongside Characters/Items/Balls — the shop is only as useful as the catalogs it can
point at, and it needs the character and ball catalogs live to sell them. Ship the panel and the
schema together; the client change is `ShopModel` / `GeneralShopModel` reading windows and the
widened `category`, which is small.

---

## 12. PNG / texture report (2026-08-24) — findings and what was done

Measured across `Assets/`: **2,455 PNGs, 3.63 GB of source**. Conclusions first, because most of
the intuitive answers here are wrong.

### 12.1 Lossless PNG recompression is not worth doing

**Measured headroom: 1.3 %** (zlib-9 re-deflate + strippable metadata chunks, sampled over 14
`Resources` PNGs; metadata is 3 KB across the sample). A stronger optimiser (oxipng filter search
/ zopfli) would reach perhaps 5–12 %, i.e. ~10–20 MB of repo.

More importantly: **it would change the build by exactly zero bytes.** Unity re-encodes every
texture to ASTC at build time (`ProjectSettings` sets format `3` = ASTC for both Android and iOS);
the PNG is a source format that never ships. Against that, recompression churns 376+ files, forces
a full texture reimport, and produces a diff nobody can review. **Recommendation: don't.**

### 12.2 Import settings are already clean — nothing to fix

The usual build-size waste is absent, verified across all 2,508 `.png.meta`:

| Check | Result |
|---|---|
| Sprites with mipmaps enabled (+33 % size and memory, no benefit for UI) | **0 of 925** |
| Read/Write enabled (doubles runtime memory) | **4** — 3 in a vendor pack, 1 in `Art/Original UI`; none ship |
| Crunch compression (lossy) | 0 |
| Compression | Automatic / Normal quality → ASTC 6×6, correct for mobile |

Also worth knowing, since it kills an obvious-sounding idea: because the target is **ASTC**, an
opaque alpha channel costs nothing — ASTC blocks are 16 bytes regardless of alpha. Stripping alpha
from RGBA textures would be a win on ETC2 and is a no-op here.

### 12.3 What actually ships, and where it goes

`Assets/Resources` — the guaranteed-shipped set — is 376 PNGs, 184 MB of source,
**~78 MB in-build** after `maxTextureSize` clamping and ASTC 6×6:

| Folder | Files | In-build |
|---|---|---|
| `Clubs/Controls` | 78 | 26.3 MB |
| `Clubs/Full` | 71 | 19.8 MB → **15.3 MB** after §12.4 |
| `Characters/Homescreen` | 13 | 11.2 MB |
| `Balls/Thumbnails` | 20 | 8.1 MB |
| `Portraits/FullBody` | 12 | 4.3 MB |
| `Clubs/Portraits` | 84 | 3.9 MB |
| everything else | 98 | 4.2 MB |

`Characters/Homescreen` at 1090×1907 is genuinely 1:1 for a modern phone screen — not waste, but
it is 0.93 MB per character and grows with the roster. Club art is 63 % of the shipped total and
covers only about a third of the 799-row roster's distinct sprites. Both are arguments for §10.2
(art by URL), not for compression.

### 12.4 The one real optimisation — DONE

**Five of the 71 `Resources/Clubs/Full` sprites shipped at 2148×3600 — exactly 4× the 537×900 the
art pipeline spec mandates** (`club_art_batches/SPEC.md` → "Sprite targets"). The other 66 conform.
They were raw generation output committed without the post-process step:
`Putter-GolfinX`, `WedgeA-Fyloe`, `WedgeP-RoyalSwing`, `Iron9-Klyro`, `Placeholder`.

This is spec conformance, not a quality trade. Resampled Lanczos to 537×900 and re-applied the
30 px rounded-corner alpha (intersected with existing alpha, never added). **Verification: against
the original downscaled to the size the UI actually draws, mean absolute pixel difference is
0.0 and maximum is 0 — bit-identical at display size.**

- Source: **37.8 MB → 3.4 MB** (repo −34.4 MB); `Clubs/Full` 86.1 MB → 51.7 MB.
- In-build: **5.6 MB → 1.1 MB** (−4.5 MB, ASTC 6×6).
- `.png.meta` files were left in place, so **GUIDs and every reference are preserved**.
- The high-resolution originals are **not deleted** — two of them are W1 generation templates.
  They moved to `Assets/Art/Clubs/Full_Masters~/`; the trailing `~` makes Unity ignore the folder,
  so they stay available to the art pipeline without shipping. `club_art_batches/SPEC.md` §Template
  registry was updated to point there.

### 12.5 Not done, and why

- **131 duplicate PNG groups, 68.8 MB**, across `Resources` + `Art` + `Courses/Maps`. Inside
  `Resources` alone it is only **4 groups / 0.5 MB** (the `Sprites/Shops` hero/storefront pairs),
  so almost none of it ships. The rest is `Art/` masters mirrored into `Resources/` — which is the
  intended arrangement — plus genuinely redundant flat normal maps in `Art/3D/Balls` (one identical
  file stored 5×). Worth a cleanup pass, but it is repo hygiene and needs a call on which copy is
  canonical. Not touched.
- **Vendor packs.** `Assets/Packs` (3.6 GB) and `Assets/Realistic Tree` are third-party. Optimising
  them creates an unreviewable diff and breaks any future package update, for zero build benefit.
  Leave them alone.
- **Repo weight is not a PNG problem.** `Assets` is 12 GB: `Packs` 3.6 GB, `Golf` 3.4 GB, `Scenes`
  2.1 GB, `Courses` 601 MB, `Screenshots` 305 MB, `_Recovery` 92 MB, `References` 75 MB. Several of
  those neither ship nor need to be in git. That is the lever worth pulling on repo size — a
  separate task, and a bigger one than every PNG in the project combined.
