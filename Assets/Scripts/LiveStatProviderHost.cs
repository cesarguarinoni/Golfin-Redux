#nullable enable
using UnityEngine;
using Golfin.Gameplay.Defaults;
using Golfin.Gameplay.Session;   // GameSession
using Golfin.Gameplay.UI.HUD;    // ClubContext, BallContext, ClubEntry
using Golfin.Physics.Math;
using Golfin.Physics.Stats;
using Golfin.Roster;             // PlayerCharacterData, CharacterManager
using Golfin.Inventory;          // ClubDataRuntime, PlayerClubData, BallDataRuntime

/// <summary>
/// Assembly-CSharp MonoBehaviour that registers the live stat resolver with StatProviderBus.
/// Lives on ShellScene → PersistentUI; survives scene reloads via DontDestroyOnLoad.
///
/// On every shot ShotController calls StatProviderBus.Resolve(isPutt), which calls
/// ResolveLive here. If any selection is missing (no character / club / ball),
/// ResolveLive returns null and the bus falls through to DefaultStatProvider.
///
/// Lab path is unchanged: PhysicsLabController.InjectStatBundle sets
/// _statBundleOverridden=true which short-circuits before Resolve is called.
/// </summary>
public class LiveStatProviderHost : MonoBehaviour
{
    [SerializeField, Tooltip("Log one line per shot on both live and fallback paths. Helpful during wiring; turn off for ship.")]
    bool _enableDiagLog = true;

    void Awake()
    {
        StatProviderBus.Resolver = ResolveLive;
    }

    void OnDestroy()
    {
        if (StatProviderBus.Resolver == (System.Func<bool, StatBundle?>)ResolveLive)
            StatProviderBus.Resolver = null!;
    }

    StatBundle? ResolveLive(bool isPutt)
    {
        // ── CHARACTER ──────────────────────────────────────────────────────────
        string charId = GameSession.SelectedCharacterId;
        if (string.IsNullOrEmpty(charId))
        {
            if (_enableDiagLog) Debug.Log($"[LiveStatProvider] FALLBACK {(isPutt ? "putt" : "swing")} reason=no-character");
            return null;
        }

        var charData = CharacterManager.Instance?.GetCharacterData(charId);
        if (charData == null)
        {
            if (_enableDiagLog) Debug.Log($"[LiveStatProvider] FALLBACK {(isPutt ? "putt" : "swing")} reason=character-lookup-failed char={charId}");
            return null;
        }

        var characterStats = BuildCharacterStats(charData);

        // ── BALL ───────────────────────────────────────────────────────────────
        string ballId = BallContext.SelectedBallId;
        if (string.IsNullOrEmpty(ballId))
        {
            if (_enableDiagLog) Debug.Log($"[LiveStatProvider] FALLBACK {(isPutt ? "putt" : "swing")} reason=no-ball char={charId}");
            return null;
        }

        var ballTemplate = BallDatabaseCSV.Instance?.GetBall(ballId);
        if (ballTemplate == null)
        {
            if (_enableDiagLog) Debug.Log($"[LiveStatProvider] FALLBACK {(isPutt ? "putt" : "swing")} reason=ball-lookup-failed ball={ballId}");
            return null;
        }

        var ballStats = BuildBallStats(ballTemplate);

        if (isPutt)
        {
            // ── PUTTER — read player's equipped putter from ClubContext.EquippedBag (LabClubIndex == 3) ──
            ClubEntry? putterEntry = null;
            foreach (var entry in ClubContext.EquippedBag)
            {
                if (entry.LabClubIndex == 3) { putterEntry = entry; break; }
            }

            if (putterEntry == null)
            {
                if (_enableDiagLog) Debug.Log($"[LiveStatProvider] FALLBACK putt reason=no-putter char={charId} ball={ballId}");
                return null;
            }

            var putterTemplate = ClubDatabaseCSV.Instance?.GetClub(putterEntry.ClubId);
            if (putterTemplate == null)
            {
                if (_enableDiagLog) Debug.Log($"[LiveStatProvider] FALLBACK putt reason=putter-lookup-failed putter={putterEntry.ClubId}");
                return null;
            }

            var putterClubData = ClubManager.Instance?.GetClubData(putterEntry.ClubId);
            if (putterClubData == null)
            {
                if (_enableDiagLog) Debug.Log($"[LiveStatProvider] FALLBACK putt reason=putter-lookup-failed putter={putterEntry.ClubId}");
                return null;
            }

            var putterStats = BuildPutterStats(putterClubData, putterTemplate);
            var bundle = new StatBundle(
                putterStats,
                ballStats,
                characterStats,
                fp.FromFloat(charData.currentStaminaEnergy),
                fp.FromFloat(charData.maxStaminaEnergy));

            if (_enableDiagLog) Debug.Log($"[LiveStatProvider] LIVE putt char={charId} ball={ballId} club=putter:{putterEntry.ClubId}");
            return bundle;
        }

        // ── CLUB (swing) ───────────────────────────────────────────────────────
        string clubId = ClubContext.SelectedClubId;
        if (string.IsNullOrEmpty(clubId))
        {
            if (_enableDiagLog) Debug.Log($"[LiveStatProvider] FALLBACK swing reason=no-club char={charId} ball={ballId}");
            return null;
        }

        var clubTemplate = ClubDatabaseCSV.Instance?.GetClub(clubId);
        if (clubTemplate == null)
        {
            if (_enableDiagLog) Debug.Log($"[LiveStatProvider] FALLBACK swing reason=club-lookup-failed club={clubId}");
            return null;
        }

        var clubPlayerData = ClubManager.Instance?.GetClubData(clubId);
        if (clubPlayerData == null)
        {
            if (_enableDiagLog) Debug.Log($"[LiveStatProvider] FALLBACK swing reason=club-lookup-failed club={clubId}");
            return null;
        }

        var clubStats = BuildClubStats(clubPlayerData, clubTemplate);
        var swingBundle = new StatBundle(
            clubStats,
            ballStats,
            characterStats,
            fp.FromFloat(charData.currentStaminaEnergy),
            fp.FromFloat(charData.maxStaminaEnergy));

        if (_enableDiagLog) Debug.Log($"[LiveStatProvider] LIVE swing char={charId} club={clubId} ball={ballId}");
        return swingBundle;
    }

    // ── Mapping helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Maps PlayerCharacterData (roster level/SP state) → CharacterStats (physics struct).
    /// Uses current* int stat values which already include SP allocation.
    /// </summary>
    static CharacterStats BuildCharacterStats(PlayerCharacterData data)
    {
        return new CharacterStats(
            strength:    data.currentStrength,
            clubControl: data.currentClubControl,
            recovery:    data.currentRecovery,
            stamina:     data.currentStamina);
    }

    /// <summary>
    /// Maps PlayerClubData + ClubDataRuntime (inventory state) → ClubStats (physics struct).
    /// Base stats come from the CSV template; SP allocations are applied via GetXxx(template).
    /// Physics flight params (velocity, loft, backspin) come from the CSV template fields
    /// added in this task (ballSpeedMps, launchAngleDeg, spinRateRpm).
    /// </summary>
    static ClubStats BuildClubStats(PlayerClubData pcd, ClubDataRuntime tpl)
    {
        return new ClubStats(
            power:           pcd.GetPower(tpl),
            accuracy:        pcd.GetAccuracy(tpl),
            lieResistance:   pcd.GetLieResistance(tpl),
            durability:      pcd.currentDurability,
            loftDegrees:     fp.FromFloat(tpl.launchAngleDeg),
            baseVelocityMps: fp.FromFloat(tpl.ballSpeedMps),
            baseBackspinRpm: fp.FromFloat(tpl.spinRateRpm));
    }

    /// <summary>
    /// Maps PlayerClubData + ClubDataRuntime (putter) → PutterStats (physics struct).
    /// Field mapping per Q2 lock:
    ///   Accuracy      = pcd.GetAccuracy(tpl)    — gravity well radius
    ///   Control       = pcd.GetLieResistance(tpl) — off-center forgiveness
    ///   Weight        = pcd.GetPower(tpl)        — putter "feel weight" (power slot)
    ///   Durability    = pcd.currentDurability
    ///   LoftDegrees   = tpl.launchAngleDeg       — CSV loft
    ///   BaseVelocityMps = 5f canonical             — CSV doesn't carry a putter-velocity field today
    /// </summary>
    static PutterStats BuildPutterStats(PlayerClubData pcd, ClubDataRuntime tpl)
    {
        return new PutterStats(
            control:         pcd.GetLieResistance(tpl),
            accuracy:        pcd.GetAccuracy(tpl),
            weight:          pcd.GetPower(tpl),
            durability:      pcd.currentDurability,
            loftDegrees:     fp.FromFloat(tpl.launchAngleDeg),
            baseVelocityMps: fp.FromFloat(5f));  // canonical DefaultPutter value; CSV putter-velocity TBD
    }

    /// <summary>
    /// Maps BallDataRuntime (ball template) → BallStats (physics struct).
    /// windResistance in BallDataRuntime corresponds to WindCut in BallStats.
    /// </summary>
    static BallStats BuildBallStats(BallDataRuntime tpl)
    {
        return new BallStats(
            power:   tpl.power,
            rebound: tpl.rebound,
            windCut: tpl.windResistance,
            roll:    tpl.roll,
            spin:    tpl.spin);
    }
}
