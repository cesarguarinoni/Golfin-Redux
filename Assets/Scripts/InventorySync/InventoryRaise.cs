// ─────────────────────────────────────────────────────────────────────────────
// InventorySync — one quantity the additive merge put back up.
//
// Task: content_cleanup_quick item 5 (Docs/TellCode.md)
// Decision of record: Docs/CONTENT_PIPELINE_PLAN.md §6.5 decision 1
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable

namespace Golfin.InventorySync
{
    /// <summary>What kind of stack a <see cref="InventoryRaise"/> is about.</summary>
    public enum InventoryRaiseKind
    {
        Item,
        Ball,
        Ticket,
    }

    /// <summary>
    /// A merge RAISED a quantity the player already held. This is the refundable-spend path, made
    /// countable.
    ///
    /// <para>
    /// WHY THIS TYPE EXISTS (PLAN §6.5 decision 1). The additive merge can restore a consumed
    /// item: device A spends a repair kit (5 → 4) and pushes; device B, holding a stale rev, pushes
    /// 5; <c>max(4, 5)</c> is 5 and the kit is back. RP stays debited, so it is a free consumable
    /// rather than currency duplication, and for testers that is the correct trade — it is the trade
    /// the additive merge was chosen for, because it makes LOSS diagnostic.
    /// </para>
    /// <para>
    /// The cost is not player harm, it is DATA harm. Beta consumption figures are what
    /// <c>ECONOMY_MASTER.md</c> §1 says will tune the economy, and a silent refund path skews
    /// exactly those numbers. So every raise is logged with the player and the item: it turns an
    /// unknown into a count, and that count is what decides whether PLAN §6 step 4d
    /// (server-authoritative spends) stays a launch-gate or moves up. ~0 through the beta and it
    /// stays where it is; anything else and it does not.
    /// </para>
    /// <para>
    /// ⚠️ ONLY A KEY THE SAVE ALREADY HELD COUNTS. A quantity arriving on a key this device does
    /// not have is a RESTORE — a fresh install pulling its inventory back — which is the feature
    /// working, not the refund path, and counting it would bury the signal under every reinstall.
    /// Levels and SP are excluded for the same reason in reverse: nothing consumes them, so a raise
    /// there can never be a refund.
    /// </para>
    /// </summary>
    public readonly struct InventoryRaise
    {
        public InventoryRaise(InventoryRaiseKind kind, string id, int from, int to)
        {
            Kind = kind;
            Id   = id;
            From = from;
            To   = to;
        }

        public InventoryRaiseKind Kind { get; }

        /// <summary>The item / ball id, or the ticket type as a number.</summary>
        public string Id { get; }

        /// <summary>What this device held before the merge — the post-spend figure.</summary>
        public int From { get; }

        /// <summary>What the merge left. <b>-1 is the UNLIMITED sentinel</b> on balls, so this is
        /// not always greater than <see cref="From"/> numerically.</summary>
        public int To { get; }

        public override string ToString() => $"{Kind}:{Id} {From}->{To}";
    }
}
