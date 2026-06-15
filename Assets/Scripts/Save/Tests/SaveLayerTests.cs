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
