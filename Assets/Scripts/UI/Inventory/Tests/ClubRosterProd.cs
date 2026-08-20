// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Inventory.Tests — ClubRosterProd
//
// ASSEMBLY NOTE: the club system (ClubCsvParser, ClubInfoText, ClubManager) lives in
// Assembly-CSharp, which an asmdef test assembly cannot reference. It is reached by
// REFLECTION through this shared helper — the same idiom the tournament ladder tests
// use (Prod in Assets/Scripts/TournamentsRuntime/Tests/RemoteScheduleTests.cs).
//
// Everything here binds to the PRODUCTION members. Nothing re-declares a club id or a
// parsing rule locally, so a test can never pass against a private copy of the data.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Golfin.Inventory.Tests
{
    internal static class ClubRosterProd
    {
        internal static Type Find(string fullName)
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

        internal static readonly Type Parser      = Find("Golfin.Inventory.ClubCsvParser");
        internal static readonly Type InfoText    = Find("Golfin.Inventory.ClubInfoText");
        internal static readonly Type ClubManager = Find("ClubManager");

        // ── Shipped CSV ───────────────────────────────────────────────────────

        internal const string CsvRelativePath = "Assets/Resources/Data/Clubs.csv";

        /// <summary>
        /// Locates the shipped Clubs.csv by walking up from the compiled test assembly
        /// (&lt;project&gt;/Library/ScriptAssemblies/…). Mirrors the shipped-CSV lookup in
        /// BotFieldInvariantTests so both stay System-only (no Resources, no Application).
        /// </summary>
        internal static string? FindShippedCsv()
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            for (int i = 0; i < 5; i++)
            {
                string candidate = Path.Combine(dir, CsvRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetFullPath(Path.Combine(dir, ".."));
            }
            return null;
        }

        internal static string? ReadShippedCsv()
        {
            string? path = FindShippedCsv();
            return path == null ? null : File.ReadAllText(path);
        }

        // ── Reflection wrappers ───────────────────────────────────────────────

        /// <summary>ClubCsvParser.Parse(text) — returned as a non-generic IList of ClubCsvRow.</summary>
        internal static IList Parse(string? csvText)
        {
            var m = Parser.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
            return (IList)m.Invoke(null, new object?[] { csvText })!;
        }

        /// <summary>Reads one public field off a ClubCsvRow instance.</summary>
        internal static T Field<T>(object row, string field)
        {
            var f = row.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new InvalidOperationException($"ClubCsvRow.{field} not found.");
            return (T)f.GetValue(row)!;
        }

        /// <summary>Enum fields come back boxed; compare by name so the test needs no enum reference.</summary>
        internal static string EnumName(object row, string field)
        {
            var f = row.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance)!;
            return f.GetValue(row)!.ToString()!;
        }

        internal static string ParseTypeName(string token)
        {
            var m = Parser.GetMethod("ParseType", BindingFlags.Public | BindingFlags.Static)!;
            return m.Invoke(null, new object?[] { token })!.ToString()!;
        }

        internal static string ResolveInfo(string? info, string? infoJa)
        {
            var m = InfoText.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(string), typeof(string) }, null)!;
            return (string)m.Invoke(null, new object?[] { info, infoJa })!;
        }

        /// <summary>Reads a private static string[] off ClubManager (the real shipped id lists).</summary>
        internal static string[] IdList(string fieldName)
        {
            var f = ClubManager.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException($"ClubManager.{fieldName} not found.");
            return (string[])f.GetValue(null)!;
        }

        internal static List<string> AllIds(IList rows)
        {
            var ids = new List<string>(rows.Count);
            foreach (var r in rows) ids.Add(Field<string>(r!, "id"));
            return ids;
        }
    }
}
