# How to Create and Import HoleDatabase

There are **two ways** to populate the HoleDatabase: manually in Unity Inspector, or by importing from CSV (recommended).

---

## Method 1: Import from CSV (Recommended) ⚡

This is **much faster** for managing many holes! You can edit the CSV in Excel/Google Sheets and import with one click.

### Step 1: Create the Database Asset

1. In Unity Project window, go to `Assets/Data/`
2. Right-click → **Create → Golfin → Hole Database**
3. Name it `HoleDatabase`

### Step 2: Import from CSV

1. In Unity menu bar, go to **Golfin → Import Holes from CSV**
2. In the importer window:
   - **CSV File:** Drag `HoleDatabase.csv` (from Assets/Data/)
   - **Target Database:** Drag the `HoleDatabase` asset you just created
3. Click **Import**
4. Done! All holes from CSV are now in the database

### Step 3: Wire It Up

Drag `HoleDatabase` into **HomeScreenController → Optional: Hole Database** field

---

## CSV Format

The CSV has these columns:
```
courseNameKey,holeNumber,reward1Type,reward1Amount,reward2Type,reward2Amount,reward3Type,reward3Amount
```

**Example:**
```csv
HOLE_LOMOND_5,5,Points,100,RepairKit,1,Ball,3
HOLE_RIVERSIDE_1,1,Points,50,Ball,2,,
```

**Rules:**
- **courseNameKey**: Must match a key in `LocalizationText.csv`
- **holeNumber**: 1-18 typically
- **rewardType**: `Points`, `RepairKit`, or `Ball`
- **rewardAmount**: Any positive number
- Leave reward columns empty if you don't need all 3 rewards (use `,,` in CSV)

### Pre-Made CSV

A ready-to-use `HoleDatabase.csv` is included with 5 example holes:
- Lomond CC - Hole 5
- Riverside GC - Hole 1
- Highland Hills - Hole 3
- Lomond CC - Hole 6
- Riverside GC - Hole 2

Just import it and you're done!

---

## Method 2: Manual Entry in Unity Inspector

If you prefer to add holes manually:

### Step 1: Create the Database

Same as above (Create → Golfin → Hole Database)

### Step 2: Add Holes Manually

Select `HoleDatabase` asset and in Inspector:

**Hole 1:**
- Course Name Key: `HOLE_LOMOND_5`
- Hole Number: `5`
- Rewards (click + to add):
  - Type: Points, Amount: 100
  - Type: RepairKit, Amount: 1
  - Type: Ball, Amount: 3

**Hole 2:**
- Course Name Key: `HOLE_RIVERSIDE_1`
- Hole Number: `1`
- Rewards:
  - Type: Points, Amount: 50
  - Type: Ball, Amount: 2

(Continue for all holes...)

---

## Editing Holes

### Editing CSV (Easy!)

1. Open `Assets/Data/HoleDatabase.csv` in Excel, Google Sheets, or text editor
2. Add/edit/remove rows (keep the header!)
3. Save the CSV
4. In Unity: **Golfin → Import Holes from CSV** → Import again
5. Done! Database updated

### Editing in Unity Inspector

1. Select `HoleDatabase` asset
2. Expand **Holes** list
3. Click **+** to add, or edit existing entries
4. Save changes (Ctrl+S)

---

## Troubleshooting

### "Golfin → Import Holes from CSV" menu doesn't appear

- Make sure you've pulled the latest code
- Unity might need to recompile - try **Assets → Reimport All**
- Or close Unity and reopen

### Import says "0 holes imported"

- Check CSV has data rows (not just header)
- Make sure fields are comma-separated
- Check for empty lines at the end of the file

### Rewards not showing in game

- Check `courseNameKey` exists in `LocalizationText.csv`
- Make sure reward types are spelled correctly: `Points`, `RepairKit`, `Ball` (case-insensitive)
- Make sure database is assigned to HomeScreenController

---

## Adding New Holes

### In CSV:
1. Add a new line to `HoleDatabase.csv`:
   ```csv
   HOLE_AUGUSTA_12,12,Points,300,Ball,10,RepairKit,5
   ```
2. Add localization key to `LocalizationText.csv`:
   ```csv
   HOLE_AUGUSTA_12,Augusta National - Hole 12,オーガスタナショナル - ホール12
   ```
3. Re-import: **Golfin → Import Holes from CSV**

### In Unity Inspector:
1. Select `HoleDatabase` asset
2. Click **+** in Holes list
3. Fill in fields
4. Don't forget to add localization key!

---

## Why CSV?

**Benefits:**
- ✅ Faster for adding many holes
- ✅ Easy to edit in spreadsheet software
- ✅ Can share with non-Unity team members
- ✅ Easy to version control (Git diff shows changes clearly)
- ✅ Can bulk-edit (find/replace, formulas, etc.)

**When to use Inspector:**
- Quick edits to 1-2 holes
- Prototyping/testing
- Don't have CSV editing tool available
