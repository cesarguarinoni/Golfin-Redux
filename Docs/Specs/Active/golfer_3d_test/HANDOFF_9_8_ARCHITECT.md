# §9.8 Mixamo-native check — handoff to Architect

**From:** PC session (Claude Code), 2026-09-07
**Commit:** `4e48c8632` + this document
**STATUS.md:** `IMPLEMENTER_BLOCKED`
**Verdict:** the pipeline half of §9.8 is done and verified; **the evidence half is not**, and the
retarget-vs-clips conclusion is therefore deliberately **unwritten**.

Cesar stopped the work: *"you are not equipped to fix this"*. He is right — the club-mount problem
below is an authoring judgement made in seconds by eye and repeatedly failed by me in minutes by
inference. Five attempts, five corrections from him.

---

## 1. What is done and verified

| step | state | evidence |
|---|---|---|
| `MixamoChar_TPose.fbx` import | DONE | Humanoid, Create From This Model; avatar valid, `isHuman=True`; 7 skinned meshes, 35,196 tris |
| Four clips import | DONE | Humanoid, **Avatar = Copy From Other Avatar → Remy's**; §5.1 root bake (rotation / Y / XZ Bake Into Pose, Based Upon Original); Loop Time only on `ANIM_Idle`; lengths 5.867 / 3.833 / 11.800 / 8.333 s |
| Scale | DONE | see §2 — foot→head **1.328 m, identical to Quaternius** |
| Materials | DONE | extracted to URP/Lit with Remy's real diffuse textures |
| `PfGolfer_MixamoNative` | DONE | mesh + avatar swapped only; club under the right-hand socket |
| "no grip / no finger / no forearm" | DONE | `forceGripPose=false` — see §3 |
| Native controller | DONE, with a flagged deviation | see §4 |
| Both prefabs down one path | DONE | see §5 |
| Foot-slide instrument | DONE | Quaternius baseline measured |
| **Mixamo foot-slide + 3 side-by-side frames** | **NOT DONE** | see §6, §7 |
| **Conclusion** | **NOT WRITTEN** | deliberately — see §8 |

## 2. Scale: the one genuinely useful import finding

Remy arrived **2.33× oversized** — foot→head 3.089 m against Quaternius' 1.328 m.

**`useFileScale` must stay ON.** The FBX carries a 0.01 file scale; turning it off does not shrink
the model, it multiplies by 100. Measured, not assumed: 3.089 m → **132.811 m**. The correction
belongs in `globalScale`, because the final scale is `fileScale × globalScale`.

Applied to all five FBXs: `useFileScale = true`, `globalScale = 0.42992` (= 1.328 / 3.089).
Result: foot→head **1.328 m on both prefabs**, hips 0.899 vs 0.980 (different bodies, expected).

This is worth carrying into the roster-model spec — it will bite on every Mixamo import.

## 3. The three prohibitions cost zero code

§9.8 says no grip solver, no finger bake, no forearm aim. All three live inside
`GolferPresenter.ApplyGripPose`, which early-returns on `if (!forceGripPose) return;`. Setting the
serialized `forceGripPose = false` on the variant satisfies all three — including
`JoinLeadHandToShaft`, which is the forearm aim. No code branch, no `#if`, no per-prefab special
case.

## 4. Deliberate deviation: the controller was duplicated

`AnimatorController_Golfer_MixamoNative` — same states, transitions and `cycleOffset`s, with the
motions repointed at the `MixamoNative/` clips.

**Why this is not a violation of "same controller".** Verbatim, the same controller asset plays the
**Y-Bot clips from `_Test/Mixamo/`**, retargeted by Unity onto Remy's avatar. That is precisely the
retargeting §9.8 exists to eliminate — the experiment would have measured nothing and would have
looked like it worked. Verified by asset path afterwards:

```
AnimatorController_Golfer              -> _Test/Mixamo/ANIM_Golf_Drive.fbx      (Y Bot)
AnimatorController_Golfer_MixamoNative -> _Test/MixamoNative/ANIM_Golf_Drive.fbx (Remy)
```

If the Architect intended the literal reading, this needs reverting — but then §9.8 cannot answer
its own question.

## 5. Both golfers reach the hole down one path

`GolferTestBootstrap.ResourcePathOverride` (already inside `#if GOLFIN_GOLFER_TEST`) plus a
`GOLFIN > Golfer Test > Verify Mixamo-native on Hole 06 (§9.8)` menu item. Same bootstrap, same
`GameSession.OnRoundStarted`, same `PlaceAtBall`, same harness — so the two frames differ only by
the prefab, which is the whole point of a comparison.

## 6. The club mount — where this stopped, and the data

The club's **position** is right on both: `stance.address.clubReachesBall = 0.0000 m` for
Quaternius **and** for the Mixamo variant. Only the **orientation** is wrong.

Five attempts, each corrected by Cesar on sight:

| # | what I did | he saw |
|---|---|---|
| 1 | straight duplicate | "huge and facing the wrong way" |
| 2 | scale fix + 180° on the model | "still looking left"; then "model is ok, club backwards" |
| 3 | cancelled that 180° on the sockets only | blade to the sky |
| 4 | derived from an elbow→hand frame | still wrong — that frame leaves **roll about the forearm free**, and roll is exactly what decides blade-at-ground vs blade-at-sky |
| 5 | roll pinned with the finger axis (`RightMiddleProximal`) | handle top-**right**, blade bottom-**left** — mirrored from the wanted "handle top left, blade bottom right" |
| 6 | 180° yaw in root space | went end-over-end instead of mirroring; **reverted** |

**The state left in the repo is attempt 5**, and it is the defensible one, because it is the only
one that is *measurably* correct rather than eyeballed:

```
shaft direction expressed in the hand frame
  Quaternius : (-0.076, -0.645, 0.760)
  Mixamo     : (-0.076, -0.645, 0.760)   ← identical
```

**So the mount relative to the hand is now provably the same on both rigs.** Any remaining
difference in how the club sits in the world comes from the **hand's pose at address**, i.e. from
the clip — Remy's own Mixamo golf clip rolls the wrist differently from the Y-Bot clip.

That is not a detail to guess at; it may itself be part of the §9.8 answer. **Architect decision
needed:** is the correct target (a) match the club's world orientation to the Quaternius render, or
(b) accept the clip's own wrist and report the difference as a finding?

Cheapest deterministic route if (a): log the shaft direction in **golfer-root space** at address
from the harness, run both prefabs once each, and bake `FromToRotation(mixamoDir, quaterniusDir)`
into the socket. Two runs, no eyeballing. I did not do it because Cesar stopped the work, and
because it presumes (a) is the goal.

## 7. Tooling findings that cost most of the session

- **Console "Error Pause" was ON.** Every Mixamo run paused partway and never wrote its invariants
  JSON, because the grip assertions log exceptions — they measure Quaternius bone names
  (`middle_02_r` …) that do not exist on `mixamorig:*`, so they read ~70–78 m and throw. I spent a
  long time waiting on runs that were sitting paused. Cesar spotted it. Now cleared via
  `ConsoleWindow.SetConsoleErrorPause(false)`.
  **Follow-up worth doing:** the harness's grip block should skip (or report N/A) when the rig has
  no Quaternius-named finger bones, instead of logging 78 m failures.
- **`Launch` refuses with "already in play mode"** and returns silently; twice I waited on a run
  that had never started. Worth making that a thrown error or an auto-stop.
- **Unity threw repeated `NullReferenceException` in `UIElements.UIR.RenderChainCommand`** after a
  material reimport during play; the Editor was restarted to clear it.

## 8. Why there is no conclusion

The numbers actually in hand:

| measure | Quaternius | Mixamo-native |
|---|---|---|
| `stance.address.onGround` | 0.0000 m | 0.0000 m |
| `stance.address.clubReachesBall` | 0.0000 m | 0.0000 m |
| foot slide during swing (worst) | **0.4770 m** (L 0.4770 / R 0.4552) | **not measured** |
| invariants | 37 pass / 0 fail | not completed |

The Quaternius foot slide — **0.477 m of travel on a planted foot** — is a real quantification of
the sliding-legs artefact §9 attributes to retargeting. But the whole hypothesis is *comparative*:
without the Mixamo figure there is nothing to compare it against. Writing "the clips are fine, the
retarget is at fault" from one half of a two-sided measurement would be inventing the finding the
section exists to produce, so it is not written.

## 9. Repo state

- Profile left on **`iOS-Full-Golfer`** (the define must be ON to run the harness). **Not** restored
  to `iOS-Full-GPS`, because this is not the final commit.
- Uncommitted and deliberately left: `Docs/Diag/baked-pivot/M0-regression-*.md` (regenerated by the
  EditMode suite itself) and untracked `Assets/Animations.meta` (predates this session).
- Play mode stopped; Editor left idle.

## 10. My play/pause/stop handling was wrong — the fix

Cesar had to stop Unity by hand. Three separate mistakes, all mine:

1. **I re-entered play mode while trying to unpause.** To resume a paused session I called
   `editor-application-set-state {isPaused: false, isPlaying: true}`. If play mode had actually
   ended in the meantime, `isPlaying: true` does not "keep playing" — it **starts a new play
   session**. That is why runs kept reappearing after I thought I had stopped them.
   **Fix: pass exactly one intent per call.** Unpause is `{isPaused: false}` and nothing else.
   Stop is `{isPlaying: false}` and nothing else.

2. **I treated the tool's return value as the post-condition.** `set-state {isPlaying:false}`
   returns `IsPlaying: true`, because leaving play mode takes a frame and a domain settle. I read
   that as "stop failed" and issued more calls, compounding mistake 1.
   **Fix: the setter is a request; poll `editor-application-get-state` until
   `IsPlaying == false && IsPlayingOrWillChangePlaymode == false` before doing anything else.**

3. **I polled on fixed sleeps instead of on state.** I sat on 150–300 s timers waiting for frames
   from runs that had never started — `Launch` had refused with "already in play mode" and returned
   silently. Cesar asked "what are you waiting for?" twice, correctly.
   **Fix: poll the actual condition (`IsPlaying`, then the artifact's mtime), never a bare timer;
   and treat "already in play mode" as a hard error, not a log line.**

Plus the environmental one in §7: **Console Error Pause was ON**, so every Mixamo run paused on its
first logged exception. Disable it at session start when driving the harness unattended:
`ConsoleWindow.SetConsoleErrorPause(false)`.

Sequence that should have been used throughout, and should be from now on:

```
1. set-state {isPlaying:false}
2. poll get-state until IsPlaying==false && IsPlayingOrWillChangePlaymode==false
3. launch the menu item
4. poll get-state until IsPlaying==true          (proves it actually started)
5. poll the artifact mtime for completion
   - if IsPaused goes true: set-state {isPaused:false} ONLY, and note why it paused
```
