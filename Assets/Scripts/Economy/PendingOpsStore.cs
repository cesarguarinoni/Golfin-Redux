// Order: reward_points_backend Slice 1 — where the pending-ops queue lives on disk.
using System;
using System.IO;
using UnityEngine;

namespace Golfin.Economy
{
    /// <summary>Persistence seam for <see cref="PendingOpsQueue"/>. Mirrors the <c>ISavePersister</c>
    /// pattern already used by <c>Golfin.Save</c> so tests can swap in memory or a temp file.</summary>
    public interface IPendingOpsStore
    {
        /// <summary>Stored JSON, or null when nothing has been written yet.</summary>
        string Read();

        void Write(string json);

        void Delete();
    }

    /// <summary>
    /// The shipping store: one JSON file in <c>Application.persistentDataPath</c> (SPEC §4 Slice 1).
    ///
    /// Written atomically — temp file, flush, then <see cref="File.Replace(string,string,string)"/> —
    /// because this queue is the ONLY record of an earn the player has already been shown locally.
    /// A torn write mid-save would lose points that the server has not been told about yet.
    /// </summary>
    public sealed class FilePendingOpsStore : IPendingOpsStore
    {
        public const string DefaultFileName = "points_pending_ops.json";

        private readonly string _path;

        public FilePendingOpsStore(string path)
        {
            _path = path;
        }

        /// <summary>Default location: <c>&lt;persistentDataPath&gt;/points_pending_ops.json</c>.</summary>
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, DefaultFileName);

        /// <summary>Absolute path this store reads and writes.</summary>
        public string FilePath => _path;

        public string Read()
        {
            try
            {
                return File.Exists(_path) ? File.ReadAllText(_path) : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PendingOpsQueue] Could not read '{_path}': {ex.Message}");
                return null;
            }
        }

        public void Write(string json)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string tmp = _path + ".tmp";
                File.WriteAllText(tmp, json);

                if (File.Exists(_path)) File.Replace(tmp, _path, null);
                else File.Move(tmp, _path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PendingOpsQueue] Could not write '{_path}': {ex.Message}");
            }
        }

        public void Delete()
        {
            try { if (File.Exists(_path)) File.Delete(_path); }
            catch (Exception ex) { Debug.LogWarning($"[PendingOpsQueue] Could not delete '{_path}': {ex.Message}"); }
        }
    }

    /// <summary>In-memory store for tests that do not care about the disk round-trip.</summary>
    public sealed class InMemoryPendingOpsStore : IPendingOpsStore
    {
        public string Json;
        public int WriteCount;

        public string Read() => Json;

        public void Write(string json) { Json = json; WriteCount++; }

        public void Delete() { Json = null; }
    }
}
