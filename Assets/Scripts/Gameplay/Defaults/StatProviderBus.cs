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
        /// Called by ShotController.GetStatBundle() every shot.
        /// </summary>
        public static StatBundle Resolve(bool isPutt)
        {
            var live = Resolver?.Invoke(isPutt);
            if (live.HasValue) return live.Value;
            return isPutt
                ? DefaultStatProvider.BuildPuttBundle()
                : DefaultStatProvider.BuildSwingBundle();
        }
    }
}
