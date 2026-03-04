# Quick Start: HoleDatabase Auto-Load from CSV

The **easiest** way to manage holes - just edit the CSV and play! No manual import needed.

---

## 2-Minute Setup (Auto-Load)

### 1. Add HoleDatabaseLoader to Scene (1 min)

In Unity:
1. Select any GameObject in your scene (e.g., `ShellSceneRoot` or create a new empty called `GameData`)
2. **Add Component → Hole Database Loader**
3. In Inspector:
   - **Hole Database CSV:** Drag `Assets/Data/HoleDatabase.csv`
   - **Auto Load On Awake:** ✅ (checked)

### 2. Test It (30 sec)

Enter Play Mode:
- Holes automatically load from CSV! 🎉
- Course name shows: "Lomond Country Club - Hole 5"
- Rewards: 100 Points, 1 Repair Kit, 3 Balls

**Done!** No manual database creation or import needed.

---

## How It Works

Similar to localization, the `HoleDatabaseLoader` automatically:
1. Reads `HoleDatabase.csv` when the scene starts
2. Creates a runtime database in memory
3. HomeScreenController loads holes from it

**Just edit the CSV and play - changes apply instantly!**

---

## Adding More Holes

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
4. Enter Play Mode - new hole automatically loads!

No import button, no manual steps. Just edit and play.

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

## Alternative: Manual Import (Advanced)

If you prefer a ScriptableObject asset instead of auto-loading:

1. Create: **Create → Golfin → Hole Database**
2. Import: **Golfin → Import Holes from CSV**
3. Assign to **HomeScreenController → Optional: Hole Database**

The manual approach is useful for:
- Editing holes in Unity Inspector (visual interface)
- Sharing hole data as a Unity asset
- When you don't want CSV to control game data

But **auto-load is recommended** for most cases!

---

## Benefits of Auto-Load

**vs. Manual Import:**
- ⚡ **No import step** - just edit CSV and play
- 🔄 **Instant updates** - changes apply immediately
- 📊 **Edit in Excel** - easier than Unity Inspector
- 👥 **Non-Unity team members** can edit holes
- 🎯 **Less error-prone** - no forgetting to import

**vs. Hardcoded Data:**
- 📝 **Easy to edit** - no code changes
- 🚀 **Fast iteration** - change and test instantly
- 📚 **Better organization** - all holes in one place

---

## Troubleshooting

**Holes don't load:**
- Check HoleDatabaseLoader is in the scene
- Check CSV file is assigned in Inspector
- Check Console for errors (CSV parsing issues)

**Course name shows key instead of text:**
- Add localization key to `LocalizationText.csv`
- Make sure LocalizationManager is initialized first

**Rewards don't show:**
- Check reward types are spelled correctly in CSV
- Check reward amounts are > 0
- Make sure reward icons are assigned in HomeScreenController

---

## Performance

**Is loading from CSV slow?**

No! The CSV is parsed once on scene start (Awake). Takes <10ms for 100 holes.

If you need faster startup, use the manual approach (pre-import to ScriptableObject).

---

## Full Documentation

- `Assets/Data/CREATE_DATABASE.md` - Manual import guide (alternative approach)
- `Assets/Data/README_HOLES.md` - General usage
- `Assets/Data/HoleDatabase.csv` - Example CSV with 5 holes

---

**Ready to add 100 holes? Just edit the CSV and play!** 🚀
