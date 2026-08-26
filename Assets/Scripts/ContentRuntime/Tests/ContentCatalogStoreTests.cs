// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Content.Tests — ContentCatalogStoreTests
//
// The store's job is small; the ASSERT is the reason it exists. A database that
// parses before ContentService installs the overlay reads an empty store, shows
// bundled rows, and looks exactly like a working client. RequireReady is what
// turns that into a log line, so these tests pin its three states.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Golfin.Content;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Golfin.Content.Tests
{
    [TestFixture]
    public class ContentCatalogStoreTests
    {
        [TearDown]
        public void TearDown() => ContentCatalogStore.Clear();

        private static ContentCatalog Catalog(string name, params string[] ids)
        {
            var rows = new List<ContentRow>();
            foreach (string id in ids)
                rows.Add(new ContentRow(id, true, 0,
                         new Dictionary<string, string?> { { "id", id } }));
            return new ContentCatalog(name, 7, false, rows);
        }

        // ── The three states ──────────────────────────────────────────────────

        [Test]
        public void NoContentServiceAtAll_IsNotAnError()
        {
            // A physics lab / EditMode scene has no ContentService and correctly runs bundled.
            // Logging an error there would train everyone to ignore the one that matters.
            ContentCatalogStore.Clear();

            Assert.AreEqual(ContentStoreState.NotRun, ContentCatalogStore.State);
            Assert.IsFalse(ContentCatalogStore.RequireReady("SomeDatabase"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DeclaredButNotReady_IsAnERROR_BecauseTheOverlayWillBeSilentlyAbsent()
        {
            // ContentService exists but this reader's execution order puts it AHEAD of -900. The
            // store it reads is empty and will be filled moments later, unread, for the whole
            // session — with no other symptom than "the overlay didn't work".
            ContentCatalogStore.Declare();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "EXECUTION ORDER BROKEN: EarlyDatabase is parsing before ContentService"));

            Assert.IsFalse(ContentCatalogStore.RequireReady("EarlyDatabase"));
        }

        [Test]
        public void Ready_LetsDatabasesRead()
        {
            ContentCatalogStore.ConfigureForTest(Catalog(ContentCatalogs.Clubs, "club_a"));

            Assert.AreEqual(ContentStoreState.Ready, ContentCatalogStore.State);
            Assert.IsTrue(ContentCatalogStore.RequireReady("ClubDatabaseCSV"));
            LogAssert.NoUnexpectedReceived();
        }

        // ── Reads ─────────────────────────────────────────────────────────────

        [Test]
        public void InstalledRowsAreReadableByIdAndInOrder()
        {
            ContentCatalogStore.ConfigureForTest(Catalog(ContentCatalogs.Clubs, "club_a", "club_b"));

            Assert.IsNotNull(ContentCatalogStore.Row(ContentCatalogs.Clubs, "club_a"));
            Assert.IsNull(ContentCatalogStore.Row(ContentCatalogs.Clubs, "club_missing"));
            Assert.AreEqual(2, ContentCatalogStore.Rows(ContentCatalogs.Clubs).Count);
            Assert.AreEqual("club_a", ContentCatalogStore.Rows(ContentCatalogs.Clubs)[0].Id);
        }

        [Test]
        public void ACatalogThatWasNeverInstalled_ReadsAsEmptyNotNull()
        {
            ContentCatalogStore.ConfigureForTest(Catalog(ContentCatalogs.Clubs, "club_a"));

            Assert.IsFalse(ContentCatalogStore.IsOverlaid(ContentCatalogs.Characters));
            Assert.IsNull(ContentCatalogStore.Catalog(ContentCatalogs.Characters));
            Assert.IsEmpty(ContentCatalogStore.Rows(ContentCatalogs.Characters),
                "an un-overlaid catalog must read as an empty list, never null — every database " +
                "iterates this without a guard");
            Assert.IsNull(ContentCatalogStore.Row(ContentCatalogs.Characters, "char_a"));
        }

        [Test]
        public void ANullOrEmptyIdIsSafe()
        {
            ContentCatalogStore.ConfigureForTest(Catalog(ContentCatalogs.Clubs, "club_a"));
            Assert.IsNull(ContentCatalogStore.Row(ContentCatalogs.Clubs, null));
            Assert.IsNull(ContentCatalogStore.Row(ContentCatalogs.Clubs, ""));
        }

        [Test]
        public void DeclareWipesAPreviousInstall()
        {
            // A second ContentService.Awake (domain reload, additive scene) must not leave last
            // session's rows visible while the new caches are still being read.
            ContentCatalogStore.ConfigureForTest(Catalog(ContentCatalogs.Clubs, "club_a"));
            ContentCatalogStore.Declare();

            Assert.IsFalse(ContentCatalogStore.IsOverlaid(ContentCatalogs.Clubs));
            Assert.AreEqual(ContentStoreState.Declared, ContentCatalogStore.State);
        }

        [Test]
        public void CatalogNamesCompareCaseInsensitively()
        {
            // The server's names are lower-case, but a hand-edited cache should not cost a whole
            // catalog over capitalisation.
            ContentCatalogStore.ConfigureForTest(Catalog("Clubs", "club_a"));
            Assert.IsTrue(ContentCatalogStore.IsOverlaid(ContentCatalogs.Clubs));
        }
    }
}
