DONE

water_splash_fx (Order 349) — Cesar-approved 2026-06-15, moved to Completed.

Shipped:
- Splash VFX (WaterSplash.prefab — subtle layered foam/ring/jet/scatter, ~24 particles; splash
  materials renderQueue 3100 so the particles draw on top of the transparent water).
- Problem A (grey water): PhysicsLabController.DisableShellDirectionalLight() on hole load — a duplicate
  ShellScene directional light was double-lighting the URPWater flat grey. Restored on unload.
- Camera dwell: PhysicsLabController.WaterSplashCameraHold() — on a water landing the camera holds on
  the entry ~1.2s so the splash plays, then drops + re-aims (camera-only; gameplay result unchanged).
- WaterSplashController + 4 EditMode tests (pass), WaterSplashCaptureRig (editor/bot-only).

Commits: d1266448 (feat), 7d61da61 (WaterSurface.mat hand-tune — REVERT if unintended),
58b2e527 (MCP package bump, infra), + close-out.

Open follow-ups (not blocking): RP save restore (999999 → intended, original not captured);
sound_effects (Order 350) supplies the splash audio clip.
