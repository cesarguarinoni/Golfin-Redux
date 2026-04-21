using UnityEngine;
using Golfin.Physics.Math;

namespace Golfin.Physics.Math.Unity
{
    // Vector3 bridge — the ONLY UnityEngine reference in the math layer.
    // Lives in a separate assembly so Golfin.Physics.Math stays noEngineReferences.
    public static class FP3Extensions
    {
        public static Vector3 ToVector3(this fp3 v) =>
            new Vector3(v.x.ToFloat(), v.y.ToFloat(), v.z.ToFloat());
    }
}
