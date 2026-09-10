// ─────────────────────────────────────────────────────────────────────────────
// screen_hints §3.2 — the resolver's rules, on the SHIPPED catalogs where the
// spec names a real screen (Inventory 1/2 not 1/3, GeneralShop alone) and on a
// fixture where it names a hypothetical (TIP_STORE flipped active).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections;
using System.Linq;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class ScreenHintResolverTests
    {
        static IList Shipped => Hints.ShippedHints();
        static IList ShippedTips => Hints.ShippedTips();

        [Test]
        public void Inventory_ShowsTwo_RepairIsInactiveAndNotCounted()
        {
            string[] r = Hints.HintsFor("Inventory", Shipped, ShippedTips, Hints.State());
            CollectionAssert.AreEqual(new[] { "TIP_CLUBSTATS", "TIP_BALLS" }, r, "§2.1 — 1/2 today, not 1/3");
        }

        [Test]
        public void GeneralShop_ShowsGachaAlone_StoreIsInactive()
        {
            string[] r = Hints.HintsFor("GeneralShop", Shipped, ShippedTips, Hints.State());
            CollectionAssert.AreEqual(new[] { "TIP_GACHA" }, r);
        }

        [Test]
        public void Roster_ShowsFourInOrder()
        {
            string[] r = Hints.HintsFor("Roster", Shipped, ShippedTips, Hints.State());
            CollectionAssert.AreEqual(new[] { "TIP_RARITIES", "TIP_STATS", "TIP_CONDITION", "TIP_LEVELUP" }, r);
        }

        [Test]
        public void ASeenScreen_ShowsNothing_EvenWithUnseenKeys()
        {
            string[] r = Hints.HintsFor("Roster", Shipped, ShippedTips, Hints.State(screens: new[] { "Roster" }));
            Assert.That(r, Is.Empty, "§3.2 — seenScreens wins before anything else");
        }

        [Test]
        public void MissionSelection_AfterModeSelection_ShowsDailyOnly()
        {
            // ModeSelection showed MISSIONS / TOURNAMENT / VERSUS; MissionSelection skips
            // MISSIONS and is left with DAILY (Architect default b).
            object state = Hints.State(screens: new[] { "ModeSelection" },
                                       keys: new[] { "TIP_MISSIONS", "TIP_TOURNAMENT", "TIP_VERSUS" });
            string[] r = Hints.HintsFor("MissionSelection", Shipped, ShippedTips, state);
            CollectionAssert.AreEqual(new[] { "TIP_DAILY" }, r);
        }

        [Test]
        public void MissionSelection_AfterHomeAndModeSelection_ShowsNothing()
        {
            object state = Hints.State(screens: new[] { "Home", "ModeSelection" },
                                       keys: new[] { "TIP_RP", "TIP_DAILY", "TIP_MISSIONS", "TIP_TOURNAMENT", "TIP_VERSUS" });
            string[] r = Hints.HintsFor("MissionSelection", Shipped, ShippedTips, state);
            Assert.That(r, Is.Empty, "§2.1 — every key already read elsewhere leaves nothing; the screen is still marked seen by the presenter");
        }

        [Test]
        public void FlippingStoreActive_GivesGeneralShopTwo_WithNoCatalogChange()
        {
            // The fixture is the shipped tips with TIP_STORE flipped to active=1.
            var tips = ShippedTips.Cast<object>().Select(t =>
                Tips.Key(t) == "TIP_STORE" ? Tips.Tip("TIP_STORE", false, 29, "Tip_STORE", active: true) : t).ToArray();
            string[] r = Hints.HintsFor("GeneralShop", Shipped, Tips.Rows(tips), Hints.State());
            CollectionAssert.AreEqual(new[] { "TIP_GACHA", "TIP_STORE" }, r, "§2.1 — the counts grow with no change here");
        }

        [Test]
        public void AKeyWithNoTipRow_IsDropped_AndTheRestKeepTheirOrder()
        {
            object hints = Hints.Rows(Hints.Hint("X", 2, "TIP_STATS"), Hints.Hint("X", 1, "TIP_NOPE"), Hints.Hint("X", 3, "TIP_LEVELUP"));
            string[] r = Hints.HintsFor("X", hints, ShippedTips, Hints.State());
            CollectionAssert.AreEqual(new[] { "TIP_STATS", "TIP_LEVELUP" }, r);
        }

        [Test]
        public void UnknownScreen_AndNullSafeInputs_AreEmpty()
        {
            Assert.That(Hints.HintsFor("NotAScreen", Shipped, ShippedTips, Hints.State()), Is.Empty);
            Assert.That(Hints.HintsFor("", Shipped, ShippedTips, Hints.State()), Is.Empty);
            // A state with null arrays (a blob written by an older build) must not throw.
            object nullState = System.Activator.CreateInstance(Hints.StateT)!;
            CollectionAssert.AreEqual(new[] { "TIP_RP", "TIP_DAILY" }, Hints.HintsFor("Home", Shipped, ShippedTips, nullState));
        }

        [Test]
        public void TheSameKeyTwiceOnOneScreen_IsOneHint()
        {
            object hints = Hints.Rows(Hints.Hint("X", 1, "TIP_STATS"), Hints.Hint("X", 2, "TIP_STATS"));
            CollectionAssert.AreEqual(new[] { "TIP_STATS" }, Hints.HintsFor("X", hints, ShippedTips, Hints.State()));
        }
    }
}
