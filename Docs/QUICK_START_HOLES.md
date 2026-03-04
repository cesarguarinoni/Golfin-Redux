# Quick Start: HoleDatabase CSV Import

The **fastest** way to add holes to the game!

---

## 3-Minute Setup

### 1. Create the Database Asset (1 min)

In Unity:
1. Right-click `Assets/Data/` → **Create → Golfin → Hole Database**
2. Name it `HoleDatabase`

### 2. Import from CSV (1 min)

1. Unity menu: **Golfin → Import Holes from CSV**
2. In the window:
   - **CSV File:** Drag `Assets/Data/HoleDatabase.csv`
   - **Target Database:** Drag your `HoleDatabase` asset
3. Click **Import**
4. Should say "Imported 5 holes from CSV" ✅

### 3. Wire It Up (30 seconds)

Drag `HoleDatabase` into **HomeScreenController → Optional: Hole Database** field

**Done!** The game now has 5 playable holes with rewards.

---

## Test It

Enter Play Mode:
- Course name should show: "Lomond Country Club - Hole 5"
- Rewards: 100 Points, 1 Repair Kit, 3 Balls

---

## Add More Holes

### Edit the CSV (Easy!)

1. Open `Assets/Data/HoleDatabase.csv` in Excel or text editor
2. Add a new line:
   ```csv
   HOLE_AUGUSTA_12,12,Points,300,Ball,10,RepairKit,5
   ```
3. Add to `Assets/Localization/LocalizationText.csv`:
   ```csv
   HOLE_AUGUSTA_12,Augusta National - Hole 12,オーガスタナショナル - ホール12
   ```
4. Re-import: **Golfin → Import Holes from CSV** (same steps as before)

Done! New hole is now in the game.

---

## CSV Format

```csv
courseNameKey,holeNumber,reward1Type,reward1Amount,reward2Type,reward2Amount,reward3Type,reward3Amount
```

**Reward Types:**
- `Points` - Main currency
- `RepairKit` - Item
- `Ball` - Consumable

**Tips:**
- Leave reward columns empty if not needed (use `,,` in CSV)
- Reward types are case-insensitive
- Can have 1-3 rewards per hole

---

## Why CSV?

**vs. Manual Entry in Unity Inspector:**
- ⚡ **10x faster** for adding many holes
- 📊 Edit in Excel/Google Sheets
- 👥 Non-Unity team members can edit
- 📝 Better Git diffs (easy to see what changed)
- 🔄 Bulk operations (find/replace, formulas, etc.)

---

## Full Documentation

- `Assets/Data/CREATE_DATABASE.md` - Complete guide
- `Assets/Data/README_HOLES.md` - General usage
- `Assets/Data/HoleDatabase.csv` - Example CSV

---

## Troubleshooting

**Import menu doesn't appear:**
- Unity might need to recompile: **Assets → Reimport All**
- Or restart Unity

**Holes don't show in game:**
- Check database is assigned to HomeScreenController
- Check `courseNameKey` exists in `LocalizationText.csv`
- Make sure reward types are spelled correctly

---

**Ready to add 100 holes?** Just edit the CSV and click Import! 🚀
