# Wrist angles at address — what real golfers measure, against ours (2026-09-15)

Looked up after Cesar's stage-2 verdict ("the wrists seem to bend too much compared to real golfers").
Our numbers are the forearm→hand angle from `HandHingeStage2` (`grip.wrist.angle_l/_r`): total, flexion
(+ = extended / cupped), deviation (ulnar).

## Published / measured references

| Quantity | Value | Source |
|---|---|---|
| Lead wrist **ulnar deviation at address** | "usually held in ulnar deviation of the order of **17°**"; DeChambeau quoted at **20°** at address, 14° radial at the top, 15° ulnar at impact | IntechOpen chapter *Leading Wrist Injuries in a Golfing Population* (2021), https://www.intechopen.com/chapters/75940 |
| Lead wrist **flexion/extension at address** | "neutral or slightly extended" (same chapter); **15–20° of extension** typical, target zone **0–20°** (HackMotion, >1 M swings) | https://hackmotion.com/wrist-position-at-address/ |
| Trail wrist at address | "a small amount of extension, a slight cup" (no number given); extension grows to 40–50° at the top; pros are 10–20° *more* extended at impact than at address | https://hackmotion.com/trail-wrist-in-golf/ , https://hackmotion.com/wrist-position-at-impact-in-golf/ |
| Skill effect | high-handicap golfers show 5.7° more peak lead-wrist radial deviation and 7.1° more at impact than low-handicap (n = 28) | Fedorcik et al. 2012, J Sci Med Sport, https://pubmed.ncbi.nlm.nih.gov/22154489/ |
| Instruction, arm–shaft | with a driver the lead arm and shaft form close to a straight line, hands high; the angle grows for wedges (hands lower) — no degrees given | https://usgolftv.com/instruction/the-perfect-golf-shaft-angle-at-address/ , https://golf-info-guide.com/golf-tips/setting-up-your-shot/top-4-tips-on-irons-and-hybrids-shaft-angle/ |
| Not usable | Sweeney et al. 2012 (Konstanz ISBS) reports downswing ranges and velocities only; the 2023 lead-vs-trail EMG paper reports angular velocities, no address angles | https://ojs.ub.uni-konstanz.de/cpa/article/view/5195/4770 |

Real-golfer envelope at address, from the above: lead wrist ≈ **17–20° ulnar deviation + 0–20° extension**
(total forearm→hand angle ≈ **20–30°**); trail wrist a slight cup, deviation not published (physically similar
to or a little less than the lead).

## Ours

| Configuration | lead total (ext, dev) | trail total (ext, dev) |
|---|---|---|
| real golfers (above) | ≈ 20–30° (0–20, 17–20) | slight cup, ≈ 20–30° |
| **the mocap clip itself** (rig off) | 46.2° (28.3, −33.0) | 41.8° (−8.0, 40.7) |
| min-wrist bake (Cesar saw) | 61.7° (27.7, −48.4) | 59.9° (22.8, 50.7) |
| **wrist-angle bake (committed)** | 55.0° (23.8, −45.5) | 47.7° (24.8, 37.6) |

## Reading

- The excess is almost all **deviation**: 45° against 17–20°. Extension is only slightly high (24° vs 15–20°).
- The **clip already has 33° / 41° of deviation** — 1.7–2× the reference — before any anchor touches it. The
  actor's hands sit high on the shaft line for this character.
- Deviation is set by the angle between the hanging forearm and the shaft, i.e. by hand height over the ball
  and club length. The stage-2 solve confirmed the club pivot cannot buy it within the arms' reach; a
  **shorter club for the 1.33 m character** (the driver is at 0.87 scale) and/or a lower hand position with
  the arms extended is what moves this number, not the grip anchors.
- Target for a "real" look, to hand to whoever changes the club/stance: lead deviation ≤ 25°, extension
  10–20°; trail extension ≤ 20°.
