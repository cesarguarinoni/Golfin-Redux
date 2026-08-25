READY_FOR_ARCHITECT_REVIEW

Task: content_cursor_per_catalog
Kind: backend (SPEC_KIND: backend — no Unity surface, no Figma, no screenshot)
Iteration: 1
Updated: 2026-08-25

All twelve applicable acceptance items PASS. One item is marked N/A-with-a-correction
(§5 "texts 501 vs 502") and one records a deploy whose CLI
lied about failing (D-4, playlife-api is live on prod, image version 50); both are written up in IMPLEMENTER_REPORT.md § Spec deviations. No item was marked PASS
on anything but a pasted tool result.

Evidence: acceptance_probe.txt (local, real router + real prod Supabase) and
acceptance_probe_prod.txt (the same list re-run against live https://playlife-api.fly.dev).
