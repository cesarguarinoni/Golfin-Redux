# Architect review — iter-7 (§3.9 landmarks + wrap) — 2026-09-11

**Re:** `IMPLEMENTER_REPORT.md` iter-7, `7e5f546ce`, `evidence/grip39/`. **Verdict: the placement half is right and stays; the fingers are open because of one sign, not a cap. SPEC §3.10 added; one more run.**

## What iter-7 got right, and I am keeping
- Overlap 0.0064, heel pad 0.67, trail palm 0.66, no interpenetration 0.0112 — the *shape* of a grip, for the first time. Kept.
- Determinism: three runs at R = 0.0057 / 0.0054 / 0.0057. The foot-slide row is a measurement again. Kept; `captureDeltaTime` stays for every measured run from now on.
- Both deviations are correct calls: rule targets must be sampled rig-on (they are world points); the rig-off station search cannot converge because nothing moves. The rig-on two-point fit is the method.
- `shaftRadius` / `fingerRadius` scaled — a 37 % error I should have caught when I wrote §3.7.

## What the frames say that the report does not
`address_targetside.png`: the trail fingers are **straight, hyperextended and fanned** — not "as closed as an 80° cap allows". A joint flexed 80° curls. These were rotated the other way. `GolferPresenter.PalmNormal` is `cross(along, across)`, mirror-antisymmetric between hands; `WrapJoint` then chooses its sign by "which rotation reduces distance to the shaft", which is extension whenever the shaft is in or behind the finger plane. And §3.9.1 put the trail shaft `n·(t+r)` on the *back* side of the MCP row using that same normal. The lead hand, whose normal happens to point the right way, closed better in the same frame. `grip.axis.landmarks_l = 0.0221` is a separate, smaller thing: the index PIP is a finger joint and moves with the wrap; the landmark has to be wrap-invariant.

## Decisions (SPEC §3.10)
1. One handedness-fixed palm normal `n_out`, verified by a 5° flex test and printed. All rules use it.
2. The wrap **only flexes**; the direction heuristic and the fist branch go. Caps 90/90/70. Per-joint play-mode log for all 21 joints.
3. Lead landmark B made wrap-invariant (`IndexProximal + u·0.6·L_prox`).
4. Lead station from the butt cap (`LeftHand` projection 10 mm·s below `ClubStart`), then the §3.4 solve re-runs with it fixed.
5. Thumb clock sign from the trail palm centre; trail-palm-on-thumb asserted geometrically.
6. Blade orientation still §3.9.7, untouched.

Not accepted: none. Iter-7 did what §3.9 said; §3.9 inherited a sign from code written for a different rig, and I did not read that code before citing it. Sorry.
