// Assets/Tests/EditMode/GachaHistoryPagingTests.cs
// gacha_history_rebuild_stall — the paging decisions, tested on the SHIPPING seams.
//
// WHY THESE EXIST. `game_polish_a`'s A13 perf run measured a 1 271 ms / 297 MB frame on every
// arrival at Gacha History: `OnEnable` destroyed and re-instantiated one row per prize record —
// up to ~1 000 of them, each building a whole `BagClubCard`. The fix is a first page of 40, an
// append on scroll-end, and a prepend-instead-of-rebuild on the store's OnChanged.
//
// The two decisions that fix can get wrong are arithmetic (which slice is the next page) and
// identity (is this a pull landing on top, or a replaced log). Both are static and record-typed
// precisely so they can be checked here without a scene, a ScrollRect or a play session.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode. GachaHistoryScreenController, GachaHistoryRow and
// GachaHistoryRecord all live in Assembly-CSharp, which an asmdef cannot reference, so every
// production call goes through System.Reflection — the same pattern as GachaClientRealPullTests,
// and for the same reason (feedback_tests_must_target_production_type: the seam under test must be
// the SHIPPING one, never a copy of it living in the test file).
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class GachaHistoryPagingTests
    {
        // ── Reflection handles ────────────────────────────────────────────────

        private static readonly Type ControllerType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaHistoryScreenController, Assembly-CSharp");
        private static readonly Type RowType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaHistoryRow, Assembly-CSharp");
        private static readonly Type RecordType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaHistoryRecord, Assembly-CSharp");

        private const BindingFlags Statics =
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

        [Test]
        public void Production_types_exist_in_AssemblyCSharp()
        {
            // If this fails, every other test here would pass vacuously by never reaching the
            // production code at all — which is the failure mode this project has hit before.
            Assert.IsNotNull(ControllerType, "GachaHistoryScreenController not found");
            Assert.IsNotNull(RowType, "GachaHistoryRow not found");
            Assert.IsNotNull(RecordType, "GachaHistoryRecord not found");
            Assert.IsNotNull(ControllerType.GetMethod("NextPageEnd", Statics), "NextPageEnd missing");
            Assert.IsNotNull(ControllerType.GetMethod("PrependCount", Statics), "PrependCount missing");
            Assert.IsNotNull(RowType.GetMethod("TicketSprite", Statics), "TicketSprite missing");
        }

        private static int NextPageEnd(int rendered, int total, int pageSize) =>
            (int)ControllerType.GetMethod("NextPageEnd", Statics)
                 .Invoke(null, new object[] { rendered, total, pageSize });

        private static int PrependCount(object list, object firstRendered) =>
            (int)ControllerType.GetMethod("PrependCount", Statics)
                 .Invoke(null, new object[] { list, firstRendered });

        private static object NewRecord() => Activator.CreateInstance(RecordType);

        /// <summary>A `List&lt;GachaHistoryRecord&gt;` built by reflection — it satisfies the
        /// method's `IReadOnlyList&lt;GachaHistoryRecord&gt;` parameter directly.</summary>
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
            Assert.AreEqual(40, NextPageEnd(0, 1000, 40), "first page");
            Assert.AreEqual(80, NextPageEnd(40, 1000, 40), "second page");
        }

        [Test]
        public void NextPageEnd_ClampsTheLastPageInsteadOfOverrunning()
        {
            // The failure this pins is an out-of-range read on the final, short page — 1 000
            // records is not a multiple of 40 in general, and the last page must be the remainder.
            Assert.AreEqual(1000, NextPageEnd(980, 1000, 40), "short final page");
            Assert.AreEqual(1000, NextPageEnd(1000, 1000, 40), "already complete");
            Assert.AreEqual(7, NextPageEnd(0, 7, 40), "fewer records than one page");
        }

        [Test]
        public void NextPageEnd_WithANonPositivePageSize_MakesNoProgress()
        {
            // Better to render nothing further than to spin forever appending empty pages.
            Assert.AreEqual(40, NextPageEnd(40, 1000, 0));
            Assert.AreEqual(40, NextPageEnd(40, 1000, -5));
        }

        // ── Prepend vs rebuild ────────────────────────────────────────────────

        [Test]
        public void AnUnchangedLog_NeedsNoRedraw()
        {
            object a = NewRecord(), b = NewRecord();
            object list = RecordList(a, b);

            Assert.AreEqual(0, PrependCount(list, a),
                            "the first record is still first — nothing was added");
        }

        [Test]
        public void APullLanding_IsAPrependOfExactlyTheNewRows()
        {
            // GachaHistoryStore.Prepend keeps the existing record OBJECTS and puts new ones in
            // front, so the old head is found further down — and its index IS the number added.
            object oldHead = NewRecord(), older = NewRecord();
            object new1 = NewRecord(), new2 = NewRecord(), new3 = NewRecord();

            object after = RecordList(new1, new2, new3, oldHead, older);

            Assert.AreEqual(3, PrependCount(after, oldHead));
        }

        [Test]
        public void AReplacedLog_ForcesARebuild()
        {
            // GachaHistoryStore.Refresh maps a fresh server page, so every record is a NEW object
            // even when the content is identical. Reference identity is what tells the two apart.
            object renderedHead = NewRecord();
            object after = RecordList(NewRecord(), NewRecord());

            Assert.AreEqual(-1, PrependCount(after, renderedHead));
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

        // ── Ticket sprite cache ───────────────────────────────────────────────

        [Test]
        public void TheTicketSprite_IsResolvedOncePerTicketType_NotOncePerRow()
        {
            RowType.GetMethod("ClearTicketSpriteCache", Statics).Invoke(null, null);

            MethodInfo ticketSprite = RowType.GetMethod("TicketSprite", Statics);
            Type ticketTypeEnum = ticketSprite.GetParameters()[0].ParameterType;
            object ticket = Enum.ToObject(ticketTypeEnum, 0);

            ticketSprite.Invoke(null, new[] { ticket });
            int loadsAfterFirst = Loads();

            for (int i = 0; i < 50; i++) ticketSprite.Invoke(null, new[] { ticket });

            // Asserted as "the second and every later call added no load", which holds whether or
            // not this ticket type has an icon in the catalog — a MISS is cached too, and must be,
            // or an iconless type re-hits Resources on all ~1 000 rows.
            Assert.AreEqual(loadsAfterFirst, Loads(),
                            "51 calls for one ticket type hit Resources more than once");
            Assert.AreEqual(1, CacheCount(), "one ticket type should occupy one cache entry");
        }

        private static int Loads() =>
            (int)RowType.GetProperty("TicketSpriteLoads", Statics).GetValue(null);

        private static int CacheCount()
        {
            object dict = RowType.GetField("_ticketSprites", Statics).GetValue(null);
            return (int)dict.GetType().GetProperty("Count").GetValue(dict);
        }
    }
}
