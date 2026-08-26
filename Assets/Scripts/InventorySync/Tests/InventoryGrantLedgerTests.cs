// ─────────────────────────────────────────────────────────────────────────────
// content_player_inventory — the applied-grant ledger and its schema migration.
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using Golfin.InventorySync;
using Golfin.Save;
using NUnit.Framework;

namespace Golfin.InventorySync.Tests
{
    public class InventoryGrantLedgerTests
    {
        [Test]
        public void The_v10_to_v11_migration_adds_an_empty_ledger()
        {
            // An existing save has never applied a grant — there was no grants queue before this
            // build — so an empty list is the TRUE history, not a default standing in for one.
            var legacy = new SaveData { schemaVersion = 10 };
            SaveSchemaMigrator.Migrate(legacy);

            Assert.AreEqual(SaveSchemaMigrator.CurrentSchemaVersion, legacy.schemaVersion);
            Assert.IsNotNull(legacy.appliedGrantIds);
            Assert.AreEqual(0, legacy.appliedGrantIds.Count);
        }

        [Test]
        public void A_fresh_save_is_stamped_at_the_current_version_and_never_migrated()
        {
            Assert.AreEqual(SaveSchemaMigrator.CurrentSchemaVersion, SaveData.CreateFresh().schemaVersion);
        }

        [Test]
        public void A_null_ledger_from_a_hand_edited_save_does_not_NRE_on_drain()
        {
            var save = SaveData.CreateFresh();
            save.appliedGrantIds = null;

            var result = InventoryGrants.Apply(
                new List<InventoryGrant>
                {
                    new InventoryGrant
                    { Id = "g1", Kind = InventoryGrants.KindItem, RefId = "item_repair_kit", Amount = 2 },
                },
                save, EmptyInventoryCatalog.Instance);

            Assert.AreEqual(1, result.AppliedCount);
            Assert.AreEqual(2, save.itemQuantities["item_repair_kit"]);
            CollectionAssert.AreEqual(new[] { "g1" }, save.appliedGrantIds);
        }

        [Test]
        public void Repeated_grants_of_the_same_id_add_once_but_different_ids_stack()
        {
            var save = SaveData.CreateFresh();
            var g1 = new InventoryGrant
            { Id = "g1", Kind = InventoryGrants.KindItem, RefId = "item_repair_kit", Amount = 3 };
            var g2 = new InventoryGrant
            { Id = "g2", Kind = InventoryGrants.KindItem, RefId = "item_repair_kit", Amount = 3 };

            InventoryGrants.Apply(new List<InventoryGrant> { g1 }, save, null);
            InventoryGrants.Apply(new List<InventoryGrant> { g1 }, save, null);
            Assert.AreEqual(3, save.itemQuantities["item_repair_kit"], "same id, applied once");

            // A grant is NEW history, so a second, different grant stacks — unlike the merge, where
            // max is the only non-destructive answer for two views of the SAME history.
            InventoryGrants.Apply(new List<InventoryGrant> { g2 }, save, null);
            Assert.AreEqual(6, save.itemQuantities["item_repair_kit"]);
        }

        [Test]
        public void An_already_applied_grant_is_still_acked_so_it_stops_coming_back()
        {
            var save = SaveData.CreateFresh();
            var g = new InventoryGrant
            { Id = "g1", Kind = InventoryGrants.KindHole, RefId = "7", Amount = 1 };

            InventoryGrants.Apply(new List<InventoryGrant> { g }, save, null);
            var second = InventoryGrants.Apply(new List<InventoryGrant> { g }, save, null);

            Assert.AreEqual(0, second.AppliedCount);
            Assert.AreEqual(1, second.DuplicateCount);
            CollectionAssert.AreEqual(new[] { "g1" }, second.AckIds);
        }

        [Test]
        public void A_character_grant_unlocks_a_locked_row_rather_than_duplicating_it()
        {
            var save = SaveData.CreateFresh();
            save.ownedCharacters.Add(new PersistedCharacter
            { characterId = "char_mia", currentLevel = 88, isOwned = false });

            InventoryGrants.Apply(
                new List<InventoryGrant>
                {
                    new InventoryGrant
                    { Id = "g1", Kind = InventoryGrants.KindCharacter, RefId = "char_mia", Amount = 1 },
                },
                save, null);

            Assert.AreEqual(1, save.ownedCharacters.Count);
            Assert.IsTrue(save.ownedCharacters[0].isOwned);
            Assert.AreEqual(88, save.ownedCharacters[0].currentLevel, "the progress they already had survives");
        }

        [Test]
        public void A_ticket_grant_adds_to_the_right_kind()
        {
            var save = SaveData.CreateFresh();
            save.ticketBalances.Add(new PersistedTicketBalance { ticketTypeInt = 0, balance = 2 });

            InventoryGrants.Apply(
                new List<InventoryGrant>
                {
                    new InventoryGrant
                    { Id = "g1", Kind = InventoryGrants.KindTicket, RefId = "0", Amount = 5 },
                },
                save, null);

            Assert.AreEqual(7, save.ticketBalances[0].balance);
        }
    }
}
