using System.Collections;
using UnityEngine;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.Controls.Bot
{
    /// <summary>
    /// The two waits every bot swing does before it touches anything — <c>VersusBot</c> steps 6
    /// and 7, moved verbatim (bot_scheme_parity §3.1).
    ///
    /// <para>SHARED SO THE FOUR EXECUTORS CANNOT DRIFT. Both gates are bounded at 4 s and both
    /// existed because a bot that starts a drag before the controller is Idle silently no-ops and
    /// the whole hole then hangs waiting for a ball that was never hit. The Idle gate is a HARD
    /// stop (the caller re-checks and abandons the swing); the ball-state gate only warns, because
    /// a scaffold scene with no state machine at all is a legitimate place to swing.</para>
    /// </summary>
    public static class BotSwingGates
    {
        private const float GateSeconds = 4f;

        public static IEnumerator WaitForSwingReady(BotExecutionContext ctx)
        {
            ShotController shot = ctx?.Shot;
            if (shot == null) yield break;

            float gateElapsed = 0f;
            while (shot.State != ShotState.Idle && gateElapsed < GateSeconds)
            {
                gateElapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (shot.State != ShotState.Idle)
            {
                Debug.LogWarning($"{ctx.LogTag} TakeShot: ShotController never reached Idle (state={shot.State})");
                yield break;   // the caller re-checks State and abandons the swing
            }

            if (ctx.BallReady == null) yield break;

            gateElapsed = 0f;
            while (!ctx.BallReady() && gateElapsed < GateSeconds)
            {
                gateElapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!ctx.BallReady())
                Debug.LogWarning($"{ctx.LogTag} TakeShot: BallSM never reached Aiming");
        }
    }
}
