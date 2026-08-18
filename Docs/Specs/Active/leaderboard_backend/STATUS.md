AWAITING_CESAR_MANUAL_PASS

# STATUS — leaderboard_backend

- **2026-08-18 — SPEC_READY.** Server half already built + deployed by the Architect (migration applied,
  `fly deploy` green, all four periods and the PUT answering 403-not-404). SPEC §1 is the live contract.

- **2026-08-18 — IMPLEMENTED (code complete, tests green).** The Rankings screen reads the backend.
  `BackendLeaderboardProvider` (per-period snapshot + atomic raw-body disk cache, §3), `LeaderboardDtos`
  (§1), `LeaderboardProviderPolicy` (§4 selection rule), `GolfinCharacterSync` (§5 PUT, throttled,
  fire-and-forget), `ApiClient.Put<T>` + two `Endpoints` members (§2). Screen hooks are the two refresh
  calls in `OnEnable` / `OnTabClicked` (§4). **ZERO prefab or scene edits** — `ShellScene.unity` is
  untouched by this task and was never saved.

- **EditMode: 1369 passed / 0 failed / 3 pre-existing skips of 1372**, run unfiltered AND per-assembly
  across all 18 EditMode assemblies. The per-assembly counts sum to exactly 1369 + 3 skipped = 1372,
  so no assembly was silently missed. 35 of those tests are new (SPEC §7 EditMode list, all six items).

- **Two deviations from the SPEC's literal file list, both flagged in IMPLEMENTER_REPORT.md:**
  one extra new file (`LeaderboardProviderPolicy.cs`, so §7's provider-selection item is testable), and
  a re-selection hook on `AuthService.SignedIn` (without it, `LeaderboardManager.Awake` running at boot
  — before the auth gate — would pin a first-launch player to the local fakes for the whole session).

- **Next state: `DONE`** — blocked ONLY on Cesar's device pass of the six SPEC §7 manual items, which
  need two real accounts, a real earn, and airplane mode. Nothing else is outstanding.
