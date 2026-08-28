READY_FOR_SELF_REVIEW

iter-5 2026-08-28. Stopped patching one defect at a time (Cesar). Named the shape — "an irreversible
side effect committed before, or independently of, the check that authorises it, with nothing
undoing it when a later check fails" — and audited ALL EIGHT side effects in the file against it
rather than waiting for the next gate to find the next one.

Three new instances found and fixed: a throw mid-catalog orphaned that catalog's already-written
assets (registration moved before the loop that can throw); the catalog CSV write was non-atomic (a
half-written Clubs.csv is 799 rows lost — now staged + File.Replace); DeleteAsset's return value was
ignored at both sites.

Plus one the audit turned up that is not a rollback bug: the in-build size was reported with
Profiler.GetRuntimeMemorySizeLong, which over-reports by ~2x and is state-dependent. Now
TextureUtil.GetStorageMemorySizeLong — 26,912 B, corroborated by hand (ASTC_6x6 block math) and by
§10.2's own source:build ratio. Every earlier "53.5 KB" in the report is that overstatement.

EditMode 1904 / 1901 passed / 0 failed / 3 pre-existing skips.
See IMPLEMENTER_REPORT.md § iter-5 for the full audit table.
