READY_FOR_ARCHITECT_REVIEW

ARCHITECT_DECISION §1 (Option A) implemented by the orchestrating main thread, 2026-08-28.

The seam is fixed and PROVEN END-TO-END against live Supabase Storage on the kept fixture:
launch 1 withholds and downloads; launch 2 renders the URL art (available 11 -> 12,
renderable=true, 200/200 sampled pixels match the uploaded image and 32/200 match the row's
own bundled art, which is blank in the fixture anyway).

Boot delta, finally measured (SPEC §7, owed since iter-1):
  [CatalogArt] Boot art decode: 1 file(s), 3.1 ms, 0.08 MB read from the on-disk cache (cap 24/session).

Gates: EditMode 1877 / 1874 passed / 0 failed / 3 pre-existing skips; Tools/content 26 pass;
dashboard `npm run build` green. The new disk test was tripwire-demonstrated per
PIPELINE_HARDENING §20 — removing the synchronous disk read turns both disk tests red, and
the revert is byte-identical.
