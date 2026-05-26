using System;
using Golfin.Physics.Stats;

namespace Golfin.Gameplay.Defaults
{
    /// <summary>
    /// Static bus that lets Assembly-CSharp register a live stat resolver. Named-asmdef
    /// code (e.g. ShotController) calls Resolve(isPutt) which forwards to the registered
    /// resolver, or falls through to DefaultStatProvider when nothing is registered.
    /// Matches the HoleContext static-bus precedent for cross-asmdef data flow.
    /// </summary>
    public static class StatProviderBus
    {
        /// <summary>
        /// Set by LiveStatProviderHost (Assembly-CSharp) on Awake. Returns null when
        /// the live state is incomplete (no character / club / ball selected), which
        /// causes Resolve() to fall through to the default bundle.
        /// </summary>
        public static Func<bool, StatBundle?> Resolver;

        /// <summary>
        /// Tracks the current lab club index (0=Driver, 1=Iron7, 2=Wedge, 3=Putter).
        /// Set by PhysicsLabController.SetClub() so the FALLBACK path in Resolve()
        /// can pick the correct DefaultStatProvider club bundle.
        ///
        /// NOTE stat_to_physics_mapping_audit Q3 (2026-05-25): this is the cross-asmdef
        /// bridge for the FALLBACK club-aware fix. PhysicsLabController (Golfin.Physics.Viewer)
        /// cannot be referenced from ShotController (Golfin.Gameplay.Input) due to asmdef
        /// build order — StatProviderBus (Golfin.Gameplay.Defaults, autoReferenced=true) is
        /// the correct location for cross-cutting shared state.
        /// </summary>
        public static int CurrentLabClubIndex { get; private set; }

        /// <summary>
        /// Called by PhysicsLabController.SetClub(index) to keep the bus in sync with
        /// the active lab club selection. Thread-safe only from the Unity main thread.
        /// </summary>
        public static void SetCurrentLabClubIndex(int index)
        {
            CurrentLabClubIndex = index;
        }

        /// <summary>
        /// Called by ShotController.GetStatBundle() every shot.
        /// Falls through to DefaultStatProvider when the Resolver is null or returns null.
        /// In the FALLBACK path, uses CurrentLabClubIndex to pick the per-club default bundle.
        /// </summary>
        public static StatBundle Resolve(bool isPutt)
        {
            var live = Resolver?.Invoke(isPutt);
            if (live.HasValue) return live.Value;
            return isPutt
                ? DefaultStatProvider.BuildPuttBundle()
                : DefaultStatProvider.BuildSwingBundle(CurrentLabClubIndex);
        }
    }
}
