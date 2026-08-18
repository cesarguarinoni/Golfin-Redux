DONE

Cesar approved 2026-08-18. Panel live at https://admin.golfin.world/telemetry
(deploys f7533c7a… then 840d6155… for the alphabetical sidebar; Access 302 verified
after both).

Still open, and NOT a defect in this build — it needs data that does not exist yet:
SPEC §5.6, matching the KPI totals against hand-run SQL once the ~20 testers have
actually played. Re-run it during the beta week — the queries are written and
waiting in `live_smoke_5.6.sql` next to this file; set the two dates to the
range the panel is showing and compare block by block.
