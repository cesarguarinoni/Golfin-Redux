// ─────────────────────────────────────────────────────────────────────────────
// BannerPolicyTests — the security surface of game_banners (SPEC §6, §5)
//
// ASSEMBLY: Golfin.TournamentsRuntime.Tests (named EditMode test asmdef)
//
// Same access pattern as RemoteScheduleTests, and for the same reason: the
// production types live in Assembly-CSharp (Assets/Scripts/BannersRuntime/),
// which an asmdef cannot reference. They are reached by REFLECTION; everything
// asserted is a primitive, so the assertions need no casting games.
//
// COVERAGE
//   §1  Art allowlist   — accept/reject table for IsArtAllowed
//   §2  Link allowlist  — accept/reject table for IsLinkAllowed
//   §3  Resolution ladder — expiry, language preference, cross-locale fallback
//   §4  Wire parsing    — placement strings, expires_at as absolute UTC, envelope
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace Golfin.Tournaments.WireupTests
{
    /// <summary>Reflection handles onto the Assembly-CSharp banner types.</summary>
    internal static class BannerProd
    {
        private static Type Find(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            throw new InvalidOperationException(
                $"Production type '{fullName}' not found. It should live in Assembly-CSharp " +
                "(Assets/Scripts/BannersRuntime/, no asmdef).");
        }

        internal static readonly Type Policy  = Find("Golfin.Banners.BannerPolicy");
        internal static readonly Type Service = Find("Golfin.Banners.BannerService");

        internal static string ArtPrefix =>
            (string)Policy.GetField("AllowedArtPrefix", BindingFlags.Public | BindingFlags.Static)!
                .GetRawConstantValue()!;

        internal static string CacheDirName =>
            (string)Policy.GetField("CacheDirName", BindingFlags.Public | BindingFlags.Static)!
                .GetRawConstantValue()!;

        internal static bool IsArtAllowed(string? url) =>
            (bool)Policy.GetMethod("IsArtAllowed", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, new object?[] { url })!;

        internal static bool IsLinkAllowed(string? url) =>
            (bool)Policy.GetMethod("IsLinkAllowed", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, new object?[] { url })!;

        internal static bool IsExternalLinkAllowed(string? url) =>
            (bool)Policy.GetMethod("IsExternalLinkAllowed", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, new object?[] { url })!;

        /// <summary>
        /// <c>BannerPolicy.TryGetInternalRoute</c>. Returns the ScreenId as its NAME, so this test
        /// assembly never has to reference the Assembly-CSharp enum.
        /// </summary>
        internal static (bool Matched, string Screen) TryGetInternalRoute(string? url)
        {
            var m = Policy.GetMethod("TryGetInternalRoute", BindingFlags.Public | BindingFlags.Static)
                    ?? throw new InvalidOperationException("BannerPolicy.TryGetInternalRoute not found.");
            object?[] args = { url, null };
            bool matched = (bool)m.Invoke(null, args)!;
            return (matched, args[1]?.ToString() ?? "");
        }

        internal static string InternalScheme =>
            (string)Policy.GetField("InternalScheme", BindingFlags.Public | BindingFlags.Static)!
                .GetRawConstantValue()!;

        internal static string StandaloneScheme =>
            (string)Policy.GetField("StandaloneScheme", BindingFlags.Public | BindingFlags.Static)!
                .GetRawConstantValue()!;

        /// <summary>The production ladder, called exactly as <c>TryGet</c> calls it.</summary>
        internal static string? Resolve(
            string? en, string? ja, bool japanese, DateTime? expiresAtUtc, DateTime nowUtc)
        {
            var m = Service.GetMethod("ResolveImageUrl",
                        BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException("BannerService.ResolveImageUrl not found.");
            return (string?)m.Invoke(null, new object?[] { en, ja, japanese, expiresAtUtc, nowUtc });
        }

        internal static DateTime? ParseUtc(string? value)
        {
            var m = Service.GetMethod("ParseUtc", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (DateTime?)m.Invoke(null, new object?[] { value });
        }

        /// <summary>Returns (matched, placementName) so the test needs no enum reference.</summary>
        internal static (bool Matched, string Name) TryParsePlacement(string? wire)
        {
            var m = Service.GetMethod("TryParsePlacement", BindingFlags.NonPublic | BindingFlags.Static)!;
            object?[] args = { wire, null };
            bool matched = (bool)m.Invoke(null, args)!;
            return (matched, args[1]?.ToString() ?? "");
        }

        /// <summary>Deserialize a raw or unwrapped body; returns the placement of each banner.</summary>
        internal static List<string> DeserializePlacements(string json)
        {
            var m = Service.GetMethod("Deserialize", BindingFlags.NonPublic | BindingFlags.Static)!;
            // BannerSource.DiskCache — the second arg is only used in the failure log.
            object source = Enum.Parse(Find("Golfin.Banners.BannerSource"), "DiskCache");
            object? dto = m.Invoke(null, new object?[] { json, source });

            var names = new List<string>();
            if (dto == null) return names;

            var list = (System.Collections.IList?)dto.GetType().GetField("Banners")!.GetValue(dto);
            if (list == null) return names;

            foreach (object? row in list)
            {
                if (row == null) continue;
                names.Add((string?)row.GetType().GetField("Placement")!.GetValue(row) ?? "");
            }
            return names;
        }

        internal static string? DeserializeFirstExpiresAt(string json)
        {
            var m = Service.GetMethod("Deserialize", BindingFlags.NonPublic | BindingFlags.Static)!;
            object source = Enum.Parse(Find("Golfin.Banners.BannerSource"), "Server");
            object? dto = m.Invoke(null, new object?[] { json, source });
            if (dto == null) return null;

            var list = (System.Collections.IList?)dto.GetType().GetField("Banners")!.GetValue(dto);
            if (list == null || list.Count == 0) return null;

            object first = list[0]!;
            return (string?)first.GetType().GetField("ExpiresAt")!.GetValue(first);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §1  Art host allowlist
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class BannerArtAllowlistTests
    {
        private static string P => BannerProd.ArtPrefix;

        [Test]
        public void Prefix_is_the_game_banners_bucket_on_this_project()
        {
            Assert.AreEqual(
                "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/game-banners/",
                P,
                "The art allowlist is the whole control on this path — changing it is a security change.");
        }

        [Test]
        public void Cache_dir_is_separate_from_tournament_art()
        {
            Assert.AreEqual("game-banners", BannerProd.CacheDirName);

            // Prod.ArtPolicy is the reflection handle onto TournamentArtPolicy, which lives in
            // Assembly-CSharp and cannot be referenced from an asmdef.
            string tournamentDir = (string)Prod.ArtPolicy
                .GetField("CacheDirName", BindingFlags.Public | BindingFlags.Static)!
                .GetRawConstantValue()!;

            Assert.AreNotEqual(
                tournamentDir,
                BannerProd.CacheDirName,
                "Sharing a directory would let the two 50 MB LRU budgets evict each other.");
        }

        [Test]
        public void Accepts_a_well_formed_object_url()
        {
            Assert.IsTrue(BannerProd.IsArtAllowed(P + "home_promo-en-a1b2c3d4e5f6.jpg"));
            Assert.IsTrue(BannerProd.IsArtAllowed(P + "rankings-ja-0f1e2d3c4b5a.png"));
            Assert.IsTrue(BannerProd.IsArtAllowed(P + "nested/path/art.webp"));
        }

        [Test]
        public void Rejects_the_reject_table()
        {
            var table = new (string Url, string Why)[]
            {
                (P.Replace("https://", "http://") + "a.jpg", "http, not https"),
                ("https://evil.example/storage/v1/object/public/game-banners/a.jpg", "wrong host"),
                ("https://user@wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/game-banners/a.jpg",
                 "userinfo"),
                ("https://wmszyghwwkaptgqdunel.supabase.co:8443/storage/v1/object/public/game-banners/a.jpg",
                 "explicit port"),
                (P, "the bucket root itself names no object"),
                ("https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/tournament-art/a.jpg",
                 "the other bucket"),
                ("", "empty"),
                ("   ", "whitespace"),
                ("not-a-url", "unparseable"),
                ("/storage/v1/object/public/game-banners/a.jpg", "relative"),
            };

            foreach (var (url, why) in table)
                Assert.IsFalse(BannerProd.IsArtAllowed(url), $"Must reject ({why}): '{url}'");

            Assert.IsFalse(BannerProd.IsArtAllowed(null), "Must reject null.");
        }

        [Test]
        public void Rejects_traversal_that_normalizes_out_of_the_bucket()
        {
            // A raw StartsWith(prefix) check passes every one of these, and then the HTTP stack
            // collapses the dot segments and GETs something else entirely.
            var traversals = new[]
            {
                P + "../../../../../rest/v1/rpc/x",
                P + "..%2f..%2f..%2frest/v1/rpc/x",
                P + "%2e%2e/%2e%2e/rest/v1/rpc/x",
                P + "a/../../../rest/v1/rpc/x",
            };

            foreach (string url in traversals)
                Assert.IsFalse(BannerProd.IsArtAllowed(url), $"Must reject traversal: '{url}'");
        }

        [Test]
        public void Shares_the_tournament_policy_check_rather_than_forking_it()
        {
            // If this ever stops resolving, someone copied the check instead of reusing it — and
            // the two copies will drift on the next security fix.
            var under = Prod.ArtPolicy.GetMethod(
                "IsAllowedUnder", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(under, "TournamentArtPolicy.IsAllowedUnder must exist and stay shared.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §2  Link host allowlist
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class BannerLinkAllowlistTests
    {
        [Test]
        public void Accepts_the_four_allowlisted_hosts()
        {
            Assert.IsTrue(BannerProd.IsLinkAllowed("https://golfin.io/x"));
            Assert.IsTrue(BannerProd.IsLinkAllowed("https://www.golfin.io/x"));
            Assert.IsTrue(BannerProd.IsLinkAllowed("https://golfin.world/y"));
            Assert.IsTrue(BannerProd.IsLinkAllowed("https://www.golfin.world/y"));
        }

        [Test]
        public void Accepts_a_bare_host_with_query_and_fragment()
        {
            Assert.IsTrue(BannerProd.IsLinkAllowed("https://golfin.io"));
            Assert.IsTrue(BannerProd.IsLinkAllowed("https://golfin.io/campaign/august?utm=banner#top"));
        }

        // ── gps_hub_entry §1 — in-app routes ──────────────────────────────────

        [Test]
        public void Accepts_the_gps_hub_internal_route_and_resolves_it_to_GpsHub()
        {
            Assert.AreEqual("golfin", BannerProd.InternalScheme);

            Assert.IsTrue(BannerProd.IsLinkAllowed("golfin://gps"),
                "The Home promo banner's whole purpose in gps_hub_entry is this one link.");

            var (matched, screen) = BannerProd.TryGetInternalRoute("golfin://gps");
            Assert.IsTrue(matched);
            Assert.AreEqual("GpsHub", screen,
                "The route must resolve to ScreenId.GpsHub, not merely be 'allowed'.");
        }

        [Test]
        public void Internal_route_is_case_insensitive_because_Uri_lower_cases_scheme_and_host()
        {
            Assert.IsTrue(BannerProd.IsLinkAllowed("GOLFIN://GPS"));
            Assert.AreEqual((true, "GpsHub"), BannerProd.TryGetInternalRoute("GOLFIN://GPS"));
        }

        // ── gps_standalone_shell §D6 — the PLAYLIFE shell's own scheme ────────

        /// <summary>
        /// The shell is a SEPARATE app installed BESIDE the game, and two apps claiming one
        /// custom scheme is undefined on iOS — so it claims <c>golfingps://</c> and the game keeps
        /// <c>golfin://</c>. Both resolve HERE, in both variants, because a banner row is written
        /// once in the dashboard and served to every app: the scheme decides which app opens, the
        /// route names the same surface either way.
        /// </summary>
        [Test]
        public void Accepts_the_standalone_scheme_for_the_same_hub_route()
        {
            Assert.AreEqual("golfingps", BannerProd.StandaloneScheme);
            Assert.AreNotEqual(BannerProd.InternalScheme, BannerProd.StandaloneScheme);

            Assert.IsTrue(BannerProd.IsLinkAllowed("golfingps://gps"));
            Assert.AreEqual((true, "GpsHub"), BannerProd.TryGetInternalRoute("golfingps://gps"));
            Assert.AreEqual((true, "GpsHub"), BannerProd.TryGetInternalRoute("GOLFINGPS://GPS"));
        }

        /// <summary>
        /// The second scheme widens WHO may open the app, not WHAT a link may say: every refusal
        /// the game's scheme gets, the shell's gets too.
        /// </summary>
        [Test]
        public void The_standalone_scheme_is_held_to_the_same_enumerated_routes()
        {
            Assert.IsFalse(BannerProd.TryGetInternalRoute("golfingps://shop").Matched);
            Assert.IsFalse(BannerProd.TryGetInternalRoute("golfingps://gps/checkin").Matched);
            Assert.IsFalse(BannerProd.TryGetInternalRoute("golfingps://gps?tab=1").Matched);
            Assert.IsFalse(BannerProd.TryGetInternalRoute("golfingps://a@gps").Matched);
            Assert.IsFalse(BannerProd.TryGetInternalRoute("golfingpsx://gps").Matched);
        }

        [Test]
        public void An_unenumerated_internal_route_is_refused_not_guessed()
        {
            // A newer dashboard may be ahead of this build. Guessing would hand a server-supplied
            // string a navigation grant to a screen this build never vetted.
            Assert.IsFalse(BannerProd.IsLinkAllowed("golfin://shop"));
            Assert.IsFalse(BannerProd.TryGetInternalRoute("golfin://shop").Matched);

            // A path, query or userinfo means the link is saying something the switch does not read.
            Assert.IsFalse(BannerProd.TryGetInternalRoute("golfin://gps/checkin").Matched);
            Assert.IsFalse(BannerProd.TryGetInternalRoute("golfin://gps?tab=1").Matched);
            Assert.IsFalse(BannerProd.TryGetInternalRoute("golfin://a@gps").Matched);

            // A near-miss scheme is not the internal scheme.
            Assert.IsFalse(BannerProd.TryGetInternalRoute("golfinx://gps").Matched);
            Assert.IsFalse(BannerProd.TryGetInternalRoute("https://gps").Matched);
            Assert.IsFalse(BannerProd.TryGetInternalRoute(null).Matched);
            Assert.IsFalse(BannerProd.TryGetInternalRoute("").Matched);
        }

        [Test]
        public void The_external_allowlist_never_accepts_an_internal_route()
        {
            // IsLinkAllowed is the UNION of the two; the halves must stay separable, or a future
            // change to the host list could silently start opening golfin:// in a browser.
            Assert.IsFalse(BannerProd.IsExternalLinkAllowed("golfin://gps"));
            Assert.IsTrue(BannerProd.IsExternalLinkAllowed("https://golfin.io/x"));
        }

        [Test]
        public void Rejects_the_reject_table()
        {
            var table = new (string Url, string Why)[]
            {
                ("http://golfin.io",             "http, not https"),
                ("https://evil-golfin.io",       "prefix-adjacent host — a suffix match would pass this"),
                ("https://golfin.io.attacker.net", "suffix-adjacent host"),
                ("https://golfin.io:8443",       "explicit port"),
                ("https://a@golfin.io",          "userinfo"),
                ("https://sub.golfin.io",        "no wildcard subdomains"),
                ("golfin.io/x",                  "no scheme"),
                ("javascript:alert(1)",          "not https"),
                ("",                             "empty"),
            };

            foreach (var (url, why) in table)
                Assert.IsFalse(BannerProd.IsLinkAllowed(url), $"Must reject ({why}): '{url}'");

            Assert.IsFalse(BannerProd.IsLinkAllowed(null), "Must reject null.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §3  Resolution ladder — the whole "which image, or none" decision
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class BannerResolutionLadderTests
    {
        private static readonly DateTime Now = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
        private const string En = "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/game-banners/home_promo-en-aaaaaaaaaaaa.jpg";
        private const string Ja = "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/game-banners/home_promo-ja-bbbbbbbbbbbb.jpg";

        [Test]
        public void English_player_gets_the_english_image()
        {
            Assert.AreEqual(En, BannerProd.Resolve(En, Ja, japanese: false, null, Now));
        }

        [Test]
        public void Japanese_player_gets_the_japanese_image()
        {
            Assert.AreEqual(Ja, BannerProd.Resolve(En, Ja, japanese: true, null, Now));
        }

        [Test]
        public void Japanese_player_falls_back_to_english_when_ja_is_absent()
        {
            Assert.AreEqual(En, BannerProd.Resolve(En, null, japanese: true, null, Now));
            Assert.AreEqual(En, BannerProd.Resolve(En, "",   japanese: true, null, Now),
                "An empty string is the same as absent — the column is nullable free text.");
        }

        [Test]
        public void English_player_falls_back_to_japanese_when_en_is_absent()
        {
            Assert.AreEqual(Ja, BannerProd.Resolve(null, Ja, japanese: false, null, Now));
        }

        [Test]
        public void Both_absent_means_no_banner()
        {
            Assert.IsNull(BannerProd.Resolve(null, null, japanese: false, null, Now));
            Assert.IsNull(BannerProd.Resolve(null, null, japanese: true,  null, Now));
            Assert.IsNull(BannerProd.Resolve("",   "",   japanese: false, null, Now));
        }

        [Test]
        public void Expiry_in_the_past_means_no_banner_even_with_art()
        {
            DateTime past = Now.AddMinutes(-1);
            Assert.IsNull(BannerProd.Resolve(En, Ja, japanese: false, past, Now),
                "A cached row whose window closed while the player was offline must be dropped.");
        }

        [Test]
        public void Expiry_is_exclusive_at_the_instant()
        {
            Assert.IsNull(BannerProd.Resolve(En, null, japanese: false, Now, Now),
                "now >= expires_at is the rule, matching the endpoint's end_at > now().");
        }

        [Test]
        public void Expiry_in_the_future_still_shows()
        {
            Assert.AreEqual(En, BannerProd.Resolve(En, null, japanese: false, Now.AddHours(1), Now));
        }

        [Test]
        public void No_expiry_never_expires()
        {
            Assert.AreEqual(En, BannerProd.Resolve(En, null, japanese: false, null, Now.AddYears(5)));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §4  Wire parsing
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class BannerWireParsingTests
    {
        [Test]
        public void Placement_strings_map_to_the_auto_served_slots()
        {
            Assert.AreEqual((true, "HomePromo"), BannerProd.TryParsePlacement("home_promo"));
            Assert.AreEqual((true, "Rankings"),  BannerProd.TryParsePlacement("rankings"));
            Assert.AreEqual((true, "Store"),     BannerProd.TryParsePlacement("store"));
        }

        [Test]
        public void Unknown_placement_is_refused_not_guessed()
        {
            // A newer dashboard may legitimately be ahead of this build; guessing a slot would
            // draw a banner in the wrong place.
            Assert.IsFalse(BannerProd.TryParsePlacement("home_banner").Matched);
            Assert.IsFalse(BannerProd.TryParsePlacement("HOME_PROMO").Matched, "Case-sensitive.");
            // The Store slot's object is named WinterSaleBanner and its screen class is
            // GeneralShopScreenController — neither is the wire value, and neither may be guessed.
            Assert.IsFalse(BannerProd.TryParsePlacement("shop").Matched);
            Assert.IsFalse(BannerProd.TryParsePlacement("STORE").Matched, "Case-sensitive.");
            Assert.IsFalse(BannerProd.TryParsePlacement("").Matched);
            Assert.IsFalse(BannerProd.TryParsePlacement(null).Matched);
        }

        [Test]
        public void Expires_at_parses_as_absolute_utc_regardless_of_machine_zone()
        {
            DateTime? utc = BannerProd.ParseUtc("2026-08-17T04:00:00+00:00");
            Assert.IsTrue(utc.HasValue);
            Assert.AreEqual(DateTimeKind.Utc, utc!.Value.Kind);
            Assert.AreEqual(new DateTime(2026, 8, 17, 4, 0, 0, DateTimeKind.Utc), utc.Value);

            // Same instant expressed in JST must land on the same UTC value.
            Assert.AreEqual(utc.Value, BannerProd.ParseUtc("2026-08-17T13:00:00+09:00")!.Value);

            // A bare timestamp is ASSUMED UTC, not local.
            Assert.AreEqual(utc.Value, BannerProd.ParseUtc("2026-08-17T04:00:00")!.Value);
        }

        [Test]
        public void Missing_or_unparseable_expires_at_is_no_expiry()
        {
            Assert.IsNull(BannerProd.ParseUtc(null));
            Assert.IsNull(BannerProd.ParseUtc(""));
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("expires_at"));
            Assert.IsNull(BannerProd.ParseUtc("whenever"));
        }

        [Test]
        public void Deserialize_accepts_both_the_raw_envelope_and_an_unwrapped_payload()
        {
            const string inner =
                "{\"fetched_at\":\"2026-08-17T04:00:00+00:00\",\"banners\":[" +
                "{\"placement\":\"home_promo\",\"image_url_en\":\"x\",\"image_url_ja\":null," +
                "\"link_url\":null,\"expires_at\":null}," +
                "{\"placement\":\"rankings\",\"image_url_en\":\"y\",\"image_url_ja\":null," +
                "\"link_url\":null,\"expires_at\":null}]}";

            // The disk cache holds the RAW body, still wrapped in {"data": …}.
            CollectionAssert.AreEqual(
                new[] { "home_promo", "rankings" },
                BannerProd.DeserializePlacements("{\"data\":" + inner + "}"));

            // A live fetch has already been unwrapped by ApiEnvelope.
            CollectionAssert.AreEqual(
                new[] { "home_promo", "rankings" },
                BannerProd.DeserializePlacements(inner));
        }

        [Test]
        public void Deserialize_leaves_expires_at_as_the_exact_characters_the_server_sent()
        {
            // Typing this field as DateTime would let Newtonsoft hand back a LOCAL time and give
            // two players in different zones different behaviour. DateParseHandling.None is what
            // keeps ParseUtc the only place a timestamp is interpreted.
            const string json =
                "{\"data\":{\"fetched_at\":\"2026-08-17T04:00:00+00:00\",\"banners\":[" +
                "{\"placement\":\"home_promo\",\"expires_at\":\"2026-09-01T00:00:00+00:00\"}]}}";

            Assert.AreEqual("2026-09-01T00:00:00+00:00", BannerProd.DeserializeFirstExpiresAt(json));
        }

        [Test]
        public void Malformed_payload_yields_nothing_rather_than_throwing()
        {
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex("Could not parse"));
            CollectionAssert.IsEmpty(BannerProd.DeserializePlacements("{ not json"));
        }

        [Test]
        public void Empty_banner_array_is_a_healthy_response()
        {
            CollectionAssert.IsEmpty(BannerProd.DeserializePlacements(
                "{\"data\":{\"fetched_at\":\"2026-08-17T04:00:00+00:00\",\"banners\":[]}}"));
        }
    }
}
