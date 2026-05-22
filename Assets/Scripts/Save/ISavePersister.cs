#nullable enable
using System.Threading.Tasks;

namespace Golfin.Save
{
    /// <summary>
    /// Abstraction over the persistence back-end.
    /// LocalJsonPersister is the default (file system).
    /// A future CloudSyncPersister swaps in here with zero change to SaveDataHost or consumers.
    /// </summary>
    public interface ISavePersister
    {
        /// <summary>
        /// Attempt to load a JSON string from the storage back-end.
        /// Returns false and sets json to null if no save file exists yet.
        /// </summary>
        bool TryLoad(out string? json);

        /// <summary>
        /// Persist the given JSON string to the storage back-end.
        /// Must be implemented with the atomic temp-file-then-rename pattern.
        /// Never blocks the main thread (all disk I/O is async).
        /// </summary>
        Task SaveAsync(string json);
    }
}
