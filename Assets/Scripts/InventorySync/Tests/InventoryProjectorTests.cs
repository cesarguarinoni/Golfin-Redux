// ─────────────────────────────────────────────────────────────────────────────
// content_player_inventory — projection and the additive apply.
//
// Acceptance covered:
//   * "RP, leaderboard accumulators and tournament entries are NOT in the blob"
//   * "a fresh install with no local save restores from it"
//   * the merge never subtracts
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using Golfin.InventorySync;
using Golfin.Save;
using NUnit.Framework;

namespace Golfin.InventorySync.Tests
{
    public class InventoryProjectorTests
    {
        private static SaveData Populated()
        {
            var save = SaveData.CreateFresh();
            save.rewardPoints      = 12345;
            save.lifetimeRpEarned  = 999;
            save.rpDaily = save.rpWeekly = save.rpMonthly = 50;
            save.dailyPeriodKey    = 20000;
            save.tournamentEntries.Add(new PersistedTournamentEntry { tournamentId = "kasumigaseki_open" });
            save.playedHoles.AddRange(new[] { 1, 2, 3 });

            save.ownedClubs.Add(new PersistedClub
            { clubId = "club_a", currentLevel = 10, currentDurability = 40, maxDurability = 40, equippedBagSlot = 1 });
            save.ownedCharacters.Add(new PersistedCharacter
            { characterId = "char_ken", currentLevel = 10, isOwned = true, conditionEnergy = 42f,
              conditionUpdatedUtc = "2026-08-26T00:00:00Z" });
            save.itemQuantities["item_repair_kit"] = 3;
            save.ballQuantities["ball_standard"] = -1;
            save.ticketBalances.Add(new PersistedTicketBalance { ticketTypeInt = 0, balance = 10 });
            save.unlockedHoles.Clear();
            save.unlockedHoles.AddRange(new[] { 1, 2 });
            save.starterCharacterId  = "char_ken";
            save.selectedCharacterId = "char_ken";
            return save;
        }

        // ── What must not be in the blob ─────────────────────────────────────

        [Test]
        public void Server_owned_and_device_local_fields_never_reach_the_wire()
        {
            string json = InventoryCodec.Encode(
                InventoryProjector.Project(Populated()), EmptyInventoryCatalog.Instance);

            foreach (string banned in new[]
            {
                "rewardPoints", "12345",           // RP balance
                "lifetimeRpEarned", "rpDaily", "rpWeekly", "rpMonthly",
                "dailyPeriodKey", "weeklyPeriodKey", "monthlyPeriodKey",
                "tournamentEntries", "kasumigaseki_open",
                "playedHoles",                      // device-local history
                "conditionEnergy", "conditionUpdatedUtc",  // a regenerating pool; see InventorySnapshot
            })
                StringAssert.DoesNotContain(banned, json, $"'{banned}' must never be in the blob");
        }

        [Test]
        public void Projection_carries_exactly_what_moves()
        {
            var snap = InventoryProjector.Project(Populated());

            Assert.AreEqual(1, snap.Clubs.Count);
            Assert.AreEqual(1, snap.Characters.Count);
            Assert.AreEqual(3, snap.Items["item_repair_kit"]);
            Assert.AreEqual(-1, snap.Balls["ball_standard"]);
            Assert.AreEqual(10, snap.Tickets[0]);
            CollectionAssert.AreEqual(new[] { 1, 2 }, snap.UnlockedHoles);
            Assert.AreEqual("char_ken", snap.StarterCharacterId);
            Assert.AreEqual("char_ken", snap.SelectedCharacterId);
            Assert.AreEqual(0f, snap.Characters[0].conditionEnergy);
        }

        // ── Restore ──────────────────────────────────────────────────────────

        [Test]
        public void A_fresh_install_with_no_local_save_restores_the_whole_blob()
        {
            var snap = InventoryProjector.Project(Populated());

            var fresh = SaveData.CreateFresh();
            fresh.unlockedHoles.Clear();
            Assert.IsTrue(InventoryProjector.Apply(snap, fresh));

            Assert.AreEqual(1, fresh.ownedClubs.Count);
            Assert.AreEqual("club_a", fresh.ownedClubs[0].clubId);
            Assert.AreEqual(1, fresh.ownedClubs[0].equippedBagSlot, "an arriving club keeps the slot it arrived with");
            Assert.AreEqual(1, fresh.ownedCharacters.Count);
            Assert.IsTrue(fresh.ownedCharacters[0].isOwned);
            Assert.AreEqual(3, fresh.itemQuantities["item_repair_kit"]);
            Assert.AreEqual(-1, fresh.ballQuantities["ball_standard"]);
            Assert.AreEqual(10, fresh.ticketBalances[0].balance);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, fresh.unlockedHoles);
            Assert.AreEqual("char_ken", fresh.starterCharacterId);
            Assert.AreEqual("char_ken", fresh.selectedCharacterId);

            // RP is server-owned and was never in the blob, so a restore must not invent one.
            Assert.AreEqual(0, fresh.rewardPoints);
            Assert.AreEqual(0, fresh.tournamentEntries.Count);
        }

        [Test]
        public void Applying_the_same_snapshot_twice_reports_no_change_the_second_time()
        {
            // A no-op restore must not dirty the save, or every boot writes to disk for nothing.
            var snap = InventoryProjector.Project(Populated());
            var save = SaveData.CreateFresh();
            save.unlockedHoles.Clear();

            Assert.IsTrue(InventoryProjector.Apply(snap, save));
            Assert.IsFalse(InventoryProjector.Apply(snap, save));
        }

        // ── Never subtracts ──────────────────────────────────────────────────

        [Test]
        public void Apply_never_removes_a_club_a_character_or_a_hole()
        {
            var save = Populated();
            var poorer = new InventorySnapshot();          // the server knows about nothing
            Assert.IsFalse(InventoryProjector.Apply(poorer, save));

            Assert.AreEqual(1, save.ownedClubs.Count);
            Assert.AreEqual(1, save.ownedCharacters.Count);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, save.unlockedHoles);
            Assert.AreEqual(3, save.itemQuantities["item_repair_kit"]);
        }

        [Test]
        public void Apply_never_lowers_a_level_or_a_quantity()
        {
            var save = Populated();
            save.ownedClubs[0].currentLevel = 30;
            save.itemQuantities["item_repair_kit"] = 9;

            var lower = new InventorySnapshot();
            lower.Clubs.Add(new PersistedClub { clubId = "club_a", currentLevel = 10 });
            lower.Items["item_repair_kit"] = 1;

            Assert.IsFalse(InventoryProjector.Apply(lower, save));
            Assert.AreEqual(30, save.ownedClubs[0].currentLevel);
            Assert.AreEqual(9,  save.itemQuantities["item_repair_kit"]);
        }

        [Test]
        public void Apply_never_re_locks_a_character()
        {
            var save = Populated();
            var theirs = new InventorySnapshot();
            theirs.Characters.Add(new PersistedCharacter
            { characterId = "char_ken", currentLevel = 10, isOwned = false });

            InventoryProjector.Apply(theirs, save);
            Assert.IsTrue(save.ownedCharacters[0].isOwned);
        }

        [Test]
        public void Apply_raises_a_level_the_other_device_earned()
        {
            var save = Populated();
            var theirs = new InventorySnapshot();
            theirs.Clubs.Add(new PersistedClub
            { clubId = "club_a", currentLevel = 44, spentPower = 5, currentDurability = 40, maxDurability = 40 });

            Assert.IsTrue(InventoryProjector.Apply(theirs, save));
            Assert.AreEqual(44, save.ownedClubs[0].currentLevel);
            Assert.AreEqual(5,  save.ownedClubs[0].spentPower);
        }

        [Test]
        public void Apply_never_overwrites_the_starter_or_a_live_selection()
        {
            var save = Populated();
            var theirs = new InventorySnapshot
            { StarterCharacterId = "char_other", SelectedCharacterId = "char_other" };

            InventoryProjector.Apply(theirs, save);
            Assert.AreEqual("char_ken", save.starterCharacterId);
            Assert.AreEqual("char_ken", save.selectedCharacterId);
        }

        [Test]
        public void An_unlimited_ball_is_not_downgraded_to_a_finite_stack()
        {
            var save = Populated();                      // ball_standard = -1 (unlimited)
            var theirs = new InventorySnapshot();
            theirs.Balls["ball_standard"] = 5;

            Assert.IsFalse(InventoryProjector.Apply(theirs, save));
            Assert.AreEqual(-1, save.ballQuantities["ball_standard"]);
        }

        [Test]
        public void A_null_save_or_snapshot_is_a_no_op_rather_than_an_exception()
        {
            Assert.IsFalse(InventoryProjector.Apply(null, SaveData.CreateFresh()));
            Assert.IsFalse(InventoryProjector.Apply(new InventorySnapshot(), null));
            Assert.AreEqual(0, InventoryProjector.Project(null).Clubs.Count);
        }
    }
}
