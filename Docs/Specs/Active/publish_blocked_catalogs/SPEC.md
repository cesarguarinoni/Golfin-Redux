# SPEC — `publish_blocked_catalogs`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. `STATUS.md` tracks pipeline state. `ARCHITECT_BRIEF.md` (Code's diagnosis,
> 2026-09-01) is background — where this spec and the brief disagree, this spec wins.

## Status

See `STATUS.md`. Starts at `SPEC_READY` (2026-09-01).

## Goal

Two catalogs cannot be published from the admin because `validateCatalog` reports false
positives: `mission_loadouts` (17 errors — the validator does not speak the loadout mask
vocabulary the runtime translates) and `gacha_pools` (1 error — a rule fires on a row that is
already `is_active=false`). Fix both validator rules, and fix the one **real** gameplay bug the
diagnosis uncovered on the way: `OWN_NO_IRONS` (`ban:Iron7,Iron9`) only bans the two iron models
the design workbook knew about, so mission 24 *"No Irons Allowed / アイアン禁止"* lets 96 of the
114 irons in `Clubs.csv` (Iron 4/5/6/8) through. At the end both catalogs publish from the admin
with zero errors and mission 24 bans every iron.

## Decisions (Cesar, 2026-09-01)

| # | Decision |
|---|---|
| D1 | Validator **mirrors** the resolver's mapping (not a new `loadoutType` column, not dropping the rule). Both sides are hardened: iron number parsed from the anchored name pattern `Iron <N>`, never "any digit anywhere". One shared parity fixture is run by vitest AND an EditMode test. |
| D2 | `Iron` becomes a **family token** = any `ClubType.Iron` regardless of loft. `OWN_NO_IRONS` data changes to `ban:Iron` through the importer path. Supplied masks keep `Iron7` / `Iron9`. |
| D3 | `gacha_pools`: the ref-existence / ref-active / default-ball rules **skip deactivated rows**; the rarity/weight/quantity/dupeRp format rules keep firing on them (a deactivated row must still be sane if reactivated). Mirrors the shop carve-out. |
| D4 | Ban masks get validated: every `ban:` token must be a known token and must ban ≥ 1 active club. |

## Architecture context

- **Runtime (C#, Assembly-CSharp):** `Assets/Scripts/UI/MissionSelection/MissionLoadoutResolver.cs`
  — `ResolveSupplied` (first active club in CSV order matching token + rarity), `ResolveOwn`
  (drops bag clubs whose token is banned), `ClubTypeName(ClubDataRuntime)` (`:163`),
  `IronName(ClubDataRuntime)` (`:180`, the "contains 9 / contains 7" probe).
- **Club data:** `Assets/Scripts/UI/Inventory/ClubData.cs` — `enum ClubType { Driver, Wood, Iron,
  A_Wedge, P_Wedge, S_Wedge, Putter }`; `ClubDataRuntime` (plain class: `clubId`, `name`, `type`,
  `rarity : CharacterRarity`). `Assets/Resources/Data/Clubs.csv` — 799 rows, 114 irons, every
  iron NAME starts `Iron <N> ` (N ∈ 4..9: 36/30/18/12/12/6); CSV `type` strings are
  `Driver, Wood, Iron, A.Wedge, P.Wedge, S.Wedge, Putter`. No brand contains a digit today.
- **Loadout data:** `Assets/Resources/Data/mission_loadouts.csv` (13 rows, catalog registered in
  `Tools/content/catalogs.py:165`). Mask tokens in use: `Driver, Wood, Iron7, Iron9, AW, PW, SW,
  Putter`. Consumer of `OWN_NO_IRONS`: `Assets/Resources/Data/missions.csv:25` (mission 24).
  Player line `LOADOUT_OWN_NO_IRONS` = "Your bag — no irons / 自分のバッグ — アイアン禁止"
  (`Assets/Localization/LocalizationText.csv:631`) — already correct, no text change.
- **Validator (TS):** `Tools/admin-dashboard/lib/contentValidate.ts` — `mission_loadouts` block
  `:1174-1233` (`const clubs = ctx.otherCatalogs.get("clubs")` at `:1181`; own-mask shape check at `:1200`, supplied reachability loop at `:1217-1231`
  comparing the token to `text(club.data.type)`); `gacha_pools` block from `:1559` (rules 5 + 21
  in one `else-if` chain, then 6 and 7). Tests: `lib/__tests__/contentValidate.test.ts` (the shop
  precedent is `"leaves a DEACTIVATED ticket row alone"` at `:268`), `lib/__tests__/missionValidate.test.ts`.
- **Gacha runtime already skips inactive pool rows:** client `Assets/Scripts/UI/Gacha/GachaBannerModel.cs:262, :416`,
  `GachaRatesModalController.cs:165`; server `golfin_gacha_pull` §8 ("entries = active gacha_pools rows").
  Nothing runtime changes for gacha in this task.
- **Not touched, same vocabulary (follow-up, see §9):** `MissionGoalEvaluator.ClubMatches`
  (`Assets/Scripts/Gameplay/Missions/MissionGoalEvaluator.cs:345`) matches goal params like
  `Iron7` against shot labels with its own rules.

## Implementation

### §1 Token grammar (the one definition both languages implement)

Tokens are case-insensitive.

| Token | Matches a club when |
|---|---|
| `Driver`, `Wood`, `Putter` | `type` is that enum value |
| `AW` / `PW` / `SW` | `type` is `A_Wedge` / `P_Wedge` / `S_Wedge` (CSV `A.Wedge` / `P.Wedge` / `S.Wedge`) |
| `Iron` | `type == Iron`, any loft (**family token**, D2) |
| `IronN` (N = 1 digit) | `type == Iron` **and** `IronLoft(club) == N` |
| anything else | never matches; the validator reports it as unknown |

`IronLoft(club)`: first match of, in order — (1) name against `^\s*Iron\s+(\d)\b` (case-insensitive);
(2) id against `^club_iron(\d)`; (3) otherwise `null` (a loft-less iron matches only `Iron`).
This is the D1 hardening: `Iron 5 X7` is loft 5, not 7; `FAIRLOFT Iron` / `club_iron7_y` is loft 7
via the id fallback; `GOLFIN Iron` / `club_iron_z` is loft-less.
Today the results are identical to the current probe for all 114 irons (12 × `Iron7`, 6 × `Iron9`,
none flip), and remain identical for the ids `club_iron7_mireo` / `club_iron9_klyro`.

The *supplied* rule is unchanged: for each token, the **first** active club in CSV order that
matches the token **and** the loadout rarity; any token with no match ⇒ empty bag (runtime) /
error (validator). The *own* rule: a bag club is dropped when **any** ban token matches it.

### §2 C# — extract the grammar, keep the resolver's shape

New file `Assets/Scripts/UI/MissionSelection/LoadoutTokens.cs`, namespace
`GolfinRedux.UI.MissionSelection`, `public static class LoadoutTokens`:

```csharp
public static bool IsKnown(string token);                       // grammar table above
public static int? IronLoft(string clubId, string name);        // §1 parse, regex, anchored
public static bool Matches(ClubDataRuntime club, string token); // §1 table
```

`MissionLoadoutResolver`: `ResolveSupplied` and `ResolveOwn` call `LoadoutTokens.Matches(club, token)`
instead of comparing `ClubTypeName(club)` to the token. Delete `ClubTypeName` and `IronName`
(the ban `HashSet` becomes a `List<string>` of tokens tested with `Matches`). Warning strings and
the "first match in CSV order" comment stay. No behaviour change for any shipped supplied mask.

### §3 TS — the same grammar, next to the validator

New file `Tools/admin-dashboard/lib/loadoutTokens.ts` exporting the same three functions over
`{ id: string; name: string; type: string }` (the CSV `type` string — map `A.Wedge`→`AW` etc.
inside). `contentValidate.ts` `mission_loadouts` block:

- supplied loop (`:1217-1231`): replace `text(club.data.type).toLowerCase() === type.toLowerCase()`
  with `matches({ id: club.rowId, name: text(club.data.name), type: text(club.data.type) }, type)`
  (`DraftRow.rowId` is the `clubs` key `id`, `contentValidate.ts:28-33`); an unknown token gets its
  own error first: `Unknown club token "<t>". Known: Driver, Wood, Iron, Iron4–Iron9, AW, PW, SW, Putter.`
- own branch (`:1200`, D4): after the shape check, when the mask is `ban:…`, split the tokens and
  for each: unknown ⇒ the same unknown-token error; known but matching zero **active** clubs (any
  rarity) ⇒ `"ban:<t>" bans nothing — no active clubs row matches it.` Both are errors (a mission
  that promises a restriction and does not apply it is broken content). `*` still passes with no
  further checks. If `clubs` was not loaded, keep the existing behaviour (skip silently) — do not
  invent a new "catalog not loaded" error for this block.

Dashboard strings: none are player-facing and validator messages are English-only today (they are
not in `lib/i18n.ts` DICT) — keep that convention, do not add DICT keys.

### §4 `gacha_pools` — deactivated rows (D3)

In the `gacha_pools` block (`contentValidate.ts:1559`), wrap **only** the rule 5 + 21 `else-if`
chain (unknown kind → empty refId → catalog not loaded → ref missing → ref deactivated → default
ball) in `if (row.isActive) { … }`. Keep computing `referenced` before the guard so rule 6 (rarity
equals the ref's rarity) and rule 7 (weight/quantity/dupeRp/featured) still run on every row.
Add a comment citing the shop precedent (`"leaves a DEACTIVATED ticket row alone"`): a deactivated
row is invisible to client and server, and deactivation must stay a valid remedy, otherwise the
catalog is permanently unpublishable. No data change to `psc1_ball_golfin`.

### §5 Data — `OWN_NO_IRONS` (D2)

`Assets/Resources/Data/mission_loadouts.csv`: row `OWN_NO_IRONS`, column `clubs`:
`"ban:Iron7,Iron9"` → `ban:Iron`. `label` and every other column unchanged. Importer path, in
this order (the validator must be **deployed** before the publish step or the publish is refused):

1. `cd Tools/admin-dashboard && npm test` green → `npm run deploy` → quote the Cloudflare
   deployment id (PIPELINE_HARDENING §23).
2. `python3 Tools/content/import_content.py --env-file Tools/admin-dashboard/.env.development.local --catalogs mission_loadouts`
   (PLAN — read the verdicts; a CONFLICT means an admin draft is in flight: stop and report) →
   `--apply`.
3. Publish `mission_loadouts` **and** `gacha_pools` from the admin (the second needs no draft —
   it is the publish itself that was blocked). Quote both new published versions.
4. `python3 Tools/content/export_content.py --env-file Tools/admin-dashboard/.env.development.local --check` clean.

No new player-facing strings. `min_build` on the changed row untouched.

### §6 Tests

**Shared parity fixture** — `Tools/content/tests/loadout_tokens_fixture.csv`, columns
`clubId,name,type,token,expected`, read by BOTH tests below (the two implementations are allowed
to exist only because this file proves they agree). Minimum rows:

| clubId | name | type | token | expected |
|---|---|---|---|---|
| club_iron_mireo_common | Iron 7 MireO | Iron | Iron7 | true |
| club_iron_mireo_common | Iron 7 MireO | Iron | Iron | true |
| club_iron_mireo_common | Iron 7 MireO | Iron | Iron9 | false |
| club_iron9_klyro | Iron 9 Klyro | Iron | Iron9 | true |
| club_iron_x | Iron 5 X7 | Iron | Iron7 | false |
| club_iron_x | Iron 5 X7 | Iron | Iron5 | true |
| club_iron7_y | FAIRLOFT Iron | Iron | Iron7 | true |
| club_iron_z | GOLFIN Iron | Iron | Iron7 | false |
| club_iron_z | GOLFIN Iron | Iron | Iron | true |
| club_aw_gf | A.Wedge G&F | A.Wedge | AW | true |
| club_aw_gf | A.Wedge G&F | A.Wedge | A.Wedge | false |
| club_driver_gf | Driver G&F | Driver | driver | true |
| club_putter_gf | Putter G&F | Putter | Iron | false |

- **vitest** `Tools/admin-dashboard/lib/__tests__/loadoutTokens.test.ts`: every fixture row; plus
  a full-catalog regression: parse `Assets/Resources/Data/Clubs.csv` + `mission_loadouts.csv`
  (the repo copies, relative path from the test) into rows and assert `validateCatalog("mission_loadouts", …)`
  with `clubs` in `otherCatalogs` yields **0 errors**, and that `ban:Iron` against that catalog
  matches 114 clubs.
- **vitest** `contentValidate.test.ts`: `"reports an unknown club token"`, `"reports a ban that bans
  nothing"`, and `"leaves a DEACTIVATED pool row alone"` (a pool row whose ref is the default ball
  with `isActive:false` ⇒ no `refId` error, and the same row with `rarity: "Platinum"` ⇒ still a
  `rarity` error).
- **EditMode** `Assets/Tests/EditMode/LoadoutTokensTests.cs` (asmdef `GolfinRedux.Tests.EditMode`
  already references Assembly-CSharp): every fixture row through `LoadoutTokens.Matches` on a
  hand-built `ClubDataRuntime` (path `Path.Combine(Application.dataPath, "../Tools/content/tests/loadout_tokens_fixture.csv")`);
  plus, over `Clubs.csv` rows (NOTE: reuse whatever CSV parse `ClubDatabaseCSV.LoadCSV` (`:79`)
  delegates to, or the `Assets/Tests/EditMode` helper other data tests use — do not write a
  second CSV parser), assert each shipped supplied mask resolves every token at its rarity and
  `ban:Iron` drops exactly the 114 `ClubType.Iron` rows.

### §7 Runtime proof (Editor, no device pass — Cesar's standing rule)

Play mode → Missions → mission 24 with a bag containing an Iron 4 or Iron 5 (any non-7/9 iron):
the in-round club selector shows no iron. Before this task it does. Screenshot to `screenshots/`.
Also confirm one supplied mission (e.g. `SUP_FULL`) still hands out 7 clubs.

## Out of scope

- A `loadoutType` column on `clubs` (durable fix, rejected for now — D1).
- `MissionGoalEvaluator.ClubMatches` and `ClubSpec.Id` — they use the same words with their own
  matching; not changed here (§9).
- Any change to `psc1_ball_golfin`, gacha runtime, or the server.
- The cross-catalog "every name reference resolves against what the build bundles" CI check the
  brief proposes — its own task.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Every item `PASS`/`FAIL` with what was measured.

- [ ] `LoadoutTokens.cs` + `loadoutTokens.ts` exist; `ClubTypeName`/`IronName` are gone from the resolver; grep for `Contains("9")` in `MissionSelection/` returns nothing.
- [ ] Fixture CSV has ≥ the 13 rows above; vitest and EditMode both read **that file** (paths quoted) and pass.
- [ ] Full-catalog vitest: `mission_loadouts` validates with 0 errors against the repo `Clubs.csv`; `ban:Iron` matches 114.
- [ ] vitest: unknown token error, bans-nothing error, deactivated pool row carve-out (rarity still enforced) — all green; `npm test` total green.
- [ ] EditMode: every shipped supplied mask resolves; `ban:Iron` drops 114 irons; full EditMode run has no new failures (count quoted).
- [ ] `npm run deploy` done, Cloudflare deployment id quoted; live admin footer/`/api/version` shows the new commit (§23).
- [ ] `import_content.py` PLAN + `--apply` output quoted for `mission_loadouts` (1 changed row, no conflicts).
- [ ] Both `mission_loadouts` and `gacha_pools` **published from the live admin** — new version numbers quoted, validator shows 0 errors on each.
- [ ] `export_content.py --check` clean (output quoted).
- [ ] §7 Editor proof: screenshot of mission 24 with no iron selectable; a supplied mission still hands out its full bag.
- [ ] Zero new hardcoded `.text` literals; no new `LocalizationText.csv` rows (none needed).
- [ ] Unity Console has no errors related to this task.
- [ ] Deviations flagged at the bottom of the report with justification.

## Files this task touches

- `Assets/Scripts/UI/MissionSelection/LoadoutTokens.cs` — NEW, the grammar (§1, §2)
- `Assets/Scripts/UI/MissionSelection/MissionLoadoutResolver.cs` — use `LoadoutTokens.Matches`; delete the two private helpers
- `Assets/Tests/EditMode/LoadoutTokensTests.cs` — NEW (§6)
- `Tools/admin-dashboard/lib/loadoutTokens.ts` — NEW, the grammar in TS (§3)
- `Tools/admin-dashboard/lib/contentValidate.ts` — mission_loadouts block (§3), gacha_pools block (§4)
- `Tools/admin-dashboard/lib/__tests__/loadoutTokens.test.ts` — NEW; `contentValidate.test.ts` — 3 cases (§6)
- `Tools/content/tests/loadout_tokens_fixture.csv` — NEW shared fixture (§6)
- `Assets/Resources/Data/mission_loadouts.csv` — `OWN_NO_IRONS` → `ban:Iron` (§5)
- `Docs/AI_CONTEXT.md`, this folder's `STATUS.md` / `IMPLEMENTER_REPORT.md`

## §9 Follow-ups (not this task — Architect keeps the list)

1. `loadoutType` column on `clubs` if the name-anchored parse ever bites (it cannot today: no brand has a digit, every iron name starts `Iron <N>`).
2. `MissionGoalEvaluator.ClubMatches` — fourth spelling of the club vocabulary; fold into `LoadoutTokens` when goals next change.
3. Brief's cross-cutting proposal: a CI gate that every cross-catalog name reference resolves against what the build bundles (would have caught the ball-thumbnail, club-full-art and this defect).
