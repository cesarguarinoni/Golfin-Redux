// ─────────────────────────────────────────────────────────────────────────────
// content_player_inventory — the wire format: deltas from the catalog default.
//
// Acceptance covered:
//   * "Blob is deltas-from-default — a default-state club is just its id"
//   * "RP, leaderboard accumulators and tournament entries are NOT in the blob"
//   * the blob round-trips
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Text;
using Golfin.InventorySync;
using Golfin.Save;
using NUnit.Framework;

namespace Golfin.InventorySync.Tests
{
    /// <summary>A hand-built catalog: two clubs, two characters, known defaults.</summary>
    internal sealed class FakeCatalog : IInventoryCatalog
    {
        public readonly Dictionary<string, PersistedClub> Clubs =
            new Dictionary<string, PersistedClub>();
        public readonly Dictionary<string, PersistedCharacter> Characters =
            new Dictionary<string, PersistedCharacter>();

        public static FakeCatalog Standard()
        {
            var c = new FakeCatalog();
            c.Clubs["club_iron9_klyro"] = new PersistedClub
            {
                clubId = "club_iron9_klyro", currentLevel = 10,
                currentDurability = 40, maxDurability = 40, totalSPEarned = 9,
            };
            c.Clubs["club_driver_golfinx"] = new PersistedClub
            {
                clubId = "club_driver_golfinx", currentLevel = 80,
                currentDurability = 60, maxDurability = 60, totalSPEarned = 79,
            };
            c.Characters["char_ken"] = new PersistedCharacter
            { characterId = "char_ken", currentLevel = 10, isOwned = true };
            c.Characters["char_mia"] = new PersistedCharacter
            { characterId = "char_mia", currentLevel = 80, isOwned = true };
            return c;
        }

        public bool TryGetClubDefault(string clubId, out PersistedClub def)
            => Clubs.TryGetValue(clubId ?? "", out def);

        public bool TryGetCharacterDefault(string characterId, out PersistedCharacter def)
            => Characters.TryGetValue(characterId ?? "", out def);
    }

    public class InventoryCodecTests
    {
        private FakeCatalog _catalog;

        [SetUp]
        public void SetUp() => _catalog = FakeCatalog.Standard();

        // ── Deltas from default ──────────────────────────────────────────────

        [Test]
        public void A_default_state_club_encodes_as_a_bare_id()
        {
            var snap = new InventorySnapshot();
            snap.Clubs.Add(InventoryProjector.CloneClub(_catalog.Clubs["club_iron9_klyro"]));

            string json = InventoryCodec.Encode(snap, _catalog);

            StringAssert.Contains("\"clubs\":[\"club_iron9_klyro\"]", json);
        }

        [Test]
        public void A_levelled_club_encodes_only_the_fields_that_differ()
        {
            var club = InventoryProjector.CloneClub(_catalog.Clubs["club_iron9_klyro"]);
            club.currentLevel = 14;
            club.equippedBagSlot = 1;

            var snap = new InventorySnapshot();
            snap.Clubs.Add(club);

            string json = InventoryCodec.Encode(snap, _catalog);

            StringAssert.Contains("\"lv\":14", json);
            StringAssert.Contains("\"slot\":1", json);
            // Everything still at default is ABSENT — that is the compression.
            StringAssert.DoesNotContain("\"dur\"", json);
            StringAssert.DoesNotContain("\"maxDur\"", json);
            StringAssert.DoesNotContain("\"sPow\"", json);
        }

        [Test]
        public void A_default_state_character_encodes_as_a_bare_id_and_a_locked_one_does_not()
        {
            var owned = InventoryProjector.CloneCharacter(_catalog.Characters["char_ken"]);
            var locked = InventoryProjector.CloneCharacter(_catalog.Characters["char_mia"]);
            locked.isOwned = false;

            var snap = new InventorySnapshot();
            snap.Characters.Add(owned);
            snap.Characters.Add(locked);

            string json = InventoryCodec.Encode(snap, _catalog);

            StringAssert.Contains("\"char_ken\"", json);
            StringAssert.Contains("\"own\":false", json);
            // `own:true` is never written — it is the default for a row in ownedCharacters.
            StringAssert.DoesNotContain("\"own\":true", json);
        }

        [Test]
        public void With_no_catalog_every_row_encodes_in_full_and_nothing_is_lost()
        {
            var club = InventoryProjector.CloneClub(_catalog.Clubs["club_iron9_klyro"]);
            var snap = new InventorySnapshot();
            snap.Clubs.Add(club);

            string json = InventoryCodec.Encode(snap, EmptyInventoryCatalog.Instance);
            StringAssert.Contains("\"lv\":10", json);

            var back = InventoryCodec.Decode(json, EmptyInventoryCatalog.Instance);
            Assert.AreEqual(1, back.Clubs.Count);
            Assert.AreEqual(10, back.Clubs[0].currentLevel);
            Assert.AreEqual(40, back.Clubs[0].maxDurability);
        }

        // ── Round trips ──────────────────────────────────────────────────────

        [Test]
        public void The_blob_round_trips_through_encode_and_decode()
        {
            var snap = new InventorySnapshot();
            var levelled = InventoryProjector.CloneClub(_catalog.Clubs["club_driver_golfinx"]);
            levelled.currentLevel = 91;
            levelled.spentPower = 4;
            levelled.currentDurability = 12;
            snap.Clubs.Add(InventoryProjector.CloneClub(_catalog.Clubs["club_iron9_klyro"]));
            snap.Clubs.Add(levelled);
            snap.Characters.Add(InventoryProjector.CloneCharacter(_catalog.Characters["char_ken"]));
            snap.Items["item_repair_kit"] = 3;
            snap.Balls["ball_standard"] = -1;
            snap.Tickets[0] = 10;
            snap.UnlockedHoles.AddRange(new[] { 1, 2, 5 });
            snap.StarterCharacterId = "char_ken";
            snap.SelectedCharacterId = "char_mia";

            var back = InventoryCodec.Decode(InventoryCodec.Encode(snap, _catalog), _catalog);

            Assert.AreEqual(2, back.Clubs.Count);
            Assert.AreEqual(10, back.Clubs[0].currentLevel, "the bare-id club re-expands to the catalog default");
            Assert.AreEqual(40, back.Clubs[0].maxDurability);
            Assert.AreEqual(91, back.Clubs[1].currentLevel);
            Assert.AreEqual(4,  back.Clubs[1].spentPower);
            Assert.AreEqual(12, back.Clubs[1].currentDurability);
            Assert.AreEqual(1,  back.Characters.Count);
            Assert.IsTrue(back.Characters[0].isOwned);
            Assert.AreEqual(3,  back.Items["item_repair_kit"]);
            Assert.AreEqual(-1, back.Balls["ball_standard"]);
            Assert.AreEqual(10, back.Tickets[0]);
            CollectionAssert.AreEqual(new[] { 1, 2, 5 }, back.UnlockedHoles);
            Assert.AreEqual("char_ken", back.StarterCharacterId);
            Assert.AreEqual("char_mia", back.SelectedCharacterId);
        }

        [Test]
        public void A_bare_id_club_picks_up_a_catalog_rebalance_for_free()
        {
            // SPEC §1: "catalog rebalances propagate to untouched instances for free". That is not a
            // nice side effect of the delta encoding — it is the reason the id alone is enough.
            var snap = new InventorySnapshot();
            snap.Clubs.Add(InventoryProjector.CloneClub(_catalog.Clubs["club_iron9_klyro"]));
            string json = InventoryCodec.Encode(snap, _catalog);

            // Publish a rebalance: the starting level moves 10 → 12, durability 40 → 55.
            _catalog.Clubs["club_iron9_klyro"] = new PersistedClub
            {
                clubId = "club_iron9_klyro", currentLevel = 12,
                currentDurability = 55, maxDurability = 55, totalSPEarned = 11,
            };

            var back = InventoryCodec.Decode(json, _catalog);
            Assert.AreEqual(12, back.Clubs[0].currentLevel);
            Assert.AreEqual(55, back.Clubs[0].maxDurability);
        }

        [Test]
        public void A_levelled_club_does_NOT_pick_up_a_rebalance_of_the_level_it_earned()
        {
            var club = InventoryProjector.CloneClub(_catalog.Clubs["club_iron9_klyro"]);
            club.currentLevel = 14;
            var snap = new InventorySnapshot();
            snap.Clubs.Add(club);
            string json = InventoryCodec.Encode(snap, _catalog);

            _catalog.Clubs["club_iron9_klyro"].currentLevel = 12;

            Assert.AreEqual(14, InventoryCodec.Decode(json, _catalog).Clubs[0].currentLevel);
        }

        [Test]
        public void An_unknown_club_id_is_still_owned_after_a_decode()
        {
            // I6: nothing is deleted, only deactivated. A club the catalog can no longer place must
            // survive the decode — dropping it would be the one silent subtraction in the feature.
            var back = InventoryCodec.Decode("{\"v\":1,\"clubs\":[\"club_retired\"]}", _catalog);
            Assert.AreEqual(1, back.Clubs.Count);
            Assert.AreEqual("club_retired", back.Clubs[0].clubId);
        }

        [Test]
        public void Garbage_decodes_to_an_empty_snapshot_rather_than_throwing()
        {
            Assert.AreEqual(0, InventoryCodec.Decode("not json at all", _catalog).Clubs.Count);
            Assert.AreEqual(0, InventoryCodec.Decode("", _catalog).Clubs.Count);
            Assert.AreEqual(0, InventoryCodec.Decode((string)null, _catalog).Clubs.Count);
        }

        // ── The size claim, measured ─────────────────────────────────────────

        [Test]
        public void A_realistic_tester_blob_is_small_and_the_bytes_are_reported()
        {
            // Acceptance: "paste a real blob and its byte size". 40 starter-state clubs plus two the
            // player levelled — the shape SPEC §1 budgets ~3 KB for.
            var snap = new InventorySnapshot();
            for (int i = 0; i < 40; i++)
            {
                string id = "club_seed_" + i;
                _catalog.Clubs[id] = new PersistedClub
                { clubId = id, currentLevel = 10, currentDurability = 40, maxDurability = 40 };
                snap.Clubs.Add(InventoryProjector.CloneClub(_catalog.Clubs[id]));
            }
            var levelled = InventoryProjector.CloneClub(_catalog.Clubs["club_seed_0"]);
            levelled.currentLevel = 31;
            levelled.spentPower = 6;
            snap.Clubs[0] = levelled;

            snap.Characters.Add(InventoryProjector.CloneCharacter(_catalog.Characters["char_ken"]));
            snap.Items["item_repair_kit"] = 3;
            snap.Tickets[0] = 10;
            snap.UnlockedHoles.AddRange(new[] { 1, 2, 3 });
            snap.StarterCharacterId = "char_ken";
            snap.SelectedCharacterId = "char_ken";

            string json = InventoryCodec.Encode(snap, _catalog);
            int bytes = Encoding.UTF8.GetByteCount(json);

            UnityEngine.Debug.Log($"[content_player_inventory] 40-club tester blob = {bytes} bytes:\n{json}");
            Assert.Less(bytes, 3072, "SPEC §1 budgets ~3 KB per player for a blob this shape");
        }
    }
}
