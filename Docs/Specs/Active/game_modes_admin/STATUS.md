READY_FOR_REDTEAM

golfin-reviewer PASS iter-3. Iter-2 blocker (SPEC §6 item 9,
content_version.txt stale at modes=4) closed and re-verified from primary
sources: `--check --catalogs modes` exit 0 with stdout confirming
modes.csv unchanged and version file unchanged; disk `modes=6` matches
prod `published_version=6`; all nine catalog cursors enumerated against
prod via PostgREST (zero stale); modes.csv md5 `c36e4288…` unchanged and
absent from iter-3 fix commit `6f6ce4b44`. Red-team blocker fix not
regressed: mirrorForCatalog remains sole mirror writer (grep line 298),
two callers only (publishCatalog:396, rollbackCatalog:537), rollbackCatalog
body 525-547 mirrors from snapshot BEFORE rpc and aborts on error; prod
mirror `updated_at 10:41:01.697` is 119ms before catalog `10:41:01.816` —
direct live evidence the fix fires on rollback. Every SPEC §6 item
re-derived this pass: backend 118 passed in 0.39s, Tools/content 26 OK,
dashboard tsc exit 0 silent, live API smoke health=200/content=200/
spend=403. Rule-15 exploration into RemoteContentSource.BuildSince
(cursor parity + rollback-lower), AddFallbackModes stale-price path, and
bundled cursor shipped-into-future-builds — server explicitly handles
cursor>published_version with FULL send, stale client fees bounded by
fee_changed protocol tested in §6 item 1, no new hole found. Standing
bans clean, deploy scope clean (dashboard diff since 7337bdf67 empty,
API v59 unchanged, no scene/physics/M_Splash diff). Gates 14/16/17/18/19/21
legitimately do not engage. Hands to golfin-redteam-reviewer.
