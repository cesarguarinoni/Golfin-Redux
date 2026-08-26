#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Golfin.Save
{
    /// <summary>
    /// Singleton MonoBehaviour that owns the canonical SaveData instance.
    /// Script Execution Order: -100 (before any manager that reads SaveData).
    ///
    /// Responsibilities:
    ///   - Load save on Awake (with schema migration + PlayerPrefs one-time migration)
    ///   - Provide Data property for read-through facades
    ///   - Accept dirty notifications via MarkDirty() — debounced 250ms writes
    ///   - Flush pending write on app-pause
    ///   - Fire OnSaved after every successful disk write
    ///
    /// Q-LOCKS (§4):
    ///   Q1 Single slot v1.
    ///   Q2 Fail-hard if schemaVersion in file > code.
    ///   Q3 Debounced 250ms tail writes on every MarkDirty().
    /// </summary>
    public class SaveDataHost : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        public static SaveDataHost Instance { get; private set; } = null!;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fires after every successful disk write (post-File.Replace).</summary>
        public event Action? OnSaved;

        // ── State ─────────────────────────────────────────────────────────────

        private SaveData _data = SaveData.CreateFresh();
        private ISavePersister _persister = null!;
        private Coroutine? _debounceCoroutine;
        private bool _pendingWrite;

        private const float DebounceSeconds = 0.25f; // Q3: 250ms tail debounce
        private const string LegacyPrefsKey = "GOLFIN_REWARD_POINTS";

        // ── Public accessors ──────────────────────────────────────────────────

        /// <summary>The live SaveData. Managers read/write fields here.</summary>
        public SaveData Data => _data;

        /// <summary>
        /// True once <see cref="LoadData"/> has finished, i.e. <see cref="Data"/> is the persisted
        /// save (or a deliberate fresh one) rather than the field initialiser.
        ///
        /// <para>
        /// THE RUNTIME ASSERT FOR "SaveDataHost RAN FIRST"
        /// (content_kill_switch_and_order §2). <c>Instance != null</c> only proves Awake STARTED —
        /// it is assigned before <c>LoadData()</c> — and every manager that reads the save is
        /// ordered after this one by an <c>executionOrder:</c> field committed in a <c>.cs.meta</c>
        /// that nothing re-asserts except this class's own [InitializeOnLoad] hook. The same shape
        /// as <c>ClubDatabaseCSV.IsLoaded</c> and <c>CharacterDatabaseCSV.IsLoaded</c>, and it
        /// exists for the same reason: a reader must be able to CHECK the invariant instead of
        /// trusting a comment about it.
        /// </para>
        /// </summary>
        public bool IsLoaded { get; private set; }

        /// <summary>
        /// Call after any in-memory mutation to schedule a debounced disk write.
        /// Multiple calls within 250ms collapse to one write.
        /// </summary>
        public void MarkDirty()
        {
            _pendingWrite = true;
            if (_debounceCoroutine != null)
                StopCoroutine(_debounceCoroutine);
            _debounceCoroutine = StartCoroutine(DebounceWrite());
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _persister = new LocalJsonPersister();
            LoadData();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null!;
        }

        // ── App-pause flush hook (Q3 requirement) ─────────────────────────────

        private void OnApplicationPause(bool paused)
        {
            if (paused && _pendingWrite)
            {
                // Cancel any running debounce coroutine (coroutines stop at pause anyway)
                if (_debounceCoroutine != null)
                {
                    StopCoroutine(_debounceCoroutine);
                    _debounceCoroutine = null;
                }

                // Flush synchronously — acceptable at app-pause (no frame budget).
                // FlushNow + LocalJsonPersister.SaveAsync both use ConfigureAwait(false) so their
                // continuations run on thread-pool threads instead of the blocked main-thread context;
                // this breaks the sync-over-async deadlock that would otherwise occur under
                // UnitySynchronizationContext.
                FlushNow().GetAwaiter().GetResult();
            }
        }

        // ── Load / Save ───────────────────────────────────────────────────────

        /// <summary>
        /// Read the save through the current persister and raise <see cref="IsLoaded"/>.
        /// <para>
        /// The flag is set HERE rather than inside <see cref="LoadDataCore"/>, which returns early
        /// on the happy path — a flag that only became true on the fallback branch would be worse
        /// than no flag. Setting it in this wrapper also means <see cref="ReloadFromDisk"/> marks
        /// the host loaded, which is what a test that fakes the boot (EditMode never calls Awake)
        /// needs in order to be a faithful stand-in for one.
        /// </para>
        /// </summary>
        private void LoadData()
        {
            LoadDataCore();
            IsLoaded = true;
        }

        private void LoadDataCore()
        {
            if (_persister.TryLoad(out string? json) && json != null)
            {
                try
                {
                    var loaded = JsonConvert.DeserializeObject<SaveData>(json);
                    if (loaded != null)
                    {
                        // Q2: fail-hard if schema version in file > code version
                        SaveSchemaMigrator.Migrate(loaded);
                        _data = loaded;
                        Debug.Log($"[SaveDataHost] Loaded save (schema v{_data.schemaVersion}, " +
                                  $"rp={_data.rewardPoints}, holes={_data.unlockedHoles.Count})");
                        return;
                    }
                }
                catch (SaveSchemaVersionException ex)
                {
                    Debug.LogError($"[SaveDataHost] {ex.Message}. Using default data.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SaveDataHost] Failed to parse save file: {ex.Message}. Using default data.");
                }
            }

            // No save file yet — try one-time PlayerPrefs migration.
            // CreateFresh (not `new SaveData()`) stamps the CURRENT schema version: a fresh save
            // written to disk at the class default (v2) would come back on the next boot looking
            // like a legacy save and run every migration against a brand-new player.
            _data = SaveData.CreateFresh();
            MigrateFromPlayerPrefs();

            // Seed default: Hole 1 unlocked
            if (_data.unlockedHoles.Count == 0)
                _data.unlockedHoles.Add(1);

            Debug.Log($"[SaveDataHost] Fresh save data (rp={_data.rewardPoints}, " +
                      $"legacyMigrated={!PlayerPrefs.HasKey(LegacyPrefsKey) || _data.rewardPoints > 0})");
        }

        /// <summary>
        /// One-time migration: if save.json is absent but GOLFIN_REWARD_POINTS exists in PlayerPrefs,
        /// hydrate rewardPoints from it and immediately write save.json.
        /// </summary>
        private void MigrateFromPlayerPrefs()
        {
            if (PlayerPrefs.HasKey(LegacyPrefsKey))
            {
                int legacyPoints = PlayerPrefs.GetInt(LegacyPrefsKey);
                _data.rewardPoints = legacyPoints;
                Debug.Log($"[SaveDataHost] Migrated legacy PlayerPrefs rewardPoints={legacyPoints} to SaveData.");
                // Fire-and-forget: immediate write after migration
                _ = FlushNow();
            }
        }

        private IEnumerator DebounceWrite()
        {
            yield return new WaitForSecondsRealtime(DebounceSeconds);
            _debounceCoroutine = null;
            if (_pendingWrite)
                yield return StartCoroutine(WriteAsync());
        }

        private IEnumerator WriteAsync()
        {
            Task flushTask = FlushNow();
            yield return new WaitUntil(() => flushTask.IsCompleted);

            if (flushTask.IsFaulted && flushTask.Exception != null)
                Debug.LogError($"[SaveDataHost] Write failed: {flushTask.Exception.InnerException?.Message}");
        }

        /// <summary>
        /// Flush in-memory SaveData to disk. Returns a Task that resolves after the write completes.
        /// Fires OnSaved on success.
        ///
        /// ConfigureAwait(false) on the SaveAsync await is REQUIRED:
        ///   OnApplicationPause calls this method via .GetAwaiter().GetResult() (sync-over-async).
        ///   UnitySynchronizationContext is installed on Unity's main thread. Without ConfigureAwait(false),
        ///   SaveAsync's continuation is posted back onto the main-thread context which .GetResult() is
        ///   blocking → neither the File.WriteAllTextAsync continuation nor the File.Replace step ever
        ///   execute → SaveAsync never completes → deadlock (ANR on Android, watchdog kill on iOS).
        ///   ConfigureAwait(false) makes SaveAsync's continuation run on a thread-pool thread, allowing
        ///   it to complete while the main thread waits in .GetResult().
        ///
        ///   Note: OnSaved?.Invoke() and the Debug.Log below run on the thread-pool continuation after
        ///   ConfigureAwait(false). This is safe because both are null-conditional or Debug-only calls;
        ///   they do not touch Unity scene objects.
        /// </summary>
        public async Task FlushNow()
        {
            if (!_pendingWrite) return;

            string json = JsonConvert.SerializeObject(_data, Formatting.Indented);
            try
            {
                // ConfigureAwait(false): continuation must NOT marshal back to UnitySynchronizationContext
                // (prevents deadlock when called via .GetAwaiter().GetResult() from OnApplicationPause)
                await _persister.SaveAsync(json).ConfigureAwait(false);
                _pendingWrite = false;
                OnSaved?.Invoke();
                Debug.Log("[SaveDataHost] Saved to disk.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveDataHost] SaveAsync failed: {ex.Message}");
            }
        }

        // ── Injection (for tests) ─────────────────────────────────────────────

        /// <summary>
        /// Inject a custom persister. Called by tests or Bootstrap for alternative persisters.
        /// Must be called before Awake completes or the default LocalJsonPersister is already set.
        /// </summary>
        public void SetPersister(ISavePersister persister)
        {
            _persister = persister;
        }

        /// <summary>
        /// Force-load from the current persister. Used in durability tests to simulate restart, and
        /// by EditMode harnesses that stand in for a boot Awake never runs — it is the call that
        /// makes <see cref="IsLoaded"/> true, so a hand-built host is in the same state a booted one
        /// would be.
        /// </summary>
        public void ReloadFromDisk()
        {
            LoadData();
        }
    }
}
