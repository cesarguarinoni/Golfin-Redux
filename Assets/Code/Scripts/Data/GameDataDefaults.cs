using UnityEngine;

/// <summary>
/// Reference document for default game data values.
/// Use these to create ScriptableObject assets in Unity:
///   Right-click in Project → Create → GOLFIN → Club/Ball/Character Data
///
/// All stats are on a 0-100 scale.
/// Upgrade formula: stat = base + perLevel * (level - 1)
/// XP formula: xpBase * level^xpExponent
/// </summary>
public static class GameDataDefaults
{
    // ═══════════════════════════════════════════════════════════════
    // CLUBS (5 clubs, one per type)
    // ═══════════════════════════════════════════════════════════════
    //
    // ┌─────────────┬────────┬──────────┬─────────┬──────────┬─────────┐
    // │ Club        │ Type   │ Rarity   │ Power   │ Accuracy │ Control │
    // ├─────────────┼────────┼──────────┼─────────┼──────────┼─────────┤
    // │ Iron Eagle  │ Driver │ Common   │ 75      │ 35       │ 30      │
    // │ Fairway Fox │ Wood   │ Common   │ 55      │ 50       │ 45      │
    // │ Steel Viper │ Iron   │ Uncommon │ 45      │ 60       │ 55      │
    // │ Sand Shark  │ Wedge  │ Uncommon │ 30      │ 55       │ 70      │
    // │ Silk Touch  │ Putter │ Common   │ 15      │ 70       │ 75      │
    // └─────────────┴────────┴──────────┴─────────┴──────────┴─────────┘
    //
    // Upgrade path per level:
    // ┌─────────────┬───────────┬────────────────┬─────────────────┬──────────┐
    // │ Club        │ MaxLevel  │ Power/Lvl      │ Accuracy/Lvl    │ Ctrl/Lvl │
    // ├─────────────┼───────────┼────────────────┼─────────────────┼──────────┤
    // │ Iron Eagle  │ 10        │ +4.0           │ +2.0            │ +1.5     │
    // │ Fairway Fox │ 10        │ +3.0           │ +3.0            │ +2.5     │
    // │ Steel Viper │ 12        │ +2.5           │ +3.5            │ +3.0     │
    // │ Sand Shark  │ 12        │ +1.5           │ +3.0            │ +4.0     │
    // │ Silk Touch  │ 10        │ +0.5           │ +4.0            │ +4.5     │
    // └─────────────┴───────────┴────────────────┴─────────────────┴──────────┘
    //
    // At max level:
    //   Iron Eagle  → Power 111, Accuracy 53,  Control 43.5
    //   Fairway Fox → Power 82,  Accuracy 77,  Control 67.5
    //   Steel Viper → Power 72.5,Accuracy 98.5,Control 88
    //   Sand Shark  → Power 46.5,Accuracy 88,  Control 114 (capped display at 100)
    //   Silk Touch  → Power 19.5,Accuracy 106, Control 115.5 (capped display at 100)
    //
    // XP per level: 100 * level^1.5
    //   L1→2: 100, L2→3: 283, L3→4: 520, L5→6: 894, L9→10: 2700

    // ═══════════════════════════════════════════════════════════════
    // BALLS (5 balls)
    // ═══════════════════════════════════════════════════════════════
    //
    // ┌──────────────┬──────────┬──────────┬──────┬────────────┐
    // │ Ball         │ Rarity   │ Distance │ Spin │ Wind Resist│
    // ├──────────────┼──────────┼──────────┼──────┼────────────┤
    // │ Tour Basic   │ Common   │ 40       │ 40   │ 40         │
    // │ Pro Spin     │ Uncommon │ 35       │ 65   │ 30         │
    // │ Long Drive   │ Uncommon │ 70       │ 25   │ 35         │
    // │ Wind Cutter  │ Rare     │ 45       │ 40   │ 70         │
    // │ Premium Tour │ Legendary│ 60       │ 55   │ 55         │
    // └──────────────┴──────────┴──────────┴──────┴────────────┘
    //
    // MaxLevel: 8 for all balls
    // Per level: Distance +2, Spin +2.5, WindResist +2
    // XP: 80 * level^1.4

    // ═══════════════════════════════════════════════════════════════
    // CHARACTERS (4 characters)
    // ═══════════════════════════════════════════════════════════════
    //
    // ┌───────────────┬──────────┬────────────┬──────────────┬──────────────────┐
    // │ Character     │ Rarity   │ Power Bonus│ Accuracy Bns │ Special Ability  │
    // ├───────────────┼──────────┼────────────┼──────────────┼──────────────────┤
    // │ Alex (M)      │ Common   │ 3%         │ 3%           │ PowerDrive       │
    // │ Sakura (F)    │ Common   │ 2%         │ 5%           │ PrecisionPutt    │
    // │ Diego (M)     │ Uncommon │ 5%         │ 2%           │ WindMaster       │
    // │ Yuki (F)      │ Rare     │ 4%         │ 4%           │ SpinWizard       │
    // └───────────────┴──────────┴────────────┴──────────────┴──────────────────┘
    //
    // MaxLevel: 15 for all characters
    // Per level: PowerBonus +0.5%, AccuracyBonus +0.4%, Special +2
    // XP: 150 * level^1.6
    //
    // At max level:
    //   Alex   → Power +10%, Accuracy +8.6%, Special 78
    //   Sakura → Power +9%,  Accuracy +10.6%, Special 78
    //   Diego  → Power +12%, Accuracy +7.6%, Special 78
    //   Yuki   → Power +11%, Accuracy +9.6%, Special 78

    // ═══════════════════════════════════════════════════════════════
    // DESIGN NOTES
    // ═══════════════════════════════════════════════════════════════
    //
    // Philosophy:
    //   - No gacha. All items acquired through gameplay progression.
    //   - Clear upgrade paths: play → earn XP → level up items.
    //   - Each item type has a distinct role:
    //       Clubs = primary gameplay stats (per shot)
    //       Balls = passive modifiers (per round)
    //       Characters = % bonuses + unique abilities
    //   - 3 stats per item type keeps UI clean and decisions simple.
    //   - Rarity affects: starting stats, max potential, visual flair.
    //     NOT behind paywall — earn through tournaments/challenges.
    //
    // Acquisition (TBD — to be designed):
    //   - Starter set: 5 Common clubs + 1 ball + 1 character
    //   - Tournament rewards
    //   - Season pass milestones
    //   - Achievement unlocks
    //   - Daily challenges
    //
    // Balancing:
    //   - Driver is high power, low accuracy → risk/reward on tee shots
    //   - Putter is high accuracy/control → reliable on greens
    //   - Characters give small % bonuses (2-12%) — impactful but not game-breaking
    //   - Ball choice is strategic per-course (windy course → Wind Cutter)
}
