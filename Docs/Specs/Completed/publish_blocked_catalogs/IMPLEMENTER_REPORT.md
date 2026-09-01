# IMPLEMENTER_REPORT — `publish_blocked_catalogs`

Implemented directly by Claude Code (main thread), 2026-09-01, on Cesar's instruction — not
through the subagent chain. Baseline: `f84d2dd3e` (session start `3f43e9800`; the only commit
between them, `f84d2dd3e ball_data_wiring + ball_art_and_stats: DONE`, is docs-only and does not
touch anything this task edits). Implementation commit `bbf9996e3`.

## Files modified or created

| File | What |
|---|---|
| `Assets/Scripts/UI/MissionSelection/LoadoutTokens.cs` | **NEW** — the mask grammar in C#: `IsKnown`, `IronLoft`, `Matches(ClubDataRuntime, token)`, plus a string-only `Matches(clubId, name, type, token)` parity overload and `FamilyOf`. |
| `Assets/Scripts/UI/MissionSelection/MissionLoadoutResolver.cs` | Supplied + own now ask `LoadoutTokens.Matches`; `ClubTypeName` and `IronName` deleted; the ban `HashSet<string>` is a `List<string>` tested with `IsBanned`. |
| `Assets/Scripts/UI/Inventory/Tests/LoadoutTokensTests.cs` | **NEW** — EditMode fixture: every shared-fixture row, the anchored-loft cases, `IsKnown`, every shipped supplied mask over the real roster, `ban:Iron` = 114, `ban:Iron7,Iron9` = 18, and the `OWN_NO_IRONS` data row. |
| `Assets/Scripts/UI/Inventory/Tests/Golfin.Inventory.Tests.asmdef` | + `Golfin.Gameplay.Missions` reference, so the test reuses `MissionCsv.Parse` instead of writing a second CSV parser. |
| `Tools/admin-dashboard/lib/loadoutTokens.ts` | **NEW** — the same grammar in TypeScript: `isKnown`, `ironLoft`, `matches`, `familyOf`, `KNOWN_TOKENS_HINT`. |
| `Tools/admin-dashboard/lib/contentValidate.ts` | `mission_loadouts`: supplied resolves through the grammar; `ban:` masks validated (unknown token / bans-nothing). `gacha_pools`: the rule-5 + rule-21 chain is wrapped in `if (row.isActive)`; rules 6–7 stay outside it. |
| `Tools/admin-dashboard/lib/__tests__/loadoutTokens.test.ts` | **NEW** — every fixture row, `ironLoft`/`isKnown` units, and the full-catalog regression against the repo `Clubs.csv` / `mission_loadouts.csv`. |
| `Tools/admin-dashboard/lib/__tests__/contentValidate.test.ts` | +9 cases: mission_loadouts club tokens (6) and the gacha_pools deactivated carve-out (3). |
| `Tools/content/tests/loadout_tokens_fixture.csv` | **NEW** — the shared parity fixture, 21 data rows (spec floor: 13). |
| `Assets/Resources/Data/mission_loadouts.csv` | `OWN_NO_IRONS.clubs`: `"ban:Iron7,Iron9"` → `ban:Iron`. |
| `Assets/Resources/Data/content_version.txt` | `mission_loadouts=1` → `2`, written by `export_content.py` after the publish. |
| `Docs/Specs/Active/publish_blocked_catalogs/{STATUS,IMPLEMENTER_REPORT}.md`, `screenshots/` | This report + the play-mode artifact. |
| `Docs/AI_CONTEXT.md` | Session log. |

Nothing else in the working tree was touched. `git status --porcelain --untracked-files=all` at
commit time carried three paths that are NOT this task's and were deliberately left alone:
`Docs/TellCode.md` (M), `Docs/Specs/Active/gps_profile_pack/*` (??), `.claude/launch.json` (??).

## Acceptance checklist

**1. `LoadoutTokens.cs` + `loadoutTokens.ts` exist; `ClubTypeName`/`IronName` gone; no `Contains("9")` in `MissionSelection/` — PASS.**

```
$ grep -rn 'ClubTypeName\|IronName\|Contains("9")\|Contains("7")' Assets/Scripts/UI/MissionSelection/
(no matches)
```
Both new files are on disk and both compile: an MCP reflection probe reports
`LoadoutTokens=Assembly-CSharp; TestsFixture=Golfin.Inventory.Tests`.

**2. Fixture CSV ≥ 13 rows; vitest and EditMode both read THAT file — PASS.**

`Tools/content/tests/loadout_tokens_fixture.csv`, 21 data rows.
- vitest path: `join(__dirname, "../../../..", "Tools/content/tests/loadout_tokens_fixture.csv")`
  (`lib/__tests__/loadoutTokens.test.ts`), asserted non-empty and `>= 13` so a silent load
  failure cannot make the `it.each` vacuous.
- EditMode path: `Path.Combine(RepoRoot(), "Tools/content/tests/loadout_tokens_fixture.csv")`,
  where `RepoRoot()` is derived from `ClubRosterProd.FindShippedCsv()`; also asserted `>= 13`.

**TRIPWIRE (both suites really read the same file).** Flipping ONE row
(`club_iron_z,GOLFIN Iron,Iron,Iron,true` → `false`) turned both red, then reverting turned both
green again:

```
vitest   × the shared parity fixture > club_iron_z (GOLFIN Iron / Iron) vs token Iron → false
         Tests  1 failed | 30 passed (31)

EditMode Golfin.Inventory.Tests.LoadoutTokensTests.EveryFixtureRowMatchesWhatTheFixtureSays
         Failed — "C# and the fixture disagree — so C# and the TypeScript validator disagree:
                   club_iron_z (GOLFIN Iron / Iron) vs "Iron": expected False, got True"
         TotalTests 2212, Passed 2208, Failed 1
```
This matters because `tests-run` ignores class/assembly filters and reports only failures
(memory `reference_tests_run_ignores_class_filters`) — the tripwire is the only way to show the
new EditMode fixture actually executed.

**3. Full-catalog vitest: `mission_loadouts` 0 errors against the repo `Clubs.csv`; `ban:Iron` matches 114 — PASS.**

`loadoutTokens.test.ts` loads the real files (799 club rows, 13 loadout rows — both asserted) and
gets `[]` errors; `ban:Iron` → 114, `Iron7` → 12, `Iron9` → 6.

**4. vitest: unknown token, bans-nothing, deactivated pool carve-out — PASS; `npm test` green.**

```
$ npm test
 ✓ lib/__tests__/mirrorRowMapping.test.ts (12 tests)
 ✓ lib/__tests__/telemetryGacha.test.ts (13 tests)
 ✓ lib/__tests__/gachaAudit.test.ts (20 tests)
 ✓ lib/__tests__/missionValidate.test.ts (33 tests)
 ✓ lib/__tests__/contentValidate.test.ts (46 tests)
 ✓ lib/__tests__/gachaValidate.test.ts (47 tests)
 ✓ lib/__tests__/rewardsValidation.test.ts (10 tests)
 ✓ lib/__tests__/gachaOdds.test.ts (18 tests)
 ✓ lib/__tests__/banner.test.ts (8 tests)
 ✓ lib/__tests__/missionScore.test.ts (48 tests)
 ✓ lib/__tests__/loadoutTokens.test.ts (31 tests)

 Test Files  11 passed (11)
      Tests  286 passed (286)
```
(`contentValidate.test.ts` was 37 tests before this task, `loadoutTokens.test.ts` is new.)
The deactivated-pool block has three cases, not one: the ACTIVE control still fires the DEFAULT
ball error, the deactivated row raises no `refId` error, and the deactivated row STILL fails on
`rarity: "Platinum"` and on `weight: 0` — the "rules 6 and 7 stay outside the guard" half of D3.

**5. EditMode: every shipped supplied mask resolves; `ban:Iron` drops 114; no new failures — PASS.**

```
tests-run EditMode (whole mode — filters do not work on this MCP tool)
  TotalTests 2212, Passed 2209, Failed 0, Skipped 3, Duration 00:01:28.95
```
The 3 skips are pre-existing (`Golfin.Physics.Tests.HoleCompleteDriverTests`, "Stage C1:
HandleShotComplete is now a no-op"). The run immediately before this task's EditMode file existed
is not separately recorded, but the tripwire above proves the six new tests are in the 2212 and
that they fail when they should.

**6. `npm run deploy`, Cloudflare id quoted, live footer shows the new commit — PASS.**

```
→  stamping build as bbf9996e3          (clean — no "-DIRTY" warning line)
✓  bundle carries no service_role key
Uploaded golfin-admin (7.93 sec)
Deployed golfin-admin triggers (1.98 sec)
  admin.golfin.world (custom domain)
Current Version ID: 33d07d75-705c-404d-9ad3-624cf10e8ed9
```
Shell check (§23): `grep bbf9996e3 .open-next/server-functions/default/.next/server/app/api/version/route.js` → `bbf9996e3`.
Live check (§23, the browser route — `/api/version` is behind Access): the sidebar footer at
`https://admin.golfin.world/gacha-pools` reads **`bbf9996e3`**, read in Cesar's Chrome.

**7. `import_content.py` PLAN + `--apply` — PASS, 1 changed row, 0 conflicts.**

```
$ python3 Tools/content/import_content.py --env-file Tools/admin-dashboard/.env.development.local --catalogs mission_loadouts
catalog         add  change   same  conflict  csv
  mission_loadouts    0       1     12         0  Assets/Resources/Data/mission_loadouts.csv
PLAN ONLY — 1 draft(s) would be written (0 new, at min_build 2562). Nothing was written.

$ ... --apply
catalog         add  change   same  conflict  csv
  mission_loadouts    0       1     12         0  Assets/Resources/Data/mission_loadouts.csv
Wrote 1 draft(s) as cesar.guarinoni@gmail.com.
```

**8. Both catalogs published from the live admin — PASS for `mission_loadouts`; `gacha_pools` had nothing to publish (deviation D2).**

- `mission_loadouts`: the publish drawer showed `0 added / 1 changed / 0 deactivated`, the diff
  being `OWN_NO_IRONS.clubs  Published ban:Iron7,Iron9 → Draft ban:Iron`, zero validator errors.
  Published with the note *"publish_blocked_catalogs: OWN_NO_IRONS ban:Iron7,Iron9 -> ban:Iron
  (mission 24 was letting Iron 4/5/6/8 play)"* →
  **`Published mission_loadouts v2 — 0 added, 1 changed, 0 deactivated.`** Panel now reads
  `mission_loadouts · Published v2 · No unpublished changes`.
- `gacha_pools`: already **`Published v2`**, `No unpublished changes`, drawer says *"Drafts match
  what is published. There is nothing to publish."*, zero validator errors. See D2 — the block the
  spec describes is real but it applies to the NEXT publish, and it is proven fixed below rather
  than by a version bump I would have had to manufacture.

**9. `export_content.py --check` clean — PASS.**

The first run was correctly stale on one file (the publish had just bumped the version):
```
--check: 1 file(s) would change:
  Assets/Resources/Data/content_version.txt
--check: FAILED — 1 stale file(s).
```
After running the exporter (`mission_loadouts=1` → `mission_loadouts=2`, the only line that
changed; all 20 catalogs `unchanged`, `mission_loadouts v2  13 rows  unchanged`):
```
--check: clean — no file would change and no catalog has drifted.
```

**10. §7 Editor proof — PARTIAL. The resolver half is proven end-to-end in play mode against the real bag; the in-round club-selector screenshot for mission 24 was NOT taken, because mission 24 is not reachable through the real UI on this save.**

Play mode, ShellScene, real entry path: Home → `DailyMissionPill.onClick` → MissionSelectionScreen.
`Tab_PRO` is `interactable=False` ("PRO 0/10"; BEGINNER 1/10) — mission 24's `unlock` is
`clear:23`, so its card cannot be tapped and no round can be started for it. Rather than fake
progression or call `MissionLauncher.TryStart(m, isPlayable:true)` behind the widget (which
PIPELINE_HARDENING rule 2 bans as evidence), I drove the production path that FEEDS the selector.

`MissionSessionBag` is what `BagManager.GetClubsInBag` returns to the in-round club selector, and
what `MissionSession` pushes into it is `MissionDefinition.ClubIds` — composed by `MissionCatalog`
calling the installed production resolver. Read live, in play mode:

```
PRODUCTION delegate = GolfinRedux.UI.MissionSelection.MissionLoadoutResolver.Resolve

EQUIPPED BAG (real BagManager, real save):
    club_driver_golfin_common  (Driver GOLFIN  / Driver)
    club_wood_golfin_common    (Wood GOLFIN    / Wood)
    club_iron_golfin_common    (Iron 4 GOLFIN  / Iron)      <-- the Iron 4 the spec asks for
    club_pwedge_golfin_common  (P.Wedge GOLFIN / P_Wedge)
    club_awedge_golfin_common  (A.Wedge GOLFIN / A_Wedge)
    club_swedge_golfin_common  (S.Wedge GOLFIN / S_Wedge)
    club_putter_golfin_common  (Putter GOLFIN  / Putter)

mission 24 loadout=OWN_NO_IRONS supplied=False
  ClubIds -> club_driver_golfin_common(Driver), club_wood_golfin_common(Wood),
             club_pwedge_golfin_common(P_Wedge), club_awedge_golfin_common(A_Wedge),
             club_swedge_golfin_common(S_Wedge), club_putter_golfin_common(Putter)
```

Six clubs, no iron. And the A/B, same live bag, same production delegate, only the mask differing:

```
AFTER  (shipped now): OWN_NO_IRONS [ban:Iron]         -> 6 clubs, no Iron
BEFORE (old mask)   : OWN_NO_IRONS [ban:Iron7,Iron9]  -> 7 clubs, club_iron_golfin_common(Iron) SURVIVES
```

That is the bug and its fix, on the real save, through the real resolver.

Supplied side (§7's second half), same run:
```
SUP_FULL  [Driver,Wood,Iron7,Iron9,AW,PW,Putter] -> 7 clubs
          club_driver_gf, club_wood_gf, club_iron_mireo_common, club_iron_klyro_common,
          club_awedge_gf_common, club_pwedge_gf_common, club_putter_gf_common
SUP_IRONS [Iron7,Iron9,Putter]                   -> 3 clubs
```

Screenshot (real entry path, 1170×2532, play mode, `Application.runInBackground = true`,
captured via `GOLFIN/Screenshot/Capture Game View`):
`screenshots/missions_screen_pro_tier_locked.png` — the Missions screen showing
`BEGINNER 1/10 · 🔒 AMATEUR 0/10 · 🔒 PRO 0/10 · 🔒 LEGEND 0/10`, i.e. the gate that makes the
mission-24 in-round shot impossible on this save.

**11. Zero new hardcoded `.text` literals; no new `LocalizationText.csv` rows — PASS.**

`texts` is `v26  797 rows  unchanged` in `export_content.py --check`. `LOADOUT_OWN_NO_IRONS`
("Your bag — no irons / 自分のバッグ — アイアン禁止") was already correct and is untouched. The new
validator messages are English-only, matching the existing `contentValidate.ts` convention — no
`lib/i18n.ts` `DICT` keys added (§3). No `.text =` assignment exists in any file this task touched.

**12. Unity Console has no errors related to this task — PASS.**

`console-get-logs` after the post-edit `assets-refresh` returned zero `Error` entries; every entry
is a pre-existing `CS0618`/`CS8632` warning in files this task does not touch (recorder scripts,
inventory editor scripts, `TelemetryService`).

**13. Deviations flagged — see below.**

## Extra evidence not required by the spec: the two error counts, on live production data

The spec's "17" and "1" are the reason the task exists, so I checked them rather than trusting
them. The 11 live `gacha_pools` draft rows and 13 live `mission_loadouts` draft rows were pulled
from production (`content_rows`, via `Tools/content/rest.py`) and run through BOTH the pre-change
`contentValidate.ts` (`git show HEAD~1:…`) and the new one:

```
gacha_pools BEFORE: [
  "psc1_ball_golfin/refId: \"ball_golfin\" is the DEFAULT ball — every player already owns one,
   so a slot that pays it pays nothing. Point this entry at another ball, or clear isDefault on that row."
]
gacha_pools AFTER : []

mission_loadouts BEFORE count: 17
  e.g. "SUP_FULL/clubs: No active clubs row is type \"Iron7\" at rarity \"Common\" …"
mission_loadouts AFTER : []
```

Exactly 17 and exactly 1, and both go to zero. (Scratch test file, run and deleted; not committed.)

## Deviations

**D1 — the EditMode test lives at `Assets/Scripts/UI/Inventory/Tests/LoadoutTokensTests.cs`, not `Assets/Tests/EditMode/`.**
SPEC §6 names the latter and says its asmdef "already references Assembly-CSharp". It cannot: an
asmdef assembly can never reference a predefined assembly, and `GolfinRedux.Tests.EditMode`'s
references are `Golfin.Net, Golfin.Content, Golfin.Gameplay.Missions, Golfin.Save,
Golfin.InventorySync, Golfin.Economy, Golfin.Localization`. The same §6 also says to reuse the CSV
parse the shipping loader delegates to rather than write a second one — that is `ClubCsvParser`,
reached through the `internal` helper `ClubRosterProd`, which lives in `Golfin.Inventory.Tests`.
`internal` does not cross assemblies, so honouring the file path would have meant duplicating both
the reflection helper and the repo-root walk. The test sits with `ClubRosterProd` instead and
reaches `LoadoutTokens` by reflection (with a hard "type not found" throw, so it cannot pass
vacuously — the `CharacterManager` namespace trap). `Golfin.Gameplay.Missions` was added to that
asmdef so `MissionCsv.Parse` reads the fixture and `mission_loadouts.csv`: still no second parser.

**D2 — `gacha_pools` was NOT re-published, because there is nothing to publish.**
The panel reads `Published v2 · No unpublished changes`, and the drawer says "Drafts match what is
published." Version history explains it: `v2` was published **2026-08-31 10:54 UTC** by
`cesar.guarinoni@gmail.com`, note *"Golfin ball is the default infinite ball — never a prize
(Cesar 2026-08-31)"* — i.e. the deactivation of `psc1_ball_golfin` went live. Rule 21 landed
**after** it: `19f0c8c2b, 2026-08-31 20:56:21 +0900` = 11:56 UTC, one hour later. So the sequence
is: Cesar deactivated the row and published, then rule 21 shipped and started firing on the
already-deactivated row — blocking every SUBSEQUENT `gacha_pools` publish, with deactivation (the
remedy already applied) the only exit the rule would not accept. Nothing in the draft state needs
changing, so a version bump was available only by manufacturing an edit, which I did not do.
Instead the fix is proven directly on those live rows: 1 error before, 0 after (above). This is
the discrepancy Cesar flagged in the kickoff ("published since this was written").

**D3 — `LoadoutTokens` has two public members beyond SPEC §2's three:**
`Matches(string clubId, string name, string type, string token)` and `FamilyOf`. The four-string
overload is the parity surface — it takes exactly the fixture's columns, matches the TypeScript
signature shape, and is what an asmdef test can call by reflection without constructing an
Assembly-CSharp `ClubDataRuntime`. `Matches(ClubDataRuntime, token)` delegates to it, so both
paths make one decision. No behaviour differs from the spec's grammar.

**D4 — the fixture has 21 data rows, not 13.** §6 says "minimum rows"; the extra eight are `IRON7`
(case-insensitivity of the token), `Iron 5 X7` vs `Iron` (family match on a lofted iron), PW/SW
family rows, `S.Wedge` vs `PW` (a wedge must not answer to a different wedge), `Driver` vs `Iron`,
`Wood` vs `Wood`, and `Putter` vs `Putter`. All 21 are asserted in both languages.

**D5 — `content_version.txt` is in the diff.** Not listed in SPEC § "Files this task touches", but
`export_content.py` writes it whenever a published version moves, and `--check` is not clean until
it does. It is the mechanical consequence of the mandated publish, not a separate edit.
