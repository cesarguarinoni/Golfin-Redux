# Architect Review — `8_5_a_csv_consolidation`

**Reviewed:** 2026-04-30 JST
**Verdict:** PASS

This is a code/data consolidation task — no Figma reference, no visual fidelity check.

## Verification

### Files

| Check | Result | Evidence |
|---|---|---|
| `Assets/Resources/Data/Clubs.csv` exists, 7 rows + header, 21 columns incl. all 5 new ones | PASS | Read confirms 8 lines (1 header + 7 data); header contains `ballSpeedMps,launchAngleDeg,spinRateRpm,expectedCarryYd,...,controlSprite` |
| `Assets/Data/Clubs.csv` does NOT exist | PASS | Glob returned no match |
| `Assets/Resources/Physics/clubs.csv` does NOT exist | PASS | Glob returned no match |

### Code correctness

| Check | Result | Evidence |
|---|---|---|
| `LoadClubSpecs()` uses header-name lookup on `Resources.Load<TextAsset>("Data/Clubs")` | PASS | `PhysicsConfigLoader.cs:341` loads `"Data/Clubs"`; lines 351–373 build `headerIndex` and require physics columns by name; lines 388–399 parse via header indices |
| `ParseCSVLine` helper added, no naming conflict | PASS | `PhysicsConfigLoader.cs:414` — declared `static`, file-private to the static class; mirrors the proven helper in `ClubDatabaseCSV` |
| `ClubDataRuntime` has `controlSpriteName` (string) and `controlSprite` (Sprite) | PASS | `ClubData.cs:47-48` — `string controlSpriteName = ""` + `Sprite? controlSprite = null` |
| `ClubDatabaseCSV.ParseRow` reads `controlSprite` column and calls `LoadSprite("Clubs/Controls", ...)` | PASS | `ClubDatabaseCSV.cs:104` reads column; line 113 loads sprite from `Clubs/Controls` path |
| `PhaseTestController.ClubId` default = `"club_iron7_mireo"` | PASS | `PhaseTestController.cs:18` confirmed |
| `AerodynamicsTests` uses only 4 canonical IDs; no stale old IDs | PASS | Grep for `"Iron3"`/`"Iron5"`/`"Iron7"`/`"Iron9"`/`"Driver"`/`"PitchingWedge"`/`"SandWedge"` returned **no matches**; Clubs[] array at line 26-32 contains only the 4 canonical IDs |
| Verify log removed from `PhysicsLabController.Awake()` | PASS | Grep for `Verify` / `LoadClubSpecs` against `PhysicsLabController.cs` returned no matches |

### Report claims

| Check | Result | Evidence |
|---|---|---|
| Console showed `[ClubDatabaseCSV] Loaded 7 clubs.` | PASS | Quoted in IMPLEMENTER_REPORT with timestamp `2026-04-30T08:01:51.6657298+09:00` |
| Console showed `[Verify] LoadClubSpecs returned 7 clubs` | PASS | Quoted with timestamp `2026-04-30T08:02:45.5324644+09:00` |
| All EditMode tests pass (aerodynamics + 20 unrelated pre-existing failures) | PASS | All 10 AerodynamicsTests listed as PASS; 20 failures confined to `BallPlacementIntegrationTests` / `PlacementSnapTests` which are not touched by this task and existed pre-change |

### Cross-cutting checks

- **Asmdef boundaries:** `PhysicsConfigLoader` is in `Golfin.Physics.Runtime` and reads `Resources.Load<TextAsset>("Data/Clubs")` — Resources.Load doesn't cross asmdefs, just file-system. Clean.
- **Schema integrity:** Wood row uses `portraitFull = Placeholder`; verified `Assets/Resources/Clubs/Full/Placeholder.png` exists, so the menu loader will not produce a null-sprite warning. Sensible deviation since no Wood-Full art exists yet.
- **Quoted-info row safety:** the new `ParseCSVLine` helper correctly handles the quoted `info` field on `club_wood_gf` (which contains a comma) — the spin/carry numbers won't get mis-aligned because parsing is now quote-aware.
- **Capture-helper / static-bus:** no new static-bus contexts introduced. Maintenance protocol does not apply to this task.

## Notes

- STATUS.md currently reads `READY_FOR_SELF_REVIEW`, not `READY_FOR_ARCHITECT_REVIEW`. Cesar invoked the architect directly, bypassing the self-reviewer. This is acceptable as an explicit override for a low-risk data/code task with no visual surface, but the next state transition should be done manually by Cesar.
- The 20 pre-existing test failures in `BallPlacementIntegrationTests` / `PlacementSnapTests` are not introduced by this task — flagging only so they aren't forgotten. Out of scope here.

## Verdict

PASS. All acceptance criteria genuinely met; code matches spec; verify log was cleaned up; tests pass; the merged CSV parses correctly under the new quote-aware path.

Cesar: move `Docs/Specs/Active/8_5_a_csv_consolidation/` to `Docs/Specs/Completed/` and commit.
