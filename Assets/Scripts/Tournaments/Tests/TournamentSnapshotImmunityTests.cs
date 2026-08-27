// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments.Tests — TournamentSnapshotImmunityTests
//
// SPEC §3 of content_overlay_catalogs: "Tournaments are already safe — do not
// 'fix' them. Add a test that pins this, because it is the kind of thing a
// later refactor removes without noticing."
//
// THE PROPERTY. PersistedTournamentEntry.snapshot freezes a character's stats at
// SIGN-UP (LocalTournamentBackend.Register → ICharacterStatsProvider.SnapshotFor),
// so a mid-event balance publish — which is exactly what Phase 2 makes possible —
// cannot alter an entry that is already running. Nothing in the content pipeline
// touches these tests, and that is the point: the safety comes from the snapshot
// being a COPY, and a refactor that made it a live lookup would compile, pass
// every other test, and silently re-balance tournaments in flight.
//
// Each test therefore does the same thing in a different place: change the stats
// AFTER registration and prove the entry does not move.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Golfin.Save;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.Tournaments.Tests
{
    /// <summary>
    /// A stats provider whose answers can be CHANGED between calls — standing in for a content
    /// publish landing between sign-up and the next launch.
    /// </summary>
    internal sealed class MutableStatsProvider : ICharacterStatsProvider
    {
        public int Level = 42, Strength = 20, ClubControl = 18, Recovery = 15, Stamina = 22;
        public int Calls { get; private set; }

        public CharacterSnapshot SnapshotFor(string characterId)
        {
            Calls++;
            return new CharacterSnapshot(characterId, Level, Strength, ClubControl, Recovery, Stamina);
        }

        /// <summary>Simulate a published balance change: everything moves, hard.</summary>
        public void PublishNerf()
        {
            Level = 10; Strength = 1; ClubControl = 1; Recovery = 1; Stamina = 1;
        }
    }

    [TestFixture]
    public class TournamentSnapshotImmunityTests
    {
        private string _testDir  = null!;
        private GameObject _hostGo = null!;
        private SaveDataHost _host = null!;
        private SaveBackedEntryStore _store = null!;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "golfin_snapshot_pin_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);

            _hostGo = new GameObject("TEST_SaveDataHost_SnapshotPin");
            _host   = _hostGo.AddComponent<SaveDataHost>();
            _host.SetPersister(new LocalJsonPersister(Path.Combine(_testDir, "save.json")));
            _host.ReloadFromDisk();

            _store = new SaveBackedEntryStore(_host);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hostGo != null) UnityEngine.Object.DestroyImmediate(_hostGo);
            if (Directory.Exists(_testDir)) Directory.Delete(_testDir, recursive: true);
        }

        private static EntryState Entry(CharacterSnapshot snapshot) => new EntryState(
            tournamentId:       "kasumigaseki_open",
            characterId:        snapshot.CharacterId,
            snapshot:           snapshot,
            perHole:            new List<HoleResult>(),
            startedUtc:         new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc),
            lastHoleUtc:        null,
            status:             EntryStatus.InProgress,
            conditionRemaining: -1f);

        // ═══════════════════════════════════════════════════════════════════════
        // 1. The snapshot is a COPY, taken once
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SnapshotIsTakenOnceAtSignUp_NotReReadPerAccess()
        {
            var stats = new MutableStatsProvider();
            CharacterSnapshot atSignUp = stats.SnapshotFor("char_james");
            int callsAfterSignUp = stats.Calls;

            stats.PublishNerf();

            Assert.AreEqual(20, atSignUp.Strength,
                "the snapshot the entry holds must be a VALUE captured at sign-up. If this ever " +
                "fails, someone turned CharacterSnapshot into a live lookup and every running " +
                "tournament is now re-balanced mid-event.");
            Assert.AreEqual(callsAfterSignUp, stats.Calls,
                "reading the snapshot must not call back into the stats provider");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 2. It survives the save round trip unchanged
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SnapshotSurvivesSaveAndReload_EvenAfterAPublishChangesTheCatalog()
        {
            var stats = new MutableStatsProvider();
            var entry = Entry(stats.SnapshotFor("char_james"));

            _store.Save(entry);

            // A content publish lands. Under Phase 2 this is a real, one-click operator action.
            stats.PublishNerf();

            EntryState? reloaded = _store.Load("kasumigaseki_open");

            Assert.IsNotNull(reloaded);
            Assert.IsNotNull(reloaded!.Snapshot);
            Assert.AreEqual(42, reloaded.Snapshot!.Level,       "level frozen at sign-up");
            Assert.AreEqual(20, reloaded.Snapshot.Strength,     "STR frozen at sign-up");
            Assert.AreEqual(18, reloaded.Snapshot.ClubControl,  "CC frozen at sign-up");
            Assert.AreEqual(15, reloaded.Snapshot.Recovery,     "REC frozen at sign-up");
            Assert.AreEqual(22, reloaded.Snapshot.Stamina,      "STA frozen at sign-up");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 3. The clamp does not reach it
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void TheClampStepDoesNotTouchTournamentSnapshots()
        {
            // ContentClamp works over SaveData.ownedClubs and SaveData.ownedCharacters. A running
            // entry lives in SaveData.tournamentEntries and must be untouched by it — which is why
            // the clamp deliberately has no tournament overload at all.
            var stats = new MutableStatsProvider();
            _store.Save(Entry(stats.SnapshotFor("char_james")));

            // The roster row for the SAME character is clamped hard, exactly as a rarity downgrade
            // would clamp it on the next launch.
            var owned = new PersistedCharacter
            {
                characterId = "char_james", currentLevel = 42,
                spentStrength = 30, spentClubControl = 30,
            };
            _host.Data.ownedCharacters.Add(owned);

            var defs = new Dictionary<string, Golfin.Content.CharacterClampDefinition>
            {
                { "char_james", new Golfin.Content.CharacterClampDefinition(
                      "char_james", startLevel: 10, maxLevel: 20,
                      maxSpentStrength: 5, maxSpentClubControl: 5,
                      maxSpentRecovery: 5, maxSpentStamina: 5) }
            };

            var events = Golfin.Content.ContentClamp.ClampCharacters(_host.Data.ownedCharacters, defs);
            Assert.IsNotEmpty(events, "the ROSTER row is genuinely clamped…");
            Assert.AreEqual(20, owned.currentLevel);
            Assert.AreEqual(5,  owned.spentStrength);

            EntryState? reloaded = _store.Load("kasumigaseki_open");
            Assert.IsNotNull(reloaded!.Snapshot);
            Assert.AreEqual(42, reloaded.Snapshot!.Level,
                "…and the tournament entry in flight is completely unaffected by it");
            Assert.AreEqual(20, reloaded.Snapshot.Strength);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 4. Two entries signed up at different times keep different stats
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void EntriesSignedUpEitherSideOfAPublish_KeepTheirOwnFrozenStats()
        {
            var stats = new MutableStatsProvider();

            var before = Entry(stats.SnapshotFor("char_james"));
            _store.Save(before);

            stats.PublishNerf();

            var afterEntry = new EntryState(
                tournamentId:       "second_open",
                characterId:        "char_james",
                snapshot:           stats.SnapshotFor("char_james"),
                perHole:            new List<HoleResult>(),
                startedUtc:         new DateTime(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc),
                lastHoleUtc:        null,
                status:             EntryStatus.InProgress,
                conditionRemaining: -1f);
            _store.Save(afterEntry);

            Assert.AreEqual(20, _store.Load("kasumigaseki_open")!.Snapshot!.Strength,
                "the entry signed up BEFORE the publish keeps the old stats");
            Assert.AreEqual(1, _store.Load("second_open")!.Snapshot!.Strength,
                "the entry signed up AFTER it picks up the new ones — a snapshot is a freeze, " +
                "not a permanent pin to whatever shipped in the build");
        }
    }
}
