IMPLEMENTER_BLOCKED

§9.8 is PART DONE and stopped short of its evidence. Cesar was watching the Game View live and
corrected the variant three times (huge / facing left / club backwards / blade to the sky); the
third club-mount attempt is still not right, and the Unity session has since degraded — it is
throwing repeated NullReferenceException in UIElements.UIR.RenderChainCommand, which is why every
run after ~15:17 stalled or entered a paused state and never wrote its invariants JSON.
The Editor needs a restart before any further capture is trustworthy.

DONE and verified statically:
 - MixamoChar_TPose.fbx imported Humanoid, Create From This Model; avatar valid + isHuman.
 - The four clips imported Humanoid with Avatar = Copy From Other Avatar -> Remy's, §5.1 root
   bake (rotation/Y/XZ Bake Into Pose, Based Upon Original; Loop Time only on Idle).
 - Scale corrected AT IMPORT: useFileScale must stay ON (the FBX carries 0.01; turning it off
   multiplied by 100 - measured 3.089 m -> 132.811 m). globalScale 0.42992 now gives
   foot->head 1.328 m, EXACTLY matching PfGolfer_Test.
 - Materials extracted (URP/Lit + real Remy diffuse textures) - he rendered white, then magenta
   mid-reimport, now textured.
 - PfGolfer_MixamoNative built: mesh + avatar swapped ONLY, forceGripPose=false (which is what
   gates the grip solve, the finger bake AND JoinLeadHandToShaft, so §9.8's "no grip / no finger
   / no forearm" needed no code change), club under the right-hand socket, model turned 180 deg
   so the body faces the ball (Cesar confirmed "model is ok").
 - AnimatorController_Golfer_MixamoNative: same states/transitions/cycleOffsets, motions repointed
   to the MixamoNative clips. DEVIATION FROM THE LITERAL SPEC, deliberate: "same controller"
   verbatim would have played the Y-Bot clips retargeted onto Remy, which is precisely the
   retargeting §9.8 exists to eliminate - the experiment would have tested nothing.
 - GolferTestBootstrap.ResourcePathOverride + a "Verify Mixamo-native on Hole 06" menu item, so
   both prefabs reach the hole down the IDENTICAL path.
 - Foot-slide instrument added to the harness. QUATERNIUS baseline measured: left 0.4770 m,
   right 0.4552 m, WORST 0.4770 m. stance.address.onGround 0.0000 m. 37 pass / 0 fail.

NOT DONE:
 - The club MOUNT orientation on Remy's hand. Three attempts (180 deg on the model, cancelling it
   on the sockets, then deriving it from an elbow->hand anatomical frame) all landed wrong. The
   cause is that the socket's local rotation was authored for Quaternius' hand_r axis convention
   and means nothing on mixamorig:RightHand. Note stance.address.clubReachesBall is 0.0000 m for
   BOTH prefabs - the club HEAD is in the right place; only its orientation is wrong.
 - The Mixamo foot-slide number, and the three side-by-side frames. Without the Mixamo slide
   figure there is no retarget-vs-clips conclusion, so NONE is written - inventing one from the
   Quaternius half would be fabricating the finding the whole section exists to produce.

Active build profile is still iOS-Full-Golfer (the define must be ON to run the harness). It is
NOT restored to iOS-Full-GPS because this is not the final commit.
