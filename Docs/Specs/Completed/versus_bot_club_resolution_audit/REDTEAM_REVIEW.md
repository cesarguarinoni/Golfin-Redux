# Red-Team Review — `versus_bot_club_resolution_audit`

**Iteration:** 1
**Date:** 2026-07-20 15:05 JST
**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Verdict:** **ARCHITECT_REVIEW_PASS**

I actively tried to break this on all six attack axes the dispatch named and could not. Evidence below is my own — re-read source, re-extracted frames across the whole clip, re-derived the production resolution path from code.

---

## Angle I captured myself (not re-used)

Extracted 8 independent frames from `videos/762_wedge_proof.mp4` at t=1/8/15/25/35/45/52/58s (full-res 1170×2532, `ffprobe`-confirmed: 1170×2532, 59.9s, 1774 frames). Read the canonical + t=1/8/25/45/58s. Every frame is upright, full-res, HUD intact (all nav/action buttons render with icons), no y-flip, no fall-through, no feature-covering caption.

Decisive frame the flatter angle would hide: **t=58s** shows the club chip switched from `P. WEDGE` to `PUTTER` (1 mts to pin, both players TURN 2). The chip is not a static label — it tracks the live-resolved club, which is exactly what a working ClubContext sync produces and a fake/stale-driver fix could not.

---

## Six break-attempts and why each failed

**1. Is the fired club really a wedge on the LIVE path, or is the HUD lying? — FAILED to break.**
`LiveStatProviderHost.ResolveLive()` builds `swingBundle` from `string clubId = ClubContext.SelectedClubId;` (confirmed at the swing branch, `Assets/Scripts/LiveStatProviderHost.cs`). `BotClubSync` sets `SelectedClubId`, `SelectedTypeLabel`, `SelectedDistance`, `SelectedPortrait` from the **same** `ClubEntry` atomically (BotClubSync.cs:88–92) — so the HUD chip and the physics club cannot decouple. Video corroboration: chip dynamically switches P.WEDGE→PUTTER (t=8s vs t=58s). Log corroboration: `clubVel=42.00m/s` (wedge-class; driver≈75) with `bundle.Club.HasValue=True`, immediately preceded by `[VersusBot] BotClubSync → 'club_pwedge_royal'` (LabClubIndex 2 = wedge, not iron7). HUD is not lying.

**2. Real-entry-path integrity (map_view scar). — FAILED to break.**
`VersusMatchController.AwaitShot(active)` calls the **same production** `StartCoroutine(_bot.TakeShot())` for any bot turn (line 261–266). `_debugBothBots` only swaps P1's human input for a bot (`active==0 && !_debugBothBots` gates the human branch); P2 is always a bot in real 1v1. `_debugBothBots`/`_debugStartLie` are `[System.NonSerialized]` → default false/zero → inert in production (tee reads `BallPosition` when `_debugStartLie==Vector3.zero`). No `*Gate` added to `Scenarios.cs` (`git diff` empty). Capture uses the established `VersusHudCaptureMenu` menu-item pattern (peers: versus_launch, versus_resolution_clip, bot_hardening_water, audio_match_stinger). The audited code path is production-identical; only the invocation and start-lie are scripted. Not a synthetic entry point.

**3. Production independence from the LabInventoryStub. — FAILED to break (this was my hardest attack).**
Proved the production 1v1 resolves a wedge **without** the stub change: `ClubContextPopulator.Refresh()` (`Assets/Scripts/UI/HUD/ClubContextPopulator.cs:38–60`) reads `BagManager.Instance.GetClubsInBag(slot)` and sets `ClubContext.EquippedBag` with `LabClubIndex = MapClubTypeToLabIndex(t.type)` (P/A/S_Wedge → 2). Post-Order-761 (commit `abb6df4f9`, verified) the default equipped bag carries a wedge, so in production `EquippedBag` has a LabClubIndex==2 entry, and `BotClubSync.SyncToClubContext(2)` exact-resolves it → wedge fires. The stub gate fires only when `BagManager` AND `BallManager` are both null (lab-capture only), so it cannot mask or alter the production path. Fix is real in production; stub change only aligns the manager-absent lab harness. Not a masked defect.

**4. Regression surface. — FAILED to break.**
`git diff` on VersusBot is +14/−1, cleanly bounded **between** `_controller.SetClub(club)` and `_shotController.ClearStatBundleOverride()`. The 2b error-injection (D2 aim/power, D3 club noise), H2 layup, and H3 slope overrides all run **before** this window and are byte-identical. The re-`SetClub(resolvedLab)` fires only on bag divergence and calls only `PhysicsLabController.SetClub` (lab index + putter/cone UI) — it does **not** re-apply difficulty (difficulty already applied to aimYaw/power01 upstream). BotDriver refactor is behaviour-identical: the moved block preserves the same exact-lookup + nearest-available (largest ≤ desired else smallest > desired) fallback, same field pushes incl. Portrait/Distance, same divergence→SetClub, same empty-bag WARN. LogStep now reads back the just-set ClubContext fields (same values).

**5. Clean match / whole-clip scan. — FAILED to break.**
Frames t=1/8/25/45/58s: continuous progression CAMILA/TARO through TURN 2, wedge played on genuine 20–80m-band approaches (t=3s flag 37 yds ≈ 34m; a 61.3yd shot at t=45s), putter correctly on the green (t=58s, 1 mts). No stuck-recovery, no fall-through, no upside-down/broken frame. Full 1170×2532 throughout.

**6. Test integrity. — FAILED to break.**
`git diff --stat` touches only BotClubSync, VersusBot, BotDriver, LabInventoryStub, VersusHudCaptureMenu — none stamina/schema/audio. The 3 reported failures (`StaminaLiveWiringTests` T6 v8→v9 ×2, `AudioEmitterTests.MinInterval`) cannot stem from this diff; the v8→v9 stamina fallout traces to Order 761 `abb6df4f9` (verified it bumped schema v8→v9). No EditMode test references `LabInventoryStub`/`s_TestClubIds`, so the +1 stub club (now 5 clubs, putter shifted to bagIdx 4 / LabClubIndex 3) breaks no assertion, and the putter path still resolves (LiveStatProviderHost putter branch keys on LabClubIndex==3, still satisfied).

---

## Prior rejections

No `CESAR_REJECTION.md` in this task folder — first pass through the pipeline. Nothing to replay.

---

## Minor issues (cosmetic / documentation — NOT blockers)

- **Caption ghosting:** the `drawtext` captions render doubled/offset at the extreme bottom edge (t=1s shows the club-select caption overlapping the task-title caption; t=8s shows the clubVel caption ghosted). Cosmetic, at the very bottom, does not cover the HUD feature. Note the on-screen caption text is implementer-authored and is NOT the evidence I relied on — the code path + dynamic chip switch + velocity log are.
- **Stale comment:** `VersusHudCaptureMenu.cs` comments name `club_sandwedge_gf`, but the actual fired club is `club_pwedge_royal`. Comment-only inaccuracy; behaviour fires a wedge either way.
- **Lomond-hole doc gate:** SPEC Hard Gate 1 says "on a Lomond hole"; capture used Hole_04. Did not independently confirm Hole_04's course. The fix is hole-agnostic, so this is a documentation nicety, not a correctness defect.

None rise to a FAIL. Cesar may want the caption ghosting cleaned on a future clip, but it does not obscure the proof.

---

## Verdict

The MEASURE-FIRST audit found a real divergence (VersusBot pushed no ClubContext, LIVE provider read the stale driver), the Stage-2 fix mirrors BotDriver's proven pattern via a shared `BotClubSync` helper in the correct asmdef (Lesson W), is surgically bounded, production-safe, leaves difficulty/H2/H3 and the static sentinels untouched, and — critically — its production correctness is independently established from code (ClubContextPopulator + BagManager + post-761 wedge + exact-resolve), not merely from the lab capture. The video proves the bot plays a wedge on real approaches and a putter on the green with a live-tracking HUD chip. I tried six ways to break it and each failed.

**Advancing to `ARCHITECT_REVIEW_PASS`.**
