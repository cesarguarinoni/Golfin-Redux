// gps device pass 2026-09-03 — the gift catalog reads Japanese in an English build.
using System.Collections.Generic;
using Golfin.Social;
using NUnit.Framework;

namespace Golfin.Social.Tests
{
    /// <summary>
    /// The catalog's names are server data with no second language, so the only thing worth
    /// pinning is the SEAM: which key a row resolves to, and what happens when that key is not
    /// published yet. The fallback is the part that matters — a miss must render the Japanese the
    /// player would have seen anyway, never the key.
    /// </summary>
    public class GiftItemNameTests
    {
        private static readonly LocalizedTextRow Cap =
            new LocalizedTextRow { key = "GIFT_ITEM_8CBC1D6B", english = "Basic Cap", japanese = "ベーシックキャップ" };

        private static GiftItemDto Item(string id, string name) =>
            new GiftItemDto { Id = id, Name = name };

        private static void Publish(params LocalizedTextRow[] rows)
        {
            var map = new Dictionary<string, LocalizedTextRow>();
            foreach (var r in rows) map[r.key] = r;
            LocalizationManager.ApplyOverlay(map);
        }

        [SetUp]
        public void SetUp() => Publish(Cap);

        [Test]
        public void TheKeyIsTheFirstEightHexOfTheRowId_Uppercased()
        {
            Assert.AreEqual("GIFT_ITEM_8CBC1D6B",
                            GiftItemName.KeyFor("8cbc1d6b-42dc-4cbf-89f7-518514b5eea9"));
        }

        [Test]
        public void APublishedRow_RendersInTheCurrentLanguage()
        {
            LocalizationManager.SetLanguage(Language.English);
            Assert.AreEqual("Basic Cap",
                GiftItemName.Of(Item("8cbc1d6b-42dc-4cbf-89f7-518514b5eea9", "ベーシックキャップ")));

            LocalizationManager.SetLanguage(Language.Japanese);
            Assert.AreEqual("ベーシックキャップ",
                GiftItemName.Of(Item("8cbc1d6b-42dc-4cbf-89f7-518514b5eea9", "ベーシックキャップ")));
        }

        [Test]
        public void AnUnpublishedRow_FallsBackToTheServerName_NotTheKey()
        {
            LocalizationManager.SetLanguage(Language.English);
            string shown = GiftItemName.Of(Item("deadbeef-0000-0000-0000-000000000000", "まだ翻訳なし"));
            Assert.AreEqual("まだ翻訳なし", shown);
            StringAssert.DoesNotContain("GIFT_ITEM_", shown);
        }

        [Test]
        public void ARowWithNoUsableId_StillRendersItsName()
        {
            Assert.AreEqual("グローブ", GiftItemName.Of(Item(null, "グローブ")));
            Assert.AreEqual("グローブ", GiftItemName.Of(Item("abc", "グローブ")));
        }

        [Test]
        public void ANullItem_IsEmpty_NotAnException()
        {
            Assert.AreEqual(string.Empty, GiftItemName.Of(null));
        }

        [TearDown]
        public void TearDown() => LocalizationManager.SetLanguage(Language.English);
    }
}
