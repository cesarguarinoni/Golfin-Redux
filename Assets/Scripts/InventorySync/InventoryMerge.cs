// ─────────────────────────────────────────────────────────────────────────────
// InventorySync — the additive merge.
//
// Spec: Docs/Specs/Active/content_player_inventory/SPEC.md §3
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Golfin.Save;

namespace Golfin.InventorySync
{
    /// <summary>
    /// Fold two snapshots into one, taking the union of what either side has.
    ///
    /// <para>
    /// THIS IS THE ~30-LINES-INSTEAD-OF-~5 THE SPEC BUDGETED FOR (§3), and the reason is not
    /// sentimentality about tester data. It is that additive merge makes LOSS DIAGNOSTIC. If the
    /// only merge rule is "nothing is ever removed", then a missing item is unambiguously a bug and
    /// someone goes and finds it. Under last-write-wins, some loss is the correct outcome of two
    /// devices and some loss is a bug — they are indistinguishable in a bug report, so every report
    /// costs an investigation that usually ends in a shrug. That is worth MORE while a build is
    /// being tested, not less, and it is the hardest thing to retrofit once real players exist.
    /// </para>
    ///
    /// <para>
    /// The cost is real and bounded: a merge can lose a purchase's TIMING (two devices, one blob) but
    /// never a player's PROPERTY. Subtraction happens only through an explicit server-side spend,
    /// which already exists for RP.
    /// </para>
    /// </summary>
    public static class InventoryMerge
    {
        /// <summary>
        /// <paramref name="mine"/> ∪ <paramref name="theirs"/>. Neither input is mutated.
        ///
        /// <para>
        /// <paramref name="mine"/> is the local device and wins every genuine TIE-BREAK (bag slot,
        /// starter, selection) — not because it is more correct, but because those are the fields
        /// where "more" is meaningless and the local device is the one the player is looking at.
        /// </para>
        /// </summary>
        public static InventorySnapshot Additive(InventorySnapshot? mine, InventorySnapshot? theirs)
        {
            var outSnap = new InventorySnapshot();
            mine ??= new InventorySnapshot();
            theirs ??= new InventorySnapshot();

            // ── Clubs ────────────────────────────────────────────────────────
            var clubs = new Dictionary<string, PersistedClub>();
            var clubOrder = new List<string>();
            foreach (var c in mine.Clubs)
            {
                if (c == null || string.IsNullOrEmpty(c.clubId)) continue;
                if (!clubs.ContainsKey(c.clubId)) clubOrder.Add(c.clubId);
                clubs[c.clubId] = InventoryProjector.CloneClub(c);
            }
            foreach (var c in theirs.Clubs)
            {
                if (c == null || string.IsNullOrEmpty(c.clubId)) continue;
                if (!clubs.TryGetValue(c.clubId, out var acc))
                {
                    clubOrder.Add(c.clubId);
                    clubs[c.clubId] = InventoryProjector.CloneClub(c);
                    continue;
                }
                acc.currentLevel       = Max(acc.currentLevel,       c.currentLevel);
                acc.maxDurability      = Max(acc.maxDurability,      c.maxDurability);
                // "keep the higher durability" (SPEC §3) — a repair on either device survives.
                acc.currentDurability  = Max(acc.currentDurability,  c.currentDurability);
                acc.totalSPEarned      = Max(acc.totalSPEarned,      c.totalSPEarned);
                acc.spentPower         = Max(acc.spentPower,         c.spentPower);
                acc.spentAccuracy      = Max(acc.spentAccuracy,      c.spentAccuracy);
                acc.spentLieResistance = Max(acc.spentLieResistance, c.spentLieResistance);
                acc.spentDurability    = Max(acc.spentDurability,    c.spentDurability);
                // equippedBagSlot: MINE wins. A bag slot is an arrangement, not a quantity — see
                // InventoryProjector.RaiseClub.
            }
            foreach (var id in clubOrder) outSnap.Clubs.Add(clubs[id]);

            // ── Characters ───────────────────────────────────────────────────
            var chars = new Dictionary<string, PersistedCharacter>();
            var charOrder = new List<string>();
            foreach (var c in mine.Characters)
            {
                if (c == null || string.IsNullOrEmpty(c.characterId)) continue;
                if (!chars.ContainsKey(c.characterId)) charOrder.Add(c.characterId);
                chars[c.characterId] = InventoryProjector.CloneCharacter(c);
            }
            foreach (var c in theirs.Characters)
            {
                if (c == null || string.IsNullOrEmpty(c.characterId)) continue;
                if (!chars.TryGetValue(c.characterId, out var acc))
                {
                    charOrder.Add(c.characterId);
                    chars[c.characterId] = InventoryProjector.CloneCharacter(c);
                    continue;
                }
                acc.currentLevel     = Max(acc.currentLevel,     c.currentLevel);
                acc.totalSPEarned    = Max(acc.totalSPEarned,    c.totalSPEarned);
                acc.spentStrength    = Max(acc.spentStrength,    c.spentStrength);
                acc.spentClubControl = Max(acc.spentClubControl, c.spentClubControl);
                acc.spentRecovery    = Max(acc.spentRecovery,    c.spentRecovery);
                acc.spentStamina     = Max(acc.spentStamina,     c.spentStamina);
                acc.isOwned          = acc.isOwned || c.isOwned;   // OR, never AND
            }
            foreach (var id in charOrder) outSnap.Characters.Add(chars[id]);

            // ── Quantities ───────────────────────────────────────────────────
            MergeInto(outSnap.Items,   mine.Items,   theirs.Items);
            MergeInto(outSnap.Balls,   mine.Balls,   theirs.Balls);
            MergeInto(outSnap.Tickets, mine.Tickets, theirs.Tickets);

            // ── Unlocked holes ───────────────────────────────────────────────
            foreach (int h in mine.UnlockedHoles)
                if (!outSnap.UnlockedHoles.Contains(h)) outSnap.UnlockedHoles.Add(h);
            foreach (int h in theirs.UnlockedHoles)
                if (!outSnap.UnlockedHoles.Contains(h)) outSnap.UnlockedHoles.Add(h);
            outSnap.UnlockedHoles.Sort();

            // ── Scalars: mine, unless mine is empty ──────────────────────────
            outSnap.StarterCharacterId = string.IsNullOrEmpty(mine.StarterCharacterId)
                ? theirs.StarterCharacterId : mine.StarterCharacterId;
            outSnap.SelectedCharacterId = string.IsNullOrEmpty(mine.SelectedCharacterId)
                ? theirs.SelectedCharacterId : mine.SelectedCharacterId;

            return outSnap;
        }

        /// <summary>
        /// Merge one quantity with another. Normally the max — but <b>-1 is the "unlimited"
        /// sentinel</b> on <c>ballQuantities</c>, and <c>Math.Max(-1, 5)</c> is 5, which would
        /// quietly turn an unlimited ball into a stack of five. Unlimited beats every finite count,
        /// so it is checked before the max, not after.
        /// </summary>
        public static int MergeQuantity(int mine, int theirs)
        {
            if (mine < 0 || theirs < 0) return -1;
            return mine >= theirs ? mine : theirs;
        }

        private static void MergeInto<TKey>(
            IDictionary<TKey, int> into, IDictionary<TKey, int> mine, IDictionary<TKey, int> theirs)
        {
            foreach (var kv in mine) into[kv.Key] = kv.Value;
            foreach (var kv in theirs)
                into[kv.Key] = into.TryGetValue(kv.Key, out int have)
                    ? MergeQuantity(have, kv.Value)
                    : kv.Value;
        }

        private static int Max(int a, int b) => a >= b ? a : b;
    }
}
