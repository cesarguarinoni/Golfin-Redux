#nullable enable
using System;
using System.Collections.Generic;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Session;
using UnityEngine;

namespace Golfin.Gameplay.Missions
{
    /// <summary>
    /// Decides whether a mission's goals were met. Spec: missions_v1 §B4.
    ///
    /// IT READS THE SHOT HISTORY, NOT THE SIMULATION. `GameSession.ShotHistory` is the record
    /// the game already keeps of every shot — club, origin, resting place, terminal state,
    /// final surface, penalties — and it is written once per shot by the one path that knows.
    /// Deriving goals from it rather than from a parallel subscription to the physics means a
    /// goal can never disagree with the scorecard the player is looking at.
    ///
    /// EVERYTHING IS EVALUATED AT HOLE-OUT, and some things ALSO fail earlier. §B4 asks for
    /// both, and the reason is the player's time: a NO_HAZARD mission is over the moment the
    /// ball is in the water, and four more shots spent finding that out is the worst way to be
    /// told. So `EvaluateProgressive` marks a goal `Met = false` as soon as it is unreachable,
    /// the HUD can grey it, and `EvaluateFinal` still runs the complete list at the end — the
    /// early verdict is an optimisation of the message, never of the decision.
    ///
    /// UNITS. Goal params are YARDS (the design is written in yards); `ShotRecord` distances
    /// are METRES. Every comparison converts explicitly — a silent metre-vs-yard mix would make
    /// a 150-yard goal ask for 164.
    /// </summary>
    public sealed class MissionGoalEvaluator
    {
        private const float YardsPerMetre = 1.0936133f;

        private readonly MissionDefinition _mission;
        private readonly Vector3 _pinWorld;
        private bool _attached;

        public MissionGoalEvaluator(MissionDefinition mission, Vector3 pinWorld)
        {
            _mission = mission;
            _pinWorld = pinWorld;
            foreach (var goal in mission.Goals) goal.Met = null;
        }

        /// <summary>Raised whenever a goal's verdict changes, so the HUD strip can redraw.</summary>
        public event Action? OnGoalsChanged;

        public void Attach()
        {
            if (_attached) return;
            GameSession.OnHistoryChanged += EvaluateProgressive;
            _attached = true;
        }

        public void Detach()
        {
            if (!_attached) return;
            GameSession.OnHistoryChanged -= EvaluateProgressive;
            _attached = false;
        }

        // ── Progressive: only the goals that can FAIL early ──────────────────────

        /// <summary>
        /// Mark the goals that have already become impossible. It never marks a goal MET —
        /// "no hazard so far" is not "no hazard", and a goal that flickered to met and back
        /// would be worse than one that simply stayed undecided.
        /// </summary>
        public void EvaluateProgressive()
        {
            var shots = GameSession.ShotHistory;
            bool changed = false;

            foreach (var goal in _mission.Goals)
            {
                if (goal.Met.HasValue) continue;      // already decided
                bool doomed = false;

                switch (goal.Type)
                {
                    case MissionGoalType.NO_HAZARD:
                        foreach (var s in shots)
                            if (IsHazard(s)) { doomed = true; break; }
                        break;

                    case MissionGoalType.AVOID:
                        foreach (var s in shots)
                            if (SurfaceMatches(s.FinalSurface, goal.Param)) { doomed = true; break; }
                        break;

                    case MissionGoalType.AVOID_CLUB:
                        foreach (var s in shots)
                            if (ClubMatches(s.ClubLabel, goal.Param)) { doomed = true; break; }
                        break;

                    case MissionGoalType.LAND_TEE:
                        if (shots.Count >= 1 && !SurfaceMatches(shots[0].FinalSurface, goal.Param))
                            doomed = true;
                        break;

                    case MissionGoalType.USE_CLUB:
                        if (shots.Count >= 1 && !ClubMatches(shots[0].ClubLabel, goal.Param))
                            doomed = true;
                        break;

                    case MissionGoalType.PUTTS:
                        if (goal.ParamInt.HasValue && CountPutts(shots) > goal.ParamInt.Value)
                            doomed = true;
                        break;

                    case MissionGoalType.SHOTS:
                        if (goal.ParamInt.HasValue && shots.Count > goal.ParamInt.Value) doomed = true;
                        break;

                    case MissionGoalType.SCORE:
                        if (goal.ParamInt.HasValue && StrokesSoFar(shots) - _mission.Par > goal.ParamInt.Value)
                            doomed = true;
                        break;

                    case MissionGoalType.UP_DOWN:
                        if (shots.Count > 2) doomed = true;
                        break;

                    case MissionGoalType.GIR:
                        // Missed the green by the regulation shot — GIR is gone, and unlike the
                        // others this one cannot be recovered by playing on.
                        int reg = Mathf.Max(1, _mission.Par - 2);
                        if (shots.Count >= reg && !SurfaceMatches(shots[reg - 1].FinalSurface, "Green"))
                            doomed = true;
                        break;
                }

                if (doomed) { goal.Met = false; changed = true; }
            }

            if (changed) OnGoalsChanged?.Invoke();
        }

        // ── Final: the whole list, at hole-out ───────────────────────────────────

        /// <summary>
        /// The verdict. Every goal gets an explicit true/false — a goal left null here would
        /// reach the claim as "not met" anyway, but silently, and the modal would show a blank
        /// where a cross belongs.
        /// </summary>
        public MissionResult EvaluateFinal(HoleCompletionData completion, string sessionGuid)
        {
            var shots = GameSession.ShotHistory;
            int strokes = completion.Strokes;
            int putts = CountPutts(shots);

            var result = new MissionResult
            {
                MissionId = _mission.Id,
                Strokes = strokes,
                Putts = putts,
                FailedOnStrokeCap = completion.TerminalState != BallState.InCup,
                IdempotencyKey = $"mission:{_mission.Id}:{sessionGuid}",
            };

            foreach (var goal in _mission.Goals)
            {
                goal.Met = Decide(goal, shots, strokes, putts, completion);
                result.Goals.Add(goal);
            }

            // A hole that ended on the stroke cap is a FAILED hole whatever the goal list says:
            // the ball is not in the cup, so the score is not a score.
            result.Cleared = !result.FailedOnStrokeCap;
            foreach (var goal in result.Goals)
                if (goal.Met != true) { result.Cleared = false; break; }

            OnGoalsChanged?.Invoke();
            return result;
        }

        private bool Decide(MissionGoal goal, IReadOnlyList<ShotRecord> shots,
                            int strokes, int putts, HoleCompletionData completion)
        {
            switch (goal.Type)
            {
                case MissionGoalType.SCORE:
                    return goal.ParamInt.HasValue && strokes - _mission.Par <= goal.ParamInt.Value;

                case MissionGoalType.SHOTS:
                    return goal.ParamInt.HasValue && strokes <= goal.ParamInt.Value;

                case MissionGoalType.PUTTS:
                    return goal.ParamInt.HasValue && putts <= goal.ParamInt.Value;

                case MissionGoalType.NO_HAZARD:
                    foreach (var s in shots) if (IsHazard(s)) return false;
                    return true;

                case MissionGoalType.AVOID:
                    foreach (var s in shots) if (SurfaceMatches(s.FinalSurface, goal.Param)) return false;
                    return true;

                case MissionGoalType.AVOID_CLUB:
                    foreach (var s in shots) if (ClubMatches(s.ClubLabel, goal.Param)) return false;
                    return true;

                case MissionGoalType.LAND_TEE:
                    return shots.Count > 0 && SurfaceMatches(shots[0].FinalSurface, goal.Param);

                case MissionGoalType.LAND_ANY:
                    foreach (var s in shots) if (SurfaceMatches(s.FinalSurface, goal.Param)) return true;
                    return false;

                case MissionGoalType.USE_CLUB:
                    return shots.Count > 0 && ClubMatches(shots[0].ClubLabel, goal.Param);

                case MissionGoalType.GIR:
                {
                    int reg = Mathf.Max(1, _mission.Par - 2);
                    return shots.Count >= reg && SurfaceMatches(shots[reg - 1].FinalSurface, "Green");
                }

                case MissionGoalType.DIST:
                case MissionGoalType.CARRY:
                {
                    // ⚠️ CARRY IS EVALUATED AS TOTAL DISTANCE. `ShotRecord` carries
                    // `DistanceXZMeters` (origin → resting place) and nothing that separates
                    // the flight from the roll, so a true carry cannot be read from it. No
                    // campaign mission uses CARRY and the daily never draws it, so nothing
                    // ships on this approximation — but a CARRY goal authored in the admin
                    // WOULD be easier than it reads, and that is worth knowing before one is.
                    if (!goal.ParamFloat.HasValue) return false;
                    float target = goal.ParamFloat.Value;
                    foreach (var s in shots)
                        if (s.DistanceXZMeters * YardsPerMetre >= target) return true;
                    return false;
                }

                case MissionGoalType.NEAR_PIN:
                {
                    if (!goal.ParamFloat.HasValue) return false;
                    foreach (var s in shots)
                    {
                        if (!SurfaceMatches(s.FinalSurface, "Green")) continue;
                        // The FIRST shot to reach the green — a later putt sitting closer is
                        // not what the goal is asking about.
                        Vector3 d = s.FinalPosition - _pinWorld;
                        float yards = new Vector2(d.x, d.z).magnitude * YardsPerMetre;
                        return yards <= goal.ParamFloat.Value;
                    }
                    return false;
                }

                case MissionGoalType.UP_DOWN:
                    return _mission.StartKind == "short" && strokes <= 2;

                default:
                    return false;
            }
        }

        // ── Matching helpers ────────────────────────────────────────────────────

        private static int StrokesSoFar(IReadOnlyList<ShotRecord> shots)
        {
            int n = 0;
            foreach (var s in shots) n += 1 + s.PenaltyStrokes;
            return n;
        }

        private static int CountPutts(IReadOnlyList<ShotRecord> shots)
        {
            int n = 0;
            foreach (var s in shots)
                if (!string.IsNullOrEmpty(s.ClubLabel) &&
                    s.ClubLabel.IndexOf("Putt", StringComparison.OrdinalIgnoreCase) >= 0) n++;
            return n;
        }

        private static bool IsHazard(ShotRecord s)
            => string.Equals(s.TerminalState, "OB", StringComparison.OrdinalIgnoreCase)
               || !string.IsNullOrEmpty(s.OBReason);

        /// <summary>
        /// Does a shot's final surface satisfy a goal's surface parameter?
        ///
        /// The two vocabularies do not match and neither is wrong: the DESIGN writes
        /// "Bunker", "S.Rough" and "Rough&amp;Semi"; `SurfaceType` says `Sand`, `Semirough`.
        /// Normalising here — once, in the place that compares them — beats renaming either.
        /// </summary>
        private static bool SurfaceMatches(string? actual, string goalParam)
        {
            if (string.IsNullOrEmpty(actual) || string.IsNullOrEmpty(goalParam)) return false;
            string a = actual.Replace(".", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
            string want = goalParam.Replace(".", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();

            switch (want)
            {
                case "bunker":
                case "sand":       return a == "sand" || a == "bunkerlip";
                case "water":      return a == "water";
                case "fairway":    return a == "fairway";
                case "green":      return a == "green" || a == "greencollar";
                case "rough":      return a == "rough";
                case "srough":
                case "semirough":  return a == "semirough";
                // "Rough&Semi" is one goal covering both, and it is the hardest AVOID on the
                // weights table precisely because it covers both.
                case "rough&semi": return a == "rough" || a == "semirough";
                default:           return a == want;
            }
        }

        /// <summary>
        /// Does a shot's club label satisfy a goal's club-type parameter?
        ///
        /// `ShotRecord.ClubLabel` is display text ("Iron 7", "Wedge"); the goal param is a
        /// catalog TYPE ("Iron7", "AW"). Compared with punctuation and case stripped, plus the
        /// three wedge abbreviations that share the word "wedge".
        /// </summary>
        private static bool ClubMatches(string? label, string goalParam)
        {
            if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(goalParam)) return false;
            string a = label.Replace(" ", "").Replace(".", "").ToLowerInvariant();
            string want = goalParam.Replace(" ", "").Replace(".", "").ToLowerInvariant();

            switch (want)
            {
                case "aw": return a.Contains("approachwedge") || a == "awedge" || a == "aw";
                case "pw": return a.Contains("pitchingwedge") || a == "pwedge" || a == "pw";
                case "sw": return a.Contains("sandwedge") || a == "swedge" || a == "sw";
                default:   return a == want || a.Contains(want);
            }
        }
    }
}
