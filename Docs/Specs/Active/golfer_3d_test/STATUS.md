IMPLEMENTER_BLOCKED

§9 as amended is complete EXCEPT §9.8, which is blocked on assets that were never dropped:
Assets/Art/3D/Characters/_Test/MixamoNative/ does not exist (a project-wide search for
*MixamoNative* returns nothing), so there is no character T-pose and no "With Skin" clips to
import, nothing to duplicate PfGolfer_Test against, no side-by-side to capture and no foot-slide
numbers to measure. Per Rule 19 ("surface, don't rebuild") nothing was substituted.

Everything else is done and verified with the define genuinely OFF (profile now iOS-Full-GPS):
 §9.1 Eyebrows dropped, budget.tris 15,632 -> 14,648      (14f63e26e)
 §9.2 kept — keep-conditions MET: define-off path byte-identical AND proven absent at runtime
      (OnShotResolvedImmediate / GolferTestBootstrap.Boot do not exist under GPS); tests green
 §9.3 / §9.4 cancelled — no grip, fingertip or visual tuning done
 §9.5 one line in the report (§10.4)
 §9.6 EditMode sweep with the define OFF: 2711/2718 pass; GolferTestBuildGateTests 5/5, now
      exercising the EXCLUSION branch. All 4 failures accounted for, none mine.
 §9.7 maintenance.lock does not exist; there are no uncommitted control-scheme edits to sweep
 profile restored to iOS-Full-GPS

NOT set to READY_FOR_SELF_REVIEW deliberately: §9.8 is a mandated deliverable that cannot be
produced, and sending a reviewer to check work that does not exist wastes a gate round. Drop the
MixamoNative assets and I will finish §9.8, or say the word and I will flip this to
READY_FOR_SELF_REVIEW for the parts that are done.

Also still outstanding: SPEC.md has no §9 (147 lines, ends at §8) — all three close-out iterations
were implemented from chat text, and §8 still lists §9.2's work as out of scope.
