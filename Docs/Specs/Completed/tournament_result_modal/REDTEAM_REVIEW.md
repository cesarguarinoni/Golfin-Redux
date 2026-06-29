# Red-Team Review — tournament_result_modal (iter-2)

**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Date:** 2026-06-29 13:05 CEST
**HEAD SHA:** `c341d7503a114fd1006b48be42a804fa111dd1a7`
**Verdict:** `ARCHITECT_REVIEW_PASS`

I tried to break this across visual, geometric, and spec-intent axes and could not
find a Cesar-visible defect. Every claim below was re-derived from source/prefab
YAML on disk, the Unity Editor.log, and my own pixel crops — not carried forward
from the reviewer.

---

## Angle I captured myself
- Re-cropped the canonical `iter2_v2_fixed_panel.png` (1170×2532) at full res into:
  modal body (y 980–1600), top edge (y 955–1085), bottom edge, CLAIM pill (upscaled),
  RANK band (upscaled). Files in scratchpad; findings inline below.
- Side-by-side A/B of the built CLAIM pill and RANK band vs `reference/Prize_modal_13498-2067.png`.

## Metrics I re-ran (my numbers)
- Panel `m_SizeDelta: {x: 978, y: 605}` — read from prefab line 2052. Renders 968px wide on screen (panel border left x=96, right x=1073) = 99% of Figma 978. PASS.
- CLAIM pill bbox (my pixel scan): x[430,739] y[1453,1619]. Panel inner extends to ~1657 (center-column navy/border transition). CLAIM contained with ~120px top pad, ~38px bottom pad. **inside=True, independently confirmed.** iter-1 escape bug gone.
- RANK: prefab `m_fontSize:48`, `m_fontStyle:0` (Normal), `m_fontWeight:400` — Cesar non-bold override is REAL on the live prefab.
- CLAIM: prefab `m_fontSize:50.8`, `m_fontStyle:1`. Sponsor 20, Reward 28 (#73E080), all match the non-uniform Signup-derived divisor.
- 5 sprite GUIDs read from prefab → all resolve to real on-disk assets (HoleCard bg `064cba0b…`, Divider `9e62d8f4…`×2, RP coin `aab2dfa3…`, gold Button-Retry `aee5ccf2…`). No `<NONE>`+flat fill. Clone source GUID `8041c091…` matches.

## Four flagged stress points
1. **Tie-break determinism** — GONE/NON-ISSUE. `TryFindPresentable` strict `<` on EndUtc; `GetTournaments()` returns the immutable CSV-ordered `_definitions` unchanged each call → ties resolve to the first CSV entry, deterministic and reproducible.
2. **Re-entrant OnScreenChanged during 1.0s wait** — NON-ISSUE. `_presenting` set late, so a 2nd coroutine CAN be scheduled, but the 2nd `Open→Show()` is a no-op via `ModalController.Show()`'s `if(_isVisible) return;`. No double-modal, no double count. `_presenting` reset on every 1→0 (claim/Hide/OnDisable) → cannot stick. Latent inefficiency, not a defect.
3. **Indirect OpenModalCount flips** — NON-ISSUE. MAINTENANCE NOTICE = plain TMP (no ModalController). 9 ModalController subclasses total; the 6 needing `base.OnDisable()` got it (bodies preserved, `_isVisible` guard prevents double-decrement). The 2 un-updated: BagSelection (no OnDisable, inherits base guard — fine); HoleComplete (shadowing `private void OnDisable`, but overrides Show/Hide to never touch OpenModalCount → bypassed guard is harmless). Nothing can suppress or co-present.
4. **CLAIM glyph height** — PASS. 50.8 (÷1.299 from 66px Figma); A/B crop shows the glyph fills the gold pill in the same proportion as the Figma render.

## Non-negotiables (re-verified independently)
- **Rule 2 real-entry:** Editor.log lines 52712–52986 show `gotemba results: Rank#1 Prize=20000 → Navigated to Home → [Modal] TournamentResultModal shown` with a real stack trace through `PresentAfterDelay→Open→Show` (PresentAfterDelay.cs:167). CLAIM via real `OnClaim` (cs:160) → `Claimed=True`. Force-disable leak guard returned count to 0 on exit. Log corroborates the report — not fabricated.
- **Rule 11/19 clone provenance:** real sprites on disk (above). Genuine.
- **Rule 18 Figma fidelity:** table real + per-element; my re-pull/visual diff agrees.
- **OpenModalCount balance / no regression:** S1/S2 diffs clean+additive; subclass chains safe; no double-decrement path.
- **Cesar overrides:** RANK non-bold real on prefab + renders lighter (intentional, not failed). CLAIM inside panel confirmed by my own pixels.

## Three break-attempts, why each failed
- **Visual:** top-edge "overlap" I suspected was Home content ABOVE the modal, not bleed — modal's white border is crisp, all content contained. Failed to break.
- **Geometric:** panel 99% width, CLAIM padding 120/38px — comfortable, not marginal. Failed to break.
- **Spec-intent:** state machine cannot double-present (Show guard) or stick (_presenting always resets). Failed to break.

## Minor non-blocking note for Cesar (NOT a route-back)
CLAIM text color on the prefab is `#1A2535` (dark navy), inherited from the cloned Signup
CONFIRM button, vs SPEC §4.3 token `#321506` (Figma warm brown). This matches the shipped
clone source and the O-2 "match Signup" precedent, but was not disclosed in the report's
Spec-deviations section. Cosmetic, dark-on-gold either way; flagging for awareness only.
