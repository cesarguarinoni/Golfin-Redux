# Architect Review — `shot_ui_translucency_glow`

> Filed post-hoc 2026-08-07 by the Architect (chat session). The task was live-directed by
> Cesar through iter-3/iter-4 and closed before this file was filled; this review completes
> the record against the final code state (`TeeIdleGlowController.cs`, `BallConeAlphaMirror.cs`,
> dragger/button diffs, IMPLEMENTER_REPORT iter-4 + video).

## Verdict

`PASS`

## Architectural / cross-cutting checks

| Check | Result | Notes |
|---|---|---|
| Asmdef boundaries | PASS | New code in `Golfin.Gameplay.UI`; reads `Golfin.Gameplay.Input` + `Golfin.Gameplay.Session` only. Compiles; no backdoor refs. |
| Single-writer rule (cone alpha) | PASS | `ConeAlphaController` diff = 0. `BallConeAlphaMirror` is read-only on the group, writes only the ball `Image.color`. |
| Reuse over duplication | PASS | `OtherButtonsFader.AnyOverlayOpen`, `MapViewController.IsOpen`, `GameSession` reused as specced. Radial sprite generated (static, one per domain) — acceptable: pure gradient, club-agnostic, `HideAndDontSave`. |
| Intent vs letter | PASS | Spec's literal "clone handle sprite + tint" produced the hard gold echo (iter-3); Cesar redirected to the soft centred halo (iter-4). Final look is Cesar-approved intent; spec recipe superseded — correct escalation path was followed. |
| Cross-feature safety | PASS | Bot/versus path has no pointer events → `OnHandleTouched` unreachable; `NotifyOtherInteraction` null-guards `s_instance`; `OnDestroy` cleans up the sibling `HandleGlow` GO (it would NOT be auto-destroyed with ClubHandle — correctly handled). `OnDisable` clears `s_instance` and unsubscribes. Physics/ diff = 0. |
| Latent-bug scan | PASS | Iter-3 caught the real one (glow scale vs ClubHandle localScale 2.0 — animating into an occluded rect while logs read "PASS"; classic Lesson-O case, resolved with pixel evidence). Remaining nits are cosmetic only, listed below. |

## Accepted deviations / nits (no action required)

1. **Button reset fires on `onClick` (pointer-up), not pointer-down** as the spec asked. Practical difference is nil (modal-opening buttons are covered by the modal branch; a held-but-not-released button not resetting the timer is imperceptible). Flagged in the report — accepted.
2. Redundant `if (!armed) _idleTimer = 0f; else _idleTimer = 0f;` branch in `Update()` — cosmetic; both arms intentional per the comments (disarm vs modal-pause). Fine to leave.
3. `controls.csv` mirroring skipped — spec explicitly allowed Inspector-only for v1.
4. Putter-on-tee glows (gate is first-stroke, not club-type) — matches spec reading; noted in checklist.
5. Acceptance items 7/8 (grab-mid-glow, stroke-2-no-glow) verified at code/branch level + recorder path, not in the clip. Items 4/5/6 ARE covered end-to-end by `videos/raw_tee_idle_glow.mp4`. Acceptable for a UI hint feature; on-device smoke will exercise them naturally.

## Figma fidelity

N/A — behavior spec; SPEC §Reference states no Figma node. (Rule 18 not triggered.)

## Lessons captured

- Logs can report a perfect animation into an invisible rect: any runtime-generated visual that overlays a scaled UI element must multiply the target's `localScale`, and needs PIXEL evidence (zoomed still or clip), not state logs — reconfirms Lesson O, extends it to generated overlays.
- Unity UI: a child can never render behind its parent's Image — behind-effects must be lower-index siblings, and then need explicit `OnDestroy` cleanup + per-frame rect sync.
- Domain reload during play mode invalidates capture sessions (raw loc keys, default character, 0-yd club = the fingerprint). Compile first, then play, never touch C# mid-session.

## Cesar's final approval

- [x] Approved by Cesar — live-directed iter-3→4 in Unity (soft halo, #FFC94A retained after #98855B trial), confirmed "task done" in chat 2026-08-07. Task resides in `Docs/Specs/Completed/`.
