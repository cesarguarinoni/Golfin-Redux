using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Golfin.UI.EditorTools
{
    /// <summary>
    /// <c>Golfin.Gameplay.Input.FlickPullMath</c>, reached from the editor verification bots.
    ///
    /// <para>WHY REFLECTION AND NOT A REFERENCE: these bots live in the default
    /// <c>Assembly-CSharp-Editor</c>, and both <c>Golfin.Gameplay.Input</c> and
    /// <c>Golfin.Gameplay.Config</c> are <c>autoReferenced: false</c> — which is exactly why
    /// <c>MissDuffFlickVerify</c> already reaches <c>ShotController</c> and
    /// <c>FlickShotViewVerify</c> already reads <c>ControlsConfig.Default</c> this way.</para>
    ///
    /// <para>WHY NOT JUST RE-DERIVE THE THREE LINES HERE: a bot that aims for "100%" has to put
    /// the finger exactly where the shipped mapping says 100% is. A second copy of the arithmetic
    /// would agree today and diverge the first time either threshold is retuned — which is the
    /// failure this file exists to make impossible. There is ONE pull mapping; this is a window
    /// onto it, not a reimplementation.</para>
    /// </summary>
    internal static class FlickPullReflect
    {
        private static readonly Type       _mathType = Resolve("Golfin.Gameplay.Input", "Golfin.Gameplay.Input.FlickPullMath");
        private static readonly Type       _cfgType  = Resolve("Golfin.Gameplay.Config", "Golfin.Gameplay.Config.ControlsConfig");
        private static readonly object     _cfg      = _cfgType?.GetField("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        private static readonly MethodInfo _restY    = _mathType?.GetMethod("RestYPx",            BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo _localY   = _mathType?.GetMethod("ConeLocalYForPower", BindingFlags.Public | BindingFlags.Static);

        private static Type Resolve(string assembly, string typeName) => AppDomain.CurrentDomain
            .GetAssemblies().FirstOrDefault(a => a.GetName().Name == assembly)?.GetType(typeName);

        /// <summary>True when the mapping was found. False means the bot must not pretend to have
        /// driven a calibrated pull — say so rather than falling back to a guess.</summary>
        public static bool Available => _cfg != null && _restY != null && _localY != null;

        /// <summary>Cone-local y the club RESTS at — the zero of the pull.</summary>
        public static float RestYPx(float coneHeightPx)
            => Available ? (float)_restY.Invoke(null, new[] { (object)coneHeightPx, _cfg }) : coneHeightPx;

        /// <summary>Cone-local y a given power puts the club (and therefore the finger) at.</summary>
        public static float ConeLocalYForPower(float power, float coneHeightPx, bool isPutt)
            => Available
                ? (float)_localY.Invoke(null, new[] { (object)power, RestYPx(coneHeightPx), coneHeightPx, _cfg, isPutt })
                : Mathf.Clamp01(1f - power) * coneHeightPx;
    }
}
