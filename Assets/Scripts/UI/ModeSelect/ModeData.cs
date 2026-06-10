namespace GolfinRedux.UI.ModeSelect
{
    /// <summary>
    /// Plain DTO for one row in Assets/Resources/Data/modes.csv.
    /// Columns: id, title, tagline, description, entryFee, rewards, locked, target, order.
    /// </summary>
    public class ModeData
    {
        // id — e.g. "practice" / "versus_1v1" / "driving_range" / "missions"
        public string id = "";
        // title — display name shown on the card
        public string title = "";
        // tagline — short subtitle shown in collapsed view
        public string tagline = "";
        // description — longer body text shown when card is expanded
        public string description = "";
        // entryFee — RP cost to launch; 0 = no fee (renders "NO ENTRY FEE" per SPEC)
        public int entryFee;
        // rewards — RP reward for completing the mode; shown as coin row
        public int rewards;
        // locked — true for "coming soon" modes (Driving Range, Missions); no progression service
        public bool locked;
        // target — launch route: "hole_select" / "matchmaking_1v1" / "none"
        public string target = "";
        // order — sort order in both carousel and full-screen list (1-based)
        public int order;
        // versusStrokeCapOverPar — safety cap for 1v1 matches (strokes over par before forced draw).
        // 0 means not applicable (non-versus modes). versus_1v1 = 5 by default (CSV-tunable).
        public int versusStrokeCapOverPar;
    }
}
