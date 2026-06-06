# Cesar Rejection — `practice_1v1_matchmaking_split`

**Rejected after:** `ARCHITECT_REVIEW_PASS` (red-team passed).
**Date:** 2026-06-06
**Verdict from Cesar (manual Unity check):** "All good EXCEPT when you Cancel during the matchmaking modal. The old HoleSelect card appears behind the new carousel if you do that."

---

## Defect 1 (BLOCKER) — Cancel on the matchmaking modal resurrects the dead `NextHolePanel`

### Repro
1. Home → Mode Select → 1v1 mode card → **PLAY** → matchmaking modal opens ("FINDING OPPONENT…").
2. Tap **CANCEL** on the modal before the opponent-found handoff.
3. **BUG:** the old HoleSelect / "Next Hole" card appears **behind the mode carousel**. The home screen is now in a corrupted visual state (stale legacy panel showing through).

### Root cause (verified by architect)
`Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs`:
- `OnShow()` (line 115) hides two home panels: `homeNoticePanel.SetActive(false)` and `homeNextHolePanel.SetActive(false)`.
- `OnHide()` (line 126) and `OnDisable()` (line 140) **unconditionally re-activate** them: `homeNextHolePanel.SetActive(true)`.
- `homeNextHolePanel` is wired in `ShellScene.unity` to `{fileID: 446239784}` = **`Canvas > ScreensRoot > HomeScreen > NextHolePanel`** — the SAME legacy panel that is `m_IsActive: 0` and was superseded by the mode carousel (this is the F-3 panel ruled out-of-scope earlier).

This restore logic was written for the OLD flow where matchmaking launched from the home screen with `NextHolePanel` visible. Now that 1v1 launches matchmaking from the Mode Select carousel — where `NextHolePanel` must stay deactivated — the modal's Cancel/disable path forces it back on, resurrecting a panel that should never show.

### Required fix
Make the modal restore the panels to their **prior active-state**, not force `SetActive(true)`:
- In `OnShow()`, BEFORE hiding, capture the current state, e.g.:
  ```csharp
  _noticeWasActive   = homeNoticePanel   != null && homeNoticePanel.activeSelf;
  _nextHoleWasActive = homeNextHolePanel != null && homeNextHolePanel.activeSelf;
  ```
- In `OnHide()` and `OnDisable()`, restore to the captured value instead of `true`:
  ```csharp
  if (homeNoticePanel != null)   homeNoticePanel.SetActive(_noticeWasActive);
  if (homeNextHolePanel != null) homeNextHolePanel.SetActive(_nextHoleWasActive);
  ```
This keeps the legacy home-launch behavior intact (panels were on → restored on) while leaving `NextHolePanel` OFF when matchmaking is cancelled from the carousel path (panel was off → stays off). Do NOT simply stop hiding the panels — the home-launch path still needs them hidden behind the backdrop.

### New acceptance gate (add to the report)
- **1v1 Cancel:** Mode Select → 1v1 PLAY → matchmaking modal → **CANCEL** → returns cleanly to the Mode Select carousel with **NO** `NextHolePanel` / HoleSelect card showing behind it. Capture a frame of the post-Cancel home/carousel state proving the panel is gone.
- Re-confirm the existing two gates still pass (Practice solo loop; 1v1 → gameplay).

---

## Defect 2 (PROCESS) — Record bot videos at FULL size, not 250×540

The bot videos were recorded at **250×540** (the downscaled cap). Cesar: this **breaks the bottom nav bar** in the recording and won't preview inline in chat. **Record at full iPhone 14 resolution (1170×2532)** next time. Update the `BotVideoRecorder` / recording config used by the new `PracticeFlowGate` / `Matchmaking1v1Gate` scenarios so the re-shoot for this fix is full-size.

---

## Out of scope (unchanged)
F-3 (the legacy `HomeScreenController.OnPlayClicked` button) remains out of scope — do NOT re-route or disable it. This rejection is specifically about the modal's Cancel/restore logic accidentally re-activating `NextHolePanel`, which is a different defect.
