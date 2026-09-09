// asset_loans §3 — a borrowed asset never reaches the synced blob.
//
// WHY THIS IS WORTH ITS OWN TEST, when two layers upstream already stop it. The blob's merge is
// ADDITIVE (`InventoryMerge` only ever raises), so a borrowed row that leaks into it once is in it
// on every device FOREVER, and no loan ending can take it back out: the player would permanently
// "own" somebody else's character, in a store the server treats as the client's own assertion.
// Three guards exist for that reason — the [NonSerialized] flags, the two manager-side skips, and
// this one — and this is the only one an EditMode test can hold still.
using Golfin.InventorySync;
using Golfin.Save;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Golfin.InventorySync.Tests
{
    public class InventoryCodecLoanTests
    {
        private static InventorySnapshot Snapshot()
        {
            var snap = new InventorySnapshot();
            snap.Characters.Add(new PersistedCharacter
            {
                characterId = "char_mine", currentLevel = 12, totalSPEarned = 4, isOwned = true
            });
            snap.Characters.Add(new PersistedCharacter
            {
                characterId = "char_borrowed", currentLevel = 90, totalSPEarned = 0,
                isOwned = true, isBorrowed = true
            });
            snap.Clubs.Add(new PersistedClub
            {
                clubId = "club_mine", currentLevel = 5, currentDurability = 80, maxDurability = 100
            });
            snap.Clubs.Add(new PersistedClub
            {
                clubId = "club_borrowed", currentLevel = 60, currentDurability = 100,
                maxDurability = 100, isBorrowed = true
            });
            return snap;
        }

        [Test]
        public void ABorrowedCharacterIsNotEncoded()
        {
            JObject root = InventoryCodec.EncodeToObject(Snapshot(), null);
            var ids = root["characters"].Values<JToken>();

            foreach (JToken t in ids)
            {
                string id = t.Type == JTokenType.String ? (string)t : (string)t["id"];
                Assert.AreNotEqual("char_borrowed", id,
                    "a borrowed character in the blob is permanent — the merge only ever raises");
            }
        }

        [Test]
        public void ABorrowedClubIsNotEncoded()
        {
            JObject root = InventoryCodec.EncodeToObject(Snapshot(), null);

            foreach (JToken t in root["clubs"].Values<JToken>())
            {
                string id = t.Type == JTokenType.String ? (string)t : (string)t["id"];
                Assert.AreNotEqual("club_borrowed", id);
            }
        }

        [Test]
        public void TheOwnedRowsAreStillThere()
        {
            // The skip must remove the borrowed rows and NOTHING else — a filter that dropped a
            // real row would be a silent subtraction from the player's inventory.
            JObject root = InventoryCodec.EncodeToObject(Snapshot(), null);

            Assert.AreEqual(1, ((JArray)root["characters"]).Count);
            Assert.AreEqual(1, ((JArray)root["clubs"]).Count);
        }

        [Test]
        public void TheProjectorCarriesTheFlagSoTheSkipIsReal()
        {
            // The codec reads the flag off the snapshot, and the snapshot is built by the
            // projector. If the projector dropped the flag on the way through, the codec's guard
            // would be decorative — it could never fire even on a genuine upstream leak.
            var save = new SaveData();
            save.ownedCharacters.Add(new PersistedCharacter
            {
                characterId = "char_borrowed", isOwned = true, isBorrowed = true
            });
            save.ownedClubs.Add(new PersistedClub { clubId = "club_borrowed", isBorrowed = true });

            InventorySnapshot snap = InventoryProjector.Project(save);

            Assert.IsTrue(snap.Characters[0].isBorrowed);
            Assert.IsTrue(snap.Clubs[0].isBorrowed);

            JObject root = InventoryCodec.EncodeToObject(snap, null);
            Assert.IsNull(root["characters"], "nothing but borrowed rows means no characters key at all");
            Assert.IsNull(root["clubs"]);
        }
    }
}
