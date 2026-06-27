// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — ItemRewardServiceAdapter
// Production adapter for IItemRewardService.
// Resolves SaveDataHost.Instance lazily per-call (never cached in a field).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using Golfin.Save;
using UnityEngine;

namespace Golfin.Tournaments
{
    /// <summary>
    /// Production <see cref="IItemRewardService"/> that writes directly to
    /// <c>SaveDataHost.Instance.Data.itemQuantities</c> and calls
    /// <c>SaveDataHost.Instance.MarkDirty()</c> after every mutation.
    /// <para>
    /// Resolves <c>SaveDataHost.Instance</c> lazily per-call — never cached.
    /// </para>
    /// </summary>
    public sealed class ItemRewardServiceAdapter : IItemRewardService
    {
        /// <inheritdoc/>
        public void Grant(string itemId, int qty)
        {
            // Guard: no-op on zero/negative qty
            if (qty <= 0)
            {
                if (qty < 0)
                    Debug.LogWarning($"[ItemRewardServiceAdapter] Grant called with negative qty={qty} for '{itemId}' — ignoring.");
                return;
            }

            // Guard: no-op on null/empty itemId
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.LogWarning("[ItemRewardServiceAdapter] Grant called with null or empty itemId — ignoring.");
                return;
            }

            var host = SaveDataHost.Instance;
            if (host == null)
            {
                Debug.LogError("[ItemRewardServiceAdapter] SaveDataHost.Instance is null — cannot grant item.");
                return;
            }

            var d = host.Data.itemQuantities;
            d[itemId] = (d.TryGetValue(itemId, out var n) ? n : 0) + qty;
            host.MarkDirty();

            Debug.Log($"[ItemRewardServiceAdapter] Granted {qty}x '{itemId}' (new qty={d[itemId]})");
        }
    }
}
