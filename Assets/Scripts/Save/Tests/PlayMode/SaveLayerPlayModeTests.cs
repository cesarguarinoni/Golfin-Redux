#nullable enable
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Golfin.Save.PlayMode.Tests
{
    /// <summary>
    /// PlayMode tests for SaveDataHost MonoBehaviour behaviour.
    ///
    /// These tests require PlayMode (MonoBehaviour lifecycle + coroutine runtime).
    ///
    /// SPEC §Definition of done:
    ///   - OnSaved event fires after every disk write (post-File.Replace)
    ///   - Debounced writes (250ms tail): 10 MarkDirty calls → 1 write
    /// </summary>
    [TestFixture]
    public class SaveLayerPlayModeTests
    {
        private string _testDir = null!;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"GolfinPMTest_{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }

        // ── Test A: OnSaved fires after a real disk write ─────────────────────
        //
        // Instantiates a SaveDataHost, injects a CountingPersisterPM, marks dirty,
        // calls FlushNow(), and asserts that OnSaved fired exactly once after
        // SaveAsync returned (post-File.Replace).
        //
        // Per SPEC §Definition of done: "OnSaved event fires after every disk write
        // (post-File.Replace)."

        [UnityTest]
        public IEnumerator OnSaved_Fires_AfterRealDiskWrite()
        {
            // Arrange: create a SaveDataHost in the test scene
            var go   = new GameObject("TestSaveDataHost_OnSaved");
            var host = go.AddComponent<SaveDataHost>();
            // Awake fires here, sets up default LocalJsonPersister and loads data.
            // We immediately inject a test persister that writes to our temp dir.

            var savePath         = Path.Combine(_testDir, "save_onsaved_test.json");
            int persistWriteCount = 0;
            var spy              = new SpyPersister(savePath, onWrite: () => persistWriteCount++);
            host.SetPersister(spy);

            int onSavedFiredCount = 0;
            host.OnSaved += () => onSavedFiredCount++;

            // Act: MarkDirty() sets _pendingWrite=true and starts the 250ms debounce coroutine.
            // Calling FlushNow() immediately writes without waiting for the debounce to expire.
            // After FlushNow() sets _pendingWrite=false, the debounce coroutine will exit cleanly.
            host.MarkDirty();
            Task flushTask = host.FlushNow();
            yield return new WaitUntil(() => flushTask.IsCompleted);

            // Assert: OnSaved fired exactly once, persister.SaveAsync was called exactly once.
            Assert.AreEqual(1, onSavedFiredCount,
                "OnSaved must fire exactly once after SaveDataHost.FlushNow() completes a real disk write.");
            Assert.AreEqual(1, persistWriteCount,
                "SpyPersister.SaveAsync must be called exactly once by FlushNow().");
            Assert.IsTrue(File.Exists(savePath),
                "save.json must exist on disk after FlushNow() completes.");

            // Cleanup
            Object.Destroy(go);
            yield return null; // let OnDestroy run
        }

        // ── Test C: App-pause flush path — sync-over-async must not deadlock ────
        //
        // This is the regression test for ARCHITECT_REVIEW FAIL 1.
        //
        // SaveDataHost.OnApplicationPause(true) calls FlushNow().GetAwaiter().GetResult() on
        // Unity's main thread (which carries UnitySynchronizationContext). Without
        // ConfigureAwait(false) on the flush path, each continuation is posted back to the
        // main-thread message queue which .GetResult() is blocking → deadlock. The fix is
        // ConfigureAwait(false) on both SaveDataHost.FlushNow() and LocalJsonPersister.SaveAsync().
        //
        // How this test catches the regression:
        //   - It calls FlushNow().GetAwaiter().GetResult() from inside a [UnityTest] coroutine,
        //     which runs on Unity's main thread (same UnitySynchronizationContext that
        //     OnApplicationPause has). If ConfigureAwait(false) is absent, .GetResult() deadlocks
        //     and the test runner hangs forever (itself the failure signal).
        //   - After the call returns, we assert the file was written.
        //
        // IMPORTANT: This test has no explicit timeout in C# — Unity's [UnityTest] framework
        // will surface a hang as a test timeout in the editor. A passing result proves the
        // sync-block completes without deadlocking. A regression (removing ConfigureAwait(false))
        // will cause this test to never return.

        [UnityTest]
        public IEnumerator AppPauseFlush_SyncOverAsync_CompletesWithoutDeadlock()
        {
            // Arrange: create a real SaveDataHost with a real LocalJsonPersister.
            // Using LocalJsonPersister directly (not SpyPersister) because:
            //   a) This is exactly the production code path OnApplicationPause uses.
            //   b) SpyPersister.SaveAsync does not have ConfigureAwait(false) on its
            //      internal await; if SpyPersister were used here, it would re-introduce
            //      the exact deadlock we're testing against (its continuation would post
            //      back to UnitySynchronizationContext via the missing ConfigureAwait(false),
            //      even though LocalJsonPersister + FlushNow both have ConfigureAwait(false)).
            //      The deadlock regression test must only exercise code that actually uses
            //      ConfigureAwait(false) — i.e. the production LocalJsonPersister path.
            var go   = new GameObject("TestSaveDataHost_AppPause");
            var host = go.AddComponent<SaveDataHost>();
            // Awake fires; default LocalJsonPersister is created (pointing to Application.persistentDataPath).
            // We inject a LocalJsonPersister that points to our temp dir for isolation.
            var savePath    = Path.Combine(_testDir, "save_app_pause_test.json");
            var persister   = new LocalJsonPersister(savePath);
            host.SetPersister(persister);

            // Mark dirty so FlushNow has work to do (identical condition as OnApplicationPause)
            host.MarkDirty();

            // Act: simulate exactly what OnApplicationPause(true) does —
            // call FlushNow().GetAwaiter().GetResult() synchronously on the main thread.
            // This BLOCKS the main thread until FlushNow's Task completes.
            // With ConfigureAwait(false) in place on both FlushNow and LocalJsonPersister.SaveAsync:
            //   - SaveAsync's continuation runs on a thread-pool thread (not UnitySynchronizationContext)
            //   - File.WriteAllTextAsync + File.Move complete on the thread pool
            //   - FlushNow's Task completes → .GetResult() unblocks the main thread
            // Without ConfigureAwait(false): each await posts continuation back to the blocked main
            // thread → File.Replace step never executes → deadlock (ANR / watchdog kill on mobile).
            //
            // If this line never returns, the test hangs (= deadlock regression caught).
            host.FlushNow().GetAwaiter().GetResult();

            // Assert: FlushNow returned (no deadlock), and save.json was written to disk.
            Assert.IsTrue(File.Exists(savePath),
                "save.json must exist on disk after the synchronous app-pause flush " +
                "(FlushNow().GetAwaiter().GetResult()) completes without deadlocking.");

            // Cleanup
            Object.Destroy(go);
            yield return null; // let OnDestroy run
        }

        // ── Test B: Debounce coalesces 10 rapid MarkDirty calls into 1 write ──
        //
        // Instantiates a SaveDataHost, injects a SpyPersister, calls MarkDirty() 10
        // times within a single frame (~16ms, well inside the 250ms debounce tail),
        // then yields 400ms and asserts the persister was called exactly once.
        //
        // Per SPEC §Definition of done: "Debounced writes (250ms tail) — verified by
        // EditMode test that fires 10 OnChanged events in 50ms and asserts 1 write."
        // (Implemented here as a PlayMode test because the debounce requires
        // MonoBehaviour coroutine + real-time elapsed; the SPEC allows [UnityTest].)

        [UnityTest]
        public IEnumerator Debounce_TenMarkDirtyCallsWithinOneFrame_CollapseToOneWrite()
        {
            // Arrange
            var go   = new GameObject("TestSaveDataHost_Debounce");
            var host = go.AddComponent<SaveDataHost>();

            var savePath     = Path.Combine(_testDir, "save_debounce_test.json");
            int writeCount   = 0;
            var spy          = new SpyPersister(savePath, onWrite: () => writeCount++);
            host.SetPersister(spy);

            // Act: fire 10 MarkDirty() calls back-to-back in the same frame.
            // Each call restarts the 250ms debounce coroutine; only the last one survives.
            // All 10 calls complete within ~1ms (single frame), well inside the 50ms window
            // the SPEC cites for the coalescing scenario.
            for (int i = 0; i < 10; i++)
            {
                host.MarkDirty();
            }

            // Wait 400ms (> 250ms debounce tail) for the debounce coroutine to fire.
            yield return new WaitForSecondsRealtime(0.4f);

            // Assert: exactly 1 write despite 10 MarkDirty calls.
            Assert.AreEqual(1, writeCount,
                "After 10 MarkDirty calls within one frame, the 250ms debounce must coalesce " +
                "them into exactly 1 disk write.");

            // Cleanup
            Object.Destroy(go);
            yield return null;
        }
    }

    /// <summary>
    /// Test spy: a minimal ISavePersister that counts SaveAsync calls and writes
    /// through to a real LocalJsonPersister so OnSaved fire conditions are authentic.
    /// </summary>
    internal class SpyPersister : ISavePersister
    {
        private readonly LocalJsonPersister _inner;
        private readonly System.Action _onWrite;

        public SpyPersister(string savePath, System.Action onWrite)
        {
            _inner   = new LocalJsonPersister(savePath);
            _onWrite = onWrite;
        }

        public bool TryLoad(out string? json) => _inner.TryLoad(out json);

        public async Task SaveAsync(string json)
        {
            // ConfigureAwait(false): prevents deadlock when SaveAsync is called from a context
            // that blocks on the result (e.g. .GetAwaiter().GetResult() from the main thread).
            await _inner.SaveAsync(json).ConfigureAwait(false);
            _onWrite(); // increments counter AFTER the real disk write completes
        }
    }
}
