using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase A4: Load-determinism diff helper.
/// After running the fixed 5-shot sequence across 3 cold-load Unity cycles, this tool
/// reads the per-cycle CSVs and produces A4-diff-summary.md.
///
/// GOLFIN > Tools > A4 - Load Determinism Diff
///
/// Expected CSV file structure (written by RealHoleA4DiagTest or manual runs):
///   Docs/DIAG/realtest-20260425/A4-cycle-{1..3}-shot-{1..5}.csv
///   Docs/DIAG/realtest-20260425/A4-cycle-{1..3}-shot-{1..5}-hits.csv
/// </summary>
public static class A4DiffHelper
{
    [MenuItem("GOLFIN/Tools/A4 - Load Determinism Diff")]
    public static void RunDiff()
    {
        string diagDir = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Docs", "DIAG", "realtest-20260425"));

        var sb = new StringBuilder();
        sb.AppendLine("# A4 — Load Determinism Diff Summary");
        sb.AppendLine($"# Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        bool anyFallThrough = false;
        bool anyDivergence  = false;
        bool allFilesPresent = true;

        const float FallThroughThreshold = -0.05f; // ballY - groundY < this = fall-through
        const int NumCycles = 3, NumShots = 5;

        for (int shot = 1; shot <= NumShots; shot++)
        {
            sb.AppendLine($"## Shot {shot}");

            // Load all 3 cycles for this shot
            var cycleRows = new List<List<StepRow>>();
            for (int cycle = 1; cycle <= NumCycles; cycle++)
            {
                string path = Path.Combine(diagDir, $"A4-cycle-{cycle}-shot-{shot}.csv");
                if (!File.Exists(path))
                {
                    sb.AppendLine($"  Cycle {cycle}: FILE MISSING ({path})");
                    allFilesPresent = false;
                    cycleRows.Add(null);
                }
                else
                {
                    cycleRows.Add(ParseStepCsv(path));
                    sb.AppendLine($"  Cycle {cycle}: {cycleRows[cycle-1].Count} rows loaded.");
                }
            }

            // Check: all 3 cycles have same row count
            int[] counts = cycleRows.Select(r => r?.Count ?? -1).ToArray();
            bool sameLengths = counts.Distinct().Count() == 1 && counts[0] > 0;
            sb.AppendLine($"  Row counts: [{string.Join(", ", counts)}] — {(sameLengths ? "MATCH" : "DIFFER")}");
            if (!sameLengths) anyDivergence = true;

            // Find first divergence step
            int divergeStep = -1;
            if (sameLengths && cycleRows.All(r => r != null))
            {
                for (int i = 0; i < cycleRows[0].Count; i++)
                {
                    bool identical = true;
                    for (int c = 1; c < NumCycles; c++)
                    {
                        if (!cycleRows[c][i].ApproxEquals(cycleRows[0][i]))
                        { identical = false; break; }
                    }
                    if (!identical) { divergeStep = i; break; }
                }
            }

            if (divergeStep < 0 && sameLengths)
                sb.AppendLine("  Trajectory: BIT-IDENTICAL across all 3 cycles.");
            else if (divergeStep >= 0)
            {
                anyDivergence = true;
                sb.AppendLine($"  Trajectory: DIVERGES at step {divergeStep}.");
                for (int c = 0; c < NumCycles; c++)
                {
                    if (cycleRows[c] == null) continue;
                    var row = cycleRows[c][divergeStep];
                    sb.AppendLine($"    Cycle {c+1}: frame={row.Frame} phase={row.Phase} surf={row.Surface} ballY={row.BallY:F4} gY2={row.GroundY2:F4} gY3={row.GroundY3:F4}");
                }
            }

            // Fall-through check: does ballY drop below groundY3 - 0.05 at any step?
            for (int c = 0; c < NumCycles; c++)
            {
                if (cycleRows[c] == null) continue;
                float worst = 0f;
                int worstStep = -1;
                for (int i = 0; i < cycleRows[c].Count; i++)
                {
                    float gap = cycleRows[c][i].BallY - cycleRows[c][i].GroundY3;
                    if (gap < worst) { worst = gap; worstStep = i; }
                }
                if (worst < FallThroughThreshold)
                {
                    anyFallThrough = true;
                    sb.AppendLine($"  Cycle {c+1}: FALL-THROUGH at step {worstStep}, gap={worst:F4}m (ballY-groundY3).");
                }
                else
                {
                    sb.AppendLine($"  Cycle {c+1}: No fall-through. Min gap={worst:F4}m");
                }
            }

            sb.AppendLine();
        }

        // Verdict
        sb.AppendLine("## Verdict");
        string verdict;
        string recommendedPath;

        if (!allFilesPresent)
        {
            verdict = "INCOMPLETE — some cycle CSV files are missing.";
            recommendedPath = "Run all 3 cold-load cycles first, then re-run this diff.";
        }
        else if (!anyDivergence && !anyFallThrough)
        {
            verdict = "All 3 cycles bit-identical, no fall-through.";
            recommendedPath = "Tactical fix viable. Continue Phase B.";
        }
        else if (anyDivergence && !anyFallThrough)
        {
            verdict = "Trajectories diverge across cold loads but no fall-through observed in these 5 shots.";
            recommendedPath = "Ambiguous — Architect reviews CSVs. Non-determinism present but may not cause fall-through in these specific shots.";
        }
        else if (anyDivergence && anyFallThrough)
        {
            verdict = "Trajectories diverge across cold loads AND fall-through occurs in at least one cycle.";
            recommendedPath = "PIVOT TO ARCHITECTURAL FIX — matches activation trigger #3 in Docs/Specs/Queued/SIM_BAKED_DATA_PATH.md";
        }
        else // !anyDivergence && anyFallThrough
        {
            verdict = "All cycles identical AND fall-through occurs consistently.";
            recommendedPath = "Different bug than manual observation — Architect reviews and re-scopes.";
        }

        sb.AppendLine($"**Outcome:** {verdict}");
        sb.AppendLine($"**Recommended path:** {recommendedPath}");

        string outPath = Path.Combine(diagDir, "A4-diff-summary.md");
        File.WriteAllText(outPath, sb.ToString());

        Debug.Log($"[A4Diff] Done. Verdict: {verdict}. Saved: {outPath}");
    }

    static List<StepRow> ParseStepCsv(string path)
    {
        var rows = new List<StepRow>();
        bool header = true;
        foreach (var line in File.ReadAllLines(path))
        {
            if (header) { header = false; continue; }
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(',');
            if (parts.Length < 8) continue;
            rows.Add(new StepRow
            {
                Frame    = int.TryParse(parts[0], out int f) ? f : 0,
                Phase    = parts[1],
                Surface  = parts[2],
                BallX    = float.TryParse(parts[3], out float bx) ? bx : 0f,
                BallY    = float.TryParse(parts[4], out float by) ? by : 0f,
                BallZ    = float.TryParse(parts[5], out float bz) ? bz : 0f,
                GroundY2 = float.TryParse(parts[6], out float gy2) ? gy2 : 0f,
                GroundY3 = float.TryParse(parts[7], out float gy3) ? gy3 : 0f,
            });
        }
        return rows;
    }

    class StepRow
    {
        public int    Frame;
        public string Phase, Surface;
        public float  BallX, BallY, BallZ, GroundY2, GroundY3;

        public bool ApproxEquals(StepRow o, float eps = 0.001f)
            => Phase == o.Phase && Surface == o.Surface
               && Mathf.Abs(BallX  - o.BallX)  < eps
               && Mathf.Abs(BallY  - o.BallY)  < eps
               && Mathf.Abs(BallZ  - o.BallZ)  < eps
               && Mathf.Abs(GroundY2 - o.GroundY2) < eps
               && Mathf.Abs(GroundY3 - o.GroundY3) < eps;
    }
}
