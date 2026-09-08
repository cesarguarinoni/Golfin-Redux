DONE

# STATUS — `game_polish_b`

**Current:** `DONE` (2026-09-08) — approved by Cesar in chat. Notion 2111, slice b of three.

**Every design section §D0–§D7 is implemented, compile-verified and gated by tests or the
probe.** Evidence is substantially in: the §D2 parity gate (6 traces, fail 0), the §D1 modal
gate (14 modals, fail 0), five captioned A4 clips, and the §D3 count-DOWN proven frame by frame.

**A3, A6, A7 and A8 are now measured too** — and A3 found a real defect: three early returns in
TournamentLeaderboard that ended a paint without clearing the shimmer. That was the second
defect of one shape, so every `Shimmer` call site was audited and two more were fixed.

**§D3 is complete**: the modal-local numbers landed (level `Pop`, stat-bar `Tween`, readout
`CountUp` on both level-up panels). `MissionCard` gets none, and that is a decision with a
stated reason — its numbers are strings bound once, a clock, and a +1.

**A11 is measured, not waived**, and it found a real defect: the six §D4 shimmer hosts kinked
their 9-slice corners because one prefab is stretched to six shapes. The corner is sized to each
image's own box now — six hosts 0 FAIL / 0 WARN, the eight modal prefabs unchanged at FAIL 0 /
WARN 79 before and after the flag, and the seven GPS sites sharing the prefab linted and left
alone.

**All seven A4 clips exist.** (b) and (e) were recorded last, and both of their original bail
reasons turned out to be wrong — in each case the harness had reported a fact about the game
when the truth was a fact about the harness. Reading the (e) frames also found a §D1.4 wart:
the reward count-up ran after the card bind, so it showed its answer before counting to it.

One gap remains, stated rather than closed: cold frames for five of the six shimmer sites need a
backend provider with an empty first response, which was not obtainable in this session.

| Date | State | Note |
|---|---|---|
| 2026-09-05 | `SPEC_READY` | Written while the audit runs; 13 modals (SchemeConfirm added since the map). |
| 2026-09-08 | `IMPLEMENTER_WORKING` | D0/D1.1/D1.3/D2/D3/D5. Modal count measured at **15**, not 13. D2 parity gate closed. |
| 2026-09-08 | `IMPLEMENTER_WORKING` | D4 + the rest of D6. 6 shimmer hosts, 0 active at rest. Two sites moved on evidence. |
| 2026-09-08 | `IMPLEMENTER_WORKING` | D1.4 + D7. Result choreography, skippable. Probe: modals **14 / fail 0**. Found and fixed a pre-existing bug — a duplicate presenter was destroying the tournament result modal at boot. |
| 2026-09-08 | `IMPLEMENTER_WORKING` | **Evidence.** 5 captioned clips at 1170x2532; RP count-DOWN proven at 6.148→6.143→6.140→6.139. Three clips were discarded and re-taken because the caption did not match the frame. |
| 2026-09-08 | `IMPLEMENTER_WORKING` | **Modal-local numbers — §D3 complete.** Both level-up panels share `ModalNumbers`: level `Pop` only when the level changed, stat bars `Tween`, readouts `CountUp`. First paint snaps; every tween settles exact even when the next `[+]` interrupts it. `MissionCard` deliberately gets none. EditMode **2874 / 0 failed**. |
| 2026-09-08 | `IMPLEMENTER_WORKING` | **A5 per-site table.** 8 RP call sites mapped to the 2 armed methods; spend and earn COUNT (14 intermediate values each), the unarmed dev path snaps in the same run. Found a missing §D3 site — the ticket pill was still snapping — and wired it at the two player-caused paths. `RpArmingTests` makes the completeness claim gate-enforced. EditMode **2868 / 0 failed**. |
| 2026-09-08 | `IMPLEMENTER_WORKING` | **A3/A6/A7/A8.** Rest parity measured over 11 screens with every difference opened and attributed. A3 found a real defect (an arm that ends a wait must clear the shimmer); the shape was audited across all 5 sites and 2 more fixed. A6's cold cycle captured end to end for `missions.daily`. EditMode **2863 / 0 failed**. |
| 2026-09-08 | `IMPLEMENTER_WORKING` | **A11 lint.** 8 modal prefabs FAIL 0 / WARN 79, delta zero vs `9575baaa2^`. 6 shimmer hosts went 6 kink warnings → 0 FAIL / 0 WARN; the first fix (PPUM=1) made it 18 FAILs and was reverted. Scene diff is 30 `m_PixelsPerUnitMultiplier` overrides and nothing else. |
| 2026-09-08 | `DONE` | **A4 (b) and (e).** All seven clips recorded. Both bail reasons corrected: the shop opens on the GACHA tab (no BUY exists there), and gameplay loads ADDITIVELY so ShellScene is never unloaded. Real 75 RP purchase, 6,123 → 6,048. Hole 2 entered from its own card. §D1.4's count-up ordering fixed after the frames showed it counting to an answer it had already displayed. **Approved by Cesar.** |
