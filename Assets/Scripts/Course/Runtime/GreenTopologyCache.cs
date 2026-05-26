// Phase 1 of Docs/Specs/Queued/green_topology_and_pin_authoring/SPEC.md.
// Process-lifetime cache for GreenTopology instances keyed by hole number.

using System.Collections.Generic;

namespace Golfin.Course.Runtime
{
    /// <summary>
    /// Process-lifetime cache layered over <see cref="GreenTopology.LoadFromResources"/>. Decode of
    /// the base64 slope grid happens once per hole per process; subsequent <see cref="GetForHole"/>
    /// calls return the cached instance without touching Resources.
    ///
    /// <para>
    /// <b>Negative caching.</b> Holes that <see cref="GreenTopology.LoadFromResources"/> reports as
    /// missing (no <c>green.json</c> on disk) are remembered in a separate set so per-step physics
    /// queries don't repeatedly hit <c>Resources.Load</c>. Phase 6 (BallSimulation slope sampling)
    /// asks per step; this matters.
    /// </para>
    ///
    /// <para>
    /// <b>Consumers (per SPEC):</b>
    /// <list type="bullet">
    ///   <item>Phase 2 — <c>GreenTopologyEditor.Save</c> calls <see cref="Invalidate"/> after a
    ///         re-bake so subsequent reads pick up the new grid.</item>
    ///   <item>Phase 6 — <c>BallSimulation.InitializeForHole</c> calls <see cref="GetForHole"/> at
    ///         simulation start; <c>HoleSessionDriver.OnHoleUnloaded</c> calls
    ///         <see cref="Invalidate"/> on the just-finished hole to free memory.</item>
    ///   <item>Phase 7 — <c>PutterGreenReader</c> (or successor) reads the slope grid for arrow
    ///         rendering after <c>HoleContext.OnChanged</c>.</item>
    ///   <item>Phase 8 — <c>PhysicsLabController</c> pulls <see cref="GreenTopology.GetDefaultPin"/>
    ///         via <see cref="GetForHole"/> during hole-load pin resolution.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// Not thread-safe. All current and planned consumers run on the Unity main thread. If a
    /// future async path needs it, wrap accessors in a lock.
    /// </para>
    /// </summary>
    public static class GreenTopologyCache
    {
        private static readonly Dictionary<int, GreenTopology> _cache = new();

        // Holes confirmed to have no green.json on disk. Avoids repeated Resources.Load misses.
        private static readonly HashSet<int> _missingHoles = new();

        /// <summary>
        /// Returns the cached <see cref="GreenTopology"/> for the hole, loading it from Resources
        /// on first request. Returns <c>null</c> when no <c>green.json</c> exists for the hole;
        /// subsequent calls for the same missing hole return <c>null</c> without re-attempting Load.
        /// </summary>
        public static GreenTopology GetForHole(int holeNumber)
        {
            if (_cache.TryGetValue(holeNumber, out var cached))
                return cached;
            if (_missingHoles.Contains(holeNumber))
                return null;

            var loaded = GreenTopology.LoadFromResources(holeNumber);
            if (loaded == null)
            {
                _missingHoles.Add(holeNumber);
                return null;
            }

            _cache[holeNumber] = loaded;
            return loaded;
        }

        /// <summary>
        /// Drops the cached entry for a single hole (both the loaded instance and the missing-file
        /// negative cache). Next <see cref="GetForHole"/> call for this hole re-loads from disk.
        /// </summary>
        public static void Invalidate(int holeNumber)
        {
            _cache.Remove(holeNumber);
            _missingHoles.Remove(holeNumber);
        }

        /// <summary>
        /// Clears every cached hole and every negative-cache entry. Call from test teardown or
        /// when bulk-editing many holes (e.g. Phase 5 heightmap reconciliation across all 18).
        /// </summary>
        public static void InvalidateAll()
        {
            _cache.Clear();
            _missingHoles.Clear();
        }
    }
}
