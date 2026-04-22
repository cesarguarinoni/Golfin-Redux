using System;
using UnityEngine;

namespace Golfin.SceneSnapshot
{
    /// <summary>
    /// Stable identifier stamped on manually-placed GameObjects by the Manual Scene
    /// Snapshot tool. The GUID is generated on first capture and persists across
    /// scene re-imports so the snapshot can find the same object on Restore.
    ///
    /// Do NOT add this manually in the inspector — it's stamped by the Capture pass.
    /// Removing it disconnects the object from snapshot tracking; the next Capture
    /// will treat it as new and assign a fresh GUID.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManualPropId : MonoBehaviour
    {
        [SerializeField, HideInInspector] private string guid;

        public string Guid => guid;

        public void EnsureGuid()
        {
            if (string.IsNullOrEmpty(guid))
                guid = System.Guid.NewGuid().ToString("N");
        }

#if UNITY_EDITOR
        public void SetGuidEditor(string g) { guid = g; }
#endif
    }
}
