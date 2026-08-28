READY_FOR_SELF_REVIEW

Implemented 2026-08-28 in §5 order (backend → content → admin → Unity → docs).
Deployment is DONE, not pending: playlife-api v58
(`playlife-api:deployment-01M13PM5NTDK20FB5E7HKRKFD5`) and admin.golfin.world
Cloudflare version `429883ff-99ce-495a-b755-f4d5805a2f57`, sidebar stamp
`256f21587` read back in the browser.

The §21 live E2E RAN, in the live admin, against prod: practice 10 → 15
published (modes v2), a stale client asking to pay 10 got
`{"status":"fee_changed","fee":15}` with the ledger unchanged, and the second tap
debited 15 (ledger row `mode_entry_fee:practice`, −15). Live state restored
afterwards (practice fee 10 / rewards 5, versus_win 20); `modes` sits at v4
because a publish never rewinds its version.

NOTE (unchanged): the legacy bare `mode_entry_fee` reason closure remains a
SEPARATE commit on Cesar's word after the build carrying the suffixed reason
ships. It was re-verified live as still accepted.
