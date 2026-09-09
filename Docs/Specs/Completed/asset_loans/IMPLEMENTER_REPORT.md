# IMPLEMENTER_REPORT — `asset_loans`

**Iteration shape:** `loans:first-implementation`
**Date:** 2026-09-09
**Canonical screenshot:** `screenshots/loan_roster_B_on_loan_2026-09-09_15-06-07.png` (1170×2532)

---

## What is done, and what is NOT

**Done and verified here:** the whole server half (migration + router + the RP split), the whole
client half (service, managers, save/blob guards, the points queue, both detail panels, both card
badges, the lend modal, the return popup, the level-up modal's borrowed branch), the 31 strings
published and bundled, and 33 new EditMode tests.

**NOT done, and it is not mine to do:** the SPEC's **live two-account E2E**. The migration is not
applied and `playlife-api` is not deployed — both are Cesar's steps, in that order — so there is
nothing on the other end of the socket. Everything below that says "verified" was verified against
the real client code path with the HTTP transport stubbed by server-shaped JSON; everything that
needs a real server is listed under § Needs Cesar and is honestly marked NOT VERIFIED.

---

## Acceptance checklist

| # | Item | Verdict | Measurement |
|---|---|---|---|
| 1 | Migration applied, `fly deploy` green, `/api/v1/loans` 403-not-404 | **VERIFIED** | Cesar applied the migration 2026-09-09; **all ten verification rows match** (quoted below). Deployed and verified by IMAGE VERSION AND A LIVE PROBE, never the exit code (`reference_flyctl_401_false_deploy_failure`): image `…M220GD2ZW5…` → `…M22D44WAHH…`, machines v70 → **v71**. All three loan routes answer **403** unauthenticated while `/api/v1/nonexistent` answers 404 — which is what makes the 403 mean "mounted and auth-gated" rather than a blanket response. |
| 2 | Live E2E, two accounts, character | **NOT VERIFIED — needs Cesar** | No longer blocked by infrastructure — the table, the functions and the routes are all live. What it still needs is two signed-in accounts with a follow between them and a hole actually played, which is a person on a device. The client half of every step is exercised by the capture run and the tests; the server half is exercised by the 31 router tests but not against prod. |
| 3 | Same for a club (equipped → leaves the bag; durability frozen; Repair disabled) | **PARTIAL** | Client side VERIFIED: `loan_clubs_B_on_loan` shows the club unequipped ("EQUIP", not "EQUIPPED") after the lend reconciled, with LEVEL UP / REPAIR / COMPARE / LEND all disabled. Durability freezing needed no code: a grep of `Assets/Scripts` on 2026-09-09 found **no durability-wear call site at all** (`RepairClub` and `UseBestRepairKit` only ever RAISE it), so there is nothing to freeze — noted as the SPEC invited. Server side is live but unexercised against prod — see #2. |
| 4 | Refusals | **PARTIAL** | `self`, `bad_days`, `unknown_ref`, `not_following`, `already_on_loan`, `borrower_has_it` (both the live-loan and the inventory-blob route), `limit_out` are each pinned by a router test (`backend/tests/test_loans.py`, 31 tests, all green). The **client's** mapping of each status to its own toast key is pinned by `LoanServiceTests.EveryKnownRefusalMapsToItsOwnKey`. The selected-character rule is client-side and verified in the capture run (§ Figma fidelity, row "LEND on the selected character"). A live authenticated curl still needs a real token — see #2. |
| 5 | Expiry: a past `ends_at` flips to `expired` and both clients reconcile | **VERIFIED (client) / NOT VERIFIED (server)** | Server: `TestList.test_a_past_ends_at_is_flipped_to_expired_on_read` asserts the row is patched to `expired` with `ended_at` and the owner's `level_at_end`. Client: `LoanServiceTests.APastEndsAtIsNotLiveEvenWhileStatusSaysActive` — a row still marked `active` past its clock is **not live**, so an unswept row can never be used. |
| 6 | Borrowed rows never reach the blob | **VERIFIED (client)** | `InventoryCodecLoanTests` — 4 tests. Three guards, and the test proves the third is REAL rather than decorative by checking the projector carries the flag through (`TheProjectorCarriesTheFlagSoTheSkipIsReal`). A live `profiles.golfin_inventory` read after a real borrowed session still needs #2. |
| 7 | Rect self-diff | **VERIFIED — 0.000 px** | Measured below. |
| 8 | Figma fidelity table | **VERIFIED, with 3 stated deviations** | Table below. |
| 9 | Strings: 31 rows EN+JA, PLAN/APPLY, published, `--check` clean, zero hardcoded literals | **VERIFIED** | Numbers below. |
| 10 | EditMode suite before/after; the named new tests | **VERIFIED** | 2942 → **2975** total; **2971 passed, 0 failed, 4 skipped** (all four skips pre-existing). |
| 11 | Telemetry rows; console clean; `[SerializeField]` wired; deviations flagged | **PARTIAL** | Wiring VERIFIED (below). Telemetry calls are in place; the rows land once a real session drives the modal — see #2. |

---

## Rect self-diff (§4.1) — measured, not eyeballed

Read with `GetWorldCorners` and converted into each RightPanel's own local space.

```
ROSTER  LevelUp.L = -245.800   Compare.L = -245.800   Δ = 0.000
ROSTER  Boost.R   =  240.200   Lend.R    =  240.200   Δ = 0.000
ROSTER  gap = 16.000   cmpW = 235   lendW = 235   sameY = True

CLUB    LevelUp.L = -629.850   Compare.L = -629.850   Δ = 0.000
CLUB    Repair.R  = -394.850   Lend.R    = -394.850   Δ = 0.000
CLUB    cmpW = 235   lendW = 235

ROSTER  ribbon 537×72, topΔ = 0.000 vs the portrait's top, leftΔ = 0.000
ROSTER  dim 537×1483, coversPortrait = True, ribbon draws OVER the dim = True
CLUB    ribbon 537×72 at the LeftPanel's top; dim 537×1355

BADGE   CharacterThumbnailCard 44×44 at (8, −8), anchor (0,1), inactive by default
BADGE   ClubThumbnailCard      44×44 at (8, −8), anchor (0,1), inactive by default
```

**The two panels are built differently, and that is deliberate.** The Roster's `RightPanel`
positions its children absolutely; the club's is a `VerticalLayoutGroup` where every child sits at
(0,0) and the group decides. So the Roster row is positioned by copying the two reference buttons'
edges, and the club row is a **carbon copy of the `ButtonsPanel` above it** — same rect, same
`HorizontalLayoutGroup` settings, same child sizes, no `LayoutElement` on any of the three. That
makes the acceptance property true *by construction* rather than by arithmetic, which matters here
because the panel is 485.7 wide and its content is 486: any derivation from widths and gaps would
have been 0.15 px out.

**Compare-mode mirrors untouched.** `CompareRightPanel/CompareInfoPanel` and the Item/Ball panels
are not in the diff. The scene name census confirms it: the only names the diff ADDS are
`CompareLendRow` ×1, `LendButton` ×2, `LentDim` ×2, `LoanRibbon` ×2 and their `Icon`/`Label`/`Text
(TMP)` children. **No name was lost or reduced**, and `m_IsActive: 0` went 100 → 104 — exactly the
four overlays this task authors inactive, and no boot-critical container deactivated
(PIPELINE_HARDENING §14).

---

## Figma fidelity (Rule 18)

Re-pulled nothing from the Figma API — the nine node renders in `reference/` are the A/B ground
truth the architect dropped at spec time, and the SPEC's token table was reconciled against them.

| Element | Node | Spec | Built | Verdict |
|---|---|---|---|---|
| Compare + Lend row (Roster) | `14181:107801` | 2 × ~235 + gap, aligned to the LEVEL UP / BOOST row | 235 + 16 + 235, edges Δ 0.000 | **PASS** |
| Compare + Lend row (Clubs) | `14183:108281` | same, replacing the 489-wide Compare | 235 + 16 + 235 in a row copied from `ButtonsPanel `, edges Δ 0.000 | **PASS** |
| COMPARE / LEND button art | `Main Buttons / Silver -Small` | silver small | `ButtonLevelUp.png`, **native 235×56** — the same atom the row above uses, so nothing is stretched | **PASS** |
| Loan ribbon | `14182:32760` / `107177` / `14183:109280` / `109309` | 537×72 at the panel top, `#050F1F @ 72 %`, top corners r=20, HORIZONTAL pad 24 / gap 16 | 537×72, `S_LoanRibbon` tinted `#050F1F` **alpha 0.72 (read off the live Image)**, HLG pad 24 spacing 16 | **PASS** |
| Ribbon icon | `IconLoanOut` / `IconLoanIn` | 40×40 tray+arrow, white | `IconLoanOutBig` / `IconLoanInBig`, 40×40, swapped by side | **PASS** |
| Ribbon text | TEXT | Rubik SemiBold 28 white, one line | Rubik-SemiBold SDF 28 white, `enableAutoSizing` 20–28 + ellipsis so a long name shrinks rather than wraps | **PASS** |
| Ribbon copy | `LOAN_STATUS_OUT_FMT` / `_IN_FMT` | "ON LOAN TO {0} · {1}" / "BORROWED FROM {0} · {1}" | rendered "ON LOAN TO MARTA · 2d 3h" and "BORROWED FROM KENJI · 5h 23m" — **both time formats exercised** | **PASS** |
| Lent dim | `14182:32765` / `14183:109279` | full Left panel, `#000 @ 55 %`, BELOW the ribbon | 537×1483 (Roster) / 537×1355 (Clubs), alpha **0.55 read off the live Image**, sibling index below the ribbon. Rendered effect measured: portrait mean luma 126.0 → 86.5 (ratio 0.686, which is what a 55 % linear-space veil gives in sRGB) | **PASS** |
| Disabled buttons, lender view | `14181:33672` / `14183:108287` | LEVEL UP, BOOST/REPAIR, COMPARE, LEND, SELECT/EQUIP all `Enabled=No` | measured live: `LevelUpButton interactable=False · BoostButton False · CompareButton False · SelectButton False · LendButton False`, all still ACTIVE | **PASS (deviation D-1 on the mechanism)** |
| Borrowed view | `14181:33894` / `14183:108675` | BOOST hidden (Roster); REPAIR disabled (Clubs); COMPARE, RETURN, SELECT/EQUIP live | `loan_roster_C_borrowed` shows LEVEL UP alone on its row with BOOST gone, and COMPARE + RETURN below | **PASS** |
| Card loan badge | `14182:32786` / `107182` / `14183:109305` / `109319` | 44×44 at (8,8) top-left, `#050F1F @ 85 %`, 2 px white stroke, 26×26 glyph | 44×44 at (8,−8) anchored top-left; `S_LoanBadge` carries the 85 % disc and the 2 px ring as baked pixels; 26×26 glyph child | **PASS** |
| Lend modal panel | `14183:32983` / `14185:34374` | 780 wide, HUG height, `#133453→#091B33`, 3 px white stroke, r=20, behind it a 50 % scrim | measured 780×1244 with every child `insidePanel=True`; `Next Hole Panel.png` sliced at `pixelsPerUnitMultiplier 3.2` (border 64 ÷ 3.2 = 20 px radius); backdrop `#000 @ 50 %` | **PASS** |
| Modal title | first TEXT | `LOAN_MODAL_TITLE`, SemiBold 45 white, left | "LEND JOHAN", Rubik-SemiBold 45, left | **PASS** |
| DURATION + segments | `14183:32988/32990` | label SemiBold 39; three 230×54, gap 21; unselected silver, selected gold; **3 DAYS default** | label 39; three 230×56 chips gap 21; 3 DAYS on the gold sprite at open | **PASS (deviation D-2 on height)** |
| Terms line | TEXT | `LOAN_TERMS_FMT`, Regular 30, width 732, wraps | rendered "They level it up, it comes back levelled. You get 20% of the RP they earn with it." over two lines; **the 20 comes from `lender_share_bp` on the row**, never a constant | **PASS** |
| Equipped warning | TEXT in `14185:34374` | `LOAN_WARN_EQUIPPED`, SemiBold 30 `#FFB847`, only when bagged | authored `#FFB847` SemiBold 30, `SetActive(clubIsEquipped)` | **PASS** |
| LEND TO list | `14183:107512` | label 39; rows 732×96 r=12, avatar 64, name SemiBold 33 FILL, "Lv {n}" Regular 30 `#BFD1E6`; unselected `#050F1F @60 %`, selected `#2775DD @35 %` + 3 px stroke; gap 16; scrolls > 4 | rendered MARTA Lv 21 / LUCAS Lv 8 / AIKO Lv 47 in 732×96 rows, gap 16, in a 432-tall (4-row) viewport | **PASS (deviation D-3 on the avatar)** |
| Modal footer | `14183:107530` | CANCEL silver 354×120 + LEND gold 354×120, gap 24, **equal size** | both 354×120, gap 24; both 9-sliced so neither corner radius distorts at a width its sprite was not baked at | **PASS** |
| Return confirm | `14183:107775` | 810 wide, pad 40/48/32/48, gap 32; title `#ED6B21` centred SemiBold 45; body Regular 33 centred 714; CANCEL silver 345 + RETURN **gold** 345, gap 24 | 810 wide, those paddings; "RETURN CHARACTER?" in `#ED6B21`; body "Return OLIVIA to KENJI now? It goes back at Lv 64 — the levels you bought stay with it."; CANCEL + **gold** RETURN, 345 each | **PASS** |

### Playbook § 7 self-diff — what I found by cropping rather than asserting

Crops of the built modal against `reference/Roster_LendModal_14183-32758.png`, and of each panel
state against its node render, surfaced three real differences. All three are listed as deviations
below rather than quietly passed.

---

## Deviations (all deliberate, all flagged)

**D-1 — a disabled button is a COLOUR TINT, not a swapped sprite.** Figma's lender frame swaps each
instance to its `Enabled=No` variant. The project already has disabled sprites for some of these
(`ButtonLevelUpDisabled.png`, `ButtonSelectDisabled.png`), but **no shipped code swaps them** —
`CharacterDetailPanel.ApplyLockedState`, the level-up gate and the repair gate all express disabled
as `interactable = false` and let the Button's ColorTint do the rest. I matched the project, not the
node: introducing a sprite-swap convention for the loan states alone would make the lent panel look
different from every other disabled state in the game. Cheap to change if Cesar wants the variant
art — it is one swap per button in `ApplyLoanState`.

**D-2 — the duration chips are 56 tall, not 54.** The node says 230×54. The scene's small-button
atom is natively 235×56 and every neighbouring small button is 56, so a 54 would be the only
two-pixel-short button on the screen. Width is the node's 230.

**D-3 — the recipient avatar is the placeholder circle, always.** The node specifies "`avatar_url`
when present, else the `#38597F` placeholder", and `avatar_url` IS carried all the way onto
`LoanRecipientRow.AvatarUrl`. But **there is no remote-avatar loader in this client**:
`CatalogArtCache` serves catalog art only, and a grep on 2026-09-09 found nothing anywhere reading
`UserDetailDto.AvatarUrl`. Inventing a second image-fetch path inside a lending task is the wrong
place for it, so the row draws the placeholder and the URL is held for the loader when it exists —
one call site to fill.

**Architect defaults that Cesar has not blessed** (the SPEC asked for these to be flagged): 3 loans
out / 3 in, `LOAN_LENDER_SHARE_BP = 2000`, and the 40 %-of-an-action cap when several borrowed
assets are used in the same round. All three are one-line server constants.

---

## Clone provenance (Rule 19)

Every reused atom is loaded by an explicit asset path and **asserted non-null by the builder** — a
missing source throws `REUSE SOURCE MISSING` and stops the run, so nothing can be quietly rebuilt
from scratch. The run logs what it bound; these GUIDs are that log, not a claim.

| Element | Source | GUID |
|---|---|---|
| COMPARE + LEND button art (both panels) | `Assets/Art/RosterScreen/ButtonLevelUp.png` | `3a504f4c40d48e14c81071475d87974b` |
| LEND button object | **cloned from the live `CompareButton`** in each panel (`Object.Instantiate`), then its inherited `onClick` persistent-call array cleared | scene clone |
| Duration chip, unselected | `ButtonLevelUp.png` | `3a504f4c40d48e14c81071475d87974b` |
| Duration chip, selected (gold) | `Assets/Art/RosterScreen/ButtonLevelUpLong.png` | `a51f6c0ee74bbf24b8347aa84715db47` |
| CANCEL (both modals) | `Assets/Art/RosterScreen/ButtonCancel.png` | `6021c639e9c124b44a06c8ccd977896f` |
| LEND / RETURN (both modals) | `Assets/Art/RosterScreen/ButtonConfirm.png` | `bc649f28836576548b310e79ce614a06` |
| Modal panel chrome (both modals) | `Assets/Art/HomeScreen/Next Hole Panel.png` | `3663aafeba2bd1f42a04eabf9d34c220` |
| Ribbon icons | `IconLoanOutBig` / `IconLoanInBig` | `07b578fc700e74ed2962f727c7d7ad2d` / `607dca87b7c7e426aae95de3584446bf` |
| Badge glyphs | `IconLoanOutSmall` / `IconLoanInSmall` | `34a4bc53e32254f62aac303e3e7a36be` / `13ea13224efd342b29cecbfe955c8ba8` |
| Fonts | `Rubik-SemiBold SDF` / `Rubik-VariableFont_wght SDF` | `39fb7824ee463ab408c7f2e76c362562` / `0e84913c86a5b7f4881cb73d5e80728f` |
| Ribbon / badge / row / row-selected | **generated**, `Docs/Scripts/make_loan_sprites.py` | `059e3c5df…` / `47049e2ee…` / `f7cd714fa…` / `3b4565a6e…` |
| Modal chrome behaviour | `ModalController` base class | — |

**Four sprites are BAKED rather than reused, and each says why** (in the script's header and in
`UI_ELEMENT_PALETTE.md`): the ribbon needs top-only rounding, which no shipped atom has and which
would notch against the portrait; the badge is two colours in one sprite so it cannot be a tinted
white shape; the selected row carries a 3 px stroke that must not be an `Outline` component (C5 —
an Outline is four offset copies, and the fidelity linter fails it).

**Two importer borders were set on EXISTING atoms** (`ButtonConfirm` 25/25/25/25 — its silver twin
`ButtonCancel`'s own shipped border — and `ButtonLevelUpLong` 25/0/25/0). A border is **inert for
`Image.Type.Simple`**, which is how every current consumer draws them, so no existing usage
changes; it exists so the loan buttons can be `Sliced` at 230/345/354 without corner distortion.

---

## UI fidelity lint (Rule 21)

**NOT RUN — and I am flagging it rather than claiming it.** `UIFidelityLinter.LintPrefab` takes a
per-element `spec.json` generated by `Docs/Scripts/figma_node_to_spec.py` from a live
`get_design_context` pull, and **the Figma MCP server is not authorised in this session** (`/mcp` is
unavailable non-interactively), so the node re-pull that Rule 9 also requires could not be done at
all. What stands in its place, and what does not:

* the **render-health half** of what the linter checks is covered by construction and stated above:
  no 9-slice collapse (every sliced sprite's border is stated with its `pixelsPerUnitMultiplier`),
  no non-9-slice corner distortion (nothing is stretched off its native size), no null-sprite
  flat-fill fabrication (the provenance table is exhaustive and builder-asserted), no
  `Outline`-as-border (explicitly replaced by a sprite swap), no tiny text;
* the **node-spec half** — px/font/gap/sprite compared against a freshly pulled node — is **not**
  covered. The `reference/` renders were compared by eye and by crop, which is weaker.

This is the one gate in the pipeline this iteration cannot satisfy, and it is an access problem, not
a shortcut. A reviewer with Figma access should run `figma_node_to_spec.py` on the nine nodes and
then `LintPrefab` on `LoanModal.prefab` and `LoanReturnModal.prefab`.

---

## Strings (§5)

* **31 rows**, EN + JA, appended to `Assets/Localization/LocalizationText.csv`. The SPEC names 29;
  the extra two are `LOAN_DURATION` ("DURATION") and `LOAN_LEND_TO` ("LEND TO") — the two section
  labels the modal node draws, which the SPEC's list omits and which would otherwise have been the
  only hardcoded literals in the feature.
* **PLAN:** `texts  add 31  change 0  same 1109  conflict 0` — exactly the 31, no other drift.
* **APPLY:** `Wrote 31 draft(s) … (31 new, min_build 2823)`.
* **PUBLISHED:** `content_publish('texts')` → **version 49**.
* **`export --check`:** `clean — no file would change, no catalog has drifted`. `content_version.txt`
  updated and staged.
* **Bundled table rebuilt** (`Tools/Localization/Import Text CSV`): **1140 rows, 31 `LOAN_*` keys**,
  read back off `LocalizationTextTable.asset` — not off the CSV, and not via
  `LocalizationManager.Get`, which returns the key in edit mode because its map is built at runtime.
* **Zero hardcoded player-facing literals.** The grep, with its real output rather than a claim:
  ```
  $ grep -nE '\.text\s*=\s*"' Assets/Scripts/UI/Loans/*.cs \
        Assets/Scripts/EconomyRuntime/LoanSyncBehaviour.cs
  Assets/Scripts/UI/Loans/LoanRecipientRow.cs:83:  nameText.text  = "—";
  Assets/Scripts/UI/Loans/LoanRecipientRow.cs:84:  levelText.text = "—";
  ```
  **Two matches, and both are the em-dash loading placeholder**, which is the shape the GPS screens
  already use for a row that has not arrived — a glyph, not copy, and identical in both languages.
  Every other assignment in the loan UI goes through `LocalizationManager.Get` or
  `string.Format(LocalizationManager.Get(key), …)`.

---

## EditMode tests

**Before 2942 → after 2975 (+33). 2971 passed, 0 failed, 4 skipped** — all four skips pre-existing
(three `HoleCompleteDriverTests` Stage-C1 skips and one editor-frame-clock skip in
`UiMotionAllocationTests`).

| File | Tests | What it pins |
|---|---|---|
| `Assets/Scripts/Social/Tests/LoanServiceTests.cs` | 22 | The lend body's snake_case fields; refusals as 200 payloads and their key mapping (including an unknown future status → `LOAN_ERR_GENERIC`); the liveness predicate matching the server's; a failed refresh keeping the last list; reconciliation applying an ended loan **on the side it arrived on** and **exactly once** across the server's 14-day window; the round snapshot naming only borrowed assets, staying null when nothing is borrowed, and surviving a loan that ends mid-round. |
| `Assets/Scripts/Economy/Tests/PendingPointsOpLoanTests.cs` | 7 | `loan_ids` omitted entirely for an ordinary earn (byte-identical to the pre-loans request); present when borrowed; the list COPIED not aliased (the caller's list is a live snapshot); an op queued by an older build deserialising with none. |
| `Assets/Scripts/InventorySync/Tests/InventoryCodecLoanTests.cs` | 4 | A borrowed character and a borrowed club never reach the blob, the owned rows still do, and the projector carries the flag so the codec's guard is real rather than decorative. |
| `playlife/backend/tests/test_loans.py` | 31 | (Python) auth gating, every refusal, replay, lazy expiry, the split arithmetic and its 40 % cap. |

**Two failures this iteration caused, both found by the suite and both fixed:**

1. `ScrollFeelTests.EveryInScopeScrollRect_InEveryPrefab_IsAtTheReference` — the modal's new
   `ScrollRect` was at Unity's defaults. The builder now reads `ScrollElasticity` /
   `ScrollDeceleration` / `ScrollSensitivity` **off `GamePolishBuilder`** rather than retyping them,
   so they cannot drift apart.
2. `GameplaySceneLoaderTests.BeginGameplayLoad_HidesBottomNav` — the round snapshot I added to
   `ApplyPreloadSetup` touched `LoanService.Instance`, which builds `ApiClient.Instance`, which
   calls `DontDestroyOnLoad` — illegal outside play mode, and that suite drives the prelude from an
   EditMode test. `SnapshotRoundLoans` now returns early when not playing and when the points
   backend flag is off, which is also the correct production behaviour.

---

## `[SerializeField]` wiring — no white boxes

Every reference is wired by `LoanUiBuilder` through `SerializedObject`, and a field name that does
not exist throws rather than passing silently. Read back from the saved scene and prefabs:

```
club.loanRibbon = LoanRibbon      club.lendButton = LendButton
club.loanModal  = LoanModal       club.loanReturnModal = LoanReturnModal
(same four on the Roster's CharacterDetailPanel)
CharacterThumbnailCard.prefab  LoanBadge 44×44 (8,−8) anchor(0,1) active=False
ClubThumbnailCard.prefab       LoanBadge 44×44 (8,−8) anchor(0,1) active=False
LoanModal.prefab  RecipientScroll  move=Elastic elast=0.1 inertia=True decel=0.135 sens=20
```

`ButtonPressFeedback` (Rule 11) is added in the same operation as every new `Button`: the LEND
button on both panels, the three duration chips, both footer buttons in each modal, and the
recipient row.

**Modal children are authored INACTIVE** (`reference_modal_children_author_inactive`) —
`ModalController.Awake` forces it anyway, and authoring them active throws a
`UIParticle.OnDisable` `MissingReferenceException` on every play-mode entry.

---

## Screenshots — real navigation, stubbed socket

All eight are 1170×2532, captured through `CaptureCore.SnapPlayModeSafe` in a play-mode run that
**boots the app, waits for the boot transitions to settle, taps the real Characters / Bag nav
button, and drives the real LEND / RETURN button's `onClick`**. Only the HTTP transport is stubbed,
with the exact envelope `routers/loans.py` writes — so the DTOs, the envelope unwrapper,
`LoanService.Apply`, the reconciler, both panels and both modals are all the shipped code.

| File | State |
|---|---|
| `loan_roster_A_lend_enabled_2026-09-09_15-06-06.png` | owned, not lent — COMPARE + LEND, LEND live |
| `loan_roster_B_on_loan_2026-09-09_15-06-07.png` | **canonical** — ON LOAN TO MARTA · 2d 3h, dim on, every button off |
| `loan_roster_C_borrowed_2026-09-09_15-06-08.png` | BORROWED FROM KENJI · 5h 23m, BOOST hidden, COMPARE + RETURN |
| `loan_roster_D_lend_modal_2026-09-09_15-06-13.png` | lend modal, populated (MARTA / LUCAS / AIKO) |
| `loan_roster_E_lend_modal_empty_2026-09-09_15-06-15.png` | lend modal, `LOAN_NO_FOLLOWING` empty state |
| `loan_roster_F_return_confirm_2026-09-09_15-06-09.png` | return confirm popup |
| `loan_clubs_A_lend_enabled_2026-09-09_15-06-18.png` | club panel, COMPARE + LEND row |
| `loan_clubs_B_on_loan_2026-09-09_15-06-19.png` | club ON LOAN — ribbon, dim, unequipped, all buttons off |

### Three defects the capture run found, and what each cost

Worth recording because each was invisible in the frame until it was measured, and two of them made
the bot lie about what it had photographed.

1. **The club ribbon and dim never appeared** — the club panel's `LeftPanel` is a
   `VerticalLayoutGroup` (the Roster's is not), so both overlays were laid out into the vertical
   flow instead of over the artwork. The frame showed a club whose buttons had correctly gone to
   their lent state with no ribbon and an undimmed portrait. Fixed with
   `LayoutElement.ignoreLayout = true` on both, unconditionally.
2. **Three "Roster" captures were pictures of Home**, with the log cheerfully reporting "reached
   Roster on attempt 1" — `ScreenManager.CurrentScreen` had been set by a navigation the boot chain
   then stomped, and `ShowScreen(x)` returns immediately when `_currentScreen == x`, so every retry
   was a no-op. The bot now waits for Home to be QUIET for two seconds before navigating, asserts
   the destination's own panel is active rather than trusting the field, and logs the screen at
   every snap.
3. **The "boot gate" search matched Home's PLAY button** and started loading a hole. It now only
   looks for a gate when we are not already on Home.

**Nothing here was decided by eye.** "Is it dimmed?" was answered by reading `Image.color.a` off the
live object (0.55) *and* by measuring the rendered luma (126.0 → 86.5). "Are the buttons disabled?"
was answered by dumping `interactable` for every button on the panel. Both dumps are in the report
above.

---

## Video

`Docs/Specs/Active/asset_loans/videos/asset_loans_captioned.mp4` — **60.8 s, 1170×2532, 6.1 MB**
(copy in `Docs/Reports/Media/asset_loans/`). Recorded by `LoanDemoRecorder`
(`GOLFIN ▸ Loans ▸ Record demo`), Unity Recorder over the **GameView** source — a camera source
drops the Overlay HUD under URP — with nothing calling `CaptureCore` while it runs, so no frame is
flipped.

Every tap is a real widget's `onClick`: the Splash StartButton, the bottom-nav Characters and Bag
buttons, the detail panel's own LEND / RETURN, the modal's duration chips, a recipient row, and the
footer LEND. The LOAN DATA is stubbed with the exact envelope `routers/loans.py` returns — the API
is deployed but no loan exists yet, and creating one needs a second account the player follows.

**The caption timing is measured off the encoded clip, not off the runner's clock.** The first burn
put four captions a whole step late — "Lent: a ribbon, a dim" over the BORROWED panel and
"Borrowed…" over the return popup — because a DemoRecorder stamps captions in WALL time while the
Recorder writes variable-frame-rate video, and the gap between the two ran 2.9–4.8 s and kept
changing. So `Docs/Scripts/retime_captions_by_state.py` (new) classifies sampled frames against the
eight state screenshots the capture bot already produced, collapses them into measured windows, and
writes the sidecar from those. The measured timeline:

```
  0.0 ->  7.0   boot / Home
  7.0 -> 13.0   roster, COMPARE + LEND
 13.0 -> 27.0   the lend modal
 27.0 -> 34.5   roster, ON LOAN (ribbon + dim)
 34.5 -> 40.0   roster, BORROWED (RETURN, no BOOST)
 40.0 -> 45.5   the return confirm
 48.0 -> 54.0   clubs, COMPARE + LEND
 54.0 -> 60.5   clubs, ON LOAN
```

Then **every one of the ten windows was checked against its own decoded frame** — accurate seek
(`-ss` after `-i`), never a keyframe sample — and all ten now sit on the screen they describe.

Three recorder defects were found and fixed getting here, each of which produced a plausible-looking
artifact that was wrong:

1. **An 18 MB mp4 with no moov atom.** `EditorApplication.isPlaying = false` from inside the
   coroutine tore the domain down before the Recorder finalised the container. `ExitPlaymode()` is
   the graceful path.
2. **Still no moov atom.** The `ArmedKey` guard sat in front of BOTH branches of
   `OnPlayModeChanged`, and the flag is cleared on ENTERING play mode — so by `ExitingPlayMode` it
   was false and `StopRecorder()` never ran at all. Stopping is now unconditional.
3. **The title card covered the panel.** `build_bot_video.py` draws caption 0 CENTRED, so a content
   caption in that slot sits over the thing it describes. The title is now passed as `--title` with
   its own 4.5 s window.

## Files modified or created

Every uncommitted path outside this task's spec folder appears here (Rule 13). Verified against
`git status --porcelain --untracked-files=all`.

### playlife — committed as `2add3a5`

| File | What |
|---|---|
| `backend/migrations/2026_09_09_golfin_loans.sql` | NEW — `golfin_loans` + 2 partial unique indexes, `golfin_progress_events.on_behalf_of`, loan-aware `golfin_level_up`, `golfin_loan_split`, verification block |
| `backend/routers/loans.py` | NEW — `GET /loans`, `POST /loans`, `POST /loans/{id}/return`, `resolve_shares` |
| `backend/tests/test_loans.py` | NEW — 31 router tests over an in-memory Supabase fake |
| `backend/routers/points.py` | `EarnGameRequest.loan_ids`; the split branch in `earn_game_pts` |
| `backend/main.py` | mount `/api/v1/loans` |

### GolfinRedux — NOT yet committed

| File | What |
|---|---|
| `Assets/Scripts/Social/LoanDtos.cs` | NEW — `LoanDto` (+ the ONE liveness predicate), `LoanListDto`, `LoanMutationDto` (+ status→key), `FollowedUserDto` |
| `Assets/Scripts/Social/LoanService.cs` | NEW — the service, `ILoanReconciler`, the round snapshot, reconciliation |
| `Assets/Scripts/Social/Tests/LoanServiceTests.cs` | NEW — 22 tests |
| `Assets/Scripts/EconomyRuntime/LoanSyncBehaviour.cs` | NEW — the bridge to the managers; refresh triggers; the toast + "seen" record |
| `Assets/Scripts/UI/Loans/LoanRibbonView.cs` | NEW — the ribbon + dim, and the time formatting |
| `Assets/Scripts/UI/Loans/LoanBadgeView.cs` | NEW — the card badge |
| `Assets/Scripts/UI/Loans/LoanRecipientRow.cs` | NEW — one LEND TO row |
| `Assets/Scripts/UI/Loans/LoanModalController.cs` | NEW — the lend modal |
| `Assets/Scripts/UI/Loans/LoanReturnModalController.cs` | NEW — the return confirm |
| `Assets/Scripts/UI/Loans/Editor/LoanUiBuilder.cs` | NEW — the idempotent authoring pass (`GOLFIN ▸ Loans ▸ Build Loan UI`) |
| `Assets/Scripts/UI/Loans/Editor/LoanUiCaptureBot.cs` | NEW — the play-mode capture bot |
| `Assets/Scripts/Net/Endpoints.cs` | `Loans`, `LoansReturn(id)`, `SocialFollowing(userId, limit)` |
| `Assets/Scripts/CharacterManager.cs` | `EnsureBorrowed` / `RemoveBorrowed` / `SetLentOut` / `IsBorrowed` / `IsLentOut` / `FirstSelectableCharacterId` / `ApplyLoanLevelCatchUp` / `RaiseRosterChanged`; borrowed rows skip the save; `SelectCharacter` and `LevelUp` refuse a lent asset |
| `Assets/Scripts/ClubManager.cs` | the same six + `RaiseInventoryChanged`; `PersistOwnedClubs` skips borrowed; `EquipClub` refuses a lent club **only when `bagSlot > 0`**, so reconciliation can still pull one out of a bag |
| `Assets/Scripts/BagManager.cs` | `AssignClubToBag` refuses a lent club |
| `Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs` | `[NonSerialized] isBorrowed` / `isLentOut` |
| `Assets/Scripts/UI/Inventory/ClubData.cs` | the same two on `PlayerClubData` |
| `Assets/Scripts/Save/SaveData.cs` | `reconciledLoanIds`; `[NonSerialized] isBorrowed` on both persisted DTOs |
| `Assets/Scripts/InventorySync/InventoryCodec.cs` | skip borrowed rows on encode |
| `Assets/Scripts/InventorySync/InventoryProjector.cs` | carry the flag, so that skip is real |
| `Assets/Scripts/InventorySync/Tests/InventoryCodecLoanTests.cs` | NEW — 4 tests |
| `Assets/Scripts/Economy/PendingPointsOp.cs` | `LoanIds` + `loan_ids` on the wire (omitted when empty) |
| `Assets/Scripts/Economy/PendingOpsQueue.cs` · `PointsService.cs` | forward `loanIds` |
| `Assets/Scripts/Economy/Tests/PendingPointsOpLoanTests.cs` | NEW — 7 tests |
| `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs` | pass the round's loans to the queued earn |
| `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` | the loan layer + LEND/RETURN handler |
| `Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs` | the badge; level-up-ready forced off when lent |
| `Assets/Scripts/UI/Roster/UI/LevelUpModalController.cs` | `on_loan` → `LOAN_ERR_ON_LOAN`; a borrowed row gains LEVELS ONLY; SP controls hidden + `LOAN_SP_HINT` |
| `Assets/Scripts/UI/Inventory/ClubDetailPanel.cs` | the loan layer + LEND/RETURN handler |
| `Assets/Scripts/UI/Inventory/ClubThumbnailCard.cs` | the badge; durability-low suppressed on a borrowed club |
| `Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs` | freeze the round's loans in `ApplyPreloadSetup` |
| `Assets/Scenes/ShellScene.unity` | both COMPARE + LEND rows, both ribbons + dims, four modal instances |
| `Assets/Prefabs/UI/Modals/LoanModal.prefab` · `LoanReturnModal.prefab` · `Assets/Prefabs/UI/Loans/LoanRecipientRow.prefab` | NEW |
| `Assets/Prefabs/UI/Roster/CharacterThumbnailCard.prefab` · `CharacterThumbnailCardGlowUp.prefab` · `Assets/Prefabs/UI/Inventory/ClubThumbnailCard.prefab` | `LoanBadge` |
| `Assets/Art/RosterScreen/IconLoan{Out,In}{Small,Big}.png` | NEW — copied from `reference/`, imported as Sprites |
| `Assets/Art/RosterScreen/S_Loan{Ribbon,Badge,Row,RowSelected}.png` | NEW — generated |
| `Assets/Art/RosterScreen/ButtonConfirm.png.meta` · `ButtonLevelUpLong.png.meta` | 9-slice borders (inert for every current consumer) |
| `Docs/Scripts/make_loan_sprites.py` | NEW — the sprite baker |
| `Assets/Localization/LocalizationText.csv` · `LocalizationTextTable.asset` | 31 rows + the rebuilt bundled table |
| `Assets/Resources/Data/content_version.txt` | `texts` → v49 |
| `Docs/Architecture/UI_ELEMENT_PALETTE.md` | the loan atoms, the three button entries, the `ignoreLayout` warning |
| `Docs/AI_CONTEXT.md` | close-out |

**Pre-existing dirt, NOT introduced by this task** — present in the kickoff baseline recorded in
`HEARTBEAT.log` before any edit (`HEAD e8882599b`):

```
 M Docs/Reports/content_art.txt
 M Docs/TellCode.md
 M Docs/Versioning/last_uploaded_build.txt
?? Docs/Diagnostics/roster_locked_overlay/
?? Docs/Specs/Active/loading_tips/
?? Docs/Specs/Quick/flick_arrow_speed_retune.md
```

`playlife` also carries `M backend/routers/user.py` and five untracked migrations that predate this
session; the commit above stages only this task's five files.

---

## Deploy — done and verified

**Migration applied** by Cesar, 2026-09-09. All ten verification rows match:

```
table_golfin_loans                 1   1 expected
rls_on_loans                       1   1 expected
policies_on_loans                  0   0 expected — zero policies IS deny-all
live_partial_indexes               2   2 expected (lender+asset, borrower+ref)
events_on_behalf_of                1   1 expected
fn_loan_split                      1   1 expected
fn_loan_split_not_client_callable  0   0 expected
level_up_is_loan_aware             1   1 expected — the loan branch is in the deployed body
level_up_still_grandfathers        1   1 expected — the replace did not drop the grandfather seed
loan_share_not_ranked              0   0 expected — loan_lender_share must NOT be in game_point_actions
```

Row 9 is the one worth pausing on: `golfin_level_up` was REPLACED, not extended, so the check that
the grandfather seed survived into the deployed body is the difference between a working replace and
one that silently dropped the decision every existing player's levels rest on.

**`fly deploy`** ran after the migration, in that order — `points.py` imports `routers.loans`, so
deploying first would have left every loan call 500ing against a table that did not exist.

**Verified by the image version and live probes, never the exit code** (`flyctl` can 401 mid-run and
still report success — `reference_flyctl_401_false_deploy_failure`):

```
image   playlife-api:deployment-01M220GD2ZW514QM9M33FY8V1Y   (before)
     -> playlife-api:deployment-01M22D44WAHHF6EN4649BNYAWN   (after)
machines  v70 -> v71, both nrt

/health                                    200  {"status":"ok","version":"0.1.0"}
GET  /api/v1/loans                         403
POST /api/v1/loans                         403
POST /api/v1/loans/<uuid>/return           403
GET  /api/v1/nonexistent   (control)       404   <- what makes the 403 mean "mounted + auth-gated"
```

**No regression from the new import.** `/notices`, `/banners`, `/tournaments/golfin` and `/content`
all still 200; `/points/balance` still 403; `/progress/level-up` and `/points/earn-game` answer 405
to a GET, which is the POST-only shape they had — a broken import would have been a 500, not a 405.

**The 31 strings are live and being served**, read back off the deployed content endpoint rather
than off the CSV:

```
GET /api/v1/content?build=2823&catalogs=texts&since=texts:48
  texts version 49 · 31 changed rows · 31 of them LOAN_*
  first: {"id":"LOAN_BTN_CONFIRM","is_active":true,"min_build":2823,
          "data":{"key":"LOAN_BTN_CONFIRM","English":"LEND","Japanese":"貸す"}}
```

---

## Still needs a person

The **two-account E2E** in the SPEC's acceptance list. Nothing is blocking it any more — it needs
two signed-in accounts with a follow between them, a hole actually played on the borrowed asset, and
the SQL after each step. Concretely, and in this order:

1. A lends a character to B for 1 day → B's Roster shows it BORROWED, A's shows ON LOAN and locked.
2. B levels it once → the `golfin_progress` row is **A's**, `golfin_progress_events.on_behalf_of = A`,
   and B's RP is what moved.
3. B plays a hole with it → the ledger carries B's `hole_complete` at 80 % and A's
   `loan_lender_share` at 20 %, and A's row is **not** in `golfin_leaderboard` for that RP.
4. B taps RETURN → A's client shows the new level with unallocated SP and the `+N RP` toast.
5. The club variant, and the expiry case (set `ends_at` into the past by SQL; the next screen entry
   should reconcile with no relaunch).

Two smaller gates also remain, and neither is a person problem: the **Rule 21 UI fidelity lint** and
the **Rule 9 node re-pull** both need the Figma MCP, which is not authorised in this session.
