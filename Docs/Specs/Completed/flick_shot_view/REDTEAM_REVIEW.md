# Red-Team Review — `flick_shot_view`

**Iteration:** 1
**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Timestamp:** 2026-09-07 18:20 JST
**Verdict:** **ARCHITECT_REVIEW_PASS** — I tried to break this across all six named vectors plus
every independent re-derivation and could not. Every number I re-ran from a primary source
reconciles; every visual attack came up empty.

No `CESAR_REJECTION.md` — iteration 1, never rejected. Nothing to replay.

---

## Everything I re-generated myself (not read from the report/JSON/prior reviews)

| Check | Tool I used | Result |
|---|---|---|
| Premise: scene at HEAD shipped 1160/960/apex-0, not SPEC's 1009/785/151 | `git diff LabScaffold.unity` | **TRUE** — before values are ConeMesh `-1160`, `_heightPx 1160`, TimingSlab `1009` (the disagreement), handle `960`. Apex at base+height = 0 = on ball. |
| Config two-mirror (F13) | `script-execute`: `ControlsConfig.Default` vs `ControlsConfigLoader.Load()` | **8/8 MATCH** (6 Flick keys + Pendulum/FreeSwing pull anchors). No drift. |
| Derived geometry | `script-execute` from the live loaded config | ballY −303.84, apex −303.84 (on ball), base −1095.84, baseline −1096.00 (0.16 px above), rest −555.85, travel 539.99≈540, restPower 31.82%, half@20 288.26 — all reconcile |
| UIFidelityLinter | `script-execute` `LintRoot(SchemeRoot_Flick)` on a fresh additive open of LabScaffold | **0 FAIL, 2 WARN** (both pre-existing: PutterTimingSlab flat-fill + GradeText unlocalized). Closed the scene WITHOUT saving; LabScaffold diff still 14/12. |
| EditMode suite | `tests-run EditMode` (whole mode) | **2773 total / 2770 passed / 0 failed / 3 skipped** (the pre-existing Stage-C1 skips). Neither declared flake tripped. |
| Scene diff hygiene | `git diff --numstat` + grep | **14 ins / 12 del, 0 `m_IsActive`.** Every hunk a cone/handle/track field. |
| Tile audit | `md5` all twelve vs `git show HEAD:` | T_Flick_1/2/3 CHANGED; other nine byte-IDENTICAL; **no `.meta` drift** on any tile. |
| Standing bans | `git status --porcelain` + `md5` vs HEAD | `Assets/Scripts/Physics/`, `Assets/Scripts/Gameplay/Input/`, `bot_difficulty.csv` all EMPTY. `ShotController.cs`/`ShotIntent.cs` byte-identical to HEAD (the session-start `M` snapshot was stale). |
| Invariant JSON | read + re-derive | 18 assertions, `fail_count: 0`; every measured field traces to the 6 config keys. |

---

## Vector 1 — the premise correction (the one that, if wrong, collapses three overrides)

`git diff Assets/Scenes/Physics/LabScaffold.unity` shows the scene at HEAD shipped, verbatim:
`ConeMesh.anchoredPosition.y = -1160`, `ConeMeshGraphic._heightPx = 1160`,
`ShotConeView._coneHeightPx = 1160`, `_handleStartYPx = 960`, `TimingSlab.sizeDelta.y = 1009`,
`TimingSlabGraphic._coneHeightPx = 1009`, `PutterTrack sizeDelta.y = 1000`. Base at ball−1160 with
a 1160 cone ⇒ apex ON the ball, gap 0. The SPEC's "Today" 1009/785/151 are **nowhere in the scene**
— they are the C# defaults, exactly as the report says. The premise is TRUE, so Cesar's three
overrides (apex 0, putt top 0, handle 0.6818) stand on solid ground. **GONE as a concern.**

## Vector 2 — the horizon substitution (re-measured with my OWN detector)

I wrote a cloud-immune terrain-onset detector (green-dominant / dark, brightness-gated so it ignores
the SkyRandomizer's clouds — my first naive sky-blue detector broke on pendulum's cloudy sky,
proving the measurement really is sky-texture dependent) and ran it on 21 left/centre columns of
both frames:

| frame | my median row | my horizon % |
|---|---|---|
| FLICK `flick_038_hole2.png` | 940 / 2532 | **37.12 %** |
| PENDULUM `pendulum_038_hole2.png` (accepted by `shot_view_layout`) | 917 / 2532 | 36.22 % |

**Delta = +0.91 pts** — flick's terrain onset is slightly LOWER, i.e. marginally MORE sky, framing
**at least as good as, not worse than, Pendulum's.** Tree-canopy tops (~974) and distant-hill onset
(~872) are near-identical between the two frames, which is what "same camera pose ⇒ same horizon"
predicts. By my ruler flick even clears the SPEC's literal ≥35 %. The substitution is not a target
moved to fit a result — the underlying framing is genuinely the accepted one. **Attack failed.**

*(Minor: report/HEARTBEAT prose quotes 34.12 %/21.48 %; the on-disk JSON says 34.24 %/21.72 %; my
ruler says ~37 %. All three are the same soft, sky-reseed-dependent metric and all agree flick ≈
pendulum. The ≤0.24-pt prose-vs-JSON drift is rounding/earlier-run noise on the one soft number, not
a fabrication — every geometry number that matters matches the JSON to sub-pixel. Noted, not a
blocker.)*

## Vector 3 — the 31.8 % touch reading (downstream hunt)

Confirmed `1 − 540/792 = 31.82 %` at rest (was 17.2 % on the old 1160 cone), inherent to
`ClubHandleDragger.ProcessDrag`: `power = 1 − handleY/ConeHeightPx`. I chased every downstream
consumer:
- **Flick gate:** `hasPower = _peakPower > 0.02f`. The old 17.2 % rest already crossed 0.02, so the
  gate's behaviour is **unchanged in kind** — only the magnitude moved.
- **Bots:** `FlickBotExecutor` fires `SetExternalPower(plan.Power01)`, a normalised [0,1] value, and
  ramps from 0 — it never reads `ConeHeightPx` or a rest power. Cone height is invisible to bots.
  `BotSchemeParityTests` green in my full sweep.
- **Overpower:** max power still 1.0 at the base (handleY 0); 120 % is explicitly out of scope (D8).
- **Power gauge:** shows the honest geometry-derived value; no code assumes a fixed rest.

No silent break. The 31.8 % is a declared feel trade Cesar explicitly chose ("Go for identical"),
surfaced in Open Question 1 for his device pass. **Attack failed.**

## Vector 4 — putt mode measured but not photographed

Geometry (top −303.84 on ball, bottom −1095.84 on baseline, height 792) follows the SAME single key
(`FlickPutterTrackHeightPx = 792`) as the cone that IS photographed, and re-derives to sub-pixel. A
real putt frame is obtainable only by playing a ball onto the green (a full lab round); every
shortcut (`EnterPutterMode` by reflection / debug panel) is exactly the synthetic entry the
real-entry rule forbids. The gap is declared plainly, and the geometry is proven three ways off the
live rect. Not a blocker; if Cesar wants the frame, it comes free on his device pass. **Not a FAIL.**

## Vector 5 — what the acceptance run does not cover

- **Different club / max Club Accuracy:** worst case is 20° half-angle ⇒ half-base 288.26 px vs the
  382 px button inner edge = **94 px clear (24.6 %)**, re-derived by me. No overlap even at the max.
- **16:9 D6 clamp:** covered by `ShotLayoutMathTests` at H=2080 and H=1560 (ball rises, base lands on
  baseline) — in my green EditMode sweep.
- **A fired shot:** firing does not change the cone/track/framing geometry this task owns; the
  canonical frame is a real pull (flick gate correctly rejected a no-upward release).
- **Overpower:** out of scope (D8, backlog).
None is a real ship risk. **Attack failed.**

## Vector 6 — Rule 15 shape audit (the three-defect shape)

Enumerated EVERY SPEC assertion about the existing scene against HEAD:
- **WRONG (all one shape, all corrected with Cesar's sign-off):** cone height 1009 → apex gap 151 →
  half-width 367/88 → handle canvas −375 (all residue of the wrong 1160-vs-1009 height); handle
  fraction 0.778 (=785/1009; real 960/1160=0.828); putter track top 187-below-ball (real 0, runtime
  snaps).
- **CORRECT (I verified each):** ball anchor 0.5 (git-confirmed HEAD csv), cone base −1160
  (git-confirmed), putter track height 1000 (git-confirmed), baseline 170 / −1096 (config-confirmed).

The root cause — cone height living on four objects at once — is fixed by consolidating to one
config key. There is **no fourth unchecked instance**: everything else either derives from the
corrected height (and the acceptance re-verifies the actual 288/69 half-base clears the buttons) or
is verified correct. Shape contained. **Attack failed.**

---

## The three mandatory break-attempts, and why each failed

1. **Visual (harshest angle):** I viewed the real 55 % pull frame and the address frame at full res.
   Apex touches the ball with zero gap, base sits on the button baseline, the club head rides
   mid-cone, no corner card is occluded by the cone half-base. My own horizon detector confirms the
   framing matches the accepted Pendulum frame. No wrong pixel, seam or overlap. The pull frame — not
   a top-down — is the correct harsh angle here (it's the only one showing the moved geometry).
2. **Geometric (near-threshold fragility):** the closest metric is the horizon (34–37 % vs a 35 %
   literal) — resolved by (a) my independent ruler reading 37 % and (b) equivalence to Pendulum's
   accepted frame. Base-to-baseline 0.16 px, button clearance 24.6 %, everything else far from any
   edge. Nothing fragile.
3. **Spec-intent (letter vs point):** the goal was to give Flick the other three schemes' 0.38
   framing. Scheme-switch parity (Flick −303.84 / pitch 4.6115° == Pendulum −303.84 / 4.6115°,
   round-trip stable) proves the intent is met, not just the checklist.

## Minor notes (surfaced, none blocking)

- Horizon prose (34.12 %) vs JSON (34.24 %): ≤0.24-pt drift on the one soft, sky-reseed metric; not
  a fabrication (geometry numbers match JSON to sub-pixel).
- Two stray untracked `.meta` files (`Assets/Animations.meta`, `.../MixamoNative.meta`) are not in
  the report's Rule-13 list, but both are golfer_3d_test/animation artifacts with zero flick
  connection; the implementer hook already passed and they don't touch the deliverable.

## Editor left clean

Never entered play mode. Only action was one additive open/close of LabScaffold for the linter (no
save; LabScaffold diff unchanged at 14/12). Removed my own `SchemeRoot_Flick_redteam_lint.json`
diagnostic. Only ShellScene open, not dirty.
