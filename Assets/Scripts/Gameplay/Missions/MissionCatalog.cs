#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Golfin.Content;
using UnityEngine;

namespace Golfin.Gameplay.Missions
{
    /// <summary>
    /// Reads the seven mission catalogs and RESOLVES a campaign row into the handful of facts a
    /// session needs. Spec: missions_v1 §A1/§B2.
    ///
    /// ⚠️ RESOLUTION HAPPENS HERE, ONCE, AND NOT AT PLAY TIME. A `missions` row says
    /// `startAreaId=GREEN, windPresetId=CROSS_S, loadoutId=SUP_PUTTER` — three names that mean
    /// nothing until three more catalogs have been read. Doing that lookup when the player taps
    /// PLAY would mean discovering a broken mission at the one moment there is no good way to
    /// fail. Resolving at screen-build time instead means a mission that cannot be assembled
    /// renders as a warned, un-playable card — the standing invariant, which is that a client
    /// missing information never shows a broken card and never wrongly spends or earns.
    ///
    /// BUNDLED CSV IS THE FLOOR, THE PUBLISHED CATALOG IS THE OVERLAY — the same contract
    /// `ModesDatabaseCSV` follows, and for the same reason: a build must be playable with no
    /// network, and an admin edit must reach it without one.
    ///
    /// ⚠️ THE CLUB LOOKUP IS INJECTED, and that is what keeps this class testable. Resolving a
    /// `supplied:` loadout needs `ClubDatabaseCSV`, and an `own:` one needs the player's bag —
    /// both in Assembly-CSharp, which this LEAF assembly may not reference (it is what the
    /// Viewer and the Hole Complete modal reference, so the arrow only goes one way).
    ///
    /// The first version put the whole catalog in Assembly-CSharp to reach them, and the cost
    /// showed up immediately: EditMode tests would have had to drive it by REFLECTION, the way
    /// ModesOverlayTests reaches ModesDatabaseCSV. Injecting one delegate instead
    /// (<see cref="ClubResolver"/>, installed at boot by `MissionLoadoutResolver`) moves the
    /// ONE Assembly-CSharp dependency out and leaves everything else — the overlay, the seven
    /// catalogs, the unlock rules — directly testable.
    /// </summary>
    public static class MissionCatalog
    {
        private const string Tag = "[MissionCatalog]";

        /// <summary>Every campaign mission, in campaign order.</summary>
        public static IReadOnlyList<MissionDefinition> All => _all;

        /// <summary>Why a mission cannot be played, or "" when it can. Keyed by mission id.</summary>
        public static IReadOnlyDictionary<string, string> Warnings => _warnings;

        private static readonly List<MissionDefinition> _all = new List<MissionDefinition>();
        private static readonly Dictionary<string, string> _warnings = new Dictionary<string, string>();
        private static readonly List<MissionTier> _tiers = new List<MissionTier>();
        private static bool _loaded;

        /// <summary>
        /// Turns a `mission_loadouts` row into the club ids a mission plays with, plus a
        /// warning when it cannot. Installed at boot by `MissionLoadoutResolver`
        /// (Assembly-CSharp), which is the only code that can see the club catalog and the
        /// player's bag.
        ///
        /// NULL is a real state, not a bug: in EditMode, and before the databases have woken
        /// up, there is nothing to resolve against. Every mission still resolves its hole,
        /// start, wind and goals — only the bag is missing, and the card says so.
        /// </summary>
        public delegate List<string> ClubResolverFn(Dictionary<string, string> loadoutRow, out string warning);

        public static ClubResolverFn? ClubResolver;

        public sealed class MissionTier
        {
            public string Tier = "";
            public int Order;
            public int ScoreMin;
            public int ScoreMaxExcl;
            public int FirstClearRP;
            public int ReplayRP;
            public int TierClearBonusRP;
            /// <summary>Clears of the PREVIOUS tier that open this one. 0 = always open.</summary>
            public int UnlockClears;
            public int MissionsInTier = 10;
        }

        public static IReadOnlyList<MissionTier> Tiers { get { EnsureLoaded(); return _tiers; } }

        public static void Reload() { _loaded = false; EnsureLoaded(); }

        /// <summary>
        /// The world XZ extent of one hole, for projecting a start point into its top-down
        /// thumbnail (§C2's start marker).
        ///
        /// It is derived from the hole's OWN BAKED START AREAS rather than from a second table
        /// of hole extents. Those five points — green, fringe, a 110 m fairway spot, the rough
        /// beside it, the greenside bunker — already span the part of the hole a player looks
        /// at, and they are the only per-hole world coordinates this feature has that are
        /// tracked, drift-gated and reproducible. Inventing a second source would be one more
        /// thing to keep in step with the bake.
        ///
        /// Padded by 15 % so a start ON the boundary does not render on the very edge of the
        /// thumbnail. False when the hole has no baked areas at all — the caller then draws no
        /// marker, which is better than one in the wrong place.
        /// </summary>
        public static bool TryGetHoleBounds(int holeNumber, out Vector2 min, out Vector2 max)
        {
            EnsureLoaded();
            min = max = Vector2.zero;
            if (!_holeBounds.TryGetValue(holeNumber, out var b)) return false;
            min = b.min; max = b.max;
            return true;
        }

        private static readonly Dictionary<int, (Vector2 min, Vector2 max)> _holeBounds =
            new Dictionary<int, (Vector2, Vector2)>();

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _all.Clear();
            _warnings.Clear();
            _tiers.Clear();

            var areas    = Index(ContentCatalogs.MissionStartAreas, "id");
            var winds    = Index(ContentCatalogs.MissionWindPresets, "id");
            var loadouts = Index(ContentCatalogs.MissionLoadouts, "id");
            var tierRows = Index(ContentCatalogs.MissionTiers, "tier");
            var missions = Rows(ContentCatalogs.Missions);

            foreach (var t in tierRows.Values)
            {
                _tiers.Add(new MissionTier
                {
                    Tier = Str(t, "tier"),
                    Order = Int(t, "order"),
                    ScoreMin = Int(t, "scoreMin"),
                    ScoreMaxExcl = Int(t, "scoreMaxExcl"),
                    FirstClearRP = Int(t, "firstClearRP"),
                    ReplayRP = Int(t, "replayRP"),
                    TierClearBonusRP = Int(t, "tierClearBonusRP"),
                    UnlockClears = Int(t, "unlockClears"),
                    MissionsInTier = Math.Max(1, Int(t, "missionsInTier", 10)),
                });
            }
            _tiers.Sort((a, b) => a.Order.CompareTo(b.Order));

            // Index the per-hole start areas by (holeId, areaId) — the pair a mission names.
            // The same areaId is a DIFFERENT point on a different hole, so indexing by areaId
            // alone would silently start a mission on another hole's green.
            var areaByHoleAndId = new Dictionary<string, Dictionary<string, string>>();
            _holeBounds.Clear();
            var boundsAcc = new Dictionary<int, (float minX, float minZ, float maxX, float maxZ)>();
            foreach (var a in areas.Values)
            {
                areaByHoleAndId[$"{Str(a, "holeId")}:{Str(a, "areaId")}"] = a;

                // Accumulate the hole's extent from whatever IS baked. Tee rows carry no
                // coordinates by design, so a hole's box is its short areas — which is the part
                // of the hole the thumbnail is mostly showing anyway.
                string sx = Str(a, "x"), sz = Str(a, "z");
                if (sx.Length == 0 || sz.Length == 0) continue;
                int hole = Int(a, "holeId");
                float x = F(sx), z = F(sz);
                if (!boundsAcc.TryGetValue(hole, out var acc)) boundsAcc[hole] = (x, z, x, z);
                else boundsAcc[hole] = (Mathf.Min(acc.minX, x), Mathf.Min(acc.minZ, z),
                                        Mathf.Max(acc.maxX, x), Mathf.Max(acc.maxZ, z));
            }
            foreach (var kv in boundsAcc)
            {
                var a = kv.Value;
                float padX = Mathf.Max(1f, (a.maxX - a.minX) * 0.15f);
                float padZ = Mathf.Max(1f, (a.maxZ - a.minZ) * 0.15f);
                _holeBounds[kv.Key] = (new Vector2(a.minX - padX, a.minZ - padZ),
                                       new Vector2(a.maxX + padX, a.maxZ + padZ));
            }

            foreach (var row in missions)
            {
                var def = Resolve(row, areaByHoleAndId, winds, loadouts, out string warning);
                if (def == null) continue;
                _all.Add(def);
                if (!string.IsNullOrEmpty(warning)) _warnings[def.Id] = warning;
            }
            _all.Sort((a, b) => a.Order.CompareTo(b.Order));

            int warned = _warnings.Count;
            Debug.Log($"{Tag} {_all.Count} missions, {_tiers.Count} tiers" +
                      (warned > 0 ? $", {warned} un-playable (see warnings)" : ", all playable"));
        }

        // ── Resolution ──────────────────────────────────────────────────────────

        private static MissionDefinition? Resolve(
            Dictionary<string, string> row,
            Dictionary<string, Dictionary<string, string>> areaByHoleAndId,
            Dictionary<string, Dictionary<string, string>> winds,
            Dictionary<string, Dictionary<string, string>> loadouts,
            out string warning)
        {
            warning = "";
            string id = Str(row, "id");
            if (string.IsNullOrEmpty(id)) return null;

            var def = new MissionDefinition
            {
                Id = id,
                Order = Int(row, "order"),
                Tier = Str(row, "tier"),
                Key = Str(row, "key"),
                NameKey = "MISSION_NAME_" + Str(row, "key").ToUpperInvariant(),
                HoleNumber = Int(row, "holeId", 1),
                Par = Int(row, "par", 4),
                StartAreaId = Str(row, "startAreaId"),
                PinIndex = Int(row, "pinIndex"),
                StaminaDrain = Float(row, "staminaDrain", 8f),
                FirstClearRP = Int(row, "firstClearRP"),
                ReplayRP = Int(row, "replayRP"),
                DifficultyScore = Int(row, "difficultyScore"),
                Unlock = Str(row, "unlock"),
                ItemRewards = Str(row, "itemRewards"),
            };

            for (int slot = 1; slot <= 3; slot++)
            {
                string type = Str(row, $"goal{slot}Type");
                if (string.IsNullOrEmpty(type)) continue;
                var parsed = MissionGoal.Parse(type);
                if (parsed == MissionGoalType.None)
                {
                    warning = $"goal{slot} type '{type}' is not one this build understands";
                    continue;
                }
                def.Goals.Add(new MissionGoal(parsed, Str(row, $"goal{slot}Param")));
            }

            // ── Start area ───────────────────────────────────────────────────────
            if (!areaByHoleAndId.TryGetValue($"{def.HoleNumber}:{def.StartAreaId}", out var area))
            {
                warning = $"start area '{def.StartAreaId}' has no row for hole {def.HoleNumber}";
                return def;
            }
            def.StartKind = Str(area, "kind").ToLowerInvariant();
            if (def.StartKind == "tee")
            {
                // TEE_BACK -> "back". The scene's own TeeMarker_<label>_L/R is the spawn; there
                // is nothing to bake and nothing to resolve here.
                def.TeeLabel = def.StartAreaId.StartsWith("TEE_", StringComparison.Ordinal)
                    ? def.StartAreaId.Substring(4).ToLowerInvariant()
                    : "regular";
            }
            else
            {
                string sx = Str(area, "x"), sy = Str(area, "y"), sz = Str(area, "z");
                if (sx.Length == 0 || sy.Length == 0 || sz.Length == 0)
                {
                    // The Phase-A state of every short area, and the state hole 13's SAND is in
                    // permanently — it has no greenside bunker to bake.
                    warning = $"start area '{def.StartAreaId}' on hole {def.HoleNumber} has not been " +
                              "baked (Golfin ▸ Missions ▸ Bake Start Areas)";
                    return def;
                }
                def.StartWorld = new Vector3(F(sx), F(sy), F(sz));
            }

            // ── Wind ─────────────────────────────────────────────────────────────
            string windId = Str(row, "windPresetId");
            if (!winds.TryGetValue(windId, out var wind))
            {
                warning = $"wind preset '{windId}' does not exist";
                return def;
            }
            def.WindPresetId = windId;
            def.WindRelDirDeg = Float(wind, "relDirDeg");
            def.WindSpeedMph = Float(wind, "speed");
            def.WindGusty = string.Equals(windId, "GUSTY", StringComparison.OrdinalIgnoreCase);

            // ── Loadout ──────────────────────────────────────────────────────────
            string loadoutId = Str(row, "loadoutId");
            if (!loadouts.TryGetValue(loadoutId, out var loadout))
            {
                warning = $"loadout '{loadoutId}' does not exist";
                return def;
            }
            def.LoadoutId = loadoutId;
            def.LoadoutKey = "LOADOUT_" + loadoutId.ToUpperInvariant();
            def.LoadoutSupplied = string.Equals(Str(loadout, "kind"), "supplied", StringComparison.OrdinalIgnoreCase);

            string loadoutWarning = "the club catalog is not loaded yet";
            var clubs = ClubResolver != null ? ClubResolver(loadout, out loadoutWarning) : new List<string>();
            if (clubs.Count == 0)
            {
                // §C3 — never a dead card. The screen renders this with the Hole Selection
                // "missing equipment" style and PLAY disabled, rather than letting the player
                // into a hole with an empty bag.
                warning = loadoutWarning.Length > 0 ? loadoutWarning : $"loadout '{loadoutId}' resolved to an empty bag";
                return def;
            }
            def.ClubIds.AddRange(clubs);
            if (loadoutWarning.Length > 0) warning = loadoutWarning;

            return def;
        }

        /// <summary>
        /// Compose a <see cref="MissionDefinition"/> from loose component ids — the DAILY's
        /// path in, where the "row" was generated this morning rather than authored.
        ///
        /// It reuses the SAME resolution the campaign uses (the same start-area lookup by
        /// (hole, area), the same wind preset, the same club resolver), so a daily and a
        /// campaign mission are indistinguishable to everything downstream. Returns null when
        /// any component fails to resolve — the caller then hides the card rather than showing
        /// one nobody can play.
        /// </summary>
        public static MissionDefinition? BuildFromRecipe(
            string id, int holeNumber, int par, string startAreaId, string windPresetId,
            string loadoutId, int pinIndex, float staminaDrain)
        {
            EnsureLoaded();

            var areas = Index(ContentCatalogs.MissionStartAreas, "id");
            var winds = Index(ContentCatalogs.MissionWindPresets, "id");
            var loadouts = Index(ContentCatalogs.MissionLoadouts, "id");

            Dictionary<string, string>? area = null;
            foreach (var a in areas.Values)
                if (Str(a, "holeId") == holeNumber.ToString() && Str(a, "areaId") == startAreaId) { area = a; break; }
            if (area == null || !winds.TryGetValue(windPresetId, out var wind)
                             || !loadouts.TryGetValue(loadoutId, out var loadout))
            {
                Debug.LogWarning($"{Tag} daily recipe names a component that does not resolve " +
                                 $"(hole {holeNumber} / {startAreaId} / {windPresetId} / {loadoutId}).");
                return null;
            }

            var def = new MissionDefinition
            {
                Id = id,
                // NOT the pill's key — the pill already says DAILY MISSION, and reusing it
                // here printed the words twice on the card. The daily's subtitle is its HOLE,
                // which is the one thing a player needs before tapping PLAY.
                NameKey = "",
                // The daily's base payout, so the card can advertise something. What is
                // actually paid is decided by `golfin_daily_claim` from `daily_mission.pts`
                // (plus any streak bonus and the DOUBLE_RP modifier) — this is the card's
                // number, and the server's is the one that lands.
                FirstClearRP = 30,
                HoleNumber = holeNumber,
                Par = par,
                StartAreaId = startAreaId,
                StartKind = Str(area, "kind").ToLowerInvariant(),
                PinIndex = pinIndex,
                StaminaDrain = staminaDrain,
                WindPresetId = windPresetId,
                WindRelDirDeg = Float(wind, "relDirDeg"),
                WindSpeedMph = Float(wind, "speed"),
                WindGusty = string.Equals(windPresetId, "GUSTY", StringComparison.OrdinalIgnoreCase),
                LoadoutId = loadoutId,
                LoadoutKey = "LOADOUT_" + loadoutId.ToUpperInvariant(),
                LoadoutSupplied = string.Equals(Str(loadout, "kind"), "supplied", StringComparison.OrdinalIgnoreCase),
                Tier = "",
            };

            if (def.StartKind != "tee")
            {
                string sx = Str(area, "x"), sy = Str(area, "y"), sz = Str(area, "z");
                if (sx.Length == 0 || sy.Length == 0 || sz.Length == 0)
                {
                    Debug.LogWarning($"{Tag} the daily's start area {startAreaId} on hole {holeNumber} is not baked.");
                    return null;
                }
                def.StartWorld = new Vector3(F(sx), F(sy), F(sz));
            }
            else
            {
                def.TeeLabel = startAreaId.StartsWith("TEE_", StringComparison.Ordinal)
                    ? startAreaId.Substring(4).ToLowerInvariant() : "regular";
            }

            // NOT `out _`. The caller drops the whole card when ClubIds is empty, so discarding
            // the resolver's reason meant the daily card could vanish with nothing in the log
            // saying why -- which is exactly how it vanished on 2026-08-29. The campaign path
            // above already keeps its warning; this one now does too.
            string loadoutWhy = "";
            var clubs = ClubResolver != null
                ? ClubResolver(loadout, out loadoutWhy)
                : new List<string>();
            if (clubs.Count == 0)
                Debug.LogWarning($"{Tag} the daily's loadout '{loadoutId}' resolved to no clubs" +
                                 (ClubResolver == null
                                     ? " — no ClubResolver is installed."
                                     : (loadoutWhy.Length > 0 ? $" — {loadoutWhy}." : ".")));
            def.ClubIds.AddRange(clubs);
            return def;
        }

        // ── Catalog access (bundled floor + published overlay) ──────────────────

        private static List<Dictionary<string, string>> Rows(string catalog)
        {
            var outRows = new List<Dictionary<string, string>>();

            // The BUNDLED CSV is the floor — a build with no network still has every mission.
            var csv = Resources.Load<TextAsset>($"Data/{catalog}");
            if (csv == null)
            {
                Debug.LogError($"{Tag} no bundled Resources/Data/{catalog}.csv — the catalog is empty.");
                return outRows;
            }
            var parsed = MissionCsv.Parse(csv.text);
            Resources.UnloadAsset(csv);
            foreach (var r in parsed) outRows.Add(r);

            // The PUBLISHED catalog is the overlay — an admin edit reaches an installed build.
            ContentCatalog? overlay = ContentCatalogStore.RequireReady(nameof(MissionCatalog))
                ? ContentCatalogStore.Catalog(catalog)
                : null;
            if (overlay == null) return outRows;

            string idCol = catalog == ContentCatalogs.MissionTiers ? "tier" : "id";
            var byId = new Dictionary<string, Dictionary<string, string>>();
            foreach (var r in outRows) if (r.TryGetValue(idCol, out var v)) byId[v] = r;

            int patched = 0, appended = 0, dropped = 0;
            foreach (var orow in overlay.Rows)
            {
                if (!orow.IsActive)
                {
                    // I6 — deactivation is how a mission is withdrawn. Dropping it here is what
                    // makes the card disappear rather than becoming unclaimable.
                    if (byId.TryGetValue(orow.Id, out var gone)) { outRows.Remove(gone); byId.Remove(orow.Id); dropped++; }
                    continue;
                }
                if (byId.TryGetValue(orow.Id, out var existing))
                {
                    foreach (var kv in orow.Data)
                        if (kv.Value != null) existing[kv.Key] = kv.Value;
                    patched++;
                }
                else
                {
                    var added = new Dictionary<string, string>();
                    foreach (var kv in orow.Data) added[kv.Key] = kv.Value ?? "";
                    added[idCol] = orow.Id;
                    outRows.Add(added);
                    appended++;
                }
            }
            if (patched + appended + dropped > 0)
                Debug.Log($"{Tag} {catalog}: overlay v{overlay.Version} — {patched} patched, " +
                          $"{appended} appended, {dropped} withdrawn.");
            return outRows;
        }

        private static Dictionary<string, Dictionary<string, string>> Index(string catalog, string idCol)
        {
            var map = new Dictionary<string, Dictionary<string, string>>();
            foreach (var r in Rows(catalog))
                if (r.TryGetValue(idCol, out var id) && id.Length > 0) map[id] = r;
            return map;
        }

        // ── Field helpers ───────────────────────────────────────────────────────

        private static string Str(Dictionary<string, string> r, string col)
            => r.TryGetValue(col, out var v) ? (v ?? "").Trim() : "";

        private static int Int(Dictionary<string, string> r, string col, int def = 0)
            => int.TryParse(Str(r, col), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : def;

        private static float Float(Dictionary<string, string> r, string col, float def = 0f)
            => float.TryParse(Str(r, col), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : def;

        private static float F(string s)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
    }
}
