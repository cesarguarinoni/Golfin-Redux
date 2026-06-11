DONE

# STATUS — `versus_bot_hardening` (Order 345)

- **State:** DONE — Cesar-approved 2026-06-11. Moved to `Docs/Specs/Completed/`.
- **Pipeline:** 3 implementer iterations through the full two-gate chain. iter-1 self-review FAIL (H2/H3 videos didn't exercise the behaviors; H3 gain ~10× too large). iter-2 red-team FAIL (H2 canonical video ended frozen on a 100%-putt off-world self-destruct; H1 collapsed to always-wedge). iter-3 PASS (fly-over check + putter-power guards fixed the self-destruct; H1 distance bands; red-team ARCHITECT_REVIEW_PASS).
- **Shipped:** H1 calibrated `bot_clubs.csv` + distance-band club selection; H2 proactive water layup/fly-over + ±10/20° retarget + reactive OBReason backstop; H3 additive `PutterGreenReader.TryGetSlopeAt` + slope-aim/power nudge. `VersusBot` shippable; diff confined; no change to turn-flow/resolution/HUD/RP/solo.
- **Next:** 1v1 Phase 2b (char-level → CSV error-band difficulty model) on this hardened baseline.
