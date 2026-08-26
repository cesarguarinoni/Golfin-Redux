// ─────────────────────────────────────────────────────────────────────────────
// InventorySync — the in-memory shape of one player's synced inventory.
//
// Spec: Docs/Specs/Active/content_player_inventory/SPEC.md §1
// Plan: Docs/CONTENT_PIPELINE_PLAN.md §6
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Golfin.Save;

namespace Golfin.InventorySync
{
    /// <summary>
    /// Everything that moves server-side, and nothing else.
    ///
    /// <para>
    /// THE THREE-WAY SPLIT (SPEC §1) IS THE WHOLE DESIGN, so it is worth stating here rather than
    /// only in the doc:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Moves</b> — the fields on this class. Property the player accumulated: clubs,
    /// characters, items, balls, tickets, unlocked holes, and which character they picked.</item>
    /// <item><b>Stays server-owned</b> — RP balance, <c>lifetimeRpEarned</c>, the daily/weekly/
    /// monthly accumulators, <c>tournamentEntries</c>. The server ALREADY holds all of these
    /// authoritatively (points_transactions, the leaderboard tables, tournament_entries). A copy
    /// here would be a second, writable, lower-trust source for a number that already has one.
    /// There is no field for them on this class and there must never be; the endpoint strips them
    /// too, so the mistake is impossible on both sides rather than merely documented on one.</item>
    /// <item><b>Stays device-local</b> — language, audio, UI state, <c>playedHoles</c>. Preference
    /// and history, not property; syncing them would make a second device overwrite settings the
    /// player set on purpose ON that device.</item>
    /// </list>
    ///
    /// <para>
    /// STAMINA CONDITION IS DELIBERATELY ABSENT from the character rows even though
    /// <see cref="PersistedCharacter"/> carries it. SPEC §1 moves "ownedCharacters (level, SP,
    /// allocation)" — condition is none of the three. It is a time-regenerating pool, so an
    /// ADDITIVE merge on it (take the max) would hand a player a free refill every time they
    /// touched a second device, which is a live economy exploit dressed as a sync rule.
    /// <see cref="InventoryProjector"/> zeroes it on the way out and never writes it on the way in.
    /// </para>
    /// </summary>
    public sealed class InventorySnapshot
    {
        /// <summary>Owned clubs, full state. The delta-from-default compression happens in
        /// <see cref="InventoryCodec"/>, on the wire — never here, so every rule in
        /// <see cref="InventoryMerge"/> compares like with like.</summary>
        public List<PersistedClub> Clubs = new List<PersistedClub>();

        /// <summary>Characters the save carries a record for. <c>isOwned=false</c> rows are
        /// included: post-v10 a locked character can still hold progress, and dropping the row
        /// would be a subtraction.</summary>
        public List<PersistedCharacter> Characters = new List<PersistedCharacter>();

        /// <summary>itemId → quantity.</summary>
        public Dictionary<string, int> Items = new Dictionary<string, int>();

        /// <summary>ballId → quantity. <b>-1 means UNLIMITED</b>, which is why the merge cannot
        /// simply take the numeric max — see <see cref="InventoryMerge.MergeQuantity"/>.</summary>
        public Dictionary<string, int> Balls = new Dictionary<string, int>();

        /// <summary>(int)TicketType → balance.</summary>
        public Dictionary<int, int> Tickets = new Dictionary<int, int>();

        public List<int> UnlockedHoles = new List<int>();

        /// <summary>Set once, at starter selection. Never overwritten by a merge — see
        /// <see cref="InventoryMerge"/>.</summary>
        public string StarterCharacterId = "";

        /// <summary>The single selected character. Stored ONCE at the top level rather than as an
        /// <c>isSelected</c> flag per row: two devices each flagging their own choice would produce
        /// a snapshot with two selected characters, and a union-of-flags merge has no way to pick.
        /// One scalar has exactly one answer.</summary>
        public string SelectedCharacterId = "";
    }
}
