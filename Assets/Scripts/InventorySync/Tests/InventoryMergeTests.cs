// ─────────────────────────────────────────────────────────────────────────────
// content_player_inventory — the additive merge.
//
// Acceptance covered: "`rev` mismatch merges additively — union ids, max levels;
// nothing lost on either side".
// ─────────────────────────────────────────────────────────────────────────────
using Golfin.InventorySync;
using Golfin.Save;
using NUnit.Framework;

namespace Golfin.InventorySync.Tests
{
    public class InventoryMergeTests
    {
        private static InventorySnapshot Snap(params PersistedClub[] clubs)
        {
            var s = new InventorySnapshot();
            s.Clubs.AddRange(clubs);
            return s;
        }

        private static PersistedClub Club(string id, int level = 1, int slot = 0, int dur = 10)
            => new PersistedClub
            { clubId = id, currentLevel = level, equippedBagSlot = slot,
              currentDurability = dur, maxDurability = 40 };

        // ── Union, never subtraction ─────────────────────────────────────────

        [Test]
        public void Ids_are_unioned_and_neither_side_loses_a_club()
        {
            var merged = InventoryMerge.Additive(
                Snap(Club("club_a"), Club("club_b")),
                Snap(Club("club_b"), Club("club_c")));

            CollectionAssert.AreEquivalent(
                new[] { "club_a", "club_b", "club_c" },
                merged.Clubs.ConvertAll(c => c.clubId));
        }

        [Test]
        public void Levels_and_spent_sp_take_the_max_from_either_side()
        {
            var mine = Club("club_a", level: 30);
            mine.spentPower = 2;
            mine.spentAccuracy = 7;
            var theirs = Club("club_a", level: 12);
            theirs.spentPower = 9;
            theirs.spentAccuracy = 1;

            var merged = InventoryMerge.Additive(Snap(mine), Snap(theirs));

            Assert.AreEqual(30, merged.Clubs[0].currentLevel);
            Assert.AreEqual(9,  merged.Clubs[0].spentPower);
            Assert.AreEqual(7,  merged.Clubs[0].spentAccuracy);
        }

        [Test]
        public void The_higher_durability_wins()
        {
            // SPEC §3 names this explicitly: a repair on either device must survive the merge.
            var merged = InventoryMerge.Additive(
                Snap(Club("club_a", dur: 4)),
                Snap(Club("club_a", dur: 38)));

            Assert.AreEqual(38, merged.Clubs[0].currentDurability);
        }

        [Test]
        public void The_local_bag_slot_wins_because_a_slot_is_an_arrangement_not_a_quantity()
        {
            var merged = InventoryMerge.Additive(
                Snap(Club("club_a", slot: 0)),
                Snap(Club("club_a", slot: 1)));

            Assert.AreEqual(0, merged.Clubs[0].equippedBagSlot);
        }

        [Test]
        public void Character_ownership_is_ORed_never_ANDed()
        {
            var mine = new InventorySnapshot();
            mine.Characters.Add(new PersistedCharacter { characterId = "char_ken", isOwned = false, currentLevel = 10 });
            var theirs = new InventorySnapshot();
            theirs.Characters.Add(new PersistedCharacter { characterId = "char_ken", isOwned = true, currentLevel = 44 });

            var merged = InventoryMerge.Additive(mine, theirs);

            Assert.IsTrue(merged.Characters[0].isOwned);
            Assert.AreEqual(44, merged.Characters[0].currentLevel);
        }

        [Test]
        public void Quantities_take_the_max_and_holes_are_unioned()
        {
            var mine = new InventorySnapshot();
            mine.Items["item_repair_kit"] = 3;
            mine.Tickets[0] = 10;
            mine.UnlockedHoles.AddRange(new[] { 1, 2 });

            var theirs = new InventorySnapshot();
            theirs.Items["item_repair_kit"] = 8;
            theirs.Items["item_tee"] = 1;
            theirs.Tickets[0] = 2;
            theirs.UnlockedHoles.AddRange(new[] { 2, 6 });

            var merged = InventoryMerge.Additive(mine, theirs);

            Assert.AreEqual(8, merged.Items["item_repair_kit"]);
            Assert.AreEqual(1, merged.Items["item_tee"]);
            Assert.AreEqual(10, merged.Tickets[0]);
            CollectionAssert.AreEqual(new[] { 1, 2, 6 }, merged.UnlockedHoles);
        }

        [Test]
        public void Unlimited_beats_every_finite_count_in_both_directions()
        {
            Assert.AreEqual(-1, InventoryMerge.MergeQuantity(-1, 5));
            Assert.AreEqual(-1, InventoryMerge.MergeQuantity(5, -1));
            Assert.AreEqual(9,  InventoryMerge.MergeQuantity(9, 5));
        }

        [Test]
        public void Scalars_prefer_mine_and_fall_back_to_theirs_when_mine_is_empty()
        {
            var theirs = new InventorySnapshot
            { StarterCharacterId = "char_theirs", SelectedCharacterId = "char_theirs" };

            var mineSet = new InventorySnapshot
            { StarterCharacterId = "char_mine", SelectedCharacterId = "char_mine" };
            var a = InventoryMerge.Additive(mineSet, theirs);
            Assert.AreEqual("char_mine", a.StarterCharacterId);

            // The fresh-install case: nothing local, so the blob answers.
            var b = InventoryMerge.Additive(new InventorySnapshot(), theirs);
            Assert.AreEqual("char_theirs", b.StarterCharacterId);
            Assert.AreEqual("char_theirs", b.SelectedCharacterId);
        }

        [Test]
        public void The_merge_is_a_superset_of_both_sides_so_nothing_is_lost_either_way()
        {
            var mine = Snap(Club("club_a", level: 30), Club("club_b"));
            mine.Items["item_repair_kit"] = 3;
            var theirs = Snap(Club("club_a", level: 12), Club("club_c"));
            theirs.Items["item_tee"] = 2;

            var merged = InventoryMerge.Additive(mine, theirs);

            // Applying the merge to EITHER original save adds only, never removes.
            foreach (var side in new[] { mine, theirs })
            {
                var save = SaveData.CreateFresh();
                save.unlockedHoles.Clear();
                InventoryProjector.Apply(side, save);
                int clubsBefore = save.ownedClubs.Count;

                InventoryProjector.Apply(merged, save);
                Assert.GreaterOrEqual(save.ownedClubs.Count, clubsBefore);
                Assert.AreEqual(3, save.ownedClubs.Count);
                Assert.AreEqual(30, save.ownedClubs.Find(c => c.clubId == "club_a").currentLevel);
            }
        }

        [Test]
        public void Merging_with_null_is_a_no_op_rather_than_an_exception()
        {
            Assert.AreEqual(1, InventoryMerge.Additive(Snap(Club("club_a")), null).Clubs.Count);
            Assert.AreEqual(1, InventoryMerge.Additive(null, Snap(Club("club_a"))).Clubs.Count);
            Assert.AreEqual(0, InventoryMerge.Additive(null, null).Clubs.Count);
        }
    }
}
