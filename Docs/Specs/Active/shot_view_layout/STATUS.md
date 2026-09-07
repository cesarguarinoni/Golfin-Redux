READY_FOR_ARCHITECT_REVIEW

Implemented 2026-09-07 by Claude Code (direct, no subagent chain — Cesar dispatched it in-thread).

The ball anchor question is RESOLVED: Cesar picked option 4 on 2026-09-07, so the baseline clamp
now guards the 120% HANDLE (where the finger is, per SPEC D3) rather than the lane's rounded end
(D6's formula). Ball sits at viewport 0.3800 exactly, the 120% handle 244 px above the screen edge
— D3's own figure — and the horizon measures 37.8% from the top against a Figma target of ~38%.

One item remains open, and it is not a blocker for the layout itself:

- Confirm tiles (§3.7) were re-captured and then REVERTED. The new crops clip the timing marker bar
  and the 100%/120% labels — the auto-crop is width-driven and clamped at MaxCropW 900 (a guard that
  keeps the HUD columns out of the tile), and an 888 px lane no longer fits inside it. Shipping
  those would explain the control worse than the tiles on disk. Evidence and the three ways out are
  in IMPLEMENTER_REPORT.md.
