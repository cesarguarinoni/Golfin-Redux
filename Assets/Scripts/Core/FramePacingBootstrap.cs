using UnityEngine;

namespace Golfin.Core
{
    /// <summary>
    /// Frame-pacing bootstrap (ui_frame_pacing task, 2026-08-05).
    ///
    /// WHY: When runtime code never assigns <see cref="Application.targetFrameRate"/>,
    /// Unity's MOBILE default is 30 fps (the value is -1 / "platform default", which on
    /// iOS and Android is a 30 fps cap, NOT the display's native refresh). The editor
    /// runs uncapped at the Mac's refresh, so animations look smooth there and choppy on
    /// device — the mode-slide carousel's 0.22 s cubic ease-out gets ~6-7 rendered frames
    /// per slide at 30 fps, with the largest positional steps front-loaded. That IS the
    /// choppiness; the animation code itself is frame-rate independent and correct.
    ///
    /// FIX: pin the runtime to 60 fps once, before the first scene loads. One knob, one
    /// place — following the SfxBusReset / StaminaRuntimeService.Boot / BuildStamp.Bootstrap
    /// [RuntimeInitializeOnLoadMethod] pattern. No scene edits, purely additive.
    ///
    /// vSyncCount is deliberately NOT touched — it is ignored on iOS, and on Android it
    /// would clamp targetFrameRate to the display's Hz. Leaving it alone lets
    /// targetFrameRate be authoritative on the platforms we ship.
    ///
    /// 120 Hz / ProMotion is a deliberate NON-goal here: it needs targetFrameRate = 120
    /// PLUS the CADisableMinimumFrameDurationOnPhone Info.plist key, and carries a real
    /// battery/thermal cost. Per-tier fps (e.g. 60 on standard iPhones, 120 on Pro) is an
    /// Order 900 quality-tier decision — this is the hook, not the implementation.
    /// </summary>
    internal static class FramePacingBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            // Mobile defaults to a 30 fps cap when targetFrameRate is left unset; pin 60.
            Application.targetFrameRate = 60;

            Debug.Log($"[FramePacingBootstrap] Application.targetFrameRate set to {Application.targetFrameRate}");
        }
    }
}
