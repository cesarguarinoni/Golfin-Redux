#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Golfin.Gameplay.Missions
{
    /// <summary>
    /// One mission, RESOLVED — every component already looked up and turned into the handful
    /// of facts a session needs. Spec: missions_v1 §B2.
    ///
    /// This is deliberately NOT the `missions` catalog row. A row says
    /// `startAreaId=GREEN, windPresetId=CROSS_S, loadoutId=SUP_PUTTER`; those are names that
    /// only mean something after three more catalogs have been read. Resolving once, at the
    /// screen, means the session cannot half-resolve a mission at the moment play begins —
    /// which is the moment there is no good way to fail.
    ///
    /// The DAILY produces one of these too, from its recipe. Nothing downstream of here knows
    /// or cares which it is playing.
    /// </summary>
    public sealed class MissionDefinition
    {
        /// <summary>Catalog row id ("7"), or "daily:2026-09-01" for the daily.</summary>
        public string Id = "";

        /// <summary>Localization key for the display name, or "" for the daily.</summary>
        public string NameKey = "";

        /// <summary>Campaign ladder position. The screen sorts by this.</summary>
        public int Order;

        /// <summary>`Beginner` | `Amateur` | `Pro` | `Legend`.</summary>
        public string Tier = "";

        /// <summary>The stable slug (`b_first_putt`) the name key is built from.</summary>
        public string Key = "";

        /// <summary>What the card ADVERTISES. What is actually paid is decided by the server
        /// from `golfin_mission_rewards`; these two agreeing is the publish validator's job,
        /// not this client's.</summary>
        public int FirstClearRP;
        public int ReplayRP;

        /// <summary>Display only — the publish RECOMPUTES it from the component weights.</summary>
        public int DifficultyScore;

        /// <summary>`start` or `clear:&lt;mission id&gt;`.</summary>
        public string Unlock = "";

        /// <summary>Raw `itemRewards` cell, e.g. `RepairKit x1`. "" when there are none.</summary>
        public string ItemRewards = "";

        /// <summary>The wind preset's id, for the card's wind line.</summary>
        public string WindPresetId = "";

        /// <summary>The loadout's id, for the card's clubs line.</summary>
        public string LoadoutId = "";

        public int HoleNumber = 1;
        public int Par = 4;

        // ── Start ────────────────────────────────────────────────────────────────

        /// <summary>`GREEN` | `FRINGE` | `FAIRWAY` | `ROUGH` | `SAND` | `TEE_*`.</summary>
        public string StartAreaId = "";

        /// <summary>`tee` or `short`.</summary>
        public string StartKind = "tee";

        /// <summary>
        /// Baked world position for a SHORT start. Null for a tee start, which resolves at
        /// load time to the scene's own `TeeMarker_&lt;label&gt;_L/R` midpoint — the markers are
        /// scene objects and there is nothing to bake.
        /// </summary>
        public Vector3? StartWorld;

        /// <summary>`ladies` | `front` | `regular` | `back` for a tee start, else "".</summary>
        public string TeeLabel = "";

        // ── Pin ──────────────────────────────────────────────────────────────────

        /// <summary>Index into <c>GreenTopology.GetPinCandidates()</c>. 0 = the default pin.</summary>
        public int PinIndex;

        // ── Wind ─────────────────────────────────────────────────────────────────

        /// <summary>Degrees RELATIVE to the spawn→pin bearing. 0 = tailwind, 180 = headwind.</summary>
        public float WindRelDirDeg;

        /// <summary>mph. For <see cref="WindGusty"/> this is the ceiling of the re-roll range.</summary>
        public float WindSpeedMph;

        /// <summary>GUSTY re-rolls its speed in [6, 18] on every completed shot.</summary>
        public bool WindGusty;

        // ── Loadout ──────────────────────────────────────────────────────────────

        /// <summary>
        /// The exact club ids this mission plays with, already resolved from the loadout —
        /// supplied types at a rarity, or the player's own bag minus the banned types.
        /// EMPTY means "no override", which is not a state a mission should reach: an empty
        /// bag is the "never a dead card" invariant failing, and the screen refuses to start.
        /// </summary>
        public readonly List<string> ClubIds = new List<string>();

        /// <summary>true when <see cref="ClubIds"/> came from a `supplied:` loadout. Supplied
        /// clubs are not owned, so they never wear.</summary>
        public bool LoadoutSupplied;

        /// <summary>Localization key for the loadout line on the card.</summary>
        public string LoadoutKey = "";

        // ── Session rules ────────────────────────────────────────────────────────

        /// <summary>Condition drained at hole completion; overrides the config's flat 8.</summary>
        public float StaminaDrain = 8f;

        /// <summary>The character starts at 50 % condition (the daily LOW_STAMINA_START).</summary>
        public bool LowStaminaStart;

        /// <summary>Reward is doubled (the daily DOUBLE_RP). The SERVER applies it; this is
        /// carried only so the card can say so.</summary>
        public bool DoubleRp;

        public readonly List<MissionGoal> Goals = new List<MissionGoal>();

        /// <summary>
        /// The tightest stroke cap the goals imply, as strokes OVER PAR, or null when no goal
        /// bounds the stroke count.
        ///
        /// This is what makes a failed mission END rather than drag on: a SCORE 0 mission is
        /// already lost the moment the player is 1 over, and playing out four more shots to be
        /// told so is the worst version of that news. `GameSession.StrokeCapEnabled` +
        /// `StrokeCapOverPar` is the existing seam — documented in GameSession as the Missions
        /// opt-in before this task existed.
        /// </summary>
        public int? StrokeCapOverPar
        {
            get
            {
                int? tightest = null;
                foreach (var goal in Goals)
                {
                    int? cap = goal.ImpliedStrokeCapOverPar(Par);
                    if (cap.HasValue && (!tightest.HasValue || cap.Value < tightest.Value))
                        tightest = cap;
                }
                return tightest;
            }
        }

        public bool IsDaily => Id.StartsWith("daily:", System.StringComparison.Ordinal);

        /// <summary>Localization key for this mission's wind line (`WIND_CROSS_S`).</summary>
        public string WindKey => string.IsNullOrEmpty(WindPresetId) ? "" : "WIND_" + WindPresetId;

        /// <summary>Localization key for this mission's start-area line (`START_AREA_SAND`).</summary>
        public string StartAreaKey => string.IsNullOrEmpty(StartAreaId) ? "" : "START_AREA_" + StartAreaId;
    }
}
