#nullable enable
using System;

namespace Golfin.UI.Rankings
{
    /// <summary>
    /// Abstraction over the authoritative UTC clock.
    /// Production: NetworkTimeProvider (HTTP Date header + device offset).
    /// Tests: inject a fixed/stub implementation.
    /// </summary>
    public interface ITimeProvider
    {
        /// <summary>Current authoritative UTC time.</summary>
        DateTime UtcNow { get; }

        /// <summary>True when the time was fetched from the network; false when falling back to device clock.</summary>
        bool IsAuthoritative { get; }
    }
}
