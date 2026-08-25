READY_FOR_ARCHITECT_REVIEW

Task: content_panels_gaps
Kind: backend (SPEC_KIND: backend — Next.js dashboard + 4 empty CSV columns, no Unity code)
Iteration: 1
Updated: 2026-08-25

All 12 acceptance items PASS. §4 was a deliberate no-op and the art path has a zero-line diff.

Deployed: Version ID 053c80d6-11ee-41a6-9ef7-d250d8a78857. Root still 302s to
cloudflareaccess, and the new /api/content/[catalog]/versions route is behind Access too.

The §1 correction is confirmed from primary source: all 799/799 club rows carry data.rarity,
the 7 hand-authored ids included, and data->>rarity=eq.Common returns exactly 133 on prod. My
previous FAIL grade was right; the cause I gave was wrong — I measured the id convention when
the facet should have read `data`. The coverage caveat is gone from the UI.

Two defects found and fixed during verification, both mine, both measured not eyeballed:
the rollback button rendered 185px OUTSIDE the drawer (unclickable — and it is the whole point
of §2), and the history table overflowed with no scroll container.
