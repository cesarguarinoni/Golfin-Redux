// ─────────────────────────────────────────────────────────────────────────────
// ContentRuntime — ContentShopWindow  (SPEC §6)
//
// shop_catalog carries startAt / endAt / saleStartAt / saleEndAt (added by
// content_panels_gaps). The columns have shipped in the CSV since then and the
// client has never read them, so every window an operator has authored so far
// has been silently ignored.
//
// THREE RULES, and each one is a decision rather than an obvious default:
//
//   1. endAt is EXCLUSIVE.  A row with endAt = 2026-09-01T00:00:00Z is gone at
//      exactly midnight, not one second later. startAt is INCLUSIVE. This is
//      the half-open interval every scheduling system should use and the one
//      the dashboard's validator already assumes.
//
//   2. Outside the sale window the sale price is IGNORED — the row still sells
//      at rpCost. A sale window is a discount on a listing, never a listing of
//      its own, so an expired sale must not remove a product from the store.
//
//   3. A present-but-unparseable bound DROPS THE ROW. Fail closed, matching
//      routers/notices.py `_parse` and the dashboard's own validator. The
//      alternative — treat garbage as "no bound" — silently converts a
//      fat-fingered date into a permanently-live product, which is the exact
//      shape of the incident this rule exists to prevent. An ABSENT bound is
//      not garbage: it means unbounded, and that is the common case.
//
// Pure and clock-injected: every decision is a function of (row, nowUtc), so
// the whole matrix is an EditMode test rather than something you find out about
// on a Tuesday.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Globalization;

namespace Golfin.Content
{
    /// <summary>The four scheduling columns of one shop_catalog row, as authored.</summary>
    public readonly struct ShopWindowSpec
    {
        public readonly string? StartAt;
        public readonly string? EndAt;
        public readonly string? SaleStartAt;
        public readonly string? SaleEndAt;

        public ShopWindowSpec(string? startAt, string? endAt, string? saleStartAt, string? saleEndAt)
        {
            StartAt     = startAt;
            EndAt       = endAt;
            SaleStartAt = saleStartAt;
            SaleEndAt   = saleEndAt;
        }
    }

    /// <summary>What the window says about one row, right now.</summary>
    public readonly struct ShopWindowVerdict
    {
        /// <summary>False when the row must not appear in the store at all.</summary>
        public readonly bool Listed;

        /// <summary>True when the row's saleRpCost may be charged. Only meaningful when Listed.</summary>
        public readonly bool OnSale;

        /// <summary>Why the row was dropped, for the log. Empty when Listed.</summary>
        public readonly string Reason;

        public ShopWindowVerdict(bool listed, bool onSale, string reason)
        {
            Listed = listed;
            OnSale = onSale;
            Reason = reason;
        }
    }

    public static class ContentShopWindow
    {
        /// <summary>
        /// Evaluate one row's four bounds against <paramref name="nowUtc"/>.
        /// <para>See the file header — the three rules are the whole contract.</para>
        /// </summary>
        public static ShopWindowVerdict Evaluate(ShopWindowSpec spec, DateTime nowUtc)
        {
            // ── Listing window. A bad bound here drops the row (rule 3). ──────
            if (!TryBound(spec.StartAt, out DateTime? startAt))
                return new ShopWindowVerdict(false, false,
                    $"startAt '{spec.StartAt}' is present but unparseable — failing closed");

            if (!TryBound(spec.EndAt, out DateTime? endAt))
                return new ShopWindowVerdict(false, false,
                    $"endAt '{spec.EndAt}' is present but unparseable — failing closed");

            if (startAt.HasValue && nowUtc < startAt.Value)
                return new ShopWindowVerdict(false, false, $"startAt {Iso(startAt.Value)} is in the future");

            // EXCLUSIVE: at exactly endAt the row is already gone.
            if (endAt.HasValue && nowUtc >= endAt.Value)
                return new ShopWindowVerdict(false, false, $"endAt {Iso(endAt.Value)} has passed (exclusive)");

            // ── Sale window. A bad bound here drops the row too. ──────────────
            //
            // Deliberately NOT "ignore the sale and keep selling at list price": an unparseable
            // sale bound means the operator's intent for this row's PRICE is unknown, and charging
            // an unintended price is the worse of the two failures. It is the same fail-closed
            // reading rule 3 applies to the listing bounds, applied to the money.
            if (!TryBound(spec.SaleStartAt, out DateTime? saleStartAt))
                return new ShopWindowVerdict(false, false,
                    $"saleStartAt '{spec.SaleStartAt}' is present but unparseable — failing closed");

            if (!TryBound(spec.SaleEndAt, out DateTime? saleEndAt))
                return new ShopWindowVerdict(false, false,
                    $"saleEndAt '{spec.SaleEndAt}' is present but unparseable — failing closed");

            bool onSale = (!saleStartAt.HasValue || nowUtc >= saleStartAt.Value) &&
                          (!saleEndAt.HasValue   || nowUtc <  saleEndAt.Value);

            return new ShopWindowVerdict(true, onSale, string.Empty);
        }

        /// <summary>
        /// Parse one bound. Returns FALSE only for a present-but-unparseable value; an absent or
        /// blank bound is a successful parse of "unbounded" (null).
        /// </summary>
        public static bool TryBound(string? raw, out DateTime? utc)
        {
            utc = null;
            if (string.IsNullOrWhiteSpace(raw)) return true;   // absent == unbounded, not an error

            // AdjustToUniversal + AssumeUniversal: an ISO-8601 string with a Z or an offset is
            // converted, and one WITHOUT a zone is read as UTC rather than as the device's local
            // time. A phone in JST must not see a different shop window than one in UTC.
            if (DateTime.TryParse(raw!.Trim(), CultureInfo.InvariantCulture,
                                  DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                                  out DateTime parsed))
            {
                utc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                return true;
            }

            return false;
        }

        private static string Iso(DateTime utc) =>
            utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }
}
