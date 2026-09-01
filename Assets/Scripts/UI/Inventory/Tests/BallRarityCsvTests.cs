// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Inventory.Tests — BallRarityCsvTests
//
// ball_data_wiring §4.1 / §10. Asserts the SHIPPED Assets/Data/Balls.csv parses to the
// approved rarity tier for every one of the 20 balls, through the PRODUCTION reader.
//
// ASSEMBLY NOTE: BallDatabaseCSV and BallDataRuntime live in Assembly-CSharp, which an
// asmdef test assembly cannot reference — they are reached by REFLECTION, the same idiom
// ClubRosterProd establishes for the club side. Nothing here re-implements the CSV split,
// the header index or the rarity parse: a green test cannot mean "my private copy agrees
// with itself".
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Golfin.Content;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.Inventory.Tests
{
    [TestFixture]
    public class BallRarityCsvTests
    {
        private const string CsvRelativePath = "Assets/Data/Balls.csv";

        /// <summary>The approved tier per ball — ball_data_wiring SPEC §2, transcribed once.</summary>
        private static readonly Dictionary<string, string> ExpectedRarity = new()
        {
            ["ball_golfin"]          = "Common",
            ["ball_par_perfect"]     = "Common",
            ["ball_fyloe_soft"]      = "Common",
            ["ball_ace_attire"]      = "Common",
            ["ball_birdie_v1"]       = "Common",
            ["ball_golfin_mk2"]      = "Uncommon",
            ["ball_gf"]              = "Uncommon",
            ["ball_tifto"]           = "Uncommon",
            ["ball_fairloft"]        = "Uncommon",
            ["ball_fyloe_aim"]       = "Uncommon",
            ["ball_clover_pro"]      = "Uncommon",
            ["ball_golfinix"]        = "Rare",
            ["ball_klyro"]           = "Rare",
            ["ball_royal_swing"]     = "Rare",
            ["ball_fairway_threads"] = "Rare",
            ["ball_putt_ace"]        = "Rare",
            ["ball_mireo"]           = "Mythic",
            ["ball_cirq"]            = "Mythic",
            ["ball_soralis"]         = "Mythic",
            ["ball_shimmer_g"]       = "Legendary",
        };

        // ── Reflection into the production reader ─────────────────────────────

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            throw new InvalidOperationException(
                $"Production type '{fullName}' not found. It should live in Assembly-CSharp " +
                "(Assets/Scripts/UI/Inventory/, no asmdef).");
        }

        private static readonly Type TDatabase = FindType("Golfin.Inventory.BallDatabaseCSV");

        /// <summary>
        /// A BallDatabaseCSV whose Awake has NOT run. Added to an INACTIVE GameObject on
        /// purpose: Awake calls LoadCSV, which logs an error for the unassigned Inspector
        /// TextAsset and would fail the fixture. Only ParseRow is under test here.
        /// </summary>
        private static (GameObject go, object db) NewReader()
        {
            var go = new GameObject("BallRarityCsvTests_Reader");
            go.SetActive(false);
            return (go, go.AddComponent(TDatabase));
        }

        private static string? ReadShippedCsv()
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            for (int i = 0; i < 5; i++)
            {
                string candidate = Path.Combine(dir, CsvRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return File.ReadAllText(candidate);
                dir = Path.GetFullPath(Path.Combine(dir, ".."));
            }
            return null;
        }

        private static List<string> SplitLine(string line)
        {
            var m = TDatabase.GetMethod("ParseCSVLine", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (List<string>)m.Invoke(null, new object[] { line })!;
        }

        private static Dictionary<string, int> HeaderIndex(object db, List<string> headers)
        {
            var m = TDatabase.GetMethod("BuildHeaderIndex", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (Dictionary<string, int>)m.Invoke(db, new object[] { headers })!;
        }

        private static object? ParseRow(object db, ContentFields fields)
        {
            var m = TDatabase.GetMethod("ParseRow", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return m.Invoke(db, new object?[] { fields, null, null });
        }

        private static string Field(object ball, string name) =>
            ball.GetType().GetField(name)!.GetValue(ball)!.ToString()!;

        /// <summary>Every row of the given CSV text, parsed by the production ParseRow.</summary>
        private static List<object> ParseAll(string csvText)
        {
            var (go, db) = NewReader();
            try
            {
                var lines  = csvText.Split('\n');
                var header = HeaderIndex(db, SplitLine(lines[0]));
                var rows   = new List<object>();
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    var ball = ParseRow(db, ContentFields.Csv(SplitLine(line), header));
                    if (ball != null) rows.Add(ball);
                }
                return rows;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void ShippedCsv_EveryBallParsesItsApprovedRarity()
        {
            string? text = ReadShippedCsv();
            if (text == null)
                Assert.Inconclusive($"Shipped {CsvRelativePath} not found — full checkout only.");

            var rows = ParseAll(text!);

            Assert.AreEqual(ExpectedRarity.Count, rows.Count,
                $"the shipped ball catalog must parse to {ExpectedRarity.Count} rows " +
                "(ball_data_wiring took it from 2 to 20)");

            foreach (var ball in rows)
            {
                string id = Field(ball, "ballId");
                Assert.IsTrue(ExpectedRarity.ContainsKey(id),
                    $"'{id}' is in Balls.csv but not in the SPEC §2 rarity table — one of the two is wrong");
                Assert.AreEqual(ExpectedRarity[id], Field(ball, "rarity"),
                    $"'{id}' must parse as {ExpectedRarity[id]}");
            }
        }

        /// <summary>
        /// The column is NEW. A published <c>content_rows</c> row written before it existed carries
        /// no <c>rarity</c> key at all, and must not throw or land on a random tier — ClubCsvParser
        /// .ParseRarity defaults unknown/blank to Common, which is what makes reusing it correct.
        /// </summary>
        [Test]
        public void RowWithNoRarityColumn_ParsesAsCommon()
        {
            const string csv =
                "id,name,brand,power,rebound,windResistance,roll,spin,thumbnailSprite,fullSprite,info,thumbnailUrl,fullUrl,isDefault\n" +
                "ball_legacy,Legacy,Legacy,0,0,0,0,0,Golfin,Golfin,blurb,,,false\n";

            var rows = ParseAll(csv);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("Common", Field(rows[0], "rarity"),
                "a row predating the rarity column must default to Common, not throw and not shift tier");
        }

        [Test]
        public void BlankAndBogusRarity_BothFallBackToCommon()
        {
            const string csv =
                "id,name,brand,rarity,power,rebound,windResistance,roll,spin,thumbnailSprite,fullSprite,info,thumbnailUrl,fullUrl,isDefault\n" +
                "ball_blank,Blank,B,,0,0,0,0,0,Golfin,Golfin,blurb,,,false\n" +
                "ball_bogus,Bogus,B,Platinum,0,0,0,0,0,Golfin,Golfin,blurb,,,false\n";

            var rows = ParseAll(csv);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("Common", Field(rows[0], "rarity"), "blank rarity → Common");
            Assert.AreEqual("Common", Field(rows[1], "rarity"), "unrecognised rarity → Common");
        }
    }
}
