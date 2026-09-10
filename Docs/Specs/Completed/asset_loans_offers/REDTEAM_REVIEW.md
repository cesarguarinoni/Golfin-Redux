# Red-Team Review — iter-2

**Reviewer:** golfin-redteam-reviewer
**Timestamp:** 2026-09-10 14:05 JST
**Verdict:** PASS → `ARCHITECT_REVIEW_PASS`

I tried to break this across visual, geometric and spec-intent axes, re-derived every number
off primary sources rather than trusting the two prior reports, and could not find a blocker.

## Angles I captured / evidence I generated myself (not re-used)

- **Live Figma node pull — Figma MCP did NOT 403 for me** (the reviewer's Rule-9 gap is closed).
  `get_design_context 14261:109994` returns `text-[48px]` title / `text-[30px]` subtitle — so the
  iter-2 fix (48/30) is correct against the **live node**, not just the `reference/` PNG. Downloaded
  live renders of `14261:109878` (settings), `109119` (roster offered), `107063` (offer modal),
  `109475` (lend modal) and A/B'd each against the built captures.
- **Static scene dump** of `SettingsList` (my own `script-execute`): `LoanOffersRow` Title
  fontSize=48 (Rubik-SemiBold SDF), Subtitle fontSize=30 (BFD1E6); every sibling section-title
  Label = 48 (Sound/Graphics/Controls/Language/Terms/Privacy/Faq/About/Contact/LogOut/UserProfile).
  Row sizeDelta=(−48,124) = RowH 124. HLG pad 24 gap 24. `SettingsList` has
  ContentSizeFitter(PreferredSize)+VLG (reflows). UserProfileSubmenu = 392 tall. LogOutRow +
  CloseButton both present & active.
- Read the built settings, roster-offered (canonical), offer-modal, home-pill (one+many),
  lend-modal, rescind-confirm and toggle-tapped captures at full 1170×2532.

## Metrics I re-ran (my numbers)

| Check | My result | Report claim | Verdict |
|---|---|---|---|
| Settings Title fontSize | 48 (live TMP) | 48 | ✅ matches node |
| Settings Subtitle fontSize | 30 (live TMP) | 30 | ✅ matches node |
| Sibling section-title Labels | all 48 | all 48 | ✅ (D-2 shape closed — read off `fontSize`, not `sizeDelta`) |
| RowH | sizeDelta.y=124 | 124 | ✅ |
| `GOLFIN_POINTS_BACKEND` define | True for Assembly-CSharp/-Editor/Golfin.Economy in Editor+Player under iOS target (`CompilationPipeline.GetAssemblies`) | active | ✅ genuinely on (not the vacuous `CompiledDefault` probe) |
| EditMode suite (my re-run) | 3018 / 3015 pass / **0 fail** / 3 skip (2m36s) | 3018/3015/0/3 | ✅ |
| UIFidelityLinter (my re-run, all 5) | LoanOfferModal 0F/5W · LoanRescindModal 0F/4W · LoanModal 0F/8W · **LoanRecipientRow 0F/0W** · LoanReturnModal 0F/5W | 0 FAIL | ✅ (reviewer never re-ran LoanRecipientRow) |
| Rule 19 pill clone (live readback) | Panel `448cb5f34eebb4b38962e7959d0a11ed`, Glow `086acc78ed8a34ce090a7cec8d2d5aea` — **identical guids to live DailyMissionPill** | cloned object-for-object | ✅ real clone |
| Lender dim (my luma, portrait crop) | FREE 106.43 · OFFERED 72.43 · LENT 72.43 | offered dimmed, offered==lent | ✅ (absolute differs by crop; relationship identical) |
| ProjectSettings.asset diff | exactly 1 line: `+iPhone: GOLFIN_POINTS_BACKEND;…` | single line, iPhone defines | ✅ |
| ShellScene m_IsActive | 0× `:0` added; +12/−1 `:1` (−1 = TipContent reorder) | no stealth deactivation | ✅ |

## Prior-rejection replay

No `CESAR_REJECTION.md` — iter-1 was a **reviewer** FAIL on one item (D-2, Settings row fontSize 40/25).

| Prior defect | Verdict | Proof |
|---|---|---|
| D-2: row shipped 40/25, defended with "siblings render 48 at 40" (that 40 was `sizeDelta.y`, not `fontSize`) | **GONE** | Live TMP: title=48, subtitle=30; every sibling Label=48; node `14261:109994` = 48/30; cap-height matches SOUND SETTINGS in the capture |
| iter-1 #2: offer-modal asset name rendered nothing (chars=0) | **GONE** | Built offer modal renders "RICHARD" clearly |
| iter-1 #4: lend modal's two sections drew on top of each other | **GONE** | RESULTS and PEOPLE YOU FOLLOW cleanly separated |
| iter-1 #3: rescind modal stuck open under later captures | **GONE** | Search frames show the lend modal, not a stacked rescind dialog |

## Three break-attempts (all failed)

1. **Visual.** A/B'd 5 built captures vs 5 live node renders. Every element matches with only
   expected data-snapshot differences (character/lender names, follower counts, RarityHelper
   colours). No oval pill / distorted radius / flat-fill fabrication (linter render-health 0 FAIL).
   LOG OUT + CLOSE on screen, unclipped. **Failed to find a wrong pixel.**
2. **Geometric.** Re-derived every metric above off primary sources — all correct, and this time
   `fontSize` was read off `fontSize` (not the D-2 wrong-property trap). The only margins near a
   threshold are the one-line text slots (TextSlot(30)=42 vs ~35.5 line box, ~18%), but the
   real-play captures prove title AND subtitle render fully, so the margin is proven-adequate.
   **Failed to find a metric past threshold.**
3. **Spec-intent.** Full offer lifecycle is present and correct: OFFERED ribbon reads the **offer**
   clock ("46h to answer", not 0h), asset locks/dims (0.68× luma), RESCIND in the LEND slot, home
   pill (single "LOAN OFFER FROM KENJI" / multi "2 LOAN OFFERS" / gone), offer modal binds real
   art+name+rarity+terms(20%)+"expires in 46h", lend modal search finds anyone, settings toggle
   persists + reverts-with-"Connection required"-toast, 24h cooldown surfaced in the rescind copy.
   **Failed to find a missed intent.**

## Deviations — judged, each acceptable

- **D-1** (row anchor EMAIL/ACCOUNT ID/DELETE ACCOUNT doesn't exist in scene → placed last in
  submenu): **validated** against the live node — the design draws those three; the shipped
  submenu has only USERNAME/CHANGE. "Last in submenu" is the honest resolution. The design also has
  fewer top-level rows than the shipped game (no GRAPHICS/CONTROLS/LOG OUT), which is exactly why
  the node render clips and the game (correctly) does not.
- **D-3** ("Lv 80" unlocalised): matches the project's own level-chip convention. OK.
- **D-4** (architect defaults 48h/3/24h/clock-at-accept): SPEC's defaults, surfaced for Cesar; the
  24h is shown to the player in the rescind copy. A Cesar-awareness item, not a defect.
- **D-5/D-6/D-7**: minor, surfaced, non-blocking. D-7 (DumpSettingsDiagnostics false-negative on
  CloseButton) independently disproven — CloseButton exists, is active, and is visible on screen.

## Notes for Cesar (non-blocking — report hygiene, NOT fabrication)

Three internal numbers are stale in different rows of `IMPLEMENTER_REPORT.md`, but each real value
is verifiable and correct (I re-derived all three; the reviewer's cited values agree with mine — so
this is not a me-vs-reviewer disagreement and not gamed evidence):
- acceptance row says `content_version.txt texts=52`; actual file + file-table + For-Cesar say **53** (confirmed 53).
- one row says the submenu "grew 248 → 372"; actual UserProfileSubmenu = **392** (report's Defects §6 also says 392).
- test row says "3 skipped" (correct) then prose says "all 4 skips"; my run = **3 skipped**.

Backend (`playlife/`) is a **separate repo, not in this tree** — the migration file is not present
here and cannot be verified from the client repo; scoped by the kickoff as already-live (v72,
migration 11/11, two-JWT E2E). Client is what I reviewed.

## Verdict

**PASS → `ARCHITECT_REVIEW_PASS`.** All 8 flagged attack surfaces resolved in the work's favour on
independent re-derivation; three genuine break-attempts failed. Advancing to Cesar.
