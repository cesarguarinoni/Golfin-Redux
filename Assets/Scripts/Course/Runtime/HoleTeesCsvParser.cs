// HoleTeesCsvParser — SPEC §3.2 (Phase 3)
// Static parser for Assets/Data/HoleTees.csv.
// Columns: courseId,holeNumber,teeSet,yards,color
//
// Lives in Golfin.Course.Runtime so it can be directly tested by Golfin.Course.Tests.
//
// Usage:
//   var teesLookup = HoleTeesCsvParser.Parse(csvText, courseSlug);
//   if (teesLookup.TryGetValue(1, out var hole1Tees)) { ... }

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Golfin.Course.Runtime
{
    public static class HoleTeesCsvParser
    {
        /// <summary>
        /// Parses the HoleTees CSV and returns a dictionary keyed by hole number,
        /// containing only rows whose courseId matches <paramref name="courseId"/>.
        /// The header row is skipped automatically.
        /// </summary>
        /// <param name="csvText">Raw text of HoleTees.csv.</param>
        /// <param name="courseId">Course slug to filter by (e.g. "lomond-country-club").</param>
        public static Dictionary<int, List<TeeData>> Parse(string csvText, string courseId)
        {
            var result = new Dictionary<int, List<TeeData>>();

            if (string.IsNullOrWhiteSpace(csvText)) return result;

            string[] lines = csvText.Split('\n');

            // Skip header (line 0)
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] fields = line.Split(',');
                if (fields.Length < 4) continue;  // minimum: courseId,holeNumber,teeSet,yards

                try
                {
                    string rowCourse = fields[0].Trim();
                    if (!string.Equals(rowCourse, courseId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!int.TryParse(fields[1].Trim(), out int holeNumber)) continue;
                    if (!Enum.TryParse<TeeSet>(fields[2].Trim(), ignoreCase: true, out TeeSet teeSet)) continue;
                    if (!int.TryParse(fields[3].Trim(), out int yards)) continue;

                    string color = fields.Length > 4 ? fields[4].Trim() : null;
                    if (string.IsNullOrEmpty(color)) color = null;

                    var teeData = new TeeData
                    {
                        set   = teeSet,
                        yards = yards,
                        color = color,
                    };

                    if (!result.TryGetValue(holeNumber, out var list))
                    {
                        list = new List<TeeData>();
                        result[holeNumber] = list;
                    }
                    list.Add(teeData);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[HoleTeesCsvParser] Failed to parse line {i + 1}: {line}\nError: {e.Message}");
                }
            }

            return result;
        }
    }
}
