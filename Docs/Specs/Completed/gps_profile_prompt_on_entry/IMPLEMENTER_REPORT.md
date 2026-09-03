# IMPLEMENTER_REPORT — `gps_profile_prompt_on_entry`

**Iteration shape:** `gps_auth_extras:trigger_placement`
**Iteration:** iter-1
**Canonical screenshot:** `screenshots/01_home_stays.png` (1170x2532 — the acceptance is that
Home comes up and STAYS, so the canonical frame is Home with the offer armed and untaken)

## What changed, in one paragraph

The once-per-device Golf Profile offer moved off Home and onto the first entry into the GPS
surface. `HomeScreenController` lost both call sites and the deferred coroutine; `ScreenManager.
Navigate` gained a single intercept that routes `ScreenId.GpsHub` to `ScreenId.GpsGolfProfile`
through the new pure seam `GpsAuthExtrasFlow.InterceptHubEntry(requested)`. Because the intercept
sits in `Navigate`, every entry point is covered by construction — the Home pill, the home_promo
banner's `golfin://gps` route, and (later) the standalone shell's boot, which will call the same
function. `ShouldOffer()` keeps its three inputs unchanged.

## Files modified or created

| File | Change |
|---|---|
| `Assets/Scripts/UI/Gps/GpsAuthExtrasFlow.cs` | Added `PendingHubEntry` (one-shot in-memory marker) and `InterceptHubEntry` — a public live seam plus an `internal` two-arg testable core. Rewrote the class docs: the trigger rationale is now the GPS entry, not Home. |
| `Assets/Scripts/UI/ScreenManager.cs` | One intercept in `Navigate`, after the three gates and before history bookkeeping. Re-enters `Navigate` with the new id (same shape as the existing AuthGate redirect) so `GpsGolfProfile` is gated on its own account rather than inheriting `GpsHub`'s verdict. Sets `PendingHubEntry` when it diverts. |
| `Assets/Scripts/UI/HomeScreenController.cs` | Removed both `GpsAuthExtrasFlow.ShouldOffer()` call sites, the `OfferGolfProfileNextFrame` coroutine and the `[HomeScreen] auth_golf_profile —` log line. A comment records why nothing replaces them. |
| `Assets/Scripts/UI/Gps/GpsWelcomeScreenController.cs` | Both exits clear `PendingHubEntry` (GET STARTED before it navigates, SKIP before it returns Home). Destinations unchanged. |
| `Assets/Tests/EditMode/GpsAuthExtrasFlowTests.cs` | Four new fixtures: the full intercept table, the intercept under every non-offering build state, the `PendingHubEntry` seam, and `Home_NoLongerCarriesTheOffer` (asserts the deleted coroutine stays deleted). |
| `Assets/Scripts/UI/Gps/Editor/GpsProfilePromptOnEntryRun.cs` (new, + `.meta`) | The real-navigation acceptance run: `GOLFIN/Gps/Run Profile Prompt On Entry Acceptance`. Boots the app, taps through the Splash gate, holds Home for 8 s with the offer armed, then drives the REAL pill's own `onClick`. |
| `Docs/GPS/GPS_DEVICE_PASS.md` | Rows 1.3 / 1.8 rewritten to the new trigger; new row 1.9 for the banner route; row 0.6 clarified. |
| `Docs/Specs/Active/gps_profile_prompt_on_entry/{STATUS,IMPLEMENTER_REPORT}.md`, `HEARTBEAT.log`, `screenshots/` | This report. |

Every uncommitted path outside this spec folder is listed above (Rule 13). The three pre-existing
dirty docs (`Docs/Reports/content_art.txt`, `Docs/TellCode.md`,
`Docs/Versioning/last_uploaded_build.txt`) were already dirty at kickoff — see the
`=== iter-1 kickoff baseline ===` block in `HEARTBEAT.log`, which quotes them verbatim. They are
not mine and were not touched.

## Acceptance checklist

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | Fresh Editor run with `gps_profile_prompted` cleared: Home comes up and STAYS | **PASS** | `[PromptOnEntry] reached Home. ShouldOffer=True` then `ACCEPT 1 PASS — Home held for 8.0 s with the offer armed; no GpsGolfProfile.` The hold matters more than the arrival: the old behaviour also reached Home and left one frame later, so the run fails on the first frame that is not Home. `ShouldOffer=True` proves the offer was live and declined by Home, not merely absent. `screenshots/01_home_stays.png` |
| 2 | Pill tap → Golf Profile → SAVE → Welcome → GET STARTED → hub, real navigation | **PASS** | The pill is resolved off `HomeScreenController`'s own serialized `gpsPillButton` field and invoked through its own `onClick` — no test-only button (PIPELINE_HARDENING rule 2): `tapping the REAL pill: Canvas/ScreensRoot/HomeScreen/GpsPill`. Then `ACCEPT 2 PASS — pill -> GpsGolfProfile`; `SAVE -> GpsWelcome OK; prompted flag now = True`; `ACCEPT 2 PASS (cont.) — GET STARTED -> GpsHub`. SAVE was a real `PUT /user/update` (`[GpsGolfProfile] saved: name='Cratilo' … colour=green`). `screenshots/02_golf_profile_via_pill.png`, `03_welcome.png`, `04_hub_after_get_started.png` |
| 3 | Relaunch + pill → hub directly | **PASS** | `second pill tap -> currentScreen=GpsHub (expect GpsHub, NOT GpsGolfProfile). ShouldOffer=False` → `ACCEPT 3 PASS`. Driven from Home so it is a real second pill tap, not a hub-to-hub no-op. `screenshots/05_hub_direct_second_entry.png` |
| 4 | Skip path: Golf Profile Skip → Welcome → Skip → Home; flag set; next pill tap → hub | **PASS** | Flag re-cleared, pill re-armed and re-offered, then `[GpsGolfProfile] skipped — no PUT /user/update issued.` → `Skip -> GpsWelcome OK; prompted flag now = True` → `ACCEPT 4 PASS — Skip -> Welcome -> Skip -> Home; PendingHubEntry cleared = True` → `ACCEPT 4 PASS (cont.) — next pill tap after Skip -> GpsHub` |
| 5 | `golfin://gps` / banner binder with the flag cleared → Golf Profile first | **PASS** | `BannerPolicy.TryGetInternalRoute("golfin://gps") -> True, GpsHub`, then that exact navigation → `ACCEPT 5 PASS — the banner deep link is covered by the same Navigate intercept.` `BannerSlotBinder.OpenLink`'s only action for an internal link is `ShowScreen(TryGetInternalRoute(link))`, so resolving the route and driving that navigation IS the banner path — there is no third step to fake. `screenshots/06_deep_link_offers.png` |
| 6 | "Punch it" build (gate off): `InterceptHubEntry(GpsHub)` returns `GpsHub`; Home never offers | **PASS** | `InterceptHubEntry_IsIdentity_ForEveryNonOfferingBuildState` walks all four `(signedIn × prompted)` combinations with `gpsEnabled=false` and asserts identity each time, plus the two live non-offering reasons inside a GPS build. `GpsGate.Enabled` is a const that is `true` in the Editor, so this is only reachable through the three-arg core — the same reason that overload exists. |
| 7 | EditMode: `GpsAuthExtrasFlowTests` updated; full sweep green, suites executed by name | **PASS** | Sweep: **2329 total / 2326 passed / 0 failed / 3 skipped** (the three skips are the pre-existing `HoleCompleteDriverTests` Stage-C1 skips). The runner ignores class filters and hides passes, so the four new fixtures were proven by a **tripwire**: an `Assert.Fail` in each produced exactly four failures, named `Home_NoLongerCarriesTheOffer`, `InterceptHubEntry_DivertsOnlyTheHub_AndOnlyWhenOffering`, `InterceptHubEntry_IsIdentity_ForEveryNonOfferingBuildState`, `PendingHubEntry_IsAPublicResettableFlag`. Tripwires reverted (`grep -c TRIPWIRE` = 0) and the sweep re-run green. |
| 8 | Rest-state parity untouched — no prefab change | **PASS** | `git status --porcelain --untracked-files=all -- Assets/Prefabs` is **empty**. No scene was saved: `scene-list-opened` reports `ShellScene IsDirty:false`, and `git status Assets/Scenes` is empty. No strings added (no `LocalizationManager.Get` key is new — the change adds no user-facing text), no backend change. |
| 9 | `Docs/GPS/GPS_DEVICE_PASS.md` §1 rows 1.3 / 1.8 updated | **PASS** | 1.3 now reads "Sit on Home for ~10 s, then tap the GPS pill … Home comes up and **STAYS**"; 1.8 now reads "…sit on Home, tap the pill again \| Home stays Home; the pill goes **straight to the hub**". Added 1.9 for the banner route (the other entry the intercept now covers) and clarified 0.6. |

No item is PARTIAL and none is FAIL.

## Two things worth flagging

**1. A real regression the first sweep caught, and the fix.** The intercept originally called
`GpsAuthExtrasFlow.InterceptHubEntry(screenId)` unconditionally, and that seam evaluated
`ShouldOffer()` eagerly. `ShouldOffer()` touches `AuthService.Instance`, whose getter *creates* a
`DontDestroyOnLoad` host — illegal outside play mode. `NavBackMemoryTests.A15` drives `Navigate`
reflectively from EditMode and died on it. `InterceptHubEntry` now tests `requested == GpsHub`
first and only reaches `ShouldOffer()` for a hub entry, which fixes the test and is also simply
better: an ordinary screen change no longer reads the session. The reason is written into the
method's docs so it survives the next refactor.

**2. Why the intercept re-enters `Navigate` instead of rewriting `screenId` in place.** The spec
says "after the gates". Rewriting the local would have carried `GpsHub`'s Demo/Gps/Auth verdicts
over to `GpsGolfProfile`, which is a different screen with its own allowlist entries. Re-entering
`Navigate` — the shape the existing `AuthGate` redirect already uses two blocks above — puts the
new id through all three gates on its own account. There is no recursion risk: only `GpsHub` is
ever intercepted, and `InterceptHubEntry_DivertsOnlyTheHub_AndOnlyWhenOffering` pins that,
including the case that would loop (`GpsGolfProfile` must map to itself).

## Needs manual on-device verification

Nothing here is Unity-unverifiable, but two things are worth a glance on glass because the
Editor cannot reproduce them:

- **The fade on Home → Golf Profile.** The boundary fade is `FadeController`'s, unchanged by this
  task, but the pill now sometimes ends on Golf Profile instead of the hub. Device-pass row 1.3.
- **A genuinely fresh install.** The Editor clears `gps_profile_prompted` via `ClearPrompted()`;
  only a **deleted** (not offloaded) app proves the real cold-start ordering. Device-pass row 0.6.

## Editor left clean

Play mode exited by the run itself; `ShellScene` is loaded and not dirty; no scene was saved; no
`GOLFIN_*` EditorPref left armed (`golfin.gpspromptonentry.armed` is cleared by `Pump` on the
first tick of the run). The device flag is left in the state a played-through player has
(`MarkPrompted()`), matching the "second entry goes straight to the hub" end state.
