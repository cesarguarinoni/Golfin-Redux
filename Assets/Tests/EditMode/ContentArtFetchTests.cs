// Assets/Tests/EditMode/ContentArtFetchTests.cs
// content_art_bundling — the two pieces of ContentArtFetcher that decide what lands in the repo
// and are silently wrong when they drift: the DERIVED NAME (§4) and the CSV SPLICE (§3.8).
//
// ASSEMBLY: GolfinRedux.Tests.EditMode (asmdef, autoReferenced:false). An asmdef cannot reference
// a PREDEFINED assembly, and ContentArtFetcher lives in Assembly-CSharp-Editor because SPEC §3.1
// requires it to call CatalogArtPolicy.IsArtAllowed directly (Assembly-CSharp). So the shipping
// type is reached by reflection — the same technique, and the same reason, as
// ContentRenderableTests: this drives the SHIPPING derivation table, not a copy of its rules
// living in the test.
//
// WHAT IS *NOT* HERE, AND WHY. The refusals (allowlist, WebP, the 500 KB cap, collision, empty
// folder) and the ladder handover are proven by RUNNING the tool against the real CSVs and the
// live catalog-art bucket — see IMPLEMENTER_REPORT § Acceptance. A unit test for them would have
// to mutate the shipped CSVs and stub the network, i.e. it would prove a fixture, and
// PIPELINE_HARDENING §21 exists because that is exactly how content_art_urls shipped a feature
// that never worked. These two are here because they are pure, and because a wrong NAME is the
// one failure that produces a plausible-looking asset in the wrong place.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class ContentArtFetchTests
    {
        const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        const BindingFlags Instanced = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        Type _fetcher;
        IList _catalogs;

        [OneTimeSetUp]
        public void FindType()
        {
            _fetcher = Type.GetType("Golfin.EditorTools.ContentArtFetcher, Assembly-CSharp-Editor");
            Assert.NotNull(_fetcher, "ContentArtFetcher not found in Assembly-CSharp-Editor.");

            _catalogs = (IList)_fetcher.GetField("Catalogs", Statics).GetValue(null);
            Assert.NotNull(_catalogs, "ContentArtFetcher.Catalogs not found.");
        }

        // ── Naming (SPEC §4) ────────────────────────────────────────────────

        /// <summary>Runs the SHIPPING derivation for one (catalog, url column) against a row.</summary>
        string Derive(string catalog, string urlColumn, Dictionary<string, string> row)
        {
            foreach (object spec in _catalogs)
            {
                if ((string)spec.GetType().GetField("Name", Instanced).GetValue(spec) != catalog) continue;

                var slots = (IEnumerable)spec.GetType().GetField("Slots", Instanced).GetValue(spec);
                foreach (object slot in slots)
                {
                    Type st = slot.GetType();
                    if ((string)st.GetField("UrlColumn", Instanced).GetValue(slot) != urlColumn) continue;

                    var derive = (Delegate)st.GetField("Derive", Instanced).GetValue(slot);
                    Func<string, string> field = c => row.TryGetValue(c, out string v) ? v : "";
                    return (string)derive.DynamicInvoke(field);
                }
            }
            Assert.Fail($"No slot for {catalog}.{urlColumn}");
            return null;
        }

        string Folder(string catalog, string urlColumn)
        {
            foreach (object spec in _catalogs)
            {
                if ((string)spec.GetType().GetField("Name", Instanced).GetValue(spec) != catalog) continue;
                var slots = (IEnumerable)spec.GetType().GetField("Slots", Instanced).GetValue(spec);
                foreach (object slot in slots)
                {
                    Type st = slot.GetType();
                    if ((string)st.GetField("UrlColumn", Instanced).GetValue(slot) != urlColumn) continue;
                    return (string)st.GetField("Folder", Instanced).GetValue(slot);
                }
            }
            Assert.Fail($"No slot for {catalog}.{urlColumn}");
            return null;
        }

        // Every expectation below is a name that ALREADY EXISTS in Resources/, read off the folder
        // — so the rule is checked against the shipped art, not against itself.

        [Test]
        public void Characters_DeriveTheShippedThumbnailAndFullBodyNames()
        {
            var james = new Dictionary<string, string> { ["id"] = "char_james", ["name"] = "James" };
            Assert.AreEqual("James", Derive("characters", "portraitUrl", james),
                "Portraits/Thumbnails/James.png is the shipped file.");
            Assert.AreEqual("BigRosterJames", Derive("characters", "fullUrl", james),
                "Portraits/FullBody/BigRosterJames.png is the shipped file.");

            // The case the rule EXISTS for: an admin-created row with no art anywhere.
            var zoe = new Dictionary<string, string> { ["id"] = "char_zoe", ["name"] = "Zoe" };
            Assert.AreEqual("Zoe", Derive("characters", "portraitUrl", zoe));
            Assert.AreEqual("BigRosterZoe", Derive("characters", "fullUrl", zoe));
        }

        [Test]
        public void Items_DeriveNameDashRarity_WhichTheIdCannotProduce()
        {
            var kit = new Dictionary<string, string>
            {
                ["id"] = "repairkit_common", ["name"] = "Repair Kit", ["rarity"] = "Common",
            };
            // Items/Thumbnails/RepairKit-Common.png and Items/Full/RepairKit-Common.png both ship.
            Assert.AreEqual("RepairKit-Common", Derive("items", "thumbnailUrl", kit));
            Assert.AreEqual("RepairKit-Common", Derive("items", "fullUrl", kit));

            kit["id"] = "repairkit_rare";
            kit["rarity"] = "Rare";
            Assert.AreEqual("RepairKit-Rare", Derive("items", "thumbnailUrl", kit),
                "Items/Thumbnails/RepairKit-Rare.png ships. The id 'repairkit_rare' could not " +
                "produce this name — that is why the rule reads the row's own columns.");
        }

        [Test]
        public void Balls_OmitTheRaritySuffix_BecauseBallsCsvHasNoRarityColumn()
        {
            // Balls.csv columns: id,name,brand,power,…  — there is NO rarity column, and the two
            // shipped names are bare Pascal(name). One rule, both catalogs.
            var putt = new Dictionary<string, string> { ["id"] = "ball_putt_ace", ["name"] = "Putt Ace" };
            Assert.AreEqual("PuttAce", Derive("balls", "thumbnailUrl", putt),
                "Balls/Thumbnails/PuttAce.png is the shipped file — no '-' suffix.");
            Assert.AreEqual("PuttAce", Derive("balls", "fullUrl", putt));

            var golfin = new Dictionary<string, string> { ["id"] = "ball_golfin", ["name"] = "Golfin" };
            Assert.AreEqual("Golfin", Derive("balls", "thumbnailUrl", golfin));
        }

        [Test]
        public void Clubs_EachFolderKeepsItsOwnPrefix()
        {
            var fairloft = new Dictionary<string, string>
            {
                ["id"] = "club_awedge_fairloft_common", ["type"] = "A.Wedge", ["brand"] = "FAIRLOFT",
            };

            // These three are the names the 792 generated rows carry, verbatim from Clubs.csv.
            Assert.AreEqual("S_Menu_Wedge_FAIRLOFT", Derive("clubs", "portraitUrl", fairloft));
            Assert.AreEqual("Wedge-Fairloft", Derive("clubs", "fullUrl", fairloft));
            Assert.AreEqual("S_Controls_Wedge_FAIRLOFT", Derive("clubs", "controlUrl", fairloft));

            // The folder each lands in — a bare "Wedge-Fairloft" in Clubs/Controls would be the
            // only one of 78 files there without the S_Controls_ prefix (Architect correction 1).
            Assert.AreEqual("Clubs/Portraits", Folder("clubs", "portraitUrl"));
            Assert.AreEqual("Clubs/Full", Folder("clubs", "fullUrl"));
            Assert.AreEqual("Clubs/Controls", Folder("clubs", "controlUrl"));
        }

        [Test]
        public void Clubs_AllSixRaritiesOfOneBrandAndTypeDeriveToTheSameName()
        {
            // §9 answer 1: shared art across rarities IS the intent, so the six rows must collapse
            // onto ONE derived name — that is what makes the de-dup in TryFetchOne possible at all.
            string[] rarities = { "Common", "Uncommon", "Rare", "Mythic", "Legendary", "Supreme" };
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (string rarity in rarities)
            {
                names.Add(Derive("clubs", "portraitUrl", new Dictionary<string, string>
                {
                    ["id"] = $"club_driver_fairloft_{rarity.ToLowerInvariant()}",
                    ["type"] = "Driver", ["brand"] = "FAIRLOFT", ["rarity"] = rarity,
                }));
            }

            Assert.AreEqual(1, names.Count,
                "Six rarity rows must derive to one name (got: " + string.Join(", ", names) + ")");
        }

        [Test]
        public void Clubs_BrandPunctuationSurvivesFullAndIsStrippedFromTheTag()
        {
            var gf = new Dictionary<string, string>
            {
                ["id"] = "club_driver_gf", ["type"] = "Driver", ["brand"] = "G&F",
            };
            Assert.AreEqual("Driver-G&F", Derive("clubs", "fullUrl", gf),
                "Clubs/Full/Driver-G&F.png is the shipped file — the ampersand is part of the name.");
            Assert.AreEqual("S_Menu_Driver_GF", Derive("clubs", "portraitUrl", gf),
                "The S_Menu_/S_Controls_ tag is alphanumerics only, upper-cased.");

            var royal = new Dictionary<string, string>
            {
                ["id"] = "club_pwedge_royal", ["type"] = "P.Wedge", ["brand"] = "Royal Swing",
            };
            Assert.AreEqual("Wedge-RoyalSwing", Derive("clubs", "fullUrl", royal),
                "Spaces are dropped and each word title-cased — generate_clubs.py's .title().");
        }

        [Test]
        public void DerivationIsDeterministic_WhichIsWhatMakesARerunANoOp()
        {
            var row = new Dictionary<string, string>
            {
                ["id"] = "char_zoe", ["name"] = "Zoe",
            };
            string first = Derive("characters", "portraitUrl", row);
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(first, Derive("characters", "portraitUrl", row),
                    "A second run must derive the same name, or it would fetch the asset again " +
                    "under a new name instead of finding the CSV already filled in.");
        }

        // ── CSV splice (SPEC §3.8) ──────────────────────────────────────────
        //
        // "git status shows exactly those two changes plus the .meta" (§7) is only true if the
        // write touches ONE FIELD. Re-serialising the row would re-quote fields the tool never
        // looked at and bury the signal.

        List<object> Spans(string line) =>
            new List<object>(
                (IEnumerable<object>)ToObjects(
                    _fetcher.GetMethod("ParseCsvSpans", Statics).Invoke(null, new object[] { line })));

        static IEnumerable<object> ToObjects(object list)
        {
            foreach (object o in (IEnumerable)list) yield return o;
        }

        string SetField(string line, int column, string value)
        {
            object spans = _fetcher.GetMethod("ParseCsvSpans", Statics).Invoke(null, new object[] { line });
            return (string)_fetcher.GetMethod("SetField", Statics)
                .Invoke(null, new object[] { line, spans, column, value });
        }

        [Test]
        public void Splice_FillsAnEmptyFieldAndTouchesNothingElse()
        {
            // A real Characters.csv shape: quoted bio with commas, then the two URL columns.
            const string line =
                "char_zoe,Zoe,Vale,Rare,7,6,5,7,,,80,119,\"Out of nowhere, and fast.\",0,https://x/y.png,";

            string after = SetField(line, 8, "Zoe");

            Assert.AreEqual(
                "char_zoe,Zoe,Vale,Rare,7,6,5,7,Zoe,,80,119,\"Out of nowhere, and fast.\",0,https://x/y.png,",
                after);
            Assert.AreEqual(line.Length + 3, after.Length, "Only the inserted name changed length.");
            Assert.IsTrue(after.Contains("\"Out of nowhere, and fast.\""),
                "The quoted bio must survive byte-for-byte — re-serialising is what would break it.");
        }

        [Test]
        public void Splice_FillsTheLastFieldOnTheLine()
        {
            const string line = "ball_zap,Zap,Golfin,0,0,0,0,0,,,\"Fast.\",https://x/y.png,";
            // fullSprite is index 9; but exercise the true tail too — index 12 is the last field.
            Assert.AreEqual(line + "TAIL", SetField(line, 12, "TAIL"));
        }

        [Test]
        public void Splice_KeepsATrailingCarriageReturnInsideTheLastField()
        {
            // Split('\n') leaves \r on the line. It must not be clipped or duplicated.
            const string line = "a,b,\r";
            Assert.AreEqual("a,b,X\r", SetField(line, 2, "X"),
                "The \\r belongs to the last field's raw span and must ride along.");
        }

        [Test]
        public void Splice_IsANoOpForAnOutOfRangeColumn()
        {
            const string line = "a,b,c";
            Assert.AreEqual(line, SetField(line, 9, "X"));
            Assert.AreEqual(line, SetField(line, -1, "X"));
        }

        [Test]
        public void ParseSpans_HandlesQuotedCommasAndEscapedQuotes()
        {
            var spans = Spans("a,\"b,c\",\"say \"\"hi\"\"\",d");
            Assert.AreEqual(4, spans.Count);

            Type t = spans[0].GetType();
            string Value(int i) => (string)t.GetField("Value", Instanced).GetValue(spans[i]);

            Assert.AreEqual("a", Value(0));
            Assert.AreEqual("b,c", Value(1));
            Assert.AreEqual("say \"hi\"", Value(2));
            Assert.AreEqual("d", Value(3));
        }

        // ── The shared report file (SPEC §6) ────────────────────────────────

        [Test]
        public void TheFetchLogMarkerIsTheOneTheValidatorPreserves()
        {
            // ContentArtValidator.WriteReport carries everything from this marker to EOF forward
            // when it rewrites content_art.txt. If the two ever name different strings, the fetch
            // log survives exactly until the next build and nobody notices.
            string marker = (string)_fetcher.GetField("LogMarker", Statics).GetValue(null);
            Assert.IsNotEmpty(marker);

            Type validator = Type.GetType("Golfin.EditorTools.ContentArtValidator, Assembly-CSharp-Editor");
            Assert.NotNull(validator);

            string source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName,
                    "Assets/Editor/ContentArtValidator.cs"));
            Assert.IsTrue(source.Contains("ContentArtFetcher.LogMarker"),
                "ContentArtValidator must reference the fetcher's marker constant, never a copy " +
                "of the string — a copy is how the two drift apart silently.");
        }

        // ── Collision (SPEC §4) ─────────────────────────────────────────────
        //
        // "Collision is a REFUSAL, never an overwrite." ExistingAsset is the ONLY gate in front of
        // File.WriteAllBytes, and the dev/CI filesystem is case-insensitive APFS — so a comparison
        // that is case-SENSITIVE is not a gate at all for a case-variant name: the guard passes,
        // the write lands, and APFS replaces the existing file's bytes while KEEPING its original
        // name, .meta and GUID. Silent asset replacement with no rename and no new file.
        //
        // Found by the red-team gate 2026-08-28. Both earlier gates tested collision with a
        // same-case example (char_JAMES → "James"), the one input where Ordinal and the filesystem
        // agree, so both passed it.

        /// <summary>Invokes the shipping private <c>ExistingAsset</c>.</summary>
        string ExistingAsset(string folder, string name)
        {
            string root = System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return (string)_fetcher.GetMethod("ExistingAsset", Statics)
                .Invoke(null, new object[] { root, folder, name });
        }

        [Test]
        public void Collision_IsDetectedRegardlessOfCase()
        {
            // Portraits/Thumbnails/James.png ships. All three spellings name the SAME file on a
            // case-insensitive filesystem, so all three must be refused.
            Assert.IsNotNull(ExistingAsset("Portraits/Thumbnails", "James"),
                "The exact name must collide — if this fails the whole guard is broken.");

            Assert.IsNotNull(ExistingAsset("Portraits/Thumbnails", "james"),
                "A LOWER-CASE variant did not collide with the shipped James.png. On APFS the " +
                "write would then replace that file's bytes while keeping its name, .meta and " +
                "GUID — an artist's asset silently swapped. SPEC §4: never an overwrite.");

            Assert.IsNotNull(ExistingAsset("Portraits/Thumbnails", "JAMES"),
                "An UPPER-CASE variant did not collide either.");
        }

        [Test]
        public void Collision_CaseVariantIsReachableFromTheRealNamingRules()
        {
            // Not theoretical. BrandPascal lower-cases interior letters, so the hand-dropped
            // Clubs/Full/Driver-FairX.png in the tree today derives as "Driver-Fairx".
            var fairx = new Dictionary<string, string>
            {
                ["id"] = "club_driver_fairx", ["type"] = "Driver", ["brand"] = "FairX",
            };
            Assert.AreEqual("Driver-Fairx", Derive("clubs", "fullUrl", fairx),
                "If this ever equals 'Driver-FairX' the reachability argument changes, but the " +
                "guard must stay case-insensitive regardless.");
        }

        [Test]
        public void Collision_ANameNothingResolvesTo_IsNotAFalsePositive()
        {
            // The guard must not refuse everything — that would be a different way to be broken.
            Assert.IsNull(ExistingAsset("Portraits/Thumbnails", "NoSuchPortrait_content_art_bundling"));
        }

        [Test]
        public void TheDownloadCeilingIsTheUploadCap_NotTheClientBackstop()
        {
            // Architect correction 2: 500 KB (contentArtMutations.ts CATALOG_ART_SPEC.maxBytes),
            // NOT the client's 1 MB backstop. A file larger than the upload cap did not come
            // through the admin's upload path, and that alone is a reason to refuse it.
            int max = (int)_fetcher.GetField("MaxBytes", Statics).GetValue(null);
            Assert.AreEqual(500 * 1024, max);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // The handover itself (SPEC §7, acceptance 8: "the whole point of the task")
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// After the art is bundled, the row must resolve through §2.2 <b>rule 2</b> — the build's own
    /// sprite — and not through a URL rung. Otherwise the asset this whole task exists to produce
    /// lands in the build and is never used: every player keeps paying for the network fetch, the
    /// added megabytes buy nothing, and NOTHING SAYS SO.
    ///
    /// <para>
    /// This is the shape that was actually broken. The four loaders defaulted "the bundled row's
    /// URL" to <c>""</c> when there was no overlay to compare against, so a bundled row carrying a
    /// URL compared its own URL against <c>""</c> — always "different" — and rule 1 served the
    /// cached download in front of the bundled sprite. Observed live on 2026-08-28 against the real
    /// <c>catalog-art</c> bucket before the fix (report § Ladder handover).
    /// </para>
    ///
    /// <para>
    /// THE CACHE IS DELIBERATELY WARM. With a cold cache every rung but 2 returns null anyway and
    /// the assertion would pass on a loader that had no ordering at all.
    /// </para>
    /// </summary>
    [TestFixture]
    public class ContentArtLadderHandoverTests
    {
        const BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        const BindingFlags Stat = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        // Allowlisted so CatalogArtPolicy.IsArtAllowed says yes; never fetched, only hashed.
        const string Url =
            "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/catalog-art/" +
            "characters-char_handover-portraitUrl-00000000ffff.png";

        const string Header =
            "id,name,lastName,rarity,baseStrength,baseClubControl,baseRecovery,baseStamina," +
            "portraitSprite,portraitFull,startLevel,maxLevel,bio,starterCandidate,portraitUrl,fullUrl";

        Type _db;
        string _cacheFile = "";
        GameObject _go;

        [SetUp]
        public void WarmTheCache()
        {
            _db = Type.GetType("Golfin.Roster.CharacterDatabaseCSV, Assembly-CSharp");
            Assert.NotNull(_db);

            // A real PNG, encoded here so the fixture is owned by the test — and 8x8, so it is
            // trivially distinguishable from the 170x343 shipped portrait.
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var px = new Color32[64];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(9, 9, 9, 255);
            tex.SetPixels32(px); tex.Apply();
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            // TournamentArtService / TournamentArtPolicy / CatalogArtCache all live in
            // Assembly-CSharp (TournamentsRuntime has no asmdef, and CatalogArtPolicy is
            // deliberately outside Golfin.Content — see its header). An asmdef cannot reference a
            // PREDEFINED assembly, so they are reached the same way ContentArtFetcher is.
            var svcType = Type.GetType("Golfin.Tournaments.TournamentArtService, Assembly-CSharp");
            var policyType = Type.GetType("Golfin.Tournaments.TournamentArtPolicy, Assembly-CSharp");
            Assert.NotNull(svcType); Assert.NotNull(policyType);

            object svc = svcType.GetProperty("CatalogArt", Stat).GetValue(null);
            string dir = (string)svcType.GetProperty("CacheDir", Inst).GetValue(svc);
            string file = (string)policyType.GetMethod("CacheFileName", Stat)
                .Invoke(null, new object[] { Url });

            System.IO.Directory.CreateDirectory(dir);
            _cacheFile = System.IO.Path.Combine(dir, file);
            System.IO.File.WriteAllBytes(_cacheFile, png);

            ResetAll();
        }

        [TearDown]
        public void Cleanup()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (!string.IsNullOrEmpty(_cacheFile) && System.IO.File.Exists(_cacheFile))
                System.IO.File.Delete(_cacheFile);
            ResetAll();
        }

        static void ResetAll()
        {
            Golfin.Content.ContentCatalogStore.Clear();
            Golfin.Content.ContentSpriteGuard.ResetForTest();
            Type.GetType("Golfin.CatalogArt.CatalogArtCache, Assembly-CSharp")
                .GetMethod("ResetForTest", Stat).Invoke(null, null);
        }

        /// <summary>Install a one-row overlay patch for char_handover.</summary>
        static void Overlay(params (string column, string value)[] columns)
        {
            var data = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var c in columns) data[c.column] = c.value;
            Golfin.Content.ContentCatalogStore.ConfigureForTest(
                new Golfin.Content.ContentCatalog(
                    Golfin.Content.ContentCatalogs.Characters, 1, true,
                    new List<Golfin.Content.ContentRow> {
                        new Golfin.Content.ContentRow("char_handover", true, 0, data) }));
        }

        object LoadRow(string row, string id)
        {
            _go = new GameObject("LadderHandoverProbe");
            _go.SetActive(false);
            var db = _go.AddComponent(_db);
            _db.GetField("charactersCSV", Inst).SetValue(db, new TextAsset(Header + "\n" + row + "\n"));
            _db.GetMethod("LoadCharactersFromCSV", Inst).Invoke(db, null);

            var all = (IEnumerable)_db.GetMethod("GetAllCharacters", Inst).Invoke(db, null);
            foreach (object c in all)
                if ((string)c.GetType().GetField("characterId").GetValue(c) == id) return c;
            return null;
        }

        static UnityEngine.Sprite SpriteOf(object row) =>
            (UnityEngine.Sprite)row.GetType().GetField("portraitSprite").GetValue(row);

        [Test]
        public void BundledSpriteWins_EvenWhenTheRowsOwnUrlIsCached()
        {
            // portraitSprite = James (ships), portraitUrl = an allowlisted URL whose art IS cached.
            string row = "char_handover,Hand,Over,Rare,7,6,5,7,James,BigRosterJames,80,119," +
                         "\"handover fixture\",0," + Url + ",";

            object parsed = LoadRow(row, "char_handover");
            Assert.NotNull(parsed, "The row was dropped entirely.");

            var sprite = SpriteOf(parsed);
            Assert.NotNull(sprite, "Nothing resolved at all.");

            string path = UnityEditor.AssetDatabase.GetAssetPath(sprite);
            Assert.AreEqual("Assets/Resources/Portraits/Thumbnails/James.png", path,
                "Rule 2 lost to a URL rung. The bundled sprite must win whenever the overlay has " +
                "not changed the URL — a bundled row has no overlay, so nothing has changed. " +
                "A cached-URL sprite is created at runtime and has NO asset path, which is how " +
                "this assertion tells the two apart.");
            Assert.AreNotEqual(8, sprite.texture.width,
                "Resolved the 8x8 cache fixture — that is the cached URL, not the bundled art.");
        }

        [Test]
        public void ARealReuploadStillTakesTheUrlRung()
        {
            // The behaviour the fix must NOT break: the overlay names a DIFFERENT URL than the
            // bundled CSV, i.e. art was re-uploaded after this build was cut. Rule 1 must fire even
            // though the bundled sprite resolves.
            string row = "char_handover,Hand,Over,Rare,7,6,5,7,James,BigRosterJames,80,119," +
                         "\"handover fixture\",0,https://wmszyghwwkaptgqdunel.supabase.co/storage/" +
                         "v1/object/public/catalog-art/characters-char_handover-portraitUrl-0000000000aa.png,";

            Overlay(("portraitUrl", Url));

            object parsed = LoadRow(row, "char_handover");
            Assert.NotNull(parsed);

            var sprite = SpriteOf(parsed);
            Assert.NotNull(sprite);
            Assert.AreEqual(8, sprite.texture.width,
                "Rule 1 did not fire on a genuine re-upload — the overlay's URL differs from the " +
                "bundled one, so the newer art must win over the build's own sprite.");
        }

        [Test]
        public void OldBuild_NameItDoesNotHave_StillRendersFromTheUrl()
        {
            // The OLD-BUILD half (Architect correction 3), and the case most likely to regress:
            // this build's CSV has no sprite name, the overlay publishes one this build does NOT
            // carry, and the row must still render — ContentSpriteGuard lets it through on
            // SpriteRef.HasRemote rather than vetoing the overlay.
            string row = "char_handover,Hand,Over,Rare,7,6,5,7,,,80,119,\"handover fixture\",0," + Url + ",";

            Overlay(("portraitSprite", "NameThisBuildDoesNotHave_content_art_bundling"));

            object parsed = LoadRow(row, "char_handover");
            Assert.NotNull(parsed, "The row was dropped.");

            var sprite = SpriteOf(parsed);
            Assert.NotNull(sprite,
                "The row was withheld. A build that predates the bundled asset receives the " +
                "published NAME it does not have, and must fall through to the cached URL — that " +
                "is why content_art_urls §2.2 keeps the URL after bundling.");
            Assert.AreEqual(8, sprite.texture.width, "Rendered something other than the cached art.");
            Assert.IsTrue((bool)parsed.GetType().GetField("renderable").GetValue(parsed));
        }
    }

}
