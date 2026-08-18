# SPEC — `leaderboard_backend`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. `STATUS.md` tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Current: `SPEC_READY`.

## Goal

The Rankings screen moves off `LocalFakeLeaderboardProvider` onto the backend. Every player
sees the SAME board — real players aggregated from the one `points_transactions` ledger,
plus the server-side fake pool. **Unity client only** — the server half is already written
by the Architect (deployed status tracked in TellCode; do not start until the endpoint is
live, STATUS will say so).

Decisions of record (Cesar, 2026-08-18): server-side fake pool; character id + level sync
to the backend; Architect built/deploys the server half.

## What already exists (do NOT rebuild)

Verified in the tree 2026-08-18:

| Layer | What exists | Where |
|---|---|---|
| UI | The entire Rankings screen — tabs, podium, scroll list, pinned player row, countdown | `Assets/Scripts/UI/Rankings/RankingsScreenController.cs`, `Top3CardWidget.cs`, `RankingsCardWidget.cs` |
| Seam | `ILeaderboardProvider` — built for this exact swap ("Phase 2+: backend implementation — UI code is untouched") | `UI/Rankings/Core/ILeaderboardProvider.cs` |
| Cache | `LeaderboardManager` singleton, per-period session cache, `Provider` setter clears it | `UI/Rankings/LeaderboardManager.cs` |
| Net | `ApiClient` (`Get<T>` at :67, 401-refresh-and-replay, transient retries), bearer via `AuthServiceTokenProvider` | `Assets/Scripts/Net/ApiClient.cs` |
| Net | `Endpoints` — add two lines here, nothing else | `Assets/Scripts/Net/Endpoints.cs` |
| Disk cache pattern | raw-body mirror, atomic `.tmp`+replace, null on any failure | `Assets/Scripts/BannersRuntime/RemoteBannerSource.cs` |
| Identity | `PlayerIdentity.DisplayNameOr("YOU")` | `Assets/Scripts/Auth/PlayerIdentity.cs` |
| Character | `CharacterManager.Instance` — `GetSelectedCharacterId()` (:469), events `OnCharacterSelected`, `OnCharacterLeveledUp`, `OnRosterChanged` (:38-40) | `Assets/Scripts/CharacterManager.cs` |
| Fakes (client) | `LocalFakeLeaderboardProvider` + `Resources/Data/fake_players.csv` — RETIRES from the signed-in path (see §6) | `UI/Rankings/LocalFakeLeaderboardProvider.cs` |

## 1. Server contract (already implemented — this is the truth to code against)

### GET `{Endpoints.BaseUrl}/leaderboards/{period}`  · period ∈ `daily|weekly|monthly|historic`

**AUTH REQUIRED** (bearer rides ApiClient automatically). The server identifies the caller
from the token and always returns their row. 404 for an unknown period. Envelope: `{data:…}`.

```json
{"data": {
  "fetched_at": "2026-08-18T05:30:00+00:00",
  "period": "daily",
  "period_end_utc": "2026-08-19T00:00:00+00:00",
  "entries": [
    {"rank": 1, "is_tie": false, "display_name": "SMAUG", "character_id": "char_olivia",
     "level": 232, "score": 312, "is_player": false},
    {"rank": 2, "is_tie": true, "display_name": "Cratilo", "character_id": "char_james",
     "level": 12, "score": 220, "is_player": true}
  ],
  "player": {"rank": 2, "is_tie": true, "display_name": "Cratilo",
             "character_id": "char_james", "level": 12, "score": 220}
}}
```

Facts the client may rely on:

- `entries` is the top ≤100, already sorted, ranks + `is_tie` computed server-side with the
  same standard competition ranking (1,2,2,4) the client used. Do NOT re-rank.
- `player` is ALWAYS present, even at score 0 / rank outside the slice. `is_player` marks the
  caller's row inside `entries` when they are in the top slice.
- `period_end_utc` is `null` for `historic` — drive the countdown from it (with `fetched_at`
  as the server-now reference) instead of local period math.
- `character_id` can be NULL (PLAYLIFE-only users, players who never synced) → render the
  existing default-portrait path.
- Scores are game-action RP only (server filters the ledger by the `game_point_actions`
  catalog): GPS earns, admin grants and PLAYLIFE actions never appear.

### PUT `{Endpoints.BaseUrl}/user/golfin-character`

**AUTH REQUIRED.** Body `{"character_id": "char_james", "level": 12}` → `{data:{…profile…}}`.
400 on empty/oversized `character_id`; level clamped server-side to 1–999.
⚠️ `ApiClient` has `Get<T>`/`Post<T>` but no PUT — add `Put<T>` mirroring `Post<T>`
(one line, same `SendRoutine` path with method "PUT").

## 2. Endpoints.cs — two additions

```csharp
/// <summary>GET → the ranked board + the caller's own row. AUTH REQUIRED.</summary>
public static string Leaderboard(string period) => BaseUrl + "/leaderboards/" + period;

/// <summary>PUT {character_id, level} — leaderboard portrait sync. AUTH REQUIRED.</summary>
public static string UserGolfinCharacter => BaseUrl + "/user/golfin-character";
```

## 3. `BackendLeaderboardProvider` (new, `UI/Rankings/`)

Implements `ILeaderboardProvider` synchronously over a per-period SNAPSHOT it holds, plus an
async refresh the screen drives:

- `Refresh(LeaderboardPeriod period, Action<bool> onDone)` — `ApiClient.Instance.Run(Get<…>)`
  against `Endpoints.Leaderboard(...)`; on success store the snapshot, mirror the raw body to
  disk (`leaderboard_{period}.json`, `RemoteBannerSource` atomic-write discipline), invoke
  `onDone(true)`. Any failure: keep the previous snapshot (or disk cache loaded at
  construction), `onDone(false)`, silent otherwise — no error UI in this task.
- `GetRanking(period)` → snapshot entries mapped to `LeaderboardEntry` (Rank/IsTie/
  DisplayName/CharacterId/Level/Score/IsPlayer verbatim from the payload).
- `GetPlayerEntry(period)` → the payload `player` object (IsPlayer=true). For the player's
  OWN row keep the client-side name override `PlayerIdentity.DisplayNameOr("YOU")` — the
  server name may lag the local one.
- `GetPeriodEndUtc(period)` → `period_end_utc` adjusted by (local now − `fetched_at`) so the
  countdown ignores device clock skew; `DateTime.MaxValue` for historic/null. Keep using
  `NetworkTimeProvider.UtcNow` as the "now" side, as `UpdateCountdownLabel` already does.

DTOs: `LeaderboardDtos.cs` next to it, `[Serializable]` classes matching §1, parsed with the
same JSON path the Banners DTOs use.

## 4. Wiring — screen drives refresh

- `LeaderboardManager.Awake`: choose provider — `BotSessionOverride`/signed-out →
  keep `LocalFakeLeaderboardProvider` (bots are offline by design and must not hit prod);
  otherwise `BackendLeaderboardProvider`.
- `RankingsScreenController.OnEnable`: after the existing `InvalidateAllCache()` +
  `RebuildList()` (which now renders the disk-cached snapshot instantly), call
  `Refresh(_activePeriod, ok => { if (ok) { LeaderboardManager.Instance?.InvalidateCache(period); RebuildList(); } })`.
  Same on `OnTabClicked` for the tapped period. Guard against double-refresh in flight.
- NO prefab/scene edits: every hook is code-side in existing methods.

## 5. Character sync (new, small)

`GolfinCharacterSync` (suggested `Assets/Scripts/Net/` or next to the provider): subscribes
`CharacterManager.OnCharacterSelected` + `OnCharacterLeveledUp` (subscribe OnEnable /
unsubscribe OnDisable per house pattern), and on sign-in. Sends
`Put<…>(Endpoints.UserGolfinCharacter, {selected id, its currentLevel})`, fire-and-forget,
throttled (skip if an identical payload was sent this session). Silent on failure — the
leaderboard is cosmetic. NOTE: for the sign-in hook, reuse whatever hook `rp_balance_sync`
lands (its spec flagged that AuthService has no sign-in event; do not invent a second one).

## 6. LocalFakeLeaderboardProvider — retired, not deleted

Stays in the tree for the bot/signed-out path (§4) and its EditMode tests. Its SaveData
accumulator writes (`RolloverStalePeriods`) keep running only on that path. Do NOT delete
`fake_players.csv`. Real-player accumulators (`rpDaily` etc.) keep being written by the earn
path — untouched, they're now display-irrelevant when the backend provider is active.

## 7. Acceptance

EditMode:
- [ ] DTO parse of the §1 payload (incl. null `character_id`, null `period_end_utc`).
- [ ] Provider maps payload → `LeaderboardEntry` verbatim (rank/tie NOT recomputed).
- [ ] Countdown end-time math: skewed device clock (±10 min) still yields the server delta.
- [ ] Disk cache round-trip; corrupt cache file → null → provider falls back to empty + refresh.
- [ ] Provider selection: BotSessionOverride active → LocalFake; signed-in → Backend.
- [ ] Full per-assembly EditMode sweep stays green (filtered runs mask failures).

Manual (Cesar, device):
- [ ] Two signed-in accounts see the SAME board (same fakes, same scores) on all four tabs.
- [ ] Play a hole → reopen Rankings → daily/weekly/monthly/historic all reflect the earn.
- [ ] Player row pinned with correct rank when outside top 100 (fresh account, score 0).
- [ ] Character switch on device A → board on device B shows the new portrait after refresh.
- [ ] Airplane mode → Rankings opens with the last cached board, no errors.
- [ ] Countdown label matches UTC midnight / Monday / month boundary.

## 8. Out of scope

Previous-period results popup, leagues (`ApplyLeagueLabel` stays hardcoded), SNS share,
tap-username profile view, push of daily results, fake-pool dashboard panel, removing the
SaveData accumulators, backend/dashboard edits, tournament leaderboards
(`TournamentLeaderboardScreenController` is a different system).

## 9. Files this task touches

**New:** `UI/Rankings/BackendLeaderboardProvider.cs`, `UI/Rankings/LeaderboardDtos.cs`,
`GolfinCharacterSync.cs` (location per §5), EditMode tests.
**Modified:** `Net/Endpoints.cs` (+2), `Net/ApiClient.cs` (+`Put<T>`),
`UI/Rankings/LeaderboardManager.cs` (provider selection), `UI/Rankings/RankingsScreenController.cs`
(refresh hooks), `Docs/AI_CONTEXT.md`, `Docs/TellCode.md`, this folder's STATUS/report.
