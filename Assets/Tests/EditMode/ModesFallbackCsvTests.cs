// ─────────────────────────────────────────────────────────────────────────────
// The guard on ModesDatabaseCSV.FallbackCsv — the copy of modes.csv the game
// parses when Resources.Load cannot produce the real file.
//
// This suite exists because of what the fallback USED to be: a hand-built list
// of ModeData objects, i.e. a second model of the same five rows kept in sync by
// whoever remembered to. Nobody did. By 2026-09-09 it claimed Missions was
// locked with target=none (six weeks after missions_v1 unlocked it), paid 20 RP
// where the CSV pays 35, and carried no `tournaments` row at all — so the one
// code path whose entire job is to rescue a broken build would instead have
// rendered a game that does not exist.
//
// Making the fallback a copy of the TEXT killed the per-field drift. This kills
// the rest: there is now exactly one question — is the embedded string equal to
// the file? — and it is answered here in the fast loop, and again by
// ModesFallbackBuildHook, which fails the build outright.
//
// Reached by reflection into Assembly-CSharp, the way ModesOverlayTests reaches
// the same class: an asmdef-based test assembly cannot reference a predefined
// assembly.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class ModesFallbackCsvTests
    {
        private const string CsvPath = "Assets/Resources/Data/modes.csv";

        private static readonly Type? _dbType =
            Type.GetType("GolfinRedux.UI.ModeSelect.ModesDatabaseCSV, Assembly-CSharp");

        /// <summary>
        /// Both sides normalised: the repo is LF, but a CRLF checkout or a stray editor setting
        /// must not turn this guard into noise somebody disables.
        /// </summary>
        private static string Normalize(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n");

        private static string Embedded()
        {
            Assert.IsNotNull(_dbType, "ModesDatabaseCSV not found in Assembly-CSharp");
            FieldInfo? f = _dbType!.GetField("FallbackCsv", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(f, "ModesDatabaseCSV.FallbackCsv is gone — if the fallback was " +
                                "redesigned, this suite needs to follow it, not be deleted.");
            return Normalize((string)f!.GetRawConstantValue()!);
        }

        // ── The guard ─────────────────────────────────────────────────────────

        [Test]
        public void FallbackCsv_IsAVerbatimCopyOfTheBundledCsv()
        {
            Assert.IsTrue(File.Exists(CsvPath), $"{CsvPath} is missing");

            string[] onDisk = Normalize(File.ReadAllText(CsvPath)).Split('\n');
            string[] embedded = Embedded().Split('\n');

            // Line by line rather than one string compare, so a failure NAMES the drifted row
            // instead of printing two 1.2 KB blobs and leaving the reader to diff them.
            int shared = Math.Min(onDisk.Length, embedded.Length);
            for (int i = 0; i < shared; i++)
            {
                Assert.AreEqual(onDisk[i], embedded[i],
                    $"ModesDatabaseCSV.FallbackCsv has drifted from {CsvPath} at line {i + 1}. " +
                    "Run  Tools > Golfin > Modes > Sync Fallback CSV  to regenerate it.");
            }

            Assert.AreEqual(onDisk.Length, embedded.Length,
                $"{CsvPath} and the embedded copy have different row counts — a mode was added or " +
                "removed without regenerating. Run  Tools > Golfin > Modes > Sync Fallback CSV.");
        }

        // ── And that the copy is still a parseable table ──────────────────────

        [Test]
        public void FallbackCsv_IsWellFormed()
        {
            string[] lines = Embedded().Split('\n');
            var rows = new List<string[]>();

            MethodInfo parse = _dbType!.GetMethod("ParseCsvLine", BindingFlags.NonPublic | BindingFlags.Static)!;
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                rows.Add((string[])parse.Invoke(null, new object[] { trimmed })!);
            }

            Assert.GreaterOrEqual(rows.Count, 2, "a header and at least one mode");

            int columns = rows[0].Length;
            var ids = new HashSet<string>();
            var orders = new HashSet<string>();
            int iId = Array.IndexOf(rows[0], "id");
            int iOrder = Array.IndexOf(rows[0], "order");
            Assert.GreaterOrEqual(iId, 0, "header carries an `id` column");
            Assert.GreaterOrEqual(iOrder, 0, "header carries an `order` column");

            for (int r = 1; r < rows.Count; r++)
            {
                // Catches the classic hand-edit: a row appended without its trailing empty
                // reward columns, which silently reads every later column as absent.
                Assert.AreEqual(columns, rows[r].Length,
                    $"row {r + 1} has {rows[r].Length} columns, the header has {columns}");

                Assert.IsTrue(ids.Add(rows[r][iId]), $"duplicate mode id '{rows[r][iId]}'");
                Assert.IsTrue(orders.Add(rows[r][iOrder]),
                    $"two modes share order '{rows[r][iOrder]}' — the list order would be arbitrary");
            }
        }
    }
}
