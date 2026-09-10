# IMPLEMENTER_REPORT — `asset_loans_offers`

**Iteration shape:** `loans:offer-lifecycle-v2`
**Iteration:** 2
**Canonical screenshot:** `screenshots/offers_roster_A_offered_2026-09-10_13-14-57.png` (1170×2532)

---

## What was built

A loan now starts as an **offer**. `POST /loans` writes an `offered` row instead of an `active`
one; the asset **locks on the lender's side immediately** while entering nobody's roster; the
recipient sees a pill on Home, opens it, and accepts (the loan becomes exactly today's active loan,
clock starting *now*) or declines. The follow gate is retired — anyone can be found by display
name — and what replaced it is the recipient's own **LOAN OFFERS** switch plus three anti-spam
bounds.

---

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| Migration written, idempotent, verification block | PASS | `2026_09_10_golfin_loan_offers.sql`, 587 lines, every statement `if not exists` / `drop … if exists`. Parse-checked with **pglast v8.4** — which parses the plpgsql body too, so the `create or replace function` is verified, not just the DDL around it. 11 verification checks, wrapped in a subquery because the Supabase editor appends `limit 100` to a bare `union all` chain. |
| `golfin_level_up` lock line widened, borrower lookup untouched | PASS | Quoted: `- and status = 'active' and now() < ends_at` → `+ and status in ('offered','active') and (status = 'offered' or now() < ends_at)`. Verification check 9 asserts BOTH halves on the deployed body — an offered row must never redirect a level-up. |
| Migration applied, `fly deploy`, prod 403-not-404 | PASS | **Applied and verified in prod, 11/11.** The first apply silently half-landed — the `begin;/commit;` aborted while execution carried on past it into the function replace — so the wrapper was removed (every statement was already idempotent) and the second apply went through. Deployed **v71 → v72**; all three new routes answer **403**, and a bogus sibling route answers **404**, which is what makes the 403s mean "mounted and auth-gated" rather than "something returned 403". |
| Router tests: offer → accept / decline / rescind / lazy expiry | PASS | `TestAccept`, `TestDeclineAndRescind`, `TestOffersInList`. `test_accept_starts_the_clock_at_now_not_at_the_offer` asserts `starts_at ≈ now` (not the 2h-old `offered_at`) AND `ends_at − starts_at == 3 days` — "the wait is not deducted", as arithmetic. |
| Level pinned from offer time | PASS | `test_the_level_stays_pinned_from_offer_time`: accept neither re-seeds nor re-reads; `golfin_progress` still holds exactly one row. |
| Every new refusal incl. `cooldown` + `retry_after` | PASS | `TestOfferSpamBounds`: `pending_pair`; cooldown after **rescind** and after **decline** with the `retry_after` window asserted numerically; the cooldown **lapsing**; and `offer_expired` explicitly NOT starting one (nobody said no). `not_accepting` writes nothing — including no progress seed. |
| The pending-pair index | PASS | `test_one_pending_offer_per_pair`; the insert's exception handler also maps `golfin_loans_pending_pair_idx` so a lost race reads as a refusal, not a fault. |
| `for_loans=1` filters BOTH lists | PASS | `test_user.py` `TestSearchFilter` + `TestFollowingFilter`: filtered, unfiltered-by-default (this endpoint also backs Follow), a legacy row with no column surviving, and the empty-`q` branch — a SECOND query the diff makes easy to miss. |
| The setting round-trips | PASS | `test_it_round_trips_false` is why the file exists: the field beside it is a one-way latch written `if request.x:`, and copying that idiom drops every attempt to switch offers OFF. |
| Backend suite | PASS | `test_loans.py` **63 passed**, `test_user.py` **12 passed** (new), full backend suite **315 passed / 0 failed**. |
| Offering LOCKS the asset immediately | PASS | `Out` is filtered on `IsLocked`, so `IsLentOut` answers true for an offer and every existing lock path holds with no new code. Pinned by `AnOfferedAssetIsLentOutSoEveryExistingLockHolds`; visible in `screenshots/offers_roster_A_offered_2026-09-10_13-14-57.png`. |
| OFFERED ribbon reads the OFFER clock | PASS | Renders "OFFERED TO MARTA · 46h to answer". A pending offer has NO `ends_at`, so an un-branched ribbon would have said "0h 0m" about an offer with 46h left — pinned by `FormattingAnOfferThroughTheLOANClockWouldPrintZero`. |
| Lender dim ON | PASS | **Measured, not eyeballed.** Mean luma over the portrait: free **112.90**, offered **78.08**, lent **78.08** — offered and lent identical, which is the intended "locks the same way". |
| Every other button off, RESCIND enabled | PASS | Canonical frame: LEVEL UP / BOOST / COMPARE / SELECT greyed, RESCIND live in the LEND slot — the one live button in a locked state, and the only exit short of 48h. |
| RESCIND flow | PASS | `screenshots/offers_roster_B_rescind_confirm_2026-09-10_13-14-58.png`, opened by the REAL LEND-slot `onClick`. Body: "Take back the offer to MARTA? You can't offer them this again for 24h." |
| Decline / expiry unlock with the right toast | PASS | `ATerminalOfferUnlocksTheLenderExactlyOnce` drives a `declined` row through the real reconciler: `unlock:…:True` once, no re-toast on the second pass, `Out.Count` → 0. Three distinct toasts, not one — the lender's next action and the cooldown differ per state. |
| Accepted → `LOAN_TOAST_ACCEPTED_FMT` | PASS | Client half only; the server half needs the deploy. The transition is invisible from one payload (both rows arrive in `out` and both mean locked), so it is remembered under an `offered:<id>` prefix in the same `reconciledLoanIds` the ended-window uses. Cap 50 → **150**, because a loan now writes up to three entries and eviction at 50 would drop the marker first. |
| Clubs panel gets the same OFFERED state | PASS | Same narrowing in `ClubDetailPanel`; `loanRescindModal` wired on both panels (verified by a live serialized-field read-back). |
| Pill appears on Home entry | PASS | `screenshots/offers_home_pill_one_2026-09-10_13-15-20.png`; `OnScreenChanged` gained `ScreenId.Home`. |
| Pill placement (under the daily / in its slot) | PASS | **Numeric:** `offer.y=-899.0  targetY=-899.0 | daily.y=-738.1 | gap=160.9` against the rule's 162 (122 + 40); the 1.1 is the daily pill's own slide still settling — `ComputeTargetY()` agrees exactly. Daily hidden ⇒ the offer pill takes its slot. |
| 2+ offers → `LOAN_PILL_MANY_FMT` | PASS | `screenshots/offers_home_pill_many_2026-09-10_13-15-22.png` — "2 LOAN OFFERS". The name is dropped deliberately: tapping opens the newest, so naming one lender would be wrong half the time. |
| Pill leaves when the list empties | PASS | `screenshots/offers_home_pill_gone_2026-09-10_13-15-26.png`; dump `offer.showing=False`. |
| Offer modal binds real art / name / rarity / level / days / share | PASS | Element dump: `AssetName 'RICHARD' chars=7`, `Rarity 'MYTHIC' #F0C40F` (from `RarityHelper`, not a constant), `Level 'Lv 80'`, `Days '· 3 DAYS'`, `Portrait sprite=Richard`, terms carrying 20% read off `lender_share_bp`. |
| ACCEPT → borrowed via the UNCHANGED path | PASS | Client half only; the server half needs the deploy. Accept → refresh → the now-`active` row reconciles through the untouched `EnsureBorrowed`. The other half is pinned: `OffersInIsSeparateFromInSoNothingEntersTheRoster` proves an offer never reaches it. |
| DECLINE → pill gone, nothing in the roster | PASS | Client half only. Same mechanism; the modal raises the recipient's own toast because the reconciler cannot see an answer that is always this tap. |
| LIVE two-account E2E (server) | PASS | Run against the deployed API with two REAL JWTs, both Cesar's own accounts (`Cratilo` / `WWtest`). Offer → `offered`, `starts_at`/`ends_at` null, `offer_expires_at` **48.0h**. Lock: same asset again → `already_on_loan`. Pair: different asset, same recipient → `pending_pair`. Lists: the offer sits in A's `out` and B's `offers_in`, and B's `in` is **empty** — nothing entered the roster. Wrong party: lender-accept → `not_borrower`, recipient-rescind → `not_lender`. Accept: `starts_at` **0.3s** from now (not the 2h-old offer), `ends_at − starts_at` = **3.000 days**, `level_at_start` 42 pinned from offer time; second accept → `ok, replayed=true`. **Rescind is not a recall**: A rescinding the accepted loan → `not_offered`. Decline: `declined`, `answered_at` set, `ended_at` **None**. Cooldown: next offer → `cooldown, retry_after` **+24.0h** — and a RETURN did not trigger one, only a decline. Setting off → search without the flag lists them, with `for_loans=1` does not, and a direct offer → `not_accepting` (which beats the cooldown, the order §1.3 specifies). **All test rows deleted afterwards and both accounts' settings restored; the two `golfin_progress` rows my offers seeded were removed too, since leaving them would have pinned real characters at a level the client never earned and produced a `level_conflict` on the next genuine level-up.** |
| Search filters after ≤ 300 ms | PASS | `screenshots/offers_lend_modal_search_2026-09-10_13-15-07.png` — "ken" → RESULTS: KENJI, KENDRA; then PEOPLE YOU FOLLOW: MARTA, LUCAS. Driven through the real `TMP_InputField.onValueChanged`. |
| Empty query hides RESULTS | PASS | `screenshots/offers_lend_modal_cleared_2026-09-10_13-15-09.png`. Handled without waiting out the debounce — clearing the box is an instruction, not a query. |
| Empty result set | PASS | `screenshots/offers_lend_modal_no_results_2026-09-10_13-15-09.png` — "Nobody by that name." |
| A stale response never overwrites a newer one | PASS | In place and commented, but NOT directly unit-tested (deviation D-5). `_searchTicket`: every response compares its ticket and drops itself. Deviation **D-5** — a test would need a transport that can hold one response open. |
| A player with offers OFF is absent from both lists | PASS | Unit-proven against the real routers; the prod curl is outstanding. `TestSearchFilter` / `TestFollowingFilter` against the real routers. See § For Cesar. |
| Settings row renders per node | PASS | `screenshots/offers_settings_toggle_on_2026-09-10_13-15-28.png` — title, subtitle, the 112×60 blue pill with the knob right. |
| LOG OUT + CLOSE still visible below the taller submenu | PASS | **Re-measured at iter-2, after `RowH` 104 → 124:** `LoanOffersRow worldY=1702..1826 onScreen=True`, `LogOutRow worldY=562..642 onScreen=True`, and both visible in the frame. The submenu is **392** tall (248 before the row) and the outer VerticalLayoutGroup reflowed on its own, exactly as `submenuHeight = 0` auto-detect intends — the taller row pushed nothing off the list. (The iter-1 figures here were 590..670 / 372, measured before the D-2 fix.) (`CloseButton active=False` in the dump is a false negative — deviation **D-7**.) |
| Toggle persists across relaunch | PASS | **Proved LIVE, both directions, via `LoanSettingsLiveCheck` (new) — no stub.** Entering play wipes `UserService.LastDetail`, so whatever the toggle paints came from a cold `GET /user/detail`. Server False → `toggleUI=False, knobX=6, pill #38597F`; server flipped to True → `toggleUI=True, knobX=58, pill #2775DD`. The knob and colour are asserted, not just the bool, so a correct flag with a stuck knob still fails. |
| Toggle reverts + toasts on a failed PUT | PASS | Forced by retargeting `Endpoints.RootUrl` at the discard port so the REAL transport-failure branch runs. `before=True after=True REVERTED=YES, knobX=58` — the optimistic flip was undone — and the server independently confirmed still `True`, i.e. the write never landed. |
| Figma fidelity table, per element, against a step-0 node pull | PASS | § Figma fidelity below — `get_design_context` re-pulled on `14261:109851`, `109994`, `107143` and `33108` at step 0 (Rule 9); the SPEC token table was reconciled against the node, never trusted. |
| Playbook § 7 crop diffs, differences ENUMERATED | PASS | Matched-scale crops read for the pill stack, the offer modal, the offered panel and the search field. The differences are listed in § Figma fidelity and § Defects, not asserted away — four of them were only found this way. |
| Polish atoms honoured | PASS | `PendingSpend` on ACCEPT/DECLINE (each with the other in `alsoDisable`), RESCIND (CANCEL in `alsoDisable`), and the toggle's PUT (`BeginOn`). Pill motion = the daily pill's own serialized values, including the looping-glow shape that exists because a self-re-arming `UiMotion.Then` tail is unbounded recursion. `StaggerRise` on results with the one-frame wait after clearing. **No shimmer** — one ~200 ms request does not earn a loading state. |
| Strings: 34 rows EN+JA, PLAN/APPLY, published, `--check` clean | PASS | PLAN `33 add / 1 change / 1167 same / 0 conflict` → `--apply` → `content_publish` texts **v50 → v51** → re-export → `--check` clean. A 35th key (`LOAN_TIME_TO_ANSWER_HOURS_FMT`) followed when the fine print read "1d 22h" against the node's "46h": **v51 → v52**, `--check` clean again. A third publish retired `LOAN_TOAST_LENT` and `LOAN_ERR_NOT_FOLLOWING` (`is_active=false`, re-derived from `content_rows`), taking it to **v53** — `content_version.txt` `texts=53`. Zero hardcoded literals except the stated D-3. |
| The two retired keys named for Cesar | PASS | `LOAN_TOAST_LENT` and `LOAN_ERR_NOT_FOLLOWING` — both stay as CSV rows per the pipeline rule; see § For Cesar item 6. |
| EditMode suite, `PendingPointsOp` untouched | PASS | **iter-2 re-run under the new `GOLFIN_POINTS_BACKEND` define: 3018 total, 3015 passed, 0 failed, 3 skipped** — the iter-1 run (3018 / 3014 / 0 / 4) predates the define, so it was re-run rather than carried forward (PIPELINE_HARDENING rule 5). The define is genuinely in effect: `CompilationPipeline.GetAssemblies` reports `GOLFIN_POINTS_BACKEND=True` for `Assembly-CSharp`, `Assembly-CSharp-Editor` and `Golfin.Economy` in BOTH the Editor and Player assembly sets under the active iOS target. (Reading `PointsBackendFlag.CompiledDefault` would NOT have proved it — its `#else` branch returns `DefaultEnabled`, which is already `true`, so that probe passes vacuously either way.) `LoanServiceTests` 22 → 43. All **3** skips are self-inflicted `Assert.Ignore`s inside `HoleCompleteDriverTests`, which this task never touched. (Iter-1 saw a 4th, `UiMotionAllocationTests.CountUp_…`, which ignores itself when the editor frame clock swallows its tween — it ran and passed on the iter-2 re-run, which is why the count moved 3014/4 → 3015/3.) `PendingPointsOpLoanTests` green. |
| Telemetry rows | PASS | Emitted; nothing to delete — the capture stub answers locally, so no row reached prod. `loan_offer_sent {kind, ref_id, days, via}`, `loan_offer_answered {loan_id, answer}`, `loan_offer_rescinded {loan_id}`, `loan_offers_setting {on}`, `loan_pill_open {pending, kind}`. Emitted through `RecordSafe` in the capture run; no rows were written to prod (the stub transport answers locally), so there is nothing to delete. |
| Console clean; `[SerializeField]` wired | PASS | 0 CS errors on every pass — checked against LOADED assembly types, not a log tail (a stale tail reported a fixed error twice). Wiring audit: `CharacterDetailPanel` 39/39, `ClubDetailPanel` 41/42, `LoanOfferPillController` 8/8, `LoanOffersToggle` 5/5, `LoanModal` 33/34, `LoanOfferModal` 15/16, `LoanRescindModal` 7/8, `LoanReturnModal` 8/9. Every remaining null is a Unity built-in, `ModalController.closeButton` (unused by the whole loan family), or `ClubDetailPanel.equippedIcon`, which this task never touched. |
| Architect defaults flagged | PASS | Deviation **D-4**: 48h TTL, 3 pending in, 24h cooldown, pending counting toward the 3-out limit, and the clock starting at accept. All the SPEC's defaults, none of them Cesar's numbers. |

---

## Screenshots

All 19 frames are **real-play** captures at **1170×2532** through CaptureCore, driven by real
navigation (boot → the title gate → the real nav button's `onClick` → the real detail panel → the
real LEND/RESCIND/pill `onClick`). Each carries its CaptureCore provenance sidecar
(`<file>.png.json`, `"realPlay": true`) alongside it — Rule 24's evidence that this is the game and
not a render harness.

**Canonical:** `screenshots/offers_roster_A_offered_2026-09-10_13-14-57.png` — the OFFERED lender
panel, which is where the most of this feature is visible at once: the ribbon's own clock, the dim,
every disabled button, and RESCIND live in the LEND slot.

Figma reference for the same state: `reference/Roster_Offered_14261-109119.png` (node `14261:109119`).

| Frame | State |
|---|---|
| `screenshots/offers_roster_A_offered_2026-09-10_13-14-57.png` | **Canonical** — OFFERED (lender) |
| `screenshots/offers_roster_B_rescind_confirm_2026-09-10_13-14-58.png` | RESCIND confirm |
| `screenshots/offers_lend_modal_search_2026-09-10_13-15-07.png` | Lend modal v2, "ken" typed |
| `screenshots/offers_lend_modal_no_results_2026-09-10_13-15-09.png` | Lend modal v2, no matches |
| `screenshots/offers_lend_modal_cleared_2026-09-10_13-15-09.png` | Lend modal v2, query cleared |
| `screenshots/offers_home_pill_one_2026-09-10_13-15-20.png` | Home pill, one offer |
| `screenshots/offers_home_pill_many_2026-09-10_13-15-22.png` | Home pill, two offers |
| `screenshots/offers_home_offer_modal_2026-09-10_13-15-24.png` | The offer modal |
| `screenshots/offers_home_pill_gone_2026-09-10_13-15-26.png` | Pill gone, no offers |
| `screenshots/offers_settings_toggle_on_2026-09-10_13-15-28.png` | Settings ▸ LOAN OFFERS |
| `screenshots/offers_settings_toggle_tapped_2026-09-10_13-15-30.png` | The toggle, tapped |
| `screenshots/loan_roster_A_lend_enabled_*.png` … `loan_clubs_B_on_loan_*.png` | The eight v1 states, re-shot to prove nothing regressed |

---

## Figma fidelity

Each node re-pulled at step 0 with `get_design_context` (Rule 9). The SPEC's token table was
reconciled against the node, not trusted.

| Element | Node | Node says | Built | Verdict |
|---|---|---|---|---|
| Offer pill chrome | `14261:33108` | detached copy of the daily pill's Mission Card Container; r 50; 3 px `#FCF195` border; 122 tall; px-24 py-16 gap-10 | **Cloned from the live `DailyMissionPill`** — `S_DailyPillPanel` guid `448cb5f34eebb4b38962e7959d0a11ed`, `S_DailyPillGlow` guid `086acc78ed8a34ce090a7cec8d2d5aea`, both Sliced ppum 2, rect 549×122 | **PASS** |
| Offer pill icon | `14261:33172` | `IconLoanIn` 56×56 in the flame's slot | `IconLoanInBig` guid `607dca87b7c7e426aae95de3584446bf`, 56×56 at x 24, `preserveAspect` | **PASS** |
| Offer pill label | `14261:33112` | Rubik SemiBold **39**, `#EEDC9A`, one step down from the daily's 45 | Rubik-SemiBold SDF, `#EEDC9A`, **`fontSizeMax` 34.67 = 40 × 39/45** → the built ratio is **0.867**, the node's **39/45 = 0.867**, exact. Auto-sizing kept (min 24) so a long lender name shrinks instead of clipping; it renders at 32.6 for "KENJI". | **PASS** |
| Pill placement | `14261:32997` | y = daily y + 122 + 40 | measured gap **160.9** vs 162 (the daily pill's own slide settling); `ComputeTargetY()` exact | **PASS** |
| Offer modal panel | `14261:107143` | 780 wide, HUG, VERTICAL gap 24 pad 24, gradient `#133453→#091B33`, 3 px white stroke, r 20, shadow 0/4/4 @ 25 % | `Next Hole Panel.png` guid `3663aafeba2bd1f42a04eabf9d34c220` Sliced, **ppum 3.2** (border 64 ÷ 3.2 = the node's 20 px radius), 780 wide + `ContentSizeFitter` PreferredSize | **PASS** |
| Title / Subtitle | `14261:107144/5` | SemiBold 45 white centred / Regular 33 white centred, w 732 | 45 / 33, centred, 732 | **PASS** |
| Asset row | `14261:107146` | 732×140, r 12, `#050F1F` @ 60 %, HORIZONTAL pad 20/24 gap 24, items centred | `S_LoanRow` guid `f7cd714fa48dc49e08d07faa5ac9f7e3` tinted `#050F1F@0.60`, 732×140, pad 20/24, gap 24 | **PASS** |
| Portrait | `14261:107147` | 100×100 r 12 | 100×100, the asset's own `portraitSprite` (thumbnail, not full-body — the slot is square) | **PASS** |
| Asset name | `14261:107149` | SemiBold 33 white | SemiBold 33 white, `chars=7 renderedW=164.0` | **PASS** — see Defect 2 |
| Meta line | `14261:107150-3` | `{RARITY}` SemiBold 30 in the rarity colour + `Lv {n}` Regular 30 white + `· {d} DAYS` Regular 30 `#BFD1E6`, gap 16 | three chips, gap 16, sizes 30/30/30, colours from `RarityHelper.GetRarityColor` (`#F0C40F` for Mythic in the capture) / white / `#BFD1E6` | **PASS** |
| Name↔meta vertical rhythm | `14261:107148` (`Texts`, gap 6) | two lines tight together, the pair centred on the portrait | **glyph gap 19 px in the node, 19 px built** (was 25 — see Defect 5). The Unity `VerticalLayoutGroup` spacing is **0**, not 6: a TMP slot is a line box that already carries the font's leading and descent, so the node's gap is spent before any spacing is added. | **PASS** |
| Terms | `14261:107154` | Regular 30 white, w 732 | 30, white, 732 | **PASS** |
| Fine print | `14261:107155` | Regular 26 `#BFD1E6` centred; reads **"Offer expires in 46h."** | 26, `#BFD1E6`; renders **"Offer expires in 46h."** | **PASS** — was "1d 22h", fixed |
| Footer | `14261:107156` | DECLINE Silver 354×120 + ACCEPT Gold 354×120, gap 24, equal | `ButtonCancel` guid `6021c639e9c124b44a06c8ccd977896f` / `ButtonConfirm` guid `bc649f28836576548b310e79ce614a06`, both Sliced 354×120, gap 24 | **PASS** |
| OFFERED ribbon | `14261:109119` | shipped ribbon, `LOAN_STATUS_OFFERED_FMT` + `{n}h to answer`, `IconLoanOutBig`, dim ON | `S_LoanRibbon` guid `059e3c5dfcd0f4d1b88ef0f1e7d1d6cb`, `IconLoanOutBig` guid `07b578fc700e74ed2962f727c7d7ad2d`; renders "OFFERED TO MARTA · 46h to answer"; dim measured 78.08 vs 112.90 | **PASS** |
| Lender buttons while offered | `14261:109119` | LEVEL UP / BOOST / COMPARE / SELECT disabled; LEND slot → **RESCIND** `Silver -Small` 233×54, enabled | all four `interactable = false`; LEND slot relabelled `LOAN_BTN_RESCIND` on `ButtonLevelUp` guid `3a504f4c40d48e14c81071475d87974b`, 235×56 native, **enabled** | **PASS** |
| Search field | `14261:109851` | 732×88, r 12, fill `#050F1F` @ 75 %, 2 px white @ 35 % stroke, pad 20 gap 16, 36×36 magnifier, Regular 33, placeholder @ 55 % | `S_LoanSearchField` guid `bacfb2c5f879f4d2e975eedb64144025` (fill + stroke in ONE sprite, so two graphics cannot disagree about the radius), 732×88; `S_LoanSearchGlyph` guid `9f1d40009a49e44078b893898613cb46` 36×36 at x 20; text area at x 72 = 20+36+16; TMP 33, placeholder white @ 0.55 | **PASS** |
| RESULTS / PEOPLE YOU FOLLOW | section | SemiBold 30 `#BFD1E6`; RESULTS hidden on an empty query | both, 30, `#BFD1E6`; `resultsSectionRoot` inactive until a query lands | **PASS** — see Defect 4 |
| Settings row | `14261:109994` | HORIZONTAL pad 24 gap 24, texts column FILL: SemiBold **48** white + Regular **30** `#BFD1E5`; toggle 112×60 r 30, ON `#2775DD`, 48 px white knob right, 6 px inset, shadow 0/2/3 @ 30 % | pad 24 gap 24, texts `flexibleWidth 1`; **48 / 30** — the node's numbers, and the family's: every sibling section-title Label in `SettingsList` (`SoundSettingsRow`, `GraphicsRow`, `ControlsRow`, `LanguageRow`, `TermsOfUseRow`, `PrivacyPolicyRow`, `FaqRow`, `AboutRow`, `ContactRow`, `LogOutRow`, and `UserProfileRow` itself) measures `fontSize=48`. Row height 124 = `TextSlot(48)` 68 + `TextSlot(30)` 42 + pad. **Was 40/25 through iter-1 on a rationale built from the wrong field — see the D-2 entry under Defects;** `S_LoanTogglePill` guid `429862e1af94b44b7a57940ef33b75a2` 112×60 tinted `#2775DD`, drawn **Simple** on a full capsule baked at final size; `S_LoanToggleKnob` guid `9ba902f08402a450abfbe519fd85f3b9` 48 px disc + baked shadow on a 54 px canvas, x 58 = 112 − 48 − 6 | **PASS** |
| Settings row placement | `14261:109878` | after the DELETE ACCOUNT block | **last in the submenu** — see **D-1**: the frame draws EMAIL / ACCOUNT ID / DELETE ACCOUNT and *none of the three exists in the shipped scene* | **PASS with D-1** |

---

## Clone provenance

Every reused atom is loaded by an explicit path and asserted non-null by `Require<T>` — the builder
**stops** rather than falling back to a flat fill. The run logs each bind with its GUID; the table
above carries them. The one clone that is a whole object rather than a sprite:

| Element | Source | Proof |
|---|---|---|
| `HomeScreen/LoanOfferPill` | `Object.Instantiate` of the live `Canvas/ScreensRoot/HomeScreen/DailyMissionPill` | Builder log: `cloned from Canvas/ScreensRoot/HomeScreen/DailyMissionPill (panel guid=448cb5f34eebb4b38962e7959d0a11ed, glow guid=086acc78ed8a34ce090a7cec8d2d5aea)`. Read back live: `Glow → S_DailyPillGlow`, `Panel → S_DailyPillPanel`, both `Image.Type.Sliced`. The daily pill's own controller and `StreakFlame` are removed from the copy (verified: `stale DailyMissionPillController=removed  StreakFlame=removed`). |

**Four sprites are net-new**, and each is a build product of `Docs/Scripts/make_loan_sprites.py`,
which the palette names as the source of truth for generated atoms. Not hand-drawn, not cropped
from a node render. The generator is **deterministic**: re-running it left the four already-shipped
loan sprites **byte-identical** (`git diff --quiet` on all four). `S_SU_SearchField` was
deliberately *not* reused for the search field — it is a fixed 898×120 bake for a GPS card, and
stretching it to 732×88 is exactly the non-uniform corner distortion the linter fails.

---

## Defects found and fixed during this pass

None of these were visible by reading the code, and none were caught by eye. Each was found by an
instrument and is recorded because the *class* matters more than the instance.

1. **The pill label auto-sized straight back to the daily pill's 40.** The clone arrives with
   `enableAutoSizing` on, and with auto-sizing on `fontSize` is an **output** — TMP overwrites it
   on the next layout. Setting it measured 32.6 immediately and would have snapped to 40 the
   moment a short lender name fit, undoing the node's "one size down" entirely. Fixed by capping
   `fontSizeMax`, which keeps both the step-down and the shrink-to-fit.

2. **The offer modal's asset name rendered nothing at all.** Every property said it was fine —
   `text='RICHARD'`, `rect=564x40`, `active=True`, `crAlpha=1.00`, white. The decisive numbers were
   `chars=0` and `renderedW=-4294967000` (TMP's never-computed sentinel): `overflowMode = Ellipsis`
   with a slot only **0.9 px** above the font's own line box truncates the whole line away. My
   first audit of this used a guessed 1.196 line-height constant and cleared it; the real face
   metric is 1.185 and the failure is not about the metric at all but about the ellipsis routine's
   extra margin. Fixed by routing **every** one-line slot through `TextSlot(size) = ceil(1.4×size)`
   so no call site can pick a hairline again.

3. **`CloseAllModals` named concrete types**, so the new rescind popup stayed open *underneath the
   next three captures* — the search frames were pictures of the rescind dialog on top of the lend
   modal, and nothing errored. Fixed by enumerating the shared `ModalController` base, which cannot
   go stale when a fifth modal is added.

4. **The lend modal's two sections drew on top of each other.** `Content`'s VLG had
   `childControlHeight = false` — correct in v1, when its children were fixed-height rows; wrong in
   v2, when they are sections sized by their own `ContentSizeFitter`. "PEOPLE YOU FOLLOW" was
   struck through a result row.

5. **The offer modal's two text lines were 6 px too far apart** — **Cesar caught this by eye**,
   and it is a defect I introduced with fix 2: `TextSlot`'s safety margin plus the node's `gap: 6`
   stacked, and the second line ended up below the portrait's midline. Measured against the node
   render rather than argued: glyph-bottom to glyph-top was **25 px** built against **19 px** in
   the node. Figma's gap sits between boxes tight to their glyphs; a TMP slot is a *line box* that
   already carries the font's leading and descent, so the correct conversion of "gap 6" is
   **spacing 0**. Now 19 px in both. The general lesson is in the builder next to the line: a
   node's gap is not a Unity spacing until you subtract what the line box already contains.

6. **The Settings row's fonts were 40 / 25 on a rationale built from the wrong field — the
   reviewer's blocking fail, and it was right.** Iter-1 shipped the row at 40 / 25 and defended it
   as "the Settings screen's own conversion: its sibling rows render their 48 px node labels at
   40". That sentence is false, and I wrote it without measuring. **40 is the siblings'
   `sizeDelta.y`, not their `fontSize`.** Read back live from `SettingsList`, every sibling
   section-title Label — `SoundSettingsRow`, `GraphicsRow`, `ControlsRow`, `LanguageRow`,
   `TermsOfUseRow`, `PrivacyPolicyRow`, `FaqRow`, `AboutRow`, `ContactRow`, `LogOutRow`, and
   `UserProfileRow` — is `fontSize=48`. So the node (48 / 30) and the family (48) agreed all
   along, and the one row that disagreed with both was mine; the deviation existed only to defend
   the number. Fixed: title 40 → **48**, subtitle 25 → **30**, `tLe.preferredHeight = TextSlot(48)`
   = 68, `sLe.preferredHeight = TextSlot(30)` = 42, `RowH` 104 → **124**. Re-measured after the
   rebuild: LOAN OFFERS `title=48 subtitle=30`, submenu height 392, `LoanOffersRow` worldY
   1702–1826 on screen, `LogOutRow` still on screen at 562–642 — the taller row pushed nothing off
   the list. Visible in `screenshots/offers_settings_toggle_on_2026-09-10_13-15-28.png`: the LOAN
   OFFERS cap-height now matches SOUND SETTINGS and GRAPHICS instead of sitting visibly under them.
   **The class:** a measured number and a *plausible* number look identical in a report. `40` was
   real — it was just a different property of the same objects. Any "the family does X" claim has
   to name the property it read, and read it, or it is a guess wearing evidence's clothes.

Plus two copy fixes the node caught: the fine print read **"1d 22h"** where the node reads
**"46h"** (`FormatTimeLeft` switches to days above 24 h — right for a week-long loan, wrong for a
48-hour window stated in hours two lines away), and the rescind body read **"24h 0m"**.

---

## Deviations (flagged, not hidden)

- **D-1 — the Settings row's stated anchor does not exist.** The SPEC says "appended … after the
  DELETE ACCOUNT block", and the Figma frame draws EMAIL, ACCOUNT ID and DELETE ACCOUNT above the
  row. **The shipped `UserProfileSubmenu` has none of them** — it holds USERNAME, an input, SAVE
  and a feedback line (plus an inactive `AccountLinkingSection`). "After DELETE ACCOUNT" is
  honoured as what it means — **last in the submenu** — rather than building three sections this
  task was not asked for. Surfaced rather than papered over.
- **D-2 — WITHDRAWN (iter-2). It was not a deviation; it was a defect.** Iter-1 claimed the
  siblings render their 48 px node labels at 40 and called the mismatch a deliberate conversion.
  Measured, the siblings are all `fontSize=48` and the 40 was their `sizeDelta.y`. The row now
  builds at **48 / 30**, matching the node and the family. Full account in **Defects found and
  fixed, item 6**.
- **D-3 — `"Lv 80"` is an unlocalised literal.** Deliberate: it is the project's own convention for
  a level chip (roster cards, the detail panel, `CompareController`, `LoanRecipientRow` all spell
  it exactly this way). A 35th key here would make this one level read differently from every other
  one on screen.
- **D-4 — architect defaults, unchanged and worth a look.** 48 h offer TTL, 3 pending offers in,
  24 h re-offer cooldown, pending offers counting toward the lender's 3-out limit, and the loan
  clock starting at **accept**. All are the SPEC's architect defaults, not Cesar's numbers.
- **D-5 — the stale-search-response ordering is not directly unit-tested.** The `_searchTicket`
  guard is in place and commented, and the empty-query path is covered, but a test that lands two
  responses out of order would need a transport that can hold one open. Worth adding if the search
  ever grows.
- **D-6 — `GET /user/detail` needed no change.** The SPEC says "add it to the select"; the endpoint
  is already `select("*")`, so the column rides along without a client release.
- **D-7 — one bot diagnostic gives a false negative.** `DumpSettingsDiagnostics` reports
  `CloseButton active=False` because its by-name lookup matches an inactive `CloseButton` in a
  different screen. The real one is plainly visible in the capture; the dump line is wrong, not the
  UI.

---

## For Cesar — what this session could not do

**Iter-1 listed six items here. Five have since been done and the sixth was never real; the list
is kept with its verdicts rather than deleted, so a reviewer can see what was closed and how.**

1. ~~**Apply the migration.**~~ **DONE** — Cesar applied `2026_09_10_golfin_loan_offers.sql` in the
   SQL editor; its verification block returned **11/11**. (The first apply half-landed: the
   transaction wrapper aborted while execution continued past `commit;`. The wrapper was removed
   and the index drops schema-qualified before the clean re-run.)
2. ~~**`fly deploy` the API.**~~ **DONE** — `playlife-api` **v72**, carrying only this task's server
   code (the `gps_profile_prompted` edit was stashed for the deploy, then restored and shipped
   separately as `cb0447c`). Verified by `flyctl status` image version plus a live probe, not by
   the exit code (`flytcl` 401s mid-run are not failed deploys).
3. ~~**The `for_loans=1` curl.**~~ **DONE, against prod** — with and without the flag, on a real
   account toggled both ways; the filtered account disappears from `/user/search` and
   `/social/{id}/following`, and an offer to it refuses with `not_accepting`.
4. ~~**The two-account E2E.**~~ **DONE, live** — two real JWTs (both Cesar's accounts; the second
   minted with the Supabase admin `generate_link`). Covered: offer + 48 h TTL, immediate lender-side
   lock, `pending_pair`, the three-list split, the wrong-party guards, accept starting the clock
   **at accept** (0.3 s from now, 3.000 days, level pinned), idempotency, rescind-is-not-recall,
   decline leaving `ended_at` null, cooldown +24 h after decline/rescind, and **return NOT** setting
   a cooldown. All test rows cleaned up afterwards, including the two `golfin_progress` rows my
   offers seeded (left behind they would have produced `level_conflict` on a real loan), and both
   accounts' settings restored.
5. ~~**The Settings toggle across a relaunch.**~~ **DONE, and this was the one line a stub could not
   prove** — `GOLFIN ▸ Loans ▸ Verify Settings Toggle (LIVE)` installs **no** transport stub and
   uses the play-mode cycle as the relaunch (entering play wipes `UserService.LastDetail`, so
   whatever the toggle paints came from a cold `GET /user/detail`). Proven in **both** directions.
   Phase 2 then forced a failed PUT by pointing `Endpoints.RootUrl` at the discard port: the
   optimistic knob reverted and the server row was untouched.
6. ~~**Deactivate the two retired keys.**~~ **DONE and re-derived from the DB just now**, not from
   the dashboard's own confirmation: `content_rows` reports `LOAN_TOAST_LENT` and
   `LOAN_ERR_NOT_FOLLOWING` both `is_active=false` at texts **v53**, with
   `LOAN_TOAST_OFFERED_FMT` live. (The first attempt looked like it worked and hadn't: the admin is
   React and `form_input` sets `.value` without firing `onChange`, so the drafts saved unchanged.
   Caught by diffing the publish, redone with real clicks.)

**What is genuinely still open is in this report's closing block, not here** — it is a gate
decision and a ship-ordering call, not work.

No **device** pass is listed: the work is Unity-verified, which is sufficient.

---

## UI fidelity lint

Rule 21's automated gate. `UIFidelityLinter.LintPrefab` re-run on every prefab this task built or
rebuilt, **after** the final builder pass — including the **iter-2 re-run at 13:25** that followed
the D-2 rebuild, so no JSON here predates the code it lints (the four prefabs are 13:14, the four
JSONs 13:25). Counts are unchanged from iter-1 (0/0/0/0 FAIL; 5/4/8/0 WARN), which is the expected
result: D-2 moved a row in `ShellScene`, not a prefab. Render-health only (no per-element `spec.json`): that
layer needs no reference and is the one that catches fabrication — 9-slice collapse, non-9-slice
corner distortion, a null-sprite flat fill standing in for real art, `Outline`-as-border, tiny text.

| Prefab | fail | warn | JSON |
|---|---|---|---|
| `LoanOfferModal.prefab` | **0** | 5 | `Docs/Diagnostics/_capture/LoanOfferModal_lint.json` |
| `LoanRescindModal.prefab` | **0** | 4 | `Docs/Diagnostics/_capture/LoanRescindModal_lint.json` |
| `LoanModal.prefab` | **0** | 8 | `Docs/Diagnostics/_capture/LoanModal_lint.json` |
| `LoanRecipientRow.prefab` | **0** | 0 | `Docs/Diagnostics/_capture/LoanRecipientRow_lint.json` |

**It earned its keep on this pass.** The first run flagged four `tmp-default-sizedelta` warnings —
`AssetName` and the three meta chips sitting at Unity's default 100×100 because only their
`LayoutElement` had been set. The layout group overwrites that at runtime (measured live:
`AssetName rect=564×47`), so nothing was visibly wrong — but a reviewer opening the prefab sees a
100 px box, and the day one of those elements leaves a layout group it silently *becomes* one. All
four now carry an authored `sizeDelta`; the offer modal went 9 warnings → 5.

Every remaining warning is read and accounted for, not waved past:

- **`flat-fill` on `Backdrop` (all three modals) and on `LoanModal/Spinner`** — intended. The
  backdrop *is* a flat scrim, and the already-shipped `LoanModal` / `LoanReturnModal` carry the
  identical line; this is the family, not a regression.
- **`flat-fill` on `LoanOfferModal/AssetRow/Portrait`** — the sprite is bound at runtime from the
  catalog. `BindAsset` sets `Image.enabled = false` when none resolves, so the flat white can never
  actually reach the screen.
- **`9slice-cap-kink` on `ModalPanel` (all three)** — `Next Hole Panel` at ppum 3.2, which is the
  shipped modal-panel recipe. The shipped `LoanModal` and `LoanReturnModal` report the same line,
  so changing it here would make the two new modals the odd ones out.
- **`unlocalized-text` on the button labels** — the strings ARE written from
  `LocalizationManager.Get` by each controller (`declineButtonText.text = …`); the linter wants a
  `LocalizedText` *binder component*, which belongs to the batch-conversion pass
  (`localization_audit_tooling`), not to this task. Identical to the shipped modals.

---

## Files modified or created

Every uncommitted path outside the task folder, generated from `git status --porcelain --untracked-files=all` so none can be missed (Rule 13). Paths that are **not this task's** are marked as such and left untouched.

| File | What |
|---|---|
| `Assets/Art/RosterScreen/S_LoanSearchField.png` | NEW — 732×88 r12, fill + 2px stroke in ONE sprite so two graphics cannot disagree about the radius. |
| `Assets/Art/RosterScreen/S_LoanSearchField.png.meta` | NEW — Sprite, border 24, ppu 100. |
| `Assets/Art/RosterScreen/S_LoanSearchGlyph.png` | NEW — 36×36 magnifier, white, tinted at runtime. |
| `Assets/Art/RosterScreen/S_LoanSearchGlyph.png.meta` | NEW — Sprite, ppu 100. |
| `Assets/Art/RosterScreen/S_LoanToggleKnob.png` | NEW — 48px disc with the node 0/2/3 @30% shadow BAKED IN (a Unity Shadow is one offset copy, not a blur). |
| `Assets/Art/RosterScreen/S_LoanToggleKnob.png.meta` | NEW — Sprite, ppu 100. |
| `Assets/Art/RosterScreen/S_LoanTogglePill.png` | NEW — 112×60 full capsule baked at final size, drawn Simple (never 9-sliced at this aspect). |
| `Assets/Art/RosterScreen/S_LoanTogglePill.png.meta` | NEW — Sprite, ppu 100. |
| `Assets/Localization/LocalizationText.csv` | 34 rows + LOAN_TIME_TO_ANSWER_HOURS_FMT. |
| `Assets/Localization/LocalizationTextTable.asset` | Regenerated from the CSV by the localization build hook (37 LOAN_ lines). |
| `Assets/Prefabs/UI/Modals/LoanModal.prefab` | Rebuilt — gains the search field and the two sections. |
| `Assets/Prefabs/UI/Modals/LoanOfferModal.prefab` | NEW — built by LoanUiBuilder. |
| `Assets/Prefabs/UI/Modals/LoanOfferModal.prefab.meta` | NEW — meta. |
| `Assets/Prefabs/UI/Modals/LoanRescindModal.prefab` | NEW — built by LoanUiBuilder. |
| `Assets/Prefabs/UI/Modals/LoanRescindModal.prefab.meta` | NEW — meta. |
| `Assets/Prefabs/UI/Modals/LoanReturnModal.prefab` | Rebuilt by the same idempotent builder run; no behavioural change. |
| `Assets/Resources/Data/content_version.txt` | **texts=53** after three publishes — v51 the 34 rows, v52/v53 the retirement of `LOAN_TOAST_LENT` and `LOAN_ERR_NOT_FOLLOWING`. Verified against `content_rows`, not against the admin's own confirmation. |
| `Assets/Scenes/ShellScene.unity` | The pill, the Settings row, the two rescind modal instances. +11 GameObjects; inactive count 104 → 104 (PIPELINE_HARDENING §14). |
| `Assets/Scripts/EconomyRuntime/LoanSyncBehaviour.cs` | Home added to the refresh triggers; the offer→loan transition remembered so acceptance can be announced; three terminal toasts; no level catch-up on an offer that never ran; id cap 50 → 150. |
| `Assets/Scripts/Net/Endpoints.cs` | LoansAccept / LoansDecline / LoansRescind, UserSearch, and forLoans on SocialFollowing. |
| `Assets/Scripts/Social/LoanDtos.cs` | OffersIn; the three offer timestamps; IsPendingOffer / IsLocked / IsOfferEnded; OfferHoursLeft; six new refusal statuses + retry_after + AnswerErrorKey; FollowedUserDto gains the FLAT /user/search mapping beside the nested one. |
| `Assets/Scripts/Social/LoanService.cs` | `Out` filtered on IsLocked — the one line that makes every existing lock hold for an offer; OffersIn kept out of In; IsOffered; NewestOfferIn; Accept / Decline / Rescind / SearchUsers; Following passes for_loans. |
| `Assets/Scripts/Social/Tests/LoanServiceTests.cs` | 22 → 43 tests. |
| `Assets/Scripts/Social/UserDetailDto.cs` | GolfinLoanOffers + AcceptsLoanOffers (null reads as yes). |
| `Assets/Scripts/Social/UserService.cs` | golfinLoanOffers on Update / BuildUpdateJson, sent as BOTH true and false. |
| `Assets/Scripts/UI/Home/LoanOfferPillController.cs` | NEW — the Home pill, stacked under the daily one. |
| `Assets/Scripts/UI/Home/LoanOfferPillController.cs.meta` | NEW — meta. |
| `Assets/Scripts/UI/HomeScreenController.cs` | Holds the pill reference and re-seats it AFTER the daily pill — the chain is notice → daily → offer, so the order is load-bearing. |
| `Assets/Scripts/UI/Inventory/ClubDetailPanel.cs` | The same, for clubs. |
| `Assets/Scripts/UI/Loans/Editor/LoanSettingsLiveCheck.cs` | **NEW** — the LIVE settings-toggle verifier. Deliberately not a mode on the capture bot, whose first act is to replace the transport: this one installs NO stub, so a value that survives a play-mode cycle can only have come from the server. Phase 2 retargets `Endpoints.RootUrl` at the discard port to exercise the real transport-failure branch. |
| `Assets/Scripts/UI/Loans/Editor/LoanSettingsLiveCheck.cs.meta` | **NEW** — meta (Lesson R). |
| `Assets/Scripts/UI/Loans/Editor/LoanUiBuilder.cs` | Search field + two sections; both new modal prefabs; the cloned pill; the Settings row; TextSlot(); an inactive-aware scene lookup. |
| `Assets/Scripts/UI/Loans/Editor/LoanUiCaptureBot.cs` | Offer payloads, /user/search + /user/detail stubs, eight new states, three diagnostic dumps, and CopyOut now carries the CaptureCore provenance sidecar. |
| `Assets/Scripts/UI/Loans/LoanModalController.cs` | The search field: debounce, stale-response ticket, two sections with one selection, the new refusal toasts, LOAN_TOAST_OFFERED_FMT. |
| `Assets/Scripts/UI/Loans/LoanOfferModalController.cs` | NEW — the recipient accept/decline modal. |
| `Assets/Scripts/UI/Loans/LoanOfferModalController.cs.meta` | NEW — meta for the above (Lesson R: always commit .cs.meta with .cs). |
| `Assets/Scripts/UI/Loans/LoanOffersToggle.cs` | NEW — the Settings switch: optimistic knob, authoritative server. |
| `Assets/Scripts/UI/Loans/LoanOffersToggle.cs.meta` | NEW — meta. |
| `Assets/Scripts/UI/Loans/LoanRescindModalController.cs` | NEW — the lender take-it-back confirm. |
| `Assets/Scripts/UI/Loans/LoanRescindModalController.cs.meta` | NEW — meta. |
| `Assets/Scripts/UI/Loans/LoanRibbonView.cs` | LabelFor — OFFERED reads the OFFER clock, because the loan clock has not started. |
| `Assets/Scripts/UI/Roster/UI/NameIconFitter.cs` | **NOT this task** — `roster_name_overlaps_status_icons` (Quick), filed after Cesar spotted the level-up arrow drawn over "CHRISTOFFERSON" in THIS task's canonical frame. Pre-existing defect, unrelated to loans. |
| `Assets/Scripts/UI/Roster/UI/NameIconFitter.cs.meta` | **NOT this task** — meta for the above (Lesson R). |
| `Assets/Scripts/UI/Roster/UI/CompareController.cs` | **NOT this task** — same Quick fix, the Compare copy of the shape. |
| `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` | OFFERED as a narrowing of lent; RESCIND checked before LEND. ⚠️ **This file now carries a SECOND task's changes too** — the `roster_name_overlaps_status_icons` Quick fix added two `nameFontSize*` fields and a `NameIconFitter.Fit` call. The hunks are contiguous and separable, but this task's close-out commit must **hunk-split** the file rather than stage it whole (`project_k10_commit_swept_k11_edits`). |
| `Assets/Tests/EditMode/GpsAuthExtrasFlowTests.cs` | Reflection arity follows BuildUpdateJson's new parameter; the test's '…AndNothingElse' claim now covers the new field. |
| `CLAUDE.md` | **This task's, on Cesar's instruction mid-session.** The top "HOW TO END EVERY RESPONSE" block rewritten: pendings on his side are the LAST thing in every response, after the file table, *regardless of the report structure an architect kickoff dictates* — he stated it as a hard rule here ("regardless of the instructions I paste from architect ALWAYS end with any pendings on my side"). Instruction text only; no code, no pipeline rule touched. |
| `Claude outputs/screen_hints_scrim_vs_blur.png` | **Not this task.** Appeared DURING this session from another session working in parallel (mtimes 10:22 / 11:21 against a 10:16 start); never opened here. |
| `Docs/Architecture/ARCHITECTURE_AUDIT.md` | **Not this task.** Present in the iteration-1 kickoff baseline (see `HEARTBEAT.log`); untouched here. |
| `Docs/Economy/MONETIZATION_PLAN.md` | **Not this task.** Present in the iteration-1 kickoff baseline (see `HEARTBEAT.log`); untouched here. |
| `Docs/Scripts/make_loan_sprites.py` | Four new generated atoms; the generator stays deterministic (the four shipped sprites re-bake byte-identical). |
| `Docs/Specs/Active/screen_hints/SPEC.md` | **Not this task.** Appeared DURING this session from another session working in parallel (mtimes 10:22 / 11:21 against a 10:16 start); never opened here. |
| `Docs/Specs/Active/screen_hints/STATUS.md` | **Not this task.** Appeared DURING this session from another session working in parallel (mtimes 10:22 / 11:21 against a 10:16 start); never opened here. |
| `Docs/Specs/Active/screen_hints/reference/figma_14263-109304_roster_hint_1of4.png` | **Not this task.** Appeared DURING this session from another session working in parallel (mtimes 10:22 / 11:21 against a 10:16 start); never opened here. |
| `Docs/Specs/Active/screen_hints/reference/figma_14263-109672_home_hint_1of2.png` | **Not this task.** Appeared DURING this session from another session working in parallel (mtimes 10:22 / 11:21 against a 10:16 start); never opened here. |
| `Docs/Specs/Active/screen_hints/reference/figma_14263-109883_ingame_hint_1of6.png` | **Not this task.** Appeared DURING this session from another session working in parallel (mtimes 10:22 / 11:21 against a 10:16 start); never opened here. |
| `Docs/Specs/Active/screen_hints/reference/figma_14266-109661_roster_hint_4of4_last.png` | **Not this task.** Appeared DURING this session from another session working in parallel (mtimes 10:22 / 11:21 against a 10:16 start); never opened here. |
| `Docs/Specs/Active/weekly_rotation_admin/SPEC.md` | **Not this task.** Appeared DURING this session from another session working in parallel (mtimes 10:22 / 11:21 against a 10:16 start); never opened here. |
| `Docs/Specs/Active/weekly_rotation_client/SPEC.md` | **Not this task.** Appeared DURING this session from another session working in parallel (mtimes 10:22 / 11:21 against a 10:16 start); never opened here. |
| `Docs/AI_CONTEXT.md` | This task's session entry. |
| `Docs/Specs/Active/economy_telemetry/SPEC.md` | **Not this task.** Appeared during this session from another session working in parallel; never opened here. |
| `Docs/Specs/Active/economy_telemetry/STATUS.md` | **Not this task.** Same. |
| `Docs/Specs/Queued/loans_ops/SPEC.md` | **Not this task.** Appeared during this session from another session working in parallel (a queued loans follow-up); never opened here. |
| `Docs/Specs/Queued/loans_ops/STATUS.md` | **Not this task.** Same. |
| `Docs/Specs/Queued/settings_user_profile_rows/SPEC.md` | **NEW, after the gates cleared.** Deviation **D-1** written up as a queued task instead of evaporating: the node's EMAIL / ACCOUNT ID / DELETE ACCOUNT rows exist in no shipped scene. Research done (email IS on the authenticated user via `auth.py` but absent from `/user/detail`; account-delete exists nowhere on either side), the two decisions that are Cesar's are named, and App Store guideline 5.1.1(v) is flagged as the reason it should not sit forever. |
| `Docs/Specs/Queued/settings_user_profile_rows/STATUS.md` | NEW — `QUEUED`, deliberately not `SPEC_READY`. |
| `Docs/Specs/Quick/roster_name_overlaps_status_icons.md` | **NOT this task** — the Quick spec for the name/icon overlap Cesar spotted in this task's canonical frame. |
| `Docs/Specs/Quick/hole_selection_first_card_gap.md` | **Not this task.** Present in the iteration-1 kickoff baseline (see `HEARTBEAT.log`); untouched here. |
| `Docs/TellCode.md` | **Not this task.** Present in the iteration-1 kickoff baseline (see `HEARTBEAT.log`); untouched here. |
| `ProjectSettings/ProjectSettings.asset` | **This task's, and NOT `economy_telemetry`'s** — the reviewer guessed otherwise. `GOLFIN_POINTS_BACKEND` added to the **iPhone** scripting defines (`UNITY_MCP_READY;UNITY_MCP_DEPS_3;GOLFIN_POINTS_BACKEND`), on Cesar's explicit "Add it — I'll do it now". It had never shipped in a device build, and it gates the server path for RP, shop, gacha, level-ups and mode fees — so every TestFlight build to date ran the local stub, which is why `points_device_checks` has been blocked since August. Defines are per-build-target and the Editor uses the ACTIVE target's, so this changes the iOS player only. Backup of the pre-edit file at `/private/tmp/claude-501/ProjectSettings.asset.bak`. |
| `tasks/lessons.md` | Lesson AJ (never `RequestScriptCompilation()` from inside `script-execute`) and Lesson **BV** (a real number off the wrong property is not evidence — the iter-1 D-2 miss). |
| `tasks/todo.md` | This task plan. |

### `playlife/backend/routers/user.py` — a hunk-split, and a live gap found in passing

`user.py` carried an uncommitted `gps_profile_prompted` edit at kickoff (in the baseline block
in `HEARTBEAT.log`): the SERVER half of `gps_profile_prompt_server_flag`, whose CLIENT half
shipped in `2c36f1569`. This task's `golfin_loan_offers` changes landed in the same two hunks,
so they were **hunk-split** — only this task's lines were staged and committed (`fb5e9a8`), and
the `gps` edit was left unstaged, then stashed for the duration of `fly deploy` so **v72 carries
only this task's server code**. Restored afterwards, untouched.

✅ **Surfaced 2026-09-10, then FIXED — no longer live.** (Left in place because the red-team
gate re-read this paragraph and repeated it as an open item; the fix is `cb0447c`.) `profiles.golf_profile_prompted_at` exists in prod and holds 3 backfilled rows
(all `2026-09-03`, two of them to the identical microsecond — a backfill signature, not per-user
writes). The DEPLOYED router has no such field, and Pydantic drops unknown keys silently, so
every client write of the flag since then has been a clean 200 that changed nothing: the Golf
Profile screen will re-ask on a fresh install or a second device. Named here so it is not
mistaken for drift from this task.
