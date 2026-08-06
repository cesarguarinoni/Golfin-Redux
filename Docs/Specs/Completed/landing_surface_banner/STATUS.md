DONE

# STATUS — landing_surface_banner

- 2026-08-06 — Architect wrote SPEC.md (scope locked with Cesar: 8 outcomes, solo + 1v1 human shots, golf-term wording). Reference PNG pulled from Figma node 4094:26052 into `reference/`. Awaiting Cesar go → Implementer.
- 2026-08-06 — Cesar GO. Implemented: CSV +8 `LANDING_*` rows, `LandingBannerController.cs`, asmdef `Golfin.Localization` ref, LabScaffold `[Session]` component + wired `_templateBanner`, `VersusMatchController.AwaitShot()` bounded sequencing wait.
- 2026-08-06 — Verified in real play (ShellScene → Practice → hole, real physics): ROUGH / BUNKER / FAIRWAY / GREEN / SEMI-ROUGH from real landings, WATER + OB from real Hole-6 shots, InCup silent with Hole Complete unchanged, JP ウォーター, 1v1 P2-suppressed and landing→OPPONENT'S TURN strictly sequential across 3 cycles. One deviation surfaced for Cesar: clone-only `NoWrap` so SEMI-ROUGH stays on one line. → READY_FOR_SELF_REVIEW.
- 2026-08-06 — Cesar reviewed the videos and approved ("all good"). He identified the versus clip's raw `LANDING_ROUGH` as a Unity reload/harness artefact, which matches the live probe taken in that session (`bootstrapsInScene = 0`; `GAMEPLAY_STRAIGHT` and `GAMEPLAY_SHOOT` also returned raw keys) — the direct-lab harness never runs `LocalizationBootstrap.Awake`, so every localized string degrades there, not just this task's. → DONE, moved to `Docs/Specs/Completed/`.
