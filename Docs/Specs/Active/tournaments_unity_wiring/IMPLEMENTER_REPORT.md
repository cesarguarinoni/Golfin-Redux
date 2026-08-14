# IMPLEMENTER_REPORT — tournaments_unity_wiring (Phase 3 + 3b)

**Implemented by:** Claude Code (direct, at Cesar's request — NOT via the subagent pipeline)
**Iteration 2** — post `ARCHITECT_REVIEW.md`, 2026-08-14
**Baseline HEAD:** `61829b9aa` (architect review commit; the art move landed inside it)
**Server:** `playlife ece354f`, deployed and live. Working tree there clean and untouched this pass.

> **Pipeline note.** No `golfin-implementer` → self-review → reviewer chain; Cesar drove this
> directly. This phase adds no new visual element and no new screen, so Rules 17/18/19/21 (mesh
> video, Figma fidelity, clone provenance, UI lint) do not apply — all four key off a Figma node or a
> mesh bake. There is no canonical screenshot for the same reason; § 4 lists what replaces it.

---

## 1. Review items — all closed

| # | Item | Fix |
|---|---|---|
| **B1** | Entered tournament dropped by the server takes the app down | `PreserveEnteredTournaments` extracted to a **pure, tested** `TournamentScheduleMapper.MergePreservingEntered`. It is now a **union**: pass 1 walks the incoming schedule swapping entered rows back to the in-play version; **pass 2 walks the CURRENT schedule and re-adds any entered tournament the server no longer sends**, with its prize table and a warning. |
| **B2** | `..` walks out of the allowlisted bucket | `TournamentArtPolicy.IsAllowed` now parses to `Uri` and compares `Scheme` / `Host` / normalized `AbsolutePath`, plus rejects userinfo, non-default ports, and any surviving `..` / `%2e`. Verified live: the traversal normalizes to `/rest/v1/rpc/x` and is refused. |
| **B3** | Redirects followed and never re-validated | `req.redirectLimit = 0`. |
| **B4** | No download size cap | New `SizeCappedDownloadHandler : DownloadHandlerScript` refuses on `Content-Length` **before buffering a byte**, and independently caps bytes actually received so a missing/lying header cannot pass. `MaxDownloadBytes = 1 MB`. Replaces `DownloadHandlerTexture`, which buffers the whole body before any of our code runs. |
| **B5** | Sweep deletes the `.tmp` files prefetch is mid-write on | `SweepCore` skips `*.tmp`. |
| **B6** | Prefetch + sweep never ran on the cache/CSV boot paths | Both moved into a shared `WarmArt()` called from `Apply` **and** `ApplyBundledCsv`, so the 50 MB bound exists on every launch, not only ones that reach the server. |
| **C** | Timezone test did not guard the timezone fix | New `DtoTimestampsAreNeverTouchedByNewtonsoft` asserts the DTO's `StartAt` is **byte-identical** to the input — machine-independent, unlike the instant-equality test (kept alongside). |
| **C** | No mid-entry test — why B1 got through | 5 new tests in `MidEntryPreservationTests`, including the dropped-by-server case. |
| **C** | Missing empty-array / `..` / cache round-trip coverage | `ReturnsNullOnAnEmptyTournamentsArray`, `RejectsDotSegmentsThatWalkOutOfTheBucket` (+`AllowsANestedObjectNameThatIsNotATraversal`), and `ScheduleCacheRoundTripTests` (3 tests incl. `.tmp` atomicity). |
| **C** | Empty `hole_set` reaches a card | Dropped with an error, like the other bad-data cases. Test added. |
| **D** | Spurious "no art" warning one frame before download lands | `ResolveBundledSprite(def, suppressMissingWarning:)` — suppressed when `BannerUrl` is set. |

**Not touched, as instructed:** `playlife/backend/routers/tournaments.py`.
**Carried into Phase 4 (review §A):** `list_golfin` pops the uuid, but `POST /tournaments/{id}/enter` is uuid-keyed — decide slug-vs-uuid then, not now.

---

## 2. The tests were tripwired — they bite

A test that passes with its fix removed is worthless; that was the review's whole point about C. All
three headline fixes were **temporarily reverted** and the suite re-run:

| Tripwire applied | Test that went red |
|---|---|
| `DateParseHandling.None` → `.DateTime` | `DtoTimestampsAreNeverTouchedByNewtonsoft` — *"Expected `2026-08-09T00:00:00+00:00`, was `08/09/2026 07:00:00`"* |
| `MergePreservingEntered` pass 2 → `Enumerable.Empty` | `EnteredTournamentDroppedByTheServerIsCarriedForward` — *"Expected collection containing `gone_from_server`, but was `<still_there>`"* |
| `IsAllowed` → raw `StartsWith` | `RejectsDotSegmentsThatWalkOutOfTheBucket` — traversal allowed |

All three restored; `grep TRIPWIRE Assets/Scripts/` returns nothing.

⚠️ **One thing this caught about my own process:** the first post-restore probe reported B2 still
failing. The restore was correct — Unity had not recompiled, because I ran the probe without an
`assets-refresh` after editing. The probe was measuring the tripwired build. Refreshed and re-ran;
all 11 allowlist cases pass. Worth recording because a stale-assembly reading is indistinguishable
from a real failure unless you check.

---

## 3. Acceptance

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | 📱 Brand case — no name key, server title, brand art | **PENDING DEVICE** | Logic verified end-to-end on a `puma_summer_slam` payload (name `PUMA Summer Slam`, venue `lomond · 18 Holes`, art layer 1). No dashboard row with art exists yet, so the real proof still needs one + a device. |
| 2 | 📱 Schedule is live | **CLOSED (Editor)** | Live boot: `Schedule source: SERVER (live fetch). Tournaments=6`. The client now reads `GET /api/v1/tournaments/golfin`; the six slugs and their windows match the endpoint. A dashboard date edit needs dashboard access, but the mechanism is closed. |
| 3 | 📱 Cold launch, airplane mode | **PASS (Editor) / PENDING DEVICE** | Boot applies cache-or-CSV synchronously before any socket; previously observed degrading cleanly on a failing host with one named log line. |
| 4 | Cache hit — zero downloads on second launch | **CLOSED** | *Schedule:* cache written atomically, no `.tmp` left, re-maps identically (3 tests + live file inspected). *Art:* seeded a real PNG under the derived key, pumped `LoadRoutine` → served a sprite with **zero network**, logging `Cache HIT`. |
| 5 | Art removed → falls back to course photo, never another course's | **PASS** | All six resolve `Resources/TournamentImages/{course_id}`; the positional path is deleted from class and prefab. |
| 6 | Host allowlist | **CLOSED** | 11-case live table, all pass, incl. 4 traversal forms, userinfo, non-default port, http, foreign host — each traversal shown normalizing outside the bucket. 14 unit cases on top. |
| 7 | Reorder → no photo reshuffle | **PASS** | `_courseImages` + `csvIndex` gone; art keyed by `def.Id` / `def.ClubId` only. |
| 8 | Bad server data dropped, others render | **PASS** | 5 tests (dangling bot field, empty ladder, bad window, **empty hole set**, all-bad → keep existing). |
| 9 | Mid-entry stability | **CLOSED** | Not just tests — the **live boot exercised it**: this save holds real entries, and the log shows `DEFERRED update for 'hirono_invitational'` and `'kisarazu_cup'` while the server schedule was applied. Plus 5 unit tests incl. the B1 dropped case. |
| 10 | Full EditMode suite green, per assembly | **PASS** | See below. |

### Test results

**Unfiltered EditMode:** `TotalTests 1233, Passed 1230, Failed 0`, 3 pre-existing skips.

**Per-assembly sweep** (every asmdef declaring `TestAssemblies`), reconciling exactly to the
unfiltered total — 924 + 209 + 61 + 36 = **1230**:

| Assembly | Passed | Assembly | Passed |
|---|---|---|---|
| Golfin.Auth.Tests | 27 | Golfin.Save.Tests | 44 |
| Golfin.Core.Stamina.Tests | 37 | Golfin.SceneSnapshot.Tests | 8 |
| Golfin.Course.Tests | 26 | **Golfin.Tournaments.Tests** | **209** |
| Golfin.Economy.Tests | 53 | **Golfin.TournamentsRuntime.Tests** | **61** |
| Golfin.EconomyRuntime.Tests | 6 | Golfin.UI.Rankings.Tests | 17 |
| Golfin.Gameplay.Tests | 302 | Golfin.UI.Shop.Tests | 8 |
| Golfin.HoleCompleteModal.Tests | 16 | Golfin.UI.Tests | 5 |
| Golfin.Net.Tests | 18 | GolfinRedux.Tests.EditMode | 36 |
| Golfin.Physics.Tests | 357 (+3 skip) | *(2 PlayMode asmdefs: no EditMode tests)* | — |

**0 failures in every assembly.** ⚠️ `tests-run` intermittently returns "No tests found" for a valid
assembly — it did so for both Tournaments assemblies on the first sweep pass and twice for the
unfiltered run. It is transient; retried, both returned real counts. Do not read that as zero tests.

New this iteration: **+13 tests** (1220 → 1233).

---

## 4. Evidence (no canonical screenshot — no new visual element)

1. **Live boot against the deployed endpoint** — CSV applied synchronously, then two `DEFERRED
   update` lines from real save entries, then `Schedule source: SERVER (live fetch). Tournaments=6`.
2. **Live allowlist table** — 11 cases against the shipped constant, each printing the normalized path.
3. **Art-cache probe** — disk HIT with zero network; `.tmp` survived the sweep; ended-tournament art
   evicted; live entry untouched. Probe artefacts deleted afterwards.
4. **Schedule cache on disk** — 3854 bytes, 6 slugs, `fetched_at` stamped, no `.tmp` residue.
5. **Tripwire runs** (§2).

Editor left clean: not playing, `ShellScene` not dirty, art-cache probe files removed.

---

## 5. Known limitations

- **`_placeholderImage` still unwired** (`{fileID: 0}`) — accepted in review §D as the correct
  default. The branch is near-unreachable (all six dashboard courses have bundled art).
- **No tournament currently has `banner_url` set**, so the download half of the art path has never
  run against a real object. Disk-cache, decode, allowlist, cap and sweep are all covered; the
  network fetch itself is exercised only by the 404/refusal paths.
- Out of scope per spec: entries, per-hole submission, leaderboards, server-side bot generation, the
  prize resolver, sponsor logos, any new playable course.
- **Standing caveat unchanged (SPEC §8.1):** only `lomond-country-club` has playable hole data and
  `HoleParProviderAdapter` ignores `clubId` — a tournament on Kawana still plays Lomond's holes.

---

## 6. Still needs a device

| # | What | Why |
|---|---|---|
| 1 | Brand tournament end-to-end | Needs a dashboard row with uploaded art |
| 3 | Airplane-mode cold launch | Proven in-editor against a failing host; device confirmation still wanted |
| 4 | Art cache hit across real launches | Needs real art on a real device |

Everything else is closed.
