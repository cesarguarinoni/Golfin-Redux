DONE

Cesar-approved 2026-08-07.

Implemented directly by Claude Code (orchestrator route, at Cesar's instruction), not via the
implementer→self-review→review→red-team subagent chain.

Shipped in four passes, each driven by Cesar's feedback on the previous one:
  354  — diagnose + camera on the hole axis, show-region fit, hide the mountain ring, pan/zoom clamps
  354b — frame the playable footprint (OB-mask hull) instead of the OB bounding rect
  354c — off-tile ground stays GREEN; fit = ball + flag only, zoomed as tight as they allow
  354d — camera yaw snapped to the playfield axis so the field renders upright

Evidence: IMPLEMENTER_REPORT.md
Canonical screenshot: screenshots/canonical_hole1_tee_map_open.png

Open items handed back to Cesar (neither blocks the feature):
  - `_heroTiltDeg` is serialized 70 on the LabScaffold instance (spec asked 80; 90 would also
    remove the perspective trapezoid on the playfield rectangle). One Inspector field.
  - On-device pinch / two-finger-pan gestures are unexercised — no Touchscreen in the editor harness.
