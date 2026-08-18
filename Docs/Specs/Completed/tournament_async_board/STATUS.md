DONE

Approved by Cesar 2026-08-18 after the implementation pass (iter-1, shape `tournaments:remote-backend-swap`).

Shipped: client `886a3c50f`; server half live in playlife (`a44529d`, deployed and smoke-checked —
all four `/golfin/{slug}/…` endpoints 403-not-404, public schedule still 200).
Full EditMode sweep at close: 1426 total / 1423 passed / 0 failed / 3 pre-existing skips.

NOT verified by the pipeline: the six device-only manual items in SPEC §5, enumerated in
IMPLEMENTER_REPORT.md §8 (two-account board parity, single fee debit across a mid-enter drop,
airplane-mode queue flush, second-device resume, the two-rank label + 10th-human bot retirement,
ended-board T-ties). Approved on the code and the suite; those remain a device pass.
