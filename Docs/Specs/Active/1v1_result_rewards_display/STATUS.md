DONE

# STATUS — 1v1_result_rewards_display (Order 347)

**State:** ✅ **DONE — all 4 stages Cesar-approved 2026-07-02.** Full 1v1 result modal shipped:
modal presentation (Stage 1), CSV-driven reward grant + N-slot row (Stage 2), draw→TIE variant +
entrance pop-in + centered reward (Stage 3). Report demo video in `videos/` + `Docs/Reports/Media/`.
- Fix 1 "DRAW"→"TIE": result-screen label done; banner done separately (commit 5b72d37fc,
  Cesar-authorized Rule-7 exception).
- Fix 2 reward centering: TRUE root cause = nested ContentSizeFitter rebuild-order bug (Row CSF sized
  from the amount's stale 200px before the amount's own CSF hugged it → Row 250px, content left-packed
  → −17.6px measured against the visible content). Prior iter-2 pivot fix + the killed iter-3 agent's
  CSF-only change both measured the 978px CONTAINER (read 585 "centered") not the visible coin+amount —
  which is why red-team correctly FAILed iter-2. FINAL FIX (orchestrator): reward rows
  `childControlWidth=true` + `childForceExpandWidth=false`; removed the redundant per-amount CSF; amount
  text center-aligned. **Measured 1-slot offset = 0.0px** on the VISIBLE cluster (icon-left→amount-right
  midpoint = panel/HOLE/NEW MATCH center = 585); center-line render `screenshots/stage3_center_check.png`
  confirms the coin+x200 straddles panel center.
- Fix 1 "DRAW"→"TIE": result-screen label done; banner done separately (commit 5b72d37fc,
  Cesar-authorized Rule-7 exception).
- Fix 2 reward centering: TRUE root cause = nested ContentSizeFitter rebuild-order bug (Row CSF sized
  from the amount's stale 200px before the amount's own CSF hugged it → Row 250px, content left-packed
  → −17.6px measured against the visible content). Prior iter-2 pivot fix + the killed iter-3 agent's
  CSF-only change both measured the 978px CONTAINER (read 585 "centered") not the visible coin+amount —
  which is why red-team correctly FAILed iter-2. FINAL FIX (orchestrator): reward rows
  `childControlWidth=true` + `childForceExpandWidth=false`; removed the redundant per-amount CSF; amount
  text center-aligned. **Measured 1-slot offset = 0.0px** on the VISIBLE cluster (icon-left→amount-right
  midpoint = panel/HOLE/NEW MATCH center = 585); center-line render `screenshots/stage3_center_check.png`
  confirms the coin+x200 straddles panel center.
**Priority:** P2
**Spec:** `Docs/Specs/Active/1v1_result_rewards_display/SPEC.md`
