# ARCHITECT_REVIEW — tournaments_unity_wiring

**Reviewer:** Architect (Cowork session), 2026-08-14, over the uncommitted working tree.
**Verdict:** **server half SHIPPED** (playlife `ece354f`, deployed, live). **Client half: 6 fixes before commit**, then it goes in.

The implementation is good work — the assembly boundary held, the fallback ladder is right, the name ladder reached all three render sites, and the two bugs the implementer found (Newtonsoft's reader-level date coercion, and the reflection guard's zero-arg `Compose`) were both real and both correctly diagnosed. What follows is what a second pass found on top of that.

---

## A. Server half — accepted and deployed

`playlife` `ece354f`, `fly deploy` green, verified live:

- `GET /api/v1/tournaments/golfin` → 200, six tournaments, 22 prize bands, `fetched_at` stamped.
- `GET /api/v1/tournaments/active` → `{"data":[]}` — correct: the table currently holds only `kind='golfin'` rows, so the new filter is doing exactly its job. Before this change GPS clients would have started receiving game tournaments.
- The route-order comment above `list_golfin` is the right thing to have written down; without it the next person moves the function and reintroduces the 500.

⚠️ **One thing to carry into Phase 4:** `list_golfin` pops the uuid pk from the payload, so the client only ever learns the slug. The existing entry route is `POST /tournaments/{tournament_id}/enter` keyed by uuid. Phase 4 must either accept a slug there or put the uuid back in the payload — decide it then, don't patch it now.

---

## B. Client half — fix before commit

Ranked. 1–4 are the ones that would hurt a player; 5–6 are cheap and in the same files.

### B1. An entered tournament that leaves the payload takes the app down with it
`TournamentsRuntime/TournamentService.cs:197-219`

`PreserveEnteredTournaments` iterates `incoming.Definitions` and does the entered-check inside that loop, so it can only protect a tournament the server still sends. If a row is deleted (or `kind`-flipped) in the dashboard while a player holds a persisted entry, the swap drops it, and every subsequent `Backend.GetTournament(id)` throws `KeyNotFoundException` (`LocalTournamentBackend.cs:124`) — that is the signup modal (`:92`), the result modal (`:83`), the round handler (`:84`), and `SubmitHoleResult` (`LocalTournamentBackend.cs:195`), i.e. a mid-round hole submission.

Fix: union the entered-but-missing definitions from `currentById` back in, rather than intersecting. Spec §4.2 says the entered tournament must not change under the player; vanishing is the maximal change.

### B2. `..` walks straight out of the allowlisted bucket path
`TournamentsRuntime/TournamentArtPolicy.cs:40-43`

The check is `url.StartsWith(AllowedArtPrefix, Ordinal)` on the raw string — no `Uri` is ever parsed. This passes:

```
https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/tournament-art/../../../../../rest/v1/rpc/anything
```

`UnityWebRequestTexture.GetTexture` then builds a `System.Uri`, which normalizes the dot segments, and the device actually GETs `…/rest/v1/rpc/anything`. Scheme and host survive; **the path prefix — half the control D6 names — does not.** Fix: parse to `Uri`, then compare `uri.Scheme`, `uri.Host` and `uri.AbsolutePath` (post-normalization) against the constant.

Credit where due: the suffix (`…supabase.co.evil.com`), userinfo (`…supabase.co@evil.com`), `%2e%2e`, and cache-filename traversal vectors were all checked and all hold. Case differences fail closed.

### B3. Redirects are followed and never re-validated
`TournamentsRuntime/TournamentArtService.cs:148-172`

`req.redirectLimit` is Unity's default 32, and `req.uri` is never re-checked after completion. A 30x from the allowlisted origin puts third-party bytes into the texture **and** into the disk cache under the allowlisted key, where they reload every launch with no network check. `req.redirectLimit = 0` closes it in one line.

### B4. No download size cap — the 50 MB budget is disk-only
`TournamentArtService.cs:148-172`

`Content-Length` is never inspected; `DownloadHandlerTexture` buffers the whole body before `MaxCacheBytes` is consulted (that constant only reaches `SweepCore`). `Prefetch` fires these unattended at boot, so one oversized object under `tournament-art/` OOMs a mobile device on launch with no user action. The dashboard caps uploads at 500 KB, but §2.3's static-key route can write `banner_url` too — that is the whole reason the client-side control exists.

### B5. The sweep deletes the `.tmp` files the prefetch is mid-write on
`TournamentService.cs:173-174` + `TournamentArtService.cs:275-287, 318-335`

`Prefetch` and `SweepCacheAsync` are issued on the same frame; `SweepCore` enumerates `Directory.GetFiles(dir)`, which includes in-progress `.tmp` staging files, and can delete one mid-write — `File.Replace` then throws, is caught, and the art silently fails to cache. Self-healing next launch, but avoidable: filter `*.tmp` out of the sweep.

### B6. Prefetch and sweep never run on the cache/CSV boot paths
`TournamentService.cs:125-139` vs `:173-174`

Both are called only after a live fetch maps successfully. On every launch where the server is unreachable — which Risk 2 says is not rare — the cache is never trimmed, so the 50 MB bound only exists on sessions that reach the server. Cards still render (art loads lazily), so this is the bound, not the display.

---

## C. Tests — two additions, one repair

- **The timezone test does not guard the timezone fix.** `RemoteScheduleTests.cs:181-203` passes with `DateParseHandling.None` removed, because Newtonsoft's default `RoundtripKind` round trip is instant-preserving and therefore character-identical on a UTC machine. It only bites on a non-UTC host — so it goes green in CI while the exact regression it exists to catch ships. Assert the DTO's `StartAt` string is byte-identical to the input, or pin a non-UTC `TimeZoneInfo` in the test.
- **No mid-entry test at all** (spec acceptance 9) — which is precisely why B1 got through. Add one.
- Also missing: HTTP 200 with `tournaments: []` (behaviour is correct — the mapper returns null and the caller keeps its source — but nothing pins it), the `..` reject, and a cache write/read round trip including the `.tmp` atomicity claim.

Minor: `ExpandHoleSet("")` returns empty and the mapper does not drop such a row (`TournamentScheduleMapper.cs:197`), so a zero-hole tournament can reach a card. Cheap guard while you are in there.

---

## D. Judgment calls — both accepted

**`_placeholderImage` left unwired.** Correct default: warn once and hide, never show a wrong photo. Note that the branch is currently close to unreachable — the dashboard's course dropdown only offers the six ids, and all six have bundled art — so it is a guard against hand-edited SQL rather than an everyday path. Art can come later.

Small follow-on: `ApplyCardArt` paints the bundled layer unconditionally first (correct per §5.1), so a brand tournament on an artless course logs the "no art … `_placeholderImage` is unwired" warning one frame before its downloaded art appears. Suppress the warning when `BannerUrl` is set.

**`japaneseFontScale: 0` on four `LocalizedText` components.** Verified inert and correct to keep: `Assets/Localization/LocalizedText.cs:12` declares `[SerializeField] private float japaneseFontScale = 0f;` and `:56` early-returns on `<= 0f`. Five other assets in the repo already carry the field; this prefab was simply behind on serialization. Leave it in the diff rather than hand-reverting it — it will come back on the next save.

---

## E. Spec correction I owe (mine, not the implementer's)

D3 says server data replaces CSV data "wholesale or not at all — never a merge", and §4.2 requires keeping a definition the player has already entered. When the in-play definition came from the CSV, honouring §4.2 *produces* a merged schedule — a CSV `TournamentDefinition` with a CSV-keyed `PrizeTableId` sitting inside a server payload. Those two rules collide, and the implementer resolved it the right way round.

**D3 is amended:** the no-merge rule governs the schedule as a whole; a definition the player is mid-entry in is the one deliberate exception, it is carried forward with its own prize table, and the carry-forward is logged. Nothing else crosses the boundary.
