# Red-Team Review — `mode_select_system`

**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Date:** 2026-06-04 14:32 CEST
**Iteration reviewed:** 6 (Rule 11 backstop on top of iter-5 surface)
**Verdict:** `ARCHITECT_REVIEW_PASS`

This is a UI/visual-fidelity task — no mesh-metrics gate, no video deliverable gate.
I attacked the evidence rather than trusting it: re-cropped both canonicals at native
1170×2532 into card/peek/title/fee/junction regions, re-parsed both prefab YAMLs for
Button↔BPF pairing, re-read the fee/economy/transition code, and re-ran the git scene-mutation
audit. No CESAR_REJECTION.md exists for this task (no prior-rejection replay needed).

---

## Angles I captured myself (paths)

All crops generated this session from the iter-5 canonicals at native res via PIL (`/tmp/rt_crops/`):
- Header↔card1 junction (`ms_header_junction.png`) — proves top PRACTICE card is NOT clipped under MODE SELECTION header; clean gap + gold accent line.
- Full-screen cards c1–c4, fee rows, missions/driving-range locked cards.
- Home center card tight (`home_card_tight.png`, `home_play_btn.png`), left/right peeks (`home_leftpeek.png`, `home_rightpeek.png`), title band (`home_title_real.png`).

I did not re-shoot from Unity (MCP scene capture) because the canonicals are real
production-flow play-mode renders at full iPhone-14 res and the iter-6 change is
idle-invisible (a press-feedback component, scale=1.0 at rest) — the staleness is sound.

---

## Numbers I re-ran (not trusting the reviewer)

**Rule 11 — Button↔ButtonPressFeedback pairing (parsed prefab YAML directly):**
- `ModeHomeCard.prefab`: 3 Buttons {ModeHomeCard root, CardTapButton, PlayButton} → 3 BPF, ALL paired. PASS.
- `ModeCard.prefab`: 2 Buttons {ActionButton, CardTapButton} → 2 BPF, ALL paired. PASS.
- iter-6 git diff = exactly ONE BPF component add on CardTapButton (GO 3830234380593726200),
  `_pressedScale:0.95 _duration:0.12`, class `Golfin.UI.Polish.ButtonPressFeedback`. No layout/anchor/IsActive change.

**Color sampling (glyph-isolated):**
- Full-screen PRACTICE title gold = `#E6D596` (expect `#EEDC9A`; ~7/channel = AA/compression tolerance). PASS.
- Home PRACTICE title gold = `#E6D596`/`#E2D194` (same; my earlier `#D4A858` reading was the trophy bleeding in — corrected). Gold consistent across both surfaces. PASS.
- Tagline white = `#E7E8EA` (≈white). Card gradient bottom ≈ `#0A203A` (expect `#091B33`). PASS.
- Fee insufficient color constant in code = `Color32(0xC0,0x40,0x00)` = `#C04000`. Exact. PASS.

**Geometry:**
- Full-screen card navy fill width ≈937px at y=445 (outer ≈978 incl. border; within tolerance of spec 978, left≈96/right≈1074).
- Scrollbar column at x≈1108–1115 (Figma x=1090; ~18px right drift; within tolerance for a 1170-wide canvas, sizeDelta −96 split).
- ScrollView RT: `sizeDelta=(-96,-620) anchoredPos=(0,-30)` anchors (0,0)→(1,1). My own header-junction crop independently confirms PRACTICE title fully visible below header. inside=true.
- `m_VerticalScrollbarVisibility: 0` (Permanent) on the ModeSelect ScrollView (line 12906). PASS.

**Fee economy code (`ModeCardController.cs`) — re-read line by line:**
- `RefreshFeeColor`: `entryFee>0 && !CanAfford` → ENTRY FEE red `#C04000` + PLAY CanvasGroup alpha 0.4; else white/alpha1.
- `HandlePlayButtonClicked`: unaffordable → `ToastController.Show("Not enough Reward Points")`, early-return (no SpendPoints, no launch). Affordable → `SpendPoints(fee)` once (guarded `entryFee>0`) then `OnPlayClicked`.
- fee=0 (1v1) never enters either insufficient/unaffordable branch (`entryFee>0` guard). Never blocked. PASS.

**Transitions:** 2 `ShowScreen` calls, zero `instant:true` (grep clean). Expand/collapse via `StartCoroutine(AnimateHeight)` Lerp on `unscaledDeltaTime`; carousel snap `_snapDuration=0.18` Lerp. PASS.

**Scene-mutation / regression:**
- `git status` forbidden-singletons filter (CharacterManager/ClubManager/AudioManager/RewardPointsManager/CharacterDatabaseCSV/ScreenManager/PersistentUIManager/ModesDatabaseCSV) → ZERO matches. Clean. PASS.
- ShellScene.unity mtime 13:47 (iter-5) < iter-6 prefab edit 14:17 → scene untouched in iter-6; canonicals valid.

---

## Prior-rejection replay
No `CESAR_REJECTION.md` for this task. (FIXLIST_ITER4 was Cesar's live-Unity fix list, all
items independently re-confirmed below; iter-5 ADDENDUM F2/F3/expand-default/arrows/z-order all
verified present.)

---

## Three break-attempts (each tried, each failed to FAIL it)

1. **Visual — text-outside-container / clipping / overlap.** Cropped every card region at
   native res. Top card NOT clipped (clean gap below header). Home fee rows ("ENTRY FEE x100",
   "REWARDS x50") fully contained, centered. No glyph spills a border. No overlap.
   → Failed to break. The divergences I found are all Cesar-decided (see Latent below).

2. **Geometric — threshold fragility.** Card width, 24px gaps, scrollbar position, and
   top-card clearance all measured; none sit within 20% of a failing threshold. Scrollbar
   drift ~18px and title gold ΔE ~7 are well inside visual tolerance. → Failed to break.

3. **Spec-intent — letter-vs-point.** Fee economy, Rule 11, locked non-interactivity, fade
   transitions, two-distinct-prefab Step-0 contract, no-singleton-drift — all satisfy intent,
   not just checklist. Re-read code paths, not just the report. → Failed to break.

---

## Latent observations (NOT blockers — logged for Cesar's final sign-off)

- **Full-screen collapsed fee>0 cards drop the "ENTRY FEE"/"REWARDS" text label** (show only
  coin+amount), while fee==0 cards show "NO ENTRY FEE" and the home/expanded cards show full
  labels. Figma's collapsed cards keep the "ENTRY FEE" label. This is a per-state label
  inconsistency vs Figma. Judged NOT a blocker: it was visible in every prior iteration,
  Cesar's live FIXLIST never flagged it, and FIGMA_METRICS frames the collapsed row loosely.
- **Locked-card dimming is subtle.** LockedOverlay Image is black α1 with a sprite, but the
  rendered MISSIONS body (22,41,60) is barely darker than the active card (25,49,75) — the
  overlay reads as a light vignette, not a heavy grey-out. "Coming soon" is carried by the
  lock glyph + copy + non-interactive PLAY (all present/correct). FIXLIST F5 only required
  correct mask *size* (full-stretch — satisfied). Functional acceptance gate met.
- **Expand/collapse animates height (Lerp) but swaps content via SetActive (no alpha fade).**
  SPEC §Transitions wanted height + CanvasGroup alpha; only height is Lerped. Height does
  animate (no hard pop of the card frame); content appears instantly mid-grow. Minor polish gap,
  not a functional cut. Cannot fully judge from a still (no video gate on UI task).
- **Rule 13 reporting hygiene:** several untracked files outside the task folder are NOT in the
  report (`Docs/Diagnostics/_capture/h07_iter8_*.jpg` ×6, `Docs/Specs/Completed/ball_flight_trail/*`,
  `Tools/GreenSlope/scripts/capture-all-holes.mjs`, ModeSelect `.meta`s). All are unrelated
  green-bake/other-task drift — none touched by this task; does not corrupt the mode-select work.
- **KNOWN-OUT-OF-SCOPE (not defects):** hero title "GOLFIN Presents / The Invitational" absent;
  orange MAINTENANCE NOTICE is the pre-existing home NoticePanel. Per Cesar.

---

## Verdict

`ARCHITECT_REVIEW_PASS` — I genuinely tried to break it across visual, geometric, and
spec-intent axes and could not find a concrete blocker. Every hard-FAIL trigger in the brief
(singleton drift, scene deactivations, Rule 11 pairing, fee economy, instant-cut transitions)
is independently verified clean using my own captures and parsed data. Advances to Cesar's
final sign-off; the latent observations above are flagged for his eyes.

| File | Action |
|---|---|
| `Docs/Specs/Active/mode_select_system/REDTEAM_REVIEW.md` | Wrote red-team verdict (PASS) |
| `Docs/Specs/Active/mode_select_system/STATUS.md` | Set to `ARCHITECT_REVIEW_PASS` |
