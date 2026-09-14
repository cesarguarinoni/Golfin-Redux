// ─────────────────────────────────────────────────────────────────────────────
// screen_hints §3.2 — the resolver's rules, on the SHIPPED catalogs where the
// spec names a real screen (Inventory 1/2 not 1/3, GeneralShop alone) and on a
// fixture where it names a hypothetical (TIP_STORE flipped active).
//
// scheme_aware_gameplay_hints — the shot view's group is per control scheme:
// the swing tip is the SELECTED scheme's, the cone tips are Flick's alone, and
// every ControlScheme value must have a swing tip (a scheme added to the enum
// without a ScreenHints.csv row fails here, not on a player's first hole).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections;
using System.Collections.Generic;
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

        // ── scheme_aware_gameplay_hints ──────────────────────────────────────

        /// <summary>The shipping table, scheme by scheme: the swing tip first, then the four tips
        /// every scheme shares. Only Flick gets TIP_ACCURACY — the cone's aim slide and its
        /// green/red bands exist on no other scheme (CONTROL_SCHEMES_PLAN §5).</summary>
        static readonly Dictionary<string, string[]> GameplayByScheme = new Dictionary<string, string[]>
        {
            ["Flick"]     = new[] { "TIP_SWING", "TIP_ACCURACY", "TIP_GRADES", "TIP_VIEW", "TIP_CLUB", "TIP_FORECAST" },
            ["Pendulum"]  = new[] { "TIP_PENDULUM",              "TIP_GRADES", "TIP_VIEW", "TIP_CLUB", "TIP_FORECAST" },
            ["Needle"]    = new[] { "TIP_TAPTIMING",             "TIP_GRADES", "TIP_VIEW", "TIP_CLUB", "TIP_FORECAST" },
            ["FreeSwing"] = new[] { "TIP_FREESWING",             "TIP_GRADES", "TIP_VIEW", "TIP_CLUB", "TIP_FORECAST" },
        };

        static readonly string[] SwingTips = { "TIP_SWING", "TIP_PENDULUM", "TIP_TAPTIMING", "TIP_FREESWING" };

        [TestCase("Flick")]
        [TestCase("Pendulum")]
        [TestCase("Needle")]
        [TestCase("FreeSwing")]
        public void Gameplay_ShowsTheSelectedSchemesSwingTip_ThenTheSharedFour(string scheme)
        {
            string[] r = Hints.HintsFor("Gameplay", Shipped, ShippedTips, Hints.State(), scheme);
            CollectionAssert.AreEqual(GameplayByScheme[scheme], r, "Gameplay hints for " + scheme);
        }

        [Test]
        public void Flick_IsSixHints_TheOthersFive_SoTheCounterReadsPerScheme()
        {
            Assert.That(Hints.HintsFor("Gameplay", Shipped, ShippedTips, Hints.State(), "Flick").Length, Is.EqualTo(6), "1/6 … 6/6, unchanged from screen_hints");
            foreach (string s in new[] { "Pendulum", "Needle", "FreeSwing" })
                Assert.That(Hints.HintsFor("Gameplay", Shipped, ShippedTips, Hints.State(), s).Length, Is.EqualTo(5), s + " is 1/5 … 5/5");
        }

        [Test]
        public void EveryControlScheme_GetsExactlyOneSwingTip_AndItComesFirst()
        {
            // Walks the ENUM, not the table above: a fifth scheme added to ControlScheme without a
            // ScreenHints.csv row starts its tutorial at TIP_GRADES and fails here.
            var seen = new Dictionary<string, string>();
            foreach (string scheme in Hints.SchemeNames)
            {
                string[] r = Hints.HintsFor("Gameplay", Shipped, ShippedTips, Hints.State(), scheme);
                Assert.That(r, Is.Not.Empty, scheme + " has no Gameplay hints at all");
                Assert.That(SwingTips, Does.Contain(r[0]), scheme + "'s first shot-view hint must be its swing tip, got " + r[0]);
                Assert.That(r.Skip(1).Intersect(SwingTips), Is.Empty, scheme + " shows another scheme's swing tip: " + string.Join(", ", r));
                Assert.That(seen.ContainsValue(r[0]), Is.False, scheme + " shares its swing tip with " + string.Join("/", seen.Where(kv => kv.Value == r[0]).Select(kv => kv.Key)));
                seen[scheme] = r[0];
            }
        }

        [Test]
        public void ASchemeRow_IsInvisibleToTheOtherSchemes_AndABlankRowShowsForAll()
        {
            object hints = Hints.Rows(
                Hints.Hint("X", 1, "TIP_PENDULUM", "Pendulum"),
                Hints.Hint("X", 2, "TIP_FREESWING", "FreeSwing"),
                Hints.Hint("X", 3, "TIP_STATS"));               // blank: every scheme

            CollectionAssert.AreEqual(new[] { "TIP_PENDULUM", "TIP_STATS" }, Hints.HintsFor("X", hints, ShippedTips, Hints.State(), "Pendulum"));
            CollectionAssert.AreEqual(new[] { "TIP_FREESWING", "TIP_STATS" }, Hints.HintsFor("X", hints, ShippedTips, Hints.State(), "FreeSwing"));
            CollectionAssert.AreEqual(new[] { "TIP_STATS" }, Hints.HintsFor("X", hints, ShippedTips, Hints.State(), "Flick"), "no Flick row → only the shared one");
            CollectionAssert.AreEqual(new[] { "TIP_STATS" }, Hints.HintsFor("X", hints, ShippedTips, Hints.State(), "Needle"));
        }

        [Test]
        public void ScreensWithoutSchemeRows_ResolveIdentically_UnderEveryScheme()
        {
            foreach (string screen in new[] { "Home", "Roster", "Inventory", "HoleSelection", "SettingsControls" })
            {
                string[] flick = Hints.HintsFor(screen, Shipped, ShippedTips, Hints.State(), "Flick");
                foreach (string scheme in Hints.SchemeNames)
                    CollectionAssert.AreEqual(flick, Hints.HintsFor(screen, Shipped, ShippedTips, Hints.State(), scheme), screen + " under " + scheme);
            }
        }

        [Test]
        public void SeenKeysAndSeenScreens_StillWin_OverTheSchemeFilter()
        {
            // A Pendulum player who already read GRADES elsewhere (fixture) gets PENDULUM + the other three.
            object state = Hints.State(keys: new[] { "TIP_GRADES" });
            CollectionAssert.AreEqual(new[] { "TIP_PENDULUM", "TIP_VIEW", "TIP_CLUB", "TIP_FORECAST" },
                Hints.HintsFor("Gameplay", Shipped, ShippedTips, state, "Pendulum"));

            // And a seen shot view shows nothing to any scheme — switching schemes later does not
            // re-open the group (SchemeConfirmModal explains the new scheme at the switch).
            foreach (string scheme in Hints.SchemeNames)
                Assert.That(Hints.HintsFor("Gameplay", Shipped, ShippedTips, Hints.State(screens: new[] { "Gameplay" }), scheme), Is.Empty, scheme);
        }
    }
}
