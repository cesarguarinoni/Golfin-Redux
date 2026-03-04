using System.Collections.Generic;
using UnityEngine;

namespace GolfinRedux.UI
{
    /// <summary>
    /// Container for all hole data.
    /// You can load this from a CSV/JSON file or populate it in the inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "HoleDatabase", menuName = "Golfin/Hole Database")]
    public class HoleDatabase : ScriptableObject
    {
        public List<HoleData> holes = new();

        public HoleData GetHole(int index)
        {
            if (index >= 0 && index < holes.Count)
                return holes[index];
            return null;
        }
    }
}
