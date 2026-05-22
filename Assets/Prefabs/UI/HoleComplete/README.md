HoleCompleteWidget — production button wiring
=============================================

The lab widget has 6 button GameObjects but the production controller
(HoleCompleteModalController) wires only 3:

  WIRED (have ButtonPressFeedback attached):
    - Card1.ReplayButton  → OnReplay  (SUCCESS state)
    - Card1.RetryButton   → OnRetry   (FAILED state)
    - Card2.PlayButton    → OnPlayNext (SUCCESS state)

  DORMANT (no listener wired in production; ButtonPressFeedback NOT attached):
    - Card1.PlayButton
    - Card2.ReplayButton
    - Card2.RetryButton

  Inherited from the lab widget. If any of these are repurposed in
  future, attach ButtonPressFeedback then.
