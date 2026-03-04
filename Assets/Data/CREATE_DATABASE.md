# How to Create HoleDatabase in Unity

The HoleDatabase asset needs to be created in Unity Editor (not as a text file) so Unity can properly link the script references.

## Quick Steps (2 minutes)

1. **In Unity Project window:**
   - Navigate to `Assets/Data/` folder
   - Right-click → **Create → Golfin → Hole Database**
   - Name it `HoleDatabase`

2. **Add Example Holes:**
   Select the new `HoleDatabase` asset and in Inspector:

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

   **Hole 3:**
   - Course Name Key: `HOLE_HIGHLAND_3`
   - Hole Number: `3`
   - Rewards:
     - Type: Points, Amount: 150
     - Type: RepairKit, Amount: 2
     - Type: Ball, Amount: 5

   **Hole 4:**
   - Course Name Key: `HOLE_LOMOND_6`
   - Hole Number: `6`
   - Rewards:
     - Type: Points, Amount: 200
     - Type: RepairKit, Amount: 3

   **Hole 5:**
   - Course Name Key: `HOLE_RIVERSIDE_2`
   - Hole Number: `2`
   - Rewards:
     - Type: Points, Amount: 75
     - Type: Ball, Amount: 3

3. **Wire it up:**
   - Drag `HoleDatabase` into **HomeScreenController → Optional: Hole Database** field

Done! The game will now load holes from your database.

## If "Create → Golfin → Hole Database" Doesn't Appear

Unity might need to recompile first:
1. Go to **Assets → Reimport All** (this can take a few minutes)
2. Or close Unity and reopen the project
3. The menu item should now appear

## Troubleshooting

**Error: "Missing script reference"**
- Make sure `HoleData.cs` exists in `Assets/Scripts/UI/`
- Try right-clicking `HoleData.cs` → Reimport
- Close and reopen Unity

**Can't find the Create menu item:**
- Check that `HoleData.cs` has the line: `[CreateAssetMenu(fileName = "HoleDatabase", menuName = "Golfin/Hole Database")]`
- This should be on the `HoleDatabase` class (line ~50 in HoleData.cs)
