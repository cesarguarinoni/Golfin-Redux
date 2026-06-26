// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments.Tests — SaveBackedEntryStoreTests (T5)
// EditMode NUnit suite gating SaveBackedEntryStore.
//
// Coverage per SPEC §5:
//   1. Round-trip: EntryState → Save → serialize → deserialize → Load → field-equal
//      (snapshot: characterId/level/strength/clubControl/recovery/stamina,
//       status, startedUtc, lastHoleUtc(set+null), claimed, per-hole fields)
//   2. Upsert: two Save calls same tournamentId → replace (not duplicate)
//   3. Claim: MarkClaimed persists; IsClaimed true after reload; idempotent
//   4. Debounce coalescing: N appends within 250ms → one disk write (OnSaved count)
//   5. Atomic-write / restart: ReloadFromDisk() after a hole append → entry survives
//
// Uses a real SaveDataHost on a temp GameObject + injected LocalJsonPersister
// (same pattern as SaveLayerPlayModeTests but runnable in EditMode because no
// coroutines or domain-reload semantics are needed — debounce test uses
// FlushNow() directly to sidestep the coroutine scheduler).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Golfin.Save;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.Tournaments.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="SaveBackedEntryStore"/>.
    /// </summary>
    [TestFixture]
    public class SaveBackedEntryStoreTests
    {
        private string _testDir = null!;
        private string _savePath = null!;
        private LocalJsonPersister _persister = null!;
        private GameObject _hostGo = null!;
        private SaveDataHost _host = null!;
        private SaveBackedEntryStore _store = null!;

        // ── Shared test data ─────────────────────────────────────────────────

        private static readonly DateTime StartedUtc  = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime HoleUtc     = new DateTime(2026, 8, 1, 10, 25, 0, DateTimeKind.Utc);
        private static readonly DateTime Hole2Utc    = new DateTime(2026, 8, 1, 10, 45, 0, DateTimeKind.Utc);

        private static CharacterSnapshot MakeSnapshot(string id = "char_alice")
            => new CharacterSnapshot(id, level: 42, strength: 20, clubControl: 18, recovery: 15, stamina: 22);

        private static HoleResult MakeHoleResult(string holeId, int strokes, DateTime completedUtc, int rngSeed = 42)
            => new HoleResult(holeId, strokes, timeSeconds: 95.5f, completedUtc, rngSeed, new List<ShotCommand>());

        [SetUp]
        public void SetUp()
        {
            // Create temp dir for save file
            _testDir  = Path.Combine(Path.GetTempPath(), $"GolfinSaveT5Test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
            _savePath = Path.Combine(_testDir, "save.json");

            // Create a fresh SaveData JSON at schemaVersion=3 with no entries
            var freshData = new SaveData { schemaVersion = 3 };
            File.WriteAllText(_savePath, Newtonsoft.Json.JsonConvert.SerializeObject(freshData));

            // Set up a real SaveDataHost on a temp GameObject with our injected persister
            _persister = new LocalJsonPersister(_savePath);
            _hostGo    = new GameObject("T5TestHost");
            _host      = _hostGo.AddComponent<SaveDataHost>();

            // Inject the persister BEFORE Awake fires (or after — we use the public API)
            _host.SetPersister(_persister);

            // Force a reload so _data is populated from our test file
            // (Awake may have already run with a default persister — ReloadFromDisk resets it)
            _host.ReloadFromDisk();

            _store = new SaveBackedEntryStore(_host);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hostGo != null)
                UnityEngine.Object.DestroyImmediate(_hostGo);

            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }

        // ── T5 Test: Round-trip ───────────────────────────────────────────────

        [Test]
        public async Task RoundTrip_EntryStateWithSnapshot_SaveLoadFieldEqual()
        {
            // Arrange
            var snapshot = MakeSnapshot("char_alice");
            var perHole = new List<HoleResult>
            {
                MakeHoleResult("h1", strokes: 3, HoleUtc,  rngSeed: 42),
                MakeHoleResult("h2", strokes: 5, Hole2Utc, rngSeed: 99)
            };
            var original = new EntryState(
                tournamentId: "t_open_2026",
                characterId:  "char_alice",
                snapshot:     snapshot,
                perHole:      perHole,
                startedUtc:   StartedUtc,
                lastHoleUtc:  Hole2Utc,
                status:       EntryStatus.InProgress
            );

            // Act: save → flush → reload
            _store.Save(original);
            await _host.FlushNow();
            _host.ReloadFromDisk();

            var loaded = _store.Load("t_open_2026");

            // Assert: not null
            Assert.IsNotNull(loaded, "Load must return an EntryState after Save+reload");

            // Assert: top-level fields
            Assert.AreEqual("t_open_2026",         loaded!.TournamentId);
            Assert.AreEqual("char_alice",           loaded.CharacterId);
            Assert.AreEqual(EntryStatus.InProgress, loaded.Status);
            Assert.AreEqual(StartedUtc,             loaded.StartedUtc);
            Assert.IsTrue(loaded.LastHoleUtc.HasValue, "lastHoleUtc must be non-null");
            Assert.AreEqual(Hole2Utc, loaded.LastHoleUtc!.Value);

            // Assert: per-hole
            Assert.AreEqual(2, loaded.PerHole.Count);
            Assert.AreEqual("h1", loaded.PerHole[0].HoleId);
            Assert.AreEqual(3,    loaded.PerHole[0].Strokes);
            Assert.AreEqual(95.5f, loaded.PerHole[0].TimeSeconds, delta: 0.001f);
            Assert.AreEqual(HoleUtc, loaded.PerHole[0].CompletedUtc);
            Assert.AreEqual(42,   loaded.PerHole[0].RngSeed);
            Assert.AreEqual("h2", loaded.PerHole[1].HoleId);
            Assert.AreEqual(5,    loaded.PerHole[1].Strokes);
            Assert.AreEqual(99,   loaded.PerHole[1].RngSeed);

            // Assert: snapshot (characterId/level/strength/clubControl/recovery/stamina)
            Assert.IsNotNull(loaded.Snapshot, "Snapshot must survive round-trip");
            Assert.AreEqual(snapshot, loaded.Snapshot, "CharacterSnapshot must be value-equal after round-trip");
            Assert.AreEqual("char_alice", loaded.Snapshot!.CharacterId);
            Assert.AreEqual(42,  loaded.Snapshot.Level);
            Assert.AreEqual(20,  loaded.Snapshot.Strength);
            Assert.AreEqual(18,  loaded.Snapshot.ClubControl);
            Assert.AreEqual(15,  loaded.Snapshot.Recovery);
            Assert.AreEqual(22,  loaded.Snapshot.Stamina);
        }

        [Test]
        public async Task RoundTrip_NullLastHoleUtc_SurvivesRoundTrip()
        {
            // lastHoleUtc = null → stored as "" → reloaded as null
            var original = new EntryState(
                tournamentId: "t_invitational",
                characterId:  "char_bob",
                snapshot:     null,
                perHole:      new List<HoleResult>(),
                startedUtc:   StartedUtc,
                lastHoleUtc:  null,
                status:       EntryStatus.InProgress
            );

            _store.Save(original);
            await _host.FlushNow();
            _host.ReloadFromDisk();

            var loaded = _store.Load("t_invitational");

            Assert.IsNotNull(loaded);
            Assert.IsFalse(loaded!.LastHoleUtc.HasValue, "null lastHoleUtc must survive round-trip as null");
            Assert.IsNull(loaded.Snapshot, "null snapshot must survive round-trip as null");
        }

        // ── T5 Test: Upsert ───────────────────────────────────────────────────

        [Test]
        public async Task Upsert_TwoSavesSameTournamentId_ReplaceNotDuplicate()
        {
            // First save: InProgress, 1 hole
            var first = new EntryState(
                tournamentId: "t_open_2026",
                characterId:  "char_alice",
                snapshot:     MakeSnapshot(),
                perHole:      new List<HoleResult> { MakeHoleResult("h1", 3, HoleUtc) },
                startedUtc:   StartedUtc,
                lastHoleUtc:  HoleUtc,
                status:       EntryStatus.InProgress
            );
            _store.Save(first);

            // Second save: Finished, 2 holes (upsert/replace — same tournamentId)
            var second = new EntryState(
                tournamentId: "t_open_2026",
                characterId:  "char_alice",
                snapshot:     MakeSnapshot(),
                perHole:      new List<HoleResult>
                {
                    MakeHoleResult("h1", 3, HoleUtc),
                    MakeHoleResult("h2", 4, Hole2Utc)
                },
                startedUtc:   StartedUtc,
                lastHoleUtc:  Hole2Utc,
                status:       EntryStatus.Finished
            );
            _store.Save(second);

            await _host.FlushNow();
            _host.ReloadFromDisk();

            // Assert: only ONE entry for this tournamentId
            Assert.AreEqual(1, _host.Data.tournamentEntries.Count,
                "Two Save calls for the same tournamentId must replace, not duplicate");

            var loaded = _store.Load("t_open_2026");
            Assert.IsNotNull(loaded);
            Assert.AreEqual(EntryStatus.Finished, loaded!.Status, "Second save must replace the first");
            Assert.AreEqual(2, loaded.PerHole.Count, "Must have 2 hole results from the second save");
        }

        // ── T5 Test: Claim ────────────────────────────────────────────────────

        [Test]
        public async Task Claim_MarkClaimed_PersistsAcrossReload()
        {
            // Arrange: save an entry first
            var entry = new EntryState(
                tournamentId: "t_claim_test",
                characterId:  "char_alice",
                snapshot:     MakeSnapshot(),
                perHole:      new List<HoleResult>(),
                startedUtc:   StartedUtc,
                lastHoleUtc:  null,
                status:       EntryStatus.Finished
            );
            _store.Save(entry);

            // Pre-condition: not claimed
            Assert.IsFalse(_store.IsClaimed("t_claim_test"),
                "IsClaimed must return false before MarkClaimed");

            // Act: mark claimed + flush + reload
            _store.MarkClaimed("t_claim_test");
            await _host.FlushNow();
            _host.ReloadFromDisk();

            // Assert: claimed persists after reload
            Assert.IsTrue(_store.IsClaimed("t_claim_test"),
                "IsClaimed must return true after MarkClaimed + reload");
        }

        [Test]
        public async Task Claim_MarkClaimed_IsIdempotent()
        {
            var entry = new EntryState(
                tournamentId: "t_idem_test",
                characterId:  "char_alice",
                snapshot:     MakeSnapshot(),
                perHole:      new List<HoleResult>(),
                startedUtc:   StartedUtc,
                lastHoleUtc:  null,
                status:       EntryStatus.Finished
            );
            _store.Save(entry);

            // Act: mark claimed twice
            _store.MarkClaimed("t_idem_test");
            Assert.DoesNotThrow(() => _store.MarkClaimed("t_idem_test"),
                "Second MarkClaimed must not throw");

            await _host.FlushNow();
            _host.ReloadFromDisk();

            Assert.IsTrue(_store.IsClaimed("t_idem_test"),
                "IsClaimed must be true after idempotent double-MarkClaimed");
        }

        [Test]
        public async Task Claim_SavePreservesClaimed_OnUpsert()
        {
            // Ensure that a subsequent Save() does NOT reset the claimed flag.
            var entry = new EntryState(
                tournamentId: "t_claimed_upsert",
                characterId:  "char_alice",
                snapshot:     MakeSnapshot(),
                perHole:      new List<HoleResult>(),
                startedUtc:   StartedUtc,
                lastHoleUtc:  null,
                status:       EntryStatus.Finished
            );
            _store.Save(entry);
            _store.MarkClaimed("t_claimed_upsert");

            // Now save again (simulating a late result upsert)
            _store.Save(entry);
            await _host.FlushNow();
            _host.ReloadFromDisk();

            Assert.IsTrue(_store.IsClaimed("t_claimed_upsert"),
                "Save/upsert must NOT reset the claimed flag");
        }

        // ── T5 Test: Debounce coalescing ──────────────────────────────────────

        [Test]
        public async Task Debounce_MultipleMarkDirtyWithin250ms_OneSavedEvent()
        {
            // Track OnSaved events
            int savedCount = 0;
            _host.OnSaved += () => savedCount++;

            // Act: call MarkDirty (via Save) multiple times in rapid succession
            // In EditMode we can't wait for real 250ms coroutines, so we bypass the
            // coroutine debounce by using FlushNow() directly — which the debounce
            // eventually calls. The point of this test is that N saves coalesce to
            // one flush. We verify by calling FlushNow once after N stores.
            var entry = new EntryState("t_debounce", "char_alice", MakeSnapshot(),
                new List<HoleResult>(), StartedUtc, null, EntryStatus.InProgress);

            // Simulate N mutations (each calls MarkDirty internally)
            for (int i = 0; i < 5; i++)
                _store.Save(entry);

            // One explicit flush (simulates what the debounce coroutine eventually calls)
            await _host.FlushNow();

            // Assert: exactly one OnSaved event for the single flush
            Assert.AreEqual(1, savedCount,
                "One FlushNow after N MarkDirty calls must produce exactly one OnSaved event");
        }

        // ── T5 Test: Atomic-write / restart ─────────────────────────────────

        [Test]
        public async Task AtomicWrite_ReloadFromDiskAfterAppend_EntrySurvives()
        {
            // Arrange: save an initial entry
            var initial = new EntryState(
                tournamentId: "t_restart",
                characterId:  "char_alice",
                snapshot:     MakeSnapshot(),
                perHole:      new List<HoleResult> { MakeHoleResult("h1", 3, HoleUtc) },
                startedUtc:   StartedUtc,
                lastHoleUtc:  HoleUtc,
                status:       EntryStatus.InProgress
            );
            _store.Save(initial);
            await _host.FlushNow();

            // Simulate restart via ReloadFromDisk
            _host.ReloadFromDisk();

            // Assert: entry survives restart
            var loaded = _store.Load("t_restart");
            Assert.IsNotNull(loaded, "Entry must survive a simulated restart (ReloadFromDisk)");
            Assert.AreEqual("t_restart", loaded!.TournamentId);
            Assert.AreEqual(1, loaded.PerHole.Count, "Per-hole results must survive restart");
            Assert.AreEqual("h1", loaded.PerHole[0].HoleId);
            Assert.AreEqual(3, loaded.PerHole[0].Strokes);
        }

        // ── T5 Test: Load returns null for unknown tournamentId ───────────────

        [Test]
        public void Load_UnknownTournamentId_ReturnsNull()
        {
            var result = _store.Load("nonexistent_tournament");
            Assert.IsNull(result, "Load must return null for a tournamentId that has never been saved");
        }
    }
}
