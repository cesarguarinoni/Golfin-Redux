# Red-Team Review — `ball_trail_shot_isolation` — iter-5

**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Timestamp:** 2026-07-30 JST
**Verdict:** **ARCHITECT_REVIEW_FAIL** — one concrete blocker on §5.2 evidence provenance. The fix itself is correct and proven on real Hole 1; the designated §5.2 matched-pair artifact is not real Hole 1 and its capture narrative contradicts the frame.

Not a mesh/terrain task (Rule 16 N/A). Not a Figma-node task (Rules 9–12/18/19/21 N/A). No world→screen invariant JSON gate applies (Rule 3 N/A — this is a runtime-presentation fix, not a projected-geometry feature). No bespoke `*Gate` scenario (Scenarios.cs untouched — verified).

---

## Angles I captured / re-derived myself (not reused)

- **Video frame-scan** of `videos/trail_before_after.mp4` (1170×2532 h264, 46.07s — confirmed via ffprobe, not report): extracted frames at t=3/8/13/19 (BEFORE segment) and t=24/28/34/44 (AFTER segment). Files in scratchpad `vframe_*.jpg`.
  - **BEFORE t=8s** — real Hole 1, `PLAYER Lv 1 / PAR 5`, real fairway+trees+cart-path, `BALL: Aiming`, a broad gold ribbon bleeding straight down through the aiming view. This is the defect on real terrain.
  - **AFTER t=44s** — real Hole 1, `PAR 5`, willow-tree fairway, `BALL: Aiming`, ball on tee with only the translucent aim cone + thin aim line — no gold ribbon. This is the fix on real terrain.
  - Both segments read `PAR 5` and show real Hole 1 geometry → the video genuinely demonstrates before/after on real Hole 1.
- **Matched pair** `before_aim_matched.png` / `after_aim_matched.png` viewed at full res.
- **OB stills** `boundary_ob_red_ribbon.png` and `water_ob_red_ribbon.jpg` viewed at full res.
- **`before_turn07_aiming_ribbon_bleed.jpg`** viewed (real Hole 1, PAR 5, JAMES Lv 10, gold bleed in aiming).

## Metrics / facts I re-ran

- `HoleDatabase.csv` → `HOLE_LOMOND_1,1,5` → **real Hole 1 is PAR 5** (orchestrator's derivation CONFIRMED). `HOLE_LOMOND_6,6,3` → Hole 6 is PAR 3.
- md5 matched pair: `5c2d9b5a…` vs `7e04b33d…` — **distinct** (iter-1 byte-identical scar GONE).
- md5 all four OB stills: distinct.
- `git diff HEAD -- Assets/Scripts/Physics/` → exactly `BallTrailController.cs` + `PhysicsLabController.cs`. `ForceOBRecolorForCapture` NOT in diff (iter-3 banned-seam scar GONE). `Scenarios.cs` untouched.
- `git status --porcelain` outside task folder: only the 4 pre-existing baseline files. **No `.unity` mutation.**
- `test_results_iter5.txt` read directly: `943/938/2/3`; both failures `StaminaLiveWiringTests` (gacha_history schema v8-vs-9) — pre-existing, orthogonal.

## Prior-rejection defects — replayed

| Prior defect | Verdict |
|---|---|
| iter-1: two byte-identical stills given different names | **GONE** — matched pair md5s distinct; visual content differs (gold band present vs absent). |
| iter-3: OB stills contained no red whatsoever | **GONE** — boundary_ob (red-orange line down cart path) and water_ob (thick bright red over the lake) both clearly red on real Hole 6 (PAR 3 matches DB), genuine `BALL: OB / CAM: OBFreeze`. |
| iter-3: banned `ForceOBRecolorForCapture` seam substituted for a real OB shot | **GONE** — seam absent from diff and iter-5 evidence. |

---

## Acceptance re-run (independent)

- **§5.1 Stage-1 log / H3** — PASS. `trail_probe_log.txt` BEFORE holds posCount=91 emitting=False from t=1.81→119.78; `trail_probe_log_after.txt` drops posCount 123→0 atomically at t=4.12 (`→Aiming`). One trID throughout → H1/H2 eliminated, H3 confirmed.
- **§5.2 Matched aiming A/B** — **FAIL** (see blocker below).
- **§5.3 OB red both paths** — PASS. Red unambiguous on both, real Hole 6, no banned seam.
- **§5.4 Perfect-shot gold** — PASS. `gold_flight_t035s.jpg` gold ribbon, real Hole 1 flight.
- **§5.5 ZTest/renderQueue** — PASS. `EnsureTrail()` zero diff lines; both hunks inside `HandleStateChanged`.
- **§5.6 Tests** — PASS. 943/938/2/3, failures pre-existing.
- **§9 BoundaryOBHold code** — PASS. Coroutine waits BEFORE `RepositionBallWithLookDir` (ribbon parented to ball stays at OB spot), mirrors `WaterSplashCameraHold` shape, `BoundaryOBDwellSeconds=2.0f` (settled by Cesar — not flagged), scoped to the file, nothing else touched.
- **Scope / bans** — PASS. Two authorized Physics files; no scene mutation; no `*Gate`; `M_Splash*` untouched.

---

## THE BLOCKER — §5.2 matched pair is not real Hole 1, and the report's capture narrative contradicts the frame

The matched pair is a genuine single-variable A/B (gold band present in BEFORE, gone in AFTER — the fix mechanism is real). **But it was not captured on Hole 1.** Three independent, mutually-reinforcing tells, all re-derived from the frame:

1. **PAR 4** on the HUD. `HoleDatabase.csv` says real Hole 1 is **PAR 5** (confirmed above). Every artifact that IS real Hole 1 in this pack — the 46s video, `before_turn07_aiming_ribbon_bleed.jpg`, `gold_flight_t035s.jpg` — reads PAR 5. PAR 4 means the hole record was not bound.
2. **Featureless sky/haze void** — no fairway, grass, trees, or cart path. Real Hole 1 tee is NOT a void: video vframe_28 (AFTER, TURN 1, real Hole 1) shows full terrain.
3. **`0 yds` to flag at TURN 2.** On the same real Hole 1 the video reads 459/416 yds to flag at TURN 2. A 0-yd flag distance means the flag/hole geometry was not loaded.

The report's §5.2 narrative states the pair was captured after "Entered Hole 1 (LOMOND) via real ShellScene → `BeginGameplayLoad(1)`… fired the TURN 1 tee shot, waited for ball to reach AtRest **on the fairway**… TURN 2 Aiming." If the ball had actually come to rest on the Hole 1 fairway at TURN 2, the flag distance would read ~400 yds and terrain would be visible — as the video shows. It reads 0 yds over a void. **The narrative contradicts the pixel evidence** (Visual-Review checklist item 7 → FAIL; and this is precisely the "worse than bare" state the orchestrator surfaced — the hole data was not loaded, so labelling the pair "Hole 1" is inaccurate).

I am NOT treating this as fabrication/Rule-6 escalation: the HUD literally prints "HOLE 1 - REGULAR", so the implementer plausibly believed a Hole-1 load occurred and did not notice the load had degraded to a scaffold. This is misread evidence, not a manufactured tool result. It routes back to the implementer, not to Cesar.

### Why this fails the gate rather than passing on the "evidence-set" reading

Both prior gates passed §5.2 by reading the amendment as dropping any real-terrain requirement. That over-reads the amendment. §5.2's framing sentence is unchanged: *"Fire 5 consecutive shots **on Hole_01**… the next shot's aiming view shows zero residual ribbon."* The amendment relaxed flight-frame → aiming-frame; it did **not** relax "on Hole_01." And critically, **no single artifact satisfies §5.2 as written:**
- The **matched pair** provides matched-turn + matched-position + stash-discipline — but on a non-Hole-1 scaffold (void, PAR 4, 0-yd flag).
- The **video** is genuinely on real Hole 1 (PAR 5) before/after — but it is two separate 3-shot sequences, **not** a BEFORE/AFTER pair at a matched turn and ball position.

The union covers the intent, but the specific artifact §5.2 asks for — a matched BEFORE/AFTER pair on Hole 1 — does not exist in valid form. Given this task's history (Cesar rejected prior iterations on sight; the designated *canonical* screenshot is currently the PAR-4 void), surfacing that void as the headline artifact is exactly the risk this gate exists to stop.

### The passing artifact (concrete)

Re-capture the §5.2 matched BEFORE/AFTER pair on **real Hole 1**:
- HUD reads **PAR 5**, non-zero flag distance, visible fairway/trees (i.e. a real loaded hole — the video proves this is trivially reproducible; vframe_8 already is a real-Hole-1 aiming frame with the bleed, vframe_44 a real-Hole-1 aiming frame that's clean).
- Same turn (≥2), same ball position between the two frames.
- BEFORE with the H3 fix `git stash`-ed (gold ribbon bleeding into the aiming view); AFTER with the fix popped (aim guide only, no gold band).
- Redesignate the real-Hole-1 AFTER frame as the canonical screenshot. Do not present the current PAR-4 void pair as Hole 1.

No code change is required — the fix is correct (verified: clean minimal diff; video before/after on real Hole 1; posCount 91→0). This is an evidence re-shoot only. Do not re-engineer `BallTrailController.cs` or `PhysicsLabController.cs`.

## Break-attempts summary

1. **Visual** — harsh-angle re-shoot via video frame-scan across both segments: the fix holds on real Hole 1 (bleed in BEFORE aiming, clean in AFTER aiming, PAR 5). OB red present on both paths. No flipped/broken frames. → did not break the fix; DID expose the matched-pair provenance.
2. **Geometric/data** — re-derived PAR from `HoleDatabase.csv`, flag distance, terrain presence: matched pair fails all three as "real Hole 1." → blocker.
3. **Spec-intent** — §5.2 wants one matched pair on Hole_01; no single artifact provides it. Prior gates papered the gap with an over-broad amendment reading. → blocker confirmed.

**Non-blocking fix-forward (carry to the re-shoot):** save the four iter-5 OB `[OBCapture]` log lines to files in the task folder so the termination-reason/RGBA can be derived from disk (both prior gates requested this; pixel evidence already unambiguous).
