IMPLEMENTER_BLOCKED

Stopped by Cesar ("Done"), 2026-09-09. Two defects he reported from the live scene are OPEN and are
the right starting point for whoever picks this up:

  1. "Club disappears before the shot" — not investigated. Suspects, in order: GolferPresenter
     .HandlePutterMode SetActive on driver/putter socket roots; ApplyCulling (cullingMode goes to
     CullUpdateTransforms / CullCompletely when not swinging); or the club being deactivated on the
     shot-state transition. None confirmed — do not trust this list without measuring.
  2. "Still holding it wrong" — EXPECTED at the point he looked, see below.

WHAT IS DONE AND VERIFIED
 - ARCHITECT_DECISION_3_2 applied: avatarRoot gate logged (MixamoChar_TPose, matched), ClubRoot
   re-parented under it, identity local. The "not a child of the Animator hierarchy" exception is
   GONE from the play-mode log; RigBuilder.Build() succeeds. The Architect's diagnosis was right.
 - The clubs were MISSING from the prefab entirely (Cesar's catch: "there is no club visible").
   GOLFIN_Driver/GOLFIN_Putter restored from PfGolfer_Test under ClubSlot/PutterSlot, driver active,
   presenter socket refs re-pointed. Club is visible again — evidence/address_club_restored.jpg.
 - Anchors + ClubStart/ClubEnd re-parented under ClubSlot so they ride with the club (§3.2 "children
   of the club"); as siblings they would have stayed behind the moment §3.4 authored an offset.
 - §3.4 SOLVED but NOT APPLIED — this is why the grip still looks wrong:
       ClubSlot localPosition = (0.04678, 0.07685, -0.04668)
       ClubSlot localEuler    = (324.940, 279.642, 138.664)
   Derived by measurement at address, not by eye: shaft pointed from the ball up through the lead
   hand, club slid until GripAnchor_Lead lands on that hand. The geometry fits to 1 mm —
   lead-anchor-to-ClubEnd is 0.9100 m and lead-hand-to-ball measured 0.9090 m — so condition (b)
   falls out of (a). Roll pinned by the hand line so the face cannot end at the sky (golfer_3d_test
   F3's failure). THE WRITE NEVER LANDED: the script-execute that would have set it failed on an
   MCP disconnect, so the prefab still carries ClubSlot at identity. Applying those two lines is
   the next action, followed by one verification run and a LOOK at the frame.

NUMBERS (with a real club; every earlier value measured empty markers and is void)
   gripWorstL 0.3210 m · gripWorstR 0.3686 m · headAtBall 0.3161 m · gate 0.035 m
   foot slide L 0.0527 / R 0.0872 vs the §9.8 baseline 0.0528 / 0.0915 — still in band.

ALSO OPEN
 - grip.targetTracksHands FAILs: GripTarget lands exactly on handR, not the 50/50 midpoint, though
   the prefab YAML has m_Length 2 with both source weights 0.5. May be cross-frame IK feedback
   (layer 2 moves the hands layer 1 read) rather than a weighting bug. NOT established either way.
 - PutterSlot has no authored pose yet (§3.4 step 3 wants its own, measured on the putt address).

Active build profile restored to iOS-Full-GPS. Branch golfer_3d_test.
