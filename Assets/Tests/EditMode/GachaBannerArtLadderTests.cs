// Assets/Tests/EditMode/GachaBannerArtLadderTests.cs
// polish_regressions_0909 R3 — a placeholder artSprite must not mask a row's uploaded art.
//
// THE BUG THIS PINS, because it is invisible from every other angle and shipped for a week:
//
//   GachaBannerArt.Resolve is a ladder. Step 1 (CatalogArtCache.Cached(url, bundledUrl))
//   deliberately returns null when `url == bundledUrl` — "the overlay has not re-uploaded art
//   since this build, so let the bundled sprite win". Step 2 loads artSprite. Step 3 is the url.
//
//   That is correct ONLY while artSprite really is this row's own bundled art. It is NOT, for
//   every row whose art has been published but never bundled: those rows point at the SHARED
//   PLACEHOLDER. The moment the exporter does its job and writes the published artUrl into the
//   bundled CSV (c5558a400, 2026-09-02), `url == bundledUrl` becomes true, step 1 goes quiet,
//   step 2 loads the placeholder, the placeholder RESOLVES, and step 3 is never reached. The
//   uploaded art cannot appear on ANY launch — not "one launch late". Cesar: "it simply does not
//   show".
//
//   Nothing caught it: ids matched, values matched, export --check said clean, and
//   `Validate Catalog Art` said "every sprite column resolves" — because it does resolve. To the
//   wrong picture.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode. GachaBannerArt, GachaBannerEntry and
// TournamentArtService are all outside this asmdef's references, so everything is reached by
// reflection — the same pattern as GachaClientRealPullTests, and for the same reason
// (feedback_tests_must_target_production_type: the ladder under test is the SHIPPING one).

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class GachaBannerArtLadderTests
    {
        private static readonly Type ArtType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaBannerArt, Assembly-CSharp");
        private static readonly Type EntryType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaBannerEntry, Assembly-CSharp");
        // Assembly-CSharp, NOT Golfin.Tournaments: the asmdef of that name covers
        // Assets/Scripts/Tournaments, while TournamentArtService lives in
        // Assets/Scripts/TournamentsRuntime, which has no asmdef at all. The namespace and the
        // assembly disagree, and only the assembly decides where Type.GetType looks.
        private static readonly Type ServiceType =
            Type.GetType("Golfin.Tournaments.TournamentArtService, Assembly-CSharp");

        /// <summary>A real, resolvable sprite in Art/Gacha/Banners that is NOT any test row's own
        /// art — i.e. exactly what an un-bundled row's artSprite cell points at.</summary>
        private const string Placeholder = "GachaBanner_StandardClub1";

        private const string Url = "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/"
                                 + "public/catalog-art/gacha_banners-banner_ladder_test-artUrl-deadbeef.png";

        private Sprite _urlArt;

        [SetUp]
        public void SetUp()
        {
            Assert.IsNotNull(ArtType,     "GachaBannerArt not found in Assembly-CSharp.");
            Assert.IsNotNull(EntryType,   "GachaBannerEntry not found in Assembly-CSharp.");
            Assert.IsNotNull(ServiceType, "TournamentArtService not found in Golfin.Tournaments.");

            Assert.IsNotNull(Resources.Load<Sprite>("Art/Gacha/Banners/" + Placeholder),
                "The test's stand-in placeholder must be a REAL bundled sprite — the whole defect is "
                + "that it resolves. If this fails, " + Placeholder + " was renamed.");

            ArtType.GetMethod("Reload").Invoke(null, null);
            _urlArt = MakeSprite();
            SeedCache(Url, _urlArt);
        }

        [TearDown]
        public void TearDown()
        {
            // Guarded: a SetUp that failed its own asserts still runs TearDown, and an
            // unguarded reflection call here turns one honest failure into five NREs that
            // bury it.
            if (ServiceType != null) ClearCache();
            if (ArtType != null) ArtType.GetMethod("Reload").Invoke(null, null);
            if (_urlArt != null)
            {
                UnityEngine.Object.DestroyImmediate(_urlArt.texture);
                UnityEngine.Object.DestroyImmediate(_urlArt);
                _urlArt = null;
            }
        }

        // ── The regression ────────────────────────────────────────────────────

        [Test]
        public void APlaceholderSpriteDoesNotMaskTheRowsUploadedArt()
        {
            // The exact shape c5558a400 baked in: a url the bundled CSV also carries (so step 1
            // is silent by design), a sprite cell naming the shared placeholder, and the url's
            // bytes already in the cache. Before the fix this returned the placeholder.
            object entry = MakeEntry("banner_ladder_test", artUrl: Url, artSprite: Placeholder);

            Sprite got = Resolve(entry);

            Assert.AreSame(_urlArt, got,
                "the row's UPLOADED art must win over a placeholder that merely happens to resolve");
        }

        [Test]
        public void ARowsOwnBundledSpriteStillWinsOverTheCache()
        {
            // The other direction, and it must not regress: when artSprite IS this row's own
            // bundled art AND the url has not changed since the build, the BUILD's copy is
            // authoritative and no download is preferred over it.
            //
            // Driven off the REAL shipped row rather than a literal, because the condition being
            // exercised is `url == bundledUrl` — which only holds for a url the bundled CSV
            // actually carries. Hardcoding one would silently stop testing anything the day the
            // art is re-uploaded (the bucket filename is content-hashed, so new bytes = new url).
            (string bannerId, string url, string sprite) = ShippedRowWithArt();
            Assert.IsNotNull(url, "no bundled banner row carries an artUrl — this test has nothing to pin");

            Sprite bundled = Resources.Load<Sprite>("Art/Gacha/Banners/" + sprite);
            Assert.IsNotNull(bundled, $"'{bannerId}' names bundled art '{sprite}' that does not resolve");

            SeedCache(url, _urlArt);              // the url IS cached — step 3 could answer
            try
            {
                object entry = MakeEntry(bannerId, artUrl: url, artSprite: sprite);
                Sprite got = Resolve(entry);

                Assert.AreNotSame(_urlArt, got,
                    "a row's own bundled art must not be replaced by the cache");
                Assert.AreEqual(sprite, got.name, "step 2 must still answer for a row's own art");
            }
            finally { SpriteTable().Remove(url); }
        }

        /// <summary>The first bundled banner row that carries BOTH an artUrl and its own
        /// convention-named sprite — read out of the shipped CSV through the production
        /// bundled-only parse.</summary>
        private static (string, string, string) ShippedRowWithArt()
        {
            var asset = Resources.Load<TextAsset>("Data/gacha_banners");
            Assert.IsNotNull(asset, "Resources/Data/gacha_banners.csv is missing");

            var catalog = Type.GetType("GolfinRedux.UI.Gacha.GachaBannerCatalog, Assembly-CSharp");
            var parse = catalog.GetMethod("ParseCsv", BindingFlags.NonPublic | BindingFlags.Static,
                                          null, new[] { typeof(string) }, null);
            Assert.IsNotNull(parse, "GachaBannerCatalog.ParseCsv(string) not found");

            foreach (object row in (System.Collections.IEnumerable)parse.Invoke(null, new object[] { asset.text }))
            {
                string id     = (string)EntryType.GetProperty("BannerId").GetValue(row);
                string url    = (string)EntryType.GetProperty("ArtUrl").GetValue(row);
                string sprite = (string)EntryType.GetProperty("ArtSprite").GetValue(row);
                if (!string.IsNullOrWhiteSpace(url) && SpriteIsOwn(row)) return (id, url, sprite);
            }
            return (null, null, null);
        }

        [Test]
        public void APlaceholderStillStandsInWhileTheArtIsStillDownloading()
        {
            // The placeholder is DEMOTED, not deleted. With no url art cached yet the banner must
            // still render — withholding it would make a published banner vanish from the carousel,
            // which is worse than a stand-in that the carousel swaps out when the bytes land
            // (GachaCarouselController.OnCatalogArtCached).
            ClearCache();
            object entry = MakeEntry("banner_ladder_test", artUrl: Url, artSprite: Placeholder);

            Sprite got = Resolve(entry);

            Assert.IsNotNull(got, "a banner with un-cached url art must not be withheld outright");
            Assert.AreEqual(Placeholder, got.name);
        }

        [Test]
        public void ConventionName_MatchesWhatTheFetcherWrites()
        {
            // One string, three tools: ContentArtFetcher writes it, ContentArtValidator checks it,
            // Resolve trusts it. ASSET_NAMING_CONVENTION.md §5.
            Assert.AreEqual("GachaBanner_StandardClub1", ConventionName("banner_standard_club1"));
            Assert.AreEqual("GachaBanner_TestA",         ConventionName("banner_test_a"));
            Assert.AreEqual("GachaBanner_TestB",         ConventionName("banner_test_b"));
            // No `banner_` prefix: the id is used whole rather than losing its first seven chars.
            Assert.AreEqual("GachaBanner_Seasonal1",     ConventionName("seasonal_1"));
            Assert.AreEqual(string.Empty,                ConventionName(null));
        }

        [Test]
        public void SpriteIsOwn_IsWhatDecidesStepTwo()
        {
            Assert.IsTrue(SpriteIsOwn(MakeEntry("banner_test_a", Url, "GachaBanner_TestA")));
            Assert.IsFalse(SpriteIsOwn(MakeEntry("banner_test_a", Url, Placeholder)),
                "another row's sprite is not this row's own art");
            Assert.IsFalse(SpriteIsOwn(MakeEntry("banner_test_a", Url, "")),
                "an empty sprite cell is not this row's own art");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Sprite Resolve(object entry)
            => (Sprite)ArtType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static)
                              .Invoke(null, new[] { entry });

        private static string ConventionName(string bannerId)
            => (string)ArtType.GetMethod("ConventionName", BindingFlags.Public | BindingFlags.Static)
                              .Invoke(null, new object[] { bannerId });

        private static bool SpriteIsOwn(object entry)
            => (bool)ArtType.GetMethod("SpriteIsOwn", BindingFlags.Public | BindingFlags.Static)
                            .Invoke(null, new[] { entry });

        private static object MakeEntry(string bannerId, string artUrl, string artSprite)
        {
            object entry = Activator.CreateInstance(EntryType);
            EntryType.GetProperty("BannerId").SetValue(entry, bannerId);
            EntryType.GetProperty("ArtUrl").SetValue(entry, artUrl);
            EntryType.GetProperty("ArtSprite").SetValue(entry, artSprite);
            return entry;
        }

        private static Sprite MakeSprite()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "UrlArtUnderTest";
            return sprite;
        }

        /// <summary>Put a decoded sprite into the live service's memory table — the same place
        /// <c>Publish</c> puts one — so <c>CatalogArtCache.Cached</c> answers for the url without
        /// a download.</summary>
        private static Dictionary<string, Sprite> SpriteTable()
        {
            object svc = ServiceType.GetProperty("CatalogArt", BindingFlags.Public | BindingFlags.Static)
                                    .GetValue(null);
            return (Dictionary<string, Sprite>)ServiceType
                .GetField("_sprites", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(svc);
        }

        private static void SeedCache(string url, Sprite sprite) => SpriteTable()[url] = sprite;

        private static void ClearCache() => SpriteTable().Remove(Url);
    }
}
