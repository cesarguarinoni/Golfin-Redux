// ─────────────────────────────────────────────────────────────────────────────
// InventoryCatalogAdapter — Assembly-CSharp's answer to "what does a freshly-
// granted club/character look like", handed to Golfin.InventorySync.
//
// Spec: Docs/Specs/Active/content_player_inventory/SPEC.md §1 (deltas-from-default)
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections;
using System.Collections.Generic;
using Golfin.Content;
using Golfin.Inventory;
using Golfin.InventorySync;
using Golfin.Roster;
using Golfin.Save;
using UnityEngine;

namespace GolfinRedux.InventorySync
{
    /// <summary>
    /// The catalog half of the inventory sync, living in Assembly-CSharp because that is the only
    /// assembly that can see <c>ClubDatabaseCSV</c>, <c>CharacterDatabaseCSV</c>, <c>ClubManager</c>
    /// and <c>CharacterManager</c>.
    ///
    /// <para>
    /// THE SAME SPLIT AS <c>ClubCatalogSpec</c>: the RULES (encode, decode, merge, apply) live in
    /// <c>Golfin.InventorySync</c> where an EditMode test can load them with no scene, and the DATA
    /// comes in through a seam from here. Everything that can lose a player's inventory is on the
    /// testable side of that line, and this file is a lookup table.
    /// </para>
    ///
    /// <para>
    /// BUILT ONCE, LAZILY, AND ONLY WHEN THE DATABASES ARE UP. A catalog built from an empty
    /// database would claim every club's default is level 0 / durability 0, and every real club
    /// would then encode as a delta against nonsense — bigger, not wrong, but a bare-id row decoded
    /// against it WOULD be wrong. So <see cref="Build"/> refuses to cache anything until both
    /// databases report loaded, and until then the sync runs with
    /// <c>EmptyInventoryCatalog</c>: no compression, full rows, nothing lost.
    /// </para>
    /// </summary>
    public sealed class InventoryCatalogAdapter : MonoBehaviour, IInventoryCatalog
    {
        private const string Tag = "[InventoryCatalog]";

        private static InventoryCatalogAdapter? _instance;

        private readonly Dictionary<string, PersistedClub> _clubs =
            new Dictionary<string, PersistedClub>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, PersistedCharacter> _characters =
            new Dictionary<string, PersistedCharacter>(System.StringComparer.Ordinal);

        private bool _built;

        /// <summary>
        /// Self-bootstrapping, like <c>GolfinCharacterSync</c> and <c>InventorySyncBehaviour</c> —
        /// there is no natural owner on any screen and no inspector state to author.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;

            var go = new GameObject("[InventoryCatalog]");
            _instance = go.AddComponent<InventoryCatalogAdapter>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Installed IMMEDIATELY, before the tables are populated. TryGet* answers false until
            // Build() succeeds, which is exactly the EmptyInventoryCatalog behaviour — so there is
            // no window where the sync holds a half-built catalog.
            InventorySyncService.Instance.Catalog = this;

            // starter_restore_gate §4 — the OTHER half of the sync that Assembly-CSharp has to own.
            // The service merges the server blob into SaveData; the four managers below each built
            // their runtime dictionary once, in their own Awake, and would otherwise keep serving
            // the pre-restore answer until the next launch. This component already holds all four,
            // so the subscription lives here rather than in a fifth self-bootstrapping object.
            InventorySyncService.Instance.OnRestored += OnInventoryRestored;

            StartCoroutine(BuildWhenReady());
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
            InventorySyncService.Instance.OnRestored -= OnInventoryRestored;
            if (InventorySyncService.Instance.Catalog == (IInventoryCatalog)this)
                InventorySyncService.Instance.Catalog = EmptyInventoryCatalog.Instance;
        }

        /// <summary>
        /// A merge (boot restore or stale-PUT) changed the save — re-read it everywhere.
        ///
        /// <para>
        /// Main thread: <c>ApiClient</c> completes its callbacks there, which is what makes touching
        /// these MonoBehaviour singletons legal from a network callback at all.
        /// <c>GachaTicketManager</c> is deliberately absent — it reads <c>SaveDataHost.Data</c>
        /// through on every call and caches nothing, so it has nothing to re-read.
        /// </para>
        /// </summary>
        private static void OnInventoryRestored()
        {
            if (CharacterManager.Instance != null) CharacterManager.Instance.ReloadFromSave();
            if (ClubManager.Instance      != null) ClubManager.Instance.RehydrateFromSave();
            if (ItemManager.Instance      != null) ItemManager.Instance.ReloadFromSave();
            if (BallManager.Instance      != null) BallManager.Instance.ReloadFromSave();
        }

        /// <summary>Poll the databases into place, the same shape as
        /// <c>GolfinCharacterSync.SubscribeToRosterWhenReady</c>. One frame per attempt, and it
        /// stops the moment it builds.</summary>
        private IEnumerator BuildWhenReady()
        {
            while (!_built)
            {
                if (Build()) yield break;
                yield return null;
            }
        }

        private bool Build()
        {
            var clubDb = ClubDatabaseCSV.Instance;
            var charDb = CharacterDatabaseCSV.Instance;
            var clubMgr = ClubManager.Instance;
            var charMgr = CharacterManager.Instance;

            if (clubDb == null || !clubDb.IsLoaded) return false;
            if (charDb == null || !charDb.IsLoaded) return false;
            if (clubMgr == null || charMgr == null) return false;

            // Clubs: exactly what ClubOwnershipService.MakePersisted(spec, 0) produces — a freshly
            // GRANTED club, unequipped. The two must not drift; the encoder compares against this
            // and a granted club is meant to compress to its bare id.
            foreach (var spec in clubMgr.BuildCatalogSpecs())
            {
                if (string.IsNullOrEmpty(spec.clubId)) continue;
                _clubs[spec.clubId] = ClubOwnershipService.MakePersisted(spec, bagSlot: 0);
            }

            // Characters: catalog start level, nothing spent, OWNED — see IInventoryCatalog for why
            // owned is the default rather than locked.
            foreach (var kv in charMgr.BuildCharacterClampDefinitions())
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                _characters[kv.Key] = new PersistedCharacter
                {
                    characterId = kv.Key,
                    currentLevel = kv.Value.StartLevel,
                    isOwned = true,
                };
            }

            _built = _clubs.Count > 0 || _characters.Count > 0;
            if (_built)
                Debug.Log($"{Tag} Built {_clubs.Count} club and {_characters.Count} character " +
                          "defaults — the inventory blob now compresses to deltas.");
            return _built;
        }

        // ── IInventoryCatalog ─────────────────────────────────────────────────

        public bool TryGetClubDefault(string clubId, out PersistedClub def)
        {
            if (!string.IsNullOrEmpty(clubId) && _clubs.TryGetValue(clubId, out var found))
            {
                def = found;
                return true;
            }
            def = null!;
            return false;
        }

        public bool TryGetCharacterDefault(string characterId, out PersistedCharacter def)
        {
            if (!string.IsNullOrEmpty(characterId) && _characters.TryGetValue(characterId, out var found))
            {
                def = found;
                return true;
            }
            def = null!;
            return false;
        }
    }
}
