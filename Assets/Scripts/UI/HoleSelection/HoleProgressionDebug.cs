using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct HoleProgressionEntry
{
    public int holeNumber;
    public bool unlocked;
    public bool played;
}

namespace GolfinRedux.UI.HoleSelection
{
    /// <summary>
    /// Inspector debug surface for HoleProgressionService.
    /// Lives on ShellSceneRoot. At Awake() it pushes its overrides into the service.
    /// REMOVE or no-op once real save state lands (Loop v2).
    /// </summary>
    public class HoleProgressionDebug : MonoBehaviour
    {
        [SerializeField] private List<HoleProgressionEntry> overrides = new List<HoleProgressionEntry>();

        private void Awake()
        {
            foreach (var e in overrides)
            {
                HoleProgressionService.Instance.SetUnlockedOverride(e.holeNumber, e.unlocked);
                HoleProgressionService.Instance.SetPlayedOverride(e.holeNumber, e.played);
            }
        }
    }
}
