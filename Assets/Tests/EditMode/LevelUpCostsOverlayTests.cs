// ─────────────────────────────────────────────────────────────────────────────
// progress_server_side §2 / §4 — the `level_up_costs` overlay on the cost table
// BOTH level-up modals read.
//
// WHY THIS ONE MATTERS MORE THAN THE OTHER SEVEN OVERLAYS. Since §3 the SERVER
// prices a level-up from the same catalog. So the overlay is not a display
// nicety: it is what keeps the number the modal previews and the number
// `golfin_level_up()` charges from being two different numbers. When they do
// differ the server answers `cost_changed` and the modal re-prices — correct,
// but a round trip the overlay exists to avoid.
//
// Driven through the REAL CharacterLevelUpDatabase (Assembly-CSharp, reached by
// reflection the way GachaStage1Tests reaches TicketCatalog), never a private
// copy of the merge rule.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Golfin.Content;
using NUnit.Framework;
using UnityEngine;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class LevelUpCostsOverlayTests
    {
        private static readonly Type? _dbType =
            Type.GetType("Golfin.Roster.CharacterLevelUpDatabase, Assembly-CSharp");

        /// <summary>Three levels, so an overlay can patch the middle one and leave neighbours alone.</summary>
        private const string Csv = "level,cost_r,sp_reward\n1,10,1\n2,20,1\n3,30,2\n";

        private GameObject? _host;

        [TearDown]
        public void TearDown()
        {
            ContentCatalogStore.Clear();
            if (_host != null)
            {
                UnityEngine.Object.DestroyImmediate(_host);
                _host = null;
            }
        }

        // ── Harness ───────────────────────────────────────────────────────────

        private object NewDatabase()
        {
            Assert.IsNotNull(_dbType, "CharacterLevelUpDatabase not found in Assembly-CSharp");
            _host = new GameObject("CharacterLevelUpDatabase (test)");
            // AddComponent, not Awake: Awake would claim the Instance singleton and read the
            // Inspector-assigned TextAsset, neither of which exists here. LoadFromCSV is the seam
            // both the shipping Awake and Reload() go through.
            return _host.AddComponent(_dbType!);
        }

        private static void Load(object db, string csv)
            => _dbType!.GetMethod("LoadFromCSV", BindingFlags.Public | BindingFlags.Instance)!
                       .Invoke(db, new object[] { csv });

        private static int Cost(object db, int level)
            => (int)_dbType!.GetMethod("GetLevelUpCost", BindingFlags.Public | BindingFlags.Instance)!
                            .Invoke(db, new object[] { level })!;

        private static int Reward(object db, int level)
            => (int)_dbType!.GetMethod("GetSPReward", BindingFlags.Public | BindingFlags.Instance)!
                            .Invoke(db, new object[] { level })!;

        private static int MaxLevel(object db)
            => (int)_dbType!.GetMethod("GetMaxLevel", BindingFlags.Public | BindingFlags.Instance)!
                            .Invoke(db, Array.Empty<object>())!;

        /// <summary>Install a `level_up_costs` overlay and mark the store ready, which is what
        /// <c>RequireReady</c> gates on.</summary>
        private static void InstallOverlay(params (string level, bool active, Dictionary<string, string?> data)[] rows)
        {
            var contentRows = new List<ContentRow>();
            foreach (var (level, active, data) in rows)
                contentRows.Add(new ContentRow(level, active, 0, data));

            ContentCatalogStore.ConfigureForTest(
                new ContentCatalog(ContentCatalogs.LevelUpCosts, 7, false, contentRows));
        }

        private static Dictionary<string, string?> Data(params (string k, string? v)[] pairs)
        {
            var d = new Dictionary<string, string?>();
            foreach (var (k, v) in pairs) d[k] = v;
            return d;
        }

        // ── The floor: no overlay is bundled-only ─────────────────────────────

        [Test]
        public void NoOverlay_ReadsTheBundledCostsExactly()
        {
            object db = NewDatabase();
            Load(db, Csv);

            Assert.AreEqual(10, Cost(db, 1));
            Assert.AreEqual(20, Cost(db, 2));
            Assert.AreEqual(30, Cost(db, 3));
            Assert.AreEqual(3, MaxLevel(db));
        }

        // ── Patch ─────────────────────────────────────────────────────────────

        [Test]
        public void APublishedCost_IsWhatGetLevelUpCostReturns()
        {
            // The behaviour the whole overlay exists for: an admin retunes level 2 and the modal
            // previews the retuned price, which is the one the server will charge.
            InstallOverlay(("2", true, Data(("cost_r", "77"))));

            object db = NewDatabase();
            Load(db, Csv);

            Assert.AreEqual(77, Cost(db, 2), "the published column wins");
            Assert.AreEqual(10, Cost(db, 1), "a level the overlay did not name keeps its bundled cost");
            Assert.AreEqual(30, Cost(db, 3));
        }

        [Test]
        public void APublishedColumn_OverridesOnlyThatColumn()
        {
            // A sparse patch. Getting this backwards is how an operator editing cost_r silently
            // zeroes the SP reward for that level.
            InstallOverlay(("3", true, Data(("cost_r", "99"))));

            object db = NewDatabase();
            Load(db, Csv);

            Assert.AreEqual(99, Cost(db, 3));
            Assert.AreEqual(2, Reward(db, 3), "sp_reward was not named, so the bundled value stands");
        }

        // ── Append ────────────────────────────────────────────────────────────

        [Test]
        public void AnOverlayOnlyLevel_IsAdmitted()
        {
            // How a raised maxLevel becomes buyable without a build: the ref's maxLevel and the cost
            // rows above the bundled ceiling are published together and both land on the next launch.
            InstallOverlay(("4", true, Data(("cost_r", "40"), ("sp_reward", "3"))));

            object db = NewDatabase();
            Load(db, Csv);

            Assert.AreEqual(40, Cost(db, 4));
            Assert.AreEqual(3, Reward(db, 4));
            Assert.AreEqual(4, MaxLevel(db), "the appended level raises the client's ceiling too");
        }

        // ── Deactivation (I6) ─────────────────────────────────────────────────

        [Test]
        public void ADeactivatedLevel_IsDroppedBecauseTheServerWillNotSellIt()
        {
            // golfin_level_up() joins on is_active, so a deactivated cost row is a level the server
            // answers `costs_missing` for. Dropping it here is what stops the modal offering a level
            // the server would then refuse.
            InstallOverlay(("2", false, Data(("cost_r", "20"))));

            object db = NewDatabase();
            Load(db, Csv);

            Assert.AreEqual(0, Cost(db, 2),
                "a deactivated level has no cost — GetLevelUpCost's absent-row answer");
            Assert.AreEqual(10, Cost(db, 1), "its neighbours are untouched");
            Assert.AreEqual(30, Cost(db, 3));
        }

        // ── Reload ────────────────────────────────────────────────────────────

        [Test]
        public void Reload_IsTheSeamTheModalsUseAfterACostChangedRefusal()
        {
            // The modals call Reload() on cost_changed. It must exist, be public, and re-run the
            // same merge — the run TOTAL then comes from the server's answer (the overlay is a
            // next-launch effect, so a cost published seconds ago is not in it yet).
            MethodInfo? reload = _dbType!.GetMethod("Reload", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(reload,
                "CharacterLevelUpDatabase.Reload() is the seam both level-up modals call after a " +
                "cost_changed refusal. Removing it silently breaks the re-price path.");

            InstallOverlay(("2", true, Data(("cost_r", "55"))));
            object db = NewDatabase();
            Load(db, Csv);
            Assert.AreEqual(55, Cost(db, 2));

            // Re-running the merge is idempotent — no doubled rows, no lost patch.
            Load(db, Csv);
            Assert.AreEqual(55, Cost(db, 2));
            Assert.AreEqual(3, MaxLevel(db));
        }

        // ── The catalog is requested at all ───────────────────────────────────

        [Test]
        public void TheCatalogIsOneTheClientAsksTheServerFor()
        {
            // An unknown catalog name is IGNORED server-side (200, absent from the response), so a
            // catalog the client never asks for fails completely silently: the modal would price from
            // the bundled CSV forever and every level-up would answer cost_changed.
            CollectionAssert.Contains(ContentCatalogs.All, ContentCatalogs.LevelUpCosts);
            CollectionAssert.Contains(ContentCatalogs.Data, ContentCatalogs.LevelUpCosts);
            StringAssert.Contains("level_up_costs", ContentCatalogs.RequestList);
        }
    }
}
