READY_FOR_ARCHITECT_REVIEW

Task: content_admin_panels
Kind: backend (SPEC_KIND: backend — Next.js dashboard, no Unity surface)
Iteration: 1
Updated: 2026-08-25

Five panels + one shared publish drawer, built entirely on the six routes that already
existed. ZERO new API routes, zero schema changes, zero Unity/Assets edits.

Deployed: Version ID 3361ddfe-8132-4596-b306-2d5f89d33064. Root still 302s to
cloudflareaccess; all five new routes are behind the Access gate.

14 of 15 acceptance items PASS. One (the Clubs rarity facet) is PARTIAL and is the
headline finding: the rows route has no filter parameter, so brand and type narrow the
server query completely but rarity reaches only 792 of 799 rows. Four findings total —
three are things the six routes provably cannot serve, reported rather than worked
around by adding an endpoint. See IMPLEMENTER_REPORT.md § Findings.
