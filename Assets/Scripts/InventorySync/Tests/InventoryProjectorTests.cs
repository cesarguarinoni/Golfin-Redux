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
            // TICKETS ARE NO LONGER PROJECTED (gacha_client_real_pull §4.4): `golfin_tickets` is
            // the ledger, and uploading a balance the client does not own is how the additive
            // max-merge resurrects a pre-spend number. RP was never in the blob for the same
            // reason; tickets joined it.
            Assert.AreEqual(0, snap.Tickets.Count,
                "tickets are server-owned — the blob must not carry a balance the ledger owns");
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
            // A restore brings back everything the blob carries, and the blob no longer carries
            // tickets (§4.4). The counter is re-read from /gacha/tickets at boot instead.
            Assert.AreEqual(0, fresh.ticketBalances.Count,
                "a restore must not invent a ticket balance — the ledger is the truth");
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

        // ── The refundable spend, counted (PLAN §6.5 decision 1) ─────────────

        [Test]
        public void A_raised_quantity_on_a_key_we_already_held_is_reported()
        {
            // THE REFUND, exactly as §6.5 describes it: this device spent a repair kit (3 -> 2) and
            // the other device's blob still says 3. max(2,3) hands it back. RP stays debited, so it
            // is a free consumable — accepted for the beta, but it MUST be counted, because beta
            // consumption figures are what tune the economy.
            var save = Populated();
            save.itemQuantities["item_repair_kit"] = 2;

            var theirs = new InventorySnapshot();
            theirs.Items["item_repair_kit"] = 3;

            var raises = new List<InventoryRaise>();
            Assert.IsTrue(InventoryProjector.Apply(theirs, save, raises));

            Assert.AreEqual(1, raises.Count, "the refund path must produce exactly one row");
            Assert.AreEqual(InventoryRaiseKind.Item, raises[0].Kind);
            Assert.AreEqual("item_repair_kit", raises[0].Id, "the ITEM is half of what §6.5 asks for");
            Assert.AreEqual(2, raises[0].From);
            Assert.AreEqual(3, raises[0].To);
        }

        [Test]
        public void A_brand_new_key_is_a_restore_and_is_NOT_counted_as_a_raise()
        {
            // A fresh install pulling its inventory back is the feature working. Counting it would
            // bury the refund signal under every reinstall — see InventoryRaise.
            var save = SaveData.CreateFresh();
            var theirs = new InventorySnapshot();
            theirs.Items["item_repair_kit"] = 3;
            theirs.Balls["ball_pro"] = 7;

            var raises = new List<InventoryRaise>();
            Assert.IsTrue(InventoryProjector.Apply(theirs, save, raises));

            Assert.AreEqual(3, save.itemQuantities["item_repair_kit"], "the restore still happens");
            Assert.AreEqual(0, raises.Count, "a key we never held is a restore, not a refund");
        }

        [Test]
        public void Balls_are_counted_too_and_an_incoming_ticket_balance_is_ignored()
        {
            var save = Populated();                       // ticket 0 = 10, ball_standard = -1
            save.ballQuantities["ball_pro"] = 1;

            var theirs = new InventorySnapshot();
            theirs.Balls["ball_pro"]   = 4;               // raised   -> counted
            theirs.Balls["ball_standard"] = 5;            // unlimited stays unlimited -> not counted
            theirs.Tickets[0]          = 12;              // IGNORED  -> see below
            theirs.Items["item_repair_kit"] = 1;          // lower than ours -> not counted

            var raises = new List<InventoryRaise>();
            Assert.IsTrue(InventoryProjector.Apply(theirs, save, raises));

            // gacha_client_real_pull §4.4 — the incoming ticket balance is a number THIS client
            // (or an older build of it) uploaded, never the ledger. Folding it in with the
            // max-merge is exactly how a spent ticket comes back: pull takes the ledger 500 → 450,
            // a stale blob still says 500, and the merge would put 500 back on screen for a
            // balance the next pull is refused against.
            CollectionAssert.AreEquivalent(
                new[] { "Ball:ball_pro 1->4" },
                raises.ConvertAll(r => r.ToString()));
            Assert.AreEqual(10, save.ticketBalances[0].balance,
                "the local display cache must be left exactly as it was");
        }

        [Test]
        public void Passing_no_collector_changes_nothing_about_the_merge()
        {
            // The count is observation, never behaviour. Every pre-existing caller passes null.
            var withList = Populated();
            var withNull = Populated();
            withList.itemQuantities["item_repair_kit"] = withNull.itemQuantities["item_repair_kit"] = 2;

            var theirs = new InventorySnapshot();
            theirs.Items["item_repair_kit"] = 3;

            Assert.AreEqual(InventoryProjector.Apply(theirs, withNull),
                            InventoryProjector.Apply(theirs, withList, new List<InventoryRaise>()));
            Assert.AreEqual(withNull.itemQuantities["item_repair_kit"],
                            withList.itemQuantities["item_repair_kit"]);
        }
    }
}
