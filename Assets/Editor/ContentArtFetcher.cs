// ─────────────────────────────────────────────────────────────────────────────
// ContentArtFetcher — "the admin informs the next build" (content_art_bundling)
//
// `content_art_urls` closes the gap between a row EXISTING and its art shipping:
// the row renders by URL until a build bundles the asset, and its §2.2 ladder
// then hands the row back to the bundled sprite automatically. What it does not
// do is get the art INTO the build — that was still a human downloading a file,
// naming it, dropping it in Resources/ and setting a column.
//
// This is that step, done once, correctly, and reviewably.
//
// IT CHANGES NOTHING A PLAYER SEES. Every behaviour is already specified by
// content_art_urls §2.2; this only makes rule 2 (bundled sprite by name) start
// applying to a row sooner, by putting the asset where rule 2 can find it.
//
// ── NOT IN THE BUILD LANE (SPEC §2) ─────────────────────────────────────────
// TESTFLIGHT_RUNBOOK.md already settles this for the exporter and the reasoning
// transfers verbatim: "An export inside the lane would bake CSV changes into a
// build whose COMMIT does not contain them, and the build number IS the commit
// count." Downloading art into Assets/ at build time has exactly that defect and
// additionally dirties the tree fastlane's `ensure_git_status_clean` guards.
//
// So: a HUMAN-RUN step that produces a REVIEWABLE GIT DIFF, shaped like
// export_content.py — you run it, you look at what it did, you commit it, and
// the build that follows carries it.
//
// It runs in the EDITOR, not as a Python tool, because SPEC §1 decision 2
// requires TextureImporter work only Unity can do correctly. Hand-writing .meta
// files to avoid opening the Editor is the obvious shortcut and it is FORBIDDEN
// here: import settings are the part most likely to be silently wrong, and a
// hand-rolled .meta is how they get that way. This project's
// m_DefaultBehaviorMode is 0 (Mode3D), so a raw PNG dropped into Resources/
// imports as textureType Default — and `Resources.Load<Sprite>` on a Default
// texture returns NULL. The asset would be in the build and still invisible.
//
// NO SUPABASE CREDENTIALS. It reads the repo CSVs (which the exporter has
// already filled in) and fetches over plain HTTPS from the public bucket. The
// service key is not needed and must not be required.
//
// ── WHAT IT DOES NOT DO ─────────────────────────────────────────────────────
// It writes no catalog rows and talks to no admin API. The CSV now carries a
// name the catalog does not, which is precisely the drift import_content.py
// exists to resolve — so the closing instruction it prints is: run
// `import_content.py --apply`, publish, then export. That reuses the loop
// content_two_way built and tested rather than inventing a second way in.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Golfin.CatalogArt;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;

namespace Golfin.EditorTools
{
    public static class ContentArtFetcher
    {
        const string Tag = "[ContentArtFetch]";
        const string ReportPath = "Docs/Reports/content_art.txt";

        /// <summary>
        /// The header the fetch log is filed under. <see cref="ContentArtValidator"/> PRESERVES
        /// everything from the first occurrence of this line to EOF when it rewrites the report,
        /// so the two tools share one file (SPEC §6) instead of one silently erasing the other.
        /// </summary>
        public const string LogMarker = "── fetched art (GOLFIN/Content/Fetch URL Art)";

        /// <summary>
        /// THE UPLOAD CAP (contentArtMutations.ts `CATALOG_ART_SPEC.maxBytes`), not the client's
        /// 1 MB backstop — SPEC §3.3 / §10.2. A file larger than this did not come through the
        /// admin's upload path, and that alone is a reason to refuse it.
        /// </summary>
        public const int MaxBytes = 500 * 1024;

        const int TimeoutSeconds = 30;

        // ── Catalog wiring ──────────────────────────────────────────────────
        //
        // Folder literals are COPIES of the loader constants, cited so a rename there is
        // findable from here — the same arrangement, for the same reason, as ContentArtValidator:
        //   Portraits/Thumbnails, Portraits/FullBody      CharacterDatabaseCSV.cs:38-39
        //   Items/Thumbnails,     Items/Full              ItemDatabaseCSV.cs:27-28
        //   Balls/Thumbnails,     Balls/Full              BallDatabaseCSV.cs:26-27
        //   Clubs/Portraits, Clubs/Full, Clubs/Controls   ClubDatabaseCSV.cs:41-43
        //
        // URL column → sprite-NAME column pairing is the one in
        // Tools/admin-dashboard/lib/contentView.ts (ART_URL_COLUMNS + SPRITE_FIELD_FOLDER).

        /// <summary>Row fields by column name, for the naming rules below.</summary>
        public delegate string NameRule(Func<string, string> field);

        sealed class ArtSlot
        {
            public readonly string UrlColumn;
            public readonly string NameColumn;
            public readonly string Folder;
            public readonly NameRule Derive;

            public ArtSlot(string urlColumn, string nameColumn, string folder, NameRule derive)
            {
                UrlColumn = urlColumn;
                NameColumn = nameColumn;
                Folder = folder;
                Derive = derive;
            }
        }

        sealed class CatalogSpec
        {
            public readonly string Name;
            public readonly string CsvPath;
            public readonly string IdColumn;
            public readonly ArtSlot[] Slots;

            public CatalogSpec(string name, string csvPath, string idColumn, params ArtSlot[] slots)
            {
                Name = name;
                CsvPath = csvPath;
                IdColumn = idColumn;
                Slots = slots;
            }
        }

        // ── Naming (SPEC §4) ────────────────────────────────────────────────
        //
        // Derive to match the convention the TARGET FOLDER already uses — the Resources Path /
        // CSV Column / Naming Rule table in ASSET_NAMING_CONVENTION.md §5 — NOT the S_Char_* /
        // S_Club_* SOURCE-ART patterns of its §3, which belong to Assets/Art. A `S_Char_Zoe` in
        // Portraits/Thumbnails would be the only file in the folder not following the folder's
        // own rule (Architect correction 1, 2026-08-27).
        //
        // Deterministic + unique is the requirement; matching a hand-made name byte-for-byte is
        // NOT. Deterministic also means RE-RUNNING PRODUCES THE SAME NAME, which is what makes a
        // second run a safe no-op.

        static readonly CatalogSpec[] Catalogs =
        {
            // characters — Portraits/Thumbnails/{FirstName}, Portraits/FullBody/BigRoster{FirstName}.
            // Verified against the folder: Thumbnails/Camila.png, FullBody/BigRosterCamila.png.
            // FirstName = Pascal(id minus "char_"); every one of the 12 shipped rows agrees
            // (char_james → James → portraitSprite "James").
            new CatalogSpec("characters", "Assets/Data/Characters.csv", "id",
                new ArtSlot("portraitUrl", "portraitSprite", "Portraits/Thumbnails",
                    f => FirstName(f("id"))),
                new ArtSlot("fullUrl", "portraitFull", "Portraits/FullBody",
                    f => "BigRoster" + FirstName(f("id")))),

            // items — {Pascal(name)}-{rarity}. The existing names (RepairKit-Common) are NOT
            // derivable from the id (repairkit_common), so the rule reads the row's own name and
            // rarity columns. Added to ASSET_NAMING_CONVENTION.md §5 in the same commit.
            new CatalogSpec("items", "Assets/Data/Items.csv", "id",
                new ArtSlot("thumbnailUrl", "thumbnailSprite", "Items/Thumbnails",
                    f => RarityQualified(f("name"), f("rarity"))),
                new ArtSlot("fullUrl", "fullSprite", "Items/Full",
                    f => RarityQualified(f("name"), f("rarity")))),

            // balls — the same rule. Balls.csv has NO rarity column, and the shipped names are
            // bare Pascal(name): ball_putt_ace / "Putt Ace" → "PuttAce". So the rule is
            // {Pascal(name)}-{rarity} with the suffix OMITTED when the row carries no rarity —
            // one rule that reproduces both folders exactly (see IMPLEMENTER_REPORT § naming).
            new CatalogSpec("balls", "Assets/Data/Balls.csv", "id",
                new ArtSlot("thumbnailUrl", "thumbnailSprite", "Balls/Thumbnails",
                    f => RarityQualified(f("name"), f("rarity"))),
                new ArtSlot("fullUrl", "fullSprite", "Balls/Full",
                    f => RarityQualified(f("name"), f("rarity")))),

            // clubs — PER FOLDER, exactly as the 799 rows do (Tools/club-gen/generate_clubs.py
            // lines 141-143):
            //     portraitSprite  S_Menu_{ArtType}_{BRANDTAG}       Clubs/Portraits
            //     portraitFull    {ArtType}-{BrandPascal}           Clubs/Full
            //     controlSprite   S_Controls_{ArtType}_{BRANDTAG}   Clubs/Controls
            // The SPEC's shorthand "{Type}-{Brand}" is the Clubs/Full rule; applying it to
            // Clubs/Controls would put the only non-S_Controls_* file in a folder where all 78
            // existing files carry that prefix — which is the very defect Architect correction 1
            // called out. Match the folder, per slot.
            //
            // Art shared across rarities is the INTENT (§9 answer 1): six rarity rows of the same
            // brand × type derive to the SAME name, are de-duplicated below, and are fetched once.
            new CatalogSpec("clubs", "Assets/Resources/Data/Clubs.csv", "id",
                new ArtSlot("portraitUrl", "portraitSprite", "Clubs/Portraits",
                    f => $"S_Menu_{ClubArtType(f("type"))}_{BrandTag(f("brand"))}"),
                new ArtSlot("fullUrl", "portraitFull", "Clubs/Full",
                    f => $"{ClubArtType(f("type"))}-{BrandPascal(f("brand"))}"),
                new ArtSlot("controlUrl", "controlSprite", "Clubs/Controls",
                    f => $"S_Controls_{ClubArtType(f("type"))}_{BrandTag(f("brand"))}")),
        };

        /// <summary>`char_zoe` → `Zoe`. The id minus its `char_` prefix, Pascal-cased.</summary>
        static string FirstName(string id)
        {
            string bare = id.StartsWith("char_", StringComparison.Ordinal) ? id.Substring(5) : id;
            return Pascal(bare);
        }

        /// <summary>`("Repair Kit", "Rare")` → `RepairKit-Rare`; `("Putt Ace", "")` → `PuttAce`.</summary>
        static string RarityQualified(string name, string rarity)
        {
            string b = Pascal(name);
            string r = Pascal(rarity);
            return string.IsNullOrEmpty(r) ? b : $"{b}-{r}";
        }

        /// <summary>The three wedges share one art set; every other type is itself
        /// (generate_clubs.py: <c>art_type = "Wedge" if wedge else tdisp</c>).</summary>
        static string ClubArtType(string type) =>
            type == "A.Wedge" || type == "P.Wedge" || type == "S.Wedge" ? "Wedge" : Pascal(type);

        /// <summary>Split on every non-alphanumeric, capitalise each run, join. "Repair Kit" →
        /// "RepairKit", "zoe" → "Zoe", "A.Wedge" → "AWedge".</summary>
        static string Pascal(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder();
            bool boundary = true;
            foreach (char c in s)
            {
                if (!char.IsLetterOrDigit(c)) { boundary = true; continue; }
                sb.Append(boundary ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                boundary = false;
            }
            return sb.ToString();
        }

        /// <summary>Python's <c>brand.title().replace(" ", "")</c>, which is what the 792
        /// generated rows used: "MireO" → "Mireo", "ROYAL SWING" → "RoyalSwing", "G&amp;F" →
        /// "G&amp;F". Separators other than whitespace are preserved.</summary>
        static string BrandPascal(string brand)
        {
            if (string.IsNullOrEmpty(brand)) return string.Empty;
            var sb = new StringBuilder();
            bool boundary = true;
            foreach (char c in brand)
            {
                if (char.IsWhiteSpace(c)) { boundary = true; continue; }
                if (!char.IsLetterOrDigit(c)) { sb.Append(c); boundary = true; continue; }
                sb.Append(boundary ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                boundary = false;
            }
            return sb.ToString();
        }

        /// <summary>
        /// The UPPERCASE art tag the <c>S_Menu_*</c> / <c>S_Controls_*</c> names carry:
        /// alphanumerics only, upper-cased. "G&amp;F" → "GF", "MireO" → "MIREO".
        /// <para>
        /// generate_clubs.py hand-picks a tag per brand and two of its nineteen are shortened
        /// ("ROYAL SWING" → ROYAL, the legacy putter's GOLFINIX). Those brands all have art and
        /// names already, so this rule never fires on them; it fires on brands an operator
        /// creates, where deterministic + unique is the whole requirement.
        /// </para>
        /// </summary>
        static string BrandTag(string brand)
        {
            if (string.IsNullOrEmpty(brand)) return string.Empty;
            var sb = new StringBuilder();
            foreach (char c in brand)
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            return sb.ToString();
        }

        // ── Findings ────────────────────────────────────────────────────────

        public enum Verdict
        {
            /// <summary>Downloaded, imported, CSV updated.</summary>
            Fetched,
            /// <summary>A sibling row already produced this exact asset in this run.</summary>
            SharedWithSibling,
            /// <summary>Nothing to do — no URL, or the name column is already set.</summary>
            Skipped,
            /// <summary>Refused, and said why. Never fatal: the other rows continue.</summary>
            Refused,
        }

        public sealed class Outcome
        {
            public string Catalog = "";
            public string RowId = "";
            public string UrlColumn = "";
            public string NameColumn = "";
            public string Folder = "";
            public string DerivedName = "";
            public string Url = "";
            public Verdict Verdict;
            public string Detail = "";
            public long SourceBytes;
            public long BuildBytes;

            /// <summary>
            /// The asset actually written, kept SEPARATELY from <see cref="Detail"/> because a
            /// later <c>Refuse</c> overwrites Detail with the reason — and the cleanup path still
            /// needs to know which file to remove.
            /// </summary>
            public string WrittenPath = "";
            public string Format = "";
            public int MaxTextureSize;

            public string AssetPath => $"Assets/Resources/{Folder}/{DerivedName}";
        }

        public sealed class RunReport
        {
            public readonly List<Outcome> Outcomes = new List<Outcome>();
            public readonly List<string> Errors = new List<string>();

            public IEnumerable<Outcome> Fetched => Outcomes.Where(o => o.Verdict == Verdict.Fetched);
            public IEnumerable<Outcome> Refused => Outcomes.Where(o => o.Verdict == Verdict.Refused);
            public IEnumerable<Outcome> Shared => Outcomes.Where(o => o.Verdict == Verdict.SharedWithSibling);

            public int FetchedCount => Fetched.Count();
            public int RefusedCount => Refused.Count();
            public long SourceBytes => Fetched.Sum(o => o.SourceBytes);
            public long BuildBytes => Fetched.Sum(o => o.BuildBytes);

            /// <summary>True when the run wrote nothing at all — the shape a re-run must have.</summary>
            public bool NoOp => FetchedCount == 0;

            public string Summary() =>
                $"{FetchedCount} asset(s) added, {Kb(SourceBytes)} source → {Kb(BuildBytes)} in build" +
                (Shared.Any() ? $", {Shared.Count()} row(s) share fetched art" : "") +
                (RefusedCount > 0 ? $", {RefusedCount} refused" : "") +
                (Errors.Count > 0 ? $", {Errors.Count} catalog(s) unreadable" : "");

            /// <summary>The §6 size block, in the shape ContentArtValidator's report already uses.</summary>
            public string ToText(int build)
            {
                var sb = new StringBuilder();
                sb.AppendLine(LogMarker);
                sb.AppendLine($"build {build} · {Summary()}");
                if (StorageFallbackUsed)
                    sb.AppendLine("   ⚠ in-build sizes are the RUNTIME fallback and OVER-REPORT " +
                                  "(roughly 2×) — UnityEditor.TextureUtil could not be reached.");

                foreach (var group in Catalogs)
                {
                    var rows = Outcomes.Where(o => o.Catalog == group.Name &&
                                                   o.Verdict != Verdict.Skipped).ToList();
                    if (rows.Count == 0) continue;

                    long src = rows.Where(r => r.Verdict == Verdict.Fetched).Sum(r => r.SourceBytes);
                    long bld = rows.Where(r => r.Verdict == Verdict.Fetched).Sum(r => r.BuildBytes);
                    sb.AppendLine($"   {group.Name}: {rows.Count(r => r.Verdict == Verdict.Fetched)} added, " +
                                  $"{Kb(src)} source → {Kb(bld)} in build");

                    foreach (var o in rows.OrderBy(r => r.RowId, StringComparer.Ordinal)
                                          .ThenBy(r => r.UrlColumn, StringComparer.Ordinal))
                    {
                        switch (o.Verdict)
                        {
                            case Verdict.Fetched:
                                sb.AppendLine($"     + {o.Folder}/{o.DerivedName,-28} {Kb(o.SourceBytes),9} → " +
                                              $"{Kb(o.BuildBytes),9}  {o.Format} {o.MaxTextureSize}  " +
                                              $"({o.RowId} {o.UrlColumn})");
                                break;
                            case Verdict.SharedWithSibling:
                                sb.AppendLine($"     = {o.Folder}/{o.DerivedName,-28} shared  " +
                                              $"({o.RowId} {o.UrlColumn})");
                                break;
                            case Verdict.Refused:
                                sb.AppendLine($"     ! {o.Folder}/{o.DerivedName,-28} REFUSED — {o.Detail}  " +
                                              $"({o.RowId} {o.UrlColumn})");
                                break;
                        }
                    }
                }

                foreach (var e in Errors) sb.AppendLine($"   ! {e}");
                sb.AppendLine();
                return sb.ToString();
            }
        }

        /// <summary>The derived target one outcome writes to: <c>Folder/DerivedName</c>.</summary>
        public static string Target(Outcome o) => o.Folder + "/" + o.DerivedName;

        /// <summary>
        /// Targets whose asset did NOT survive import verification — i.e. was written and then
        /// refused. Case-insensitive for the same filesystem reason <see cref="ExistingAsset"/> is.
        /// <para>
        /// A Refused outcome with no <c>WrittenPath</c> never wrote anything (allowlist, WebP, cap,
        /// collision), so it is not a failed TARGET — nothing points at it.
        /// </para>
        /// </summary>
        public static HashSet<string> FailedTargets(IEnumerable<Outcome> outcomes)
        {
            var failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in outcomes)
                if (o.Verdict == Verdict.Refused && !string.IsNullOrEmpty(o.WrittenPath))
                    failed.Add(Target(o));
            return failed;
        }

        /// <summary>
        /// Whether one CSV splice survives into the written file.
        ///
        /// <para>
        /// ⚠️ <b>This decision has been wrong twice</b>, which is why it is a named function with
        /// its own tests instead of an inline condition. First it did not exist at all — the CSV
        /// was written before verification ran, so every splice survived unconditionally (iter-3a).
        /// Then it checked only the row's OWN verdict, so a <see cref="Verdict.SharedWithSibling"/>
        /// row survived even when the asset it points at had been deleted (iter-3b) — and clubs
        /// share one asset across six rarities by design, so that is five repo rows naming a sprite
        /// that is not in the build.
        /// </para>
        /// <para>
        /// Both halves are load-bearing: the row's own verdict AND the fate of the target it names.
        /// </para>
        /// </summary>
        public static bool SpliceSurvives(Verdict verdict, string target, HashSet<string> failedTargets)
        {
            bool ownVerdictOk = verdict == Verdict.Fetched || verdict == Verdict.SharedWithSibling;
            return ownVerdictOk && !failedTargets.Contains(target);
        }

        /// <summary>
        /// TEST SEAM. When non-null, <see cref="ApplyAndVerifyImport"/> records this string as a
        /// verification problem, so the refusal path can be exercised without corrupting a real
        /// download.
        ///
        /// <para>
        /// It exists because the iter-3 defects were reachable ONLY through a verification failure,
        /// and the only way to reach one was to edit the source — which meant the fixes were proven
        /// by a manual tripwire that cannot catch its own regression, and which no reviewer role is
        /// allowed to perform. The iter-3 self-review FAILED the task over exactly that.
        /// </para>
        /// <para>Editor-only tooling; null in every real run. Tests must clear it in TearDown.</para>
        /// </summary>
        public static string? VerificationFaultForTest;

        /// <summary>
        /// Write a text file the way it must be written when the old contents matter:
        /// staged to <c>.tmp</c>, then swapped in with <c>File.Replace</c>.
        ///
        /// <para>
        /// ⚠️ <c>File.WriteAllText</c> TRUNCATES FIRST. A crash, a full disk or a lock between the
        /// truncate and the last byte leaves a shipped catalog CSV half-written — and
        /// <c>Clubs.csv</c> is 799 rows. That is not a rollback problem, it is unrecoverable
        /// without git. The atomic swap makes the file either wholly old or wholly new.
        /// </para>
        /// <para>
        /// Same idiom, for the same reason, as <c>TournamentArtService</c>'s cache writes
        /// (TournamentArtService.cs:544-552).
        /// </para>
        /// </summary>
        static void WriteTextAtomic(string path, string text)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        /// <summary>
        /// Delete an asset and say so when it does not go. <c>AssetDatabase.DeleteAsset</c> returns
        /// false on a locked or missing file, and an unchecked delete leaves an asset on disk that
        /// the CSV no longer names — invisible until the NEXT run refuses it as a collision against
        /// a file nobody asked for.
        /// </summary>
        static void DeleteAssetOrReport(string assetPath, RunReport report)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            if (AssetDatabase.DeleteAsset(assetPath)) return;

            report.Errors.Add($"could not delete {assetPath} after refusing it — the file is still " +
                              "on disk and no CSV names it. Remove it by hand, or the next run will " +
                              "refuse that row as a collision.");
        }

        /// <summary>
        /// The texture's STORAGE size — what it costs in the build.
        ///
        /// <para>
        /// ⚠️ <b>NOT <c>Profiler.GetRuntimeMemorySizeLong</c></b>, which this used to call and which
        /// answers a different question: memory once loaded, including a second copy, so it reported
        /// roughly DOUBLE. Measured on the 170×343 fixture: storage 26,912 B, runtime 54,784 B. The
        /// hand-check agrees with storage — ASTC_6x6 is ⌈170/6⌉ × ⌈343/6⌉ = 29 × 58 blocks × 16 B =
        /// 26,912 — and so does §10.2's own ratio: 26,912 / 80,500 = 0.33, in line with its
        /// 122 MB source ≈ 50 MB in build, where 0.68 is not. The runtime value is also
        /// state-dependent, so the same asset reported different sizes on different runs.
        /// </para>
        /// <para>
        /// <c>UnityEditor.TextureUtil</c> is internal, hence the reflection. If a Unity upgrade
        /// moves it, the profiler value is used instead and the report SAYS the number is the
        /// fallback rather than quietly printing a wrong one.
        /// </para>
        /// </summary>
        static long StorageBytes(Texture2D tex)
        {
            try
            {
                _storageMethod ??= Type.GetType("UnityEditor.TextureUtil, UnityEditor")
                    ?.GetMethod("GetStorageMemorySizeLong",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (_storageMethod != null)
                    return (long)_storageMethod.Invoke(null, new object[] { tex });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} TextureUtil.GetStorageMemorySizeLong failed ({e.GetType().Name}); " +
                                 "falling back to the runtime size, which over-reports.");
            }

            StorageFallbackUsed = true;
            return Profiler.GetRuntimeMemorySizeLong(tex);
        }

        static System.Reflection.MethodInfo? _storageMethod;

        /// <summary>True when any size in this run came from the over-reporting fallback (§6).</summary>
        public static bool StorageFallbackUsed;

        static string Kb(long bytes) =>
            bytes < 1024 ? $"{bytes} B"
                         : (bytes / 1024f).ToString("F1", CultureInfo.InvariantCulture) + " KB";

        // ── Entry points ────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Content/Fetch URL Art")]
        public static void FetchMenu()
        {
            // Console, never a dialog — Cesar's standing rule on editor popups.
            Run();
        }

        /// <summary>
        /// The MenuItem-free static entry (SPEC §2), so a reviewer can drive the whole thing from
        /// <c>script-execute</c> and read the structured result instead of scraping the Console.
        /// </summary>
        public static RunReport Run()
        {
            var report = new RunReport();
            StorageFallbackUsed = false;
            string root = Directory.GetParent(Application.dataPath)!.FullName;

            // (folder, name) → the run's first successful fetch of that asset. Clubs share art
            // across rarities by design (§9 answer 1), so six rows collapse onto one download.
            // OrdinalIgnoreCase for the same reason ExistingAsset is: on a case-insensitive
            // filesystem two rows deriving `Foo` and `foo` are one file, so they must collapse
            // onto one entry here or the second silently overwrites the first.
            var produced = new Dictionary<string, Produced>(StringComparer.OrdinalIgnoreCase);

            var allPending = new List<PendingCsv>();

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var spec in Catalogs)
                {
                    try { ProcessCatalog(spec, root, report, produced, allPending); }
                    catch (Exception e)
                    {
                        report.Errors.Add($"{spec.Name} ({spec.CsvPath}): {e.GetType().Name}: {e.Message}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            // Import settings are applied AFTER the batch closes: SaveAndReimport inside
            // Start/StopAssetEditing is deferred, and §5 requires the importer to be re-READ.
            foreach (var o in report.Fetched.ToList()) ApplyAndVerifyImport(o, report);

            // ONLY NOW are the CSVs written, and only for names whose asset actually verified.
            // Writing them earlier is what let a refused import leave a name in the repo (see
            // PendingCsv).
            FinalizeCsvs(allPending, report);

            AppendToReport(report, root);
            LogSummary(report);
            return report;
        }

        /// <summary>
        /// One catalog's pending CSV edits, held in memory until import verification has run.
        ///
        /// <para>
        /// ⚠️ <b>THE CSV MUST NOT BE WRITTEN BEFORE THE IMPORT IS VERIFIED.</b> It used to be:
        /// <c>ProcessCatalog</c> spliced the name and wrote the file inside the asset batch, and
        /// <c>ApplyAndVerifyImport</c> — which can REFUSE — only ran afterwards. A refused import
        /// therefore left the name in the CSV and the file on disk while the run reported
        /// "Refused". The repo then claimed the row was bundled when it was not, and because a
        /// failed import means <c>Resources.Load&lt;Sprite&gt;</c> returns null, the row would be
        /// silently withheld at runtime behind a name that looks perfectly correct — exactly the
        /// failure SPEC §1 decision 2 exists to prevent. Observed 2026-08-28 under a forced
        /// verification failure: verdict Refused, CSV carrying `Ordertest`, asset still on disk.
        /// </para>
        /// </summary>
        sealed class PendingCsv
        {
            public string FullPath = "";
            public string RelPath = "";
            public List<string> Lines = new List<string>();

            /// <summary>Which line and column each outcome spliced, so a refusal can be undone.</summary>
            public readonly List<(Outcome Outcome, int Line, int Column)> Edits =
                new List<(Outcome, int, int)>();
        }

        sealed class Produced
        {
            public string Sha256 = "";
            public string AssetPath = "";
            public string Url = "";
        }

        // ── One catalog ─────────────────────────────────────────────────────

        static void ProcessCatalog(CatalogSpec spec, string root, RunReport report,
                                   Dictionary<string, Produced> produced, List<PendingCsv> allPending)
        {
            var pending = new PendingCsv();
            string csvFull = Path.Combine(root, spec.CsvPath);
            if (!File.Exists(csvFull))
            {
                report.Errors.Add($"{spec.Name}: {spec.CsvPath} not found");
                return;
            }

            // Read as LINES and splice in place (see SetField). Re-serialising every row would
            // re-quote fields the tool never touched and bury the two-line diff §7 asks for.
            var lines = new List<string>(File.ReadAllText(csvFull).Split('\n'));

            // ⚠️ REGISTERED NOW, NOT AT THE END. This used to be added to allPending only after the
            // row loop completed — so a throw ANYWHERE in that loop (Run catches it per catalog and
            // carries on) discarded the pending entirely, and every asset already written for this
            // catalog was orphaned: still on disk, outcomes still Fetched, counted as bundled by the
            // report, and named by no CSV. That is the same "state committed before its validator
            // ran" shape as iter-3a, one level out. Registering up front means the rows that DID
            // complete are finalized normally and the ones that never ran simply have no edits.
            pending.FullPath = csvFull;
            pending.RelPath = spec.CsvPath;
            pending.Lines = lines;
            allPending.Add(pending);
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            bool headerSeen = false;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
                    continue;

                var fields = ParseCsvSpans(line);
                if (!headerSeen)
                {
                    headerSeen = true;
                    for (int c = 0; c < fields.Count; c++) index[fields[c].Value.Trim()] = c;
                    continue;
                }

                string Field(string column) =>
                    index.TryGetValue(column, out int c) && c < fields.Count ? fields[c].Value.Trim() : "";

                string rowId = Field(spec.IdColumn);
                if (string.IsNullOrEmpty(rowId)) continue;

                foreach (var slot in spec.Slots)
                {
                    if (!index.ContainsKey(slot.UrlColumn) || !index.ContainsKey(slot.NameColumn))
                        continue;   // column absent from this CSV — nothing to do

                    string url = Field(slot.UrlColumn);
                    string existingName = Field(slot.NameColumn);

                    // The precondition (SPEC §3): a URL set AND the sprite-NAME column empty.
                    // A row whose name is already set is what a SECOND RUN sees, which is what
                    // makes the second run a no-op.
                    if (string.IsNullOrEmpty(url) || !string.IsNullOrEmpty(existingName)) continue;

                    var outcome = new Outcome
                    {
                        Catalog = spec.Name,
                        RowId = rowId,
                        UrlColumn = slot.UrlColumn,
                        NameColumn = slot.NameColumn,
                        Folder = slot.Folder,
                        Url = url,
                    };
                    report.Outcomes.Add(outcome);

                    string name;
                    try { name = slot.Derive(Field); }
                    catch (Exception e)
                    {
                        Refuse(outcome, $"could not derive a name: {e.Message}");
                        continue;
                    }

                    if (string.IsNullOrEmpty(name) || name.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                    {
                        Refuse(outcome, $"derived name '{name}' is empty or not CSV-safe");
                        continue;
                    }
                    outcome.DerivedName = name;

                    if (TryFetchOne(outcome, root, produced))
                    {
                        // The CSV gains the name (SPEC §3.8) — spliced into the empty field so
                        // the diff is the name and nothing else. NOT written to disk here: the
                        // import has not been verified yet, and a refusal has to be able to undo
                        // this. See PendingCsv.
                        // `lines` IS `pending.Lines` (same reference), so this edit is
                        // already visible to FinalizeCsvs.
                        lines[i] = SetField(lines[i], fields, index[slot.NameColumn], name);
                        fields = ParseCsvSpans(lines[i]);   // offsets moved; re-index for the next slot
                        pending.Edits.Add((outcome, i, index[slot.NameColumn]));
                    }
                }
            }

            // No `if (!dirty) return;` here any more: FinalizeCsvs already skips a pending whose
            // edits did not survive, and returning early would re-create the bug above.
        }

        /// <summary>
        /// Write the CSVs, but ONLY the names whose asset survived import verification. An outcome
        /// that flipped to <see cref="Verdict.Refused"/> in <see cref="ApplyAndVerifyImport"/> has
        /// its splice undone and its written file deleted, so a refusal leaves NOTHING behind — no
        /// half-bundled row, no orphan asset.
        /// </summary>
        static void FinalizeCsvs(List<PendingCsv> allPending, RunReport report)
        {
            // Assets that did NOT survive verification, by derived target.
            //
            // ⚠️ A SharedWithSibling row is not safe just because IT was not refused — it names
            // ANOTHER row's asset. Clubs share art across six rarities by design (§9 answer 1), so
            // one refused Fetched row deletes the file that five SharedWithSibling rows point at.
            // Keeping their names would leave five rows in the repo naming a sprite that does not
            // exist, which is the same half-bundled state this whole method exists to prevent —
            // just spread over more rows. Case-insensitive for the same filesystem reason
            // ExistingAsset is.
            var failedTargets = FailedTargets(report.Outcomes);

            foreach (var pending in allPending)
            {
                bool anySurvived = false;

                foreach (var (outcome, line, column) in pending.Edits)
                {
                    if (SpliceSurvives(outcome.Verdict, Target(outcome), failedTargets))
                    {
                        anySurvived = true;
                        continue;
                    }

                    bool ownVerdictOk = outcome.Verdict == Verdict.Fetched ||
                                        outcome.Verdict == Verdict.SharedWithSibling;

                    // A sibling of a refused fetch: it was never refused itself, so say why it is
                    // being reverted rather than leaving it reported as satisfied.
                    if (ownVerdictOk)
                        Refuse(outcome, $"the shared asset {outcome.Folder}/{outcome.DerivedName} " +
                                        "did not survive import verification, so this row's name " +
                                        "was reverted too — it would have named a sprite that is " +
                                        "no longer in the build.");

                    // Verification refused after the splice — undo it.
                    var spans = ParseCsvSpans(pending.Lines[line]);
                    pending.Lines[line] = SetField(pending.Lines[line], spans, column, string.Empty);

                    if (!string.IsNullOrEmpty(outcome.WrittenPath))
                    {
                        // Delete through the AssetDatabase so the .meta goes with it.
                        DeleteAssetOrReport(outcome.WrittenPath, report);
                        outcome.Detail += $" (the written asset {outcome.WrittenPath} was removed " +
                                          "and the CSV name reverted — a refusal leaves nothing behind)";
                    }
                }

                if (!anySurvived) continue;

                try
                {
                    WriteTextAtomic(pending.FullPath, string.Join("\n", pending.Lines));
                    // Unity reads the IMPORTED TextAsset, not the file on disk: without this the
                    // loaders would keep serving the pre-write text for the rest of the session.
                    AssetDatabase.ImportAsset(pending.RelPath, ImportAssetOptions.ForceUpdate);
                }
                catch (Exception e)
                {
                    report.Errors.Add($"could not write {pending.RelPath}: {e.GetType().Name}: {e.Message}");

                    // The names could not be recorded, so the ASSETS MUST NOT STAY. Leaving them
                    // would report rows as bundled that the repo does not name, and the next run
                    // would refuse them as collisions against files nobody asked for. Same
                    // invariant as a refused verification, different trigger — flagged as a
                    // candidate by the iter-3 self-review and fixed rather than deferred.
                    foreach (var (outcome, _, _) in pending.Edits)
                    {
                        if (!SpliceSurvives(outcome.Verdict, Target(outcome), failedTargets)) continue;
                        DeleteAssetOrReport(outcome.WrittenPath, report);
                        Refuse(outcome, $"{pending.RelPath} could not be written, so this row's " +
                                        "art was removed too — an asset the repo does not name is " +
                                        "worse than no asset.");
                    }
                }
            }
        }

        static void Refuse(Outcome o, string detail)
        {
            o.Verdict = Verdict.Refused;
            o.Detail = detail;
        }

        // ── One row / one slot ──────────────────────────────────────────────

        /// <summary>
        /// The §3 sequence for one (row, column): allowlist → WebP → download under the cap →
        /// collision → sibling → write. Returns true when the CSV should gain the name.
        /// </summary>
        static bool TryFetchOne(Outcome o, string root, Dictionary<string, Produced> produced)
        {
            string key = o.Folder + "/" + o.DerivedName;

            // 1. THE CLIENT'S OWN ALLOWLIST — reused, never re-implemented (SPEC §3.1).
            //    A URL the client would refuse must never become a bundled asset: that would ship
            //    art nobody can trace back to a legitimate upload.
            if (!CatalogArtPolicy.IsArtAllowed(o.Url))
            {
                Refuse(o, $"URL is outside the allowlist — CatalogArtPolicy.IsArtAllowed said no " +
                          $"(only {CatalogArtPolicy.AllowedArtPrefix} is fetchable)");
                return false;
            }

            // 2a. WebP by EXTENSION (SPEC §3.2 — belt; the braces is the content type, below).
            string ext = UrlExtension(o.Url);
            if (string.Equals(ext, ".webp", StringComparison.OrdinalIgnoreCase))
            {
                Refuse(o, "WebP is refused — Unity does not import it natively, so it can never " +
                          "become a bundled asset (SPEC §1 decision 1). Re-upload as PNG or JPG.");
                return false;
            }
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
            {
                Refuse(o, $"unsupported extension '{ext}' — catalog art is PNG or JPG only");
                return false;
            }

            // A sibling row in THIS run may have already produced this exact asset. Clubs share
            // art across rarities by design, so six rows collapse to one download (§9 answer 1).
            if (produced.TryGetValue(key, out var already))
            {
                if (string.Equals(already.Url, o.Url, StringComparison.Ordinal))
                {
                    o.Verdict = Verdict.SharedWithSibling;
                    o.Detail = $"same URL as a row already fetched → {already.AssetPath}";
                    return true;
                }

                // Different URL, same derived name. The bucket filename carries the ROW ID, so two
                // rows uploaded separately have different URLs even for identical bytes — which is
                // exactly the case §4 says to treat as SATISFIED. So compare the bytes.
                if (!TryDownload(o, out byte[] other)) return false;
                if (Sha256(other) == already.Sha256)
                {
                    o.Verdict = Verdict.SharedWithSibling;
                    o.Detail = $"different URL, identical bytes → {already.AssetPath}";
                    return true;
                }

                Refuse(o, $"derives to the same name as a row already fetched ({already.AssetPath}) " +
                          "but the bytes differ — two different images cannot share one asset. " +
                          "Give one of the rows a distinct type/brand/name, or upload the same art to both.");
                return false;
            }

            // 5. COLLISION IS A REFUSAL, NEVER AN OVERWRITE (SPEC §4). An artist's hand-made
            //    asset must never be replaced by a downloaded one.
            string? clash = ExistingAsset(root, o.Folder, o.DerivedName);
            if (clash != null)
            {
                Refuse(o, $"{clash} already exists — a collision is never an overwrite. Rename the " +
                          "existing asset, or set the CSV name column to it if the row already has art.");
                return false;
            }

            // 7 (prerequisite). AN EMPTY FOLDER NEVER GUESSES (SPEC §5).
            string? sibling = FindSibling(root, o.Folder, null);
            if (sibling == null)
            {
                Refuse(o, $"Assets/Resources/{o.Folder} has no reference texture to copy import " +
                          "settings from. Place one deliberately — guessing them is how a raw PNG " +
                          "ships as a non-Sprite that Resources.Load cannot return.");
                return false;
            }

            // 3. DOWNLOAD under the upload cap.
            if (!TryDownload(o, out byte[] bytes)) return false;

            // 6. WRITE into the catalog's Resources folder.
            string rel = $"Assets/Resources/{o.Folder}/{o.DerivedName}{ext}";

            // Re-check IMMEDIATELY before the write. The collision test above ran before the
            // download, and this is the one call that can destroy an existing asset — a guard
            // adjacent to the dangerous operation survives a future reordering of the steps.
            string? lateClash = ExistingAsset(root, o.Folder, o.DerivedName);
            if (lateClash != null)
            {
                Refuse(o, $"{lateClash} appeared before the write — refusing rather than overwriting.");
                return false;
            }

            try
            {
                File.WriteAllBytes(Path.Combine(root, rel), bytes);
            }
            catch (Exception e)
            {
                Refuse(o, $"could not write {rel}: {e.GetType().Name}: {e.Message}");
                return false;
            }

            o.Verdict = Verdict.Fetched;
            o.SourceBytes = bytes.Length;
            o.Detail = rel;
            o.WrittenPath = rel;      // survives a later Refuse overwriting Detail
            produced[key] = new Produced { Sha256 = Sha256(bytes), AssetPath = rel, Url = o.Url };
            return true;
        }

        static string UrlExtension(string url)
        {
            int q = url.IndexOfAny(new[] { '?', '#' });
            string path = q >= 0 ? url.Substring(0, q) : url;
            return Path.GetExtension(path).ToLowerInvariant();
        }

        /// <summary>
        /// The asset already in the folder under this name, whatever its extension, or null.
        ///
        /// <para>
        /// ⚠️ <b>CASE-INSENSITIVE, and that is load-bearing.</b> This is the ONLY gate in front of
        /// <c>File.WriteAllBytes</c>, and the dev/CI filesystem is case-insensitive APFS. With an
        /// Ordinal comparison a row deriving <c>james</c> did not match an existing
        /// <c>James.png</c>, sailed past the guard, and the write then REPLACED THAT FILE'S BYTES
        /// — while APFS kept the original filename, so the <c>.meta</c> and the GUID survived
        /// untouched. An artist's asset would have been silently swapped with no rename, no new
        /// file and no diff except the pixels, which is precisely what SPEC §4 says must NEVER
        /// happen. Found by the red-team gate 2026-08-28; both earlier gates tested collision only
        /// with a same-case example, the one input where Ordinal and the filesystem agree.
        /// </para>
        /// <para>
        /// It is reachable, not theoretical: <see cref="BrandPascal"/> lower-cases interior letters
        /// (<c>"MireO" → "Mireo"</c>), so a hand-dropped <c>Driver-FairX.png</c> — one is in the
        /// tree today — is derived as <c>Driver-Fairx</c>.
        /// </para>
        /// <para>
        /// Comparing WITHOUT the extension is also deliberate and unrelated: <c>Resources.Load</c>
        /// ignores extensions, so <c>James.png</c> and <c>james.jpg</c> are the same resource name
        /// even though the filesystem would keep both files.
        /// </para>
        /// </summary>
        static string? ExistingAsset(string root, string folder, string name)
        {
            string dir = Path.Combine(root, "Assets", "Resources", folder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(dir)) return null;

            foreach (string f in Directory.GetFiles(dir))
            {
                if (f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(Path.GetFileNameWithoutExtension(f), name, StringComparison.OrdinalIgnoreCase))
                    return "Assets/Resources/" + folder + "/" + Path.GetFileName(f);
            }
            return null;
        }

        /// <summary>
        /// A correctly-configured texture in the SAME folder to copy import settings from (§5).
        /// Ordinal-first so the reference is deterministic across machines and runs.
        /// </summary>
        static string? FindSibling(string root, string folder, string? excludeAssetPath)
        {
            string dir = Path.Combine(root, "Assets", "Resources", folder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(dir)) return null;

            foreach (string f in Directory.GetFiles(dir).OrderBy(p => p, StringComparer.Ordinal))
            {
                if (f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;

                string rel = "Assets/Resources/" + folder + "/" + Path.GetFileName(f);
                // OrdinalIgnoreCase: Path.GetFileName returns the name the FILESYSTEM holds, which
                // after a case-variant write is the ORIGINAL casing, not what we asked for. An
                // Ordinal compare would fail to exclude the asset we just wrote and could pick it
                // as its own import reference.
                if (excludeAssetPath != null &&
                    string.Equals(rel, excludeAssetPath, StringComparison.OrdinalIgnoreCase)) continue;
                if (AssetImporter.GetAtPath(rel) is TextureImporter) return rel;
            }
            return null;
        }

        // ── Download ────────────────────────────────────────────────────────

        /// <summary>
        /// Blocking HTTPS GET, refusing WebP by CONTENT TYPE and anything over the upload cap.
        /// <para>
        /// The body is buffered before the size check. That is bounded in practice, not by luck:
        /// step 1 has already confined the URL to the project's own <c>catalog-art</c> bucket, and
        /// that bucket carries a 500 KB <c>fileSizeLimit</c> of its own. The check here is the one
        /// that refuses a file which reached the bucket some other way.
        /// </para>
        /// </summary>
        static bool TryDownload(Outcome o, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            using (var req = UnityWebRequest.Get(o.Url))
            {
                req.timeout = TimeoutSeconds;
                var op = req.SendWebRequest();
                while (!op.isDone) System.Threading.Thread.Sleep(15);

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Refuse(o, $"download failed ({req.responseCode}): {req.error}");
                    return false;
                }

                // 2b. WebP by CONTENT TYPE (SPEC §3.2). The upload path blocks it, but this step
                //     must not depend on that having held.
                string contentType = (req.GetResponseHeader("Content-Type") ?? "").Split(';')[0].Trim();
                if (string.Equals(contentType, "image/webp", StringComparison.OrdinalIgnoreCase))
                {
                    Refuse(o, "the server returned image/webp — Unity does not import WebP natively, " +
                              "so it can never become a bundled asset (SPEC §1 decision 1).");
                    return false;
                }
                if (contentType.Length > 0 &&
                    !string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    Refuse(o, $"the server returned Content-Type '{contentType}' — catalog art is " +
                              "PNG or JPG only.");
                    return false;
                }

                byte[] data = req.downloadHandler.data ?? Array.Empty<byte>();
                if (data.Length == 0)
                {
                    Refuse(o, "the server returned an empty body");
                    return false;
                }

                // 3. THE UPLOAD CAP, not the client's 1 MB backstop (Architect correction 2).
                //    Anything larger did not come through the admin's upload path.
                if (data.Length > MaxBytes)
                {
                    Refuse(o, $"{Kb(data.Length)} exceeds the {MaxBytes / 1024} KB upload cap " +
                              "(contentArtMutations.ts CATALOG_ART_SPEC.maxBytes) — a file this " +
                              "large did not come through the admin's upload path.");
                    return false;
                }

                bytes = data;
                return true;
            }
        }

        static string Sha256(byte[] data)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }

        // ── Import settings (SPEC §5) ───────────────────────────────────────

        /// <summary>
        /// Copy the sibling's TextureImporter onto the new asset, reimport, then RE-READ the
        /// importer and assert. A setting that failed to apply must not pass silently — that is
        /// §1 decision 2 and the whole reason this runs in the Editor.
        /// </summary>
        static void ApplyAndVerifyImport(Outcome o, RunReport report)
        {
            string assetPath = o.WrittenPath;   // set by TryFetchOne on success
            string root = Directory.GetParent(Application.dataPath)!.FullName;

            string? siblingPath = FindSibling(root, o.Folder, assetPath);
            if (siblingPath == null)
            {
                Refuse(o, "the reference asset disappeared between the folder check and the import");
                return;
            }

            var reference = AssetImporter.GetAtPath(siblingPath) as TextureImporter;
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (reference == null || importer == null)
            {
                Refuse(o, $"no TextureImporter for {(importer == null ? assetPath : siblingPath)} — " +
                          "the file did not import as a texture");
                return;
            }

            importer.textureType = reference.textureType;
            importer.spriteImportMode = reference.spriteImportMode;
            importer.spritePixelsPerUnit = reference.spritePixelsPerUnit;
            importer.spritePivot = reference.spritePivot;
            importer.alphaIsTransparency = reference.alphaIsTransparency;
            importer.alphaSource = reference.alphaSource;
            importer.mipmapEnabled = reference.mipmapEnabled;
            importer.wrapMode = reference.wrapMode;
            importer.filterMode = reference.filterMode;
            importer.npotScale = reference.npotScale;
            importer.sRGBTexture = reference.sRGBTexture;
            importer.maxTextureSize = reference.maxTextureSize;
            importer.textureCompression = reference.textureCompression;
            importer.compressionQuality = reference.compressionQuality;

            // Compression + FORMAT live in the platform settings, and the shipping platform's
            // override is the one that decides in-build bytes. Copy every platform the reference
            // carries, not just Default.
            foreach (string platform in new[] { "DefaultTexturePlatform", "Standalone", "iPhone", "Android" })
            {
                var settings = reference.GetPlatformTextureSettings(platform);
                importer.SetPlatformTextureSettings(settings);
            }

            importer.SaveAndReimport();

            // ── RE-READ. Nothing above is evidence; this is (SPEC §5). ──────
            var applied = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (applied == null)
            {
                Refuse(o, "the importer vanished after SaveAndReimport");
                return;
            }

            var appliedDefault = applied.GetPlatformTextureSettings("DefaultTexturePlatform");
            var referenceDefault = reference.GetPlatformTextureSettings("DefaultTexturePlatform");

            var problems = new List<string>();
            if (VerificationFaultForTest != null) problems.Add(VerificationFaultForTest);
            if (applied.textureType != reference.textureType)
                problems.Add($"textureType {applied.textureType} ≠ reference {reference.textureType}");
            if (applied.maxTextureSize != reference.maxTextureSize)
                problems.Add($"maxTextureSize {applied.maxTextureSize} ≠ reference {reference.maxTextureSize}");
            if (appliedDefault.format != referenceDefault.format)
                problems.Add($"format {appliedDefault.format} ≠ reference {referenceDefault.format}");
            if (appliedDefault.textureCompression != referenceDefault.textureCompression)
                problems.Add($"textureCompression {appliedDefault.textureCompression} ≠ " +
                             $"reference {referenceDefault.textureCompression}");
            if (applied.alphaIsTransparency != reference.alphaIsTransparency)
                problems.Add($"alphaIsTransparency {applied.alphaIsTransparency} ≠ " +
                             $"reference {reference.alphaIsTransparency}");
            if (applied.spriteImportMode != reference.spriteImportMode)
                problems.Add($"spriteImportMode {applied.spriteImportMode} ≠ " +
                             $"reference {reference.spriteImportMode}");

            // ── NOT the fresh-import default. ───────────────────────────────
            // This project's m_DefaultBehaviorMode is 0 (Mode3D), so an unconfigured PNG lands as
            // textureType Default and Resources.Load<Sprite> returns NULL on it. That is the
            // silent failure §1 decision 2 names, and it is the assertion that actually bites.
            //
            // maxTextureSize and format are asserted EQUAL TO THE REFERENCE and reported as
            // numbers; they are deliberately NOT asserted "≠ Unity's default", because the
            // reference art itself sits at 2048 / Automatic and such an assertion would be a
            // statement the data cannot support.
            if (applied.textureType == TextureImporterType.Default)
                problems.Add("textureType is still the fresh-import default (Default) — " +
                             "Resources.Load<Sprite> returns null on it");

            if (problems.Count > 0)
            {
                Refuse(o, "import settings did not take: " + string.Join("; ", problems) +
                          $". Reference: {siblingPath}");
                return;
            }

            o.Format = appliedDefault.format.ToString();
            o.MaxTextureSize = applied.maxTextureSize;
            o.Detail = assetPath;

            // In-build bytes — the MEASURED compressed payload, not an estimate from the source PNG.
            // §10.2 counts Assets/Resources/Clubs at 122 MB source ≈ 50 MB in build; a tool whose
            // whole job is adding to that folder must report the number on that side of the ratio.
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            o.BuildBytes = tex != null ? StorageBytes(tex) : 0;
        }

        // ── CSV field splice ────────────────────────────────────────────────

        public struct FieldSpan
        {
            public string Value;
            /// <summary>Index in the RAW line where the field starts (its opening quote, if any).</summary>
            public int Start;
            /// <summary>Raw length, quotes included.</summary>
            public int Length;
        }

        /// <summary>
        /// Quote-aware split that also reports each field's RAW extent, so a value can be spliced
        /// into one field without re-serialising the rest of the line. The parse itself is the
        /// same shape every loader uses.
        /// </summary>
        public static List<FieldSpan> ParseCsvSpans(string line)
        {
            var fields = new List<FieldSpan>();
            var current = new StringBuilder();
            bool inQuotes = false;
            int start = 0;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(new FieldSpan { Value = current.ToString(), Start = start, Length = i - start });
                    current.Clear();
                    start = i + 1;
                }
                else current.Append(c);
            }

            fields.Add(new FieldSpan { Value = current.ToString(), Start = start, Length = line.Length - start });
            return fields;
        }

        /// <summary>
        /// Replace one field's raw span with <paramref name="value"/>, leaving every other byte of
        /// the line untouched. Trailing <c>\r</c> is inside the last field's span and survives.
        /// </summary>
        public static string SetField(string line, List<FieldSpan> fields, int column, string value)
        {
            if (column < 0 || column >= fields.Count) return line;
            var f = fields[column];

            // A CRLF file's trailing \r lives INSIDE the last field's raw span (the caller splits
            // on '\n'), so replacing that span whole would eat it and silently rewrite the line
            // ending of every row this tool touches. Carried across explicitly. Caught by
            // Splice_KeepsATrailingCarriageReturnInsideTheLastField, which went red on the first
            // sweep. Today all four CSVs are LF-only, so this is a latent hole rather than a live
            // bug — but "leave every other byte alone" is the whole premise of splicing.
            string raw = line.Substring(f.Start, f.Length);
            string eol = raw.EndsWith("\r", StringComparison.Ordinal) ? "\r" : string.Empty;

            return line.Substring(0, f.Start) + value + eol + line.Substring(f.Start + f.Length);
        }

        // ── Output ──────────────────────────────────────────────────────────

        /// <summary>
        /// Appends the §6 size block to <c>Docs/Reports/content_art.txt</c> — the file
        /// content_two_way §5 already writes — rather than starting a second report nobody reads.
        /// <see cref="ContentArtValidator"/> preserves everything from <see cref="LogMarker"/> to
        /// EOF when it rewrites that file, so the two tools coexist in one report.
        /// <para>A run that fetched nothing appends nothing: a no-op must leave no diff (§7).</para>
        /// </summary>
        static void AppendToReport(RunReport report, string root)
        {
            if (report.NoOp && report.RefusedCount == 0 && report.Errors.Count == 0) return;

            try
            {
                string full = Path.Combine(root, ReportPath);
                string existing = File.Exists(full) ? File.ReadAllText(full) : "";
                if (existing.Length > 0 && !existing.EndsWith("\n", StringComparison.Ordinal))
                    existing += "\n";
                // Atomic for the same reason the CSVs are: this is a read-modify-write over a file
                // that also carries ContentArtValidator's whole coverage record.
                WriteTextAtomic(full, existing + "\n" + report.ToText(ContentArtValidator.BuildNumber()));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} could not append the size summary to {ReportPath}: " +
                                 $"{e.GetType().Name}: {e.Message}");
            }
        }

        static void LogSummary(RunReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Tag} {report.Summary()}");

            foreach (var o in report.Fetched)
                sb.AppendLine($"   + {o.Detail}  {Kb(o.SourceBytes)} source → {Kb(o.BuildBytes)} in " +
                              $"build  ({o.Format}, max {o.MaxTextureSize})  [{o.Catalog} {o.RowId} " +
                              $"{o.UrlColumn} → {o.NameColumn}]");
            foreach (var o in report.Shared)
                sb.AppendLine($"   = {o.Folder}/{o.DerivedName}  {o.Detail}  [{o.Catalog} {o.RowId} " +
                              $"{o.UrlColumn}]");
            foreach (var o in report.Refused)
                sb.AppendLine($"   ! {o.Folder}/{o.DerivedName}  REFUSED — {o.Detail}  [{o.Catalog} " +
                              $"{o.RowId} {o.UrlColumn}]");
            foreach (var e in report.Errors) sb.AppendLine($"   ! {e}");

            if (!report.NoOp)
            {
                // The CSV now carries a name the CATALOG does not — exactly the drift
                // import_content.py exists to resolve. Reuse that loop; do not invent a second
                // way in (SPEC §3, closing instruction).
                sb.AppendLine();
                sb.AppendLine("   NEXT — the CSVs now name sprites the published catalogs do not:");
                sb.AppendLine("     1. python3 Tools/content/import_content.py --env-file " +
                              "Tools/admin-dashboard/.env.development.local --apply");
                sb.AppendLine("     2. publish the affected catalogs in the admin panel");
                sb.AppendLine("     3. python3 Tools/content/export_content.py --env-file " +
                              "Tools/admin-dashboard/.env.development.local");
                sb.AppendLine("     4. review the diff, commit, then run the lane.");
            }

            if (report.RefusedCount > 0 || report.Errors.Count > 0) Debug.LogWarning(sb.ToString());
            else if (report.NoOp) Debug.Log($"{Tag} nothing to fetch — every row with a URL already " +
                                            "names a bundled sprite. No files written.");
            else Debug.Log(sb.ToString());
        }
    }
}
