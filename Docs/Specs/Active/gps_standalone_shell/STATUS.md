SPEC_READY

# STATUS — `gps_standalone_shell`

**Current:** `SPEC_READY` — Architect, 2026-09-03, written while Cesar runs the device pass.
Decision 2026-09-02: standalone PLAYLIFE = Unity thin-shell, Flutter retired.

**D1 decided (Cesar 2026-09-03):** reuse the EXISTING App Store Connect app + its TestFlight; Architect read ASC:
"GOLFIN GPS", Bundle ID `com.nextinnovation.golfingps`, Apple ID 6737145432, same team as the game; last
TestFlight build 0.7.6 (12). standalone variant ships as 1.0.0.

**Queue:** can start now (Code is idle during the device pass); device-pass defects become quick
specs and Cesar orders them against this task.

| Date | State | Note |
|---|---|---|
| 2026-09-03 | `SPEC_READY` | Build profile iOS-Standalone, StandaloneGate + Home rewrite, hub-first boot, chrome, identity, third fastlane lane. |
