// golfer_club_grip SPEC §3.12.2 — the serialized hinge capture for one prefab
// (HandHinge_MixamoNative.asset). Its own file on purpose: Unity binds a ScriptableObject to its
// MonoScript by FILE NAME, so declared inside HandHingeModel.cs the asset saved with
// m_Script: {fileID: 0} and could not be loaded after the next domain reload.
//
// Gated like HandHingeModel; without GOLFIN_GOLFER_TEST it is an inert shell so the .asset still
// deserializes (and is never saved — see feedback_never_save_gated_prefab_define_off).

using UnityEngine;

namespace Golfin.Gameplay.Golfer
{
#if GOLFIN_GOLFER_TEST
    /// <summary>Serialized capture for one prefab, one <see cref="HandHingeHand"/> per side.</summary>
    public sealed class HandHingeData : ScriptableObject
    {
        public string sourcePrefab;
        public HandHingeHand left;
        public HandHingeHand right;
    }
#else
    /// <summary>GOLFIN_GOLFER_TEST is absent: an inert shell so the .asset still deserializes.</summary>
    public sealed class HandHingeData : ScriptableObject { }
#endif
}
