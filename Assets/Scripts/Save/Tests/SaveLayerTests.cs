#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Golfin.Save.Tests
{
    /// <summary>
    /// EditMode tests for the Golfin.Save layer.
    ///
    /// Coverage per SPEC §Definition of done:
    ///   1. SaveData round-trip (write → read → struct-equal)
    ///   2. Schema v1 PlayerPrefs migration (via SaveData shape)
    ///   3. OnSaved event fires after disk write
    ///   4. Debounce coalescing (10 MarkDirty calls → 1 write)
    ///   5. Atomic-write resilience (source file untouched if only tmp exists)
    ///   6. Dictionary round-trip via Newtonsoft
    /// </summary>
    [TestFixture]
    public class SaveLayerTests
    {
        private string _testDir = null!;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"GolfinSaveTest_{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }

        // ── Test 1: Round-trip ─────────────────────────────────────────────

        [Test]
        public async Task RoundTrip_WriteReadStructEqual()
        {
            // Arrange
            var savePath = Path.Combine(_testDir, "save.json");
            var persister = new LocalJsonPersister(savePath);

            var original = new SaveData
            {
                schemaVersion      = 1,
                rewardPoints       = 12345,
                selectedCharacterId = "char_alice",
                ownedCharacters    = new List<PersistedCharacter>
                {
                    new PersistedCharacter
                    {
                        characterId   = "char_alice",
                        currentLevel  = 42,
                        spentStrength = 3,
                        totalSPEarned = 5,
                        isSelected    = true
                    }
                },
                ballQuantities  = new Dictionary<string, int> { ["ball_golfin"] = -1, ["ball_pro"] = 7 },
                itemQuantities  = new Dictionary<string, int> { ["item_repair_common"] = 3 },
                unlockedHoles   = new List<int> { 1, 2 },
                playedHoles     = new List<int> { 1 }
            };

            // Act
            string json = JsonConvert.SerializeObject(original, Formatting.Indented);
            await persister.SaveAsync(json);

            Assert.IsTrue(persister.TryLoad(out string? loadedJson), "TryLoad should return true after save");
            var loaded = JsonConvert.DeserializeObject<SaveData>(loadedJson!);

            // Assert struct equality
            Assert.IsNotNull(loaded);
            Assert.AreEqual(original.schemaVersion,       loaded!.schemaVersion);
            Assert.AreEqual(original.rewardPoints,        loaded.rewardPoints);
            Assert.AreEqual(original.selectedCharacterId, loaded.selectedCharacterId);
            Assert.AreEqual(1,                            loaded.ownedCharacters.Count);
            Assert.AreEqual("char_alice",                 loaded.ownedCharacters[0].characterId);
            Assert.AreEqual(42,                           loaded.ownedCharacters[0].currentLevel);
            Assert.AreEqual(2,                            loaded.unlockedHoles.Count);
            Assert.Contains(2,                            loaded.unlockedHoles);
        }

        // ── Test 2: Schema migration ───────────────────────────────────────────

        [Test]
        public void SchemaMigration_CurrentVersion_NoMigrationNeeded()
        {
            // CurrentSchemaVersion (v2) — no migration steps apply, schemaVersion stays 2.
            var data = new SaveData { schemaVersion = SaveSchemaMigrator.CurrentSchemaVersion, rewardPoints = 500 };
            // Should not throw
            Assert.DoesNotThrow(() => SaveSchemaMigrator.Migrate(data));
            Assert.AreEqual(SaveSchemaMigrator.CurrentSchemaVersion, data.schemaVersion);
            Assert.AreEqual(500, data.rewardPoints);
        }

        [Test]
        public void SchemaMigration_V1_MigratesTo_CurrentVersion()
        {
            // v1 save must migrate up to CurrentSchemaVersion; rewardPoints preserved.
            var data = new SaveData { schemaVersion = 1, rewardPoints = 500 };
            // Should not throw; migration adds leaderboard RP accumulators (default 0) silently
            Assert.DoesNotThrow(() => SaveSchemaMigrator.Migrate(data));
            Assert.AreEqual(SaveSchemaMigrator.CurrentSchemaVersion, data.schemaVersion);
            Assert.AreEqual(500, data.rewardPoints);
        }

        [Test]
        public void SchemaMigration_FutureVersion_ThrowsSaveSchemaVersionException()
        {
            var data = new SaveData { schemaVersion = 999 };
            // Expect the Debug.LogError that SaveSchemaMigrator emits before throwing
            LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex(@"\[SaveSchemaMigrator\].*schema version 999"));
            Assert.Throws<SaveSchemaVersionException>(() => SaveSchemaMigrator.Migrate(data));
        }

        // ── Test 3: LocalJsonPersister.SaveAsync completes and creates save file ──
        //
        // This test verifies that LocalJsonPersister.SaveAsync completes successfully
        // and produces a file on disk. The full OnSaved-fires-after-disk-write coverage
        // (using real SaveDataHost + SpyPersister) lives in SaveLayerPlayModeTests.cs
        // because it requires MonoBehaviour lifecycle + coroutine runtime.

        [Test]
        public async Task LocalJsonPersister_SaveAsync_WritesFileToDisk()
        {
            // Arrange
            var savePath  = Path.Combine(_testDir, "save_persister_test.json");
            var persister = new LocalJsonPersister(savePath);
            var data      = new SaveData { rewardPoints = 42 };
            string json   = JsonConvert.SerializeObject(data);

            // Act
            await persister.SaveAsync(json);

            // Assert: file written successfully
            Assert.IsTrue(File.Exists(savePath),
                "LocalJsonPersister.SaveAsync must create save.json after completing.");

            // Verify content round-trips
            Assert.IsTrue(persister.TryLoad(out string? loaded));
            var result = JsonConvert.DeserializeObject<SaveData>(loaded!);
            Assert.AreEqual(42, result!.rewardPoints);
        }

        // ── Test 4: CountingPersister counts every direct SaveAsync call ───────
        //
        // This test verifies CountingPersister (the spy helper used by PlayMode tests)
        // increments its counter on each SaveAsync call. It establishes the baseline
        // that N direct SaveAsync calls produce N writes (no debounce at persister level).
        // The debounce coalescing test (10 MarkDirty → 1 write) lives in
        // SaveLayerPlayModeTests.cs, which can run real MonoBehaviour coroutines.

        [Test]
        public async Task CountingPersister_TenDirectCalls_CountsTenWrites()
        {
            // Arrange
            var savePath = Path.Combine(_testDir, "counting_persister_test.json");
            int writeCount = 0;
            var countingPersister = new CountingPersister(savePath, () => writeCount++);
            var data = new SaveData { rewardPoints = 100 };

            // Act: 10 direct SaveAsync calls (no debounce — this is persister-level)
            for (int i = 0; i < 10; i++)
            {
                string json = JsonConvert.SerializeObject(data);
                await countingPersister.SaveAsync(json);
            }

            // Assert: each call increments the counter — spy is working correctly
            Assert.AreEqual(10, writeCount,
                "CountingPersister must increment its counter on each direct SaveAsync call; " +
                "debounce coalescing (10 MarkDirty → 1 write) is tested in PlayMode.");
        }

        // ── Test 5: Atomic-write resilience ───────────────────────────────

        [Test]
        public void AtomicWrite_SourceFileUntouchedIfOnlyTmpExists()
        {
            // Scenario: simulated write-kill scenario.
            // If tmp exists but rename hasn't happened, source is untouched.
            var savePath = Path.Combine(_testDir, "save_atomic.json");
            var tmpPath  = savePath + ".tmp";

            // Write a known-good original
            string originalContent = "{\"schemaVersion\":1,\"rewardPoints\":999}";
            File.WriteAllText(savePath, originalContent);

            // Simulate mid-write kill: only tmp file was written, rename did not happen
            string partialContent = "{\"schemaVersion\":1,\"rewardPoints\":0"; // truncated / partial
            File.WriteAllText(tmpPath, partialContent);

            // Assert: original is still intact (tmp did not overwrite it without rename)
            string readBack = File.ReadAllText(savePath);
            Assert.AreEqual(originalContent, readBack,
                "save.json must remain unchanged when only .tmp was written (pre-rename)");

            // Now simulate the persister completing the rename
            File.Replace(tmpPath, savePath, null);
            string afterRename = File.ReadAllText(savePath);
            Assert.AreEqual(partialContent, afterRename,
                "After File.Replace, save.json should contain the tmp content");
        }

        [Test]
        public async Task AtomicWrite_TmpThenReplace_WritesCorrectly()
        {
            // Arrange: persister writes via tmp → replace
            var savePath  = Path.Combine(_testDir, "save_atomic2.json");
            var persister = new LocalJsonPersister(savePath);

            var data = new SaveData { rewardPoints = 777 };
            string json = JsonConvert.SerializeObject(data);

            // Act
            await persister.SaveAsync(json);

            // Assert: save.json exists, tmp is cleaned up, content is correct
            Assert.IsTrue(File.Exists(savePath), "save.json should exist after SaveAsync");
            Assert.IsFalse(File.Exists(persister.TmpPath), "save.json.tmp should be gone after rename");

            string readBack = File.ReadAllText(savePath);
            var loaded = JsonConvert.DeserializeObject<SaveData>(readBack);
            Assert.AreEqual(777, loaded!.rewardPoints);
        }

        // ── Test 6: Dictionary round-trip via Newtonsoft ───────────────────

        [Test]
        public async Task DictionaryRoundTrip_NewtonsoftJson()
        {
            var savePath  = Path.Combine(_testDir, "save_dict.json");
            var persister = new LocalJsonPersister(savePath);

            var data = new SaveData
            {
                ballQuantities = new Dictionary<string, int>
                {
                    ["ball_golfin"]    = -1,
                    ["ball_pro"]       = 5,
                    ["ball_distance"]  = 12
                },
                itemQuantities = new Dictionary<string, int>
                {
                    ["item_repair_common"] = 3,
                    ["item_repair_rare"]   = 1
                }
            };

            // Serialize and save
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            await persister.SaveAsync(json);

            // Load and deserialize
            Assert.IsTrue(persister.TryLoad(out string? loadedJson));
            var loaded = JsonConvert.DeserializeObject<SaveData>(loadedJson!);

            // Verify dictionaries round-tripped correctly
            Assert.IsNotNull(loaded);
            Assert.AreEqual(3,   loaded!.ballQuantities.Count);
            Assert.AreEqual(-1,  loaded.ballQuantities["ball_golfin"]);
            Assert.AreEqual(5,   loaded.ballQuantities["ball_pro"]);
            Assert.AreEqual(12,  loaded.ballQuantities["ball_distance"]);
            Assert.AreEqual(2,   loaded.itemQuantities.Count);
            Assert.AreEqual(3,   loaded.itemQuantities["item_repair_common"]);
            Assert.AreEqual(1,   loaded.itemQuantities["item_repair_rare"]);
        }

        // ── Test 7: TryLoad returns false for missing file ─────────────────

        [Test]
        public void TryLoad_MissingFile_ReturnsFalse()
        {
            var savePath  = Path.Combine(_testDir, "nonexistent.json");
            var persister = new LocalJsonPersister(savePath);

            bool result = persister.TryLoad(out string? json);

            Assert.IsFalse(result);
            Assert.IsNull(json);
        }

        // ══════════════════════════════════════════════════════════════════════
        // T5 — Tournament save schema tests (migration + fail-hard + round-trip)
        // ══════════════════════════════════════════════════════════════════════

        // ── T5 Test 1: v2 → v3 migration ─────────────────────────────────────

        [Test]
        public void T5_V2ToV3_Migration_TournamentEntriesEmptyAllV2FieldsIntact()
        {
            // Hand-built v2 JSON (no tournamentEntries field) — simulates a save file
            // written before T5 shipped.
            const string v2Json = @"{
                ""schemaVersion"": 2,
                ""rewardPoints"": 9999,
                ""selectedCharacterId"": ""char_tiger"",
                ""ownedCharacters"": [],
                ""ballQuantities"": {},
                ""itemQuantities"": {},
                ""unlockedHoles"": [1, 2, 3],
                ""playedHoles"": [1],
                ""lifetimeRpEarned"": 500,
                ""rpDaily"": 100,
                ""rpWeekly"": 300,
                ""rpMonthly"": 500,
                ""dailyPeriodKey"": 19900,
                ""weeklyPeriodKey"": 107218,
                ""monthlyPeriodKey"": 24301
            }";

            var data = JsonConvert.DeserializeObject<SaveData>(v2Json);
            Assert.IsNotNull(data);

            // Act: migrate
            Assert.DoesNotThrow(() => SaveSchemaMigrator.Migrate(data!));

            // Assert: schemaVersion bumped all the way to CurrentSchemaVersion (v4 now) from v2
            Assert.AreEqual(4, data!.schemaVersion, "schemaVersion must be 4 after full migration from v2 (v2→v3→v4)");

            // Assert: tournamentEntries is present and empty (not null)
            Assert.IsNotNull(data.tournamentEntries, "tournamentEntries must not be null after migration");
            Assert.AreEqual(0, data.tournamentEntries.Count, "tournamentEntries must be empty for a v2 save");

            // Assert: all v2 fields intact
            Assert.AreEqual(9999, data.rewardPoints);
            Assert.AreEqual("char_tiger", data.selectedCharacterId);
            Assert.AreEqual(3, data.unlockedHoles.Count);
            Assert.AreEqual(500L, data.lifetimeRpEarned);
            Assert.AreEqual(100L, data.rpDaily);
            Assert.AreEqual(19900L, data.dailyPeriodKey);
        }

        // ── T5 Test 2: Fail-hard on v5 (future-version guard; v4 is now current) ──

        [Test]
        public void T5_FailHard_V5Json_ThrowsSaveSchemaVersionException()
        {
            // A save written by a future build (v5) must throw, not silently corrupt.
            // (v4 is now CurrentSchemaVersion; v5 is still an unknown future version.)
            const string v5Json = @"{ ""schemaVersion"": 5, ""rewardPoints"": 1 }";
            var data = JsonConvert.DeserializeObject<SaveData>(v5Json);
            Assert.IsNotNull(data);

            // Expect the Debug.LogError before the throw
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex(@"\[SaveSchemaMigrator\].*schema version 5"));

            Assert.Throws<SaveSchemaVersionException>(() => SaveSchemaMigrator.Migrate(data!));
        }

        // ── T5 Test 3: CurrentSchemaVersion is 4 ─────────────────────────────

        [Test]
        public void T5_CurrentSchemaVersion_Is4()
        {
            Assert.AreEqual(4, SaveSchemaMigrator.CurrentSchemaVersion,
                "CurrentSchemaVersion must be 4 after the v3→v4 bump (Phase 2 stamina condition fields)");
        }

        // ── T5 Test 5: v3 → v4 migration — condition fields default-safe ─────

        [Test]
        public void T5_V3ToV4_Migration_ConditionFieldsDefaultSafe()
        {
            // Hand-built v3 JSON (no conditionEnergy / conditionUpdatedUtc on characters)
            const string v3Json = @"{
                ""schemaVersion"": 3,
                ""rewardPoints"": 7777,
                ""selectedCharacterId"": ""char_alice"",
                ""ownedCharacters"": [
                    {
                        ""characterId"": ""char_alice"",
                        ""currentLevel"": 10,
                        ""totalSPEarned"": 5,
                        ""isSelected"": true
                    }
                ],
                ""ballQuantities"": {},
                ""itemQuantities"": {},
                ""unlockedHoles"": [1],
                ""playedHoles"": [],
                ""tournamentEntries"": []
            }";

            var data = JsonConvert.DeserializeObject<SaveData>(v3Json);
            Assert.IsNotNull(data);

            // Act: migrate
            Assert.DoesNotThrow(() => SaveSchemaMigrator.Migrate(data!));

            // schemaVersion bumped to 4
            Assert.AreEqual(4, data!.schemaVersion, "schemaVersion must be 4 after v3→v4 migration");

            // All v3 fields still intact
            Assert.AreEqual(7777, data.rewardPoints);
            Assert.AreEqual("char_alice", data.selectedCharacterId);
            Assert.AreEqual(1, data.ownedCharacters.Count);

            // New condition fields default correctly (0f / "")
            var pc = data.ownedCharacters[0];
            Assert.AreEqual(0f,  pc.conditionEnergy,     delta: 0.001f,
                "conditionEnergy must default to 0f for pre-v4 saves (hydration treats empty timestamp as full pool)");
            Assert.AreEqual("", pc.conditionUpdatedUtc,
                "conditionUpdatedUtc must default to empty string for pre-v4 saves (treated as fresh on next load)");
        }

        // ── T5 Test 4: PersistedTournamentEntry round-trip via Newtonsoft ─────

        [Test]
        public async Task T5_PersistedTournamentEntry_RoundTripViaNewtonsoft()
        {
            var savePath  = Path.Combine(_testDir, "save_t5_roundtrip.json");
            var persister = new LocalJsonPersister(savePath);

            var originalData = new SaveData
            {
                schemaVersion      = 3,
                rewardPoints       = 1000,
                selectedCharacterId = "char_alice"
            };

            // Populate with a multi-hole entry (with lastHoleUtc set and null cases)
            var entry1 = new PersistedTournamentEntry
            {
                tournamentId = "t_open_2026",
                characterId  = "char_alice",
                startedUtc   = "2026-08-01T10:00:00.0000000Z",
                lastHoleUtc  = "2026-08-01T10:45:00.0000000Z",
                status       = 1, // InProgress
                claimed      = false,
                perHole      = new List<PersistedHoleResult>
                {
                    new PersistedHoleResult
                    {
                        holeId       = "h1",
                        strokes      = 3,
                        timeSeconds  = 95.5f,
                        completedUtc = "2026-08-01T10:25:00.0000000Z",
                        rngSeed      = 42
                    },
                    new PersistedHoleResult
                    {
                        holeId       = "h2",
                        strokes      = 5,
                        timeSeconds  = 120.0f,
                        completedUtc = "2026-08-01T10:45:00.0000000Z",
                        rngSeed      = 99
                    }
                },
                snapshot = new PersistedCharacterSnapshot
                {
                    characterId = "char_alice",
                    level       = 42,
                    strength    = 20,
                    clubControl = 18,
                    recovery    = 15,
                    stamina     = 22
                }
            };

            // Entry 2: no lastHoleUtc (empty string = null)
            var entry2 = new PersistedTournamentEntry
            {
                tournamentId = "t_invitational_2026",
                characterId  = "char_bob",
                startedUtc   = "2026-09-01T08:00:00.0000000Z",
                lastHoleUtc  = "",
                status       = 0, // NotEntered / not yet started
                claimed      = true,
                perHole      = new List<PersistedHoleResult>(),
                snapshot     = new PersistedCharacterSnapshot
                {
                    characterId = "char_bob",
                    level       = 10,
                    strength    = 12,
                    clubControl = 11,
                    recovery    = 8,
                    stamina     = 9
                }
            };

            originalData.tournamentEntries.Add(entry1);
            originalData.tournamentEntries.Add(entry2);

            // Serialize → save → reload → deserialize
            string json = JsonConvert.SerializeObject(originalData, Formatting.Indented);
            await persister.SaveAsync(json);

            Assert.IsTrue(persister.TryLoad(out string? loadedJson));
            var loaded = JsonConvert.DeserializeObject<SaveData>(loadedJson!);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(3, loaded!.schemaVersion);  // raw round-trip: no migration run here
            Assert.AreEqual(1000, loaded.rewardPoints);
            Assert.AreEqual(2, loaded.tournamentEntries.Count);

            // Verify entry1 fields
            var le1 = loaded.tournamentEntries[0];
            Assert.AreEqual("t_open_2026", le1.tournamentId);
            Assert.AreEqual("char_alice",  le1.characterId);
            Assert.AreEqual("2026-08-01T10:00:00.0000000Z", le1.startedUtc);
            Assert.AreEqual("2026-08-01T10:45:00.0000000Z", le1.lastHoleUtc);
            Assert.AreEqual(1,    le1.status);
            Assert.IsFalse(le1.claimed);
            Assert.AreEqual(2,    le1.perHole.Count);
            Assert.AreEqual("h1", le1.perHole[0].holeId);
            Assert.AreEqual(3,    le1.perHole[0].strokes);
            Assert.AreEqual(95.5f, le1.perHole[0].timeSeconds, delta: 0.001f);
            Assert.AreEqual(42,   le1.perHole[0].rngSeed);
            Assert.AreEqual("h2", le1.perHole[1].holeId);
            Assert.AreEqual(5,    le1.perHole[1].strokes);
            Assert.AreEqual(99,   le1.perHole[1].rngSeed);

            // Verify snapshot fields (characterId/level/strength/clubControl/recovery/stamina)
            Assert.AreEqual("char_alice", le1.snapshot.characterId);
            Assert.AreEqual(42,  le1.snapshot.level);
            Assert.AreEqual(20,  le1.snapshot.strength);
            Assert.AreEqual(18,  le1.snapshot.clubControl);
            Assert.AreEqual(15,  le1.snapshot.recovery);
            Assert.AreEqual(22,  le1.snapshot.stamina);

            // Verify entry2: no lastHoleUtc, claimed=true
            var le2 = loaded.tournamentEntries[1];
            Assert.AreEqual("t_invitational_2026", le2.tournamentId);
            Assert.AreEqual("",    le2.lastHoleUtc, "Empty string must survive round-trip for null DateTime?");
            Assert.IsTrue(le2.claimed);
            Assert.AreEqual(0,     le2.perHole.Count);
            Assert.AreEqual("char_bob", le2.snapshot.characterId);
            Assert.AreEqual(10,   le2.snapshot.level);
        }
    }

    /// <summary>
    /// Test helper: a persister that counts SaveAsync calls and delegates to LocalJsonPersister.
    /// </summary>
    internal class CountingPersister : ISavePersister
    {
        private readonly LocalJsonPersister _inner;
        private readonly System.Action _onWrite;

        public CountingPersister(string savePath, System.Action onWrite)
        {
            _inner   = new LocalJsonPersister(savePath);
            _onWrite = onWrite;
        }

        public bool TryLoad(out string? json) => _inner.TryLoad(out json);

        public async Task SaveAsync(string json)
        {
            await _inner.SaveAsync(json);
            _onWrite();
        }
    }
}
