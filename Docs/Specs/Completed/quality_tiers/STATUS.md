DONE

Task: quality_tiers (roadmap 9a, Order 900 — Phase 2 of Docs/PERF_OPTIMIZATION_PLAN.md)
Approved by Cesar 2026-08-27: fairness A/B, aim-arrow feel at 30 fps on Low, High shadows at
2 cascades / 60 m, and "Results are acceptable for now. We might revisit optimization in the future."

Gates: SELF_REVIEW_PASS -> golfin-reviewer PASS -> golfin-redteam-reviewer ARCHITECT_REVIEW_PASS.

Device: warm triage on both -O0 and -O3 builds (§12.6), COOLED protocol for High (§12.7,
3/3 runs at Nominal, median 60.0 fps at tee / 59.9 at +45 s). Endurance jobs 20-22 cancelled.
Build size +28 KB of Data/ (§12.8). Sign-off video: Docs/Reports/Media/quality_tiers_2026-08-27.mp4
