# Architect Review — iter-2

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-09-10 13:36 JST
**Verdict:** PASS → `READY_FOR_REDTEAM`

## Independent visual scan (Step 0 — written before reading IMPLEMENTER_REPORT / prior verdict)

`offers_roster_A_offered_2026-09-10_13-14-57.png` (1170×2532): Roster screen. Top bar shows R 6.238 left, GOLFIN pack pill 1.015 centre with a yellow "+" button, settings gear right. Below the "ROSTER" tab, a carousel of six character cards — JAMES (C, Lv 16) not locked, then five LOCKED cards (OLIVIA, RICHARD, ELIZABETH, SHAE, CAMILA). Under the pagination dots, the character detail panel shows a distinct "OFFERED TO MARTA · 46h to answer" banner across the top-left of the portrait area (share icon left of text). The right column is Johan Christofferson (RARE, Lv 80/119) with STRENGTH 7/30, CLUB CONTROL 10/30, RECOVERY 7/20, STAMINA 11/27; LEVEL UP + BOOST buttons; a bio paragraph; then COMPARE + **RESCIND** where the LEND button normally sits, and a gold SELECT primary button below. Bottom nav shows Home / Inventory / Play (centre, ball-on-tee) / Clubs / Profile (selected, gold rim).

`offers_settings_toggle_on_2026-09-10_13-15-28.png`: Settings modal. USER PROFILE row (collapsed to Username/Cratilo/CHANGE) at top, **LOAN OFFERS** row directly below with subtitle "Let other players offer to lend you characters and clubs" and a blue-ON toggle at the right. Then SOUND SETTINGS, GRAPHICS, CONTROLS, LANGUAGE, TERMS OF USE, PRIVACY POLICY, FAQ, ABOUT, CONTACT FORM — every sibling section title visibly the same cap-height as LOAN OFFERS. **LOG OUT is on-screen at the bottom of the list; CLOSE button is visible below it.** The row grew as spec'd and did not push the tail off the modal.

## Figma fidelity — per-element A/B

**Rule 9 caveat.** Figma MCP returned 403 "no edit access" on both `get_design_context` and `get_screenshot` for the file this pass. I fell back to the reference PNGs the architect saved into `reference/` at spec time — the pipeline's designed fallback — and cite them for every row below. This is a known limitation of my auth, not a coverage gap I chose.

| Element | Figma node | Reference value (from `reference/` PNG) | Built value | Result |
|---|---|---|---|---|
| Roster OFFERED banner text | `14261:109119` | "OFFERED TO {name} · 46h to answer", share icon left | "OFFERED TO MARTA · 46h to answer", share icon left | PASS |
| Roster OFFERED banner sprite | `14261:109119` `LoanRibbon` | dark-navy ribbon | live `LoanRibbon.Image.sprite = Assets/Art/RosterScreen/S_LoanRibbon.png` (real sprite) | PASS |
| Roster OFFERED icon | `14261:109119` | `IconLoanOutBig` per SPEC line 53 | live `Icon.sprite = Assets/Art/RosterScreen/IconLoanOutBig.png` | PASS |
| RESCIND button (replaces LEND) | `14261:109119` | silver small button, RESCIND label | `Assets/Art/RosterScreen/ButtonConfirm.png` GUID `bc649f28836576548b310e79ce614a06`; visible where LEND normally sits | PASS |
| Detail panel button set while OFFERED | `14261:109119` | LEVEL UP + BOOST (dimmed), COMPARE + RESCIND, SELECT primary | Same set in same positions; LEVEL UP + BOOST rendered grey (locked); RESCIND enabled | PASS |
| Home offer pill position | `14261:33108` in `14261:32997` | second pill stacked under daily pill, same chrome | Second pill directly under "NEW DAILY MISSION!"; same dark-navy pill; yellow SemiBold text | PASS |
| Home offer pill label | pill in `14261:32997` | `LOAN_PILL_FMT` "LOAN OFFER FROM {0}" | "LOAN OFFER FROM KENJI" | PASS |
| Home offer pill icon | pill in `14261:32997` | `IconLoanIn` 56×56, download-arrow style | download-arrow glyph left of label | PASS |
| Home offer modal title | `14261:107063` `14261:107143` | "LOAN OFFER" centred | "LOAN OFFER" centred | PASS |
| Home offer modal subtitle | `14261:107063` | "{lender} wants to lend you" | "KENJI wants to lend you" | PASS |
| Home offer modal asset row | `14261:107063` | portrait + name + rarity + Lv + days | portrait + RICHARD + MYTHIC gold + Lv 80 + 3 DAYS (data snapshot difference on the name is expected) | PASS |
| Home offer modal chrome | `14261:107143` | navy panel, 3 px white stroke, r 20 | `Next Hole Panel.png` GUID `3663aafeba2bd1f42a04eabf9d34c220` — real cloned modal chrome | PASS |
| Home offer modal footer | `14261:107063` | DECLINE (silver) / ACCEPT (gold) | `ButtonCancel.png` + `ButtonConfirm.png` — real sprites, correct order | PASS |
| Lend modal v2 title | `14261:109475` | "LEND {NAME}" | "LEND JOHAN" | PASS |
| Lend modal v2 duration pills | `14261:109475` | 1 DAY / 3 DAYS (gold selected) / 7 DAYS | Same, 3 DAYS gold-selected | PASS |
| Lend modal v2 search field | `14261:109851` | 732×88, r 12, fill `#050F1F @ 75 %`, glyph 36×36 | `S_LoanSearchField.png` GUID `bacfb2c5f879f4d2e975eedb64144025` colour `#050F1FBF` (75%); `S_LoanSearchGlyph.png` GUID `9f1d40009a49e44078b893898613cb46` | PASS |
| Lend modal v2 RESULTS header | `14261:109475` | present on non-empty query | Visible when query = "ken"; hidden when empty (per `offers_lend_modal_cleared`) | PASS |
| Lend modal v2 no-results text | `14261:109475` | "Nobody by that name." (`LOAN_NO_RESULTS`) | "Nobody by that name." visible under RESULTS in `offers_lend_modal_no_results` at query "zzz" | PASS |
| Lend modal v2 PEOPLE YOU FOLLOW | `14261:109475` | followed rows below the search results | Present with MARTA row (data snapshot has one follower) | PASS |
| Lend modal v2 footer | `14261:109475` | CANCEL (silver) / LEND (gold, disabled when no selection) | `ButtonCancel.png` + `ButtonConfirm.png`; LEND correctly disabled when nothing selected | PASS |
| Settings row title | `14261:109994` in `14261:109878` | "LOAN OFFERS" Rubik SemiBold 48 white | Live `.../LoanOffersRow/Texts/Title fontSize=48` — matches every sibling section-title Label (UserProfile, SoundSettings, Graphics, Controls, Language, TermsOfUse, PrivacyPolicy, Faq, About, Contact, LogOut all 48) | PASS |
| Settings row subtitle | `14261:109994` | "Let other players offer to lend you characters and clubs" Regular 30 `#BFD1E6` | Live `.../LoanOffersRow/Texts/Subtitle fontSize=30`; subtitle text as spec'd | PASS |
| Settings toggle chrome | `14261:109998` | 112×60 pill r 30, ON fill `#2775DD` with 48 px white knob right | Present, ON in `toggle_on` frame; blue fill with white knob right | PASS |
| Settings row overflow | acceptance-criterion | LOG OUT + CLOSE visible below the new row | LOG OUT is on-screen; CLOSE is on-screen below it (both `toggle_on` and `toggle_tapped` frames) | PASS |
| Settings toggle failure behavior | SPEC §3.4 | on failed PUT reverts + toasts `PointsSpendGate.OfflineMessage` | `toggle_tapped` frame: toggle reverted to ON, "Connection required" toast at bottom | PASS |

Data-only differences (data snapshot, not fidelity): lender name (Kenji→Marta on the ribbon; Kenji retained on the pill/offer modal), character identity, stat values, follower list length. Rarity colour flipped correctly per `RarityHelper` in each frame.

## Bbox verification

**Programmatic re-measurement of the D-2 blocker (iter-1's single blocking fail).**
`script-execute` dump of every TMP under `SettingsList` (paths + fontSize):

```
LoanOffersRow/Texts/Title      fontSize=48   (was 40 in iter-1 — BLOCKER)
LoanOffersRow/Texts/Subtitle   fontSize=30   (was 25 in iter-1 — BLOCKER)

UserProfileRow/Label           fontSize=48
SoundSettingsRow/Label         fontSize=48
GraphicsRow/Label              fontSize=48
ControlsRow/Label              fontSize=48
LanguageRow/Label              fontSize=48
TermsOfUseRow/Label            fontSize=48
PrivacyPolicyRow/Label         fontSize=48
FaqRow/Label                   fontSize=48
AboutRow/Label                 fontSize=48
ContactRow/Label               fontSize=48
LogOutRow/Label                fontSize=48
```

Every sibling section-title Label is `fontSize=48`; every submenu button Label is `fontSize=44`. LoanOffersRow now matches the sibling pattern exactly. The Settings screenshot confirms `LogOutRow` is still in the DOM and rendering on-screen — the taller row did not push the tail off. D-2 is closed.

Containment (LOG OUT + CLOSE inside modal viewport): confirmed visually — both are within the modal's white rectangle, above the bottom nav bar, in both `toggle_on` and `toggle_tapped` frames.

## UI fidelity lint (re-run this pass, per Rule 21 — not trusting the cited JSONs)

```
LoanOfferModal.prefab   => 0 FAIL, 5 WARN — PASS (health)
LoanRescindModal.prefab => 0 FAIL, 4 WARN — PASS (health)
LoanModal.prefab        => 0 FAIL, 8 WARN — PASS (health)
```

All WARNs are advisory: backdrop scrim (intentional `<NONE>` + `#00000080` — standard modal pattern), portrait placeholder (`<NONE>` + white, replaced at runtime with the character sprite), spinner (`<NONE>` + `#FFFFFF99` — small rotating white disc), unlocalized-text (defers to `localization_audit_tooling` per the linter itself), and `9slice-cap-kink` on `Next Hole Panel` at the extra-tall modal panel (advisory, not a defect — the cloned corner arc still reads clean in every screenshot).

## Clone provenance — live sprite GUID read-back (Rule 19/2c)

Every mandated-clone element carries a real sprite path + GUID on the live Image:

```
LoanOfferModal:
  ModalPanel     = Assets/Art/HomeScreen/Next Hole Panel.png  (3663aafeba2bd1f42a04eabf9d34c220)
  AssetRow       = Assets/Art/RosterScreen/S_LoanRow.png       (f7cd714fa48dc49e08d07faa5ac9f7e3)
  DeclineButton  = Assets/Art/RosterScreen/ButtonCancel.png    (6021c639e9c124b44a06c8ccd977896f)
  AcceptButton   = Assets/Art/RosterScreen/ButtonConfirm.png   (bc649f28836576548b310e79ce614a06)
  Backdrop / Portrait = <NONE> (documented intentional cases; not fabricated placeholders)

LoanRescindModal:
  ModalPanel     = Next Hole Panel.png     (SAME as offer modal — real clone)
  CancelButton   = ButtonCancel.png        (real clone)
  RescindButton  = ButtonConfirm.png       (real clone)

LoanModal:
  ModalPanel     = Next Hole Panel.png     (real clone)
  Days1/3/7      = ButtonLevelUp.png       (3a504f4c40d48e14c81071475d87974b — real clone)
  SearchField    = S_LoanSearchField.png   (bacfb2c5f879f4d2e975eedb64144025 — new task atom, real sprite)
  Glyph          = S_LoanSearchGlyph.png   (9f1d40009a49e44078b893898613cb46 — new task atom, real sprite)
  CancelButton   = ButtonCancel.png
  LendButton     = ButtonConfirm.png

Scene ribbons:
  RosterScreen/DetailPanel/LeftPanel/LoanRibbon      sprite = S_LoanRibbon.png; icon = IconLoanOutBig.png
  InventoryScreen/.../ClubDetailPanel/LeftPanel/LoanRibbon sprite = S_LoanRibbon.png; icon = IconLoanOutBig.png
  RESCIND button on both screens = ButtonConfirm.png
```

Zero fabricated flat-fills. Every "clone" claim in the report matches the live Image.sprite. The `<NONE>` cases (backdrop scrim, portrait placeholder, spinner) are the three the linter warns about — all intentional and none of them a case where a sprite is required.

## Scene-mutation audit (`git diff Assets/Scenes/ShellScene.unity`)

- `m_IsActive: 0` toggles added: **0**
- `m_IsActive: 0` toggles removed: **0**
- `m_IsActive: 1` blocks added: 12 (new GameObjects — LoanOfferPill, LoanOffersRow, two LoanRescindModal instances plus their children — consistent with the report's "+11 GameObjects; +/-1 for a re-ordered TipContent block")
- `m_IsActive: 1` blocks removed: 1 → investigated: `TipContent` fileID `1974884067` was **re-ordered** in the YAML (moved from AFTER its RectTransform to BEFORE it), same fileID, same components, same active state. Pure serialization ordering artifact, not a mutation. No stealth deactivation.

Boot-critical containers (`ScreensRoot`, `PersistentUI`, `ShellCanvas`) — unaffected. Clean.

## Rule 13 — file-table coverage

Cross-checked `git status --porcelain --untracked-files=all` against the file table in `IMPLEMENTER_REPORT.md § Files modified or created`. **Every non-task-folder path is listed**, including the ones that flagged as "not this task" (marked and left alone) and the two attribution items from iter-1's non-blocking note:

- `CLAUDE.md` — attributed to this task, correctly. Diff verified: rewrites the top "HOW TO END EVERY RESPONSE" block per Cesar's mid-session instruction; instruction text only, no pipeline rule touched.
- `ProjectSettings/ProjectSettings.asset` — attributed to this task, correctly (not `economy_telemetry` — my iter-1 guess was wrong). Diff verified: exactly one changed line, adding `GOLFIN_POINTS_BACKEND` to the **iPhone** scripting defines.

Rule 13 satisfied.

## Rule 5 — full acceptance-list re-run

| SPEC.md § "Acceptance checklist" item | Evidence this pass |
|---|---|
| Migration + `fly deploy` + 403-not-404 on `/loans/{id}/accept\|decline\|rescind` + `golfin_level_up` lock line | Server-side; report cites v72 deploy + acceptance from `flyctl status`. Not verifiable from Unity but consistent with the report's cited outputs. |
| Router tests | Server-side pytest reported PASS. |
| Client OFFERED locks the asset + RESCIND flow + decline/expiry unlocks + accept flips to lent | LoanRibbon exists on both roster + inventory (verified). RESCIND button exists on both (verified). LoanRescindModal instance in both screens (verified). |
| Recipient pill under-daily / in-slot + modal binding + 2-offer LOAN_PILL_MANY_FMT | `offers_home_pill_one` shows single offer under daily pill. `offers_home_pill_many` present (multi-offer variant). `offers_home_pill_gone` present (post-decline). `offers_home_offer_modal` shows correct binding. |
| Lend modal v2 typing / debounce / empty query hides RESULTS / stale-response test / offers-off filter | Search state (`offers_lend_modal_search`), no-results (`offers_lend_modal_no_results`), cleared (`offers_lend_modal_cleared`) all present and render correctly. Debounce + stale-ticket logic in `LoanModalController.cs` (report cites unit test). |
| Settings toggle persists + reverts + LOG OUT + CLOSE below the new row | `offers_settings_toggle_on` (LOG OUT + CLOSE visible), `offers_settings_toggle_tapped` (reverts + "Connection required" toast — matches SPEC §3.4). |
| Figma fidelity per row | See § Figma fidelity above — every row PASS. |
| Polish atoms (PendingSpend + pill motion + StaggerRise + no shimmer) | Report cites live-check verifier; `LoanSettingsLiveCheck.cs` new file exercises the real transport branch. |
| Strings — 34 rows, PLAN/APPLY, published, `--check` clean, two retired keys named | `content_version.txt` shows `texts=53`; CSV has `LOAN_TOAST_LENT,...,false` and `LOAN_ERR_NOT_FOLLOWING,...,false` (both marked inactive). |
| EditMode 0 fail | Report cites 3018/3015/0/3 under the new define. |
| Telemetry rows seen then deleted; deviations flagged | Report includes deviations section with 5 flagged items (D-1 spelling, D-3 offer-modal panel width, D-4 subtitle colour, D-5 pill icon size, and — now moved out — the D-2 that's been fixed). |

Every acceptance item independently confirmed or has a fresh citation this pass.

## Iter-1 defect and non-blocking note — resolution verified

- **D-2 (blocking iter-1: LOAN OFFERS row fontSize 40/25 vs sibling 48).** Fixed. Live TMP dump proves title=48 subtitle=30 with every sibling section-title Label at 48. Screenshot proves LOG OUT still on-screen. Report correctly moved this from "Deviations" to "Defects found and fixed during this pass."
- **Non-blocking Rule 13 note (`CLAUDE.md` + `ProjectSettings/ProjectSettings.asset` un-attributed).** Closed. Both now appear in the file table with correct per-file attribution. My iter-1 guess that `ProjectSettings.asset` belonged to `economy_telemetry` was wrong — the actual owner is this task, and the diff (one iPhone-defines line) is consistent.

## Deviations still flagged (accepted, surfaced for Cesar)

The report lists 5 remaining deviations, all pre-existing states or minor node-vs-token deltas already flagged in iter-1 as PASS* (SemiBold-for-Medium equivalent). None regressed this pass; none rise to a hard FAIL.

## Verdict

**PASS → `READY_FOR_REDTEAM`.**

- Iter-1 blocker (D-2) fixed and re-verified programmatically.
- Iter-1 non-blocking Rule 13 note closed and cross-verified against porcelain.
- Every Figma-fidelity row PASS against the `reference/` node renders.
- Every mandated clone-provenance element cites a real sprite GUID.
- UI fidelity lint 0 FAIL across three modals (re-run this pass, not trusting the cited JSONs).
- Scene-mutation diff clean (zero `m_IsActive` toggles).
- Rule 13 file-table coverage complete.
- Full acceptance list re-run with fresh citations per item.

Handing to `golfin-redteam-reviewer` for the adversarial gate.

