# tournament_entry_denied_modal — implementation note

Replaces the entry-refusal **toast** with the pop-up in Figma `13915:2273`, with the list of
requirements adapted to whichever ones the player actually failed, over a darkened backdrop.

Reference render: `reference/node_13915-2273.png` · Built renders: `screenshots/`

## What changed

| File | Change |
|---|---|
| `Assets/Scripts/Tournaments/TournamentEligibility.cs` | `TournamentRequirement` + `UnmetRequirements()` — returns EVERY unmet rule, with the bound that was missed and whether it was a floor or a ceiling. `Evaluate()` is untouched and still returns the first failure for server parity. |
| `Assets/Scripts/TournamentsRuntime/TournamentRulesText.cs` | `DeniedBody()` / `DeniedBodyFull()` compose the modal copy; `RarityNameTag()` renders a spelled-out, localized, rarity-coloured value. |
| `Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs` | `_deniedDialog` / `_deniedBodyText` / `_deniedBackButton`; `ShowDenied()` / `HideDenied()`; the client gate and the server `full` / `ineligible` denials all route here instead of to a toast. |
| `Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab` | New `DeniedDialog` child. |
| `Assets/Localization/LocalizationText.csv` | 19 rows, EN + JA (**JA drafted, flagged for native review**). |
| tests (2 files) | 14 new — the list, bound direction, Evaluate parity, the composed copy in both locales, and that every refusal headline resolves in both. |

## Clone provenance

Cloned wholesale from `InGameSettingsModal > ConfirmDialog`, which is the same
backdrop + card + separator + button shape. Nothing was hand-rolled.

| Element | Source | Verified on the saved asset |
|---|---|---|
| `DeniedBackdrop` | `ConfirmBackdrop` | `#0000008C`, full stretch, `raycastTarget` on |
| `DeniedCard` | `ConfirmCard` | sprite `Background - HoleCard` |
| `DeniedSeparator` | `Separator2` | sprite `Divider`, 882×2 |
| `DeniedBackButton` | `ConfirmBackButton` | sprite `ButtonCancel`, 359×120, `ButtonPressFeedback` present |

`Title`, `Separator1` and `ConfirmQuitButton` were deleted — the node has one text block and one
button. `LocalizedText` was removed from `DeniedBody` because its content is composed at runtime and
a key lookup would clobber it.

## Decisions worth a second opinion

**Nested GameObject, not a second `ModalController`.** `InGameSettingsModal`'s ConfirmDialog does
exactly this. A nested `ModalController` would double-count `OpenModalCount` and fight the signup
modal's own show/hide.

**The card height is content-driven** (`VerticalLayoutGroup` + `ContentSizeFitter`), because the list
is 1–3 requirements long: measured 451px for one, 712px for three. A fixed height would clip the
worst case or leave a hole in the common one.

**The value colour.** The node paints `UNCOMMON` in `#2775DD`. One hardcoded blue cannot be right for
six rarities, so a rarity value takes its own `RarityHelper` colour (the same source the RULES block
and every card badge use) and `#2775DD` is kept as the highlight for non-rarity values — levels and
player caps.

**The rarity word is localized, the colour is not.** `GetRarityFullName` returns hardcoded English
caps; this is the one actionable value on a refusal screen, so a JP player must not be sent to find
an "UNCOMMON" character. JA renders アンコモン / レジェンダリー in the right colours.

## Every refusal is the pop-up now (2026-08-21, Cesar)

Nothing that blocks a signup is a toast any more. The only surviving `ShowToast` is the fallback
inside `ShowDenied` for a dialog that was never wired — a refusal must never be swallowed silently.

| Path | Copy |
|---|---|
| Requirements not met (client gate) | headline + one line per unmet rule |
| Server `ineligible` | the local list, falling back to the server's single reason |
| Server `full` | `MAXIMUM PLAYERS:` + the cap it enforced |
| Short balance (client pre-check AND server) | `ENTRY FEE:` + `YOUR BALANCE:` — both numbers, because the gap is the actionable part and both paths already know it |
| Offline / connection required | headline only |
| Tournament gone | headline only, and **BACK closes the signup modal too** — there is nothing to go back to |
| No character / service down / register failed | headline only |

## Width

The card is **1020** wide against the signup panel's **978** — a 21px overhang per side, so the two
stacked cards no longer share an edge. 1020 is the node's own outer width, so this is the design's
number rather than an arbitrary nudge. The text boxes are pinned at 882 by their own `LayoutElement`
and did **not** follow the card; verified unchanged on the saved asset.

## Not verified

- **On device.** Renders are isolated-canvas at 1170×2532, not a real play session.
- **JA font weight.** The JA values render lighter than the EN ones — the CJK fallback face, not a
  weight this modal sets. Pre-existing across the app; worth a look but not fixed here.
- **JA copy** — 13 new rows, native review outstanding.


---

## Video proof (2026-08-21)

`videos/entry_denied_all_states.mp4` — 1170×2532, 51.5s, captioned. Recorded with
`TournamentDeniedDemoRecorder` (`GOLFIN > Tournaments > Record Entry-Denied Demo Video`), cloned
from `TournamentBannerDemoRecorder` rather than a hand-rolled capture path. A copy is in
`Docs/Reports/Media/` for the daily report. Both paths are gitignored, so the master lives in the
task folder only.

**Real vs driven — stated on the frames themselves.**

| Phase | State | How |
|---|---|---|
| A | Requirements not met | **REAL.** Common character selected, real prod tournament `restricted_test_open` opened, the player's own CONFIRM clicked. Self-audited in the log: `entry=NONE (refused)`. It broke TWO rules at once — rarity *and* club cap — so the multi-requirement list is proven on real data, not a fixture. |
| B | Several requirements, server `full`, short balance, offline, tournament gone | **DRIVEN.** The recorder calls the same `ShowDenied` the network callback calls, with the same composed body. Reaching these for real needs a full field, a forced socket failure and a drained wallet. |

### Background: correct (re-recorded signed in, 2026-08-21)

The first two takes were shot on an Editor with no session, so boot parked on `ScreenId.Login` and
every `ShowScreen(TournamentSelection)` was routed straight back — the pop-up floated over the auth
form. Cesar signed in and it was re-recorded. The self-audit now reads:

```
[DeniedDemo] Settled on TournamentSelection.
[DeniedDemo] PHASE A real CONFIRM → entry=NONE (refused)
```

The clip shows the real Tournaments screen (RP balance, filter tabs, the live Kisarazu card, bottom
nav), the sign-up modal over it, and the refusal over both — everything behind correctly dimmed.

### Captions are stamped by the recorder, not hand-authored

`TournamentDeniedDemoRunner` writes `videos/captions.json` against real elapsed time since
`StartRecording()`, which `build_bot_video.py --mode captionsjson` consumes. Hand-written timings
were re-timed twice against clips whose length changed underneath them (39.0s → 51.5s → 40.7s); a
sidecar the recorder emits cannot drift from the clip it describes.

### Also worth knowing

- `daily_report.py --test` is **not** a dry run: it really sends (to the TEST channel) and deletes
  drop-folder files afterwards. Re-copy the master before the real 20:30 run.
