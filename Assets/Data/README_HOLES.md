# Hole Database

This folder contains the HoleDatabase ScriptableObject asset which stores all hole data for the game.

## HoleDatabase.asset

The main database containing all holes. Each hole has:
- **courseNameKey**: Localization key (must exist in LocalizationText.csv)
- **holeNumber**: Hole number (1-18 typically)
- **rewards**: List of rewards (1-3 per hole)

### Reward Types

The `type` field uses numeric values:
- `0` = **Points** (main currency)
- `1` = **Repair Kit** (item to fix clubs)
- `2` = **Ball** (consumable item)

### Example Hole Entry

```yaml
- courseNameKey: HOLE_LOMOND_5
  holeNumber: 5
  rewards:
  - type: 0      # Points
    amount: 100
  - type: 1      # Repair Kit
    amount: 1
  - type: 2      # Ball
    amount: 3
```

## Adding New Holes

### In Unity Editor:
1. Select `HoleDatabase.asset` in Project window
2. In Inspector, expand **Holes** list
3. Click **+** to add a new hole
4. Set **Course Name Key** (e.g., `HOLE_AUGUSTA_12`)
5. Set **Hole Number**
6. Click **+** in **Rewards** to add rewards
7. Choose **Type** (Points/RepairKit/Ball) and **Amount**

### Don't Forget:
Add the localization key to `Assets/Localization/LocalizationText.csv`:
```csv
HOLE_AUGUSTA_12,Augusta National - Hole 12,オーガスタナショナル - ホール12
```

## Editing Manually (Advanced)

You can also edit `HoleDatabase.asset` directly in a text editor:
1. Make sure Unity is closed
2. Edit the YAML file
3. Reopen Unity to see changes

## Current Holes

The database ships with 5 example holes:
1. **Lomond CC - Hole 5**: 100 Points, 1 Repair Kit, 3 Balls
2. **Riverside GC - Hole 1**: 50 Points, 2 Balls
3. **Highland Hills - Hole 3**: 150 Points, 2 Repair Kits, 5 Balls
4. **Lomond CC - Hole 6**: 200 Points, 3 Repair Kits
5. **Riverside GC - Hole 2**: 75 Points, 3 Balls

## Future Extensions

The HoleData structure can be expanded to include:
- Par value
- Difficulty rating
- Weather conditions
- Course layout data
- Special modifiers

Just add fields to `HoleData.cs` and they'll appear in the Unity Inspector!
