// ─────────────────────────────────────────────────────────────────────────────
// game_modes_admin §2 — the `modes` overlay, and the WITHHOLD RULE that makes
// appending a mode safe.
//
// Two properties are being pinned, and only the first is standard:
//
//   1. The usual overlay treatment — patch by id, sparse columns, appended rows
//      admitted, is_active=false drops the card, RequireReady gates the read.
//
//   2. THE WITHHOLD RULE. A published mode whose `target` this BUILD does not
//      dispatch must never become a card, because its PLAY button would do
//      nothing. That is what lets an operator publish a new mode at all: the
//      worst case on an old build is "the card is not there yet", never "the
//      card is there and broken". `locked=true` is NOT this — a locked mode
//      still renders, as Coming Soon, which is what makes flipping Missions
//      live a publish rather than a build.
//
// Driven through the REAL ModesDatabaseCSV (Assembly-CSharp, reached by
// reflection the way LevelUpCostsOverlayTests reaches CharacterLevelUpDatabase),
// against the REAL bundled Assets/Resources/Data/modes.csv — so a change to that
// CSV's columns shows up here rather than in a private copy of the merge rule.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Golfin.Content;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class ModesOverlayTests
    {
        private static readonly Type? _dbType =
            Type.GetType("GolfinRedux.UI.ModeSelect.ModesDatabaseCSV, Assembly-CSharp");

        private static readonly Type? _modeType =
            Type.GetType("GolfinRedux.UI.ModeSelect.ModeData, Assembly-CSharp");

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

        /// <summary>
        /// A database that has parsed the REAL bundled CSV.
        ///
        /// AddComponent, not Awake: in EditMode Awake does not run for a plain MonoBehaviour, which
        /// is what keeps this from claiming the Instance singleton and calling DontDestroyOnLoad.
        /// LoadFromCSV is the seam Awake itself goes through.
        /// </summary>
        private IList<object> Load()
        {
            Assert.IsNotNull(_dbType, "ModesDatabaseCSV not found in Assembly-CSharp");
            _host = new GameObject("ModesDatabaseCSV (test)");
            object db = _host.AddComponent(_dbType!);

            _dbType!.GetMethod("LoadFromCSV", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(db, Array.Empty<object>());

            var raw = (IEnumerable)_dbType.GetMethod("GetAllModes", BindingFlags.Public | BindingFlags.Instance)!
                                          .Invoke(db, Array.Empty<object>())!;
            var list = new List<object>();
            foreach (object m in raw) list.Add(m);
            return list;
        }

        private static object? Find(IList<object> modes, string id)
        {
            foreach (object m in modes)
                if ((string)_modeType!.GetField("id")!.GetValue(m)! == id) return m;
            return null;
        }

        private static T Field<T>(object mode, string name)
            => (T)_modeType!.GetField(name)!.GetValue(mode)!;

        private static void InstallOverlay(params (string id, bool active, Dictionary<string, string?> data)[] rows)
        {
            var contentRows = new List<ContentRow>();
            foreach (var (id, active, data) in rows)
                contentRows.Add(new ContentRow(id, active, 0, data));

            ContentCatalogStore.ConfigureForTest(
                new ContentCatalog(ContentCatalogs.Modes, 4, false, contentRows));
        }

        private static Dictionary<string, string?> Data(params (string k, string? v)[] pairs)
        {
            var d = new Dictionary<string, string?>();
            foreach (var (k, v) in pairs) d[k] = v;
            return d;
        }

        // ── The floor: no overlay is bundled-only ─────────────────────────────

        [Test]
        public void NoOverlay_ReadsTheBundledModesExactly()
        {
            IList<object> modes = Load();

            Assert.AreEqual(5, modes.Count, "modes.csv ships five modes");

            object practice = Find(modes, "practice")!;
            Assert.IsNotNull(practice);
            Assert.AreEqual(10, Field<int>(practice, "entryFee"));
            Assert.AreEqual("hole_select", Field<string>(practice, "target"));
            Assert.IsFalse(Field<bool>(practice, "locked"));

            // Missions shipped locked until 2026-08-29, when the mode opened (bundled floor
            // locked=false, published as `modes` v8). Asserting the ROUTE as well as the flag:
            // an unlocked card pointing at a target no build can reach is the failure mode this
            // suite caught once already, when Phase A set target=none.
            object missions = Find(modes, "missions")!;
            Assert.IsFalse(Field<bool>(missions, "locked"), "Missions ships unlocked");
            Assert.AreEqual("mission_select", Field<string>(missions, "target"),
                "an unlocked Missions card must route at the screen that exists");

            object versus = Find(modes, "versus_1v1")!;
            Assert.AreEqual(5, Field<int>(versus, "versusStrokeCapOverPar"));
            Assert.AreEqual(1, Field<IList>(versus, "rewardList").Count,
                "the reward-pair columns must still parse through the overlay-aware path");

            object tournaments = Find(modes, "tournaments")!;
            Assert.AreEqual("MODE_REWARDS_VARY", Field<string>(tournaments, "rewardsTextKey"));

            // Order is the carousel's sort key and it is applied after the merge.
            Assert.AreEqual("versus_1v1", Field<string>(modes[0], "id"));
        }

        [Test]
        public void ADescriptionWithCommasSurvivesTheQuotedCsvSplit()
        {
            // Three of the five descriptions are quoted because they contain commas. The merge path
            // reads bundled columns through the same header index, so a broken split would show up
            // as a truncated description rather than a parse error.
            object versus = Find(Load(), "versus_1v1")!;
            StringAssert.Contains("outplay your opponent", Field<string>(versus, "description"));
        }

        // ── Patch ─────────────────────────────────────────────────────────────

        [Test]
        public void APublishedFee_IsWhatTheCardWillCharge()
        {
            // The behaviour the overlay exists for, and the one with money attached: an admin moves
            // practice 10 → 15, and this build shows and pays 15. A build that did NOT get this
            // would show 10, be answered `fee_changed`, and re-price on the second tap.
            InstallOverlay(("practice", true, Data(("entryFee", "15"))));

            object practice = Find(Load(), "practice")!;

            Assert.AreEqual(15, Field<int>(practice, "entryFee"));
            Assert.AreEqual("hole_select", Field<string>(practice, "target"),
                "an unnamed column keeps its bundled value");
            Assert.AreEqual("PRACTICE", Field<string>(practice, "title"));
        }

        [Test]
        public void FlippingLockedOff_MakesAComingSoonModePlayableWithNoBuild()
        {
            // The SPEC's own example: Missions goes live as a PUBLISH.
            InstallOverlay(("missions", true, Data(("locked", "false"), ("target", "hole_select"))));

            object missions = Find(Load(), "missions")!;

            Assert.IsFalse(Field<bool>(missions, "locked"));
        }

        [Test]
        public void ADeactivatedMode_IsDroppedEntirely()
        {
            // I6 — deactivate is the delete. This is NOT the same as locked: locked renders a
            // Coming Soon card, deactivated renders nothing at all.
            InstallOverlay(("driving_range", false, Data()));

            IList<object> modes = Load();

            Assert.AreEqual(4, modes.Count);
            Assert.IsNull(Find(modes, "driving_range"));
        }

        // ── Append + the withhold rule ────────────────────────────────────────

        [Test]
        public void AnAppendedModeWithARoutableTarget_BecomesACard()
        {
            InstallOverlay(("weekly_challenge", true, Data(
                ("id", "weekly_challenge"), ("title", "WEEKLY"), ("entryFee", "25"),
                ("target", "hole_select"), ("order", "6"), ("locked", "false"))));

            IList<object> modes = Load();
            object appended = Find(modes, "weekly_challenge")!;

            Assert.AreEqual(6, modes.Count);
            Assert.IsNotNull(appended);
            Assert.AreEqual(25, Field<int>(appended, "entryFee"));
            Assert.AreEqual("WEEKLY", Field<string>(appended, "title"));
        }

        [Test]
        public void AnAppendedModeWithAnUnroutableTarget_IsWITHHELD()
        {
            // THE INVARIANT. This build's dispatch has no `battle_royale` case, so a card for it
            // would render a PLAY button that logs a warning and does nothing. Withholding is the
            // only answer that keeps "a client missing information never shows a broken item" true.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("WITHHELD"));

            InstallOverlay(("battle_royale", true, Data(
                ("id", "battle_royale"), ("title", "BATTLE ROYALE"), ("entryFee", "50"),
                ("target", "battle_royale"), ("order", "7"), ("locked", "false"))));

            IList<object> modes = Load();

            Assert.AreEqual(5, modes.Count, "the unroutable mode must not become a card");
            Assert.IsNull(Find(modes, "battle_royale"));
        }

        [Test]
        public void PatchingAnEXISTINGModeToAnUnroutableTarget_AlsoWithholdsIt()
        {
            // The rule is about the RESULT, not about where the row came from. Re-pointing practice
            // at a target this build cannot route breaks its PLAY button exactly as much as
            // appending a new one would.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("WITHHELD"));

            InstallOverlay(("practice", true, Data(("target", "vr_range"))));

            IList<object> modes = Load();

            Assert.AreEqual(4, modes.Count);
            Assert.IsNull(Find(modes, "practice"));
        }

        [Test]
        public void TargetNone_IsRoutableAndStillRenders()
        {
            // `none` is not a missing target — it is the explicit "deliberately not enterable", and
            // it is what the two shipped Coming Soon cards carry. Withholding it would make Driving
            // Range and Missions vanish, which is emphatically not what the rule means.
            IList<object> modes = Load();

            object driving = Find(modes, "driving_range")!;
            Assert.IsNotNull(driving);
            Assert.AreEqual("none", Field<string>(driving, "target"));
            Assert.IsTrue(Field<bool>(driving, "locked"));
        }

        [Test]
        public void AnAppendedModeWithNoTargetAtAll_IsWithheld()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("WITHHELD"));

            InstallOverlay(("ghost", true, Data(("id", "ghost"), ("title", "GHOST"), ("order", "8"))));

            Assert.IsNull(Find(Load(), "ghost"));
        }

        // ── RequireReady ──────────────────────────────────────────────────────

        [Test]
        public void AStoreThatIsNotReady_IsNotRead()
        {
            // A database that parses before ContentService has installed the caches must read
            // BUNDLED, not a half-populated store. Declare() without MarkReady() is that window.
            ContentCatalogStore.Declare();
            ContentCatalogStore.Install(new ContentCatalog(
                ContentCatalogs.Modes, 4, false,
                new List<ContentRow> { new ContentRow("practice", true, 0, Data(("entryFee", "999"))) }));

            LogAssert.ignoreFailingMessages = true;   // RequireReady logs its own complaint
            object practice = Find(Load(), "practice")!;
            LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual(10, Field<int>(practice, "entryFee"),
                "reading a store that is not ready would silently apply a partial overlay");
        }
    }
}
