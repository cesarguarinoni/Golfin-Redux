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
        /// <summary>
        /// The SERVER-side delivery path has no stack ceiling, and that is the behaviour the
        /// local paths were changed to match on 2026-08-27.
        ///
        /// <para>
        /// Until then `ItemManager.AddItems` clamped to 99 and `InventoryGrants.AddQuantity` did
        /// not, so the same purchase delivered a different number depending on which path ran.
        /// Worse, the clamp was a SILENT SWALLOW on a paid purchase: `Apply` writes
        /// `appliedGrantIds` and acks BEFORE calling `ApplyOne`, so a clamped add spent the
        /// player's RP and delivered nothing, with the grant marked delivered.
        /// </para>
        /// </summary>
        [Test]
        public void Item_grants_stack_past_the_old_99_ceiling()
        {
            var save = new SaveData { schemaVersion = 11 };
            var grants = new List<InventoryGrant>();
            for (int i = 0; i < 12; i++)
                grants.Add(new InventoryGrant
                {
                    Id = $"g{i}", Kind = InventoryGrants.KindItem,
                    RefId = "repairkit_common", Amount = 10,
                });

            InventoryGrants.Apply(grants, save, null);

            Assert.AreEqual(120, save.itemQuantities["repairkit_common"],
                "12 distinct grants of 10 must deliver 120. A ceiling here would be a paid " +
                "purchase that delivers nothing, because Apply acks before it applies.");
        }

        /// <summary>Balls are the other stackable the shop sells, and had the same clamp.</summary>
        [Test]
        public void Ball_grants_stack_past_the_old_99_ceiling()
        {
            var save = new SaveData { schemaVersion = 11 };
            var grants = new List<InventoryGrant>();
            for (int i = 0; i < 12; i++)
                grants.Add(new InventoryGrant
                {
                    Id = $"b{i}", Kind = InventoryGrants.KindBall,
                    RefId = "ball_putt_ace", Amount = 10,
                });

            InventoryGrants.Apply(grants, save, null);

            Assert.AreEqual(120, save.ballQuantities["ball_putt_ace"]);
        }

        /// <summary>
        /// `-1` is the UNLIMITED sentinel, not a quantity, and uncapping must not have turned it
        /// into one. An unlimited ball that started accumulating would become a finite stack of
        /// 10 — strictly worse than the no-op.
        /// </summary>
        [Test]
        public void An_unlimited_quantity_is_still_left_alone()
        {
            var save = new SaveData { schemaVersion = 11 };
            save.ballQuantities ??= new Dictionary<string, int>();
            save.ballQuantities["ball_golfin_default"] = -1;

            InventoryGrants.Apply(new List<InventoryGrant>
            {
                new InventoryGrant
                {
                    Id = "u1", Kind = InventoryGrants.KindBall,
                    RefId = "ball_golfin_default", Amount = 10,
                },
            }, save, null);

            Assert.AreEqual(-1, save.ballQuantities["ball_golfin_default"],
                "-1 means unlimited; adding to it must stay a no-op.");
        }

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
        public void A_grant_whose_id_is_already_in_the_ledger_changes_nothing()
        {
            // THIS PINS THE MID-SESSION APPLY (shop_server_purchase §3.2). A shop purchase applies
            // its grant through the MANAGERS while the game is running and then records the id here.
            // The next boot's drain still sees that grant pending server-side (the ack can have died
            // on the network), so it MUST come back as a duplicate and touch nothing — otherwise a
            // bought stackable item would silently double on the launch after every purchase.
            var save = SaveData.CreateFresh();
            save.appliedGrantIds = new List<string> { "g-shop-1" };
            save.itemQuantities["item_repair_kit"] = 3;

            var result = InventoryGrants.Apply(
                new List<InventoryGrant>
                {
                    new InventoryGrant { Id = "g-shop-1", Kind = "item", RefId = "item_repair_kit", Amount = 2 }
                },
                save, null);

            Assert.AreEqual(0, result.AppliedCount, "An id already in the ledger applies NOTHING.");
            Assert.AreEqual(1, result.DuplicateCount);
            Assert.AreEqual(3, save.itemQuantities["item_repair_kit"],
                "The quantity must be untouched — the mid-session apply already added it.");
            Assert.AreEqual(1, save.appliedGrantIds.Count, "…and the id is not recorded twice.");
            CollectionAssert.Contains(result.AckIds, "g-shop-1",
                "It is still ACKED, or it comes back on every single boot forever.");
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
