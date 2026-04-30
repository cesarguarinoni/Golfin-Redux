# 8.5.A — Clubs CSV consolidation (single source of truth)

> **Tier 2 — TellCode-style established pattern.** Multi-file refactor, follows the existing CSV-loader idioms, no new architecture.
> **Created:** 2026-04-30 14:35 JST
> **Owner:** golfin-implementer
> **Depends on:** none (Block 1 of 8.5 sequence)
> **Blocks:** 8.5.B (Lab inventory seeder needs the merged ID space)

---

## Why

Two clubs CSVs exist today, with **disjoint ID spaces** and **disjoint schemas**, and they cannot be cross-referenced:

| File | Purpose | Loader | Schema |
|---|---|---|---|
| `Assets/Data/Clubs.csv` | Menu catalog (rosters, inventory, art, rarity) | `ClubDatabaseCSV.LoadCSV()` — Inspector-assigned `TextAsset`, header-name lookup | id, name, type, rarity, brand, basePower, baseAccuracy, baseLieResistance, baseLoft, maxDurability, baseDistance, portraitSprite, portraitFull, startLevel, maxLevel, info |
| `Assets/Resources/Physics/clubs.csv` | Physics tuning (sim) | `PhysicsConfigLoader.LoadClubSpecs()` — `Resources.Load<TextAsset>("Physics/clubs")`, **positional** parse | id, ball_speed_mps, launch_angle_deg, spin_rate_rpm, expected_carry_yd, notes |

Editing physics CSV without club names is painful; menu CSV doesn't carry physics tuning; nothing joins them. **Cesar wants one centralized file with all club data.**

## Decision

**Merge into `Assets/Data/Clubs.csv` as the single canonical file. Delete `Assets/Resources/Physics/clubs.csv`.**

Add 4 physics columns + 1 control-sprite column to the canonical schema. Keep the menu-side loader (`ClubDatabaseCSV`) untouched at the Inspector-`TextAsset` pattern. Rewrite the physics loader to read the same canonical CSV via `Resources.Load` — which means we move the canonical file into a `Resources/` folder so both loaders can use it.

**Key constraint from Cesar:** *"Move it wherever it fits but don't break Menus that are already working with this file."* Menu loader uses Inspector reference, so as long as we re-point the Inspector reference after the move, menus keep working.

### Final canonical schema

```csv
id,name,type,rarity,brand,basePower,baseAccuracy,baseLieResistance,baseLoft,maxDurability,baseDistance,ballSpeedMps,launchAngleDeg,spinRateRpm,expectedCarryYd,portraitSprite,portraitFull,controlSprite,startLevel,maxLevel,info
```

**New columns added** (5):
- `ballSpeedMps` — float, ball speed at impact in m/s (carries forward from `Physics/clubs.csv`)
- `launchAngleDeg` — float, launch angle in degrees
- `spinRateRpm` — float, backspin rate in rpm
- `expectedCarryYd` — float, expected carry in yards (informational/test target, not used by sim)
- `controlSprite` — string, name of the action-button sprite under `Resources/Clubs/Controls/` (e.g. `S_Controls_Driver_GF` for `S_Controls_Driver_GF.png`)

**Existing columns unchanged.**

---

## File moves

1. **Move** `Assets/Data/Clubs.csv` → `Assets/Resources/Data/Clubs.csv`.
   - Both CSV consumers will be able to use `Resources.Load<TextAsset>("Data/Clubs")` after the move.
   - The Inspector reference on `ClubDatabaseCSV` (in any scene that has it: ShellScene, Roster scene, etc.) currently points at `Assets/Data/Clubs.csv`. **After the move, re-assign the Inspector reference** so it points at `Assets/Resources/Data/Clubs.csv`. Unity should re-link automatically via GUID — but verify: open every scene that has a `ClubDatabaseCSV` component and confirm `clubsCSV` field is non-null and points at the new path. If GUID re-link fails, drag the moved CSV in.

2. **Delete** `Assets/Resources/Physics/clubs.csv` after the merged CSV ships.

3. **Create** the `Assets/Resources/Data/` folder if it doesn't exist (Unity will auto-create on move).

---

## CSV row population

The 6 existing menu rows + 1 new Wood row + 4 physics columns. Final rows (in order):

| id | name | type | physics: ballSpeedMps / launchAngleDeg / spinRateRpm / expectedCarryYd | controlSprite |
|---|---|---|---|---|
| `club_driver_gf` | Driver G&F | Driver | 75.0 / 10.9 / 2686 / 275 | `S_Controls_Driver_GF` |
| `club_wood_gf` (NEW) | Wood G&F | Wood | 70.6 / 9.2 / 3655 / 243 | `S_Controls_Wood_GF` |
| `club_iron9_klyro` | Iron 9 Klyro | Iron | 48.5 / 20.0 / 8647 / 152 | `S_Controls_Iron_KLYRO` |
| `club_iron7_mireo` | Iron 7 Mireo | Iron | 52.5 / 16.3 / 7097 / 172 | `S_Controls_Iron_MIREO` |
| `club_awedge_fyloe` | A. Wedge Fyloe | A.Wedge | 46.0 / 24.0 / 9300 / 136 | `S_Controls_Wedge_FYLOE` |
| `club_pwedge_royal` | P.Wedge Royal Swing | P.Wedge | 46.0 / 24.0 / 9300 / 136 | `S_Controls_Wedge_ROYAL` |
| `club_putter_golfinx` | Putter GolfinX | Putter | 5.0 / 5.0 / 0 / 30 | `S_Controls_Putter_GOLFINIX` |

### Physics number sources

- **Driver, Iron 7, Iron 9** — values copied from current `Assets/Resources/Physics/clubs.csv` (rows `Driver`, `Iron7`, `Iron9`).
- **Wood (new row)** — PGA Tour 3-Wood averages from Trackman (sourced via web search 2026-04-30): ball speed 158 mph (= **70.6 m/s**), launch 9.2°, spin 3655 rpm, carry 243 yd.
- **A.Wedge, P.Wedge** — both use `PitchingWedge` row values from old physics CSV (46.0 / 24.0 / 9300 / 136). Variation between A and P wedges can be tuned later if needed; for v1 they share numbers.
- **Putter** — values from `PhysicsLabController.LabClubs[3]` which uses 5.0 m/s loft / 5.0° / 0 rpm — these were never in the physics CSV. Carry of 30 is a sensible placeholder for a putt-distance club.

### New row authoring

The new `club_wood_gf` row needs a portrait. Use existing art:
- `portraitSprite` → `Wood-G&F` (note ampersand) — **CHECK FIRST:** does `Resources/Clubs/Portraits/Wood-G&F.png` exist? Run `dir Assets\Resources\Clubs\Portraits\Wood*` to confirm before authoring this row. If the file is named differently (e.g. `Wood-GF.png` without ampersand, or doesn't exist at all), pick whatever Wood portrait IS in the folder and use that name. **Do not create new art.** If no Wood portrait exists at all, surface to Architect.
- `portraitFull` → same name as `portraitSprite` (matches existing rows' pattern)
- `controlSprite` → `S_Controls_Wood_GF` (this PNG is confirmed present per `dir Assets\Resources\Clubs\Controls\S_Controls_Wood_GF.png`)
- `rarity` → `Common`
- `brand` → `G&F`
- `basePower` / `baseAccuracy` / `baseLieResistance` / `baseLoft` / `maxDurability` / `baseDistance` → use sensible Wood values: `70 / 35 / 12 / 15 / 100 / 230`. (Slightly less power than driver, slightly more accuracy, longer baseDistance than irons.)
- `startLevel` / `maxLevel` → `10 / 39` (matches Driver G&F row — same Common-tier level band)
- `info` → `"A versatile fairway wood from G&F. Solid carry, more forgiving than the driver."`

### Existing row updates

For the 6 existing rows, populate the 5 new columns:
- `ballSpeedMps`, `launchAngleDeg`, `spinRateRpm`, `expectedCarryYd` — per the table above
- `controlSprite` — per the table above. Confirm each `S_Controls_*.png` exists in `Resources/Clubs/Controls/` before authoring (we know the folder has plenty per the directory listing taken 2026-04-30):
  - `S_Controls_Driver_GF.png` ✓ confirmed
  - `S_Controls_Iron_KLYRO.png` ✓ confirmed
  - `S_Controls_Iron_MIREO.png` ✓ confirmed
  - `S_Controls_Wedge_FYLOE.png` ✓ confirmed
  - `S_Controls_Wedge_ROYAL.png` ✓ confirmed
  - `S_Controls_Putter_GOLFINIX.png` ✓ confirmed (note: the file is `GOLFINIX` per the directory; the row id is `golfinx`)

---

## Code changes

### 1. `PhysicsConfigLoader.LoadClubSpecs()` rewrite

Located at `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` lines ~337–375.

**Current implementation: positional parse, fragile.** Reads `parts[0..4]` by index.

**Replace with header-name lookup, mirroring the pattern in `ClubDatabaseCSV.ParseRow`:**

```csharp
public static List<ClubSpec> LoadClubSpecs()
{
    var result = new List<ClubSpec>();
    var ta = Resources.Load<TextAsset>("Data/Clubs");   // CHANGED from "Physics/clubs"
    if (ta == null)
    {
        Debug.LogWarning("[PhysicsConfigLoader] Data/Clubs.csv not found");
        return result;
    }

    string[] lines = ta.text.Split('\n');
    if (lines.Length < 2) return result;

    // Build header index (column name -> index)
    var headerCells = lines[0].Split(',');
    var headerIndex = new Dictionary<string, int>();
    for (int h = 0; h < headerCells.Length; h++)
        headerIndex[headerCells[h].Trim()] = h;

    // Required physics columns
    if (!headerIndex.ContainsKey("id") ||
        !headerIndex.ContainsKey("ballSpeedMps") ||
        !headerIndex.ContainsKey("launchAngleDeg") ||
        !headerIndex.ContainsKey("spinRateRpm") ||
        !headerIndex.ContainsKey("expectedCarryYd"))
    {
        Debug.LogWarning("[PhysicsConfigLoader] Data/Clubs.csv is missing one or more required physics columns " +
                         "(id, ballSpeedMps, launchAngleDeg, spinRateRpm, expectedCarryYd) — physics will use defaults");
        return result;
    }

    int idxId      = headerIndex["id"];
    int idxSpeed   = headerIndex["ballSpeedMps"];
    int idxAngle   = headerIndex["launchAngleDeg"];
    int idxSpin    = headerIndex["spinRateRpm"];
    int idxCarry   = headerIndex["expectedCarryYd"];

    for (int i = 1; i < lines.Length; i++)
    {
        var line = lines[i].Trim();
        if (line.Length == 0 || line.StartsWith("#")) continue;

        // CSV may contain quoted fields with embedded commas in the `info` column —
        // reuse the quote-aware parser pattern (see ClubDatabaseCSV.ParseCSVLine).
        var parts = ParseCSVLine(line);
        if (parts.Count <= idxCarry) continue;  // not enough cells

        string id = parts[idxId].Trim().Trim('"');
        if (string.IsNullOrEmpty(id)) continue;

        if (!float.TryParse(parts[idxSpeed].Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float speed)) continue;
        if (!float.TryParse(parts[idxAngle].Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float angle)) continue;
        if (!float.TryParse(parts[idxSpin].Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float spin)) continue;
        if (!float.TryParse(parts[idxCarry].Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float carry)) continue;

        result.Add(new ClubSpec
        {
            Id              = id,
            BallSpeedMps    = fp.FromFloat(speed),
            LaunchAngleDeg  = fp.FromFloat(angle),
            SpinRateRpm     = fp.FromFloat(spin),
            ExpectedCarryYd = fp.FromFloat(carry),
        });
    }
    return result;
}

// Add this helper at the bottom of PhysicsConfigLoader (or copy from ClubDatabaseCSV)
static List<string> ParseCSVLine(string line)
{
    var fields  = new List<string>();
    var current = new System.Text.StringBuilder();
    bool inQuotes = false;
    for (int i = 0; i < line.Length; i++)
    {
        char c = line[i];
        if (c == '"')
        {
            if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            { current.Append('"'); i++; }
            else
            { inQuotes = !inQuotes; }
        }
        else if (c == ',' && !inQuotes)
        { fields.Add(current.ToString()); current.Clear(); }
        else
        { current.Append(c); }
    }
    fields.Add(current.ToString());
    return fields;
}
```

### 2. `ClubDataRuntime` — add controlSprite field

Located at `Assets/Scripts/UI/Inventory/ClubData.cs` (or wherever `ClubDataRuntime` is defined — search if not found).

Add a field for the loaded control sprite. Mirror the pattern of `portraitSprite` / `portraitFull`:

```csharp
public string controlSpriteName;
public Sprite controlSprite;
```

### 3. `ClubDatabaseCSV.ParseRow` — load controlSprite

In `Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs`, around line ~95 (where `portraitSpriteName` and `portraitFullName` are read):

```csharp
// EXISTING:
portraitSpriteName = Get("portraitSprite"),
portraitFullName   = Get("portraitFull"),
// NEW — add this line:
controlSpriteName  = Get("controlSprite"),
```

And around line ~108 (where `LoadSprite` calls are made):

```csharp
// EXISTING:
club.portraitSprite = LoadSprite(PortraitPath, club.portraitSpriteName);
club.portraitFull   = LoadSprite(FullPath,     club.portraitFullName);
// NEW — add this line:
club.controlSprite  = LoadSprite("Clubs/Controls", club.controlSpriteName);
```

This is **purely additive** — does not break existing menu code. The new field is null on rows that don't populate `controlSprite` (which won't happen since all 7 rows in the new CSV will have it).

### 4. `PhaseTestController.cs` — update default ClubId

Located at `Assets/Scripts/Physics/Runtime/PhaseTestController.cs` line 18.

```csharp
// BEFORE:
public string ClubId = "Iron7";
// AFTER:
public string ClubId = "club_iron7_mireo";
```

### 5. `AerodynamicsTests.cs` — update test ID list

Located at `Assets/Scripts/Physics/Tests/AerodynamicsTests.cs`. The test array is at line ~25 — it's a positional list of `(id, speedMps, angleDeg, spinRpm, expectedYd)` tuples. **Update each `id` string** to match the new schema:

| Old id | New id |
|---|---|
| `"Driver"` | `"club_driver_gf"` |
| `"Iron3"` | (drop — no Iron 3 in canonical CSV) OR map to `"club_iron9_klyro"` if the test array shape requires 7 entries |
| `"Iron5"` | (drop) |
| `"Iron7"` | `"club_iron7_mireo"` |
| `"Iron9"` | `"club_iron9_klyro"` |
| `"PitchingWedge"` | `"club_pwedge_royal"` |
| `"SandWedge"` | (drop — no Sand Wedge in canonical CSV) |

**Decision for the test:** keep only the 4 IDs that exist in the new canonical CSV (`club_driver_gf`, `club_iron7_mireo`, `club_iron9_klyro`, `club_pwedge_royal`) and update the expected-yards values to match. If the test was failing/passing on specific carry numbers, update those too — read the test method to see what it asserts. **If updating the test breaks its assertion (e.g. expected yardage no longer matches), surface to Architect rather than silently changing assertion values.**

---

## Acceptance criteria

### Files

- [ ] `Assets/Resources/Data/Clubs.csv` exists, has 7 rows + header, header includes all new columns.
- [ ] `Assets/Data/Clubs.csv` does NOT exist (moved).
- [ ] `Assets/Resources/Physics/clubs.csv` does NOT exist (deleted).

### Menu side (must not break)

- [ ] Open ShellScene (or whichever scene has `ClubDatabaseCSV`). The `clubsCSV` field on `ClubDatabaseCSV` GameObject points at `Assets/Resources/Data/Clubs.csv`.
- [ ] Run play mode in ShellScene (or wherever the inventory loads). Console shows `[ClubDatabaseCSV] Loaded 7 clubs.` (or similar). No errors.
- [ ] If a roster/inventory screen is reachable from this play session: open it and verify clubs render with their portraits (no white boxes).

### Physics side

- [ ] Compile passes with no errors.
- [ ] Open LabScaffold, enter play mode. Console shows no `[PhysicsConfigLoader] Data/Clubs.csv not found` warning.
- [ ] Run a Fire shot from the Lab UI. Trajectory renders. (Sim still uses `LabClubs[]` hardcoded array, not `ClubSpecs` — that wiring is a future task. For now we just verify `LoadClubSpecs()` returns 7 entries when called, and the file loads.)
- [ ] Add a one-shot `Debug.Log` somewhere in `PhysicsLabController.Awake()`: `Debug.Log($"[Verify] LoadClubSpecs returned {PhysicsConfigLoader.LoadClubSpecs().Count} clubs");` — confirm output `7`. Remove the log after verifying.

### Tests

- [ ] All EditMode tests pass (run `GOLFIN/Tests` or whatever the test runner menu is). If `AerodynamicsTests` was edited and now fails, surface the failure with the actual vs expected numbers.

---

## Out of scope / future tasks

- **Localization** — Cesar flagged that `Clubs.csv` isn't properly linked to localization texts. Not in this spec. Cross that bridge when we get there.
- **Wiring** `ClubSpecs` **into** `PhysicsLabController.LabClubs[]` — currently the lab uses hardcoded inline `ClubStats` values, not the loaded `ClubSpecs`. Aligning the lab to use the merged CSV is a future task (likely part of the central-ball / TargetingLine block, or a follow-up to it).
- **Iron 3 / Iron 5 / Sand Wedge / Hybrid** — these existed in the old physics CSV but not in the menu CSV. They're dropped in this consolidation. If a future club catalog expansion needs them, add new rows to the canonical CSV.
- **Per-rarity stat scaling** — current basePower/etc values are hand-authored per row. No automated scaling from rarity tier yet.

---

## Done report (when complete)

In `Docs/Specs/Active/8_5_a_csv_consolidation/IMPLEMENTER_REPORT.md`:

- Files moved / deleted / modified list.
- Wood portrait file actually used (matches what was in `Resources/Clubs/Portraits/`).
- ParseRow change confirmed compiling and running on existing scenes.
- Console output from the verification log (`LoadClubSpecs returned N clubs`).
- All EditMode test pass/fail summary.
- Any deviations from spec (e.g. if a control sprite filename was different from what the table assumed).

Then set `STATUS.md` to `IMPLEMENTER_DONE` and run the self-reviewer.
