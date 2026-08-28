READY_FOR_SELF_REVIEW

iter-3 2026-08-28. TWO defects of one class, found by me while briefing the red-team, not by a gate:
the CSV was written BEFORE import verification (a refused import left the name in the repo and the
file on disk while reporting "Refused" — a row silently withheld at runtime behind a name that looks
correct), and my first fix for it treated SharedWithSibling as safe, so one refused club fetch would
have left five sibling rows naming a deleted sprite. Both fixed, both demonstrated red then green
under a forced verification failure, both happy paths re-proven.

Known gap: neither defect is covered by a regression test — reaching them needs a forced
verification failure and there is no seam for that yet. Follow-up, stated not hidden.

EditMode 1897 / 1894 passed / 0 failed / 3 pre-existing skips.
See IMPLEMENTER_REPORT.md § iter-3.
