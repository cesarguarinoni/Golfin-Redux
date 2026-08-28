READY_FOR_SELF_REVIEW

Red-team iter-1 FAILED it on a real blocker: a `modes` rollback left
`golfin_mode_fees` stranded at the last publish, re-opening the served-catalog-
vs-charged-price drift the task exists to close. Fixed at the SHAPE — one
`mirrorForCatalog` dispatcher, `MIRRORED_CATALOGS` as the named list, and
`rollbackCatalog` now mirrors from the rolled-to snapshot before the rpc and
aborts if that write fails. Covers `characters` too.

Reproduced and re-verified on PROD: publish 12 (v5, mirror 12) -> rollback to v4
(v6, served 10, mirror 10), audit `{"mirrored": true}`, and a live spend refusing
12 with `fee_changed: 10` then debiting 10.

The kill-switch sibling is ACCEPTED AND DOCUMENTED rather than changed — the
reasoning (all three options, and why only one is safe in both directions) is in
the `setCatalogEnabled` comment and ADMIN_DASHBOARD_OPS.

Dashboard redeployed: `5dd60935-66ef-46f2-b92c-e1521fb79580`, stamp `7337bdf67`.
API unchanged at v59. Live state restored; `modes` sits at v6.
