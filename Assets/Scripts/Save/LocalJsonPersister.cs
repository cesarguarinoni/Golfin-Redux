#nullable enable
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Golfin.Save
{
    /// <summary>
    /// File-system persister. Writes to Application.persistentDataPath/save.json.
    ///
    /// ATOMIC WRITES (non-negotiable per §5.1):
    ///   1. WriteAllTextAsync → save.json.tmp
    ///   2. File.Replace(tmp, dest, null)  — atomic rename on both POSIX and Windows
    ///   3. If dest doesn't exist yet: File.Move(tmp, dest)
    ///
    /// ASYNC I/O (§5.2):
    ///   All disk writes use File.WriteAllTextAsync. Zero synchronous File.WriteAllText calls.
    /// </summary>
    public class LocalJsonPersister : ISavePersister
    {
        private readonly string _savePath;
        private readonly string _tmpPath;

        public LocalJsonPersister()
        {
            _savePath = Path.Combine(Application.persistentDataPath, "save.json");
            _tmpPath  = _savePath + ".tmp";
        }

        /// <summary>Constructor with explicit path (used by EditMode tests).</summary>
        public LocalJsonPersister(string savePath)
        {
            _savePath = savePath;
            _tmpPath  = savePath + ".tmp";
        }

        public bool TryLoad(out string? json)
        {
            if (!File.Exists(_savePath))
            {
                json = null;
                return false;
            }

            try
            {
                json = File.ReadAllText(_savePath);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LocalJsonPersister] Failed to read save file: {ex.Message}");
                json = null;
                return false;
            }
        }

        /// <summary>
        /// Atomic async write. Never blocks main thread.
        /// Step 1: write to tmp. Step 2: atomically replace dest with tmp.
        /// </summary>
        public async Task SaveAsync(string json)
        {
            // Step 1: write to temp file (async, never blocks main thread)
            await File.WriteAllTextAsync(_tmpPath, json);

            // Step 2: atomic rename
            if (File.Exists(_savePath))
            {
                // File.Replace: atomic on POSIX (rename syscall) and Windows (MoveFileExW)
                File.Replace(_tmpPath, _savePath, destinationBackupFileName: null);
            }
            else
            {
                // First save — destination doesn't exist yet; File.Move is atomic for same filesystem
                File.Move(_tmpPath, _savePath);
            }
        }

        /// <summary>Exposed for tests: the path this persister writes to.</summary>
        public string SavePath => _savePath;

        /// <summary>Exposed for tests: the tmp path used during atomic writes.</summary>
        public string TmpPath => _tmpPath;
    }
}
