// ─────────────────────────────────────────────────────────────────────────────
// InventorySync — SaveData ↔ InventorySnapshot.
//
// Spec: Docs/Specs/Active/content_player_inventory/SPEC.md §1, §3
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Golfin.Save;

namespace Golfin.InventorySync
{
    /// <summary>
    /// The two halves of the boundary between the local save and the synced blob.
    ///
    /// <para>
    /// PURE, AND DELIBERATELY SO. No Unity, no <c>SaveDataHost</c>, no network — it takes a
    /// <see cref="SaveData"/> and returns a snapshot, or takes a snapshot and mutates a
    /// <see cref="SaveData"/>. Everything that could destroy a player's inventory lives in these
    /// two methods plus <see cref="InventoryMerge"/>, and all three are EditMode-testable with no
    /// scene, no play mode and no socket.
    /// </para>
    /// </summary>
    public static class InventoryProjector
    {
        /// <summary>
        /// The synced subset of a save. Copies, never references — the caller is going to serialise
        /// this off the main thread's timeline and the save keeps mutating underneath.
        ///
        /// <para>
        /// WHAT IT DOES NOT READ IS THE POINT. <c>rewardPoints</c>, <c>lifetimeRpEarned</c>,
        /// <c>rpDaily/Weekly/Monthly</c>, the period keys and <c>tournamentEntries</c> are all on
        /// <see cref="SaveData"/> and all deliberately skipped: the server owns them (SPEC §1).
        /// <c>playedHoles</c> is skipped as device-local history. Stamina condition is zeroed on the
        /// character rows — see <see cref="InventorySnapshot"/> for why an additive merge on a
        /// regenerating pool is an exploit.
        /// </para>
        /// </summary>
        public static InventorySnapshot Project(SaveData save)
        {
            var snap = new InventorySnapshot();
            if (save == null) return snap;

            if (save.ownedClubs != null)
                foreach (var c in save.ownedClubs)
                {
                    if (c == null || string.IsNullOrEmpty(c.clubId)) continue;
                    snap.Clubs.Add(CloneClub(c));
                }

            if (save.ownedCharacters != null)
                foreach (var c in save.ownedCharacters)
                {
                    if (c == null || string.IsNullOrEmpty(c.characterId)) continue;
                    snap.Characters.Add(CloneCharacter(c));
                }

            if (save.itemQuantities != null)
                foreach (var kv in save.itemQuantities) snap.Items[kv.Key] = kv.Value;

            if (save.ballQuantities != null)
                foreach (var kv in save.ballQuantities) snap.Balls[kv.Key] = kv.Value;

            if (save.ticketBalances != null)
                foreach (var t in save.ticketBalances)
                {
                    if (t == null) continue;
                    snap.Tickets[t.ticketTypeInt] = t.balance;
                }

            if (save.unlockedHoles != null)
                foreach (int h in save.unlockedHoles)
                    if (!snap.UnlockedHoles.Contains(h)) snap.UnlockedHoles.Add(h);

            snap.StarterCharacterId  = save.starterCharacterId ?? "";
            snap.SelectedCharacterId = save.selectedCharacterId ?? "";

            snap.UnlockedHoles.Sort();
            return snap;
        }

        /// <summary>
        /// Fold a snapshot INTO a save, additively.
        ///
        /// <para>
        /// ⚠️ THIS METHOD NEVER SUBTRACTS, AND THAT IS THE LOAD-BEARING PROPERTY OF THE WHOLE
        /// FEATURE (SPEC §3). It adds ids the save lacks, raises levels and quantities, and leaves
        /// everything else alone. It cannot remove a club, lock a character, lower a level, or
        /// reduce a count — there is no code path here that does any of those, by construction and
        /// not by care.
        /// </para>
        /// <para>
        /// Not because a tester's inventory is precious — testers are exactly who this ships to —
        /// but because it is what keeps LOSS DIAGNOSTIC. Under additive merge a missing item is
        /// unambiguously a bug and someone goes and finds it. Under last-write-wins some loss is
        /// correct and some is a bug, they look identical in a bug report, and you cannot tell which
        /// one you are holding. That is worth more during testing, not less, and it is the single
        /// hardest thing to change once real players exist.
        /// </para>
        /// <para>
        /// Subtraction has exactly one home: an explicit server-side spend, which already exists for
        /// RP (<c>spend_pts</c>). Nothing in this file is that.
        /// </para>
        /// <para>
        /// <paramref name="raises"/> COLLECTS THE REFUNDABLE-SPEND PATH (PLAN §6.5 decision 1). Every
        /// quantity this raises on a key the save ALREADY held is appended, because that is the case
        /// where the merge can hand back an item the player consumed — and beta consumption figures
        /// are what tune the economy, so it has to be a count rather than an assumption. Pass null
        /// when the caller does not care; nothing else about the merge changes either way. See
        /// <see cref="InventoryRaise"/> for why a NEW key is deliberately not one of these.
        /// </para>
        /// <returns>True when the save actually changed — the caller uses this to decide whether a
        /// disk write is owed. A no-op restore must not dirty the save, or every boot writes.</returns>
        /// </summary>
        public static bool Apply(InventorySnapshot snap, SaveData save, List<InventoryRaise>? raises = null)
        {
            if (snap == null || save == null) return false;

            bool changed = false;

            // ── Clubs ────────────────────────────────────────────────────────
            save.ownedClubs ??= new List<PersistedClub>();
            var clubsById = new Dictionary<string, PersistedClub>(save.ownedClubs.Count);
            foreach (var c in save.ownedClubs)
                if (c != null && !string.IsNullOrEmpty(c.clubId)) clubsById[c.clubId] = c;

            foreach (var incoming in snap.Clubs)
            {
                if (incoming == null || string.IsNullOrEmpty(incoming.clubId)) continue;

                if (!clubsById.TryGetValue(incoming.clubId, out var mine))
                {
                    // The CLONE goes in the map, not `incoming`. A snapshot should not carry the
                    // same id twice, but if one ever does, indexing the source object would raise
                    // fields on the caller's snapshot and leave the save at the first occurrence.
                    var copy = CloneClub(incoming);
                    save.ownedClubs.Add(copy);
                    clubsById[incoming.clubId] = copy;
                    changed = true;
                    continue;
                }

                changed |= RaiseClub(mine, incoming);
            }

            // ── Characters ───────────────────────────────────────────────────
            save.ownedCharacters ??= new List<PersistedCharacter>();
            var charsById = new Dictionary<string, PersistedCharacter>(save.ownedCharacters.Count);
            foreach (var c in save.ownedCharacters)
                if (c != null && !string.IsNullOrEmpty(c.characterId)) charsById[c.characterId] = c;

            foreach (var incoming in snap.Characters)
            {
                if (incoming == null || string.IsNullOrEmpty(incoming.characterId)) continue;

                if (!charsById.TryGetValue(incoming.characterId, out var mine))
                {
                    // Condition is NOT carried across (see InventorySnapshot). A restored character
                    // arrives with the "never written" sentinel, which the stamina layer hydrates to
                    // a full pool — the same thing a freshly-granted character gets.
                    var copy = CloneCharacter(incoming);
                    copy.conditionEnergy     = 0f;
                    copy.conditionUpdatedUtc = "";
                    save.ownedCharacters.Add(copy);
                    charsById[incoming.characterId] = copy;
                    changed = true;
                    continue;
                }

                changed |= RaiseCharacter(mine, incoming);
            }

            // ── Quantities ───────────────────────────────────────────────────
            save.itemQuantities ??= new Dictionary<string, int>();
            foreach (var kv in snap.Items)
                changed |= RaiseQuantity(save.itemQuantities, kv.Key, kv.Value,
                                         InventoryRaiseKind.Item, raises);

            save.ballQuantities ??= new Dictionary<string, int>();
            foreach (var kv in snap.Balls)
                changed |= RaiseQuantity(save.ballQuantities, kv.Key, kv.Value,
                                         InventoryRaiseKind.Ball, raises);

            save.ticketBalances ??= new List<PersistedTicketBalance>();
            foreach (var kv in snap.Tickets)
            {
                PersistedTicketBalance? mine = null;
                foreach (var t in save.ticketBalances)
                    if (t != null && t.ticketTypeInt == kv.Key) { mine = t; break; }

                if (mine == null)
                {
                    save.ticketBalances.Add(new PersistedTicketBalance
                        { ticketTypeInt = kv.Key, balance = kv.Value });
                    changed = true;
                }
                else
                {
                    int merged = InventoryMerge.MergeQuantity(mine.balance, kv.Value);
                    if (merged != mine.balance)
                    {
                        // The save HELD this ticket type — see InventoryRaise: an existing key
                        // going up is the refund path, a new one is a restore.
                        raises?.Add(new InventoryRaise(InventoryRaiseKind.Ticket,
                            kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            mine.balance, merged));
                        mine.balance = merged;
                        changed = true;
                    }
                }
            }

            // ── Unlocked holes ───────────────────────────────────────────────
            save.unlockedHoles ??= new List<int>();
            foreach (int h in snap.UnlockedHoles)
                if (!save.unlockedHoles.Contains(h)) { save.unlockedHoles.Add(h); changed = true; }

            // ── Scalars ──────────────────────────────────────────────────────
            // FILL-IF-EMPTY, never overwrite. The starter is set once and defines which character a
            // player was locked into; overwriting it from another device would silently relicense
            // their whole roster. The selection is a live preference on THIS device — the blob is
            // only allowed to answer "you have never chosen", which is exactly the fresh-install
            // restore case this exists for.
            if (string.IsNullOrEmpty(save.starterCharacterId) && !string.IsNullOrEmpty(snap.StarterCharacterId))
            {
                save.starterCharacterId = snap.StarterCharacterId;
                changed = true;
            }
            if (string.IsNullOrEmpty(save.selectedCharacterId) && !string.IsNullOrEmpty(snap.SelectedCharacterId))
            {
                save.selectedCharacterId = snap.SelectedCharacterId;
                changed = true;
            }

            return changed;
        }

        // ── Field-level raises ────────────────────────────────────────────────

        private static bool RaiseClub(PersistedClub mine, PersistedClub theirs)
        {
            bool changed = false;
            changed |= Raise(ref mine.currentLevel,       theirs.currentLevel);
            changed |= Raise(ref mine.maxDurability,      theirs.maxDurability);
            changed |= Raise(ref mine.currentDurability,  theirs.currentDurability);
            changed |= Raise(ref mine.totalSPEarned,      theirs.totalSPEarned);
            changed |= Raise(ref mine.spentPower,         theirs.spentPower);
            changed |= Raise(ref mine.spentAccuracy,      theirs.spentAccuracy);
            changed |= Raise(ref mine.spentLieResistance, theirs.spentLieResistance);
            changed |= Raise(ref mine.spentDurability,    theirs.spentDurability);
            // equippedBagSlot is NOT raised. A bag slot is an ARRANGEMENT, not a quantity: taking
            // the max would silently equip a club the player deliberately left out on this device,
            // and there is no "more equipped". A club already here keeps the slot it has here; a
            // club arriving from the blob keeps the slot it arrived with (the Add path above).
            return changed;
        }

        private static bool RaiseCharacter(PersistedCharacter mine, PersistedCharacter theirs)
        {
            bool changed = false;
            changed |= Raise(ref mine.currentLevel,     theirs.currentLevel);
            changed |= Raise(ref mine.totalSPEarned,    theirs.totalSPEarned);
            changed |= Raise(ref mine.spentStrength,    theirs.spentStrength);
            changed |= Raise(ref mine.spentClubControl, theirs.spentClubControl);
            changed |= Raise(ref mine.spentRecovery,    theirs.spentRecovery);
            changed |= Raise(ref mine.spentStamina,     theirs.spentStamina);

            // Ownership is OR, never AND: owning is the additive direction, and a locked row on one
            // device must not re-lock a character unlocked on another.
            if (theirs.isOwned && !mine.isOwned) { mine.isOwned = true; changed = true; }

            // isSelected is not merged per row — the snapshot carries ONE selection at the top
            // level. See InventorySnapshot.SelectedCharacterId.
            return changed;
        }

        private static bool RaiseQuantity(IDictionary<string, int> into, string key, int value,
                                          InventoryRaiseKind kind, List<InventoryRaise>? raises)
        {
            if (string.IsNullOrEmpty(key)) return false;

            // A key this save does not have is a RESTORE, not a refund — deliberately NOT counted.
            if (!into.TryGetValue(key, out int mine)) { into[key] = value; return true; }

            int merged = InventoryMerge.MergeQuantity(mine, value);
            if (merged == mine) return false;

            raises?.Add(new InventoryRaise(kind, key, mine, merged));
            into[key] = merged;
            return true;
        }

        private static bool Raise(ref int mine, int theirs)
        {
            if (theirs <= mine) return false;
            mine = theirs;
            return true;
        }

        // ── Copies ────────────────────────────────────────────────────────────
        //
        // PUBLIC because "a copy at the catalog default" is the unit every other piece works in:
        // InventoryMerge accumulates into one, InventoryCodec re-expands a bare id into one, and
        // InventoryCatalogAdapter hands one out. A second hand-rolled copy helper anywhere would be
        // a field this one remembers and that one forgets.

        public static PersistedClub CloneClub(PersistedClub c) => new PersistedClub
        {
            clubId             = c.clubId,
            currentLevel       = c.currentLevel,
            currentDurability  = c.currentDurability,
            maxDurability      = c.maxDurability,
            equippedBagSlot    = c.equippedBagSlot,
            totalSPEarned      = c.totalSPEarned,
            spentPower         = c.spentPower,
            spentAccuracy      = c.spentAccuracy,
            spentLieResistance = c.spentLieResistance,
            spentDurability    = c.spentDurability,
        };

        /// <summary>A character copy with the stamina condition ZEROED — the pool never rides the
        /// wire (see <see cref="InventorySnapshot"/>).</summary>
        public static PersistedCharacter CloneCharacter(PersistedCharacter c) => new PersistedCharacter
        {
            characterId         = c.characterId,
            currentLevel        = c.currentLevel,
            spentStrength       = c.spentStrength,
            spentClubControl    = c.spentClubControl,
            spentRecovery       = c.spentRecovery,
            spentStamina        = c.spentStamina,
            totalSPEarned       = c.totalSPEarned,
            isOwned             = c.isOwned,
            isSelected          = false,
            conditionEnergy     = 0f,
            conditionUpdatedUtc = "",
        };
    }
}
