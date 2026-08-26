// GolfinThermal.m — exposes iOS's own thermal state to the perf-baseline bot.
//
// WHY: Phase 0 measured a 36% fps drop on a hole that was rendering 31% LESS geometry and
// called it thermal throttling — correctly, but WITHOUT proof, because nothing captured the
// device's thermal state. PERF_OPTIMIZATION_PLAN §8 flags that as "leading explanation but
// unproven". NSProcessInfo.thermalState is the ground truth and there is no Unity API for it
// (Adaptive Performance would provide one, but that package is not installed).
//
// Return values map 1:1 to NSProcessInfoThermalState:
//   0 = Nominal, 1 = Fair, 2 = Serious, 3 = Critical, -1 = unavailable (pre-iOS 11)
//
// ⚠️ SHIPPING NOTE: unlike PerfBaselineBot.cs (compiled out by GOLFIN_TESTBUILD), a native
// plugin is not subject to C# scripting defines — this .m compiles into EVERY iOS build,
// including the store build. It is inert there: nothing calls it, it reads a public
// documented API, allocates nothing, and has no side effects. Flagged rather than hidden;
// if that is unacceptable, delete this file and the ThermalState property in
// PerfBaselineBot.cs and the bot simply logs thermal=n/a.

#import <Foundation/Foundation.h>

int GolfinGetThermalState(void)
{
    if (@available(iOS 11.0, *)) {
        return (int)[[NSProcessInfo processInfo] thermalState];
    }
    return -1;
}
