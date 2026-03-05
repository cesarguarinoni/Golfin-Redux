#nullable enable
using UnityEngine;

namespace Golfin.Roster
{
    /// <summary>
    /// Abstract base class for different SP allocation strategies
    /// Allows swapping between Manual allocation (current) and Automatic allocation (future)
    /// without changing the rest of the codebase
    /// </summary>
    public abstract class StatAllocationStrategy
    {
        /// <summary>
        /// Allocate earned SP to character stats
        /// Called after player confirms allocation in modal
        /// </summary>
        /// <param name="characterId">Which character to allocate for</param>
        /// <param name="earnedSP">Amount of SP earned from this level-up</param>
        public abstract void AllocateSP(string characterId, int earnedSP);
        
        /// <summary>
        /// Get the name of this strategy (for UI/logging)
        /// </summary>
        public abstract string GetStrategyName();
    }
}
