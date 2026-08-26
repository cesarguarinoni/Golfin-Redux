// ─────────────────────────────────────────────────────────────────────────────
// InventorySync — the catalog-default seam.
//
// Spec: Docs/Specs/Active/content_player_inventory/SPEC.md §1 (deltas-from-default)
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using Golfin.Save;

namespace Golfin.InventorySync
{
    /// <summary>
    /// What a club or character looks like the instant a player is granted it, straight from the
    /// catalog.
    ///
    /// <para>
    /// THIS IS WHAT MAKES "A DEFAULT-STATE CLUB IS JUST ITS ID" POSSIBLE, in both directions:
    /// encoding compares the player's row against this and writes only what differs; decoding
    /// starts from this and applies what was written. Both halves need the same answer, so both
    /// halves ask the same object.
    /// </para>
    ///
    /// <para>
    /// AND IT IS WHY A CATALOG REBALANCE PROPAGATES FOR FREE (SPEC §1). A club stored as a bare id
    /// is not "level 1 durability 40" on the wire — it is "whatever the catalog says today". Publish
    /// a new starting level and every untouched instance moves with it on the next decode, with no
    /// migration and no server write. A club the player levelled carries its own level and is
    /// untouched, which is also correct: they earned that.
    /// </para>
    ///
    /// <para>
    /// IT IS AN INTERFACE BECAUSE THE CATALOG IS UNREACHABLE FROM HERE.
    /// <c>ClubDatabaseCSV</c> and <c>CharacterDatabaseCSV</c> live in Assembly-CSharp, which no
    /// asmdef can reference. <c>ClubCatalogSpec</c> in <c>Golfin.Save</c> exists for exactly this
    /// reason and this is the same split: the rules live in an assembly a test can load with no
    /// scene, and Assembly-CSharp supplies the data through a seam
    /// (<c>InventoryCatalogAdapter</c>).
    /// </para>
    /// </summary>
    public interface IInventoryCatalog
    {
        /// <summary>
        /// The state a freshly-granted <paramref name="clubId"/> would be in: catalog starting
        /// level, full durability, seeded SP, UNEQUIPPED (slot 0). Mirrors
        /// <see cref="ClubOwnershipService.MakePersisted"/> with <c>bagSlot = 0</c>, and must keep
        /// mirroring it — the two disagreeing would make a freshly-granted club encode as a delta
        /// against itself.
        /// </summary>
        bool TryGetClubDefault(string clubId, out PersistedClub def);

        /// <summary>
        /// The state a freshly-granted <paramref name="characterId"/> would be in: catalog starting
        /// level, no SP spent, <c>isOwned = true</c>. Owned is the default because the list this
        /// projects from is <c>SaveData.ownedCharacters</c> — a LOCKED character is the unusual row
        /// and pays the bytes for it.
        /// </summary>
        bool TryGetCharacterDefault(string characterId, out PersistedCharacter def);
    }

    /// <summary>
    /// The no-catalog fallback: knows nothing, so nothing compresses.
    ///
    /// <para>
    /// USED WHEN THE REAL CATALOG IS NOT AVAILABLE — an EditMode test with no databases, or a boot
    /// where the sync ran before <c>ClubDatabaseCSV</c> did. Every row then encodes in FULL, which
    /// costs bytes and loses nothing. That is the only acceptable direction to fail in: the
    /// alternative — guessing a default — would silently encode a delta against a number the
    /// catalog never said, and the player's real level would be gone.
    /// </para>
    /// </summary>
    public sealed class EmptyInventoryCatalog : IInventoryCatalog
    {
        public static readonly EmptyInventoryCatalog Instance = new EmptyInventoryCatalog();

        public bool TryGetClubDefault(string clubId, out PersistedClub def)
        {
            def = null!;
            return false;
        }

        public bool TryGetCharacterDefault(string characterId, out PersistedCharacter def)
        {
            def = null!;
            return false;
        }
    }
}
