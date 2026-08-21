// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Inventory.Tests — ClubRosterCsvTests
// Asserts the SHIPPED Assets/Resources/Data/Clubs.csv, not a fixture: 799 rows,
// unique ids, the info_ja column, and the comment-line regression that silently
// emptied the database.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Golfin.Inventory.Tests
{
    [TestFixture]
    public class ClubRosterCsvTests
    {
        /// <summary>Row count of the shipped roster: 7 legacy rows + 792 generated.</summary>
        private const int ExpectedRowCount = 799;

        private static IList ShippedRows()
        {
            string? text = ClubRosterProd.ReadShippedCsv();
            if (text == null)
            {
                Assert.Inconclusive(
                    $"Shipped {ClubRosterProd.CsvRelativePath} not found. " +
                    "This test only runs in a full project checkout.");
            }
            return ClubRosterProd.Parse(text);
        }

        // ── The regression that emptied the database ──────────────────────────

        /// <summary>
        /// Clubs.csv opens with three <c>#</c> provenance lines. The reader used to take
        /// <c>lines[0]</c> as the header, so it built its column index out of prose, every lookup
        /// missed, every row parsed to an empty id, and the database loaded ZERO clubs — silently,
        /// because an empty result was not an error. The header is the first non-blank, non-comment
        /// line.
        /// </summary>
        [Test]
        public void Parse_SkipsLeadingCommentLines_AndFindsTheRealHeader()
        {
            const string csv =
                "# provenance line, not a header\n" +
                "# second comment\n" +
                "\n" +
                "id,name,type,rarity,info,info_ja\n" +
                "club_x,X,S.Wedge,Rare,English blurb,日本語\n";

            var rows = ClubRosterProd.Parse(csv);

            Assert.AreEqual(1, rows.Count, "the comment lines must not be parsed as data rows");
            Assert.AreEqual("club_x",       ClubRosterProd.Field<string>(rows[0]!, "id"));
            Assert.AreEqual("English blurb", ClubRosterProd.Field<string>(rows[0]!, "info"));
            Assert.AreEqual("日本語",         ClubRosterProd.Field<string>(rows[0]!, "infoJa"));
        }

        [Test]
        public void Parse_ShippedCsv_LoadsEveryRow()
        {
            var rows = ShippedRows();
            Assert.AreEqual(ExpectedRowCount, rows.Count,
                $"the shipped roster must parse to {ExpectedRowCount} rows. A count of 0 means the " +
                "header was taken from a comment line; any other count means Clubs.csv changed and " +
                "ExpectedRowCount needs updating with it.");
        }

        [Test]
        public void Parse_ShippedCsv_IdsAreUnique()
        {
            var ids  = ClubRosterProd.AllIds(ShippedRows());
            var dupes = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

            Assert.IsEmpty(dupes,
                $"duplicate club ids collapse silently into one entry in ClubDatabaseCSV's map: " +
                $"{string.Join(", ", dupes.Take(10))}");
            Assert.IsEmpty(ids.Where(string.IsNullOrWhiteSpace), "no row may have a blank id");
        }

        [Test]
        public void Parse_ShippedCsv_HasInfoJaColumn_AndEveryGeneratedRowFillsIt()
        {
            var rows = ShippedRows();
            int withJa = rows.Cast<object>()
                             .Count(r => !string.IsNullOrWhiteSpace(ClubRosterProd.Field<string>(r, "infoJa")));

            Assert.Greater(withJa, 0,
                "info_ja parsed as empty for every row — the column is missing or misnamed in Clubs.csv");

            // Coverage is now complete: the generator filled 792 rows, and the 7 legacy rows that
            // predate the column were translated by hand (2026-08-21) — a JP player was seeing the
            // English blurb on the starter clubs, which are the first clubs anyone owns.
            // A blank JA cell remains LEGAL at runtime (ClubInfoText falls back to English); this
            // is a coverage assertion, so it is pinned at "all of them" rather than at a count that
            // has to be edited every time copy lands.
            Assert.AreEqual(rows.Count, withJa,
                "every shipped club row should carry info_ja — a blank one shows a JP player English");
        }

        [Test]
        public void Parse_ShippedCsv_EveryRowHasEnglishInfo()
        {
            var rows = ShippedRows();
            var blank = rows.Cast<object>()
                            .Where(r => string.IsNullOrWhiteSpace(ClubRosterProd.Field<string>(r, "info")))
                            .Select(r => ClubRosterProd.Field<string>(r, "id"))
                            .ToList();

            Assert.IsEmpty(blank,
                $"info is the last rung of the description ladder — a blank one collapses the row " +
                $"for English AND Japanese players: {string.Join(", ", blank.Take(10))}");
        }

        // ── Types (task 3: the new S.Wedge) ───────────────────────────────────

        [Test]
        public void ParseType_MapsEveryShippedToken_IncludingSWedge()
        {
            Assert.AreEqual("Driver",  ClubRosterProd.ParseTypeName("Driver"));
            Assert.AreEqual("Wood",    ClubRosterProd.ParseTypeName("Wood"));
            Assert.AreEqual("Iron",    ClubRosterProd.ParseTypeName("Iron"));
            Assert.AreEqual("A_Wedge", ClubRosterProd.ParseTypeName("A.Wedge"));
            Assert.AreEqual("P_Wedge", ClubRosterProd.ParseTypeName("P.Wedge"));
            Assert.AreEqual("S_Wedge", ClubRosterProd.ParseTypeName("S.Wedge"));
            Assert.AreEqual("Putter",  ClubRosterProd.ParseTypeName("Putter"));

            // Case and stray spaces must not silently degrade a real type to Driver.
            Assert.AreEqual("S_Wedge", ClubRosterProd.ParseTypeName("s. wedge"));
            Assert.AreEqual("S_Wedge", ClubRosterProd.ParseTypeName("S.WEDGE"));
        }

        /// <summary>
        /// An unknown token degrades to Driver rather than throwing — a future roster column must
        /// never be able to hard-fail the boot.
        /// </summary>
        [Test]
        public void ParseType_UnknownToken_DegradesToDriver_NeverThrows()
        {
            Assert.AreEqual("Driver", ClubRosterProd.ParseTypeName("Hybrid"));
            Assert.AreEqual("Driver", ClubRosterProd.ParseTypeName(""));
        }

        [Test]
        public void Parse_ShippedCsv_NoRowSilentlyDegradedToDriver()
        {
            var rows = ShippedRows();
            var byType = new Dictionary<string, int>();
            foreach (var r in rows)
            {
                string t = ClubRosterProd.EnumName(r!, "type");
                byType[t] = byType.TryGetValue(t, out int n) ? n + 1 : 1;
            }

            foreach (var expected in new[] { "Driver", "Wood", "Iron", "A_Wedge", "P_Wedge", "S_Wedge", "Putter" })
                Assert.IsTrue(byType.ContainsKey(expected) && byType[expected] > 0,
                    $"the roster must contain at least one {expected}; ParseType silently maps an " +
                    "unrecognised token to Driver, so a missing type here means a typo in Clubs.csv");
        }

        [Test]
        public void Parse_ShippedCsv_EveryRarityTokenResolves()
        {
            var rows = ShippedRows();
            var seen = new HashSet<string>();
            foreach (var r in rows) seen.Add(ClubRosterProd.EnumName(r!, "rarity"));

            foreach (var expected in new[] { "Common", "Uncommon", "Rare", "Mythic", "Legendary", "Supreme" })
                Assert.IsTrue(seen.Contains(expected), $"the roster must contain at least one {expected} club");
        }

        // ── Quoted fields ─────────────────────────────────────────────────────

        /// <summary>
        /// Most info blurbs contain commas and are therefore quoted. A parser that split naively
        /// would shift every later column — including info_ja — by one.
        /// </summary>
        [Test]
        public void Parse_QuotedFieldContainingCommas_DoesNotShiftLaterColumns()
        {
            const string csv =
                "id,name,info,info_ja\n" +
                "club_q,Q,\"Solid carry, more forgiving than the driver.\",\"和文、読点入り\"\n";

            var rows = ClubRosterProd.Parse(csv);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("Solid carry, more forgiving than the driver.",
                ClubRosterProd.Field<string>(rows[0]!, "info"));
            Assert.AreEqual("和文、読点入り", ClubRosterProd.Field<string>(rows[0]!, "infoJa"));
        }
    }
}
