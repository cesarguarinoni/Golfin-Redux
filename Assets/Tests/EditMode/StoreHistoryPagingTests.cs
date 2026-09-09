// Assets/Tests/EditMode/StoreHistoryPagingTests.cs
// store_history §4 — the Store History paging decisions, tested on the SHIPPING seams.
//
// The clone of GachaHistoryPagingTests, for the controller that is a clone of
// GachaHistoryScreenController. It exists SEPARATELY rather than being parameterised over both
// because the two controllers are separate on purpose (SPEC §4: "Copy, don't generalise") — a
// shared test would quietly re-couple what the spec deliberately decoupled, and the day one
// controller's paging changes it would fail for the other.
//
// The two decisions the paging can get wrong are arithmetic (which slice is the next page) and
// identity (is this a purchase landing on top, or a replaced log). Both are static and
// record-typed precisely so they can be checked here without a scene, a ScrollRect or a play
// session.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode. StoreHistoryScreenController and StoreHistoryRecord live
// in Assembly-CSharp, which an asmdef cannot reference, so every production call goes through
// System.Reflection — the same pattern and the same reason as the gacha suite
// (feedback_tests_must_target_production_type: the seam under test must be the SHIPPING one).
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class StoreHistoryPagingTests
    {
        // ── Reflection handles ────────────────────────────────────────────────

        private static readonly Type ControllerType =
            Type.GetType("GolfinRedux.UI.Shop.StoreHistoryScreenController, Assembly-CSharp");
        private static readonly Type RecordType =
            Type.GetType("GolfinRedux.UI.Shop.StoreHistoryRecord, Assembly-CSharp");
        private static readonly Type RowType =
            Type.GetType("GolfinRedux.UI.Shop.StoreHistoryRow, Assembly-CSharp");
        private static readonly Type StoreType =
            Type.GetType("GolfinRedux.UI.Shop.StoreHistoryStore, Assembly-CSharp");

        private const BindingFlags Statics =
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

        [Test]
        public void Production_types_exist_in_AssemblyCSharp()
        {
            // If this fails, every other test here would pass vacuously by never reaching the
            // production code at all — which is the failure mode this project has hit before.
            Assert.IsNotNull(ControllerType, "StoreHistoryScreenController not found");
            Assert.IsNotNull(RecordType, "StoreHistoryRecord not found");
            Assert.IsNotNull(RowType, "StoreHistoryRow not found");
            Assert.IsNotNull(StoreType, "StoreHistoryStore not found");
            Assert.IsNotNull(ControllerType.GetMethod("NextPageEnd", Statics), "NextPageEnd missing");
            Assert.IsNotNull(ControllerType.GetMethod("PrependCount", Statics), "PrependCount missing");
            Assert.IsNotNull(StoreType.GetMethod("Map", Statics), "StoreHistoryStore.Map missing");
        }

        private static int NextPageEnd(int rendered, int total, int pageSize) =>
            (int)ControllerType.GetMethod("NextPageEnd", Statics)
                 .Invoke(null, new object[] { rendered, total, pageSize });

        private static int PrependCount(object list, object firstRendered) =>
            (int)ControllerType.GetMethod("PrependCount", Statics)
                 .Invoke(null, new object[] { list, firstRendered });

        private static object NewRecord() => Activator.CreateInstance(RecordType);

        /// <summary>A `List&lt;StoreHistoryRecord&gt;` built by reflection — it satisfies the
        /// method's `IReadOnlyList&lt;StoreHistoryRecord&gt;` parameter directly.</summary>
        private static object RecordList(params object[] records)
        {
            Type listType = typeof(List<>).MakeGenericType(RecordType);
            object list = Activator.CreateInstance(listType);
            MethodInfo add = listType.GetMethod("Add");
            foreach (object r in records) add.Invoke(list, new[] { r });
            return list;
        }

        // ── Page boundaries ───────────────────────────────────────────────────

        [Test]
        public void NextPageEnd_WalksTheListOnePageAtATime()
        {
            Assert.AreEqual(12, NextPageEnd(0, 100, 12), "first page");
            Assert.AreEqual(24, NextPageEnd(12, 100, 12), "second page");
        }

        [Test]
        public void NextPageEnd_ClampsTheLastPageInsteadOfOverrunning()
        {
            // The failure this pins is an out-of-range read on the final, short page — the server
            // hands back at most 100 purchases and 100 is not a multiple of 12.
            Assert.AreEqual(100, NextPageEnd(96, 100, 12), "short final page");
            Assert.AreEqual(100, NextPageEnd(100, 100, 12), "already complete");
            Assert.AreEqual(7, NextPageEnd(0, 7, 12), "fewer records than one page");
        }

        [Test]
        public void NextPageEnd_WithANonPositivePageSize_MakesNoProgress()
        {
            // Better to render nothing further than to spin forever appending empty pages.
            Assert.AreEqual(12, NextPageEnd(12, 100, 0));
            Assert.AreEqual(12, NextPageEnd(12, 100, -5));
        }

        // ── Prepend vs rebuild ────────────────────────────────────────────────

        [Test]
        public void AnUnchangedLog_NeedsNoRedraw()
        {
            object a = NewRecord(), b = NewRecord();
            Assert.AreEqual(0, PrependCount(RecordList(a, b), a),
                            "the first record is still first — nothing was added");
        }

        [Test]
        public void APurchaseLanding_IsAPrependOfExactlyTheNewRows()
        {
            // StoreHistoryStore.Prepend keeps the existing record OBJECTS and puts the new one in
            // front, so the old head is found further down — and its index IS the number added.
            object oldHead = NewRecord(), older = NewRecord();
            object bought  = NewRecord();

            Assert.AreEqual(1, PrependCount(RecordList(bought, oldHead, older), oldHead));
        }

        [Test]
        public void AReplacedLog_ForcesARebuild()
        {
            // StoreHistoryStore.Refresh maps a fresh server page, so every record is a NEW object
            // even when the content is identical. Reference identity is what tells the two apart.
            object renderedHead = NewRecord();
            Assert.AreEqual(-1, PrependCount(RecordList(NewRecord(), NewRecord()), renderedHead));
        }

        [Test]
        public void NothingRenderedYet_IsARebuild()
        {
            Assert.AreEqual(-1, PrependCount(RecordList(NewRecord()), null));
        }

        [Test]
        public void AnEmptiedLog_IsARebuild_NotAPrependOfZero()
        {
            // Distinguishing these matters: 0 means "leave the screen alone", and leaving a stale
            // list on screen after the log emptied would be wrong.
            Assert.AreEqual(-1, PrependCount(RecordList(), NewRecord()));
        }

        // ── The store's mapping ───────────────────────────────────────────────

        [Test]
        public void Map_SkipsARowWhoseCategoryThisBuildCannotRender()
        {
            // The rule that keeps the log honest: a purchase in a category shipped by a newer
            // server is OMITTED, never defaulted to Club. Club is the category with the most
            // machinery behind it, so mislabelling into it fails most confusingly — the same
            // argument GeneralShopCatalog.ParseCategory makes for dropping the catalog row.
            object page = MakePage(
                MakeRow("club",  "club_iron9_klyro", 900),
                MakeRow("sausage", "who_knows",      100),
                MakeRow("ticket", "0",               250));

            var mapped = (System.Collections.IList)StoreType.GetMethod("Map", Statics)
                                                            .Invoke(null, new[] { page });

            Assert.AreEqual(2, mapped.Count, "the unknown category must not become a record");
            Assert.AreEqual("club_iron9_klyro", RefIdOf(mapped[0]));
            Assert.AreEqual("0",                RefIdOf(mapped[1]));
        }

        [Test]
        public void Map_CarriesTheChargedPriceThatWasActuallyPaid_NotTheListPrice()
        {
            // The PRICE line renders ChargedRp, so a sale price stays a sale price in the log
            // forever rather than being re-derived from a catalog that has since changed.
            object page = MakePage(MakeRow("club", "club_iron9_klyro", 900, listRp: 1200));

            var mapped = (System.Collections.IList)StoreType.GetMethod("Map", Statics)
                                                            .Invoke(null, new[] { page });

            Assert.AreEqual(1, mapped.Count);
            Assert.AreEqual(900,  FieldInt(mapped[0], "ChargedRp"));
            Assert.AreEqual(1200, FieldInt(mapped[0], "ListRp"));
        }

        [Test]
        public void Map_OfAnEmptyOrNullPage_IsAnEmptyLog_NotAThrow()
        {
            MethodInfo map = StoreType.GetMethod("Map", Statics);
            Assert.AreEqual(0, ((System.Collections.IList)map.Invoke(null, new object[] { null })).Count);
            Assert.AreEqual(0, ((System.Collections.IList)map.Invoke(null, new[] { MakePage() })).Count);
        }

        // ── DTO helpers (Golfin.Economy is a real asmdef, but the page type is only
        //    reachable from Assembly-CSharp's perspective at runtime — reflect on it too so
        //    this suite needs no assembly reference the gacha one does not have) ────────────

        private static Type PageType =>
            Type.GetType("Golfin.Economy.ShopHistoryPage, Golfin.Economy");
        private static Type DtoType =>
            Type.GetType("Golfin.Economy.ShopPurchaseDto, Golfin.Economy");

        private static object MakeRow(string category, string refId, int chargedRp, int listRp = 0)
        {
            object dto = Activator.CreateInstance(DtoType);
            DtoType.GetField("Category").SetValue(dto, category);
            DtoType.GetField("RefId").SetValue(dto, refId);
            DtoType.GetField("EntryId").SetValue(dto, "shop_" + refId);
            DtoType.GetField("Amount").SetValue(dto, 1);
            DtoType.GetField("ChargedRp").SetValue(dto, chargedRp);
            DtoType.GetField("ListRp").SetValue(dto, listRp);
            DtoType.GetField("CreatedAt").SetValue(dto, "2026-09-08T10:00:00Z");
            return dto;
        }

        private static object MakePage(params object[] rows)
        {
            object page = Activator.CreateInstance(PageType);
            Array arr = Array.CreateInstance(DtoType, rows.Length);
            for (int i = 0; i < rows.Length; i++) arr.SetValue(rows[i], i);
            PageType.GetField("Purchases").SetValue(page, arr);
            return page;
        }

        private static string RefIdOf(object record) =>
            (string)RecordType.GetField("RefId").GetValue(record);

        private static int FieldInt(object record, string name) =>
            (int)RecordType.GetField(name).GetValue(record);
    }
}
