using UnityEngine;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// Scene component holding the loaded heightmap for the active hole.
    /// Attach to a GameObject on the hole scene; assign the heightmap TextAsset.
    /// </summary>
    public sealed class HeightProvider : MonoBehaviour
    {
        [SerializeField] private TextAsset heightmapAsset;
        public HeightmapData Data { get; private set; }

        void Awake()
        {
            if (heightmapAsset == null)
            {
                Debug.LogError("[HeightProvider] No heightmap TextAsset assigned.", this);
                return;
            }
            Data = HeightmapLoader.LoadFromTextAsset(heightmapAsset);
            if (Data == null)
                Debug.LogError("[HeightProvider] Failed to load heightmap.", this);
            else
                Debug.Log($"[HeightProvider] Loaded {Data.Resolution}×{Data.Resolution} heightmap, " +
                          $"size {Data.SizeX.ToFloat()}×{Data.SizeZ.ToFloat()} m.");
        }
    }
}
