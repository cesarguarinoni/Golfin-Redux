// ─────────────────────────────────────────────────────────────────────────────
// starter_restore_gate — the boot OUTCOME and the restore signal.
//
// Acceptance covered (SPEC §1, §4):
//   * LastBootOutcome distinguishes "has not answered" from "could not be reached"
//   * OnBootFinished fires exactly once per boot, after the grants are drained
//   * OnRestored fires when — and only when — a merge actually changed the save
//   * a FAILED boot is re-runnable and the retry re-opens the question (NotRun while in flight)
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using Golfin.Save;
using NUnit.Framework;

namespace Golfin.InventorySync.Tests
{
    public class InventoryBootOutcomeTests
    {
        private FakeTransport _server;
        private SaveData _save;
        private InventorySyncService _sync;

        [SetUp]
        public void SetUp()
        {
            _server = new FakeTransport();
            _save = SaveData.CreateFresh();
            _save.unlockedHoles.Clear();

            _sync = new InventorySyncService
            {
                Transport = _server,
                Catalog = EmptyInventoryCatalog.Instance,
                IsAuthenticated = () => true,
                SaveProvider = () => _save,
                MarkSaveDirty = () => { },
            };
            InventorySyncService.ConfigureForTest(_sync);
        }

        [TearDown]
        public void TearDown() => InventorySyncService.ResetForTest();

        /// <summary>A blob carrying nothing but a starter — the reinstall case, minimally.</summary>
        private static string StarterBlob(string characterId) =>
            "{\"v\":1,\"characters\":[{\"id\":\"" + characterId + "\",\"lv\":10,\"own\":true}]," +
            "\"starter\":\"" + characterId + "\"}";

        // ── LastBootOutcome ──────────────────────────────────────────────────

        [Test]
        public void Before_any_boot_the_outcome_is_NotRun()
        {
            Assert.AreEqual(BootOutcome.NotRun, _sync.LastBootOutcome);
            Assert.IsFalse(_sync.BootInFlight);
        }

        [Test]
        public void A_successful_boot_reports_Succeeded()
        {
            _server.ServerJson = StarterBlob("char_ken");
            _server.ServerRev = 4;

            _sync.Boot();

            Assert.AreEqual(BootOutcome.Succeeded, _sync.LastBootOutcome);
            Assert.IsTrue(_sync.BootCompleted);
            Assert.IsFalse(_sync.BootInFlight);
            Assert.AreEqual("char_ken", _save.starterCharacterId, "the restore is what the gate is waiting for");
        }

        [Test]
        public void A_failed_boot_reports_Failed_and_leaves_the_save_alone()
        {
            _server.Offline = true;

            _sync.Boot();

            Assert.AreEqual(BootOutcome.Failed, _sync.LastBootOutcome);
            Assert.IsTrue(_sync.BootCompleted, "pushes must still flow after a failed fetch");
            Assert.IsEmpty(_save.starterCharacterId ?? "");
        }

        [Test]
        public void An_ok_fetch_with_no_blob_is_a_real_answer_Succeeded()
        {
            _server.ServerJson = null;   // never-synced account

            _sync.Boot();

            Assert.AreEqual(BootOutcome.Succeeded, _sync.LastBootOutcome,
                "'the account owns nothing' is an ANSWER — it is the only case that may show the picker");
            Assert.IsEmpty(_save.starterCharacterId ?? "");
        }

        [Test]
        public void An_unauthenticated_boot_leaves_the_outcome_NotRun()
        {
            _sync.IsAuthenticated = () => false;

            _sync.Boot();

            Assert.AreEqual(BootOutcome.NotRun, _sync.LastBootOutcome,
                "not signed in is not an answer — a later sign-in must still be able to run the real boot");
            Assert.IsFalse(_sync.BootCompleted);
        }

        [Test]
        public void Reset_forgets_the_outcome()
        {
            _sync.Boot();
            Assert.AreEqual(BootOutcome.Succeeded, _sync.LastBootOutcome);

            _sync.Reset();

            Assert.AreEqual(BootOutcome.NotRun, _sync.LastBootOutcome);
            Assert.IsFalse(_sync.BootInFlight);
        }

        [Test]
        public void A_retry_reopens_the_question_before_the_new_answer_lands()
        {
            _server.Offline = true;
            _sync.Boot();
            Assert.AreEqual(BootOutcome.Failed, _sync.LastBootOutcome);

            // The gate reads LastBootOutcome the instant a caller re-resolves, so a retry that left
            // it at Failed would show the offline error forever. Observed from inside the request.
            var seenDuringFlight = new List<BootOutcome>();
            _server.OnGetInventory = () => seenDuringFlight.Add(_sync.LastBootOutcome);
            _server.Offline = false;
            _server.ServerJson = StarterBlob("char_ken");

            _sync.Boot();

            CollectionAssert.AreEqual(new[] { BootOutcome.NotRun }, seenDuringFlight);
            Assert.AreEqual(BootOutcome.Succeeded, _sync.LastBootOutcome);
        }

        // ── OnBootFinished ───────────────────────────────────────────────────

        [Test]
        public void OnBootFinished_fires_once_with_the_outcome_after_grants_drained()
        {
            var seen = new List<BootOutcome>();
            int grantGetsWhenRaised = -1;
            _sync.OnBootFinished += o => { seen.Add(o); grantGetsWhenRaised = _server.GrantGetCount; };

            _sync.Boot();

            CollectionAssert.AreEqual(new[] { BootOutcome.Succeeded }, seen);
            Assert.AreEqual(1, grantGetsWhenRaised, "raised from the DrainGrants done, not before it");
        }

        [Test]
        public void OnBootFinished_fires_on_failure_too()
        {
            _server.Offline = true;
            var seen = new List<BootOutcome>();
            _sync.OnBootFinished += seen.Add;

            _sync.Boot();

            CollectionAssert.AreEqual(new[] { BootOutcome.Failed }, seen);
        }

        [Test]
        public void OnBootFinished_does_not_fire_when_the_boot_never_ran()
        {
            _sync.IsAuthenticated = () => false;
            int calls = 0;
            _sync.OnBootFinished += _ => calls++;

            _sync.Boot();

            Assert.AreEqual(0, calls);
        }

        // ── OnRestored ───────────────────────────────────────────────────────

        [Test]
        public void OnRestored_fires_when_the_boot_merge_changed_the_save()
        {
            _server.ServerJson = StarterBlob("char_ken");
            int restored = 0;
            string starterWhenRaised = null;
            _sync.OnRestored += () => { restored++; starterWhenRaised = _save.starterCharacterId; };

            _sync.Boot();

            Assert.AreEqual(1, restored);
            Assert.AreEqual("char_ken", starterWhenRaised,
                "the managers re-read the save inside this callback, so it must already be merged");
        }

        [Test]
        public void OnRestored_fires_before_OnBootFinished()
        {
            _server.ServerJson = StarterBlob("char_ken");
            var order = new List<string>();
            _sync.OnRestored += () => order.Add("restored");
            _sync.OnBootFinished += _ => order.Add("finished");

            _sync.Boot();

            CollectionAssert.AreEqual(new[] { "restored", "finished" }, order,
                "the roster must be re-hydrated before the gate routes anyone to Home");
        }

        [Test]
        public void OnRestored_does_not_fire_when_the_merge_added_nothing()
        {
            _save.starterCharacterId = "char_ken";
            _save.ownedCharacters.Add(new PersistedCharacter
            { characterId = "char_ken", currentLevel = 10, isOwned = true });
            _server.ServerJson = StarterBlob("char_ken");

            int restored = 0;
            _sync.OnRestored += () => restored++;

            _sync.Boot();

            Assert.AreEqual(0, restored, "already in sync — nothing to re-read");
        }

        [Test]
        public void OnRestored_does_not_fire_on_a_failed_fetch()
        {
            _server.Offline = true;
            int restored = 0;
            _sync.OnRestored += () => restored++;

            _sync.Boot();

            Assert.AreEqual(0, restored);
        }

        [Test]
        public void OnRestored_fires_on_a_stale_merge_from_another_device()
        {
            _sync.Boot();                      // rev 0, empty server
            Assert.AreEqual(BootOutcome.Succeeded, _sync.LastBootOutcome);

            // Another device wrote while we were away: the server is at a rev we do not hold, and
            // its blob carries a character we do not own.
            _server.ServerJson = StarterBlob("char_ken");
            _server.ServerRev = 9;

            int restored = 0;
            _sync.OnRestored += () => restored++;

            _sync.MarkDirty();
            _sync.FlushNow(100f);

            Assert.AreEqual(1, restored, "a level another device raised must show on this device too");
            Assert.AreEqual("char_ken", _save.starterCharacterId);
        }

    }
}
