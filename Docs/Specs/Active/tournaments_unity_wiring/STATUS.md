READY_FOR_ARCHITECT_REVIEW

# tournaments_unity_wiring — Phase 3 + 3b

**2026-08-14, iteration 2 — all six review blockers plus section C and D closed. Client half committed.**

Server half shipped earlier: `playlife ece354f`, deployed, `GET /api/v1/tournaments/golfin` live.
That repo was not touched this pass and its tree is clean at `ece354f`.

## Review items

B1 (entered tournament dropped by the server → `KeyNotFoundException` everywhere) — the merge is now
a **union**, extracted to a pure, tested `TournamentScheduleMapper.MergePreservingEntered`.
B2 (`..` walks out of the bucket) — `Uri`-parsed `Scheme`/`Host`/normalized `AbsolutePath`, plus
userinfo and port rejection. B3 `redirectLimit = 0`. B4 a custom download handler that refuses on
`Content-Length` before buffering, 1 MB cap. B5 the sweep skips `*.tmp`. B6 prefetch + sweep now run
on every boot path. C: the timezone test now asserts byte-identity so it bites on any host; +13 tests
covering mid-entry (incl. the B1 case), the empty array, traversals, the cache round trip and an
empty `hole_set`. D: the spurious no-art warning is suppressed when `BannerUrl` is set.

## Verification

- **Tripwired.** All three headline fixes were reverted one at a time and the matching test went red
  each time; restored and re-verified. Details in the report §2.
- **EditMode:** 1233 total, 1230 passed, **0 failed**, 3 pre-existing skips. Per-assembly sweep of
  every test asmdef reconciles exactly to 1230.
- **Live boot against the deployed endpoint:** `Schedule source: SERVER (live fetch). Tournaments=6`,
  preceded by two real `DEFERRED update` lines — acceptance 9 exercised in production, not just tests.
- Acceptance 2, 4, 6, 9 closed. 1 and 3 still need a device; 4's art half wants real uploaded art.

## For Cesar

Client half is one commit. Nothing further is blocked. The remaining device checks are in the
report § 6, and `_placeholderImage` is still deliberately unwired (an art call, accepted in review §D).
