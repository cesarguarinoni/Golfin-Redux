DONE

# STATUS — water_entry_presentation

- 2026-08-06 — Cesar reported: camera freezes before the ball sinks, splash not visible. Diagnosed with instrumented runs (see SPEC § Diagnosis): camera aimed correctly but hard-frozen on the contact frame; ball terminal on the surface; splash fires but sits at renderQueue 3000, same queue as the water surface and coplanar with it.
- 2026-08-06 — Scope locked by Cesar: camera stops on contact but stays live until the splash plays (K10 stop-chasing ruling stands for non-water OB); ball sinks and disappears.
- 2026-08-06 — Implemented + verified on a real Hole-6 water shot. Splash now clearly visible, ball sinks, camera holds through it. EditMode camera-director tests pass (274/274, 0 failed). `M_Splash*.mat` untouched.
- 2026-08-06 — Cesar approved ("Perfect"). Moved to `Docs/Specs/Completed/`.
