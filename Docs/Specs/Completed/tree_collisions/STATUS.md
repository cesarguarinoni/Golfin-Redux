DONE

# STATUS — `tree_collisions`

**State:** DONE — Cesar approved ("Done") 2026-06-12; folder moved to `Docs/Specs/Completed/`. (Pre-approval below.)
**Pre-approval state:** ARCHITECT_REVIEW_PASS (iter-8 + iter-8c, 2026-06-12 10:38 CEST) — golfin-redteam-reviewer adversarial gate PASS. Red-team drove the live ai-game-developer MCP itself: full EditMode suite 376/379, 0 failures (matches cited); TreeCollisionTests 9/9 PASS incl. tightened CanopyEntryImpulse + PROBE7. Own live script-execute probes: canopy fires EXACTLY ONE 0.401× cut at y=8.951 then free-fall (no slow-mo); PROBE7-A/B stuck-ball configs now land at finalY=0.0213m, samples 723/821 (were 14401+ stuck); roll/putt DEFLECTED, determinism bit-exact. Sim byte-frozen to `2fb4c2b7` (all sim files + CSVs + scene empty diff). Tightened test verified NON-VACUOUS by own trace (scan truncates at first y<0.2 @ i=355; canopy drop @ i=135 is the lone in-window drop). §9 trunk clip frame-walked at red-team's own timestamps: at-rest payoff (last ~3.5s) shows ball on ground at base of BARE trunk under NORMAL chase camera, zero Downrange code in new scenario. Scope clean; one cosmetic Files-table prose nit (two wiring files mislabeled "UNCHANGED" — non-blocking). Awaiting Cesar final approval.
**Previous state:** READY_FOR_REDTEAM — golfin-reviewer PASS (iter-8 test-tightening + iter-8c trunk clip).
**Previous state:** SELF_REVIEW_PASS — golfin-self-reviewer PASS with two MINOR-DISCREPANCY bookkeeping flags (Files-table prose says LoopV2SmokeBotMenu.cs/LoopV2SmokeBot.cs "UNCHANGED" but they carry +17/+4 lines of in-scope scenario wiring — not scope drift)
**Notion:** Order 348 (P2, Gameplay Polish) · Phase 2 (tree-aware bot) = Order 351
**Spec:** `Docs/Specs/Active/tree_collisions/SPEC.md`
**Prepared:** 2026-06-11 (Architect; design Cesar-approved same day)
