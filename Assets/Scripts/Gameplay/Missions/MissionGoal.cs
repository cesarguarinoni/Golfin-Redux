#nullable enable
using System;
using System.Globalization;

namespace Golfin.Gameplay.Missions
{
    /// <summary>The fourteen goal types the campaign and the daily draw from.</summary>
    public enum MissionGoalType
    {
        None = 0,
        SCORE,       // strokes - par <= param
        SHOTS,       // strokes <= param
        PUTTS,       // putts <= param
        NO_HAZARD,   // no water, no OB
        AVOID,       // never finish a shot on <surface>
        LAND_TEE,    // the TEE shot finishes on <surface>
        LAND_ANY,    // some shot finishes on <surface>
        GIR,         // on the green in (par - 2)
        DIST,        // some shot travels >= param yards
        CARRY,       // some shot carries >= param yards
        NEAR_PIN,    // first shot to reach the green stops <= param yards from the pin
        USE_CLUB,    // the tee shot uses <club type>
        AVOID_CLUB,  // never use <club type>
        UP_DOWN,     // from a short start, hole out in <= 2
    }

    /// <summary>
    /// One goal on a mission: a type, its parameter, and (after evaluation) whether it was met.
    ///
    /// The parameter stays a STRING because that is what it is in the catalog and half the
    /// types are not numbers at all (`AVOID Bunker`, `USE_CLUB Driver`). Parsing happens where
    /// the type is known, not here, so a `SCORE fairway` is a validator problem rather than a
    /// silent 0.
    /// </summary>
    public sealed class MissionGoal
    {
        public MissionGoalType Type = MissionGoalType.None;
        public string Param = "";

        /// <summary>Null until the evaluator has decided. False the moment it becomes
        /// unreachable — a goal can fail long before the hole ends.</summary>
        public bool? Met;

        public MissionGoal() { }

        public MissionGoal(MissionGoalType type, string param)
        {
            Type = type;
            Param = param ?? "";
        }

        public static MissionGoalType Parse(string raw)
            => Enum.TryParse(raw?.Trim(), ignoreCase: false, out MissionGoalType t) ? t : MissionGoalType.None;

        public int? ParamInt
            => int.TryParse(Param, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : (int?)null;

        public float? ParamFloat
            => float.TryParse(Param, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : (float?)null;

        /// <summary>
        /// The stroke cap (over par) this goal implies, or null when it does not bound strokes.
        ///
        /// SCORE is already relative to par. SHOTS is absolute, so it converts. Nothing else
        /// caps: a NO_HAZARD mission can be failed on shot one and still be worth finishing,
        /// because the OTHER goals on the card may still be live.
        /// </summary>
        public int? ImpliedStrokeCapOverPar(int par)
        {
            switch (Type)
            {
                case MissionGoalType.SCORE: return ParamInt;
                case MissionGoalType.SHOTS: return ParamInt.HasValue ? ParamInt.Value - par : (int?)null;
                case MissionGoalType.UP_DOWN: return 2 - par;
                default: return null;
            }
        }

        /// <summary>
        /// The localization key for this goal's line on the card. SCORE has one key per value
        /// ("Score par or better" reads nothing like "Score bogey or better"); every other type
        /// is one template with {0} substituted.
        /// </summary>
        public string TextKey
            => Type == MissionGoalType.SCORE
                ? "GOAL_SCORE_" + (Param.StartsWith("-", StringComparison.Ordinal) ? "M" + Param.Substring(1) : Param)
                : "GOAL_" + Type;
    }
}
