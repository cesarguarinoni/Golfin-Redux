// Assets/Scripts/UI/Shop/Tests/GeneralShopAdmitResolutionTests.cs
// shop_stocking §6 — a shop row is admitted only when this build can actually render it.
//
// ASSEMBLY: Golfin.UI.Shop.Tests (named asmdef, overrideReferences:false).
// Cannot directly reference Assembly-CSharp types, so GeneralShopCatalog is reached by
// reflection — the same technique GeneralShopCategoryTests uses, and for the same reason:
// this drives the SHIPPING Admit, not a copy of its rules living in the test.

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GolfinRedux.UI.Shop.Tests
{
    /// <summary>
    /// WHAT THIS EXISTS TO CATCH.
    ///
    /// <para>
    /// Before this gate, <c>Admit</c> checked <c>is_active</c> and the listing window and then
    /// admitted the row. Whether the client could RENDER it was discovered later, inside
    /// <c>GeneralShopCard.Bind*</c>, after the card had been instantiated — which early-returned
    /// half-bound and left a blank card with a live BUY button on screen. With server pricing that
    /// card cannot even succeed: <c>golfin_shop_purchase</c> refuses the same row.
    /// </para>
    /// <para>
    /// The second half is just as important and much easier to break: a NULL database singleton
    /// must NOT be treated as "unresolvable". There is no scene in EditMode, so every
    /// <c>*DatabaseCSV.Instance</c> is null here — a resolver that failed closed on that would
    /// withhold the entire catalog in every test and, worse, on any lazy first access that beats
    /// the scene singletons.
    /// </para>
    /// </summary>
    [TestFixture]
    public class GeneralShopAdmitResolutionTests
    {
        private Type _catalog;
        private Type _entryType;
        private Type _categoryType;
        private MethodInfo _admit;
        private FieldInfo _entries;
        private FieldInfo _resolverOverride;
        private FieldInfo _dbAbsentLogged;
        private MethodInfo _unrenderableReason;

        [OneTimeSetUp]
        public void Setup()
        {
            _catalog = Type.GetType("GolfinRedux.UI.Shop.GeneralShopCatalog, Assembly-CSharp");
            Assert.IsNotNull(_catalog, "GeneralShopCatalog not found in Assembly-CSharp.");

            _entryType = Type.GetType("GolfinRedux.UI.Shop.ShopCatalogEntry, Assembly-CSharp");
            Assert.IsNotNull(_entryType, "ShopCatalogEntry not found in Assembly-CSharp.");

            _categoryType = Type.GetType("GolfinRedux.UI.Shop.ShopCategory, Assembly-CSharp");
            Assert.IsNotNull(_categoryType, "ShopCategory not found in Assembly-CSharp.");

            const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
            _admit = _catalog.GetMethod("Admit", Static);
            _entries = _catalog.GetField("_entries", Static);
            _resolverOverride = _catalog.GetField("_resolverOverride", Static);
            _dbAbsentLogged = _catalog.GetField("_dbAbsentLogged", Static);
            _unrenderableReason = _catalog.GetMethod("UnrenderableReason", Static);

            Assert.IsNotNull(_admit, "GeneralShopCatalog.Admit not found.");
            Assert.IsNotNull(_entries, "GeneralShopCatalog._entries not found.");
            Assert.IsNotNull(_resolverOverride, "GeneralShopCatalog._resolverOverride not found.");
            Assert.IsNotNull(_dbAbsentLogged, "GeneralShopCatalog._dbAbsentLogged not found.");
            Assert.IsNotNull(_unrenderableReason, "GeneralShopCatalog.UnrenderableReason not found.");
        }

        [SetUp]
        public void ResetCatalog()
        {
            // A fresh, EMPTY entry list so Admit has somewhere to add and the assertion is about
            // this test's row only. Reload() alone would leave _entries null and Admit would throw.
            var listType = typeof(List<>).MakeGenericType(_entryType);
            _entries.SetValue(null, Activator.CreateInstance(listType));
            _resolverOverride.SetValue(null, null);
            ClearDbLog();
        }

        [TearDown]
        public void ClearOverride()
        {
            _resolverOverride.SetValue(null, null);
        }

        // ---- helpers -----------------------------------------------------------

        private void ClearDbLog()
        {
            var set = _dbAbsentLogged.GetValue(null);
            set.GetType().GetMethod("Clear").Invoke(set, null);
        }

        private object MakeEntry(string entryId, string refId, string category)
        {
            var entry = Activator.CreateInstance(_entryType);
            _entryType.GetProperty("EntryId").SetValue(entry, entryId);
            _entryType.GetProperty("RefId").SetValue(entry, refId);
            _entryType.GetProperty("Category").SetValue(entry, Enum.Parse(_categoryType, category));
            _entryType.GetProperty("IsActive").SetValue(entry, true);
            _entryType.GetProperty("RpCost").SetValue(entry, 100);
            return entry;
        }

        /// <summary>Runs the SHIPPING Admit and returns how many entries it let through.</summary>
        private int Admit(object entry)
        {
            var args = new object[] { entry, DateTime.UtcNow, 0, 0, 0 };
            _admit.Invoke(null, args);
            var list = _entries.GetValue(null);
            return (int)list.GetType().GetProperty("Count").GetValue(list);
        }

        /// <summary>Installs a fake database verdict in place of the real singleton lookup.</summary>
        private void SetResolver(Func<object, string> verdict)
        {
            var delegateType = typeof(Func<,>).MakeGenericType(_entryType, typeof(string));
            var typed = Delegate.CreateDelegate(
                delegateType,
                verdict.Target,
                verdict.Method);
            _resolverOverride.SetValue(null, typed);
        }

        // ---- the gate ----------------------------------------------------------

        [Test]
        public void Admit_withholds_a_row_whose_reference_this_build_cannot_resolve()
        {
            SetResolver(_ => "no row in the characters catalog");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "shop_char_ghost.*WITHHELD"));

            Assert.AreEqual(0, Admit(MakeEntry("shop_char_ghost", "char_ghost", "Character")),
                "A row whose referenced character is not in this build's database must never reach " +
                "the store — that is the blank card with a live BUY button this gate exists for.");
        }

        [Test]
        public void Admit_withholds_a_row_whose_art_resolved_to_the_placeholder()
        {
            SetResolver(_ => "no usable club sprite");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "shop_club_noart.*WITHHELD"));

            Assert.AreEqual(0, Admit(MakeEntry("shop_club_noart", "club_noart", "Club")),
                "A Placeholder sprite is not art. Admitting the row would sell a card with a grey " +
                "box where the club should be.");
        }

        [Test]
        public void Admit_keeps_a_row_whose_reference_resolves()
        {
            SetResolver(_ => null);

            Assert.AreEqual(1, Admit(MakeEntry("shop_club_ok", "club_ok", "Club")),
                "A resolvable row must still be admitted — the gate is a filter, not a wall.");
        }

        // ---- the EditMode / pre-singleton path ---------------------------------

        [Test]
        public void A_null_database_singleton_admits_and_does_not_withhold_everything()
        {
            // No override: this is the REAL resolver, in an EditMode domain where every
            // *DatabaseCSV.Instance is null.
            _resolverOverride.SetValue(null, null);

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(
                "no ClubDatabaseCSV this load"));

            Assert.AreEqual(1, Admit(MakeEntry("shop_club_editmode", "club_x", "Club")),
                "With no scene there is nothing to resolve against. Failing closed here would " +
                "withhold every row in every EditMode test and on any lazy first access that " +
                "beats the scene singletons.");
        }

        [Test]
        public void A_null_database_singleton_logs_once_per_database_per_load()
        {
            _resolverOverride.SetValue(null, null);
            ClearDbLog();

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(
                "no ClubDatabaseCSV this load"));

            // Three rows of the same category; the second and third must be silent, or a 799-row
            // catalog would print 799 identical lines and bury everything else in the load summary.
            Assert.IsNull(_unrenderableReason.Invoke(null, new[] { MakeEntry("a", "club_a", "Club") }));
            Assert.IsNull(_unrenderableReason.Invoke(null, new[] { MakeEntry("b", "club_b", "Club") }));
            Assert.IsNull(_unrenderableReason.Invoke(null, new[] { MakeEntry("c", "club_c", "Club") }));

            LogAssert.NoUnexpectedReceived();
        }
    }
}
