# Hole Database

This folder is where the HoleDatabase ScriptableObject asset will be created.

## Creating the Database (2 minutes in Unity)

**You need to create this in Unity Editor**, not as a text file:

1. Right-click in this folder → **Create → Golfin → Hole Database**
2. Name it `HoleDatabase`
3. Add holes in the Inspector (see CREATE_DATABASE.md for examples)

**Full instructions:** See `CREATE_DATABASE.md` in this folder.

## HoleDatabase Structure

Each hole has:
- **courseNameKey**: Localization key (must exist in LocalizationText.csv)
- **holeNumber**: Hole number (1-18 typically)
- **rewards**: List of rewards (1-3 per hole)

### Reward Types

The `type` field uses numeric values:
- `0` = **Points** (main currency)
- `1` = **Repair Kit** (item to fix clubs)
- `2` = **Ball** (consumable item)

In Unity Inspector, these appear as a dropdown: Points/RepairKit/Ball

## Example Hole Data

```
Course Name Key: HOLE_LOMOND_5
Hole Number: 5
Rewards:
  - Type: Points, Amount: 100
  - Type: RepairKit, Amount: 1
  - Type: Ball, Amount: 3
```

## Adding Holes in Unity

1. Select `HoleDatabase.asset` in Project window
2. In Inspector, expand **Holes** list
3. Click **+** to add a new hole
4. Set **Course Name Key** (e.g., `HOLE_AUGUSTA_12`)
5. Set **Hole Number**
6. Click **+** in **Rewards** to add rewards
7. Choose **Type** (dropdown) and **Amount**

### Don't Forget:
Add the localization key to `Assets/Localization/LocalizationText.csv`:
```csv
HOLE_AUGUSTA_12,Augusta National - Hole 12,オーガスタナショナル - ホール12
```

## Using the Database

After creating it:
1. Drag `HoleDatabase.asset` into **HomeScreenController → Optional: Hole Database** field
2. Game will load hole data from database instead of hardcoded fallback

## Example Holes to Add

See CREATE_DATABASE.md for 5 pre-configured example holes ready to copy.

## Future Extensions

The HoleData structure can be expanded to include:
- Par value
- Difficulty rating
- Weather conditions
- Course layout data
- Special modifiers

Just add fields to `HoleData.cs` and they'll appear in the Unity Inspector!
