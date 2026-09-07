READY_FOR_SELF_REVIEW

§9.8 complete. Retargeting confirmed as the cause of the sliding legs: worst foot slide
0.4770 m (Quaternius, Unity retarget) vs 0.0915 m (Mixamo-native, clips on the model) = 5.2x less,
with NO grip/finger/forearm correction on the Mixamo side. Roster pipeline: rig the model in
Mixamo and download the clips on it; club-in-hand mocap is not needed.

Evidence: golfer_invariants_mixamo.json (23 pass / 1 fail / 8 skip), evidence/9_8/sbs_*.jpg
side-by-side at address, at-rest and t=0.6s. The single fail is budget.tris 36,510 — Remy own
clothed mesh, out of §9.8 scope; the shipped stand-in PfGolfer_Test remains 14,648.

Findings F1 (retarget vs clips), F2 (Mixamo import scale) and F3 (club mount, decision b) are in
IMPLEMENTER_REPORT.md. Active profile restored to iOS-Full-GPS.
