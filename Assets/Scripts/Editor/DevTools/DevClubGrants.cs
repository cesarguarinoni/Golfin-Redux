// ─────────────────────────────────────────────────────────────────────────────
// DevClubGrants — EDITOR ONLY
//
// Unlock every club in Clubs.csv on the current save, and put it back again.
// For testing the inventory at full roster size; NOT a shipping cheat — the whole
// file is inside `#if UNITY_EDITOR` and lives in an Editor-only folder.
//
// Both commands require PLAY MODE, because ownership lives on the running
// ClubManager + SaveDataHost, and they go through the production grant path
// (ClubManager.GrantClub / ClubOwnershipService.SeedStarter) rather than editing
// save.json behind the game's back.
//
// ⚠️ Grant All writes ~799 clubs into save.json. Take a backup first if the save
//    matters — Reset restores the starter set, but not levels/SP you had spent.
// ─────────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Golfin.Inventory;
using Golfin.Save;
using Debug = UnityEngine.Debug;

namespace Golfin.EditorTools
{
    public static class DevClubGrants
    {
        private const string GrantPath = "GOLFIN/Dev/Grant All Clubs (Editor Only)";
        private const string ResetPath = "GOLFIN/Dev/Reset Clubs To Starter (Editor Only)";

        [MenuItem(GrantPath, true)]
        [MenuItem(ResetPath, true)]
        private static bool RequiresPlayMode() => EditorApplication.isPlaying;

        // ── Grant all ─────────────────────────────────────────────────────────

        [MenuItem(GrantPath)]
        public static void GrantAll()
        {
            var db = ClubDatabaseCSV.Instance;
            var mgr = ClubManager.Instance;
            if (db == null || mgr == null)
            {
                Debug.LogError("[DevClubGrants] ClubDatabaseCSV / ClubManager not live. Enter play mode first.");
                return;
            }

            var all = db.GetAllClubs();
            int before = mgr.GetAllOwnedClubs().Count;

            // Every GrantClub call persists AND fires OnInventoryChanged, which rebuilds any open
            // club carousel. Doing this while the Clubs screen is open would rebuild it ~799 times,
            // so grant from Home (or any non-Inventory screen) and navigate afterwards.
            var sw = Stopwatch.StartNew();
            int granted = 0, already = 0, invalid = 0;
            foreach (var c in all)
            {
                switch (mgr.GrantClub(c.clubId))
                {
                    case ClubGrantResult.Success:      granted++; break;
                    case ClubGrantResult.AlreadyOwned: already++; break;
                    default:                           invalid++; break;
                }
            }
            sw.Stop();

            int after = mgr.GetAllOwnedClubs().Count;
            Debug.Log($"[DevClubGrants] Granted {granted} (already owned {already}, invalid {invalid}) " +
                      $"in {sw.Elapsed.TotalMilliseconds:F0}ms. Owned {before} → {after} of {all.Count} in the DB.");
            Debug.Log("[DevClubGrants] Bag is unchanged — grants land UNEQUIPPED (D5, no auto-equip).");
        }

        // ── Reset ─────────────────────────────────────────────────────────────

        [MenuItem(ResetPath)]
        public static void ResetToStarter()
        {
            var db = ClubDatabaseCSV.Instance;
            var host = SaveDataHost.Instance;
            if (db == null || host == null)
            {
                Debug.LogError("[DevClubGrants] ClubDatabaseCSV / SaveDataHost not live. Enter play mode first.");
                return;
            }

            // Reuse the REAL starter list off ClubManager rather than re-declaring it here, so this
            // can never drift from what a fresh player actually receives.
            var starterIds = (string[])typeof(ClubManager)
                .GetField("DefaultBagIds", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;

            var catalog = db.GetAllClubs()
                .Select(c => new ClubCatalogSpec(c.clubId, 1, c.maxDurability, 0, c.type.ToString()))
                .ToList();

            ClubOwnershipService.SeedStarter(host.Data, catalog, starterIds);
            host.Data.clubOwnershipSeeded = true;
            host.MarkDirty();

            Debug.LogWarning($"[DevClubGrants] Save reset to the {host.Data.ownedClubs.Count}-club starter set. " +
                             "EXIT AND RE-ENTER PLAY MODE — ClubManager hydrates its runtime dictionary in " +
                             "Awake, so the in-memory state is stale until it re-initialises.");
        }
    }
}
#endif
