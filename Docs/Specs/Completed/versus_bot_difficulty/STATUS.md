DONE

# STATUS — `versus_bot_difficulty` (1v1 Phase 2b)

**State:** DONE
**Notion:** Order 346 (P2)
**Spec:** `Docs/Specs/Active/versus_bot_difficulty/SPEC.md`
**Prepared:** 2026-06-11 (Architect; design Cesar-approved same day)

## History
- 2026-06-11 — SPEC written on the hardened 345 baseline (`4e700ae5`). Design locked: post-decision error injection (no safety re-check), club noise with power re-inversion (H2-safe by construction), putt club-noise suppression, 6-bracket band table for levels 1–240. Awaiting Cesar kickoff.
- 2026-06-11 — IMPLEMENTER_WORKING: iter-1 started.
- 2026-06-11 14:55 JST — SELF_REVIEW iter-1 PASS (all 8 §8 items CONFIRM-PASS, 0 overrides). D1 verified by direct code read (perturbed aimYaw/power01 flow straight to SetCameraYawRadians + drag ramp, no safety re-check). D3 H2-safe by construction (Mathf.Min(safeTargetDist, maxCarry)). Dispersion proof tables match band expectations (lv1 aim 15× wider than lv200). Diff confined to §6 scope; VersusMatchController untouched. → STATUS=READY_FOR_ARCHITECT_REVIEW.
- 2026-06-11 08:08 CEST — REDTEAM iter-1 PASS → ARCHITECT_REVIEW_PASS. Adversarial gate: attacked D1 through the FULL downstream commit path (SetCameraYawRadians is a pure assignment; SetExternalPower only Clamp01; CommitFlick uses CameraHeadingRadians+0+0 — no retarget/safe-clamp anywhere). Constructed D2 wedge-layup overshoot (+40–61m at lv1) and confirmed it's the spec's deliberate intent, not a bug; swept club-noise-only overshoot = 0.0m across 10–360m (monotone carry table). Independently re-derived dispersion: 14.7× aim / 12.2× pow, per-bracket stdevs match uniform distribution to 3 sig figs (authentic Random.Range). Extracted own frames (t=5/20/40s) from both videos — genuinely two different Hole_04 matches (Lv1 IRON 180 vs Lv200 WOOD 230, distinct play, correct badges). Putter suppression both directions (PutterIndex=3, clamp[0,2]+!isPutt), -1 int sentinel, zero-error fallback returns usable bracket, CSV+meta GUID real, VersusMatchController byte-untouched. No blocker found after genuine attempts. → STATUS=ARCHITECT_REVIEW_PASS.
- 2026-06-11 08:00 CEST — ARCHITECT_REVIEW iter-1 PASS. All 8 §8 items independently CONFIRM-PASS. D1 independently code-verified: only `Debug.Log` between injection-end `:691` and commit `:696/700` (no `TrySafeLanding`/`SafeYaw`/retarget). D3 `Mathf.Min(safeTargetDist, maxCarry)` cannot overshoot. D5 int `-1` sentinel (not bool — domain-reload-safe). Dispersion spread ~14.6× aim / ~12.2× pow independently recomputed. Two 1170×2532 videos on Hole_04 (≥52MB each). `VersusMatchController` byte-untouched (empty `git diff`). Risk notes routed forward (InvertClubPower design choice; wording slip on EnsureDifficultyLoaded; bracket cache re-resolution on inspector toggle). → STATUS=READY_FOR_REDTEAM.
- 2026-06-11 — DONE: Cesar approved in chat. Folder moved to Docs/Specs/Completed/. Implementation committed `5bee024c`. Canonical videos (lv1_sloppy, lv200_hardened) copied to Docs/Reports/Media/ for the daily report.
