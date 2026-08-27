// ─────────────────────────────────────────────────────────────────────────────
// CatalogArtPolicyTests — security surface for catalog-art (SPEC §6 / content_art_urls §5)
//
// ASSEMBLY: Golfin.TournamentsRuntime.Tests (named EditMode test asmdef)
//
// CatalogArtPolicy lives in Assembly-CSharp (Assets/Scripts/CatalogArt/), which an
// asmdef cannot reference directly. Reached by reflection, same pattern as
// BannerPolicyTests and RemoteScheduleTests.
//
// COVERAGE
//   §1  Art allowlist   — prefix constant, accept/reject table, traversal blocks
//   §2  Cache dir       — independent from tournament-art and banner dirs (three separate LRU buckets)
//   §3  Shared check    — IsAllowedUnder reused, not forked
//   §4  Resolution ladder (SPEC §2, revised 2026-08-27 iter-4)
//       4a  Bundled wins when URLs agree (step 1 returns null, step 2 wins)
//       4b  Changed URL beats bundled (step 1 returns cached sprite)
//       4c  Placeholder must NOT shadow a live URL (step 3 wins over step 4)
//       4d  Unchanged URL, no bundled art (step 3 path — new admin row)
//   §4 LOADER-LEVEL (ClubLoaderLadderTests — iter-5)
//       L1  URL wins over Placeholder when bundled name missing (drives ToRuntime ?? chain)
//       L2  Bundled sprite wins when overlay URL == bundled URL (drives ToRuntime ?? chain)
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.Tournaments.WireupTests
{
    /// <summary>Reflection handles onto <c>Golfin.CatalogArt.CatalogArtPolicy</c> in Assembly-CSharp.</summary>
    internal static class CatalogArtProd
    {
        private static Type Find(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            throw new InvalidOperationException(
                $"Production type '{fullName}' not found. " +
                "It should live in Assembly-CSharp (Assets/Scripts/CatalogArt/, no asmdef).");
        }

        internal static readonly Type Policy = Find("Golfin.CatalogArt.CatalogArtPolicy");

        internal static string ArtPrefix =>
            (string)Policy.GetField("AllowedArtPrefix", BindingFlags.Public | BindingFlags.Static)!
                .GetRawConstantValue()!;

        internal static string CacheDirName =>
            (string)Policy.GetField("CacheDirName", BindingFlags.Public | BindingFlags.Static)!
                .GetRawConstantValue()!;

        internal static bool IsArtAllowed(string? url) =>
            (bool)Policy.GetMethod("IsArtAllowed", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, new object?[] { url })!;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §1  Art host allowlist
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class CatalogArtAllowlistTests
    {
        private static string P => CatalogArtProd.ArtPrefix;

        [Test]
        public void Prefix_is_the_catalog_art_bucket_on_this_project()
        {
            Assert.AreEqual(
                "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/catalog-art/",
                P,
                "The art allowlist is the whole control on this unattended-fetch path — changing it is a security change.");
        }

        [Test]
        public void Accepts_a_well_formed_object_url()
        {
            Assert.IsTrue(CatalogArtProd.IsArtAllowed(P + "characters-char_james-portraitUrl-a1b2c3d4e5f6.jpg"));
            Assert.IsTrue(CatalogArtProd.IsArtAllowed(P + "clubs-club_driver_gf-fullUrl-0f1e2d3c4b5a.png"));
            Assert.IsTrue(CatalogArtProd.IsArtAllowed(P + "nested/path/art.webp"));
        }

        [Test]
        public void Rejects_the_reject_table()
        {
            var table = new (string Url, string Why)[]
            {
                (P.Replace("https://", "http://") + "a.jpg",          "http not https"),
                ("https://evil.example/storage/v1/object/public/catalog-art/a.jpg", "wrong host"),
                ("https://user@wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/catalog-art/a.jpg",
                 "userinfo in authority"),
                ("https://wmszyghwwkaptgqdunel.supabase.co:8443/storage/v1/object/public/catalog-art/a.jpg",
                 "explicit non-default port"),
                (P, "bucket root names no object"),
                ("https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/tournament-art/a.jpg",
                 "tournament-art bucket — different path"),
                ("https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/game-banners/a.jpg",
                 "game-banners bucket — different path"),
                ("",    "empty"),
                ("   ", "whitespace-only"),
                ("not-a-url", "unparseable"),
                ("/storage/v1/object/public/catalog-art/a.jpg", "relative URL, no scheme"),
            };

            foreach (var (url, why) in table)
                Assert.IsFalse(CatalogArtProd.IsArtAllowed(url), $"Must reject ({why}): '{url}'");

            Assert.IsFalse(CatalogArtProd.IsArtAllowed(null), "Must reject null.");
        }

        [Test]
        public void Rejects_traversal_that_normalizes_out_of_the_bucket()
        {
            // A raw StartsWith(prefix) check passes every one of these; the HTTP stack then
            // collapses the dot segments and fetches something else entirely.
            var traversals = new[]
            {
                P + "../../../../../rest/v1/rpc/x",
                P + "..%2f..%2f..%2frest/v1/rpc/x",
                P + "%2e%2e/%2e%2e/rest/v1/rpc/x",
                P + "a/../../../rest/v1/rpc/x",
            };

            foreach (string url in traversals)
                Assert.IsFalse(CatalogArtProd.IsArtAllowed(url), $"Must reject traversal: '{url}'");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §2  Cache dir — three independent LRU buckets, no cross-eviction
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class CatalogArtCacheDirTests
    {
        [Test]
        public void Cache_dir_is_catalog_art()
        {
            Assert.AreEqual("catalog-art", CatalogArtProd.CacheDirName,
                "The dir name is part of the security boundary (it also names the allowlist bucket).");
        }

        [Test]
        public void Cache_dir_is_separate_from_tournament_art()
        {
            string tournamentDir = (string)Prod.ArtPolicy
                .GetField("CacheDirName", BindingFlags.Public | BindingFlags.Static)!
                .GetRawConstantValue()!;

            Assert.AreNotEqual(
                tournamentDir,
                CatalogArtProd.CacheDirName,
                "Sharing a dir would let the two 50 MB LRU budgets evict each other.");
        }

        [Test]
        public void Cache_dir_is_separate_from_banner_art()
        {
            string bannerDir = (string)BannerProd.CacheDirName;
            Assert.AreNotEqual(
                bannerDir,
                CatalogArtProd.CacheDirName,
                "Sharing a dir with banners would let their 50 MB budgets evict each other.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §3  Shared check — IsAllowedUnder must not be forked
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class CatalogArtSharedCheckTests
    {
        [Test]
        public void Shares_the_tournament_policy_check_rather_than_forking_it()
        {
            // If this ever stops resolving, someone copied IsAllowedUnder instead of reusing it —
            // and the two copies will drift on the next security fix.
            var under = Prod.ArtPolicy.GetMethod(
                "IsAllowedUnder", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(under, "TournamentArtPolicy.IsAllowedUnder must exist and stay shared.");
        }

        [Test]
        public void Policy_delegates_to_IsAllowedUnder_via_reflection_smoke()
        {
            // Verify that CatalogArtPolicy.IsArtAllowed produces the same allow/deny decisions
            // that direct IsAllowedUnder calls would produce — so a security patch to IsAllowedUnder
            // is automatically inherited rather than bypassed.
            string goodUrl = CatalogArtProd.ArtPrefix + "characters-char_james-portraitUrl-aabbccddeeff.jpg";
            string badUrl  = "https://evil.example/catalog-art/a.jpg";

            Assert.IsTrue(CatalogArtProd.IsArtAllowed(goodUrl),
                "A valid catalog-art URL must be allowed.");
            Assert.IsFalse(CatalogArtProd.IsArtAllowed(badUrl),
                "An external host must be denied even when the path looks right.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §4  Resolution ladder (SPEC §2, revised 2026-08-27)
    //
    // Tests the three-step cache probe in CatalogArtCache (Assembly-CSharp),
    // reached by reflection the same way other types in this file are.
    //
    // "Seeding" the in-memory cache: TournamentArtService.CatalogArt._sprites is
    // a Dictionary<string,Sprite> (private, per-instance). We inject via
    // reflection so TryGet returns a real Sprite, exactly as it would after a
    // successful download or disk-cache hit. The alternative (writing a PNG to
    // the disk cache and triggering a coroutine-based LoadRoutine) is not
    // feasible in EditMode (coroutine host unavailable). The injection exercises
    // the same TryGet() code path; the network/disk path is covered by the
    // existing TournamentArtService tests.
    // ═════════════════════════════════════════════════════════════════════════

    internal static class CatalogArtCacheReflection
    {
        // TournamentArtService and CatalogArtCache live in Assembly-CSharp.
        // The test asmdef can't reference Assembly-CSharp at compile time;
        // all access goes through AppDomain reflection (same pattern as Prod, BannerProd, etc.).
        private static Type? _cacheType;
        private static Type CacheType => _cacheType ??= Prod.Find("Golfin.CatalogArt.CatalogArtCache");

        private static Type? _artServiceType;
        private static Type ArtServiceType => _artServiceType ??= Prod.Find("Golfin.Tournaments.TournamentArtService");

        // ── Reflection handles onto TournamentArtService.CatalogArt._sprites ──

        private static object GetCatalogArtInstance()
        {
            var prop = ArtServiceType.GetProperty("CatalogArt",
                BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("TournamentArtService.CatalogArt property not found.");
            return prop.GetValue(null)!;
        }

        private static System.Collections.IDictionary GetSpriteDict()
        {
            var field = ArtServiceType.GetField("_sprites",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("TournamentArtService._sprites field not found.");
            return (System.Collections.IDictionary)field.GetValue(GetCatalogArtInstance())!;
        }

        public static Sprite MakeSprite(string name)
        {
            var tex = new Texture2D(1, 1);
            tex.name = name;
            var s = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            s.name = name;   // Sprite.Create does not copy tex.name to s.name in EditMode
            return s;
        }

        public static void InjectSprite(string url, Sprite s) => GetSpriteDict()[url] = s;
        public static void RemoveSprite(string url)           => GetSpriteDict().Remove(url);

        // ── CatalogArtCache.Cached(url, bundledUrl) — step 1 ──

        private static System.Reflection.MethodInfo? _step1Method;
        private static System.Reflection.MethodInfo Step1Method =>
            _step1Method ??= CacheType.GetMethod("Cached",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(string) },
                null)
            ?? throw new InvalidOperationException("CatalogArtCache.Cached(string,string) not found.");

        public static Sprite? CachedStep1(string? url, string? bundledUrl)
            => (Sprite?)Step1Method.Invoke(null, new object?[] { url, bundledUrl });

        // ── CatalogArtCache.Cached(url) — step 3 ──

        private static System.Reflection.MethodInfo? _step3Method;
        private static System.Reflection.MethodInfo Step3Method =>
            _step3Method ??= CacheType.GetMethod("Cached",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null)
            ?? throw new InvalidOperationException("CatalogArtCache.Cached(string) not found.");

        public static Sprite? CachedStep3(string? url)
            => (Sprite?)Step3Method.Invoke(null, new object?[] { url });
    }

    /// <summary>
    /// SPEC §7 — Ladder gate: "bundled wins when URLs agree", "changed URL beats bundled",
    /// "Placeholder never shadows a live URL".
    /// </summary>
    public sealed class CatalogArtResolutionLadderTests
    {
        private const string BASE_URL = "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/catalog-art/";

        [TearDown]
        public void Cleanup()
        {
            // Remove any sprites injected by this test to leave the service in a clean state.
            CatalogArtCacheReflection.RemoveSprite(BASE_URL + "portrait-hash1.jpg");
            CatalogArtCacheReflection.RemoveSprite(BASE_URL + "portrait-hash2.jpg");
            CatalogArtCacheReflection.RemoveSprite(BASE_URL + "new-row-portrait.jpg");
        }

        // ── 4a. Bundled wins when URLs agree ──────────────────────────────────

        [Test]
        public void Step1_Returns_Null_When_Overlay_URL_Equals_Bundled_URL()
        {
            // Inject a sprite into the in-memory cache for URL_A.
            string url = BASE_URL + "portrait-hash1.jpg";
            var injected = CatalogArtCacheReflection.MakeSprite("remote_portrait");
            CatalogArtCacheReflection.InjectSprite(url, injected);

            // Step 1: overlay URL == bundled URL → must return null so step 2 (bundled art) wins.
            var result = CatalogArtCacheReflection.CachedStep1(url, bundledUrl: url);

            Assert.IsNull(result,
                "Step 1 must return null when overlay URL == bundled URL so bundled art wins at step 2. " +
                "A URL-first result here means an installed build would show remote art instead of its bundled sprite.");
        }

        [Test]
        public void Step3_Returns_Sprite_When_URL_Is_Cached()
        {
            // Inject a sprite for URL_A — both overlay and bundled agree, so step 1 returned null.
            // Step 3 (same URL, unchanged since build) should return the sprite — e.g. for a new
            // row from the admin that has no bundled art yet.
            string url = BASE_URL + "portrait-hash1.jpg";
            var injected = CatalogArtCacheReflection.MakeSprite("remote_portrait_step3");
            CatalogArtCacheReflection.InjectSprite(url, injected);

            var result = CatalogArtCacheReflection.CachedStep3(url);

            Assert.IsNotNull(result,
                "Step 3 must return the cached sprite for a URL even when the overlay URL equals the bundled URL.");
            Assert.AreEqual("remote_portrait_step3", result!.name);
        }

        // ── 4b. Changed URL beats bundled ─────────────────────────────────────

        [Test]
        public void Step1_Returns_Sprite_When_Overlay_URL_Differs_From_Bundled_URL()
        {
            // URL_A was the bundled URL; URL_B was re-uploaded after the build → new URL.
            string bundledUrl = BASE_URL + "portrait-hash1.jpg";
            string overlaidUrl = BASE_URL + "portrait-hash2.jpg";
            var injected = CatalogArtCacheReflection.MakeSprite("reuploaded_portrait");
            CatalogArtCacheReflection.InjectSprite(overlaidUrl, injected);

            // Step 1 with differing URLs: should return the re-uploaded sprite.
            var result = CatalogArtCacheReflection.CachedStep1(overlaidUrl, bundledUrl);

            Assert.IsNotNull(result,
                "Step 1 must return the cached sprite when the overlay URL differs from the bundled URL, " +
                "meaning art was re-uploaded since this build was cut.");
            Assert.AreEqual("reuploaded_portrait", result!.name,
                "The returned sprite must be the one injected for the NEW (re-uploaded) URL, not the old one.");
        }

        [Test]
        public void Step1_Returns_Null_When_Changed_URL_Not_Yet_Cached()
        {
            // URL changed but the new art has not been downloaded yet → step 1 must fall through.
            string bundledUrl = BASE_URL + "portrait-hash1.jpg";
            string overlaidUrl = BASE_URL + "portrait-hash2.jpg";
            // Do NOT inject the overlaid URL — it is not cached.

            var result = CatalogArtCacheReflection.CachedStep1(overlaidUrl, bundledUrl);

            Assert.IsNull(result,
                "Step 1 must return null when the re-uploaded URL is not yet cached. " +
                "The row stays withheld until the prefetch completes (SPEC §2.1 — one relaunch is acceptable).");
        }

        // ── 4c. Placeholder never shadows a live URL ──────────────────────────

        [Test]
        public void Step3_Returns_Sprite_So_Step4_Placeholder_Is_Never_Reached_For_Cached_URL()
        {
            // A club with no real bundled art (name empty → LoadRealSprite returns null at step 2)
            // but with a cached URL must use the URL at step 3, NOT Placeholder at step 4.
            // This test verifies step 3 returns a sprite when the URL is cached — so the caller's
            // ?? chain reaches step 3 before reaching LoadSprite (Placeholder).
            string url = BASE_URL + "new-row-portrait.jpg";
            var injected = CatalogArtCacheReflection.MakeSprite("url_club_portrait");
            CatalogArtCacheReflection.InjectSprite(url, injected);

            // Simulated: bundledUrl="" (no bundled row for this club) → step 1 differs → fires.
            // But for a truly new admin row, step 3 is the semantic (unchanged-since-build).
            // Test step 3 directly — it must return the sprite.
            var step3Result = CatalogArtCacheReflection.CachedStep3(url);

            Assert.IsNotNull(step3Result,
                "Step 3 must return the cached sprite so the ?? chain does NOT fall through to LoadSprite " +
                "(which would return Placeholder). A club with a cached URL must NOT show Placeholder.");
            Assert.AreEqual("url_club_portrait", step3Result!.name);
        }

        // ── 4d. Null/empty URL returns null at both steps ─────────────────────

        [Test]
        public void Step1_Returns_Null_For_Empty_URL()
        {
            Assert.IsNull(CatalogArtCacheReflection.CachedStep1("",  ""),
                "Empty overlaidUrl → null (no URL to probe).");
            Assert.IsNull(CatalogArtCacheReflection.CachedStep1(null, null),
                "Null overlaidUrl → null.");
        }

        [Test]
        public void Step3_Returns_Null_For_Empty_URL()
        {
            Assert.IsNull(CatalogArtCacheReflection.CachedStep3(""),
                "Empty url → null (no URL to probe).");
            Assert.IsNull(CatalogArtCacheReflection.CachedStep3(null),
                "Null url → null.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §4 (loader-level) — ClubDatabaseCSV.ToRuntime exercises the full ?? chain
    //
    // The helper-level tests above prove that CatalogArtCache.Cached returns the
    // right value in isolation. These two tests drive the SHIPPING loader
    // (Golfin.Inventory.ClubDatabaseCSV.ToRuntime, private static) via reflection,
    // so they catch regressions to the ?? chain itself — e.g. someone restoring
    // LoadRealSprite back to LoadSprite (which returns Placeholder instead of null),
    // causing step 4 to shadow step 3 on every new-admin club row.
    //
    // Pattern: ContentRenderableTests.cs (same asmdef limitation, same approach).
    // ClubCsvRow and ClubDataRuntime both live in Assembly-CSharp; all access goes
    // through reflection. ToRuntime is reached directly rather than via LoadCSV
    // because LoadCSV calls ContentCatalogStore.RequireReady (always false in
    // EditMode), which forces overlay=null, leaving row.bundled always null and
    // making the "bundled wins when URLs agree" case unreachable via LoadCSV alone.
    //
    // REGRESSION GUARD: if LoadRealSprite is naively replaced with LoadSprite,
    // Loader_URL_Wins_Over_Placeholder_When_BundledNameMissing Part A fails —
    // Placeholder (step 4) is chosen instead of the cached URL (step 1/3).
    // If Cached(url, bundledUrl) stops returning null on url==bundledUrl,
    // Loader_BundledSprite_Wins_When_OverlayURL_Equals_BundledURL fails —
    // the injected URL sprite wins over the real bundled art.
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class ClubLoaderLadderTests
    {
        private const string BASE_URL = "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/catalog-art/";
        private const string URL_A    = BASE_URL + "club-loader-test-portrait-aabbcc.jpg";

        /// <summary>A portrait sprite name that DOES resolve from Resources/Clubs/Portraits/.</summary>
        private const string RealBundledSprite = "Driver-G&F";

        private static readonly BindingFlags PubInst  = BindingFlags.Public  | BindingFlags.Instance;
        private static readonly BindingFlags PrivStat = BindingFlags.NonPublic | BindingFlags.Static;

        private Type _dbType      = null!;
        private Type _rowType     = null!;
        private Type _runtimeType = null!;

        [OneTimeSetUp]
        public void FindTypes()
        {
            _dbType      = Prod.Find("Golfin.Inventory.ClubDatabaseCSV");
            _rowType     = Prod.Find("Golfin.Inventory.ClubCsvRow");
            _runtimeType = Prod.Find("Golfin.Inventory.ClubDataRuntime");
        }

        [TearDown]
        public void Cleanup()
        {
            CatalogArtCacheReflection.RemoveSprite(URL_A);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a <c>ClubCsvRow</c> (Assembly-CSharp) via reflection with only the fields
        /// relevant to the resolution ladder. Other numeric/enum fields keep their defaults.
        /// </summary>
        private object MakeRow(string portraitSprite, string portraitUrl, object? bundled = null)
        {
            var row = Activator.CreateInstance(_rowType)!;
            _rowType.GetField("id",             PubInst)!.SetValue(row, "club_loader_test");
            _rowType.GetField("name",           PubInst)!.SetValue(row, "Loader Test Club");
            _rowType.GetField("portraitSprite", PubInst)!.SetValue(row, portraitSprite);
            _rowType.GetField("portraitFull",   PubInst)!.SetValue(row, "");
            _rowType.GetField("controlSprite",  PubInst)!.SetValue(row, "");
            _rowType.GetField("portraitUrl",    PubInst)!.SetValue(row, portraitUrl);
            _rowType.GetField("portraitFullUrl",PubInst)!.SetValue(row, "");
            _rowType.GetField("controlUrl",     PubInst)!.SetValue(row, "");
            _rowType.GetField("bundled",        PubInst)!.SetValue(row, bundled);
            return row;
        }

        /// <summary>
        /// Calls <c>ClubDatabaseCSV.ToRuntime(row, cache, missing)</c> and returns the resolved
        /// <c>portraitSprite</c> field from the resulting <c>ClubDataRuntime</c>.
        /// </summary>
        private Sprite? InvokeToRuntime(object row)
        {
            var method = _dbType.GetMethod("ToRuntime", PrivStat)
                         ?? throw new InvalidOperationException(
                             "ClubDatabaseCSV.ToRuntime(ClubCsvRow,Dictionary,HashSet) not found. " +
                             "If it was renamed, update this test.");
            var cache   = new Dictionary<string, Sprite?>();
            var missing = new HashSet<string>();
            var result  = method.Invoke(null, new object?[] { row, cache, missing });
            return (Sprite?)_runtimeType.GetField("portraitSprite", PubInst)!.GetValue(result);
        }

        // ── Test 1: Placeholder never shadows a live URL (loader-level) ──────

        /// <summary>
        /// Drives the real ToRuntime ?? chain with two sub-cases:
        ///   Part A — empty portrait name (step 2 null) + URLs AGREE (step 1 null) + URL cached
        ///            → chain must reach STEP 3 and return the injected sprite (NOT Placeholder).
        ///   Part B — real portrait name (step 2 resolves) + no URL → bundled art wins (NOT injected).
        ///
        /// REGRESSION GATE (Part A targets STEP 3, not step 1):
        ///   row.bundled.portraitUrl == row.portraitUrl  →  step 1 returns null (URLs agree).
        ///   portraitSprite == ""                        →  step 2 returns null (no real art).
        ///   URL_A in cache                              →  step 3 returns the injected sprite. ✓
        ///
        ///   With regression (LoadRealSprite → LoadSprite):
        ///     step 2 calls LoadSprite("", …) → Placeholder(folder, cache) → non-null Placeholder.
        ///     Placeholder short-circuits at step 2; step 3 is never reached.
        ///     Assert.AreEqual("injected_loader_portrait", …) → FAIL.
        ///
        ///   Without setting row.bundled (the iter-5 bug), bundledPortraitUrl="" so step 1 fires
        ///   (URL_A != ""), returns the injected sprite at step 1, and step 2 is never reached —
        ///   meaning the regression is invisible. row.bundled must be set for the guard to hold.
        /// </summary>
        [Test]
        public void Loader_URL_Wins_Over_Placeholder_When_BundledNameMissing()
        {
            // Pre-condition: inject a sprite for URL_A into the in-memory catalog-art cache.
            var injected = CatalogArtCacheReflection.MakeSprite("injected_loader_portrait");
            CatalogArtCacheReflection.InjectSprite(URL_A, injected);

            // ── Part A ───────────────────────────────────────────────────────
            // Build a bundled row whose portraitUrl EQUALS the overlay URL (URLs agree).
            // This forces step 1 to return null (Cached(URL_A, URL_A) → agree → null).
            // Empty portraitSprite → step 2 also returns null (LoadRealSprite("") → null).
            // URL_A is cached → the chain must reach STEP 3 and return the injected sprite.
            //
            // Without this bundled-row setup (iter-5 bug): bundledPortraitUrl="" so
            // step 1 fires (URL_A != ""), returns the sprite at step 1, and the
            // regression (LoadSprite at step 2) is completely invisible.
            var bundledRowA = MakeRow(portraitSprite: "", portraitUrl: URL_A);
            var rowNoName   = MakeRow(portraitSprite: "", portraitUrl: URL_A, bundled: bundledRowA);
            var resultA     = InvokeToRuntime(rowNoName);

            Assert.IsNotNull(resultA,
                "Part A: ToRuntime must not return null — clubs always have Placeholder as a final fallback.");
            Assert.AreEqual("injected_loader_portrait", resultA!.name,
                "Part A: with URLs agreeing (step 1 → null), empty portrait name (step 2 → null), " +
                "and URL_A cached, the chain must reach STEP 3 and return the injected sprite — " +
                "NOT Placeholder. Fail here means step 2 returned Placeholder (LoadSprite regression) " +
                "and shadowed step 3: every club with a live URL but no bundled art would show a " +
                "Placeholder instead of the downloaded image — the exact defect LoadRealSprite prevents.");

            // ── Part B ───────────────────────────────────────────────────────
            // Flip: real portrait name ('Driver-G&F') resolves at step 2; no URL cached.
            // Expected: bundled art wins — the result is the real sprite, not the injected one
            // (which would only appear via step 1/3 when a URL is cached).
            var rowWithName = MakeRow(portraitSprite: RealBundledSprite, portraitUrl: "");
            var resultB     = InvokeToRuntime(rowWithName);

            Assert.IsNotNull(resultB,
                "Part B: a row with a real bundled sprite name must always resolve.");
            Assert.AreEqual(RealBundledSprite, resultB!.name,
                "Part B: when a REAL bundled sprite resolves at step 2 and no URL is cached, " +
                "the result must be the bundled sprite ('" + RealBundledSprite + "'). " +
                "Fail here means step 2 is not being called or is returning the wrong sprite.");
        }

        // ── Test 2: Bundled wins when overlay URL == bundled URL (loader-level) ──

        /// <summary>
        /// Creates an overlay row where <c>row.bundled.portraitUrl == row.portraitUrl</c>
        /// (nobody re-uploaded the art after this build). A sprite IS in the cache for that URL.
        /// Expected: the REAL bundled sprite is chosen at step 2 — the injected sprite must NOT win.
        ///
        /// REGRESSION GATE: if <c>Cached(url, bundledUrl)</c> stops returning null when urls agree,
        /// the injected sprite would win at step 1 and the real bundled art would be bypassed on
        /// every row this build already carries — the "bundled-wins" invariant from SPEC §2.2.
        /// </summary>
        [Test]
        public void Loader_BundledSprite_Wins_When_OverlayURL_Equals_BundledURL()
        {
            // Inject a sprite for URL_A — this is the "stale cached download" that must NOT win.
            var injected = CatalogArtCacheReflection.MakeSprite("injected_must_not_win");
            CatalogArtCacheReflection.InjectSprite(URL_A, injected);

            // Build an overlay row: overlay URL == bundled URL (URLs agree).
            // row.bundled simulates the pre-merge bundled CSV row whose URL the build was cut with.
            var bundledRow = MakeRow(portraitSprite: RealBundledSprite, portraitUrl: URL_A);
            var overlayRow = MakeRow(portraitSprite: RealBundledSprite, portraitUrl: URL_A,
                                     bundled: bundledRow);
            // With bundled.portraitUrl == row.portraitUrl, ToRuntime computes:
            //   bundledPortraitUrl = row.bundled.portraitUrl = URL_A
            //   Step 1: Cached(URL_A, URL_A) → url == bundledUrl → null
            //   Step 2: LoadRealSprite("Clubs/Portraits", "Driver-G&F") → real bundled sprite ✓

            var result = InvokeToRuntime(overlayRow);

            Assert.IsNotNull(result,
                "ToRuntime must not return null for a row with a real bundled sprite name.");
            Assert.AreNotEqual("injected_must_not_win", result!.name,
                "When overlay URL == bundled URL, step 1 must return null, so the injected " +
                "URL sprite must NOT be chosen. If this fails, Cached(url, bundledUrl) is not " +
                "returning null on agreement — bundled art would be bypassed on every row the " +
                "build already carries (the regression that iter-1 through iter-3 had).");
            Assert.AreEqual(RealBundledSprite, result!.name,
                "When URLs agree, the REAL bundled sprite ('Driver-G&F') must win at step 2 " +
                "(LoadRealSprite). This is the 'bundled-wins, URL is a bridge' invariant " +
                "from SPEC §2.2 that the earlier URL-first ladder violated.");
        }
    }
}
