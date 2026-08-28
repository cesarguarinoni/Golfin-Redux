READY_FOR_SELF_REVIEW

iter-2 2026-08-28. Red-team FAIL addressed: the collision guard was case-SENSITIVE on
case-INSENSITIVE APFS, so a case-variant derived name sailed past it and the write would have
replaced an existing asset's bytes while keeping its name, .meta and GUID. Fixed in four places
(ExistingAsset, the same-run dedup key, the FindSibling exclude, plus a re-check adjacent to the
write). Tripwire-demonstrated red then green; proven live against the real Driver-FairX.png, which
is byte-identical after.

EditMode 1897 / 1894 passed / 0 failed / 3 pre-existing skips (+3 collision tests).
See IMPLEMENTER_REPORT.md § iter-2 and REDTEAM_REVIEW.md.
