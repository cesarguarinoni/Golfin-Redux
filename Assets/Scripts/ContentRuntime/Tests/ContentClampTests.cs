// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Content.Tests — ContentClampTests
//
// The clamp is the one part of content_overlay_catalogs that can corrupt a
// player's save, so it is the part with the most tests. Every case here is a
// publish an operator can actually make from the admin panel.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using System.Linq;
using Golfin.Content;
using Golfin.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Golfin.Content.Tests
{
    [TestFixture]
    public class ContentClampTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static Dictionary<string, ClubClampDefinition> ClubDef(
            string id = "club_x", int maxDurability = 100, int startLevel = 10,
            int maxLevel = 39, int maxSpentPerStat = 20)
            => new Dictionary<string, ClubClampDefinition>
            {
                { id, new ClubClampDefinition(id, maxDurability, startLevel, maxLevel, maxSpentPerStat) }
            };

        private static Dictionary<string, CharacterClampDefinition> CharDef(
            string id = "char_x", int startLevel = 10, int maxLevel = 39,
            int str = 18, int cc = 18, int rec = 13, int stam = 15)
            => new Dictionary<string, CharacterClampDefinition>
            {
                { id, new CharacterClampDefinition(id, startLevel, maxLevel, str, cc, rec, stam) }
            };

        private static ClampEvent? Find(IEnumerable<ClampEvent> events, string field)
        {
            foreach (var e in events) if (e.Field == field) return e;
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // §2 — durability. The acceptance case: a published maxDurability BELOW an
        //      owned club's currentDurability.
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void PublishedMaxDurabilityBelowCurrent_ClampsBothAndLogsEach()
        {
            var club = new PersistedClub
            {
                clubId = "club_x", currentLevel = 20,
                currentDurability = 95, maxDurability = 100,
            };

            // The publish: maxDurability 100 → 60.
            var events = ContentClamp.ClampClubs(new List<PersistedClub> { club }, ClubDef(maxDurability: 60));

            Assert.AreEqual(60, club.maxDurability,
                "the saved ceiling must follow the published catalog down");
            Assert.AreEqual(60, club.currentDurability,
                "currentDurability must follow the new ceiling — this is the save-corrupting case");

            var maxEvent = Find(events, nameof(PersistedClub.maxDurability));
            var curEvent = Find(events, nameof(PersistedClub.currentDurability));

            Assert.IsNotNull(maxEvent, "the maxDurability move must be reported");
            Assert.IsNotNull(curEvent, "the currentDurability move must be reported");

            Assert.AreEqual(100, maxEvent!.Value.OldValue);
            Assert.AreEqual(60,  maxEvent!.Value.NewValue);
            Assert.AreEqual(95,  curEvent!.Value.OldValue);
            Assert.AreEqual(60,  curEvent!.Value.NewValue);
            Assert.AreEqual("club_x", curEvent!.Value.Id, "every event must name the id");
        }

        [Test]
        public void DurabilityAlreadyInsideCeiling_IsNotTouchedAndReportsNothing()
        {
            var club = new PersistedClub
            {
                clubId = "club_x", currentLevel = 20,
                currentDurability = 40, maxDurability = 100,
            };

            var events = ContentClamp.ClampClubs(new List<PersistedClub> { club }, ClubDef(maxDurability: 100));

            Assert.AreEqual(40, club.currentDurability);
            Assert.IsEmpty(events, "a save that already fits must produce no clamp events at all");
        }

        [Test]
        public void NegativeDurability_ClampsToZero()
        {
            var club = new PersistedClub { clubId = "club_x", currentLevel = 20, currentDurability = -5, maxDurability = 100 };
            var events = ContentClamp.ClampClubs(new List<PersistedClub> { club }, ClubDef(maxDurability: 100));

            Assert.AreEqual(0, club.currentDurability);
            Assert.IsNotNull(Find(events, nameof(PersistedClub.currentDurability)));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // §2 — level
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void PublishedMaxLevelBelowOwnedLevel_ClampsClubDown()
        {
            var club = new PersistedClub { clubId = "club_x", currentLevel = 38, currentDurability = 50, maxDurability = 100 };

            // The publish: maxLevel 39 → 25.
            var events = ContentClamp.ClampClubs(new List<PersistedClub> { club },
                                                 ClubDef(maxDurability: 100, maxLevel: 25));

            Assert.AreEqual(25, club.currentLevel);
            var e = Find(events, nameof(PersistedClub.currentLevel));
            Assert.IsNotNull(e);
            Assert.AreEqual(38, e!.Value.OldValue);
            Assert.AreEqual(25, e!.Value.NewValue);
        }

        [Test]
        public void PublishedMaxLevelBelowOwnedLevel_ClampsCharacterDown()
        {
            var pc = new PersistedCharacter { characterId = "char_x", currentLevel = 38 };

            var events = ContentClamp.ClampCharacters(new List<PersistedCharacter> { pc },
                                                      CharDef(maxLevel: 25));

            Assert.AreEqual(25, pc.currentLevel);
            var e = Find(events, nameof(PersistedCharacter.currentLevel));
            Assert.IsNotNull(e);
            Assert.AreEqual(38, e!.Value.OldValue);
            Assert.AreEqual(25, e!.Value.NewValue);
        }

        [Test]
        public void LevelBelowStartLevel_ClampsUpToStartLevel()
        {
            // A published startLevel ABOVE the saved level. Rarer than the downgrade, but the SPEC
            // names the band as [startLevel, maxLevel] and a level below the floor is as invalid as
            // one above the ceiling.
            var pc = new PersistedCharacter { characterId = "char_x", currentLevel = 3 };

            var events = ContentClamp.ClampCharacters(new List<PersistedCharacter> { pc },
                                                      CharDef(startLevel: 10, maxLevel: 39));

            Assert.AreEqual(10, pc.currentLevel);
            Assert.IsNotNull(Find(events, nameof(PersistedCharacter.currentLevel)));
        }

        [Test]
        public void MaxLevelOfZero_IsTreatedAsUnbounded_NotAsClampEverythingToOne()
        {
            // A hand-seeded row missing the maxLevel column must not silently demote every owned
            // instance to level 1. Absent means "no ceiling", not "ceiling of zero".
            var pc = new PersistedCharacter { characterId = "char_x", currentLevel = 150 };

            var events = ContentClamp.ClampCharacters(new List<PersistedCharacter> { pc },
                                                      CharDef(startLevel: 10, maxLevel: 0));

            Assert.AreEqual(150, pc.currentLevel, "a missing maxLevel must not clamp anything");
            Assert.IsEmpty(events.Where(e => e.Field == nameof(PersistedCharacter.currentLevel)));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // §2 — allocated SP, i.e. the rarity downgrade. CLAMP AND LOG, NO REFUND.
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void RarityDowngradeOrphaningSp_ClampsEachStatAndLogsIt()
        {
            // Legendary (STR cap 40, base 10 → 30 spendable) demoted to Rare
            // (STR cap 30, base 10 → 20 spendable). 28 allocated points no longer fit.
            var pc = new PersistedCharacter
            {
                characterId      = "char_x",
                currentLevel     = 30,
                spentStrength    = 28,
                spentClubControl = 25,
                spentRecovery    = 12,
                spentStamina     = 9,
                totalSPEarned    = 74,
            };

            var events = ContentClamp.ClampCharacters(
                new List<PersistedCharacter> { pc },
                CharDef(maxLevel: 119, str: 20, cc: 20, rec: 10, stam: 17));

            Assert.AreEqual(20, pc.spentStrength,    "STR SP clamps to the NEW rarity's ceiling");
            Assert.AreEqual(20, pc.spentClubControl, "CC SP clamps to the NEW rarity's ceiling");
            Assert.AreEqual(10, pc.spentRecovery,    "REC SP clamps to the NEW rarity's ceiling");
            Assert.AreEqual(9,  pc.spentStamina,     "STA already fits and must not move");

            Assert.IsNotNull(Find(events, nameof(PersistedCharacter.spentStrength)));
            Assert.IsNotNull(Find(events, nameof(PersistedCharacter.spentClubControl)));
            Assert.IsNotNull(Find(events, nameof(PersistedCharacter.spentRecovery)));
            Assert.IsNull(Find(events, nameof(PersistedCharacter.spentStamina)),
                "a stat that still fits must produce no event");
        }

        [Test]
        public void RarityDowngrade_DoesNotRefundTheOrphanedSp()
        {
            // SPEC §2, explicitly out of scope: the delta is clamped and LOGGED, never handed back.
            // Refunding is its own economy decision; inventing one here would make it impossible to
            // make properly later. This test exists so a well-meaning future change has to argue
            // with it rather than slip past.
            var pc = new PersistedCharacter
            {
                characterId = "char_x", currentLevel = 30,
                spentStrength = 28, totalSPEarned = 74,
            };

            ContentClamp.ClampCharacters(new List<PersistedCharacter> { pc }, CharDef(str: 20));

            Assert.AreEqual(20, pc.spentStrength, "the orphaned 8 points are clamped away");
            Assert.AreEqual(74, pc.totalSPEarned,
                "totalSPEarned must NOT be credited back — no refund is invented (SPEC §2)");
        }

        [Test]
        public void ClubSpIsCappedFlatPerStat_NotByRarity()
        {
            // PlayerClubData.MAX_SP_PER_STAT is a flat 20 regardless of rarity, so a club rarity
            // change cannot orphan club SP the way it orphans character SP. The clamp still catches
            // a corrupt value.
            var club = new PersistedClub
            {
                clubId = "club_x", currentLevel = 20, currentDurability = 50, maxDurability = 100,
                spentPower = 45, spentAccuracy = -3,
            };

            var events = ContentClamp.ClampClubs(new List<PersistedClub> { club },
                                                 ClubDef(maxDurability: 100, maxSpentPerStat: 20));

            Assert.AreEqual(20, club.spentPower);
            Assert.AreEqual(0,  club.spentAccuracy);
            Assert.IsNotNull(Find(events, nameof(PersistedClub.spentPower)));
            Assert.IsNotNull(Find(events, nameof(PersistedClub.spentAccuracy)));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // §4 / I6 — deactivate, never delete
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void EquippedBagSlot_IsNeverTouched()
        {
            // I6: a deactivated club that is currently equipped STAYS equipped. The clamp deals in
            // numeric bounds and must not have an opinion about the bag.
            var club = new PersistedClub
            {
                clubId = "club_x", currentLevel = 60, currentDurability = 999,
                maxDurability = 100, equippedBagSlot = 3,
            };

            ContentClamp.ClampClubs(new List<PersistedClub> { club }, ClubDef(maxDurability: 40, maxLevel: 39));

            Assert.AreEqual(3, club.equippedBagSlot,
                "I6: the clamp must never unequip a club, however far its stats moved");
            Assert.AreEqual(40, club.currentDurability, "…while still clamping what it does own");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Rows with no definition
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void OwnedClubWithNoCatalogDefinition_IsLeftAloneAndReported()
        {
            // A save can name a club the catalog no longer carries. Its row is still renderable from
            // the save's own copy of maxDurability, and inventing bounds for it would be a guess
            // with a player's data.
            var club = new PersistedClub
            {
                clubId = "club_vanished", currentLevel = 55,
                currentDurability = 80, maxDurability = 80,
            };

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "no catalog definition"));

            var events = ContentClamp.ClampClubs(new List<PersistedClub> { club }, ClubDef("club_other"));

            Assert.IsEmpty(events);
            Assert.AreEqual(80, club.currentDurability, "an undefined row is left exactly as it was");
            Assert.AreEqual(55, club.currentLevel);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Idempotence + degenerate input
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ClampIsIdempotent_SecondRunMovesNothing()
        {
            var club = new PersistedClub { clubId = "club_x", currentLevel = 99, currentDurability = 500, maxDurability = 500 };
            var defs = ClubDef(maxDurability: 60, maxLevel: 39);

            var first  = ContentClamp.ClampClubs(new List<PersistedClub> { club }, defs);
            var second = ContentClamp.ClampClubs(new List<PersistedClub> { club }, defs);

            Assert.IsNotEmpty(first,  "the first pass has work to do");
            Assert.IsEmpty(second,
                "the second pass must be a no-op — otherwise every launch would re-log the same clamp");
        }

        [Test]
        public void NullAndEmptyInput_AreSafe()
        {
            Assert.IsEmpty(ContentClamp.ClampClubs(null, ClubDef()));
            Assert.IsEmpty(ContentClamp.ClampClubs(new List<PersistedClub>(), ClubDef()));
            Assert.IsEmpty(ContentClamp.ClampCharacters(null, CharDef()));
            Assert.IsEmpty(ContentClamp.ClampCharacters(new List<PersistedCharacter>(), CharDef()));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // The log line IS the artifact (SPEC §2 rule 2)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void EveryClampLogsAWarningNamingIdFieldOldAndNew()
        {
            var club = new PersistedClub { clubId = "club_logged", currentLevel = 20, currentDurability = 95, maxDurability = 100 };
            var events = ContentClamp.ClampClubs(new List<PersistedClub> { club }, ClubDef("club_logged", maxDurability: 60));

            // One warning per event + one summary. A clamp that happens silently is
            // indistinguishable from a bug report six weeks later.
            foreach (var _ in events)
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                    @"CLAMPED clubs 'club_logged': \w+ \d+ → \d+"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                @"Clamp \(clubs\): \d+ field\(s\) clamped"));

            ContentClamp.LogAll(events, "clubs");
        }

        [Test]
        public void NothingToClamp_LogsOnePlainLineAndNoWarning()
        {
            // The quiet path still says something — "the clamp ran and had nothing to do" and
            // "the clamp never ran" are different facts, and a device log six weeks later needs to
            // tell them apart. But it is a Log, never a Warning: a launch where every owned
            // instance already fits is the normal case, and warning about it would train everyone
            // to scroll past the warnings that matter.
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(
                @"Clamp \(clubs\): nothing to clamp"));

            ContentClamp.LogAll(new List<ClampEvent>(), "clubs");

            LogAssert.NoUnexpectedReceived();
        }
    }
}
