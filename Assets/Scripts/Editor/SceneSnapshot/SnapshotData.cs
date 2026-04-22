using System;
using System.Collections.Generic;
using UnityEngine;

namespace Golfin.SceneSnapshot
{
    [Serializable]
    public sealed class SceneSnapshotData
    {
        public string SchemaVersion = "1";
        public string SceneName;
        public string CapturedAtIso;
        public List<PropEntry> Props = new List<PropEntry>();
        public TerrainSnapshot Terrain;
    }

    [Serializable]
    public sealed class PropEntry
    {
        public string Guid;
        public string Name;
        public string PrefabAssetGuid;
        public string ParentGuid;
        public TransformData Transform;
        public bool ActiveSelf;
        public string TagAndLayer;
    }

    [Serializable]
    public struct TransformData
    {
        public Vector3 Position;
        public Vector3 EulerAngles;
        public Vector3 LocalScale;
    }

    [Serializable]
    public sealed class TerrainSnapshot
    {
        public string TerrainObjectName;
        public List<TreeInstanceData> TreeInstances = new List<TreeInstanceData>();
        public List<DetailLayerData> DetailLayers = new List<DetailLayerData>();
    }

    [Serializable]
    public struct TreeInstanceData
    {
        public Vector3 Position;
        public float WidthScale;
        public float HeightScale;
        public float Rotation;
        public Color32 Color;
        public Color32 LightmapColor;
        public int PrototypeIndex;
        public string PrototypePrefabGuid;
    }

    [Serializable]
    public sealed class DetailLayerData
    {
        public int PrototypeIndex;
        public string PrototypePrefabGuid;
        public int Width;
        public int Height;
        public int[] Densities;
    }
}
