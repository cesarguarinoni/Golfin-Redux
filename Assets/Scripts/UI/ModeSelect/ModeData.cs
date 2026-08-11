using System.Collections.Generic;
using GolfinRedux.UI;

namespace GolfinRedux.UI.ModeSelect
{
    /// <summary>
    /// Plain DTO for one row in Assets/Resources/Data/modes.csv.
    /// Columns: id, title, tagline, description, entryFee, rewards, locked, target, order,
    ///          versusStrokeCapOverPar, reward1Type, reward1Amount, reward2Type, reward2Amount,
    ///          reward3Type, reward3Amount, rewardsTextKey.
    /// The rewardList field is populated from the reward pair columns (Stage 2).
    /// The legacy int 'rewards' is retained for backward compatibility (other modes still use it).
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
        // rewards — legacy int kept for backward compat (modes that haven't migrated to pairs yet)
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
        // rewardList — parsed from (type,amount) reward-pair columns (Stage 2). Empty for modes
        // that only have the legacy int 'rewards' column. For versus_1v1: Points×200 on WIN.
        public List<HoleReward> rewardList = new List<HoleReward>();
        // rewardsTextKey — optional localization key; when set, the REWARDS row shows this
        // localized TEXT (no coin icon, no amount) instead of "x{rewards}". Used by
        // tournaments ("Varies by tournament"). Empty for all other modes.
        public string rewardsTextKey = "";
    }
}
