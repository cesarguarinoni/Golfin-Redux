DONE

Approved by Cesar 2026-08-29. Folder moved to `Docs/Specs/Completed/`.

Shipped: (A) `playlife/backend/fly.toml` `auto_stop_machines "stop" -> "suspend"` — cold path
5.20 s -> 1.18 s, `min_machines_running` still 0; (B) shared `Golfin.UI.Polish.PendingSpend` on all
six spend call sites, every `_purchaseInFlight` latch kept, no new art and no scene edits; (C) one
`[ApiClient] METHOD path -> status in N ms` line per completed request, LogWarning above 1500 ms.

Warm purchase measured at 246 ms (<= 400 ms), so the SPEC section 8 keep-alive follow-up is CLOSED.
EditMode suite 1964 / 1961 passed / 0 failed.
