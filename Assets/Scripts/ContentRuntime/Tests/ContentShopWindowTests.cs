// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Content.Tests — ContentShopWindowTests
//
// SPEC §6. The columns have shipped in shop_catalog.csv since
// content_panels_gaps and the client has never read them, so every one of these
// cases is a window an operator could already have authored and watched be
// ignored.
//
// The clock is a parameter, so the whole matrix runs in milliseconds and none
// of it depends on what day it is.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using Golfin.Content;
using NUnit.Framework;

namespace Golfin.Content.Tests
{
    [TestFixture]
    public class ContentShopWindowTests
    {
        private static readonly DateTime Now =
            new DateTime(2026, 08, 26, 12, 00, 00, DateTimeKind.Utc);

        private static ShopWindowVerdict Eval(string? start = null, string? end = null,
                                              string? saleStart = null, string? saleEnd = null,
                                              DateTime? now = null)
            => ContentShopWindow.Evaluate(new ShopWindowSpec(start, end, saleStart, saleEnd),
                                          now ?? Now);

        // ── Unbounded ─────────────────────────────────────────────────────────

        [Test]
        public void NoBoundsAtAll_IsListedAndOnSale()
        {
            var v = Eval();
            Assert.IsTrue(v.Listed, "the common case — a row with no schedule is always listed");
            Assert.IsTrue(v.OnSale, "…and its sale price, if any, applies");
        }

        [Test]
        public void BlankBounds_AreTreatedAsAbsent_NotAsGarbage()
        {
            // Every row in the shipped shop_catalog.csv has four EMPTY scheduling cells. If blank
            // read as unparseable, this rule would empty the entire store on the first launch.
            var v = Eval("", "  ", "", "");
            Assert.IsTrue(v.Listed, "blank means unbounded; it must never fail closed");
            Assert.IsTrue(v.OnSale);
        }

        // ── Listing window ────────────────────────────────────────────────────

        [Test]
        public void FutureStartAt_IsHidden()
        {
            var v = Eval(start: "2026-09-01T00:00:00Z");
            Assert.IsFalse(v.Listed);
            StringAssert.Contains("future", v.Reason);
        }

        [Test]
        public void PastEndAt_IsHidden()
        {
            var v = Eval(end: "2026-08-01T00:00:00Z");
            Assert.IsFalse(v.Listed);
            StringAssert.Contains("passed", v.Reason);
        }

        [Test]
        public void StartAt_IsINCLUSIVE_AtTheExactInstant()
        {
            var v = Eval(start: "2026-08-26T12:00:00Z");
            Assert.IsTrue(v.Listed,
                "a row starting exactly now is live now — startAt is the inclusive edge");
        }

        [Test]
        public void EndAt_IsEXCLUSIVE_AtTheExactInstant()
        {
            // The half-open interval. A row with endAt = midnight is gone AT midnight, not one
            // second later, so two rows scheduled back-to-back never overlap for a second.
            var v = Eval(end: "2026-08-26T12:00:00Z");
            Assert.IsFalse(v.Listed,
                "a row ending exactly now is already gone — endAt is the EXCLUSIVE edge");
        }

        [Test]
        public void InsideTheWindow_IsListed()
        {
            var v = Eval(start: "2026-08-01T00:00:00Z", end: "2026-09-01T00:00:00Z");
            Assert.IsTrue(v.Listed);
        }

        // ── Sale window (rule 2: a sale discounts a listing, it never IS one) ──

        [Test]
        public void OutsideTheSaleWindow_TheRowStaysListedButTheSalePriceIsIgnored()
        {
            var v = Eval(saleStart: "2026-01-01T00:00:00Z", saleEnd: "2026-02-01T00:00:00Z");
            Assert.IsTrue(v.Listed,
                "an EXPIRED SALE MUST NOT REMOVE THE PRODUCT — it only removes the discount");
            Assert.IsFalse(v.OnSale);
        }

        [Test]
        public void BeforeTheSaleWindow_TheRowIsListedAtListPrice()
        {
            var v = Eval(saleStart: "2026-12-01T00:00:00Z");
            Assert.IsTrue(v.Listed);
            Assert.IsFalse(v.OnSale);
        }

        [Test]
        public void InsideTheSaleWindow_TheSalePriceApplies()
        {
            var v = Eval(saleStart: "2026-08-01T00:00:00Z", saleEnd: "2026-09-01T00:00:00Z");
            Assert.IsTrue(v.Listed);
            Assert.IsTrue(v.OnSale);
        }

        [Test]
        public void SaleEndAt_IsEXCLUSIVE_LikeEndAt()
        {
            var v = Eval(saleEnd: "2026-08-26T12:00:00Z");
            Assert.IsTrue(v.Listed);
            Assert.IsFalse(v.OnSale, "the sale is over AT saleEndAt, matching endAt's edge rule");
        }

        // ── Rule 3: fail closed on a present-but-unparseable bound ────────────

        [Test]
        public void UnparseableStartAt_DropsTheRow()
        {
            var v = Eval(start: "next tuesday");
            Assert.IsFalse(v.Listed,
                "fail closed — a fat-fingered date must not become a permanently-live product");
            StringAssert.Contains("unparseable", v.Reason);
        }

        [Test]
        public void UnparseableEndAt_DropsTheRow()
        {
            var v = Eval(end: "2026-13-45");
            Assert.IsFalse(v.Listed);
            StringAssert.Contains("unparseable", v.Reason);
        }

        [Test]
        public void UnparseableSaleBound_AlsoDropsTheRow()
        {
            // Deliberately NOT "ignore the sale and keep selling at list price": an unparseable sale
            // bound means the operator's intent for this row's PRICE is unknown, and charging an
            // unintended price is the worse of the two failures.
            var v = Eval(saleStart: "soon");
            Assert.IsFalse(v.Listed);
            StringAssert.Contains("unparseable", v.Reason);

            var v2 = Eval(saleEnd: "whenever");
            Assert.IsFalse(v2.Listed);
            StringAssert.Contains("unparseable", v2.Reason);
        }

        // ── Timezone handling ─────────────────────────────────────────────────

        [Test]
        public void ABoundWithNoZone_IsReadAsUTC_NotAsDeviceLocalTime()
        {
            // A phone in JST must not see a different shop than one in UTC. AssumeUniversal is what
            // guarantees that; without it this test passes in London and fails in Tokyo.
            Assert.IsTrue(ContentShopWindow.TryBound("2026-08-26T18:00:00", out DateTime? utc));
            Assert.IsTrue(utc.HasValue);
            Assert.AreEqual(DateTimeKind.Utc, utc!.Value.Kind);
            Assert.AreEqual(18, utc.Value.Hour, "no zone means UTC, not local");
        }

        [Test]
        public void AnOffsetBound_IsConvertedToUTC()
        {
            Assert.IsTrue(ContentShopWindow.TryBound("2026-08-26T21:00:00+09:00", out DateTime? utc));
            Assert.AreEqual(12, utc!.Value.Hour, "+09:00 21:00 is 12:00 UTC");
        }

        [Test]
        public void TryBound_ReportsAbsentAsSuccessAndGarbageAsFailure()
        {
            Assert.IsTrue(ContentShopWindow.TryBound(null, out DateTime? a));
            Assert.IsNull(a, "absent parses successfully, to 'unbounded'");

            Assert.IsFalse(ContentShopWindow.TryBound("garbage", out DateTime? b));
            Assert.IsNull(b);
        }

        // ── Combination: the case that motivated rule 2 ───────────────────────

        [Test]
        public void ALiveRowWithAnExpiredSale_SellsAtListPrice()
        {
            var v = Eval(start:     "2026-08-01T00:00:00Z",
                         end:       "2026-12-01T00:00:00Z",
                         saleStart: "2026-08-01T00:00:00Z",
                         saleEnd:   "2026-08-08T00:00:00Z");

            Assert.IsTrue(v.Listed, "the listing outlives the sale");
            Assert.IsFalse(v.OnSale, "and the discount does not linger past its window");
        }
    }
}
