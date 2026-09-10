# REDTEAM_REVIEW — `screen_hints`

**Red-team reviewer:** golfin-redteam-reviewer (iter-1)
**Verdict:** ARCHITECT_REVIEW_PASS — tried to break it, could not.
**When:** 2026-09-10 16:05 JST

---

## Angle I captured myself (re-shoot, not re-use)

I re-ran the real-flow verify bot myself (`GOLFIN ▸ Hints ▸ Run verify bot (EN)`,
`ExecuteMenuItem` = True) — it booted the app through the Splash StartButton, drove real
widgets ("tapped real ActionButton"), pressed the modal's own buttons, and ran to `DONE`
(241 lines) in ~111 s. Editor returned to edit mode on ShellScene, no scene/prefab drift
(`git diff --stat HEAD` empty for ShellScene, LabScaffold, ScreenHintModal.prefab both
before and after my run).

- **Canonical regenerated fresh:** `screenshots/roster_hint_1of4.png` md5 changed
  `3924c57a…` → `342e604e…` (a genuinely new capture, not the reviewer's blessed frame).
  I opened it: valid 1170×2532, not flipped, PRO TIP / 1/4 / gold spans / 6 rarity rows /
  CONTINUE alone (no BACK) — matches `reference/figma_14263-109304_roster_hint_1of4.png`.
- I also opened the harshest non-UI-flow frames: `gameplay_hint_1of6.png` (real Lomond
  hole 2 behind, SPIN/STRAIGHT/GOLFIN/DRIVER shot UI dimmed under the scrim, not flipped),
  `generalshop_hint_single.png` (no counter, CLOSE alone, no BACK), `roster_hint_4of4.jpg`
  (BACK+CLOSE, matches 4/4 node render), `ja_home_hint_2of2.jpg` (プロのヒント / JA body /
  戻る + 閉じる / 2/2).

## Metrics I re-ran (numbers, not adjectives)

- **Rule 21 lint (re-run by me):** `UIFidelityLinter.LintPrefab(prefab, null)` →
  `0 FAIL, 1 WARN, 0 INFO — RESULT: PASS (health)`. Single WARN = `DimBackground` flat-fill,
  the intentional cloned scrim (inactive in the prefab, activated at runtime). Matches
  reviewer + on-disk JSON.
- **Rule 19 live prefab dump (re-run by me):** `Background`=`Background - HoleCard`,
  `ModalSeparator`=`Divider`, `BackButton`=`ButtonCancel`, `NextButton`=`Button - Retry`,
  `DimBackground`=`<NONE>` (intentional), `tipSprites` count=34 / nullSprites=0,
  `ModalBackdropDismiss` count=0, `animateShow`=True, Canvas sortingOrder=600, both buttons
  `LayoutElement (450,120)` and carry `ButtonPressFeedback`. No fabricated flat-fill.
- **LocalizedText keys read live (the one thing not set in code at runtime — BACK label):**
  `TitleText.key='TIP_HEADER'`, `BackButton/Text.key='HINT_BACK'`,
  `NextButton/Text.key='HINT_CONTINUE'` (swapped to CLOSE at rebind), `TipText.key=''`
  (bound per hint). The stale `.text` values in the prefab (`ROSTER_STARTER_BACK` etc.) are
  cosmetic serialized display that `LocalizedText` overwrites — screenshots confirm BACK /
  CONTINUE / CLOSE render correctly. **Not a mis-key.**
- **CSV:** 36 rows / 18 distinct screens (`grep` count).
- **Localization:** `TIP_RP` rewritten EN+JA (no "ONLY"/"NEVER FOR SALE"),
  `HINT_CONTINUE/CLOSE/BACK` present EN+JA all `true`; `content_version.txt` `texts=54`.
  Only `.text=` writes in `Assets/Scripts/UI/Hints/*.cs` are the counter digits (spec: not
  localised) and the ProTipCard raw-key fallback.
- **Fresh bot behavioural rows (re-derived from MY run's `verify_en.log`):** HOME 1/2 CONTINUE
  alone → 2/2 BACK+CLOSE → BACK → 1/2 CONTINUE alone (seenKeys unchanged, default e);
  `DOUBLE TAP index 1→1` then hint 3 (one advance — swap guard holds); GENERALSHOP
  `counter=HIDDEN back=ABSENT gold='CLOSE'`; MISSIONSELECTION nothing (default b); STACK: hint
  waits behind SchemeConfirmModal then appears; `SETTINGS opened: hint visible=False` (Controls
  OnEnable does NOT fire on Settings open) → CONTROLS `TIP_CONTROLS counter=HIDDEN`, sorting
  hint=600 settings=500, second open nothing; GAMEPLAY 1/6…6/6 `scene=LabScaffold`,
  `RAYCAST at cone: top=DimBackground sortingOrder=600` (scrim eats shot input), `tee-idle
  0.00s → 1.01s` (restart from zero), re-entry nothing. Every row reproduced identically to
  the reported log.
- **Real-entry (Rule 2):** `ScreenHintPresenter` subscribes `ScreenManager.ScreenChanged`;
  `GameplaySceneLoader.cs:200` and `ControlsSubmenu.cs:51` call `NotifyScreenEntered`. No
  `*Gate` scenario; `Scenarios.cs` and `Assets/Scripts/Physics/` untouched (git diff empty).

## Prior Cesar rejections

None. No `CESAR_REJECTION.md`. The "Hint first" stacking decision (commit `68cb8dac0`) was
Cesar accepting the code's existing order, not a rejection — nothing to replay.

## Three break attempts (all failed)

1. **Visual.** Scanned my freshly re-shot canonical + gameplay + single-hint + 4/4 + JA at
   full 1170×2532 against the four `reference/` node renders. No wrong pixel, seam, flip, or
   broken UI. The only differences are game-state data (roster contents, R value, SELECT vs
   SELECTED) and the two pre-accepted deviations below — not defects. Could not break.
2. **Geometric.** No metric sits near a hard threshold. Plate ~21 px shorter on 3-line bodies
   is the loading card's own TMP line-height (spec §4 "copied from the loading card, same
   style" — accepted). Button width 450 vs node hug 428 is an explicit §4 instruction (Unity
   Main Button width). Sorting 600 > settings 500, LayoutElement pins (450,120), counter
   (−64,−36) all correct. Could not break.
3. **Spec-intent.** The goal — relevant tips shown in-context, once per device per screen, one
   at a time, with BACK/CONTINUE/CLOSE + `n/X` counter over a scrim, in the real production
   flow — is fully realised and reproduced in my own run. The gameplay hint opens over the
   revealed tee (not the loading screen) and the scrim blocks the pull. Could not break.

## Accepted deviations (pre-reported, not blockers)

3-line plate ~21 px shorter (loading-card line height); button width 450 vs 428 (spec §4);
gameplay on Lomond hole 2 not hole 1 (nothing in the hint path depends on the hole);
"second hole nothing" proven via the same `NotifyScreenEntered` static the loader calls;
non-nav screens entered via `ScreenManager.ShowScreen` (same `ScreenChanged` a button fires);
Rule 21 lint ran render-health only (no node spec.json — the per-element A/B was done against
`reference/` renders instead); §3.6 discharged (no raw-touch aim-camera drag; no 1v1 turn
clock exists).

## Verdict

**ARCHITECT_REVIEW_PASS.** Every static fact (prefab sprites, LocalizedText keys, CSV, real-
entry wiring, standing bans) re-derived by me; Rule 19 + Rule 21 re-run by me; the entire
behavioural acceptance suite regenerated from scratch by my own real-flow bot run and matched
line-for-line; the canonical re-shot fresh (new md5) and visually correct against the node.
No blocker found across three break attempts. Editor left clean on ShellScene, edit mode.
