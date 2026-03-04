# Home Screen - Final Setup Guide

## What Was Added

### New Scripts
1. **SwipeDetector.cs** - Detects swipe left/right for news carousel
2. **HoleData.cs** - Data structure for holes (name, rewards)
3. **Updated HomeScreenController.cs** - Added:
   - Localization for course names
   - Auto-cycle timer for news panel (configurable)
   - Reward type support (Points, Repair Kit, Ball)
   - Integration with HoleData structure

### Updated Files
- **LocalizationText.csv** - Added:
  - `HOME_NEXT_HOLE` - "NEXT HOLE" title
  - `HOLE_LOMOND_5` - Lomond Country Club - Hole 5
  - `HOLE_RIVERSIDE_1` - Riverside Golf Club - Hole 1
  - `HOLE_HIGHLAND_3` - Highland Hills - Hole 3

---

## Setup Instructions

### 1. Add SwipeDetector to News Panel

1. Select the **News Panel** GameObject in the hierarchy
2. Click **Add Component** → Search for **Swipe Detector**
3. In the **Swipe Detector** component:
   - Set **Swipe Threshold** to `50` (or adjust to your preference)
   - Wire up events:
     - **On Swipe Left** → Drag **HomeScreenController** → Select `HomeScreenController.NextNewsPage()`
     - **On Swipe Right** → Drag **HomeScreenController** → Select `HomeScreenController.PreviousNewsPage()`

**Why:** This allows users to swipe left/right on the news panel to manually change announcements.

---

### 2. Configure Auto-Cycle Timer

1. Select **HomeScreenController** in the inspector
2. In **News Panel** section:
   - Set **News Auto Cycle Interval** to `5` (seconds between auto-transitions)
   - You can set to `0` to disable auto-cycle

**Why:** The news panel will automatically cycle through announcements every 5 seconds.

---

### 3. Add Reward Icons (Optional)

If you have reward icon sprites ready:

1. Select **HomeScreenController** in the inspector
2. In **Reward Icons** section:
   - Drag **Points icon sprite** → `Points Icon`
   - Drag **Repair Kit icon sprite** → `Repair Kit Icon`
   - Drag **Ball icon sprite** → `Ball Icon`

If you don't have icons yet, the script will still work (just won't show icons).

**Why:** These icons will display next to reward amounts (e.g., "Points x100" with coin icon).

---

### 4. (Optional) Create Hole Database

For more organized hole management:

1. Right-click in Project window → **Create → Golfin → Hole Database**
2. Name it `HoleDatabase`
3. In the inspector, click **+** to add holes:
   - **Course Name Key:** `HOLE_LOMOND_5` (must match CSV key)
   - **Hole Number:** `5`
   - **Rewards:**
     - Click **+** to add rewards
     - Set **Type** (Points, RepairKit, Ball) and **Amount**
     - Example: 
       - Type: `Points`, Amount: `100`
       - Type: `RepairKit`, Amount: `1`
       - Type: `Ball`, Amount: `3`
4. Drag the `HoleDatabase` asset into **HomeScreenController** → **Optional: Hole Database** field

If you skip this, the script will use fallback hardcoded data (Lomond Hole 5).

---

### 5. Test in Play Mode

**What to test:**

1. **News Panel Auto-Cycle:**
   - Enter Play Mode
   - Wait 5 seconds → News should cycle to next page
   - Dots should update to show current page

2. **News Panel Swipe:**
   - Click and drag left on News Panel → Should go to next page
   - Click and drag right → Should go to previous page

3. **Next Hole Panel:**
   - Course name should show localized text (English: "Lomond Country Club - Hole 5")
   - If you added reward icons, they should display next to amounts
   - If you added HoleDatabase, it should load from there

4. **Localization:**
   - Change language in LocalizationManager (if you have a language switcher)
   - Course name should switch to Japanese: "ロモンドカントリークラブ - ホール5"

---

## How It Works

### Auto-Cycle Timer
```csharp
private void Update()
{
    if (_autoCycleNews && totalNewsPages > 1 && newsAutoCycleInterval > 0f)
    {
        _newsTimer += Time.deltaTime;
        if (_newsTimer >= newsAutoCycleInterval)
        {
            _newsTimer = 0f;
            NextNewsPage(); // Cycles to next page
        }
    }
}
```
- Resets timer when user manually swipes (prevents jarring auto-cycle right after manual swipe)

### Localization
```csharp
courseNameText.text = LocalizationManager.Get("HOLE_LOMOND_5");
```
- Fetches localized text from CSV based on current language
- Automatically updates when language changes

### Rewards
```csharp
SetupRewardRow(0, RewardType.Points, 100);
SetupRewardRow(1, RewardType.RepairKit, 1);
SetupRewardRow(2, RewardType.Ball, 3);
```
- Shows/hides reward rows based on amount (0 = hidden)
- Sets icon sprite based on reward type
- Formats amount text as "x100", "x1", etc.

---

## Troubleshooting

### Issue: News panel doesn't auto-cycle
**Fix:**
- Check `News Auto Cycle Interval` > 0 in HomeScreenController inspector
- Check `Total News Pages` > 1
- Make sure HomeScreenController is active in the scene

### Issue: Swipe doesn't work
**Fix:**
- Make sure SwipeDetector is attached to News Panel
- Check that News Panel has an **Image** component (even if transparent) for raycast target
- Verify EventSystem exists in the scene
- Check swipe events are wired to `HomeScreenController.NextNewsPage()` / `PreviousNewsPage()`

### Issue: Course name shows key instead of text
**Fix:**
- Check that `HOLE_LOMOND_5` exists in `LocalizationText.csv`
- Verify LocalizationManager is initialized (should happen in LocalizationBootstrap)
- Check that LocalizationTextTable asset is updated (re-import CSV if needed)

### Issue: Reward icons don't show
**Fix:**
- Check that icon sprites are assigned in HomeScreenController → Reward Icons section
- If icons are null, text will still display correctly (just no icon)

### Issue: Rewards don't show at all
**Fix:**
- Check that `rewardRow1`, `rewardRow2`, `rewardRow3` GameObjects are assigned in inspector
- Check that reward amounts are > 0 (0 = hidden)
- If reward rows are null, script handles gracefully (just logs warning)

---

## Next Steps

### Now Working ✅
- ✅ Localization for Next Hole name
- ✅ Rewards display (3 reward slots with icons)
- ✅ Notice Panel carousel (auto-cycle + swipe)

### Future Enhancements
- [ ] Multiple news announcements (currently only shows one from CSV)
- [ ] Load hole data from server instead of HoleDatabase
- [ ] Reward icon animations (pulse, shine, etc.)
- [ ] Swipe animation (smooth slide transition instead of instant)
- [ ] Different rewards for replaying holes (reduced amounts)

---

## Data Structure Example

If you want to add more holes, just add more keys to the CSV:

```csv
key,English,Japanese
HOLE_PEBBLEBEACH_7,Pebble Beach - Hole 7,ペブルビーチ - ホール7
HOLE_AUGUSTA_12,Augusta National - Hole 12,オーガスタナショナル - ホール12
```

Then in HoleDatabase:
- Course Name Key: `HOLE_PEBBLEBEACH_7`
- Rewards: Points x200, Ball x5, RepairKit x2

---

## Summary

**What you need to do:**
1. Add SwipeDetector to News Panel + wire up events (2 minutes)
2. (Optional) Add reward icon sprites to HomeScreenController (1 minute)
3. (Optional) Create HoleDatabase and populate with holes (5 minutes)
4. Test in Play Mode (2 minutes)

**Total time:** ~5-10 minutes

Everything is backward-compatible - if you don't wire up icons/database, it uses fallback data and still works!

---

## HoleDatabase Asset (Included!)

A ready-to-use **HoleDatabase.asset** is now included at `Assets/Data/HoleDatabase.asset` with 5 example holes:

1. **Lomond CC - Hole 5**: 100 Points, 1 Repair Kit, 3 Balls
2. **Riverside GC - Hole 1**: 50 Points, 2 Balls
3. **Highland Hills - Hole 3**: 150 Points, 2 Repair Kits, 5 Balls
4. **Lomond CC - Hole 6**: 200 Points, 3 Repair Kits
5. **Riverside GC - Hole 2**: 75 Points, 3 Balls

### Quick Setup:
1. In Unity, select `Assets/Data/HoleDatabase.asset`
2. Drag it into **HomeScreenController → Optional: Hole Database** field
3. Done! Game will now cycle through these holes

### Adding More Holes:
1. Select `HoleDatabase.asset` in Project window
2. In Inspector, expand **Holes** list
3. Click **+** to add a new hole
4. Set **Course Name Key**, **Hole Number**, and **Rewards**
5. Don't forget to add the localization key to `LocalizationText.csv`!

**Full guide:** `Assets/Data/README_HOLES.md`

---

## Reward Types Reference

When adding rewards in the database:
- **Type 0** = Points (main currency)
- **Type 1** = Repair Kit (item)
- **Type 2** = Ball (consumable)

In Unity Inspector, it shows as a dropdown (Points/RepairKit/Ball) for easier selection.
