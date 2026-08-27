// Assets/Scripts/UI/Shop/Tests/GeneralShopCategoryTests.cs
// shop_server_purchase §3.7 — the shop_catalog `category` column, parsed strictly.
//
// ASSEMBLY: Golfin.UI.Shop.Tests (named asmdef, overrideReferences:false).
// Cannot directly reference Assembly-CSharp types, so GeneralShopCatalog.ParseCategory is
// reached by reflection — the same technique StaminaShopAddEnergyTests uses for
// StaminaRuntimeService, and the reason it matters is the same: this drives the SHIPPING
// method, not a copy of its rules living in the test.

using System;
using System.Reflection;
using NUnit.Framework;

namespace GolfinRedux.UI.Shop.Tests
{
    /// <summary>
    /// WHAT THIS EXISTS TO CATCH.
    ///
    /// <para>
    /// <c>ParseCategory</c> used to be <c>== "ball" ? Ball : Club</c>. So a <c>character</c> row —
    /// which the admin Shop panel has been able to publish since it shipped — became a CLUB card
    /// bound to a refId <c>ClubDatabaseCSV</c> has never heard of, and so did a <c>bag</c>, and so
    /// did a typo. Falling back to Club is the worst possible default: it is the category with the
    /// most machinery behind it (owned state, durability, level), so it fails the most confusingly.
    /// </para>
    /// <para>
    /// The four supported categories returning themselves is the easy half. The half that is worth a
    /// test is that everything ELSE returns null — including <c>bag</c>, which is a legitimate,
    /// publishable category that this client deliberately cannot sell (the grants queue has no bag
    /// kind, so the server refuses it and a card for it could only ever fail).
    /// </para>
    /// <para>
    /// A null return is what makes <c>ParseRow</c> drop the row with a warning naming the entryId.
    /// That drop is two lines downstream of this method and is not separately reachable without a
    /// <c>ContentFields</c> instance; the rule that decides it is here.
    /// </para>
    /// </summary>
    [TestFixture]
    public class GeneralShopCategoryTests
    {
        private MethodInfo _parseCategory;
        private Type _categoryType;

        [OneTimeSetUp]
        public void Setup()
        {
            var catalog = Type.GetType("GolfinRedux.UI.Shop.GeneralShopCatalog, Assembly-CSharp");
            Assert.IsNotNull(catalog, "GeneralShopCatalog not found in Assembly-CSharp.");

            _categoryType = Type.GetType("GolfinRedux.UI.Shop.ShopCategory, Assembly-CSharp");
            Assert.IsNotNull(_categoryType, "ShopCategory not found in Assembly-CSharp.");

            _parseCategory = catalog.GetMethod("ParseCategory",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(_parseCategory, "GeneralShopCatalog.ParseCategory not found.");
        }

        /// <returns>The enum member name, or null when the row would be dropped.</returns>
        private string Parse(string raw)
        {
            object result = _parseCategory.Invoke(null, new object[] { raw });
            return result == null ? null : Enum.GetName(_categoryType, result);
        }

        // ── the four this build can sell ──────────────────────────────────────────

        [TestCase("club",      "Club")]
        [TestCase("ball",      "Ball")]
        [TestCase("character", "Character")]
        [TestCase("item",      "Item")]
        public void ASupportedCategoryParsesToItself(string raw, string expected)
        {
            Assert.AreEqual(expected, Parse(raw));
        }

        [TestCase("  Club  ", "Club")]
        [TestCase("CHARACTER", "Character")]
        [TestCase("Item",      "Item")]
        public void CaseAndSurroundingWhitespaceAreTolerated(string raw, string expected)
        {
            // The value is a CSV cell an operator typed into the admin, so "Character" and
            // " character " are the same intent as "character".
            Assert.AreEqual(expected, Parse(raw));
        }

        // ── everything else is DROPPED, not clubbed ──────────────────────────────

        [Test]
        public void BagIsDroppedRatherThanRenderedAsAClub()
        {
            // `bag` is publishable and is deliberately NOT sellable: InventoryGrants has no bag kind,
            // so the server answers unsupported_category and a card for it could only ever fail.
            Assert.IsNull(Parse("bag"));
        }

        [TestCase("clubs")]     // plural typo
        [TestCase("Charater")]  // misspelling
        [TestCase("weapon")]    // a category from a server this build does not know
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void AnUnknownCategoryIsDroppedRatherThanClubbed(string raw)
        {
            Assert.IsNull(Parse(raw),
                "Anything unrecognised must DROP the row. Defaulting to Club is the bug this task " +
                "closes — it produced a club card bound to a refId no club catalog has.");
        }
    }
}
