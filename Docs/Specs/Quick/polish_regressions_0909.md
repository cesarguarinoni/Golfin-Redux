# Quick · `polish_regressions_0909` — three things Cesar saw after game_polish a/b/c landed

**Filed:** 2026-09-09 (Architect). **Reported by Cesar** after playing the post-polish build: (R1) the
gacha reveal is cut off about a second into the bag shake; (R2) the daily-mission card on Mission
Select visibly arrives "as a bubble from the left"; (R3) gacha banners published from the admin are
not showing up in the game — and he suspects more may be broken. **Order: R1 → R2 → R3**, each its
own commit. Every fix ships with the repro that proved it, not a guess.

## R0 · First, the sweep line the report never named

`Docs/Diagnostics/_capture/game_polish_c_final_tests.txt` (19:34 JST, 2026-09-08) reads
`passed=2885 failed=1` while `game_polish_c`'s report quotes 2884 / 0. Run the full EditMode sweep at
HEAD and quote the line; if anything is red, name it and say whether it is the terrain/raycast flake
(`RealHoleTerrainTests` / `PlacementSnapTests`, seen in the audit) or a real failure.

## R1 · Gacha reveal cut off after ~1 s of bag shake — **reproduce, then bisect at task granularity**

**Repro:** Rewards Center → any banner → PULL x1 (a real ticket; dev account has thousands). Record
the whole thing (`GachaRevealDemoRecorder` exists) and read the Console. Note exactly what "cut off"
is: (a) modal disappears and Prizes opens, (b) modal stays with a frozen bag and no cards, (c) cards
appear with no shake/pop. Quote any exception.

**Bisect, three checkouts, not a hunt:** the last known-good is `85b2365fb` (pre-track). Test at
`b2496871d` (a DONE), `9c3ae0daf` (b DONE), `2596639fa` (c). The reveal changed in b only
(`GachaRevealModalController.cs`: `StepEnter`/`StepShake`/`StepPop` → `UiMotion.Tween`; the modal's
`animateShow` flag went on; `GachaBannerCard.BeginPull` → `PendingSpend.BeginOn`, disposed in
`GachaPullFlow` when the server answers). Candidates, in the order I'd look:

1. **`Continue()`'s handover.** `WaitingSequence` loops `StepShake(tier, shakeFirst, white)` while
   `_waiting`; `Continue()` does `StopCoroutine(_sequence)` mid-`Tween` and starts `RevealSequence`.
   A stopped nested `UiMotion.Tween` is never settled (it was yielded, not `Run`), so `last`/`phase`
   state is fine — but check whether `StopCoroutine` on the OUTER handle actually stops the inner
   `Tween` enumerator Unity is driving, or whether the old tween keeps writing `_bagPivot` rotation
   under the new sequence (two writers → it can look frozen/jittery, and `StepPop` never appears to
   run).
2. **`animateShow` on this modal.** `Show()` now runs `UiMotion.Run(this, ref _panelMotion,
   Pop(modalPanel))`; `Hide()` runs `Then(Unpop, HideImmediate)`. If ANY path calls `Hide()` during
   the wait (an `Abort()` from a timeout, a second `Show()` re-entrance, `OpenModalCount`
   bookkeeping), the deferred `HideImmediate` deactivates the panel under the running sequence — the
   `Then` finalizer fires twice by design (b's own crash note). Log `Show`/`Hide`/`HideImmediate`
   with a stack trace for one pull.
3. **An exception inside a `Tween` `apply` callback** (`StepPop`'s four-quantity lambda, `StepShake`'s
   `_bagRays` rotation when rays are off) kills the coroutine with one red line — check the Console
   before anything else.
4. **`PendingSpend.BeginOn` disposal** re-enabling the banner buttons when the server answers — not
   the modal, but if the dispose ordering touches `GachaRevealModalController.Instance` (§D5 comment
   about the null-Instance degrade path), confirm the modal is not being `Abort()`ed on that path.

**Fix rule:** minimal, in the file that owns the bug, with the retrofit parity gate re-run
(`RetrofitParityRecorder` traces `gacha.enter` / `gacha.pop.curve` / `gacha.shake` must still read
fail 0 — a fix that changes the curve is a different task). Video of one full x1 and one x10 reveal
after the fix, captioned.

## R2 · Daily-mission card "arrives as a bubble from the left" — **remove the shimmer on that site**

**Cause (read, not guessed):** `game_polish_b` §D4 put a 978×374 `ShimmerBlock` host at sibling
index 1 under `MissionSelectionScreen/Content` (`GamePolishBuilder.ShimmerSites`,
`GameShimmerSites.MissionsDaily`) because the daily is "hidden until the server answers". The daily
is therefore **cold on every visit** (`_dailyGate.Cache(0)` → `IsCold` true), so the placeholder
shows on every entry for the ~0.2 s the fetch takes, its highlight band sweeps left→right across a
378 px-tall block, and the card then pops in over it. That sweep is the "bubble from the left".
Cesar's verdict: not nice. A placeholder for a 200 ms wait was never worth it.

**Fix:** delete the `MissionsDaily` shimmer site — remove it from `GamePolishBuilder.ShimmerSites`
and `GameShimmerSites.All`, remove the host from the scene object (re-run the builder's remove path
or `DestroyImmediate` via `SerializedObject`, quote the scene diff), drop the two `Shimmer(...)`
calls in `MissionSelectionScreenController` (`RefreshDaily`, `EndDailyWait`) and the `_dailyGate`
if nothing else reads it. When the daily arrives, the card **fades in** (`GpsPaintMotion.FadeInPanel`
/ `UiMotion.Fade` 0→1 over `FadeDur`, no slide, no stagger) — the same treatment as the b panels.
Tests: the b test that counts one host per site (`ShimmerHost`/builder test) updates to 5 sites.
Still of the daily card mid-fade + settled; `A5`-style rest parity on MissionSelection unchanged.

## R3 · Gacha banners not updating from the admin — **diagnose from the logs before touching code**

**Cesar's timeline (2026-09-09, overrides the ordering below):** banners WERE updating from the admin,
nothing changed admin-side, and the one thing that changed on that screen is the carousel-ring
side-request `8901e8f92` (`GachaCarouselController.cs` + a test file, nothing else). So the FIRST
test is the cheapest: the ring ships behind a serialized `_loop` bool ("can be turned off from the
Inspector without a code change") — flip it OFF on the live `GachaCarouselController`, publish a
banner change, reopen the Rewards Center. If banners update with `_loop = false`, the ring is
guilty and the suspect is what the ring does with `_cards`/`_currentOffset` across
`RebuildCarousel` (the re-base in `Update()` runs `_targetOffset -= turns * span` against a `Span`
that changes when the banner COUNT changes — a rebuild with more or fewer banners can leave
`_currentOffset` on a turn of the OLD span, so every card is placed at a "nearest copy" that is
off-screen while the dots and the count say the banners are there). Check `UpdateCardTransforms`'s
`targetX` for each card right after a rebuild with a different count. If `_loop = false` does
NOT bring the banners back, the ring is innocent and the log-driven list below applies.

Nothing in a/b/c touched the banner data path: `git log 85b2365fb..HEAD --
Assets/Scripts/UI/Gacha/GachaBannerModel.cs Assets/Scripts/ContentRuntime` is empty; the carousel
still calls `ContentService.RefreshNow()` + `GachaBannerCatalog.Reload()` + `RebuildCarousel()` in
`OnEnable` (ring commit `8901e8f92` changed positioning only). So the cause is on one of these
seams, and each one already logs:

1. **Withheld, not missing.** `GachaBannerCatalog` (§3.1 of `gacha_client_real_pull`) WITHHOLDS a
   banner whose refs fail validation and logs `[GachaBannerCatalog] N banner(s) withheld: <id> —
   <reason>`. A banner published against a `gacha_pools`/`gacha_rates`/`ticket_types` row that is
   deactivated, unpublished, or above this build's `min_build` is withheld silently from the
   player's point of view. **Read that line first.**
2. **The overlay never arrived.** `ContentService` fetches once at `Awake`, then `RefreshNow()` is
   throttled to once a minute (`ScheduleRefreshThrottle`) and the 5b re-install
   (`TryReinstallFromCache(GachaBanners)`) lands on the NEXT Rewards Center open. Log the fetched
   delta version vs the admin's published version of `gacha_banners`; quote both.
3. **`min_build` / `content_version.txt`.** The tree sweep for the punch-it-GPS build touched
   `Assets/Resources/Data/content_version.txt` (`36dc3d480`); rows whose `min_build` is above the
   running build are filtered. Quote the running build number and the new banner row's `min_build`.
4. **Admin side.** The dashboard changed under `gps_checkin` (venues panel, `registry.ts`,
   `types.ts`, `i18n.ts`) and `da_q9` published `modes`+`texts` through `contentMutations.ts`'s
   order. Confirm the admin's `gacha_banners` publish actually bumped the published version (the
   Content panel shows draft vs published) — a draft that was saved but not published is the
   cheapest explanation of all.

Report the cause with the log lines; **fix only if it is client-side** (server/admin causes go
back to the Architect with the evidence). Then, because Cesar fears collateral: one pass of the
`design_consistency_audit` route (`DesignAuditRunner` navigation) reading the Console for
NullReference / MissingReference on every screen and modal — quote the count (expected 0) and any
line that is not 0.

## Done when

- R0 sweep line quoted; R1 cause named with the repro video and the bisect result, fix committed,
  parity traces fail 0; R2 host gone (scene diff quoted), daily card fades, tests updated; R3 cause
  named from the log lines above (fixed if client-side), Console sweep count quoted.
- `git status` shows only the files each part names; nothing under `Gps/`, `UiMotion.cs` untouched.

## R4 · Gacha banner art is never bundled — and only shows up one launch late (added 2026-09-09, Cesar's question)

**Cause (read):** `Assets/Editor/ContentArtFetcher.cs` (`GOLFIN/Content/Fetch URL Art`, from
`content_art_bundling`, 2026-08-27) knows FOUR catalogs — `characters`, `items`, `balls`, `clubs`
(`static readonly CatalogSpec[] Catalogs`). The gacha catalogs landed four days later
(`gacha_admin_catalogs`, 08-31) and were never added, so `gacha_banners.artUrl` and
`ticket_types.iconUrl` are outside the bundler AND outside `GOLFIN/Content/Validate Catalog Art`
(its report in `Docs/Reports/content_art.txt` covers the same four). The repo CSV shows it:
`banner_test_a` / `banner_test_b` carry a real `artUrl` and still `artSprite =
GachaBanner_StandardClub1` — the only file in `Assets/Resources/Art/Gacha/Banners/`.

**Why it looks like "not updating":** `GachaBannerModel.cs:326-340` warms `CatalogArtCache` with
a fire-and-forget `Prefetch(artUrls)` at catalog load — "effective on the NEXT launch". So the
first launch after a banner (or re-uploaded art — the bucket filename is content-hashed, so
new bytes = new URL) shows the bundled placeholder; the real art appears the launch after, if
the fetch succeeded. Nothing rebinds the carousel when the download lands.

**Fix, two parts:**

1. **Bundle them.** Add two `CatalogSpec`s to `ContentArtFetcher.Catalogs`:
   `gacha_banners` (`Assets/Resources/Data/gacha_banners.csv`, id `bannerId`, slot `artUrl` →
   `artSprite`, folder `Art/Gacha/Banners`, name `GachaBanner_{Pascal(bannerId minus "banner_")}`
   — matches the one shipped file, add the rule to `ASSET_NAMING_CONVENTION.md` §5 in the same
   commit) and `ticket_types` (`Assets/Resources/Data/ticket_types.csv`, id `id`, slot `iconUrl`
   → `iconSprite`, folder `Art/Gacha/Tickets`, name = existing file convention in that folder —
   read it, quote it). Import settings copied from the sibling sprite as the fetcher already
   does; size appended to `content_art.txt`; `Validate Catalog Art` grows to 6 catalogs. Run it
   once on the two test banners: the diff is 2 PNGs + `.meta` + 2 CSV cells, then the importer →
   publish → export loop from `TESTFLIGHT_RUNBOOK.md` § "Art by URL: bundle it FIRST".
2. **Rebind when the download lands.** `TournamentArtService.CatalogArt.Prefetch` gets a
   completion callback (or the carousel subscribes to a `CatalogArtCache` "url cached" event);
   `GachaCarouselController` re-`Bind`s the affected card (not a full rebuild — the player may be
   mid-swipe) when a URL it drew as placeholder arrives. A newly published banner then shows its
   real art on the SAME launch, a few hundred ms after the placeholder. Log line per swap.

Done when: the two test banners render their uploaded art from `Resources/` on a fresh install
with the network OFF (bundled), and a third banner published mid-session shows its URL art on
the same launch with the network on (rebind), quoted with the log lines. Runbook step list updated
to name the six catalogs.
