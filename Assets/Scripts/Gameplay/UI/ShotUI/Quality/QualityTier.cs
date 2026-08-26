namespace Golfin.Gameplay.UI.Quality
{
    /// <summary>
    /// The three presentation tiers (quality_tiers, roadmap 9a — Phase 2 of PERF_OPTIMIZATION_PLAN).
    ///
    /// THE VALUES ARE THE QUALITY LEVEL INDICES. <c>QualitySettings.SetQualityLevel((int)tier)</c>
    /// is the whole mechanism by which a tier swaps its URP pipeline asset, so
    /// ProjectSettings/QualitySettings.asset MUST keep the level order Low(0), Mid(1), High(2),
    /// PC(3). Reordering the levels in the Quality window silently re-points every tier.
    ///
    /// FAIRNESS RULE (plan §2, locked by Cesar): a tier changes PRESENTATION ONLY — render scale,
    /// target frame rate, shadows, LOD0 skipping, tree wind, shell post-processing. It never
    /// changes terrain, tree placement, tree draw/cull distance or lodBias, so two players on
    /// different tiers see the same course and the sim reads nothing tier-dependent.
    /// </summary>
    public enum QualityTier
    {
        Low  = 0,
        Mid  = 1,
        High = 2,
    }
}
