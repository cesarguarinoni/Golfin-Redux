// TeeData — SPEC §3.1 (Phase 3)
// Tee-set schema for multi-tee support.
// Lives in Golfin.Course.Runtime so it can be directly referenced by:
//   - Assembly-CSharp (via autoReferenced: true)
//   - Golfin.Course.Tests (via explicit reference to Golfin.Course.Runtime)
//
// HoleData.cs (Assembly-CSharp) holds a List<TeeData> and TryGetTee().

using System;

namespace Golfin.Course.Runtime
{
    /// <summary>
    /// Identifies which tee set a TeeData row represents.
    /// Ordered roughly by distance (longest to shortest).
    /// </summary>
    public enum TeeSet
    {
        Tournament,
        Back,
        Regular,
        Middle,
        Front,
        Ladies
    }

    /// <summary>
    /// One tee-set entry for a single hole.
    /// Currently stores yards and color name; additional fields (metres, rating) are TODO(multi-course).
    /// </summary>
    [Serializable]
    public class TeeData
    {
        public TeeSet set;
        public int    yards;
        public string color;   // color name (e.g. "blue", "white"); null when unknown
    }
}
