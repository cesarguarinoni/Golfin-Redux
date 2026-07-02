# CESAR REJECTION — Stage 3 (post-ARCHITECT_REVIEW_PASS, 2026-07-02)

Cesar rejected after the Stage 3 red-team pass. Two fixes, both on `VersusResultScreen`. STATUS →
`CESAR_REJECTED`, route back to implementer. (Supersedes the prior Stage-0 rejection content.)

## 1. "DRAW" → "TIE"
Golf uses **TIE** more often than DRAW. Change the result-screen outcome label from "DRAW" to
**"TIE"** (`VersusResultScreenController.DrawLabel = "DRAW"` → `"TIE"`; update the neutral-state
comments to match). Keep the internal `GameSession.MatchOutcome.Draw` enum name as-is — only the
DISPLAYED label changes. Neutral color (#CCCCCC), both columns, greyed reward row — unchanged.
(Scope: the RESULTS modal only. The pre-existing WIN/LOSE/DRAW **banner** — `TurnBannerWidget`, a
separate system — is NOT in this task's surfaces; leave it unless Cesar asks for consistency.)

## 2. Reward icon + amount not centered (single-prize case)
The prize icon + amount is **not centered** in the reward row — visible whenever only ONE prize type
is shown, which is EVERY current state (win = 1 RP slot, lose/tie = 1 greyed RP slot). The single
active slot sits off-center instead of centered under the HOLE line.

Likely root cause (implementer to CONFIRM by measuring live, not guess): the reward rows
(`_rewardRow1/2/3`) are children of a `HorizontalLayoutGroup` (childAlignment=MiddleCenter) and
`BindRewardRows` `SetActive(false)`s the unused rows — so a single child SHOULD center UNLESS
`childForceExpandWidth` is on (the lone cell expands full-width and the icon+amount left-aligns inside
it), or the inner icon+amount group / the slot has its own left alignment, or the slots carry fixed
LayoutElement widths. Use the **golfin-ui-fidelity** measure→root-cause→validate loop:
`GetWorldCorners` on the active slot's icon+amount vs the reward-row container center; fix the real
cause (turn off childForceExpandWidth / center the inner content / center the single active slot). The
active slot's visual midpoint must equal the row-container horizontal center (±a few px). Must center
correctly for 1 slot AND still lay out correctly if 2–3 slots are ever active (future
repair/ball/gacha) — do not hardcode a 1-slot-only position.

## 3. Banner "DRAW" → "TIE" (DONE by orchestrator — Cesar-authorized Rule-7 exception)
Cesar asked to make the in-match banner consistent. Banner text lives at
`VersusMatchController.cs:437` under `Assets/Scripts/Physics/` (Rule-7 hard ban). Cesar explicitly
authorized a scoped one-line exception; orchestrator made the edit (`"DRAW"`→`"TIE"`), compile-verified,
and committed it separately (`5b72d37fc`, pushed). NOT in the implementer's iter-2 diff — reviewers
will not see it in the Stage-3 audit, and it must NOT be treated as a Rule-7 violation. Implementer:
do NOT touch `VersusMatchController.cs`.

## Process
- STATUS `CESAR_REJECTED`; fresh HEARTBEAT baseline block; `## Rejection follow-up` with GONE/RESOLVED
  per item + MEASURED centering (active-slot midpoint vs container center) + same-angle captures.
- Re-capture WIN (bright, centered RP) + TIE (neutral labels, greyed centered RP). Delta captures OK
  (real-flow waived per `CESAR_RULING.md`); sanctioned `CaptureHelper` only; NO `Assets/Scripts/Physics/`
  scaffolding; scoped scene/prefab diff (reward-row container + label only, no out-of-scope mutations).
- Verify compile after every C# edit. Iteration shape: `polish:tie-label-and-reward-centering`.
- Set STATUS `READY_FOR_SELF_REVIEW`; report both fixes + the measured centering in chat.
